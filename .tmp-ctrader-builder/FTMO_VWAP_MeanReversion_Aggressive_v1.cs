using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None, DefaultTimeFrame = "M5")]
    public class FTMO_VWAP_MeanReversion_Aggressive_v1 : Robot
    {
        [Parameter("Risk / Trade %", Group = "Risk", DefaultValue = 0.50, MinValue = 0.10, MaxValue = 1.00, Step = 0.05)]
        public double RiskPercent { get; set; }

        [Parameter("Daily Circuit Breaker %", Group = "Risk", DefaultValue = 3.0, MinValue = 0.5, MaxValue = 4.0, Step = 0.25)]
        public double DailyCircuitBreakerPercent { get; set; }

        [Parameter("Max Bot Exposures", Group = "Risk", DefaultValue = 2, MinValue = 1, MaxValue = 2)]
        public int MaxBotExposures { get; set; }

        [Parameter("Entry Sigma", Group = "Setup", DefaultValue = 1.8, MinValue = 1.5, MaxValue = 2.5, Step = 0.1)]
        public double EntrySigma { get; set; }

        [Parameter("Maker Sigma", Group = "Setup", DefaultValue = 1.6, MinValue = 1.0, MaxValue = 2.2, Step = 0.1)]
        public double MakerSigma { get; set; }

        [Parameter("Invalidation Sigma", Group = "Setup", DefaultValue = 2.8, MinValue = 2.2, MaxValue = 3.5, Step = 0.1)]
        public double InvalidationSigma { get; set; }

        [Parameter("Runner Sigma", Group = "Setup", DefaultValue = 0.8, MinValue = 0.3, MaxValue = 2.0, Step = 0.1)]
        public double RunnerSigma { get; set; }

        [Parameter("ADX Period", Group = "Regime", DefaultValue = 14, MinValue = 10, MaxValue = 18)]
        public int AdxPeriod { get; set; }

        [Parameter("ADX Block Level", Group = "Regime", DefaultValue = 26.0, MinValue = 15.0, MaxValue = 35.0, Step = 0.5)]
        public double AdxBlockLevel { get; set; }

        [Parameter("ATR Period", Group = "Risk", DefaultValue = 14, MinValue = 5, MaxValue = 50)]
        public int AtrPeriod { get; set; }

        [Parameter("Stop ATR Mult", Group = "Risk", DefaultValue = 1.4, MinValue = 1.0, MaxValue = 2.2, Step = 0.1)]
        public double StopAtrMultiplier { get; set; }

        [Parameter("Warmup Bars", Group = "Setup", DefaultValue = 4, MinValue = 4, MaxValue = 24)]
        public int WarmupBars { get; set; }

        [Parameter("Reentry Max Bars", Group = "Setup", DefaultValue = 5, MinValue = 1, MaxValue = 8)]
        public int ReentryMaxBars { get; set; }

        [Parameter("Pending Expiry Bars", Group = "Execution", DefaultValue = 5, MinValue = 1, MaxValue = 12)]
        public int PendingExpiryBars { get; set; }

        [Parameter("Time Stop Bars", Group = "Execution", DefaultValue = 9, MinValue = 3, MaxValue = 48)]
        public int TimeStopBars { get; set; }

        [Parameter("TP1 Close %", Group = "Exit", DefaultValue = 75.0, MinValue = 50.0, MaxValue = 90.0, Step = 5.0)]
        public double Tp1ClosePercent { get; set; }

        [Parameter("Use CVD Proxy", Group = "Flow", DefaultValue = true)]
        public bool UseCvdProxy { get; set; }

        [Parameter("CVD Divergence Lookback", Group = "Flow", DefaultValue = 8, MinValue = 4, MaxValue = 48)]
        public int CvdLookback { get; set; }

        [Parameter("Max Friction / TP1", Group = "Execution", DefaultValue = 0.30, MinValue = 0.05, MaxValue = 0.50, Step = 0.01)]
        public double MaxFrictionRatio { get; set; }

        [Parameter("Commission RT (pips)", Group = "Execution", DefaultValue = 0.70, MinValue = 0.0, MaxValue = 10.0, Step = 0.05)]
        public double CommissionRoundTurnPips { get; set; }

        [Parameter("Expected Slippage (pips)", Group = "Execution", DefaultValue = 0.30, MinValue = 0.0, MaxValue = 10.0, Step = 0.05)]
        public double ExpectedSlippagePips { get; set; }

        [Parameter("Spread Freeze Multiple", Group = "Execution", DefaultValue = 2.5, MinValue = 1.2, MaxValue = 5.0, Step = 0.1)]
        public double SpreadFreezeMultiple { get; set; }

        [Parameter("Latency Guard ms", Group = "Execution", DefaultValue = 400, MinValue = 50, MaxValue = 5000, Step = 50)]
        public int LatencyGuardMs { get; set; }

        [Parameter("Latency Freeze Min", Group = "Execution", DefaultValue = 3, MinValue = 1, MaxValue = 60)]
        public int LatencyFreezeMinutes { get; set; }

        [Parameter("Bot Label", Group = "Execution", DefaultValue = "FTMO_VWAP_MR_AGG_V1")]
        public string BotLabel { get; set; }

        private static readonly TimeSpan BarSpan = TimeSpan.FromMinutes(5);
        private Bars _m5;
        private AverageTrueRange _atr;
        private DirectionalMovementSystem _dms;
        private DateTime _lastProcessedOpen = DateTime.MinValue;
        private DateTime _riskDate;
        private double _dayStartBalance;
        private bool _dailyLocked;
        private DateTime _latencyLockedUntil = DateTime.MinValue;
        private DateTime _lastSpreadSampleMinute = DateTime.MinValue;
        private readonly Queue<SpreadSample> _spreadSamples = new Queue<SpreadSample>();
        private Signal _signal;
        private bool _replaying;

        private enum SignalStage { WaitingReentry, WaitingMakerArm }
        private enum SignalDirection { Buy, Sell }

        private sealed class Signal
        {
            public SignalDirection Direction;
            public SignalStage Stage;
            public int ExtensionIndex;
            public int ReentryIndex;
            public int ExpiryIndex;
        }

        private struct SpreadSample
        {
            public DateTime Time;
            public double Pips;
        }

        private struct VwapSnapshot
        {
            public int SessionStartIndex;
            public double Vwap;
            public double Sigma;
            public double LowerEntry;
            public double UpperEntry;
            public double LowerMaker;
            public double UpperMaker;
            public double LowerInvalidation;
            public double UpperInvalidation;
            public double LowerRunner;
            public double UpperRunner;
        }

        protected override void OnStart()
        {
            _m5 = MarketData.GetBars(TimeFrame.Minute5, SymbolName);
            EnsureHistory(800);
            _atr = Indicators.AverageTrueRange(_m5, AtrPeriod, MovingAverageType.WilderSmoothing);
            _dms = Indicators.DirectionalMovementSystem(_m5, AdxPeriod, MovingAverageType.WilderSmoothing);
            UpdateRiskDay(true);
            WarmUpSignalState();

            Print("[{0}] START | {1} | AGGRESSIVE M5 VWAP mean reversion | Risk={2:F2}% | Entry={3:F1}σ | ADX<{4:F1}",
                BotLabel, SymbolName, RiskPercent, EntrySigma, AdxBlockLevel);
            Print("[{0}] DEMO/BACKTEST BUILD | CVD proxy={1} | 24/5 while broker market is open", BotLabel, UseCvdProxy);
        }

        protected override void OnTick()
        {
            UpdateRiskDay(false);
            SampleSpread();
            ApplyDailyCircuitBreaker();
            ProcessClosedBars();
            ManageOpenPositions();
            TryPlaceMakerOrder();
        }

        private void WarmUpSignalState()
        {
            var last = ResolveLastClosedIndex();
            if (last < 0)
                return;

            var start = last;
            var date = AsUtc(_m5.OpenTimes[last]).Date;
            while (start > 0 && AsUtc(_m5.OpenTimes[start - 1]).Date == date)
                start--;

            _replaying = true;
            try
            {
                for (var i = start; i <= last; i++)
                    ProcessBar(i);
            }
            finally
            {
                _replaying = false;
            }
        }

        private void ProcessClosedBars()
        {
            var last = ResolveLastClosedIndex();
            if (last < 0)
                return;

            var first = last;
            while (first > 0 && AsUtc(_m5.OpenTimes[first - 1]) > _lastProcessedOpen)
                first--;

            for (var i = first; i <= last; i++)
            {
                var open = AsUtc(_m5.OpenTimes[i]);
                if (open <= _lastProcessedOpen)
                    continue;
                ProcessBar(i);
            }
        }

        private void ProcessBar(int index)
        {
            _lastProcessedOpen = AsUtc(_m5.OpenTimes[index]);

            VwapSnapshot snapshot;
            if (!TryGetVwapSnapshot(index, out snapshot))
                return;
            if (index - snapshot.SessionStartIndex < WarmupBars)
                return;

            var adx = _dms.ADX[index];
            if (!IsFinite(adx))
                return;

            if (_signal != null)
            {
                if (AsUtc(_m5.OpenTimes[_signal.ExtensionIndex]).Date != AsUtc(_m5.OpenTimes[index]).Date)
                {
                    ResetSignal("NEW_UTC_DAY");
                }
                else if (adx >= AdxBlockLevel)
                {
                    ResetSignal("ADX_REGIME_BLOCK");
                }
                else if (_signal.Stage == SignalStage.WaitingReentry)
                {
                    if (index > _signal.ExpiryIndex)
                    {
                        ResetSignal("REENTRY_EXPIRED");
                    }
                    else if (IsReentry(index, snapshot, _signal.Direction))
                    {
                        _signal.Stage = SignalStage.WaitingMakerArm;
                        _signal.ReentryIndex = index;
                        _signal.ExpiryIndex = index + PendingExpiryBars;
                        Log("REENTRY", "Dir={0} Close={1} VWAP={2} Sigma={3}",
                            _signal.Direction, _m5.ClosePrices[index], snapshot.Vwap, snapshot.Sigma);
                    }
                }
                else if (index > _signal.ExpiryIndex)
                {
                    ResetSignal("MAKER_ARM_EXPIRED");
                }
            }

            if (_signal != null || adx >= AdxBlockLevel)
                return;

            var close = _m5.ClosePrices[index];
            if (close < snapshot.LowerEntry && FlowConfirms(index, snapshot.SessionStartIndex, SignalDirection.Buy))
            {
                _signal = new Signal
                {
                    Direction = SignalDirection.Buy,
                    Stage = SignalStage.WaitingReentry,
                    ExtensionIndex = index,
                    ReentryIndex = -1,
                    ExpiryIndex = index + ReentryMaxBars
                };
                Log("EXTENSION", "Dir=Buy Close={0} LowerEntry={1} ADX={2:F2}", close, snapshot.LowerEntry, adx);
            }
            else if (close > snapshot.UpperEntry && FlowConfirms(index, snapshot.SessionStartIndex, SignalDirection.Sell))
            {
                _signal = new Signal
                {
                    Direction = SignalDirection.Sell,
                    Stage = SignalStage.WaitingReentry,
                    ExtensionIndex = index,
                    ReentryIndex = -1,
                    ExpiryIndex = index + ReentryMaxBars
                };
                Log("EXTENSION", "Dir=Sell Close={0} UpperEntry={1} ADX={2:F2}", close, snapshot.UpperEntry, adx);
            }
        }

        private bool IsReentry(int index, VwapSnapshot snapshot, SignalDirection direction)
        {
            var open = _m5.OpenPrices[index];
            var close = _m5.ClosePrices[index];
            if (direction == SignalDirection.Buy)
                return close > snapshot.LowerEntry && close > open;
            return close < snapshot.UpperEntry && close < open;
        }

        private bool FlowConfirms(int index, int sessionStartIndex, SignalDirection direction)
        {
            if (!UseCvdProxy)
                return true;

            var from = Math.Max(sessionStartIndex + 1, index - CvdLookback);
            if (from >= index)
                return false;

            var swingIndex = from;
            if (direction == SignalDirection.Buy)
            {
                var priorLow = double.MaxValue;
                for (var i = from; i < index; i++)
                {
                    if (_m5.LowPrices[i] < priorLow)
                    {
                        priorLow = _m5.LowPrices[i];
                        swingIndex = i;
                    }
                }

                if (_m5.LowPrices[index] >= priorLow)
                    return false;
            }
            else
            {
                var priorHigh = double.MinValue;
                for (var i = from; i < index; i++)
                {
                    if (_m5.HighPrices[i] > priorHigh)
                    {
                        priorHigh = _m5.HighPrices[i];
                        swingIndex = i;
                    }
                }

                if (_m5.HighPrices[index] <= priorHigh)
                    return false;
            }

            var currentCvd = CvdProxyAt(index, sessionStartIndex);
            var swingCvd = CvdProxyAt(swingIndex, sessionStartIndex);
            return direction == SignalDirection.Buy ? currentCvd > swingCvd : currentCvd < swingCvd;
        }

        private double CvdProxyAt(int index, int sessionStartIndex)
        {
            var cvd = 0.0;
            for (var i = sessionStartIndex; i <= index; i++)
            {
                var reference = i == sessionStartIndex ? _m5.OpenPrices[i] : _m5.ClosePrices[i - 1];
                var delta = _m5.ClosePrices[i] - reference;
                if (delta > 0)
                    cvd += _m5.TickVolumes[i];
                else if (delta < 0)
                    cvd -= _m5.TickVolumes[i];
            }
            return cvd;
        }

        private void TryPlaceMakerOrder()
        {
            if (_replaying || _dailyLocked || _signal == null || _signal.Stage != SignalStage.WaitingMakerArm)
                return;
            if (Server.Time < _latencyLockedUntil)
                return;
            if (HasExposureOnSymbol() || CountBotExposures() >= MaxBotExposures)
                return;

            var index = ResolveLastClosedIndex();
            if (index < 0 || index > _signal.ExpiryIndex)
            {
                ResetSignal("MAKER_LIVE_EXPIRED");
                return;
            }

            VwapSnapshot snapshot;
            if (!TryGetVwapSnapshot(index, out snapshot))
                return;

            var adx = _dms.ADX[index];
            var atr = _atr.Result[index];
            if (!IsFinite(adx) || adx >= AdxBlockLevel || !IsFinitePositive(atr))
                return;
            if (IsSpreadFrozen())
                return;

            var direction = _signal.Direction;
            var targetPrice = direction == SignalDirection.Buy ? snapshot.LowerMaker : snapshot.UpperMaker;

            if (direction == SignalDirection.Buy && targetPrice >= Symbol.Ask)
                return;
            if (direction == SignalDirection.Sell && targetPrice <= Symbol.Bid)
                return;

            var target1DistancePips = Math.Abs(snapshot.Vwap - targetPrice) / Symbol.PipSize;
            var spreadPips = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;
            var frictionPips = spreadPips + CommissionRoundTurnPips + ExpectedSlippagePips;
            var frictionRatio = frictionPips / Math.Max(target1DistancePips, 0.0001);

            if (!IsFinitePositive(target1DistancePips) || frictionRatio > MaxFrictionRatio)
            {
                Log("FRICTION_BLOCK", "ratio={0:F3} spread={1:F2} target1={2:F2}", frictionRatio, spreadPips, target1DistancePips);
                ResetSignal("FRICTION_TOO_HIGH");
                return;
            }

            var sigmaInvalidation = direction == SignalDirection.Buy ? snapshot.LowerInvalidation : snapshot.UpperInvalidation;
            var stopDistance = Math.Max(StopAtrMultiplier * atr, Math.Abs(targetPrice - sigmaInvalidation));
            var stopPips = stopDistance / Symbol.PipSize;

            if (!IsFinitePositive(stopPips))
            {
                ResetSignal("INVALID_STOP");
                return;
            }

            var volume = Symbol.VolumeForProportionalRisk(ProportionalAmountType.Equity, RiskPercent, stopPips, RoundingMode.Down);
            volume = Symbol.NormalizeVolumeInUnits(volume, RoundingMode.Down);

            if (volume < Symbol.VolumeInUnitsMin)
            {
                ResetSignal("VOLUME_TOO_SMALL");
                return;
            }
            if (volume > Symbol.VolumeInUnitsMax)
                volume = Symbol.NormalizeVolumeInUnits(Symbol.VolumeInUnitsMax, RoundingMode.Down);

            var tradeType = direction == SignalDirection.Buy ? TradeType.Buy : TradeType.Sell;
            var expiry = AsUtc(_m5.OpenTimes[index]).AddTicks(BarSpan.Ticks * (PendingExpiryBars + 1));
            var comment = string.Format(CultureInfo.InvariantCulture,
                "VWAPMR_AGG;INITVOL={0:R};SIG={1};RE={2}", volume, _signal.ExtensionIndex, _signal.ReentryIndex);

            var started = DateTime.UtcNow;
            var result = PlaceLimitOrder(tradeType, SymbolName, volume, targetPrice, BotLabel,
                stopPips, null, ProtectionType.Relative, expiry, comment);
            var elapsedMs = (DateTime.UtcNow - started).TotalMilliseconds;

            if (!result.IsSuccessful)
            {
                Log("ORDER_REJECTED", "Error={0} Target={1} StopPips={2:F2}", result.Error, targetPrice, stopPips);
                ResetSignal("ORDER_REJECTED");
                return;
            }

            Log("LIMIT_PLACED", "Dir={0} Target={1} SLpips={2:F2} Vol={3} Friction={4:F2}p/{5:F2}p Latency={6:F0}ms",
                direction, targetPrice, stopPips, volume, frictionPips, target1DistancePips, elapsedMs);
            ResetSignal("ORDER_PLACED");

            if (elapsedMs > LatencyGuardMs)
            {
                _latencyLockedUntil = Server.Time.AddMinutes(LatencyFreezeMinutes);
                if (result.PendingOrder != null)
                {
                    var cancel = CancelPendingOrder(result.PendingOrder);
                    Log("LATENCY_GUARD", "{0:F0}ms > {1}ms; pending cancel success={2}; frozen until {3:HH:mm:ss}",
                        elapsedMs, LatencyGuardMs, cancel.IsSuccessful, _latencyLockedUntil);
                }
            }
        }

        private void ManageOpenPositions()
        {
            var index = ResolveLastClosedIndex();
            if (index < 0)
                return;

            VwapSnapshot snapshot;
            if (!TryGetVwapSnapshot(index, out snapshot))
                return;

            var positions = Positions.FindAll(BotLabel);
            foreach (var position in positions)
            {
                if (position.SymbolName != SymbolName)
                    continue;

                var held = AsUtc(Server.Time) - AsUtc(position.EntryTime);
                if (held >= TimeSpan.FromTicks(BarSpan.Ticks * TimeStopBars))
                {
                    Log("TIME_STOP", "Position={0} HeldMin={1:F1}", position.Id, held.TotalMinutes);
                    ClosePosition(position);
                    continue;
                }

                var initialVolume = ParseInitialVolume(position.Comment, position.VolumeInUnits);
                var runnerTargetVolume = Symbol.NormalizeVolumeInUnits(initialVolume * (1.0 - Tp1ClosePercent / 100.0), RoundingMode.Down);
                runnerTargetVolume = Math.Max(Symbol.VolumeInUnitsMin, runnerTargetVolume);
                var tp1Done = position.VolumeInUnits <= runnerTargetVolume + Symbol.VolumeInUnitsStep * 0.5;
                var current = position.TradeType == TradeType.Buy ? Symbol.Bid : Symbol.Ask;

                if (!tp1Done)
                {
                    var hitVwap = position.TradeType == TradeType.Buy ? current >= snapshot.Vwap : current <= snapshot.Vwap;
                    if (!hitVwap)
                        continue;

                    var newVolume = Math.Min(position.VolumeInUnits, runnerTargetVolume);
                    if (newVolume < position.VolumeInUnits && newVolume >= Symbol.VolumeInUnitsMin)
                    {
                        var reduce = position.ModifyVolume(newVolume);
                        if (!reduce.IsSuccessful)
                        {
                            Log("TP1_REDUCE_FAIL", "Position={0} Error={1}", position.Id, reduce.Error);
                            continue;
                        }
                    }
                    else
                    {
                        ClosePosition(position);
                        continue;
                    }

                    var spreadPips = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;
                    var costPips = spreadPips + CommissionRoundTurnPips + ExpectedSlippagePips;
                    var breakEven = position.TradeType == TradeType.Buy
                        ? position.EntryPrice + costPips * Symbol.PipSize
                        : position.EntryPrice - costPips * Symbol.PipSize;

                    var stop = position.ModifyStopLossPrice(breakEven);
                    Log("TP1", "Position={0} NewVol={1} BE={2} StopModified={3}", position.Id, newVolume, breakEven, stop.IsSuccessful);
                    continue;
                }

                var runnerTarget = position.TradeType == TradeType.Buy ? snapshot.UpperRunner : snapshot.LowerRunner;
                var hitRunner = position.TradeType == TradeType.Buy ? current >= runnerTarget : current <= runnerTarget;
                if (hitRunner)
                {
                    Log("TP2", "Position={0} Target={1}", position.Id, runnerTarget);
                    ClosePosition(position);
                }
            }
        }

        private bool TryGetVwapSnapshot(int index, out VwapSnapshot snapshot)
        {
            snapshot = new VwapSnapshot();
            if (index < 0 || index >= _m5.Count)
                return false;

            var date = AsUtc(_m5.OpenTimes[index]).Date;
            var start = index;
            while (start > 0 && AsUtc(_m5.OpenTimes[start - 1]).Date == date)
                start--;

            var sumV = 0.0;
            var sumPV = 0.0;
            var sumP2V = 0.0;

            for (var i = start; i <= index; i++)
            {
                var volume = Math.Max(0.0, _m5.TickVolumes[i]);
                if (volume <= 0)
                    continue;

                var typicalPrice = (_m5.HighPrices[i] + _m5.LowPrices[i] + _m5.ClosePrices[i]) / 3.0;
                sumV += volume;
                sumPV += typicalPrice * volume;
                sumP2V += typicalPrice * typicalPrice * volume;
            }

            if (sumV <= 0)
                return false;

            var vwap = sumPV / sumV;
            var variance = Math.Max(0.0, sumP2V / sumV - vwap * vwap);
            var sigma = Math.Sqrt(variance);
            if (!IsFinitePositive(sigma))
                return false;

            snapshot.SessionStartIndex = start;
            snapshot.Vwap = vwap;
            snapshot.Sigma = sigma;
            snapshot.LowerEntry = vwap - EntrySigma * sigma;
            snapshot.UpperEntry = vwap + EntrySigma * sigma;
            snapshot.LowerMaker = vwap - MakerSigma * sigma;
            snapshot.UpperMaker = vwap + MakerSigma * sigma;
            snapshot.LowerInvalidation = vwap - InvalidationSigma * sigma;
            snapshot.UpperInvalidation = vwap + InvalidationSigma * sigma;
            snapshot.LowerRunner = vwap - RunnerSigma * sigma;
            snapshot.UpperRunner = vwap + RunnerSigma * sigma;
            return true;
        }

        private void ApplyDailyCircuitBreaker()
        {
            if (_dailyLocked)
                return;

            var floor = _dayStartBalance * (1.0 - DailyCircuitBreakerPercent / 100.0);
            if (Account.Equity > floor)
                return;

            _dailyLocked = true;
            Log("DAILY_BREAKER", "Equity={0:F2} Floor={1:F2}; flattening bot exposure", Account.Equity, floor);

            foreach (var order in PendingOrders.Where(o => o.Label == BotLabel).ToArray())
                CancelPendingOrder(order);
            foreach (var position in Positions.FindAll(BotLabel))
                ClosePosition(position);

            ResetSignal("DAILY_BREAKER");
        }

        private void UpdateRiskDay(bool force)
        {
            var date = AsUtc(Server.Time).Date;
            if (!force && date == _riskDate)
                return;

            _riskDate = date;
            var realisedToday = 0.0;
            foreach (var trade in History)
            {
                if (AsUtc(trade.ClosingTime).Date == date)
                    realisedToday += trade.NetProfit;
            }

            _dayStartBalance = Account.Balance - realisedToday;
            _dailyLocked = false;
            Log("RISK_DAY", "UTC={0:yyyy-MM-dd} StartBalance≈{1:F2}", date, _dayStartBalance);
        }

        private bool IsSpreadFrozen()
        {
            if (_spreadSamples.Count < 10)
                return false;

            var current = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;
            var average = _spreadSamples.Average(x => x.Pips);
            if (average <= 0 || current <= SpreadFreezeMultiple * average)
                return false;

            Log("SPREAD_FREEZE", "Current={0:F2}p Avg60m={1:F2}p Mult={2:F1}", current, average, SpreadFreezeMultiple);
            return true;
        }

        private void SampleSpread()
        {
            var minute = new DateTime(Server.Time.Year, Server.Time.Month, Server.Time.Day,
                Server.Time.Hour, Server.Time.Minute, 0, DateTimeKind.Utc);

            if (minute == _lastSpreadSampleMinute)
                return;

            _lastSpreadSampleMinute = minute;
            var pips = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;
            if (IsFinitePositive(pips))
                _spreadSamples.Enqueue(new SpreadSample { Time = minute, Pips = pips });

            while (_spreadSamples.Count > 0 && minute - _spreadSamples.Peek().Time > TimeSpan.FromMinutes(60))
                _spreadSamples.Dequeue();
        }

        private int CountBotExposures()
        {
            var positions = Positions.FindAll(BotLabel).Length;
            var orders = PendingOrders.Count(o => o.Label == BotLabel);
            return positions + orders;
        }

        private bool HasExposureOnSymbol()
        {
            if (Positions.FindAll(BotLabel, SymbolName).Length > 0)
                return true;
            return PendingOrders.Any(o => o.Label == BotLabel && o.SymbolName == SymbolName);
        }

        private double ParseInitialVolume(string comment, double fallback)
        {
            if (string.IsNullOrWhiteSpace(comment))
                return fallback;

            const string marker = "INITVOL=";
            var start = comment.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return fallback;

            start += marker.Length;
            var end = comment.IndexOf(';', start);
            var text = end < 0 ? comment.Substring(start) : comment.Substring(start, end - start);

            double value;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0
                ? value
                : fallback;
        }

        private int ResolveLastClosedIndex()
        {
            if (_m5 == null || _m5.Count == 0)
                return -1;

            var now = AsUtc(Server.Time);
            var lower = Math.Max(0, _m5.Count - 10);
            for (var i = _m5.Count - 1; i >= lower; i--)
            {
                if (AsUtc(_m5.OpenTimes[i]) + BarSpan <= now)
                    return i;
            }

            return -1;
        }

        private void EnsureHistory(int minimumBars)
        {
            var guard = 0;
            while (_m5.Count < minimumBars && guard++ < 20)
            {
                var loaded = _m5.LoadMoreHistory();
                if (loaded <= 0)
                    break;
            }
        }

        private void ResetSignal(string reason)
        {
            if (_signal != null)
                Log("SIGNAL_RESET", "{0} | Dir={1} Stage={2}", reason, _signal.Direction, _signal.Stage);
            _signal = null;
        }

        private void Log(string evt, string format, params object[] args)
        {
            if (_replaying && evt != "EXTENSION" && evt != "REENTRY")
                return;
            Print("[{0}] {1} | {2}", BotLabel, evt, string.Format(CultureInfo.InvariantCulture, format, args));
        }

        private static DateTime AsUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
                return value;
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinitePositive(double value)
        {
            return IsFinite(value) && value > 0.0;
        }
    }
}
