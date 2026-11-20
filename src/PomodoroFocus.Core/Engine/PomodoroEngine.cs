using System;
using System.Collections.Generic;
using System.Globalization;
using PomodoroFocus.Core.Models;

namespace PomodoroFocus.Core.Engine
{
    public sealed class PomodoroEngine
    {
        public const int MinRecordableFocusSeconds = 60;
        public const int MaxPhaseSeconds = 240 * 60;       // 240 minutes
        public const int MaxStopwatchSeconds = 4 * 3600;   // 4 hours
        public const int OverdueAutoStartCutoffSeconds = 5 * 60;

        private readonly IClock _clock;
        private AppSettings _settings;

        private TimerMode _mode;
        private TimerPhase _phase;
        private TimerRunState _state;
        private int _plannedSeconds;
        private int _remainingSeconds;
        private int _accumulatedEffectiveSeconds;
        private DateTime? _sessionStartedAtUtc;
        private DateTime? _runSegmentStartedAtUtc;
        private DateTime? _phaseEndAtUtc;
        private int _completedFocusInCycle;
        private int _customFocusMinutes; // 0 = use default FocusMinutes
        private bool _oneMinuteWarned;

        private Func<TaskItem> _activeTaskProvider;

        public PomodoroEngine(IClock clock, AppSettings settings)
        {
            if (clock == null) throw new ArgumentNullException("clock");
            if (settings == null) throw new ArgumentNullException("settings");
            _clock = clock;
            _settings = settings;
            _mode = TimerMode.Pomodoro;
            _phase = TimerPhase.Focus;
            _state = TimerRunState.Idle;
            _completedFocusInCycle = 0;
            _customFocusMinutes = 0;
            _plannedSeconds = GetDefaultPhaseSeconds(TimerMode.Pomodoro, TimerPhase.Focus);
            _remainingSeconds = _plannedSeconds;
            _accumulatedEffectiveSeconds = 0;
            _oneMinuteWarned = false;
        }

        public void SetActiveTaskProvider(Func<TaskItem> provider)
        {
            _activeTaskProvider = provider;
        }

        public event EventHandler<TimerSnapshot> Tick;
        public event EventHandler<PhaseCompletedEventArgs> PhaseCompleted;
        public event EventHandler<TimerSnapshot> OneMinuteLeft;
        public event EventHandler<FocusSession> SessionInterrupted;

        public TimerSnapshot Snapshot
        {
            get { return BuildSnapshot(_clock.UtcNow); }
        }

        public bool NeedsInterruptConfirmation
        {
            get
            {
                if (_state == TimerRunState.Idle) return false;
                if (_phase != TimerPhase.Focus) return false;
                return ComputeCurrentEffectiveSeconds(_clock.UtcNow) >= MinRecordableFocusSeconds;
            }
        }

        public int CurrentEffectiveSeconds
        {
            get { return ComputeCurrentEffectiveSeconds(_clock.UtcNow); }
        }

        public void UpdateSettings(AppSettings settings)
        {
            if (settings == null) return;
            _settings = settings;
            if (_state == TimerRunState.Idle && _mode == TimerMode.Pomodoro)
            {
                if (_phase != TimerPhase.Focus || _customFocusMinutes <= 0)
                {
                    _plannedSeconds = GetDefaultPhaseSeconds(_mode, _phase);
                    _remainingSeconds = _plannedSeconds;
                }
            }
            RaiseTick(_clock.UtcNow);
        }

