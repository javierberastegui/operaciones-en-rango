using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class LokkyBasketScalperV3 : Robot
    {
        [Parameter("Bot label", DefaultValue = "LokkyBasketScalperV3", Group = "General")]
        public string BotLabel { get; set; }

        [Parameter("Maximum cycles (0 = unlimited)", DefaultValue = 0, MinValue = 0, MaxValue = 1000000, Group = "General")]
        public int MaximumCycles { get; set; }

        [Parameter("Max orders per basket", DefaultValue = 10, MinValue = 1, MaxValue = 20, Group = "Orders")]
        public int MaxOrdersPerBasket { get; set; }

        [Parameter("Lots per order", DefaultValue = 0.01, MinValue = 0.01, Group = "Orders")]
        public double LotsPerOrder { get; set; }

        [Parameter("Max total lots", DefaultValue = 0.10, MinValue = 0.01, MaxValue = 100.0, Group = "Orders")]
        public double MaxTotalLots { get; set; }

        [Parameter("Cooldown seconds", DefaultValue = 10, MinValue = 0, MaxValue = 3600, Group = "Orders")]
        public int CooldownSeconds { get; set; }

        [Parameter("Loss cooldown seconds", DefaultValue = 30, MinValue = 0, MaxValue = 3600, Group = "Orders")]
        public int LossCooldownSeconds { get; set; }

        [Parameter("Min seconds between entries", DefaultValue = 1.0, MinValue = 0.0, MaxValue = 300.0, Group = "Orders")]
        public double MinSecondsBetweenEntries { get; set; }

        [Parameter("Fast EMA", DefaultValue = 8, MinValue = 2, MaxValue = 100, Group = "Direction")]
        public int FastEmaPeriod { get; set; }

        [Parameter("Slow EMA", DefaultValue = 21, MinValue = 3, MaxValue = 300, Group = "Direction")]
        public int SlowEmaPeriod { get; set; }

        [Parameter("Momentum lookback bars", DefaultValue = 3, MinValue = 1, MaxValue = 20, Group = "Direction")]
        public int MomentumLookbackBars { get; set; }

        [Parameter("Min EMA gap / ATR", DefaultValue = 0.05, MinValue = 0.0, MaxValue = 2.0, Group = "Direction")]
        public double MinEmaGapAtrFraction { get; set; }

        [Parameter("ATR period", DefaultValue = 14, MinValue = 2, MaxValue = 100, Group = "Dynamic spacing")]
        public int AtrPeriod { get; set; }

        [Parameter("ATR spacing multiplier", DefaultValue = 0.25, MinValue = 0.01, MaxValue = 5.0, Group = "Dynamic spacing")]
        public double AtrSpacingMultiplier { get; set; }

        [Parameter("Min price spacing", DefaultValue = 0.20, MinValue = 0.01, Group = "Dynamic spacing")]
        public double MinPriceSpacing { get; set; }

        [Parameter("Max price spacing", DefaultValue = 0.80, MinValue = 0.01, Group = "Dynamic spacing")]
        public double MaxPriceSpacing { get; set; }

        [Parameter("Require basket >= 0 to add", DefaultValue = true, Group = "Dynamic spacing")]
        public bool RequireNonNegativeBasketToAdd { get; set; }

        [Parameter("Target 1-3 orders %", DefaultValue = 0.04, MinValue = 0.01, MaxValue = 10.0, Group = "Dynamic target")]
        public double TargetSmallPercent { get; set; }

        [Parameter("Target 4-6 orders %", DefaultValue = 0.06, MinValue = 0.01, MaxValue = 10.0, Group = "Dynamic target")]
        public double TargetMediumPercent { get; set; }

        [Parameter("Target 7+ orders %", DefaultValue = 0.08, MinValue = 0.01, MaxValue = 10.0, Group = "Dynamic target")]
        public double TargetLargePercent { get; set; }

        [Parameter("Basket loss % balance", DefaultValue = 0.25, MinValue = 0.01, MaxValue = 50.0, Group = "Risk")]
        public double BasketLossPercent { get; set; }

        [Parameter("Max basket loss money", DefaultValue = 25.0, MinValue = 0.0, MaxValue = 100000.0, Group = "Risk")]
        public double MaxBasketLossMoney { get; set; }

        [Parameter("Hard drawdown %", DefaultValue = 3.0, MinValue = 0.1, MaxValue = 90.0, Group = "Risk")]
        public double HardDrawdownPercent { get; set; }

        [Parameter("Time stop seconds", DefaultValue = 120, MinValue = 10, MaxValue = 86400, Group = "Risk")]
        public int TimeStopSeconds { get; set; }

        [Parameter("Max spread price", DefaultValue = 0.50, MinValue = 0.01, MaxValue = 1000.0, Group = "Risk")]
        public double MaxSpreadPrice { get; set; }

        [Parameter("Close positions on stop", DefaultValue = true, Group = "Risk")]
        public bool ClosePositionsOnStop { get; set; }

        [Parameter("Adaptive mode", DefaultValue = true, Group = "Adaptive")]
        public bool AdaptiveMode { get; set; }

        [Parameter("Adaptive window", DefaultValue = 20, MinValue = 5, MaxValue = 100, Group = "Adaptive")]
        public int AdaptiveWindow { get; set; }

        private string _label;
        private Bars _m1Bars;
        private MovingAverage _fastEma;
        private MovingAverage _slowEma;
        private AverageTrueRange _atr;

        private TradeType _direction;
        private DateTime _nextCycleTime;
        private DateTime _nextEntryTime;
        private DateTime _cycleStartTime;
        private double _startEquity;
        private double _cycleStartBalance;
        private double _lastEntryPrice;
        private int _ordersOpenedThisCycle;
        private int _cycles;
        private bool _isClosing;
        private bool _hardLocked;

        private readonly Queue<double> _recentBasketResults = new Queue<double>();

        protected override void OnStart()
        {
            _label = $"{BotLabel}-{SymbolName}";
            _startEquity = Account.Equity;
            _nextCycleTime = Server.Time;
            _nextEntryTime = Server.Time;

            _m1Bars = MarketData.GetBars(TimeFrame.Minute, SymbolName);
            _fastEma = Indicators.MovingAverage(_m1Bars.ClosePrices, FastEmaPeriod, MovingAverageType.Exponential);
            _slowEma = Indicators.MovingAverage(_m1Bars.ClosePrices, SlowEmaPeriod, MovingAverageType.Exponential);
            _atr = Indicators.AverageTrueRange(_m1Bars, AtrPeriod, MovingAverageType.Exponential);

            Print("=== LOKKY BASKET SCALPER V3 STARTED ===");
            Print("Symbol: {0} | Balance: {1:F2} | Equity: {2:F2}", SymbolName, Account.Balance, Account.Equity);
            Print("V3: direction filter + spread filter + ATR spacing + pyramiding only on winners + dynamic targets + time stop + exposure cap + adaptive mode.");

            TryStartNewCycle("startup");
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
                    TryStartNewCycle("next cycle");

                return;
            }

            var basketNetProfit = positions.Sum(p => p.NetProfit);
            var targetMoney = GetCurrentTargetMoney(positions.Length);
            var lossLimitMoney = GetCurrentLossLimitMoney();

            if (basketNetProfit >= targetMoney)
            {
                Print("Basket target reached: {0:F2} >= {1:F2} | Orders: {2}", basketNetProfit, targetMoney, positions.Length);
                CloseBasket(basketNetProfit, "TARGET");
                return;
            }

            if (basketNetProfit <= -lossLimitMoney)
            {
                Print("Basket loss limit reached: {0:F2} <= -{1:F2}", basketNetProfit, lossLimitMoney);
                CloseBasket(basketNetProfit, "LOSS LIMIT");
                return;
            }

            if ((Server.Time - _cycleStartTime).TotalSeconds >= TimeStopSeconds)
            {
                Print("Time stop reached after {0}s | Basket P/L: {1:F2}", TimeStopSeconds, basketNetProfit);
                CloseBasket(basketNetProfit, "TIME STOP");
                return;
            }

            TryAddOrder(positions, basketNetProfit);
        }

        protected override void OnStop()
        {
            if (ClosePositionsOnStop)
                CloseAllBotPositions();

            Print("Lokky Basket Scalper V3 stopped.");
        }

        private void TryStartNewCycle(string reason)
        {
            if (_hardLocked || _isClosing || GetBotPositions().Length > 0)
                return;

            if (!SpreadIsAcceptable())
            {
                _nextCycleTime = Server.Time.AddSeconds(2);
                return;
            }

            var signal = GetMarketDirection();
            if (!signal.HasValue)
            {
                _nextCycleTime = Server.Time.AddSeconds(1);
                return;
            }

            _direction = signal.Value;
            _cycleStartBalance = Account.Balance;
            _cycleStartTime = Server.Time;
            _ordersOpenedThisCycle = 0;
            _lastEntryPrice = 0;
            _nextEntryTime = Server.Time;

            if (OpenOneOrder(true))
            {
                _cycles++;
                Print("Cycle {0} START | {1} | Max orders: {2} | Lots/order: {3:F2} | Max total lots: {4:F2} | Spacing now: {5:F2} | Target now: {6:F2} | Loss limit: {7:F2} | Reason: {8}",
                    _cycles, _direction, MaxOrdersPerBasket, LotsPerOrder, MaxTotalLots, GetDynamicSpacing(), GetCurrentTargetMoney(1), GetCurrentLossLimitMoney(), reason);
            }
            else
            {
                _nextCycleTime = Server.Time.AddSeconds(5);
            }
        }

        private void TryAddOrder(Position[] positions, double basketNetProfit)
        {
            if (_isClosing || _ordersOpenedThisCycle <= 0 || _ordersOpenedThisCycle >= MaxOrdersPerBasket)
                return;

            if (Server.Time < _nextEntryTime || _lastEntryPrice <= 0)
                return;

            if (!SpreadIsAcceptable())
                return;

            if (RequireNonNegativeBasketToAdd && basketNetProfit < 0)
                return;

            var signal = GetMarketDirection();
            if (!signal.HasValue || signal.Value != _direction)
                return;

            if (!CanAddVolume())
                return;

            var spacing = GetDynamicSpacing();
            var closeSidePrice = _direction == TradeType.Buy ? Symbol.Bid : Symbol.Ask;
            var favorableMove = _direction == TradeType.Buy
                ? closeSidePrice - _lastEntryPrice
                : _lastEntryPrice - closeSidePrice;

            if (favorableMove < spacing)
                return;

            OpenOneOrder(false);
        }

        private bool OpenOneOrder(bool firstOrder)
        {
            var desiredVolume = Symbol.NormalizeVolumeInUnits(Symbol.QuantityToVolumeInUnits(LotsPerOrder), RoundingMode.Down);
            var maxTotalVolume = Symbol.NormalizeVolumeInUnits(Symbol.QuantityToVolumeInUnits(MaxTotalLots), RoundingMode.Down);
            var currentVolume = GetBotPositions().Sum(p => p.VolumeInUnits);
            var remainingVolume = Math.Max(0, maxTotalVolume - currentVolume);
            var volume = Math.Min(desiredVolume, remainingVolume);
            volume = Symbol.NormalizeVolumeInUnits(volume, RoundingMode.Down);

            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("Exposure cap reached. No more orders will be added this cycle.");
                return false;
            }

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

            Print("Order {0}/{1} OPEN | {2} | Volume units: {3:F0} | Entry: {4:F2} | Spacing next: {5:F2} | {6}",
                _ordersOpenedThisCycle, MaxOrdersPerBasket, _direction, volume, _lastEntryPrice, GetDynamicSpacing(),
                firstOrder ? "FIRST" : "PYRAMID ADD");

            return true;
        }

        private TradeType? GetMarketDirection()
        {
            var minimumBars = Math.Max(SlowEmaPeriod + 5, MomentumLookbackBars + 5);
            if (_m1Bars == null || _m1Bars.Count < minimumBars)
                return null;

            var index = _m1Bars.Count - 2;
            var pastIndex = index - MomentumLookbackBars;
            if (pastIndex < 0)
                return null;

            var close = _m1Bars.ClosePrices[index];
            var pastClose = _m1Bars.ClosePrices[pastIndex];
            var fast = _fastEma.Result[index];
            var slow = _slowEma.Result[index];
            var atr = _atr.Result[index];

            if (atr <= 0)
                return null;

            if (Math.Abs(fast - slow) < atr * MinEmaGapAtrFraction)
                return null;

            if (fast > slow && close > fast && close > pastClose)
                return TradeType.Buy;

            if (fast < slow && close < fast && close < pastClose)
                return TradeType.Sell;

            return null;
        }

        private double GetDynamicSpacing()
        {
            var index = _m1Bars.Count - 2;
            var atr = index >= 0 ? _atr.Result[index] : MinPriceSpacing;
            var spacing = atr * AtrSpacingMultiplier * GetAdaptiveSpacingFactor();
            return Clamp(spacing, MinPriceSpacing, Math.Max(MinPriceSpacing, MaxPriceSpacing));
        }

        private double GetCurrentTargetMoney(int openOrders)
        {
            double targetPercent;

            if (openOrders <= 3)
                targetPercent = TargetSmallPercent;
            else if (openOrders <= 6)
                targetPercent = TargetMediumPercent;
            else
                targetPercent = TargetLargePercent;

            targetPercent *= GetAdaptiveTargetFactor();
            return Math.Max(0.01, _cycleStartBalance * targetPercent / 100.0);
        }

        private double GetCurrentLossLimitMoney()
        {
            var percentLimit = Math.Max(0.01, _cycleStartBalance * BasketLossPercent / 100.0);
            if (MaxBasketLossMoney <= 0)
                return percentLimit;

            return Math.Min(percentLimit, MaxBasketLossMoney);
        }

        private bool SpreadIsAcceptable()
        {
            var spread = Symbol.Ask - Symbol.Bid;
            if (spread <= MaxSpreadPrice)
                return true;

            return false;
        }

        private bool CanAddVolume()
        {
            var desiredVolume = Symbol.NormalizeVolumeInUnits(Symbol.QuantityToVolumeInUnits(LotsPerOrder), RoundingMode.Down);
            var maxVolume = Symbol.NormalizeVolumeInUnits(Symbol.QuantityToVolumeInUnits(MaxTotalLots), RoundingMode.Down);
            var currentVolume = GetBotPositions().Sum(p => p.VolumeInUnits);
            return currentVolume + Symbol.VolumeInUnitsMin <= maxVolume && desiredVolume >= Symbol.VolumeInUnitsMin;
        }

        private void CloseBasket(double basketNetProfit, string reason)
        {
            if (_isClosing)
                return;

            _isClosing = true;
            RecordBasketResult(basketNetProfit);
            CloseAllBotPositions();

            var wasLoss = basketNetProfit < 0;
            var delay = wasLoss ? Math.Max(CooldownSeconds, LossCooldownSeconds) : CooldownSeconds;
            _nextCycleTime = Server.Time.AddSeconds(delay);
            _ordersOpenedThisCycle = 0;
            _lastEntryPrice = 0;

            Print("Basket CLOSED | {0} | P/L: {1:F2} | Next cycle in {2}s | Recent win rate: {3:P0} | Adaptive target x{4:F2} | spacing x{5:F2}",
                reason, basketNetProfit, delay, GetRecentWinRate(), GetAdaptiveTargetFactor(), GetAdaptiveSpacingFactor());
        }

        private void RecordBasketResult(double result)
        {
            _recentBasketResults.Enqueue(result);
            while (_recentBasketResults.Count > AdaptiveWindow)
                _recentBasketResults.Dequeue();
        }

        private double GetRecentWinRate()
        {
            if (_recentBasketResults.Count == 0)
                return 0;

            return _recentBasketResults.Count(x => x > 0) / (double)_recentBasketResults.Count;
        }

        private double GetAdaptiveTargetFactor()
        {
            if (!AdaptiveMode || _recentBasketResults.Count < 5)
                return 1.0;

            var winRate = GetRecentWinRate();
            var average = _recentBasketResults.Average();

            if (winRate >= 0.80 && average > 0)
                return 1.05;

            if (winRate < 0.60 || average <= 0)
                return 0.85;

            return 1.0;
        }

        private double GetAdaptiveSpacingFactor()
        {
            if (!AdaptiveMode || _recentBasketResults.Count < 5)
                return 1.0;

            var winRate = GetRecentWinRate();
            var average = _recentBasketResults.Average();

            if (winRate >= 0.80 && average > 0)
                return 0.95;

            if (winRate < 0.60 || average <= 0)
                return 1.15;

            return 1.0;
        }

        private void CloseAllBotPositions()
        {
            foreach (var position in GetBotPositions())
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

        private double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
