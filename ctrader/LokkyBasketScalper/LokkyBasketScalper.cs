using System;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class LokkyBasketScalper : Robot
    {
        [Parameter("Bot label", DefaultValue = "LokkyBasketScalper", Group = "General")]
        public string BotLabel { get; set; }

        [Parameter("Start direction", DefaultValue = TradeType.Buy, Group = "General")]
        public TradeType StartDirection { get; set; }

        [Parameter("Alternate BUY/SELL", DefaultValue = true, Group = "General")]
        public bool AlternateDirection { get; set; }

        [Parameter("Orders per basket", DefaultValue = 3, MinValue = 1, MaxValue = 20, Group = "Orders")]
        public int OrdersPerBasket { get; set; }

        [Parameter("Lots per order", DefaultValue = 0.01, MinValue = 0.01, Group = "Orders")]
        public double LotsPerOrder { get; set; }

        [Parameter("Cooldown seconds", DefaultValue = 1, MinValue = 0, MaxValue = 300, Group = "Orders")]
        public int CooldownSeconds { get; set; }

        [Parameter("Loss cooldown seconds", DefaultValue = 5, MinValue = 0, MaxValue = 3600, Group = "Orders")]
        public int LossCooldownSeconds { get; set; }

        [Parameter("Basket profit % balance", DefaultValue = 0.10, MinValue = 0.01, MaxValue = 10.0, Group = "Basket")]
        public double BasketProfitPercent { get; set; }

        [Parameter("Basket loss % balance", DefaultValue = 0.50, MinValue = 0.05, MaxValue = 50.0, Group = "Basket")]
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

        [Parameter("Maximum cycles (0 = unlimited)", DefaultValue = 0, MinValue = 0, MaxValue = 1000000, Group = "General")]
        public int MaximumCycles { get; set; }

        private string _label;
        private TradeType _direction;
        private DateTime _nextCycleTime;
        private double _startBalance;
        private double _startEquity;
        private double _cycleStartBalance;
        private double _basketTargetMoney;
        private double _basketLossMoney;
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

            Print("=== LOKKY BASKET SCALPER STARTED ===");
            Print("Symbol: {0} | Balance: {1:F2} | Equity: {2:F2}", SymbolName, Account.Balance, Account.Equity);
            Print("Immediate mode: first basket is opened as soon as the cBot starts.");

            OpenBasket("startup");
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
                    OpenBasket("next cycle");

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
            }
        }

        protected override void OnStop()
        {
            if (ClosePositionsOnStop)
                CloseAllBotPositions();

            Print("Lokky Basket Scalper stopped.");
        }

        private void OpenBasket(string reason)
        {
            if (_hardLocked || _isClosing)
                return;

            if (GetBotPositions().Length > 0)
                return;

            var lots = CalculateLotsPerOrder();
            var rawVolume = Symbol.QuantityToVolumeInUnits(lots);
            var volume = Symbol.NormalizeVolumeInUnits(rawVolume, RoundingMode.Down);

            if (volume < Symbol.VolumeInUnitsMin)
                volume = Symbol.VolumeInUnitsMin;
            if (volume > Symbol.VolumeInUnitsMax)
                volume = Symbol.VolumeInUnitsMax;

            _cycleStartBalance = Account.Balance;
            _basketTargetMoney = Math.Max(0.01, _cycleStartBalance * BasketProfitPercent / 100.0);
            _basketLossMoney = Math.Max(0.01, _cycleStartBalance * BasketLossPercent / 100.0);

            var opened = 0;

            for (var i = 0; i < OrdersPerBasket; i++)
            {
                var result = ExecuteMarketOrder(_direction, SymbolName, volume, _label);

                if (result.IsSuccessful)
                {
                    opened++;
                }
                else
                {
                    Print("Order {0}/{1} failed: {2}", i + 1, OrdersPerBasket, result.Error);
                }
            }

            if (opened > 0)
            {
                _cycles++;
                Print("Cycle {0} OPEN | {1} | Orders: {2}/{3} | Lots/order: {4:F2} | Target: {5:F2} | Loss limit: {6:F2} | Reason: {7}",
                    _cycles, _direction, opened, OrdersPerBasket, lots, _basketTargetMoney, _basketLossMoney, reason);
            }
            else
            {
                Print("No orders could be opened. Retrying in 10 seconds.");
                _nextCycleTime = Server.Time.AddSeconds(10);
            }
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