        public void SelectModeAndPhase(TimerMode mode, TimerPhase phase, bool confirmInterruptIfNeeded)
        {
            DateTime now = _clock.UtcNow;
            if (mode == TimerMode.Stopwatch)
            {
                phase = TimerPhase.Focus;
            }

            if (_mode == mode && _phase == phase && _state == TimerRunState.Idle)
            {
                return;
            }

            if (_state != TimerRunState.Idle && _phase == TimerPhase.Focus)
            {
                int effective = ComputeCurrentEffectiveSeconds(now);
                if (effective >= MinRecordableFocusSeconds)
                {
                    if (!confirmInterruptIfNeeded)
                    {
                        return;
                    }
                    FocusSession interrupted = CreateSession(
                        _mode,
                        _phase,
                        SessionOutcome.Interrupted,
                        _sessionStartedAtUtc ?? now.AddSeconds(-effective),
                        now,
                        _plannedSeconds,
                        effective);
                    RaiseSessionInterrupted(interrupted);
                }
            }

            _mode = mode;
            _phase = phase;
            _state = TimerRunState.Idle;
            _customFocusMinutes = 0;
            _accumulatedEffectiveSeconds = 0;
            _sessionStartedAtUtc = null;
            _runSegmentStartedAtUtc = null;
            _phaseEndAtUtc = null;
            _oneMinuteWarned = false;

            if (_mode == TimerMode.Stopwatch)
            {
                _plannedSeconds = MaxStopwatchSeconds;
                _remainingSeconds = 0;
            }
            else
            {
                _plannedSeconds = GetDefaultPhaseSeconds(_mode, _phase);
                _remainingSeconds = _plannedSeconds;
            }

            RaiseTick(now);
        }

        public void SetCustomFocusMinutes(int minutes)
        {
            if (_state != TimerRunState.Idle || _mode != TimerMode.Pomodoro || _phase != TimerPhase.Focus)
            {
                return;
            }
            if (minutes < 1) minutes = 1;
            if (minutes > 180) minutes = 180;
            _customFocusMinutes = (minutes == _settings.FocusMinutes) ? 0 : minutes;
            _plannedSeconds = minutes * 60;
            _remainingSeconds = _plannedSeconds;
            RaiseTick(_clock.UtcNow);
        }

        public void Start()
        {
            if (_state != TimerRunState.Idle) return;
            DateTime now = _clock.UtcNow;
            _state = TimerRunState.Running;
            _sessionStartedAtUtc = now;
            _runSegmentStartedAtUtc = now;
            _accumulatedEffectiveSeconds = 0;
            _oneMinuteWarned = false;

            if (_mode == TimerMode.Pomodoro)
            {
                if (_remainingSeconds <= 0)
                {
                    _remainingSeconds = _plannedSeconds;
                }
                _phaseEndAtUtc = now.AddSeconds(_remainingSeconds);
            }
            else
            {
                _plannedSeconds = MaxStopwatchSeconds;
                _phaseEndAtUtc = now.AddSeconds(MaxStopwatchSeconds);
            }

            RaiseTick(now);
        }

        public void Pause()
        {
            if (_state != TimerRunState.Running) return;
            DateTime now = _clock.UtcNow;
            _accumulatedEffectiveSeconds = ComputeCurrentEffectiveSeconds(now);
            if (_mode == TimerMode.Pomodoro && _phaseEndAtUtc.HasValue)
            {
                int rem = (int)Math.Ceiling((_phaseEndAtUtc.Value - now).TotalSeconds);
                _remainingSeconds = rem > 0 ? rem : 0;
            }
            _runSegmentStartedAtUtc = null;
            _phaseEndAtUtc = null;
            _state = TimerRunState.Paused;
            RaiseTick(now);
        }

        public void Resume()
        {
            if (_state != TimerRunState.Paused) return;
            DateTime now = _clock.UtcNow;
            _state = TimerRunState.Running;
            _runSegmentStartedAtUtc = now;
            if (_mode == TimerMode.Pomodoro)
            {
                _phaseEndAtUtc = now.AddSeconds(_remainingSeconds);
            }
            else
            {
                int left = MaxStopwatchSeconds - _accumulatedEffectiveSeconds;
                if (left < 0) left = 0;
                _phaseEndAtUtc = now.AddSeconds(left);
            }
            RaiseTick(now);
        }

        public void Toggle()
        {
            if (_state == TimerRunState.Idle)
            {
                Start();
            }
            else if (_state == TimerRunState.Running)
            {
                Pause();
            }
            else
            {
                Resume();
            }
        }

