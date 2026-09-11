using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    public enum LokkyTradingMode
    {
        REAL_SAFE,
        DEMO_STRESS
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class LokkyBasketScalperV5 : Robot
    {
        [Parameter("Trading mode", DefaultValue = LokkyTradingMode.DEMO_STRESS, Group = "Mode")]
        public LokkyTradingMode TradingMode { get; set; }

        [Parameter("Bot label", DefaultValue = "LokkyBasketScalperV5", Group = "General")]
        public string BotLabel { get; set; }

        [Parameter("Maximum cycles (0 = unlimited)", DefaultValue = 0, MinValue = 0, MaxValue = 1000000, Group = "General")]
        public int MaximumCycles { get; set; }

        [Parameter("Max orders per basket", DefaultValue = 3, MinValue = 1, MaxValue = 10, Group = "Orders")]
        public int MaxOrdersPerBasket { get; set; }

        [Parameter("Max total lots", DefaultValue = 0.10, MinValue = 0.01, MaxValue = 100.0, Group = "Orders")]
        public double MaxTotalLots { get; set; }

        [Parameter("Cooldown profit seconds", DefaultValue = 2, MinValue = 0, MaxValue = 3600, Group = "REAL_SAFE")]
        public int CooldownSeconds { get; set; }

        [Parameter("Cooldown loss seconds", DefaultValue = 5, MinValue = 0, MaxValue = 3600, Group = "REAL_SAFE")]
        public int LossCooldownSeconds { get; set; }

        [Parameter("Demo cooldown profit sec", DefaultValue = 1.0, MinValue = 0.0, MaxValue = 60.0, Group = "DEMO_STRESS")]
        public double DemoProfitCooldownSeconds { get; set; }

        [Parameter("Demo cooldown loss sec", DefaultValue = 2.0, MinValue = 0.0, MaxValue = 60.0, Group = "DEMO_STRESS")]
        public double DemoLossCooldownSeconds { get; set; }

        [Parameter("Min seconds between entries", DefaultValue = 0.5, MinValue = 0.0, MaxValue = 300.0, Group = "Orders")]
        public double MinSecondsBetweenEntries { get; set; }

        [Parameter("Demo entry delay sec", DefaultValue = 0.20, MinValue = 0.0, MaxValue = 10.0, Group = "DEMO_STRESS")]
        public double DemoEntryDelaySeconds { get; set; }

        [Parameter("Risk per basket %", DefaultValue = 0.50, MinValue = 0.05, MaxValue = 5.0, Group = "Risk sizing")]
        public double RiskPerBasketPercent { get; set; }

        [Parameter("Risk stop ATR multiplier", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0, Group = "Risk sizing")]
        public double RiskStopAtrMultiplier { get; set; }

        [Parameter("Min sizing stop price", DefaultValue = 1.00, MinValue = 0.01, MaxValue = 1000.0, Group = "Risk sizing")]
        public double MinSizingStopPrice { get; set; }

        [Parameter("Max sizing stop price", DefaultValue = 3.00, MinValue = 0.01, MaxValue = 1000.0, Group = "Risk sizing")]
        public double MaxSizingStopPrice { get; set; }

        [Parameter("Target R multiple", DefaultValue = 0.50, MinValue = 0.10, MaxValue = 5.0, Group = "REAL_SAFE")]
        public double TargetRMultiple { get; set; }

        [Parameter("Demo target R", DefaultValue = 0.35, MinValue = 0.05, MaxValue = 5.0, Group = "DEMO_STRESS")]
        public double DemoTargetRMultiple { get; set; }

        [Parameter("Max daily loss %", DefaultValue = 1.50, MinValue = 0.10, MaxValue = 20.0, Group = "REAL_SAFE")]
        public double MaxDailyLossPercent { get; set; }

        [Parameter("Hard drawdown %", DefaultValue = 3.0, MinValue = 0.1, MaxValue = 90.0, Group = "REAL_SAFE")]
        public double HardDrawdownPercent { get; set; }

        [Parameter("Demo emergency DD %", DefaultValue = 20.0, MinValue = 1.0, MaxValue = 95.0, Group = "DEMO_STRESS")]
        public double DemoEmergencyDrawdownPercent { get; set; }

        [Parameter("Max consecutive losses", DefaultValue = 3, MinValue = 1, MaxValue = 20, Group = "REAL_SAFE")]
        public int MaxConsecutiveLosses { get; set; }

        [Parameter("Pause after loss streak sec", DefaultValue = 1800, MinValue = 0, MaxValue = 86400, Group = "REAL_SAFE")]
        public int LossStreakPauseSeconds { get; set; }

        [Parameter("Time stop seconds", DefaultValue = 45, MinValue = 5, MaxValue = 3600, Group = "REAL_SAFE")]
        public int TimeStopSeconds { get; set; }

        [Parameter("Demo time stop seconds", DefaultValue = 30, MinValue = 5, MaxValue = 3600, Group = "DEMO_STRESS")]
        public int DemoTimeStopSeconds { get; set; }

        [Parameter("Max spread price", DefaultValue = 0.50, MinValue = 0.01, MaxValue = 1000.0, Group = "Risk limits")]
        public double MaxSpreadPrice { get; set; }

        [Parameter("Close on signal flip", DefaultValue = true, Group = "Risk limits")]
        public bool CloseOnSignalFlip { get; set; }

        [Parameter("Close positions on stop", DefaultValue = true, Group = "Risk limits")]
        public bool ClosePositionsOnStop { get; set; }

        [Parameter("Fast EMA", DefaultValue = 8, MinValue = 2, MaxValue = 100, Group = "Signal")]
        public int FastEmaPeriod { get; set; }

        [Parameter("Slow EMA", DefaultValue = 21, MinValue = 3, MaxValue = 300, Group = "Signal")]
        public int SlowEmaPeriod { get; set; }

        [Parameter("ATR period", DefaultValue = 14, MinValue = 2, MaxValue = 100, Group = "Signal")]
        public int AtrPeriod { get; set; }

        [Parameter("Tick lookback", DefaultValue = 12, MinValue = 3, MaxValue = 100, Group = "Signal")]
        public int TickLookback { get; set; }

        [Parameter("Min tick move price", DefaultValue = 0.08, MinValue = 0.0, MaxValue = 100.0, Group = "Signal")]
        public double MinTickMovePrice { get; set; }

        [Parameter("Trend EMA gap / ATR", DefaultValue = 0.05, MinValue = 0.0, MaxValue = 2.0, Group = "Signal")]
        public double TrendEmaGapAtrFraction { get; set; }

        [Parameter("Enable range mode", DefaultValue = true, Group = "Signal")]
        public bool EnableRangeMode { get; set; }

        [Parameter("Range deviation / ATR", DefaultValue = 0.35, MinValue = 0.05, MaxValue = 3.0, Group = "Signal")]
        public double RangeDeviationAtrFraction { get; set; }

        [Parameter("Demo signal threshold factor", DefaultValue = 0.65, MinValue = 0.10, MaxValue = 1.0, Group = "DEMO_STRESS")]
        public double DemoSignalThresholdFactor { get; set; }

        [Parameter("ATR spacing multiplier", DefaultValue = 0.20, MinValue = 0.01, MaxValue = 5.0, Group = "Pyramiding")]
        public double AtrSpacingMultiplier { get; set; }

        [Parameter("Min price spacing", DefaultValue = 0.15, MinValue = 0.01, MaxValue = 100.0, Group = "Pyramiding")]
        public double MinPriceSpacing { get; set; }

        [Parameter("Max price spacing", DefaultValue = 0.50, MinValue = 0.01, MaxValue = 100.0, Group = "Pyramiding")]
        public double MaxPriceSpacing { get; set; }

        [Parameter("Demo spacing factor", DefaultValue = 0.75, MinValue = 0.10, MaxValue = 2.0, Group = "DEMO_STRESS")]
        public double DemoSpacingFactor { get; set; }

        [Parameter("Require basket >= 0 to add", DefaultValue = true, Group = "Pyramiding")]
        public bool RequireNonNegativeBasketToAdd { get; set; }

        private string _label;
        private Bars _m1Bars;
        private MovingAverage _fastEma;
        private MovingAverage _slowEma;
        private AverageTrueRange _atr;
        private readonly Queue<double> _tickPrices = new Queue<double>();

        private TradeType _direction;
        private DateTime _nextCycleTime;
        private DateTime _nextEntryTime;
        private DateTime _cycleStartTime;
        private DateTime _pauseUntil;
        private DateTime _sessionDate;

        private double _startEquity;
        private double _dayStartBalance;
        private double _lastEntryPrice;
        private double _plannedVolumePerOrder;
        private double _plannedTotalVolume;
        private double _plannedRiskMoney;
        private double _targetMoney;
        private double _lossLimitMoney;
        private double _sizingStopPrice;

        private int _plannedOrderCount;
        private int _ordersOpenedThisCycle;
        private int _cycles;
        private int _consecutiveLosses;
        private bool _isClosing;
        private bool _hardLocked;

        private int _statsBaskets;
        private int _statsWins;
        private int _statsLosses;
        private int _statsLossStreak;
        private int _statsMaxLossStreak;
        private double _statsGrossProfit;
        private double _statsGrossLoss;
        private double _statsNetProfit;
        private double _statsBestBasket = double.MinValue;
        private double _statsWorstBasket = double.MaxValue;
        private double _peakEquity;
        private double _maxDrawdownMoney;
        private double _maxDrawdownPercent;

        private bool IsDemoStress => TradingMode == LokkyTradingMode.DEMO_STRESS;

        protected override void OnStart()
        {
            _label = $"{BotLabel}-{SymbolName}";
            _startEquity = Account.Equity;
            _peakEquity = Account.Equity;
            _dayStartBalance = Account.Balance;
            _sessionDate = Server.Time.Date;
            _nextCycleTime = Server.Time;
            _nextEntryTime = Server.Time;
            _pauseUntil = Server.Time;

            _m1Bars = MarketData.GetBars(TimeFrame.Minute, SymbolName);
            _fastEma = Indicators.MovingAverage(_m1Bars.ClosePrices, FastEmaPeriod, MovingAverageType.Exponential);
            _slowEma = Indicators.MovingAverage(_m1Bars.ClosePrices, SlowEmaPeriod, MovingAverageType.Exponential);
            _atr = Indicators.AverageTrueRange(_m1Bars, AtrPeriod, MovingAverageType.Exponential);

            Print("=== LOKKY BASKET SCALPER V5 STARTED ===");
            Print("MODE: {0} | Risk/basket: {1:F2}% | Max total lots: {2:F2} | Max orders: {3}", TradingMode, RiskPerBasketPercent, MaxTotalLots, MaxOrdersPerBasket);

            if (IsDemoStress)
                Print("DEMO_STRESS: daily limit OFF | loss-streak pause OFF | cooldown {0:F1}/{1:F1}s | target {2:F2}R | time stop {3}s | emergency DD {4:F1}%",
                    DemoProfitCooldownSeconds, DemoLossCooldownSeconds, DemoTargetRMultiple, DemoTimeStopSeconds, DemoEmergencyDrawdownPercent);
            else
                Print("REAL_SAFE: daily loss {0:F2}% | loss streak {1} | target {2:F2}R | time stop {3}s | hard DD {4:F1}%",
                    MaxDailyLossPercent, MaxConsecutiveLosses, TargetRMultiple, TimeStopSeconds, HardDrawdownPercent);
        }

        protected override void OnTick()
        {
            UpdateTickBuffer();
            UpdateDrawdownStats();
            ResetDailyStateIfNeeded();

            if (_hardLocked)
                return;

            if (HitHardDrawdown())
            {
                EmergencyStop("EMERGENCY DRAWDOWN");
                return;
            }

            if (!IsDemoStress && HitDailyLossLimit())
            {
                EmergencyStop("DAILY LOSS LIMIT");
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

                if (!IsDemoStress && Server.Time < _pauseUntil)
                    return;

                if (Server.Time >= _nextCycleTime)
                    TryStartNewCycle();

                return;
            }

            var basketNetProfit = positions.Sum(p => p.NetProfit);

            if (basketNetProfit >= _targetMoney)
            {
                CloseBasket(basketNetProfit, "TARGET");
                return;
            }

            if (basketNetProfit <= -_lossLimitMoney)
            {
                CloseBasket(basketNetProfit, "RISK STOP");
                return;
            }

            if ((Server.Time - _cycleStartTime).TotalSeconds >= GetEffectiveTimeStopSeconds())
            {
                CloseBasket(basketNetProfit, "TIME STOP");
                return;
            }

            var signal = GetMarketDirection();
            if (CloseOnSignalFlip && basketNetProfit < 0 && signal.HasValue && signal.Value != _direction)
            {
                CloseBasket(basketNetProfit, "SIGNAL FLIP");
                return;
            }

            TryAddOrder(basketNetProfit);
        }

        protected override void OnStop()
        {
            if (ClosePositionsOnStop)
                CloseAllBotPositions();

            PrintStats("FINAL");
            Print("Lokky Basket Scalper V5 stopped.");
        }

        private void TryStartNewCycle()
        {
            if (_isClosing || GetBotPositions().Length > 0)
                return;

            if (!SpreadIsAcceptable())
            {
                _nextCycleTime = Server.Time.AddSeconds(IsDemoStress ? 0.20 : 1.0);
                return;
            }

            var signal = GetMarketDirection();
            if (!signal.HasValue)
            {
                _nextCycleTime = Server.Time.AddSeconds(IsDemoStress ? 0.10 : 0.50);
                return;
            }

            if (!PrepareRiskPlan())
            {
                _nextCycleTime = Server.Time.AddSeconds(IsDemoStress ? 0.50 : 2.0);
                return;
            }

            _direction = signal.Value;
            _cycleStartTime = Server.Time;
            _ordersOpenedThisCycle = 0;
            _lastEntryPrice = 0;
            _nextEntryTime = Server.Time;

            if (OpenOneOrder(true))
            {
                _cycles++;
                Print("Cycle {0} START | Mode: {1} | {2} | Orders plan: {3} | Lots/order: {4:F2} | Total lots plan: {5:F2} | Risk budget: {6:F2} | Planned risk: {7:F2} | Target: {8:F2} | Stop price model: {9:F2}",
                    _cycles,
                    TradingMode,
                    _direction,
                    _plannedOrderCount,
                    Symbol.VolumeInUnitsToQuantity(_plannedVolumePerOrder),
                    Symbol.VolumeInUnitsToQuantity(_plannedTotalVolume),
                    Account.Balance * RiskPerBasketPercent / 100.0,
                    _plannedRiskMoney,
                    _targetMoney,
                    _sizingStopPrice);
            }
            else
            {
                _nextCycleTime = Server.Time.AddSeconds(IsDemoStress ? 0.50 : 2.0);
            }
        }

        private bool PrepareRiskPlan()
        {
            var atrValue = GetAtrValue();
            if (atrValue <= 0 || Symbol.PipSize <= 0)
                return false;

            _sizingStopPrice = Clamp(atrValue * RiskStopAtrMultiplier,
                MinSizingStopPrice,
                Math.Max(MinSizingStopPrice, MaxSizingStopPrice));

            var stopPips = _sizingStopPrice / Symbol.PipSize;
            var riskBudgetMoney = Math.Max(0.01, Account.Balance * RiskPerBasketPercent / 100.0);

            var riskBasedVolume = Symbol.VolumeForFixedRisk(riskBudgetMoney, stopPips, RoundingMode.Down);
            riskBasedVolume = Symbol.NormalizeVolumeInUnits(riskBasedVolume, RoundingMode.Down);

            var capVolume = Symbol.NormalizeVolumeInUnits(Symbol.QuantityToVolumeInUnits(MaxTotalLots), RoundingMode.Down);
            capVolume = Math.Min(capVolume, Symbol.VolumeInUnitsMax);

            var totalVolume = Math.Min(riskBasedVolume, capVolume);
            totalVolume = Symbol.NormalizeVolumeInUnits(totalVolume, RoundingMode.Down);

            if (totalVolume < Symbol.VolumeInUnitsMin)
                return false;

            var possibleOrders = (int)Math.Floor(totalVolume / Symbol.VolumeInUnitsMin);
            _plannedOrderCount = Math.Max(1, Math.Min(MaxOrdersPerBasket, possibleOrders));

            _plannedVolumePerOrder = Symbol.NormalizeVolumeInUnits(totalVolume / _plannedOrderCount, RoundingMode.Down);
            if (_plannedVolumePerOrder < Symbol.VolumeInUnitsMin)
                _plannedVolumePerOrder = Symbol.VolumeInUnitsMin;

            _plannedTotalVolume = _plannedVolumePerOrder * _plannedOrderCount;
            if (_plannedTotalVolume > capVolume)
                _plannedTotalVolume = capVolume;

            _plannedRiskMoney = Math.Max(0.01, Symbol.AmountRisked(_plannedTotalVolume, stopPips));
            _lossLimitMoney = _plannedRiskMoney;
            _targetMoney = Math.Max(0.01, _plannedRiskMoney * GetEffectiveTargetR());

            return true;
        }

        private void TryAddOrder(double basketNetProfit)
        {
            if (_isClosing || _ordersOpenedThisCycle <= 0 || _ordersOpenedThisCycle >= _plannedOrderCount)
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
            var currentVolume = GetBotPositions().Sum(p => p.VolumeInUnits);
            var remainingVolume = Math.Max(0, _plannedTotalVolume - currentVolume);
            var volume = Math.Min(_plannedVolumePerOrder, remainingVolume);
            volume = Symbol.NormalizeVolumeInUnits(volume, RoundingMode.Down);

            if (volume < Symbol.VolumeInUnitsMin)
                return false;

            var result = ExecuteMarketOrder(_direction, SymbolName, volume, _label);

            if (!result.IsSuccessful)
            {
                Print("Order failed: {0}", result.Error);
                _nextEntryTime = Server.Time.AddSeconds(Math.Max(0.10, GetEffectiveEntryDelaySeconds()));
                return false;
            }

            _ordersOpenedThisCycle++;
            _lastEntryPrice = result.Position != null
                ? result.Position.EntryPrice
                : (_direction == TradeType.Buy ? Symbol.Ask : Symbol.Bid);
            _nextEntryTime = Server.Time.AddSeconds(GetEffectiveEntryDelaySeconds());

            Print("Order {0}/{1} OPEN | {2} | Lots: {3:F2} | Entry: {4:F2} | {5}",
                _ordersOpenedThisCycle,
                _plannedOrderCount,
                _direction,
                Symbol.VolumeInUnitsToQuantity(volume),
                _lastEntryPrice,
                firstOrder ? "FIRST" : "PYRAMID ADD");

            return true;
        }

        private TradeType? GetMarketDirection()
        {
            if (_m1Bars == null || _m1Bars.Count < Math.Max(SlowEmaPeriod + 5, AtrPeriod + 5))
                return null;

            if (_tickPrices.Count < TickLookback)
                return null;

            var index = _m1Bars.Count - 2;
            var fast = _fastEma.Result[index];
            var slow = _slowEma.Result[index];
            var atrValue = _atr.Result[index];
            var mid = (Symbol.Bid + Symbol.Ask) / 2.0;

            if (atrValue <= 0)
                return null;

            var ticks = _tickPrices.ToArray();
            var oldIndex = Math.Max(0, ticks.Length - TickLookback);
            var oldMid = ticks[oldIndex];
            var tickMove = mid - oldMid;
            var thresholdFactor = IsDemoStress ? DemoSignalThresholdFactor : 1.0;
            var minTickMove = MinTickMovePrice * thresholdFactor;
            var emaGap = Math.Abs(fast - slow);
            var trendRegime = emaGap >= atrValue * TrendEmaGapAtrFraction * thresholdFactor;

            if (trendRegime)
            {
                if (fast > slow && mid > fast && tickMove >= minTickMove)
                    return TradeType.Buy;

                if (fast < slow && mid < fast && tickMove <= -minTickMove)
                    return TradeType.Sell;
            }

            if (EnableRangeMode && !trendRegime)
            {
                var deviation = mid - fast;
                var threshold = atrValue * RangeDeviationAtrFraction * thresholdFactor;

                if (deviation >= threshold && tickMove <= -minTickMove)
                    return TradeType.Sell;

                if (deviation <= -threshold && tickMove >= minTickMove)
                    return TradeType.Buy;
            }

            return null;
        }

        private double GetDynamicSpacing()
        {
            var atrValue = GetAtrValue();
            var spacing = atrValue * AtrSpacingMultiplier;

            if (IsDemoStress)
                spacing *= DemoSpacingFactor;

            return Clamp(spacing, MinPriceSpacing, Math.Max(MinPriceSpacing, MaxPriceSpacing));
        }

        private double GetAtrValue()
        {
            if (_m1Bars == null || _m1Bars.Count < 2)
                return 0;

            var index = _m1Bars.Count - 2;
            return index >= 0 ? _atr.Result[index] : 0;
        }

        private void UpdateTickBuffer()
        {
            var mid = (Symbol.Bid + Symbol.Ask) / 2.0;
            _tickPrices.Enqueue(mid);

            var maxSize = Math.Max(TickLookback * 3, 100);
            while (_tickPrices.Count > maxSize)
                _tickPrices.Dequeue();
        }

        private void CloseBasket(double basketNetProfit, string reason)
        {
            if (_isClosing)
                return;

            _isClosing = true;
            CloseAllBotPositions();
            RecordStats(basketNetProfit);

            if (basketNetProfit < 0)
                _consecutiveLosses++;
            else
                _consecutiveLosses = 0;

            var delay = basketNetProfit < 0 ? GetEffectiveLossCooldownSeconds() : GetEffectiveProfitCooldownSeconds();
            _nextCycleTime = Server.Time.AddSeconds(delay);

            if (!IsDemoStress && _consecutiveLosses >= MaxConsecutiveLosses)
            {
                _pauseUntil = Server.Time.AddSeconds(LossStreakPauseSeconds);
                _consecutiveLosses = 0;
                Print("Loss streak protection: pausing until {0:u}", _pauseUntil);
            }

            Print("Basket CLOSED | {0} | P/L: {1:F2} | Planned risk: {2:F2} | Target: {3:F2} | Next cycle in {4:F1}s | Mode: {5}",
                reason, basketNetProfit, _plannedRiskMoney, _targetMoney, delay, TradingMode);
            PrintStats("RUNNING");

            _ordersOpenedThisCycle = 0;
            _lastEntryPrice = 0;
        }

        private void RecordStats(double result)
        {
            _statsBaskets++;
            _statsNetProfit += result;
            _statsBestBasket = Math.Max(_statsBestBasket, result);
            _statsWorstBasket = Math.Min(_statsWorstBasket, result);

            if (result >= 0)
            {
                _statsWins++;
                _statsGrossProfit += result;
                _statsLossStreak = 0;
            }
            else
            {
                _statsLosses++;
                _statsGrossLoss += Math.Abs(result);
                _statsLossStreak++;
                _statsMaxLossStreak = Math.Max(_statsMaxLossStreak, _statsLossStreak);
            }
        }

        private void UpdateDrawdownStats()
        {
            if (Account.Equity > _peakEquity)
                _peakEquity = Account.Equity;

            if (_peakEquity <= 0)
                return;

            var ddMoney = Math.Max(0, _peakEquity - Account.Equity);
            var ddPercent = ddMoney / _peakEquity * 100.0;

            _maxDrawdownMoney = Math.Max(_maxDrawdownMoney, ddMoney);
            _maxDrawdownPercent = Math.Max(_maxDrawdownPercent, ddPercent);
        }

        private void PrintStats(string prefix)
        {
            var winRate = _statsBaskets > 0 ? _statsWins / (double)_statsBaskets : 0.0;
            var profitFactor = _statsGrossLoss > 0 ? _statsGrossProfit / _statsGrossLoss : (_statsGrossProfit > 0 ? 999.0 : 0.0);
            var avgWin = _statsWins > 0 ? _statsGrossProfit / _statsWins : 0.0;
            var avgLoss = _statsLosses > 0 ? _statsGrossLoss / _statsLosses : 0.0;
            var best = _statsBaskets > 0 ? _statsBestBasket : 0.0;
            var worst = _statsBaskets > 0 ? _statsWorstBasket : 0.0;

            Print("STATS {0} | Baskets: {1} | Wins: {2} | Losses: {3} | WinRate: {4:P1} | PF: {5:F2} | Net: {6:F2} | AvgWin: {7:F2} | AvgLoss: {8:F2} | Best: {9:F2} | Worst: {10:F2} | MaxLossStreak: {11} | MaxDD: {12:F2} ({13:F2}%)",
                prefix,
                _statsBaskets,
                _statsWins,
                _statsLosses,
                winRate,
                profitFactor,
                _statsNetProfit,
                avgWin,
                avgLoss,
                best,
                worst,
                _statsMaxLossStreak,
                _maxDrawdownMoney,
                _maxDrawdownPercent);
        }

        private double GetEffectiveProfitCooldownSeconds()
        {
            return IsDemoStress ? DemoProfitCooldownSeconds : CooldownSeconds;
        }

        private double GetEffectiveLossCooldownSeconds()
        {
            return IsDemoStress ? DemoLossCooldownSeconds : LossCooldownSeconds;
        }

        private double GetEffectiveEntryDelaySeconds()
        {
            return IsDemoStress ? DemoEntryDelaySeconds : MinSecondsBetweenEntries;
        }

        private int GetEffectiveTimeStopSeconds()
        {
            return IsDemoStress ? DemoTimeStopSeconds : TimeStopSeconds;
        }

        private double GetEffectiveTargetR()
        {
            return IsDemoStress ? DemoTargetRMultiple : TargetRMultiple;
        }

        private bool SpreadIsAcceptable()
        {
            return Symbol.Ask - Symbol.Bid <= MaxSpreadPrice;
        }

        private void ResetDailyStateIfNeeded()
        {
            if (Server.Time.Date == _sessionDate)
                return;

            _sessionDate = Server.Time.Date;
            _dayStartBalance = Account.Balance;
            _consecutiveLosses = 0;
            _pauseUntil = Server.Time;
            Print("New trading day. Daily counters reset. Start balance: {0:F2}", _dayStartBalance);
        }

        private bool HitDailyLossLimit()
        {
            if (_dayStartBalance <= 0 || MaxDailyLossPercent <= 0)
                return false;

            var floor = _dayStartBalance * (1.0 - MaxDailyLossPercent / 100.0);
            return Account.Equity <= floor;
        }

        private bool HitHardDrawdown()
        {
            var limitPercent = IsDemoStress ? DemoEmergencyDrawdownPercent : HardDrawdownPercent;

            if (_startEquity <= 0 || limitPercent <= 0)
                return false;

            var floor = _startEquity * (1.0 - limitPercent / 100.0);
            return Account.Equity <= floor;
        }

        private void EmergencyStop(string reason)
        {
            _hardLocked = true;
            Print("{0} reached. Closing bot positions and stopping.", reason);
            CloseAllBotPositions();
            PrintStats("EMERGENCY");
            Stop();
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
