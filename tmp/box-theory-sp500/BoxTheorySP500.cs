using System;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    public enum BoxDirection { None = 0, Long = 1, Short = -1 }
    public enum BoxState { None = 0, AwaitTouch = 1, AwaitLaunch = 2, AwaitConfirmation = 3 }

    [Robot(TimeZone = TimeZones.EasternStandardTime, AccessRights = AccessRights.None)]
    public class BoxTheorySP500 : Robot
    {
        private const string Label = "BOX_THEORY_SP500";

        [Parameter("Fast EMA", DefaultValue = 20, MinValue = 2, Group = "Trend")]
        public int FastEmaPeriod { get; set; }

        [Parameter("Slow EMA", DefaultValue = 50, MinValue = 3, Group = "Trend")]
        public int SlowEmaPeriod { get; set; }

        [Parameter("Require close beyond fast EMA", DefaultValue = true, Group = "Trend")]
        public bool RequirePriceSide { get; set; }

        [Parameter("ATR Period", DefaultValue = 14, MinValue = 2, Group = "Box")]
        public int AtrPeriod { get; set; }

        [Parameter("Min impulse body (ATR)", DefaultValue = 0.60, MinValue = 0.10, Step = 0.05, Group = "Box")]
        public double MinImpulseBodyAtr { get; set; }

        [Parameter("Strong wick ratio", DefaultValue = 0.25, MinValue = 0.05, MaxValue = 0.90, Step = 0.05, Group = "Box")]
        public double StrongWickRatio { get; set; }

        [Parameter("Small wick ratio", DefaultValue = 0.05, MinValue = 0.0, MaxValue = 0.50, Step = 0.01, Group = "Box")]
        public double SmallWickRatio { get; set; }

        [Parameter("Box expiry (bars)", DefaultValue = 30, MinValue = 3, Group = "Box")]
        public int BoxExpiryBars { get; set; }

        [Parameter("Invalidation buffer (ATR)", DefaultValue = 0.50, MinValue = 0.0, Step = 0.10, Group = "Box")]
        public double InvalidationBufferAtr { get; set; }

        [Parameter("Replace with newer box", DefaultValue = true, Group = "Box")]
        public bool ReplaceWithNewBox { get; set; }

        [Parameter("Use 5m NY Opening Range", DefaultValue = true, Group = "Opening Range")]
        public bool UseOpeningRange { get; set; }

        [Parameter("Trade regular session only", DefaultValue = true, Group = "Opening Range")]
        public bool RegularSessionOnly { get; set; }

        [Parameter("Session end hour ET", DefaultValue = 16, MinValue = 10, MaxValue = 23, Group = "Opening Range")]
        public int SessionEndHour { get; set; }

        [Parameter("Enable LONG", DefaultValue = true, Group = "Execution")]
        public bool EnableLong { get; set; }

        [Parameter("Enable SHORT", DefaultValue = true, Group = "Execution")]
        public bool EnableShort { get; set; }

        [Parameter("Risk % equity / trade", DefaultValue = 0.25, MinValue = 0.01, MaxValue = 5.0, Step = 0.05, Group = "Execution")]
        public double RiskPercent { get; set; }

        [Parameter("Reward / Risk", DefaultValue = 2.0, MinValue = 1.0, MaxValue = 10.0, Step = 0.25, Group = "Execution")]
        public double RewardRisk { get; set; }

        [Parameter("SL buffer (pips)", DefaultValue = 1.0, MinValue = 0.0, Step = 0.1, Group = "Execution")]
        public double StopBufferPips { get; set; }

        [Parameter("Max open positions", DefaultValue = 1, MinValue = 1, MaxValue = 10, Group = "Execution")]
        public int MaxOpenPositions { get; set; }

        [Parameter("Cooldown (bars)", DefaultValue = 2, MinValue = 0, MaxValue = 100, Group = "Execution")]
        public int CooldownBars { get; set; }

        [Parameter("Max spread (pips, 0=off)", DefaultValue = 0.0, MinValue = 0.0, Step = 0.1, Group = "Execution")]
        public double MaxSpreadPips { get; set; }

        [Parameter("Daily loss guard %", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0, Step = 0.1, Group = "Safety")]
        public double DailyLossGuardPercent { get; set; }

        private ExponentialMovingAverage _emaFast;
        private ExponentialMovingAverage _emaSlow;
        private AverageTrueRange _atr;

        private DateTime _tradingDate;
        private double _dayStartEquity;

        private bool _orCollecting;
        private bool _orReady;
        private double _orHigh;
        private double _orLow;
        private BoxDirection _orBias;

        private BoxDirection _boxDirection = BoxDirection.None;
        private BoxState _boxState = BoxState.None;
        private double _boxLow;
        private double _boxHigh;
        private int _boxCreatedIndex = -1;
        private int _lastTradeIndex = -100000;

        protected override void OnStart()
        {
            _emaFast = Indicators.ExponentialMovingAverage(Bars.ClosePrices, FastEmaPeriod);
            _emaSlow = Indicators.ExponentialMovingAverage(Bars.ClosePrices, SlowEmaPeriod);
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Exponential);

            _tradingDate = Server.Time.Date;
            _dayStartEquity = Account.Equity;
            ResetOpeningRange();

            Print("BoxTheorySP500 started on {0}. Robot timezone: New York / Eastern.", SymbolName);
        }

        protected override void OnBarClosed()
        {
            if (Bars.Count < Math.Max(SlowEmaPeriod + 5, AtrPeriod + 5))
                return;

            int i = Bars.Count - 1;
            DateTime time = Bars.OpenTimes[i];

            if (time.Date != _tradingDate)
                ResetTradingDay(time.Date);

            UpdateOpeningRange(i);
            ManageActiveBox(i);

            if (!CanSearchForSetups(i))
                return;

            DetectAndStoreNewBox(i);
        }

        private void ResetTradingDay(DateTime date)
        {
            _tradingDate = date;
            _dayStartEquity = Account.Equity;
            ResetOpeningRange();
            ResetBox("new trading day");
        }

        private void ResetOpeningRange()
        {
            _orCollecting = false;
            _orReady = false;
            _orHigh = double.MinValue;
            _orLow = double.MaxValue;
            _orBias = BoxDirection.None;
        }

        private void UpdateOpeningRange(int i)
        {
            DateTime t = Bars.OpenTimes[i];
            TimeSpan tod = t.TimeOfDay;
            TimeSpan start = new TimeSpan(9, 30, 0);
            TimeSpan end = new TimeSpan(9, 35, 0);

            if (tod >= start && tod < end)
            {
                if (!_orCollecting)
                {
                    _orCollecting = true;
                    _orHigh = Bars.HighPrices[i];
                    _orLow = Bars.LowPrices[i];
                }
                else
                {
                    _orHigh = Math.Max(_orHigh, Bars.HighPrices[i]);
                    _orLow = Math.Min(_orLow, Bars.LowPrices[i]);
                }
            }

            if (_orCollecting && tod >= end)
                _orReady = true;

            if (_orReady && _orBias == BoxDirection.None)
            {
                if (Bars.ClosePrices[i] > _orHigh)
                    _orBias = BoxDirection.Long;
                else if (Bars.ClosePrices[i] < _orLow)
                    _orBias = BoxDirection.Short;
            }
        }

        private bool CanSearchForSetups(int i)
        {
            DateTime t = Bars.OpenTimes[i];

            if (RegularSessionOnly)
            {
                TimeSpan start = new TimeSpan(9, 35, 0);
                TimeSpan end = new TimeSpan(SessionEndHour, 0, 0);
                if (t.TimeOfDay < start || t.TimeOfDay >= end)
                    return false;
            }

            if (UseOpeningRange && (!_orReady || _orBias == BoxDirection.None))
                return false;

            if (DailyLossGuardHit())
                return false;

            if (i - _lastTradeIndex < CooldownBars)
                return false;

            return true;
        }

        private bool DailyLossGuardHit()
        {
            if (_dayStartEquity <= 0)
                return false;

            double threshold = _dayStartEquity * (1.0 - DailyLossGuardPercent / 100.0);
            return Account.Equity <= threshold;
        }

        private bool TrendLong(int i)
        {
            bool emaAligned = _emaFast.Result[i] > _emaSlow.Result[i];
            bool slope = _emaFast.Result[i] > _emaFast.Result[Math.Max(0, i - 3)];
            bool priceOk = !RequirePriceSide || Bars.ClosePrices[i] > _emaFast.Result[i];
            bool orOk = !UseOpeningRange || _orBias == BoxDirection.Long;
            return emaAligned && slope && priceOk && orOk;
        }

        private bool TrendShort(int i)
        {
            bool emaAligned = _emaFast.Result[i] < _emaSlow.Result[i];
            bool slope = _emaFast.Result[i] < _emaFast.Result[Math.Max(0, i - 3)];
            bool priceOk = !RequirePriceSide || Bars.ClosePrices[i] < _emaFast.Result[i];
            bool orOk = !UseOpeningRange || _orBias == BoxDirection.Short;
            return emaAligned && slope && priceOk && orOk;
        }

        private void DetectAndStoreNewBox(int i)
        {
            if (i < 2)
                return;

            int source = i - 1;
            double atr = _atr.Result[i];

            if (double.IsNaN(atr) || atr <= 0)
                return;

            double impulseOpen = Bars.OpenPrices[i];
            double impulseClose = Bars.ClosePrices[i];
            double impulseBody = Math.Abs(impulseClose - impulseOpen);

            if (impulseBody < MinImpulseBodyAtr * atr)
                return;

            bool sourceBearish = Bars.ClosePrices[source] < Bars.OpenPrices[source];
            bool sourceBullish = Bars.ClosePrices[source] > Bars.OpenPrices[source];
            bool impulseBullish = impulseClose > impulseOpen;
            bool impulseBearish = impulseClose < impulseOpen;

            bool bullishSetup =
                EnableLong &&
                TrendLong(i) &&
                sourceBearish &&
                impulseBullish &&
                impulseClose > Bars.HighPrices[source];

            bool bearishSetup =
                EnableShort &&
                TrendShort(i) &&
                sourceBullish &&
                impulseBearish &&
                impulseClose < Bars.LowPrices[source];

            if (!bullishSetup && !bearishSetup)
                return;

            if (_boxState != BoxState.None && !ReplaceWithNewBox)
                return;

            if (bullishSetup)
            {
                BuildBullishBox(source, out double low, out double high);
                SetBox(BoxDirection.Long, low, high, i);
            }
            else
            {
                BuildBearishBox(source, out double low, out double high);
                SetBox(BoxDirection.Short, low, high, i);
            }
        }

        private void BuildBullishBox(int i, out double low, out double high)
        {
            double o = Bars.OpenPrices[i];
            double c = Bars.ClosePrices[i];
            double h = Bars.HighPrices[i];
            double l = Bars.LowPrices[i];

            double range = Math.Max(h - l, Symbol.TickSize);
            double bodyTop = Math.Max(o, c);
            double bodyBottom = Math.Min(o, c);
            double upperWickRatio = (h - bodyTop) / range;

            high = h;

            if (upperWickRatio >= StrongWickRatio)
                low = bodyTop;
            else if (upperWickRatio >= SmallWickRatio)
                low = bodyBottom;
            else
                low = l;
        }

        private void BuildBearishBox(int i, out double low, out double high)
        {
            double o = Bars.OpenPrices[i];
            double c = Bars.ClosePrices[i];
            double h = Bars.HighPrices[i];
            double l = Bars.LowPrices[i];

            double range = Math.Max(h - l, Symbol.TickSize);
            double bodyTop = Math.Max(o, c);
            double bodyBottom = Math.Min(o, c);
            double lowerWickRatio = (bodyBottom - l) / range;

            low = l;

            if (lowerWickRatio >= StrongWickRatio)
                high = bodyBottom;
            else if (lowerWickRatio >= SmallWickRatio)
                high = bodyTop;
            else
                high = h;
        }

        private void SetBox(BoxDirection direction, double low, double high, int createdIndex)
        {
            if (high <= low)
                return;

            _boxDirection = direction;
            _boxLow = low;
            _boxHigh = high;
            _boxCreatedIndex = createdIndex;
            _boxState = BoxState.AwaitTouch;
        }

        private void ManageActiveBox(int i)
        {
            if (_boxState == BoxState.None || _boxDirection == BoxDirection.None)
                return;

            if (i <= _boxCreatedIndex)
                return;

            if (i - _boxCreatedIndex > BoxExpiryBars)
            {
                ResetBox("expired");
                return;
            }

            double atr = _atr.Result[i];
            double close = Bars.ClosePrices[i];

            if (_boxDirection == BoxDirection.Long &&
                close < _boxLow - InvalidationBufferAtr * atr)
            {
                ResetBox("long invalidated");
                return;
            }

            if (_boxDirection == BoxDirection.Short &&
                close > _boxHigh + InvalidationBufferAtr * atr)
            {
                ResetBox("short invalidated");
                return;
            }

            if (_boxState == BoxState.AwaitConfirmation)
            {
                bool confirmed = _boxDirection == BoxDirection.Long
                    ? Bars.ClosePrices[i] > _boxHigh && Bars.ClosePrices[i] > Bars.OpenPrices[i]
                    : Bars.ClosePrices[i] < _boxLow && Bars.ClosePrices[i] < Bars.OpenPrices[i];

                if (confirmed)
                {
                    TryEnter(i);
                    ResetBox("signal consumed");
                    return;
                }

                _boxState = BoxState.AwaitLaunch;
            }

            bool intersects =
                Bars.HighPrices[i] >= _boxLow &&
                Bars.LowPrices[i] <= _boxHigh;

            if (_boxState == BoxState.AwaitTouch && intersects)
                _boxState = BoxState.AwaitLaunch;

            if (_boxState == BoxState.AwaitLaunch)
            {
                bool launch = _boxDirection == BoxDirection.Long
                    ? Bars.ClosePrices[i] > _boxHigh && Bars.ClosePrices[i] > Bars.OpenPrices[i]
                    : Bars.ClosePrices[i] < _boxLow && Bars.ClosePrices[i] < Bars.OpenPrices[i];

                if (launch)
                    _boxState = BoxState.AwaitConfirmation;
            }
        }

        private void TryEnter(int i)
        {
            if (DailyLossGuardHit())
                return;

            var currentPositions = Positions.FindAll(Label, SymbolName);
            if (currentPositions.Length >= MaxOpenPositions)
                return;

            if (MaxSpreadPips > 0)
            {
                double spreadPips = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;
                if (spreadPips > MaxSpreadPips)
                    return;
            }

            TradeType tradeType = _boxDirection == BoxDirection.Long ? TradeType.Buy : TradeType.Sell;
            double entry = tradeType == TradeType.Buy ? Symbol.Ask : Symbol.Bid;

            double stopPrice = tradeType == TradeType.Buy
                ? _boxLow - StopBufferPips * Symbol.PipSize
                : _boxHigh + StopBufferPips * Symbol.PipSize;

            double stopLossPips = Math.Abs(entry - stopPrice) / Symbol.PipSize;
            if (stopLossPips < 1.0)
                return;

            double takeProfitPips = stopLossPips * RewardRisk;

            double volume = Symbol.VolumeForProportionalRisk(
                ProportionalAmountType.Equity,
                RiskPercent,
                stopLossPips,
                RoundingMode.Down);

            volume = Math.Max(Symbol.VolumeInUnitsMin, volume);
            volume = Math.Min(Symbol.VolumeInUnitsMax, volume);
            volume = Symbol.NormalizeVolumeInUnits(volume, RoundingMode.Down);

            var result = ExecuteMarketOrder(
                tradeType,
                SymbolName,
                volume,
                Label,
                stopLossPips,
                takeProfitPips);

            if (result.IsSuccessful)
                _lastTradeIndex = i;
        }

        private void ResetBox(string reason)
        {
            _boxDirection = BoxDirection.None;
            _boxState = BoxState.None;
            _boxLow = 0;
            _boxHigh = 0;
            _boxCreatedIndex = -1;
        }
    }
}