        public FocusSession Reset()
        {
            DateTime now = _clock.UtcNow;
            FocusSession interrupted = null;

            if (_state == TimerRunState.Idle)
            {
                _customFocusMinutes = 0;
                if (_mode == TimerMode.Pomodoro)
                {
                    _plannedSeconds = GetDefaultPhaseSeconds(_mode, _phase);
                    _remainingSeconds = _plannedSeconds;
                }
                else
                {
                    _plannedSeconds = MaxStopwatchSeconds;
                    _remainingSeconds = 0;
                }
                RaiseTick(now);
                return null;
            }

            // Stopwatch Reset discards without recording (spec 2.1.2)
            if (_mode == TimerMode.Pomodoro && _phase == TimerPhase.Focus)
            {
                int effective = ComputeCurrentEffectiveSeconds(now);
                if (effective >= MinRecordableFocusSeconds)
                {
                    interrupted = CreateSession(
                        _mode,
                        _phase,
                        SessionOutcome.Interrupted,
                        _sessionStartedAtUtc ?? now.AddSeconds(-effective),
                        now,
                        _plannedSeconds,
                        effective);
                }
            }

            _state = TimerRunState.Idle;
            _customFocusMinutes = 0;
            _accumulatedEffectiveSeconds = 0;
            _sessionStartedAtUtc = null;
            _runSegmentStartedAtUtc = null;
            _phaseEndAtUtc = null;
            _oneMinuteWarned = false;

            if (_mode == TimerMode.Pomodoro)
            {
                _plannedSeconds = GetDefaultPhaseSeconds(_mode, _phase);
                _remainingSeconds = _plannedSeconds;
            }
            else
            {
                _plannedSeconds = MaxStopwatchSeconds;
                _remainingSeconds = 0;
            }

            if (interrupted != null)
            {
                RaiseSessionInterrupted(interrupted);
            }
            RaiseTick(now);
            return interrupted;
        }

        public FocusSession Skip()
        {
            DateTime now = _clock.UtcNow;

            if (_mode == TimerMode.Stopwatch)
            {
                FocusSession completed = null;
                int effective = ComputeCurrentEffectiveSeconds(now);
                if (_state != TimerRunState.Idle && effective >= MinRecordableFocusSeconds)
                {
                    completed = CreateSession(
                        TimerMode.Stopwatch,
                        TimerPhase.Focus,
                        SessionOutcome.Completed,
                        _sessionStartedAtUtc ?? now.AddSeconds(-effective),
                        now,
                        effective,
                        effective);
                }

                _state = TimerRunState.Idle;
                _accumulatedEffectiveSeconds = 0;
                _sessionStartedAtUtc = null;
                _runSegmentStartedAtUtc = null;
                _phaseEndAtUtc = null;
                _oneMinuteWarned = false;
                _remainingSeconds = 0;

                if (completed != null)
                {
                    RaisePhaseCompleted(new PhaseCompletedEventArgs(
                        completed,
                        TimerPhase.Focus,
                        TimerPhase.Focus,
                        false,
                        false));
                }
                RaiseTick(now);
                return completed;
            }

            FocusSession recorded = null;
            if (_state != TimerRunState.Idle && _phase == TimerPhase.Focus)
            {
                int effective = ComputeCurrentEffectiveSeconds(now);
                if (effective >= MinRecordableFocusSeconds)
                {
                    recorded = CreateSession(
                        TimerMode.Pomodoro,
                        TimerPhase.Focus,
                        SessionOutcome.Interrupted,
                        _sessionStartedAtUtc ?? now.AddSeconds(-effective),
                        now,
                        _plannedSeconds,
                        effective);
                }
            }

            TimerPhase previousPhase = _phase;
            if (previousPhase == TimerPhase.LongBreak)
            {
                _completedFocusInCycle = 0;
            }

            TimerPhase nextPhase = DetermineNextPhase(previousPhase);
            bool autoStart = ShouldAutoStart(previousPhase, nextPhase);

            EnterPhase(nextPhase, autoStart, now);

            if (recorded != null)
            {
                RaiseSessionInterrupted(recorded);
            }
            RaiseTick(now);
            return recorded;
        }

