using System;
using System.Runtime.Serialization;

namespace PomodoroFocus.Core.Models
{
    public sealed class TimerSnapshot
    {
        public TimerMode Mode { get; private set; }
        public TimerPhase Phase { get; private set; }
        public TimerRunState State { get; private set; }
        public int PlannedSeconds { get; private set; }
        public int RemainingSeconds { get; private set; }
        public int ElapsedEffectiveSeconds { get; private set; }
        public double Progress { get; private set; }
        public int CompletedFocusInCycle { get; private set; }
        public int LongBreakInterval { get; private set; }
        public DateTime? EstimatedEndLocal { get; private set; }
        public bool HasCustomFocusDuration { get; private set; }

        public TimerSnapshot(
            TimerMode mode,
            TimerPhase phase,
            TimerRunState state,
            int plannedSeconds,
            int remainingSeconds,
            int elapsedEffectiveSeconds,
            double progress,
            int completedFocusInCycle,
            int longBreakInterval,
            DateTime? estimatedEndLocal,
            bool hasCustomFocusDuration)
        {
            Mode = mode;
            Phase = phase;
            State = state;
            PlannedSeconds = plannedSeconds;
            RemainingSeconds = remainingSeconds;
            ElapsedEffectiveSeconds = elapsedEffectiveSeconds;
            Progress = progress;
            CompletedFocusInCycle = completedFocusInCycle;
            LongBreakInterval = longBreakInterval;
            EstimatedEndLocal = estimatedEndLocal;
            HasCustomFocusDuration = hasCustomFocusDuration;
        }

        public int DisplaySeconds
        {
            get
            {
                return Mode == TimerMode.Stopwatch ? ElapsedEffectiveSeconds : RemainingSeconds;
            }
        }
    }

    public sealed class PhaseCompletedEventArgs : EventArgs
    {
        public FocusSession Session { get; private set; }
        public TimerPhase CompletedPhase { get; private set; }
        public TimerPhase NextPhase { get; private set; }
        public bool AutoStartedNext { get; private set; }
        public bool WasOverdueRecovery { get; private set; }

        public PhaseCompletedEventArgs(
            FocusSession session,
            TimerPhase completedPhase,
            TimerPhase nextPhase,
            bool autoStartedNext,
            bool wasOverdueRecovery)
        {
            Session = session;
            CompletedPhase = completedPhase;
            NextPhase = nextPhase;
            AutoStartedNext = autoStartedNext;
            WasOverdueRecovery = wasOverdueRecovery;
        }
    }

    [DataContract]
    public sealed class TimerPersistedState
    {
        [DataMember] public int SchemaVersion { get; set; }
        [DataMember] public int Mode { get; set; }
        [DataMember] public int Phase { get; set; }
        [DataMember] public int State { get; set; }
        [DataMember] public int PlannedSeconds { get; set; }
        [DataMember] public int RemainingSeconds { get; set; }
        [DataMember] public int AccumulatedEffectiveSeconds { get; set; }
        [DataMember] public string SessionStartedAtUtc { get; set; }
        [DataMember] public string RunSegmentStartedAtUtc { get; set; }
        [DataMember] public string PhaseEndAtUtc { get; set; }
        [DataMember] public int CompletedFocusInCycle { get; set; }
        [DataMember] public int CustomFocusMinutes { get; set; }
        [DataMember] public bool OneMinuteWarned { get; set; }

        public TimerPersistedState()
        {
            SchemaVersion = 1;
        }
    }
}
