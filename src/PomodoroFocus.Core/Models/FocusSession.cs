using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PomodoroFocus.Core.Models
{
    [DataContract]
    public sealed class FocusSession
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Mode { get; set; }
        [DataMember] public string Phase { get; set; }
        [DataMember] public string Outcome { get; set; }
        [DataMember] public string StartedAtUtc { get; set; }
        [DataMember] public string EndedAtUtc { get; set; }
        [DataMember] public string LocalDate { get; set; }
        [DataMember] public int PlannedSeconds { get; set; }
        [DataMember] public int EffectiveSeconds { get; set; }
        [DataMember] public string TaskId { get; set; }
        [DataMember] public string TaskTitleSnapshot { get; set; }
        [DataMember] public List<string> TagsSnapshot { get; set; }

        public FocusSession()
        {
            Id = Guid.NewGuid().ToString("N");
            Mode = "Pomodoro";
            Phase = "Focus";
            Outcome = "Completed";
            TagsSnapshot = new List<string>();
        }

        public bool IsFocus
        {
            get { return string.Equals(Phase, "Focus", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsCompleted
        {
            get { return string.Equals(Outcome, "Completed", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsStopwatch
        {
            get { return string.Equals(Mode, "Stopwatch", StringComparison.OrdinalIgnoreCase); }
        }
    }
}
