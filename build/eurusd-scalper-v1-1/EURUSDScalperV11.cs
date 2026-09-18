using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class EURUSDScalperV11 : Robot
    {
        private const string StrategyVersion = "EURUSD_SCALPER_V1_1";
        private const string BotLabel = "EURUSD_SCALPER_V1_1";

        private const int EmaFast = 20;
        private const int EmaSlow = 50;
        private const int AdxPeriod = 14;
        private const double AdxThreshold = 25.0;
        private const int AtrPeriod = 14;
        private const double StopAtrMultiplier = 1.0;
        private const double PullbackMinAtr = 0.15;
        private const double PullbackMaxAtr = 0.50;
        private const int ConfirmationMaxBars = 2;
        private const double RiskPercent = 0.005;
        private const double TakeProfitR = 1.75;
        private const double BreakEvenTriggerR = 1.00;
        private const double BreakEvenOffsetR = 0.00;
        private const double TrailingTriggerR = 1.25;
        private const double TrailingAtrMultiplier = 1.00;
        private const int MaxBarsInTrade = 6;
        private const int CooldownBars = 2;
        private const double MaxSpread = 0.00020;
        private const int NewsBeforeMinutes = 5;
        private const int NewsAfterMinutes = 5;
        private const double DailyLossLimit = 0.02;

        [Parameter("News Feed Available", DefaultValue = false, Group = "News / Dataset")]
        public bool NewsFeedAvailable { get; set; }

        [Parameter("HIGH EUR/USD events UTC", DefaultValue = "", Group = "News / Dataset")]
        public string HighImpactNewsUtc { get; set; }

        [Parameter("Verbose Logging", DefaultValue = true, Group = "Diagnostics")]
        public bool VerboseLogging { get; set; }

        private enum BotState
        {
            IDLE,
            WAITING_PULLBACK,
            WAITING_CONFIRMATION,
            POSITION_OPEN,
            COOLDOWN
        }

        private enum SetupSide
        {
            NONE,
            LONG,
            SHORT
        }

        private enum MarketRegime
        {
            NEUTRAL,
            LONG,
            SHORT
        }

        private enum ExitReasonCode
        {
            STOP_LOSS,
            TAKE_PROFIT,
            TIME_STOP,
            DAILY_STOP,
            MANUAL_TEST_ABORT
        }

        private Bars _m15Bars;
        private ExponentialMovingAverage _ema20M5;
        private ExponentialMovingAverage _ema50M5;
        private ExponentialMovingAverage _ema20M15;
        private ExponentialMovingAverage _ema50M15;
        private DirectionalMovementSystem _dmsM15;
        private AverageTrueRange _atrM5;

        private BotState State;
        private SetupSide SetupDirection;
        private string CandidateSwingId;
        private double CandidateSwingPrice;
        private DateTime CandidateSwingTime;
        private int _candidateSwingIndex = -1;
        private string FrozenSwingId;
        private double FrozenSwingPrice;
        private double FrozenPullbackATR;
        private double ATRAtPullback;
        private double ATRAtSignal;
        private int ConfirmationBarsElapsed;
        private int BarsInTrade;
        private int CooldownBarsElapsed;
        private double EntryRequested;
        private double EntryFilled;
        private double InitialRiskDistance;
        private double InitialStopLoss;
        private double InitialTakeProfit;
        private bool BreakEvenApplied;
        private bool TrailingActivated;
        private double MFEPrice;
        private double MFE_R;
        private double MAEPrice;
        private double MAE_R;
        private string LastConsumedSwingId;
        private double DayStartEquity;
        private double DailyDrawdown;
        private bool DailyLocked;

        private DateTime _dailyBaselineDateUtc;
        private Position _position;
        private int _positionId = -1;
        private ExitReasonCode? _pendingManualExitReason;
        private bool _closingPosition;
        private bool _volumeCapped;
        private double _riskAmount;
        private double _entrySpread;
        private DateTime _setupTimestamp;
        private DateTime _entryTimestamp;
        private string _setupId;
        private string _entrySession;
        private double _ema20M15AtEntry;
        private double _ema50M15AtEntry;
        private double _adxM15AtEntry;
        private double _ema20M5AtEntry;
        private double _ema50M5AtEntry;
        private double _entrySlippage;
        private int _regimeM15Index = -1;
        private MarketRegime _regime = MarketRegime.NEUTRAL;
        private readonly List<DateTime> _highImpactNewsEvents = new List<DateTime>();
        private bool _newsCalendarParsedSuccessfully;

        private readonly List<double> _closedTradeRs = new List<double>();
        private readonly List<double> _closedTradeMfeRs = new List<double>();
        private readonly List<double> _closedTradeMaeRs = new List<double>();
        private int _totalTrades;
        private int _winningTrades;
        private double _grossProfitSum;
        private double _grossLossAbsSum;
        private double _netPnlSum;

        protected override void OnStart()
        {
            State = BotState.IDLE;
            SetupDirection = SetupSide.NONE;
            DailyLocked = false;

            if (!string.Equals(SymbolName, "EURUSD", StringComparison.OrdinalIgnoreCase))
            {
                Print("FATAL|{0}|Symbol must be EURUSD. Current={1}", StrategyVersion, SymbolName);
                Stop();
                return;
            }

            if (TimeFrame != cAlgo.API.TimeFrame.Minute5)
            {
                Print("FATAL|{0}|Execution timeframe must be M5. Current={1}", StrategyVersion, TimeFrame);
                Stop();
                return;
            }

            if (Positions.Find(BotLabel, SymbolName) != null)
            {
                Print("FATAL|{0}|Existing bot-labelled EURUSD position detected. Close it before starting V1.1; state recovery is intentionally fail-closed.", StrategyVersion);
                Stop();
                return;
            }

            _m15Bars = MarketData.GetBars(cAlgo.API.TimeFrame.Minute15, SymbolName);
            _ema20M5 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, EmaFast);
            _ema50M5 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, EmaSlow);
            _ema20M15 = Indicators.ExponentialMovingAverage(_m15Bars.ClosePrices, EmaFast);
            _ema50M15 = Indicators.ExponentialMovingAverage(_m15Bars.ClosePrices, EmaSlow);
            _dmsM15 = Indicators.DirectionalMovementSystem(_m15Bars, AdxPeriod);
            _atrM5 = Indicators.AverageTrueRange(Bars, AtrPeriod, MovingAverageType.Exponential);

            Positions.Closed += OnPositionClosed;
            ParseNewsCalendar();
            ResetDailyBaseline(Server.Time);

            Print("START|strategy={0}|symbol={1}|tf=M5|context=M15|news_feed_available={2}|news_events={3}",
                StrategyVersion, SymbolName, NewsFeedAvailable && _newsCalendarParsedSuccessfully, _highImpactNewsEvents.Count);

            if (!NewsFeedAvailable || !_newsCalendarParsedSuccessfully)
                Print("NEWS_BLOCK|V1.1 FAIL_CLOSED active: new entries are blocked until News Feed Available=true and the frozen calendar input parses successfully.");
        }

        protected override void OnStop()
        {
            Positions.Closed -= OnPositionClosed;
            PrintSummaryMetrics();
        }

        protected override void OnTick()
        {
            UpdateDailyState(Server.Time);

            if (DailyLocked)
            {
                CancelPendingSetup("DAILY_LOCKED");
                var position = GetOwnPosition();
                if (position != null && !_closingPosition)
                    RequestClose(position, ExitReasonCode.DAILY_STOP);
                return;
            }

            var openPosition = GetOwnPosition();
            if (openPosition != null)
                UpdateMfeMae(openPosition);
        }

        protected override void OnBarClosed()
        {
            if (Bars.Count < EmaSlow + 10 || _m15Bars == null || _m15Bars.Count < EmaSlow + 10)
                return;

            int signalIndex = Bars.Count - 1;
            if (signalIndex < 4)
                return;

            DateTime signalCloseTime = Bars.OpenTimes[signalIndex].AddMinutes(5);
            UpdateDailyState(signalCloseTime);
            RefreshRegime(signalCloseTime);

            if (DailyLocked)
            {
                CancelPendingSetup("DAILY_LOCKED");
                return;
            }

            switch (State)
            {
                case BotState.IDLE:
                    if (!SessionAllowed(signalCloseTime))
                        return;
                    TryCreateCandidateSetup(signalIndex, signalCloseTime);
                    break;

                case BotState.WAITING_PULLBACK:
                    if (!SessionAllowed(signalCloseTime) || !RegimeMatchesSetup() || !M5DirectionMatchesSetup(signalIndex))
                    {
                        CancelSetup("PULLBACK_INVALIDATED");
                        break;
                    }

                    UpdatePullbackAndArmIfValid(signalIndex, signalCloseTime);
                    break;

                case BotState.WAITING_CONFIRMATION:
                    ConfirmationBarsElapsed++;

                    if (!SessionAllowed(signalCloseTime) || !RegimeMatchesSetup() || !M5DirectionMatchesSetup(signalIndex))
                    {
                        CancelSetup("CONFIRMATION_INVALIDATED");
                        break;
                    }

                    if (ConfirmationBarsElapsed > ConfirmationMaxBars)
                    {
                        CancelSetup("CONFIRMATION_EXPIRED");
                        break;
                    }

                    if (!BreakoutConfirmed(signalIndex))
                        break;

                    if (!NewsAllowed(signalCloseTime))
                    {
                        LogVerbose("ENTRY_BLOCKED|setup={0}|bar={1}|reason=NEWS|confirmation={2}", _setupId, signalCloseTime.ToString("O"), ConfirmationBarsElapsed);
                        break;
                    }

                    if (!SpreadAllowed())
                    {
                        LogVerbose("ENTRY_BLOCKED|setup={0}|bar={1}|reason=SPREAD|spread={2:F5}|confirmation={3}", _setupId, signalCloseTime.ToString("O"), Symbol.Spread, ConfirmationBarsElapsed);
                        break;
                    }

                    ATRAtSignal = _atrM5.Result[signalIndex];
                    EntryRequested = Bars.ClosePrices[signalIndex];
                    TryOpenPosition(signalIndex, signalCloseTime);
                    break;

                case BotState.POSITION_OPEN:
                    BarsInTrade++;
                    var position = GetOwnPosition();
                    if (position == null)
                    {
                        if (State == BotState.POSITION_OPEN)
                            EnterCooldown("POSITION_NOT_FOUND");
                        break;
                    }

                    if (BarsInTrade >= MaxBarsInTrade)
                    {
                        RequestClose(position, ExitReasonCode.TIME_STOP);
                        break;
                    }

                    ApplyBreakEvenIfNeeded(position);
                    if (GetOwnPosition() == null)
                        break;
                    ApplyTrailingIfNeeded(position, signalIndex);
                    break;

                case BotState.COOLDOWN:
                    CooldownBarsElapsed++;
                    if (CooldownBarsElapsed >= CooldownBars)
                    {
                        ResetSetupVariables();
                        State = BotState.IDLE;
                        LogVerbose("STATE|COOLDOWN->IDLE|time={0}", signalCloseTime.ToString("O"));
                    }
                    break;
            }
        }

        private void RefreshRegime(DateTime signalCloseTime)
        {
            int index = FindLastClosedM15Index(signalCloseTime);
            _regimeM15Index = index;
            if (index < EmaSlow)
            {
                _regime = MarketRegime.NEUTRAL;
                return;
            }

            double fast = _ema20M15.Result[index];
            double slow = _ema50M15.Result[index];
            double adx = _dmsM15.ADX[index];

            if (fast > slow && adx > AdxThreshold)
                _regime = MarketRegime.LONG;
            else if (fast < slow && adx > AdxThreshold)
                _regime = MarketRegime.SHORT;
            else
                _regime = MarketRegime.NEUTRAL;
        }

        private int FindLastClosedM15Index(DateTime signalCloseTime)
        {
            for (int i = _m15Bars.Count - 1; i >= 0; i--)
            {
                if (_m15Bars.OpenTimes[i].AddMinutes(15) <= signalCloseTime)
                    return i;
            }
            return -1;
        }

        private void TryCreateCandidateSetup(int signalIndex, DateTime signalCloseTime)
        {
            if (_regime == MarketRegime.NEUTRAL)
                return;

            if (_regime == MarketRegime.LONG && !IsM5Long(signalIndex))
                return;
            if (_regime == MarketRegime.SHORT && !IsM5Short(signalIndex))
                return;

            int k = signalIndex - 2;
            if (k < 2 || k + 2 > signalIndex)
                return;

            if (_regime == MarketRegime.LONG && IsSwingHigh(k))
            {
                string id = BuildSwingId(SetupSide.LONG, k, Bars.HighPrices[k]);
                if (id == LastConsumedSwingId)
                    return;
                CreateCandidate(SetupSide.LONG, id, Bars.HighPrices[k], Bars.OpenTimes[k], k, signalCloseTime);
            }
            else if (_regime == MarketRegime.SHORT && IsSwingLow(k))
            {
                string id = BuildSwingId(SetupSide.SHORT, k, Bars.LowPrices[k]);
                if (id == LastConsumedSwingId)
                    return;
                CreateCandidate(SetupSide.SHORT, id, Bars.LowPrices[k], Bars.OpenTimes[k], k, signalCloseTime);
            }
        }

        private void CreateCandidate(SetupSide side, string swingId, double price, DateTime swingTime, int swingIndex, DateTime setupTime)
        {
            SetupDirection = side;
            CandidateSwingId = swingId;
            CandidateSwingPrice = price;
            CandidateSwingTime = swingTime;
            _candidateSwingIndex = swingIndex;
            _setupTimestamp = setupTime;
            _setupId = string.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2:yyyyMMddHHmmss}", StrategyVersion, side, setupTime);
            State = BotState.WAITING_PULLBACK;

            LogVerbose("SETUP|id={0}|state=WAITING_PULLBACK|direction={1}|swing_id={2}|swing_price={3}|swing_time={4:O}",
                _setupId, side, swingId, price, swingTime);
        }

        private void UpdatePullbackAndArmIfValid(int signalIndex, DateTime signalCloseTime)
        {
            if (_candidateSwingIndex < 0 || signalIndex <= _candidateSwingIndex)
                return;

            double atr = _atrM5.Result[signalIndex];
            if (!(atr > 0) || double.IsNaN(atr))
                return;

            int start = _candidateSwingIndex + 1;
            double distance;

            if (SetupDirection == SetupSide.LONG)
            {
                double pullbackLow = double.PositiveInfinity;
                for (int i = start; i <= signalIndex; i++)
                    pullbackLow = Math.Min(pullbackLow, Bars.LowPrices[i]);
                distance = CandidateSwingPrice - pullbackLow;
            }
            else if (SetupDirection == SetupSide.SHORT)
            {
                double pullbackHigh = double.NegativeInfinity;
                for (int i = start; i <= signalIndex; i++)
                    pullbackHigh = Math.Max(pullbackHigh, Bars.HighPrices[i]);
                distance = pullbackHigh - CandidateSwingPrice;
            }
            else
            {
                return;
            }

            double pullbackAtr = distance / atr;
            LogVerbose("PULLBACK|setup={0}|time={1:O}|distance={2}|atr={3}|ratio={4:F4}", _setupId, signalCloseTime, distance, atr, pullbackAtr);

            if (pullbackAtr < PullbackMinAtr || pullbackAtr > PullbackMaxAtr)
                return;

            FrozenSwingPrice = CandidateSwingPrice;
            FrozenSwingId = CandidateSwingId;
            ATRAtPullback = atr;
            FrozenPullbackATR = pullbackAtr;
            ConfirmationBarsElapsed = 0;
            State = BotState.WAITING_CONFIRMATION;

            LogVerbose("STATE|WAITING_PULLBACK->WAITING_CONFIRMATION|setup={0}|time={1:O}|frozen_swing={2}|pullback_atr={3:F4}|atr_at_pullback={4}",
                _setupId, signalCloseTime, FrozenSwingPrice, FrozenPullbackATR, ATRAtPullback);
        }

        private bool BreakoutConfirmed(int signalIndex)
        {
            double close = Bars.ClosePrices[signalIndex];
            if (SetupDirection == SetupSide.LONG)
                return close > FrozenSwingPrice;
            if (SetupDirection == SetupSide.SHORT)
                return close < FrozenSwingPrice;
            return false;
        }

        private void TryOpenPosition(int signalIndex, DateTime signalCloseTime)
        {
            if (GetOwnPosition() != null)
                return;

            if (!(ATRAtSignal > 0) || double.IsNaN(ATRAtSignal))
            {
                CancelSetup("INVALID_ATR_AT_SIGNAL");
                return;
            }

            double stopPips = (ATRAtSignal * StopAtrMultiplier) / Symbol.PipSize;
            if (!(stopPips > 0))
            {
                CancelSetup("INVALID_STOP_PIPS");
                return;
            }

            _riskAmount = Account.Balance * RiskPercent;
            double rawVolume = Symbol.VolumeForFixedRisk(_riskAmount, stopPips, RoundingMode.Down);
            double volume = Symbol.NormalizeVolumeInUnits(rawVolume, RoundingMode.Down);
            _volumeCapped = false;

            if (volume > Symbol.VolumeInUnitsMax)
            {
                volume = Symbol.NormalizeVolumeInUnits(Symbol.VolumeInUnitsMax, RoundingMode.Down);
                _volumeCapped = true;
            }

            while (volume >= Symbol.VolumeInUnitsMin && Symbol.AmountRisked(volume, stopPips) > _riskAmount + 1e-8)
                volume = Symbol.NormalizeVolumeInUnits(volume - Symbol.VolumeInUnitsStep, RoundingMode.Down);

            if (volume < Symbol.VolumeInUnitsMin)
            {
                LogVerbose("ENTRY_CANCELLED|setup={0}|reason=VOLUME_BELOW_MIN|risk_amount={1:F2}|stop_pips={2:F2}|raw_volume={3}",
                    _setupId, _riskAmount, stopPips, rawVolume);
                CancelSetup("VOLUME_BELOW_MIN");
                return;
            }

            double takeProfitPips = stopPips * TakeProfitR;
            TradeType tradeType = SetupDirection == SetupSide.LONG ? TradeType.Buy : TradeType.Sell;
            _entrySpread = Symbol.Spread;
            _entrySession = GetSession(signalCloseTime);
            _entryTimestamp = signalCloseTime;

            CaptureEntryIndicators(signalIndex);

            TradeResult result = ExecuteMarketOrder(tradeType, SymbolName, volume, BotLabel, stopPips, takeProfitPips);
            if (!result.IsSuccessful || result.Position == null)
            {
                LogVerbose("ENTRY_FAILED|setup={0}|error={1}", _setupId, result.Error);
                CancelSetup("ORDER_REJECTED");
                return;
            }

            _position = result.Position;
            _positionId = _position.Id;
            EntryFilled = _position.EntryPrice;
            InitialRiskDistance = ATRAtSignal * StopAtrMultiplier;
            InitialStopLoss = SetupDirection == SetupSide.LONG
                ? NormalizeProtectiveStop(EntryFilled - InitialRiskDistance, SetupSide.LONG)
                : NormalizeProtectiveStop(EntryFilled + InitialRiskDistance, SetupSide.SHORT);
            InitialTakeProfit = SetupDirection == SetupSide.LONG
                ? NormalizeTakeProfit(EntryFilled + InitialRiskDistance * TakeProfitR, SetupSide.LONG)
                : NormalizeTakeProfit(EntryFilled - InitialRiskDistance * TakeProfitR, SetupSide.SHORT);

            TradeResult slResult = _position.ModifyStopLossPrice(InitialStopLoss);
            TradeResult tpResult = _position.ModifyTakeProfitPrice(InitialTakeProfit);
            if (!slResult.IsSuccessful || !tpResult.IsSuccessful)
            {
                Print("PROTECTION_FAILURE|position={0}|sl_ok={1}|sl_error={2}|tp_ok={3}|tp_error={4}|action=EMERGENCY_CLOSE",
                    _position.Id, slResult.IsSuccessful, slResult.Error, tpResult.IsSuccessful, tpResult.Error);
                _pendingManualExitReason = ExitReasonCode.MANUAL_TEST_ABORT;
                _closingPosition = true;
                ClosePosition(_position);
                return;
            }

            _entrySlippage = SetupDirection == SetupSide.LONG
                ? EntryFilled - EntryRequested
                : EntryRequested - EntryFilled;

            LastConsumedSwingId = FrozenSwingId;
            BreakEvenApplied = false;
            TrailingActivated = false;
            BarsInTrade = 0;
            MFEPrice = EntryFilled;
            MAEPrice = EntryFilled;
            MFE_R = 0;
            MAE_R = 0;
            State = BotState.POSITION_OPEN;

            Print("ENTRY|setup={0}|position={1}|direction={2}|time={3:O}|requested={4}|filled={5}|slippage={6}|sl={7}|tp={8}|risk_distance={9}|risk_pips={10:F2}|risk_amount={11:F2}|volume_units={12}|volume_lots={13:F4}|volume_capped={14}|spread={15:F5}|session={16}|swing_id={17}",
                _setupId, _position.Id, SetupDirection, _entryTimestamp, EntryRequested, EntryFilled, _entrySlippage,
                InitialStopLoss, InitialTakeProfit, InitialRiskDistance, stopPips, _riskAmount, _position.VolumeInUnits,
                _position.Quantity, _volumeCapped, _entrySpread, _entrySession, FrozenSwingId);
        }

        private void CaptureEntryIndicators(int signalIndex)
        {
            _ema20M5AtEntry = _ema20M5.Result[signalIndex];
            _ema50M5AtEntry = _ema50M5.Result[signalIndex];

            if (_regimeM15Index >= 0)
            {
                _ema20M15AtEntry = _ema20M15.Result[_regimeM15Index];
                _ema50M15AtEntry = _ema50M15.Result[_regimeM15Index];
                _adxM15AtEntry = _dmsM15.ADX[_regimeM15Index];
            }
        }

        private void UpdateMfeMae(Position position)
        {
            if (!(InitialRiskDistance > 0))
                return;

            if (position.TradeType == TradeType.Buy)
            {
                MFEPrice = Math.Max(MFEPrice, Symbol.Bid);
                MAEPrice = Math.Min(MAEPrice, Symbol.Bid);
                MFE_R = Math.Max(0, (MFEPrice - EntryFilled) / InitialRiskDistance);
                MAE_R = Math.Max(0, (EntryFilled - MAEPrice) / InitialRiskDistance);
            }
            else
            {
                MFEPrice = Math.Min(MFEPrice, Symbol.Ask);
                MAEPrice = Math.Max(MAEPrice, Symbol.Ask);
                MFE_R = Math.Max(0, (EntryFilled - MFEPrice) / InitialRiskDistance);
                MAE_R = Math.Max(0, (MAEPrice - EntryFilled) / InitialRiskDistance);
            }
        }

        private void ApplyBreakEvenIfNeeded(Position position)
        {
            if (BreakEvenApplied || MFE_R < BreakEvenTriggerR)
                return;

            double offset = InitialRiskDistance * BreakEvenOffsetR;
            double requested = position.TradeType == TradeType.Buy
                ? NormalizeProtectiveStop(EntryFilled + offset, SetupSide.LONG)
                : NormalizeProtectiveStop(EntryFilled - offset, SetupSide.SHORT);

            double current = position.StopLoss ?? InitialStopLoss;
            double target = position.TradeType == TradeType.Buy ? Math.Max(current, requested) : Math.Min(current, requested);

            TradeResult result = position.ModifyStopLossPrice(target);
            if (result.IsSuccessful)
            {
                BreakEvenApplied = true;
                LogVerbose("BREAK_EVEN|position={0}|mfe_r={1:F4}|new_sl={2}", position.Id, MFE_R, target);
            }
            else
            {
                LogVerbose("BREAK_EVEN_REJECTED|position={0}|mfe_r={1:F4}|requested_sl={2}|error={3}", position.Id, MFE_R, target, result.Error);
            }
        }

        private void ApplyTrailingIfNeeded(Position position, int signalIndex)
        {
            if (MFE_R < TrailingTriggerR)
                return;

            TrailingActivated = true;
            double atr = _atrM5.Result[signalIndex];
            if (!(atr > 0) || double.IsNaN(atr))
                return;

            double current = position.StopLoss ?? InitialStopLoss;
            double candidate;
            double newSl;

            if (position.TradeType == TradeType.Buy)
            {
                candidate = Bars.HighPrices[signalIndex] - atr * TrailingAtrMultiplier;
                candidate = NormalizeProtectiveStop(candidate, SetupSide.LONG);
                newSl = Math.Max(current, candidate);
                if (newSl <= current + Symbol.TickSize * 0.1)
                    return;
            }
            else
            {
                candidate = Bars.LowPrices[signalIndex] + atr * TrailingAtrMultiplier;
                candidate = NormalizeProtectiveStop(candidate, SetupSide.SHORT);
                newSl = Math.Min(current, candidate);
                if (newSl >= current - Symbol.TickSize * 0.1)
                    return;
            }

            TradeResult result = position.ModifyStopLossPrice(newSl);
            if (!result.IsSuccessful)
            {
                Print("TrailingModificationRejected|position={0}|requested_sl={1}|current_sl={2}|error={3}", position.Id, newSl, current, result.Error);
                return;
            }

            LogVerbose("TRAILING|position={0}|mfe_r={1:F4}|candidate={2}|new_sl={3}|atr={4}", position.Id, MFE_R, candidate, newSl, atr);
        }

        private void UpdateDailyState(DateTime utcNow)
        {
            DateTime date = utcNow.Date;
            if (_dailyBaselineDateUtc != date)
                ResetDailyBaseline(utcNow);

            if (DayStartEquity <= 0)
                return;

            DailyDrawdown = (DayStartEquity - Account.Equity) / DayStartEquity;
            if (!DailyLocked && DailyDrawdown >= DailyLossLimit)
            {
                DailyLocked = true;
                Print("DAILY_STOP_TRIGGERED|time={0:O}|baseline={1:F2}|equity={2:F2}|drawdown={3:P4}", utcNow, DayStartEquity, Account.Equity, DailyDrawdown);
                CancelPendingSetup("DAILY_STOP");

                var position = GetOwnPosition();
                if (position != null && !_closingPosition)
                    RequestClose(position, ExitReasonCode.DAILY_STOP);
            }
        }

        private void ResetDailyBaseline(DateTime utcNow)
        {
            _dailyBaselineDateUtc = utcNow.Date;
            DayStartEquity = Account.Equity;
            DailyDrawdown = 0;
            DailyLocked = false;
            Print("DAILY_RESET|date={0:yyyy-MM-dd}|day_start_equity={1:F2}", _dailyBaselineDateUtc, DayStartEquity);
        }

        private bool NewsAllowed(DateTime timeUtc)
        {
            if (!NewsFeedAvailable || !_newsCalendarParsedSuccessfully)
                return false;

            foreach (DateTime news in _highImpactNewsEvents)
            {
                DateTime from = news.AddMinutes(-NewsBeforeMinutes);
                DateTime to = news.AddMinutes(NewsAfterMinutes);
                if (timeUtc >= from && timeUtc <= to)
                    return false;
            }
            return true;
        }

        private void ParseNewsCalendar()
        {
            _highImpactNewsEvents.Clear();
            _newsCalendarParsedSuccessfully = false;

            if (!NewsFeedAvailable)
                return;

            if (string.IsNullOrWhiteSpace(HighImpactNewsUtc))
            {
                _newsCalendarParsedSuccessfully = true;
                return;
            }

            string[] formats =
            {
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd'T'HH:mm",
                "yyyy-MM-dd'T'HH:mm:ss",
                "O"
            };

            foreach (string token in HighImpactNewsUtc.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string value = token.Trim();
                DateTime parsed;
                if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
                {
                    Print("NEWS_PARSE_ERROR|value={0}|policy=BLOCK_ENTRIES", value);
                    return;
                }
                _highImpactNewsEvents.Add(DateTime.SpecifyKind(parsed, DateTimeKind.Utc));
            }

            _highImpactNewsEvents.Sort();
            _newsCalendarParsedSuccessfully = true;
        }

        private bool SpreadAllowed()
        {
            return Symbol.Spread <= MaxSpread;
        }

        private bool SessionAllowed(DateTime utc)
        {
            TimeSpan t = utc.TimeOfDay;
            return t >= TimeSpan.Zero && t < TimeSpan.FromHours(21);
        }

        private string GetSession(DateTime utc)
        {
            int hour = utc.Hour;
            if (hour < 8) return "ASIA";
            if (hour < 13) return "LONDON";
            if (hour < 21) return "NEW_YORK";
            return "NO_ENTRY";
        }

        private bool RegimeMatchesSetup()
        {
            return (SetupDirection == SetupSide.LONG && _regime == MarketRegime.LONG) ||
                   (SetupDirection == SetupSide.SHORT && _regime == MarketRegime.SHORT);
        }

        private bool M5DirectionMatchesSetup(int index)
        {
            return (SetupDirection == SetupSide.LONG && IsM5Long(index)) ||
                   (SetupDirection == SetupSide.SHORT && IsM5Short(index));
        }

        private bool IsM5Long(int index)
        {
            return _ema20M5.Result[index] > _ema50M5.Result[index];
        }

        private bool IsM5Short(int index)
        {
            return _ema20M5.Result[index] < _ema50M5.Result[index];
        }

        private bool IsSwingHigh(int k)
        {
            return Bars.HighPrices[k] > Bars.HighPrices[k - 1] &&
                   Bars.HighPrices[k] > Bars.HighPrices[k - 2] &&
                   Bars.HighPrices[k] > Bars.HighPrices[k + 1] &&
                   Bars.HighPrices[k] > Bars.HighPrices[k + 2];
        }

        private bool IsSwingLow(int k)
        {
            return Bars.LowPrices[k] < Bars.LowPrices[k - 1] &&
                   Bars.LowPrices[k] < Bars.LowPrices[k - 2] &&
                   Bars.LowPrices[k] < Bars.LowPrices[k + 1] &&
                   Bars.LowPrices[k] < Bars.LowPrices[k + 2];
        }

        private string BuildSwingId(SetupSide side, int index, double price)
        {
            string p = price.ToString("F" + Symbol.Digits, CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2:yyyyMMddHHmmss}-{3}",
                SymbolName, side, Bars.OpenTimes[index], p);
        }

        private Position GetOwnPosition()
        {
            if (_position != null && _position.Id == _positionId)
            {
                var found = Positions.FirstOrDefault(p => p.Id == _positionId);
                if (found != null)
                    return found;
            }

            var byLabel = Positions.Find(BotLabel, SymbolName);
            if (byLabel != null)
            {
                _position = byLabel;
                _positionId = byLabel.Id;
            }
            return byLabel;
        }

        private void RequestClose(Position position, ExitReasonCode reason)
        {
            if (_closingPosition)
                return;

            _pendingManualExitReason = reason;
            _closingPosition = true;
            TradeResult result = ClosePosition(position);
            if (!result.IsSuccessful)
            {
                _closingPosition = false;
                Print("CLOSE_FAILED|position={0}|reason={1}|error={2}", position.Id, reason, result.Error);
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position == null || args.Position.Id != _positionId || args.Position.Label != BotLabel)
                return;

            ExitReasonCode reason;
            if (_pendingManualExitReason.HasValue)
            {
                reason = _pendingManualExitReason.Value;
            }
            else if (args.Reason == PositionCloseReason.StopLoss)
            {
                reason = ExitReasonCode.STOP_LOSS;
            }
            else if (args.Reason == PositionCloseReason.TakeProfit)
            {
                reason = ExitReasonCode.TAKE_PROFIT;
            }
            else
            {
                reason = ExitReasonCode.MANUAL_TEST_ABORT;
            }

            HistoricalTrade[] closedDeals = History.FindByPositionId(args.Position.Id);
            HistoricalTrade trade = closedDeals == null ? null : closedDeals.OrderByDescending(x => x.ClosingTime).FirstOrDefault();

            DateTime exitTime = trade != null ? trade.ClosingTime : Server.Time;
            double exitPrice = trade != null ? trade.ClosingPrice : args.Position.CurrentPrice;
            double grossPnl = trade != null ? trade.GrossProfit : args.Position.GrossProfit;
            double commission = trade != null ? trade.Commissions : args.Position.Commissions;
            double swap = trade != null ? trade.Swap : args.Position.Swap;
            double netPnl = trade != null ? trade.NetProfit : args.Position.NetProfit;
            double costs = grossPnl - netPnl;
            double pnlR = _riskAmount > 0 ? netPnl / _riskAmount : 0;

            _totalTrades++;
            if (netPnl > 0) _winningTrades++;
            if (grossPnl > 0) _grossProfitSum += grossPnl;
            if (grossPnl < 0) _grossLossAbsSum += Math.Abs(grossPnl);
            _netPnlSum += netPnl;
            _closedTradeRs.Add(pnlR);
            _closedTradeMfeRs.Add(MFE_R);
            _closedTradeMaeRs.Add(MAE_R);

            Print("TRADE_LOG|timestamp_setup={0:O}|timestamp_entry={1:O}|timestamp_exit={2:O}|symbol={3}|direction={4}|session={5}|swing_id={6}|swing_price={7}|pullback_atr={8:F6}|atr_at_pullback={9}|atr_at_signal={10}|ema20_m15={11}|ema50_m15={12}|adx14_m15={13}|ema20_m5={14}|ema50_m5={15}|spread_entry={16}|entry_requested={17}|entry_filled={18}|entry_slippage={19}|initial_stop={20}|initial_tp={21}|initial_risk_pips={22:F4}|volume_lots={23:F6}|volume_units={24}|risk_amount={25:F2}|mfe_r={26:F6}|mae_r={27:F6}|be_applied={28}|trailing_activated={29}|bars_in_trade={30}|exit_price={31}|exit_reason={32}|gross_pnl={33:F2}|commission={34:F2}|swap={35:F2}|costs={36:F2}|net_pnl={37:F2}|pnl_r={38:F6}",
                _setupTimestamp, _entryTimestamp, exitTime, SymbolName, SetupDirection, _entrySession, FrozenSwingId,
                FrozenSwingPrice, FrozenPullbackATR, ATRAtPullback, ATRAtSignal, _ema20M15AtEntry, _ema50M15AtEntry,
                _adxM15AtEntry, _ema20M5AtEntry, _ema50M5AtEntry, _entrySpread, EntryRequested, EntryFilled, _entrySlippage,
                InitialStopLoss, InitialTakeProfit, InitialRiskDistance / Symbol.PipSize, args.Position.Quantity,
                args.Position.VolumeInUnits, _riskAmount, MFE_R, MAE_R, BreakEvenApplied, TrailingActivated, BarsInTrade,
                exitPrice, reason, grossPnl, commission, swap, costs, netPnl, pnlR);

            _position = null;
            _positionId = -1;
            _pendingManualExitReason = null;
            _closingPosition = false;
            EnterCooldown(reason.ToString());
        }

        private double NormalizeProtectiveStop(double price, SetupSide side)
        {
            if (Symbol.TickSize <= 0)
                return Math.Round(price, Symbol.Digits, MidpointRounding.AwayFromZero);

            double ticks = price / Symbol.TickSize;
            double normalizedTicks = side == SetupSide.LONG ? Math.Ceiling(ticks - 1e-10) : Math.Floor(ticks + 1e-10);
            return Math.Round(normalizedTicks * Symbol.TickSize, Symbol.Digits, MidpointRounding.AwayFromZero);
        }

        private double NormalizeTakeProfit(double price, SetupSide side)
        {
            if (Symbol.TickSize <= 0)
                return Math.Round(price, Symbol.Digits, MidpointRounding.AwayFromZero);

            double ticks = price / Symbol.TickSize;
            double normalizedTicks = side == SetupSide.LONG ? Math.Floor(ticks + 1e-10) : Math.Ceiling(ticks - 1e-10);
            return Math.Round(normalizedTicks * Symbol.TickSize, Symbol.Digits, MidpointRounding.AwayFromZero);
        }

        private void CancelPendingSetup(string reason)
        {
            if (State == BotState.WAITING_PULLBACK || State == BotState.WAITING_CONFIRMATION)
                CancelSetup(reason);
        }

        private void CancelSetup(string reason)
        {
            LogVerbose("SETUP_CANCEL|setup={0}|state={1}|reason={2}", _setupId, State, reason);
            ResetSetupVariables();
            State = BotState.IDLE;
        }

        private void EnterCooldown(string reason)
        {
            State = BotState.COOLDOWN;
            CooldownBarsElapsed = 0;
            SetupDirection = SetupSide.NONE;
            LogVerbose("STATE|->COOLDOWN|reason={0}", reason);
        }

        private void ResetSetupVariables()
        {
            SetupDirection = SetupSide.NONE;
            CandidateSwingId = null;
            CandidateSwingPrice = 0;
            CandidateSwingTime = default(DateTime);
            _candidateSwingIndex = -1;
            FrozenSwingId = null;
            FrozenSwingPrice = 0;
            FrozenPullbackATR = 0;
            ATRAtPullback = 0;
            ATRAtSignal = 0;
            ConfirmationBarsElapsed = 0;
            BarsInTrade = 0;
            CooldownBarsElapsed = 0;
            EntryRequested = 0;
            EntryFilled = 0;
            InitialRiskDistance = 0;
            InitialStopLoss = 0;
            InitialTakeProfit = 0;
            BreakEvenApplied = false;
            TrailingActivated = false;
            MFEPrice = 0;
            MFE_R = 0;
            MAEPrice = 0;
            MAE_R = 0;
            _setupId = null;
            _setupTimestamp = default(DateTime);
            _entryTimestamp = default(DateTime);
            _riskAmount = 0;
            _entrySpread = 0;
            _entrySlippage = 0;
            _entrySession = null;
            _volumeCapped = false;
        }

        private void PrintSummaryMetrics()
        {
            double winRate = _totalTrades > 0 ? (double)_winningTrades / _totalTrades : 0;
            double avgR = _closedTradeRs.Count > 0 ? _closedTradeRs.Average() : 0;
            double medianR = Median(_closedTradeRs);
            double avgMfe = _closedTradeMfeRs.Count > 0 ? _closedTradeMfeRs.Average() : 0;
            double avgMae = _closedTradeMaeRs.Count > 0 ? _closedTradeMaeRs.Average() : 0;
            double profitFactor = _grossLossAbsSum > 0 ? _grossProfitSum / _grossLossAbsSum : (_grossProfitSum > 0 ? double.PositiveInfinity : 0);

            Print("SUMMARY|strategy={0}|TotalTrades={1}|WinRate={2:P2}|AverageR={3:F6}|MedianR={4:F6}|ProfitFactor={5:F4}|AverageMFE_R={6:F6}|AverageMAE_R={7:F6}|NetPnL={8:F2}",
                StrategyVersion, _totalTrades, winRate, avgR, medianR, profitFactor, avgMfe, avgMae, _netPnlSum);
        }

        private static double Median(List<double> values)
        {
            if (values == null || values.Count == 0)
                return 0;
            double[] sorted = values.OrderBy(x => x).ToArray();
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
        }

        private void LogVerbose(string format, params object[] args)
        {
            if (VerboseLogging)
                Print(format, args);
        }
    }
}