        public bool AddFiveMinutes()
        {
            if (_mode == TimerMode.Stopwatch)
            {
                return false;
            }
            const int delta = 5 * 60;
            if (_plannedSeconds + delta > MaxPhaseSeconds)
            {
                return false;
            }

            DateTime now = _clock.UtcNow;
            _plannedSeconds += delta;

            if (_state == TimerRunState.Idle)
            {
                _remainingSeconds += delta;
                if (_phase == TimerPhase.Focus)
                {
                    _customFocusMinutes = _plannedSeconds / 60;
                }
            }
            else if (_state == TimerRunState.Running)
            {
                if (_phaseEndAtUtc.HasValue)
                {
                    _phaseEndAtUtc = _phaseEndAtUtc.Value.AddSeconds(delta);
                }
                _remainingSeconds = ComputeCurrentRemainingSeconds(now);
                if (_remainingSeconds > 60)
                {
                    _oneMinuteWarned = false;
                }
            }
            else if (_state == TimerRunState.Paused)
            {
                _remainingSeconds += delta;
                if (_remainingSeconds > 60)
                {
                    _oneMinuteWarned = false;
                }
            }

            RaiseTick(now);
            return true;
        }

        public void AdvanceTo(DateTime utcNow)
        {
            if (_state != TimerRunState.Running)
            {
                return;
            }

            if (_mode == TimerMode.Stopwatch)
            {
                int effective = ComputeCurrentEffectiveSeconds(utcNow);
                if (effective >= MaxStopwatchSeconds)
                {
                    DateTime endUtc = (_sessionStartedAtUtc ?? utcNow).AddSeconds(MaxStopwatchSeconds);
                    if (_phaseEndAtUtc.HasValue) endUtc = _phaseEndAtUtc.Value;

                    FocusSession completed = CreateSession(
                        TimerMode.Stopwatch,
                        TimerPhase.Focus,
                        SessionOutcome.Completed,
                        _sessionStartedAtUtc ?? endUtc.AddSeconds(-MaxStopwatchSeconds),
                        endUtc,
                        MaxStopwatchSeconds,
                        MaxStopwatchSeconds);

                    _state = TimerRunState.Idle;
                    _accumulatedEffectiveSeconds = 0;
                    _sessionStartedAtUtc = null;
                    _runSegmentStartedAtUtc = null;
                    _phaseEndAtUtc = null;
                    _remainingSeconds = 0;

                    RaisePhaseCompleted(new PhaseCompletedEventArgs(
                        completed,
                        TimerPhase.Focus,
                        TimerPhase.Focus,
                        false,
                        false));
                    RaiseTick(utcNow);
                    return;
                }

                RaiseTick(utcNow);
                return;
            }

            if (!_phaseEndAtUtc.HasValue)
            {
                return;
            }

            if (utcNow >= _phaseEndAtUtc.Value)
            {
                CompleteCurrentPomodoroPhase(utcNow, false);
                return;
            }

            int remaining = ComputeCurrentRemainingSeconds(utcNow);
            if (_settings.OneMinuteWarning && !_oneMinuteWarned && _plannedSeconds > 60 && remaining <= 60 && remaining > 0)
            {
                _oneMinuteWarned = true;
                var handler = OneMinuteLeft;
                if (handler != null)
                {
                    handler(this, BuildSnapshot(utcNow));
                }
            }

            RaiseTick(utcNow);
        }

        public TimerPersistedState ExportState()
        {
            DateTime now = _clock.UtcNow;
            var ps = new TimerPersistedState();
            ps.SchemaVersion = 1;
            ps.Mode = (int)_mode;
            ps.Phase = (int)_phase;
            ps.State = (int)_state;
            ps.PlannedSeconds = _plannedSeconds;
            ps.RemainingSeconds = _state == TimerRunState.Running
                ? ComputeCurrentRemainingSeconds(now)
                : _remainingSeconds;
            ps.AccumulatedEffectiveSeconds = _accumulatedEffectiveSeconds;
            ps.SessionStartedAtUtc = _sessionStartedAtUtc.HasValue ? FormatUtc(_sessionStartedAtUtc.Value) : null;
            ps.RunSegmentStartedAtUtc = _runSegmentStartedAtUtc.HasValue ? FormatUtc(_runSegmentStartedAtUtc.Value) : null;
            ps.PhaseEndAtUtc = _phaseEndAtUtc.HasValue ? FormatUtc(_phaseEndAtUtc.Value) : null;
            ps.CompletedFocusInCycle = _completedFocusInCycle;
            ps.CustomFocusMinutes = _customFocusMinutes;
            ps.OneMinuteWarned = _oneMinuteWarned;
            return ps;
        }

