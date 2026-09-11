using System;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class LokkyBasketScalperV2 : Robot
    {
        [Parameter("Bot label", DefaultValue = "LokkyBasketScalperV2", Group = "General")]
        public string BotLabel { get; set; }

        [Parameter("Start direction", DefaultValue = TradeType.Buy, Group = "General")]
        public TradeType StartDirection { get; set; }

        [Parameter("Alternate BUY/SELL", DefaultValue = true, Group = "General")]
        public bool AlternateDirection { get; set; }

        [Parameter("Maximum cycles (0 = unlimited)", DefaultValue = 0, MinValue = 0, MaxValue = 1000000, Group = "General")]
        public int MaximumCycles { get; set; }

        [Parameter("Max orders per basket", DefaultValue = 10, MinValue = 1, MaxValue = 20, Group = "Orders")]
        public int MaxOrdersPerBasket { get; set; }

        [Parameter("Lots per order", DefaultValue = 0.01, MinValue = 0.01, Group = "Orders")]
        public double LotsPerOrder { get; set; }

        [Parameter("Cooldown seconds", DefaultValue = 10, MinValue = 0, MaxValue = 3600, Group = "Orders")]
        public int CooldownSeconds { get; set; }

        [Parameter("Loss cooldown seconds", DefaultValue = 30, MinValue = 0, MaxValue = 3600, Group = "Orders")]
        public int LossCooldownSeconds { get; set; }

        [Parameter("Price spacing", DefaultValue = 0.25, MinValue = 0.01, Group = "Staggering")]
        public double PriceSpacing { get; set; }

        [Parameter("Min seconds between entries", DefaultValue = 1.0, MinValue = 0.0, MaxValue = 300.0, Group = "Staggering")]
        public double MinSecondsBetweenEntries { get; set; }

        [Parameter("Basket profit % balance", DefaultValue = 0.08, MinValue = 0.01, MaxValue = 10.0, Group = "Basket")]
        public double BasketProfitPercent { get; set; }

        [Parameter("Basket loss % balance", DefaultValue = 0.25, MinValue = 0.05, MaxValue = 50.0, Group = "Basket")]
        public double BasketLossPercent { get; set; }

        [Parameter("Hard drawdown %", DefaultValue = 3.0, MinValue = 0.1, MaxValue = 90.0, Group = "Safety")]
        public double HardDrawdownPercent { get; set; }

        [Parameter("Close positions on stop", DefaultValue = true, Group = "Safety")]
        public bool ClosePositionsOnStop { get; set; }

        [Parameter("Use lot compounding", DefaultValue = false, Group = "Compounding")]
        public bool UseLotCompounding { get; set; }

        [Parameter("Balance step %", DefaultValue = 100.0, MinValue = 10.0, MaxValue = 1000.0, Group = "Compounding")]
        public double BalanceStepPercent { get; set; }

        [Parameter("Lot multiplier", DefaultValue = 2.0, MinValue = 1.0, MaxValue = 10.0, Group = "Compounding")]
        public double LotMultiplier { get; set; }

        [Parameter("Maximum lots/order", DefaultValue = 1.0, MinValue = 0.01, MaxValue = 100.0, Group = "Compounding")]
        public double MaximumLotsPerOrder { get; set; }

        private string _label;
        private TradeType _direction;
        private DateTime _nextCycleTime;
        private DateTime _nextEntryTime;
        private double _startBalance;
        private double _startEquity;
        private double _cycleStartBalance;
        private double _basketTargetMoney;
        private double _basketLossMoney;
        private double _lastEntryPrice;
        private int _ordersOpenedThisCycle;
        private int _cycles;
        private bool _isClosing;
        private bool _hardLocked;

        protected override void OnStart()
        {
            _label = $"{BotLabel}-{SymbolName}";
            _direction = StartDirection;
            _startBalance = Account.Balance;
            _startEquity = Account.Equity;
            _nextCycleTime = Server.Time;
            _nextEntryTime = Server.Time;

            Print("=== LOKKY BASKET SCALPER V2 STARTED ===");
            Print("Symbol: {0} | Balance: {1:F2} | Equity: {2:F2}", SymbolName, Account.Balance, Account.Equity);
            Print("First order opens immediately. Extra orders are added only after a favorable price move and the minimum time spacing.");

            StartNewCycle("startup");
        }

        protected override void OnTick()
        {
            if (_hardLocked)
                return;

            if (HitHardDrawdown())
            {
                _hardLocked = true;
                Print("HARD DRAWDOWN reached. Closing bot positions and stopping.");
                CloseAllBotPositions();
                Stop();
                return;
            }

            var positions = GetBotPositions();

            if (positions.Length == 0)
            {
                if (_isClosing)
                    _isClosing = false;

                if (MaximumCycles > 0 && _cycles >= MaximumCycles)
                {
                    Print("Maximum cycles reached. Stopping.");
                    Stop();
                    return;
                }

                if (Server.Time >= _nextCycleTime)
                    StartNewCycle("next cycle");

                return;
            }

            var basketNetProfit = positions.Sum(p => p.NetProfit);

            if (basketNetProfit >= _basketTargetMoney)
            {
                Print("Basket target reached: {0:F2} >= {1:F2}", basketNetProfit, _basketTargetMoney);
                CloseBasket(false);
                return;
            }

            if (basketNetProfit <= -_basketLossMoney)
            {
                Print("Basket loss limit reached: {0:F2} <= -{1:F2}", basketNetProfit, _basketLossMoney);
                CloseBasket(true);
                return;
            }

            TryAddOrder();
        }

        protected override void OnStop()
        {
            if (ClosePositionsOnStop)
                CloseAllBotPositions();

            Print("Lokky Basket Scalper V2 stopped.");
        }

        private void StartNewCycle(string reason)
        {
            if (_hardLocked || _isClosing)
                return;

            if (GetBotPositions().Length > 0)
                return;

            _cycleStartBalance = Account.Balance;
            _basketTargetMoney = Math.Max(0.01, _cycleStartBalance * BasketProfitPercent / 100.0);
            _basketLossMoney = Math.Max(0.01, _cycleStartBalance * BasketLossPercent / 100.0);
            _ordersOpenedThisCycle = 0;
            _lastEntryPrice = 0;
            _nextEntryTime = Server.Time;

            var opened = OpenOneOrder(true);

            if (opened)
            {
                _cycles++;
                Print("Cycle {0} START | {1} | Max orders: {2} | Lots/order: {3:F2} | Price spacing: {4:F2} | Min entry delay: {5:F2}s | Target: {6:F2} | Loss limit: {7:F2} | Reason: {8}",
                    _cycles, _direction, MaxOrdersPerBasket, CalculateLotsPerOrder(), PriceSpacing, MinSecondsBetweenEntries,
                    _basketTargetMoney, _basketLossMoney, reason);
            }
            else
            {
                Print("First order could not be opened. Retrying cycle in 10 seconds.");
                _nextCycleTime = Server.Time.AddSeconds(10);
            }
        }

        private void TryAddOrder()
        {
            if (_isClosing || _ordersOpenedThisCycle <= 0)
                return;

            if (_ordersOpenedThisCycle >= MaxOrdersPerBasket)
                return;

            if (Server.Time < _nextEntryTime)
                return;

            if (_lastEntryPrice <= 0)
                return;

            var currentEntryPrice = _direction == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            var favorableMove = _direction == TradeType.Buy
                ? currentEntryPrice - _lastEntryPrice
                : _lastEntryPrice - currentEntryPrice;

            if (favorableMove < PriceSpacing)
                return;

            OpenOneOrder(false);
        }

        private bool OpenOneOrder(bool firstOrder)
        {
            var lots = CalculateLotsPerOrder();
            var rawVolume = Symbol.QuantityToVolumeInUnits(lots);
            var volume = Symbol.NormalizeVolumeInUnits(rawVolume, RoundingMode.Down);

            if (volume < Symbol.VolumeInUnitsMin)
                volume = Symbol.VolumeInUnitsMin;
            if (volume > Symbol.VolumeInUnitsMax)
                volume = Symbol.VolumeInUnitsMax;

            var result = ExecuteMarketOrder(_direction, SymbolName, volume, _label);

            if (!result.IsSuccessful)
            {
                Print("Order failed: {0}", result.Error);
                _nextEntryTime = Server.Time.AddSeconds(Math.Max(1.0, MinSecondsBetweenEntries));
                return false;
            }

            _ordersOpenedThisCycle++;
            _lastEntryPrice = result.Position != null
                ? result.Position.EntryPrice
                : (_direction == TradeType.Buy ? Symbol.Ask : Symbol.Bid);
            _nextEntryTime = Server.Time.AddSeconds(MinSecondsBetweenEntries);

            Print("Order {0}/{1} OPEN | {2} | Lots: {3:F2} | Entry: {4:F2} | {5}",
                _ordersOpenedThisCycle, MaxOrdersPerBasket, _direction, lots, _lastEntryPrice,
                firstOrder ? "FIRST" : "PYRAMID ADD");

            return true;
        }

        private void CloseBasket(bool wasLoss)
        {
            if (_isClosing)
                return;

            _isClosing = true;
            CloseAllBotPositions();

            if (AlternateDirection)
                _direction = _direction == TradeType.Buy ? TradeType.Sell : TradeType.Buy;

            var delay = wasLoss ? Math.Max(CooldownSeconds, LossCooldownSeconds) : CooldownSeconds;
            _nextCycleTime = Server.Time.AddSeconds(delay);
            _ordersOpenedThisCycle = 0;
            _lastEntryPrice = 0;

            Print("Basket CLOSED | Result: {0} | Next direction: {1} | Next cycle in {2}s",
                wasLoss ? "LOSS" : "PROFIT", _direction, delay);
        }

        private void CloseAllBotPositions()
        {
            var positions = GetBotPositions();

            foreach (var position in positions)
            {
                var result = ClosePosition(position);
                if (!result.IsSuccessful)
                    Print("Could not close position {0}: {1}", position.Id, result.Error);
            }
        }

        private Position[] GetBotPositions()
        {
            return Positions
                .Where(p => p.SymbolName == SymbolName && p.Label == _label)
                .ToArray();
        }

        private bool HitHardDrawdown()
        {
            if (_startEquity <= 0 || HardDrawdownPercent <= 0)
                return false;

            var floor = _startEquity * (1.0 - HardDrawdownPercent / 100.0);
            return Account.Equity <= floor;
        }

        private double CalculateLotsPerOrder()
        {
            var lots = LotsPerOrder;

            if (UseLotCompounding && _startBalance > 0 && Account.Balance > _startBalance && BalanceStepPercent > 0)
            {
                var stepFactor = 1.0 + BalanceStepPercent / 100.0;
                var balanceRatio = Account.Balance / _startBalance;
                var steps = (int)Math.Floor(Math.Log(balanceRatio) / Math.Log(stepFactor));

                if (steps > 0)
                    lots *= Math.Pow(LotMultiplier, steps);
            }

            lots = Math.Min(lots, MaximumLotsPerOrder);
            return Math.Max(0.01, lots);
        }
    }
}