        public FocusSession RestoreState(TimerPersistedState state)
        {
            if (state == null) return null;

            DateTime now = _clock.UtcNow;
            _mode = (state.Mode == (int)TimerMode.Stopwatch) ? TimerMode.Stopwatch : TimerMode.Pomodoro;
            _phase = (state.Phase >= 0 && state.Phase <= 2) ? (TimerPhase)state.Phase : TimerPhase.Focus;
            if (_mode == TimerMode.Stopwatch) _phase = TimerPhase.Focus;

            _completedFocusInCycle = state.CompletedFocusInCycle >= 0 ? state.CompletedFocusInCycle : 0;
            _customFocusMinutes = (state.CustomFocusMinutes >= 1 && state.CustomFocusMinutes <= 180)
                ? state.CustomFocusMinutes
                : 0;
            _plannedSeconds = state.PlannedSeconds > 0
                ? state.PlannedSeconds
                : GetDefaultPhaseSeconds(_mode, _phase);
            _accumulatedEffectiveSeconds = state.AccumulatedEffectiveSeconds >= 0 ? state.AccumulatedEffectiveSeconds : 0;
            _sessionStartedAtUtc = TryParseUtc(state.SessionStartedAtUtc);
            _runSegmentStartedAtUtc = TryParseUtc(state.RunSegmentStartedAtUtc);
            _phaseEndAtUtc = TryParseUtc(state.PhaseEndAtUtc);
            _oneMinuteWarned = state.OneMinuteWarned;

            TimerRunState savedState = (state.State >= 0 && state.State <= 2)
                ? (TimerRunState)state.State
                : TimerRunState.Idle;

            if (savedState == TimerRunState.Idle)
            {
                _state = TimerRunState.Idle;
                _remainingSeconds = _mode == TimerMode.Stopwatch ? 0 : _plannedSeconds;
                RaiseTick(now);
                return null;
            }

            if (savedState == TimerRunState.Paused)
            {
                _state = TimerRunState.Paused;
                _remainingSeconds = state.RemainingSeconds >= 0 ? state.RemainingSeconds : _plannedSeconds;
                _runSegmentStartedAtUtc = null;
                _phaseEndAtUtc = null;
                RaiseTick(now);
                return null;
            }

            // Saved state was Running
            _state = TimerRunState.Running;
            if (_mode == TimerMode.Stopwatch)
            {
                int effective = ComputeCurrentEffectiveSeconds(now);
                if (effective >= MaxStopwatchSeconds)
                {
                    DateTime endUtc = _phaseEndAtUtc ?? now;
                    FocusSession completed = CreateSession(
                        TimerMode.Stopwatch,
                        TimerPhase.Focus,
                        SessionOutcome.Completed,
                        _sessionStartedAtUtc ?? endUtc.AddSeconds(-MaxStopwatchSeconds),
                        endUtc,
                        MaxStopwatchSeconds,
                        MaxStopwatchSeconds);

                    _state = TimerRunState.Idle;
                    _accumulatedEffectiveSeconds = 0;
                    _sessionStartedAtUtc = null;
                    _runSegmentStartedAtUtc = null;
                    _phaseEndAtUtc = null;
                    _remainingSeconds = 0;
                    RaiseTick(now);
                    return completed;
                }
                RaiseTick(now);
                return null;
            }

            if (_phaseEndAtUtc.HasValue && now >= _phaseEndAtUtc.Value)
            {
                FocusSession completed = CompleteCurrentPomodoroPhase(now, true);
                return completed;
            }

            if (!_phaseEndAtUtc.HasValue)
            {
                _remainingSeconds = state.RemainingSeconds > 0 ? state.RemainingSeconds : _plannedSeconds;
                _phaseEndAtUtc = now.AddSeconds(_remainingSeconds);
                _runSegmentStartedAtUtc = now;
            }

            RaiseTick(now);
            return null;
        }

        public void SetDemoState(
            TimerMode mode,
            TimerPhase phase,
            TimerRunState state,
            int remainingOrElapsedSeconds,
            int completedFocusInCycle)
        {
            DateTime now = _clock.UtcNow;
            _mode = mode;
            _phase = (mode == TimerMode.Stopwatch) ? TimerPhase.Focus : phase;
            _state = state;
            _completedFocusInCycle = completedFocusInCycle >= 0 ? completedFocusInCycle : 0;
            _customFocusMinutes = 0;
            _oneMinuteWarned = false;

            if (_mode == TimerMode.Stopwatch)
            {
                _plannedSeconds = MaxStopwatchSeconds;
                int elapsed = remainingOrElapsedSeconds > 0 ? remainingOrElapsedSeconds : 0;
                if (elapsed > MaxStopwatchSeconds) elapsed = MaxStopwatchSeconds;
                _accumulatedEffectiveSeconds = elapsed;
                _remainingSeconds = 0;
                if (_state == TimerRunState.Running)
                {
                    _sessionStartedAtUtc = now.AddSeconds(-elapsed);
                    _runSegmentStartedAtUtc = now;
                    _phaseEndAtUtc = now.AddSeconds(MaxStopwatchSeconds - elapsed);
                }
                else if (_state == TimerRunState.Paused)
                {
                    _sessionStartedAtUtc = now.AddSeconds(-elapsed);
                    _runSegmentStartedAtUtc = null;
                    _phaseEndAtUtc = null;
                }
                else
                {
                    _accumulatedEffectiveSeconds = 0;
                    _sessionStartedAtUtc = null;
                    _runSegmentStartedAtUtc = null;
                    _phaseEndAtUtc = null;
                }
            }
            else
            {
                _plannedSeconds = GetDefaultPhaseSeconds(_mode, _phase);
                int rem = remainingOrElapsedSeconds;
                if (rem < 0) rem = 0;
                if (rem > _plannedSeconds) _plannedSeconds = rem;
                _remainingSeconds = rem;
                int elapsed = _plannedSeconds - _remainingSeconds;
                if (elapsed < 0) elapsed = 0;

                if (_state == TimerRunState.Running)
                {
                    _accumulatedEffectiveSeconds = elapsed;
                    _sessionStartedAtUtc = now.AddSeconds(-elapsed);
                    _runSegmentStartedAtUtc = now;
                    _phaseEndAtUtc = now.AddSeconds(_remainingSeconds);
                }
                else if (_state == TimerRunState.Paused)
                {
                    _accumulatedEffectiveSeconds = elapsed;
                    _sessionStartedAtUtc = now.AddSeconds(-elapsed);
                    _runSegmentStartedAtUtc = null;
                    _phaseEndAtUtc = null;
                }
                else
                {
                    _remainingSeconds = _plannedSeconds;
                    _accumulatedEffectiveSeconds = 0;
                    _sessionStartedAtUtc = null;
                    _runSegmentStartedAtUtc = null;
                    _phaseEndAtUtc = null;
                }
            }

            RaiseTick(now);
        }

        private FocusSession CompleteCurrentPomodoroPhase(DateTime utcNow, bool isRestore)
        {
            DateTime endUtc = _phaseEndAtUtc ?? utcNow;
            double overdueSeconds = (utcNow - endUtc).TotalSeconds;
            bool wasOverdue = isRestore || overdueSeconds > OverdueAutoStartCutoffSeconds;

            int effectiveAtEnd = ComputeEffectiveSecondsAt(endUtc);
            if (effectiveAtEnd <= 0) effectiveAtEnd = _plannedSeconds;

            TimerPhase completedPhase = _phase;
            FocusSession session = CreateSession(
                TimerMode.Pomodoro,
                completedPhase,
                SessionOutcome.Completed,
                _sessionStartedAtUtc ?? endUtc.AddSeconds(-effectiveAtEnd),
                endUtc,
                _plannedSeconds,
                effectiveAtEnd);

            if (completedPhase == TimerPhase.Focus)
            {
                _completedFocusInCycle++;
            }
            else if (completedPhase == TimerPhase.LongBreak)
            {
                _completedFocusInCycle = 0;
            }

            TimerPhase nextPhase = DetermineNextPhase(completedPhase);
            bool autoStart = !wasOverdue && ShouldAutoStart(completedPhase, nextPhase);

            EnterPhase(nextPhase, autoStart, utcNow);

            if (!isRestore)
            {
                RaisePhaseCompleted(new PhaseCompletedEventArgs(
                    session,
                    completedPhase,
                    nextPhase,
                    autoStart,
                    wasOverdue));
            }

            RaiseTick(utcNow);
            return session;
        }

        private TimerPhase DetermineNextPhase(TimerPhase completedPhase)
        {
            if (completedPhase == TimerPhase.Focus)
            {
                return (_completedFocusInCycle >= _settings.LongBreakInterval)
                    ? TimerPhase.LongBreak
                    : TimerPhase.ShortBreak;
            }
            return TimerPhase.Focus;
        }

        private bool ShouldAutoStart(TimerPhase fromPhase, TimerPhase toPhase)
        {
            if (fromPhase == TimerPhase.Focus && (toPhase == TimerPhase.ShortBreak || toPhase == TimerPhase.LongBreak))
            {
                return _settings.AutoStartBreaks;
            }
            if ((fromPhase == TimerPhase.ShortBreak || fromPhase == TimerPhase.LongBreak) && toPhase == TimerPhase.Focus)
            {
                return _settings.AutoStartFocus;
            }
            return false;
        }

        private void EnterPhase(TimerPhase nextPhase, bool autoStart, DateTime now)
        {
            _phase = nextPhase;
            _customFocusMinutes = 0;
            _plannedSeconds = GetDefaultPhaseSeconds(TimerMode.Pomodoro, nextPhase);
            _remainingSeconds = _plannedSeconds;
            _accumulatedEffectiveSeconds = 0;
            _oneMinuteWarned = false;

            if (autoStart)
            {
                _state = TimerRunState.Running;
                _sessionStartedAtUtc = now;
                _runSegmentStartedAtUtc = now;
                _phaseEndAtUtc = now.AddSeconds(_remainingSeconds);
            }
            else
            {
                _state = TimerRunState.Idle;
                _sessionStartedAtUtc = null;
                _runSegmentStartedAtUtc = null;
                _phaseEndAtUtc = null;
            }
        }

        private int GetDefaultPhaseSeconds(TimerMode mode, TimerPhase phase)
        {
            if (mode == TimerMode.Stopwatch) return MaxStopwatchSeconds;
            if (phase == TimerPhase.ShortBreak) return _settings.ShortBreakMinutes * 60;
            if (phase == TimerPhase.LongBreak) return _settings.LongBreakMinutes * 60;
            if (_customFocusMinutes > 0) return _customFocusMinutes * 60;
            return _settings.FocusMinutes * 60;
        }

        private int ComputeCurrentRemainingSeconds(DateTime utcNow)
        {
            if (_mode == TimerMode.Stopwatch) return 0;
            if (_state == TimerRunState.Running && _phaseEndAtUtc.HasValue)
            {
                int rem = (int)Math.Ceiling((_phaseEndAtUtc.Value - utcNow).TotalSeconds);
                return rem > 0 ? rem : 0;
            }
            return _remainingSeconds;
        }

        private int ComputeCurrentEffectiveSeconds(DateTime utcNow)
        {
            return ComputeEffectiveSecondsAt(utcNow);
        }

        private int ComputeEffectiveSecondsAt(DateTime utcTime)
        {
            int total = _accumulatedEffectiveSeconds;
            if (_state == TimerRunState.Running && _runSegmentStartedAtUtc.HasValue)
            {
                double seg = (utcTime - _runSegmentStartedAtUtc.Value).TotalSeconds;
                if (seg > 0)
                {
                    total += (int)Math.Floor(seg);
                }
            }
            return total;
        }

        private FocusSession CreateSession(
            TimerMode mode,
            TimerPhase phase,
            SessionOutcome outcome,
            DateTime startedUtc,
            DateTime endedUtc,
            int plannedSeconds,
            int effectiveSeconds)
        {
            DateTime localStart = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(startedUtc, DateTimeKind.Utc),
                _clock.LocalTimeZone);
            DateTime logicalDay = localStart.AddHours(-_settings.DayStartHour).Date;

            TaskItem activeTask = _activeTaskProvider != null ? _activeTaskProvider() : null;

            var session = new FocusSession();
            session.Id = Guid.NewGuid().ToString("N");
            session.Mode = mode.ToString();
            session.Phase = phase.ToString();
            session.Outcome = outcome.ToString();
            session.StartedAtUtc = FormatUtc(startedUtc);
            session.EndedAtUtc = FormatUtc(endedUtc);
            session.LocalDate = logicalDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            session.PlannedSeconds = plannedSeconds;
            session.EffectiveSeconds = effectiveSeconds;
            if (phase == TimerPhase.Focus && activeTask != null)
            {
                session.TaskId = activeTask.Id;
                session.TaskTitleSnapshot = activeTask.Title;
                session.TagsSnapshot = activeTask.Tags != null ? new List<string>(activeTask.Tags) : new List<string>();
            }
            return session;
        }

        private TimerSnapshot BuildSnapshot(DateTime utcNow)
        {
            int remaining = ComputeCurrentRemainingSeconds(utcNow);
            int effective = ComputeCurrentEffectiveSeconds(utcNow);

            double progress;
            if (_mode == TimerMode.Stopwatch)
            {
                progress = ((effective % 1500) / 1500.0);
                if (effective > 0 && effective % 1500 == 0) progress = 1.0;
            }
            else
            {
                if (_plannedSeconds <= 0)
                {
                    progress = 0.0;
                }
                else if (_state == TimerRunState.Idle)
                {
                    progress = 0.0;
                }
                else
                {
                    double preciseRemaining;
                    if (_state == TimerRunState.Running && _phaseEndAtUtc.HasValue)
                    {
                        preciseRemaining = (_phaseEndAtUtc.Value - utcNow).TotalSeconds;
                        if (preciseRemaining < 0) preciseRemaining = 0;
                    }
                    else
                    {
                        preciseRemaining = remaining;
                    }
                    progress = 1.0 - (preciseRemaining / _plannedSeconds);
                    if (progress < 0.0) progress = 0.0;
                    if (progress > 1.0) progress = 1.0;
                }
            }

            DateTime? estimatedEndLocal = null;
            if (_mode == TimerMode.Pomodoro && _state == TimerRunState.Running && _phaseEndAtUtc.HasValue)
            {
                estimatedEndLocal = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(_phaseEndAtUtc.Value, DateTimeKind.Utc),
                    _clock.LocalTimeZone);
            }

            return new TimerSnapshot(
                _mode,
                _phase,
                _state,
                _plannedSeconds,
                remaining,
                effective,
                progress,
                _completedFocusInCycle,
                _settings.LongBreakInterval,
                estimatedEndLocal,
                _customFocusMinutes > 0);
        }

        private void RaiseTick(DateTime utcNow)
        {
            var handler = Tick;
            if (handler != null)
            {
                handler(this, BuildSnapshot(utcNow));
            }
        }

        private void RaisePhaseCompleted(PhaseCompletedEventArgs args)
        {
            var handler = PhaseCompleted;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        private void RaiseSessionInterrupted(FocusSession session)
        {
            var handler = SessionInterrupted;
            if (handler != null)
            {
                handler(this, session);
            }
        }

        public static string FormatUtc(DateTime dt)
        {
            DateTime utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            return utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }

        public static DateTime? TryParseUtc(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            DateTime dt;
            if (DateTime.TryParseExact(
                s,
                new string[] { "yyyy-MM-ddTHH:mm:ss.fffZ", "yyyy-MM-ddTHH:mm:ssZ", "o" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out dt))
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
            return null;
        }
    }
}
