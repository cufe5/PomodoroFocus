using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PomodoroFocus.Core.Models
{
    [DataContract]
    public sealed class TaskItem
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Title { get; set; }
        [DataMember] public string Notes { get; set; }
        [DataMember] public int Priority { get; set; }
        [DataMember] public List<string> Tags { get; set; }
        [DataMember] public int EstimatedPomodoros { get; set; }
        [DataMember] public int CompletedPomodoros { get; set; }
        [DataMember] public bool IsCompleted { get; set; }
        [DataMember] public string CreatedAtUtc { get; set; }
        [DataMember] public string CompletedAtUtc { get; set; }

        public TaskItem()
        {
            Id = Guid.NewGuid().ToString("N");
            Title = string.Empty;
            Notes = string.Empty;
            Priority = (int)TaskPriority.None;
            Tags = new List<string>();
            EstimatedPomodoros = 0;
            CompletedPomodoros = 0;
            IsCompleted = false;
            CreatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            CompletedAtUtc = null;
        }

        public TaskPriority PriorityEnum
        {
            get
            {
                if (Priority < 0 || Priority > 3) return TaskPriority.None;
                return (TaskPriority)Priority;
            }
            set
            {
                Priority = (int)value;
            }
        }

        public int RemainingPomodoros
        {
            get
            {
                int diff = EstimatedPomodoros - CompletedPomodoros;
                return diff > 0 ? diff : 0;
            }
        }

        public void Sanitize()
        {
            if (string.IsNullOrEmpty(Id))
            {
                Id = Guid.NewGuid().ToString("N");
            }
            if (Title == null) Title = string.Empty;
            Title = Title.Trim();
            if (Title.Length > 80) Title = Title.Substring(0, 80);

            if (Notes == null) Notes = string.Empty;
            if (Notes.Length > 500) Notes = Notes.Substring(0, 500);

            if (Priority < 0) Priority = 0;
            if (Priority > 3) Priority = 3;

            if (EstimatedPomodoros < 0) EstimatedPomodoros = 0;
            if (EstimatedPomodoros > 20) EstimatedPomodoros = 20;

            if (CompletedPomodoros < 0) CompletedPomodoros = 0;

            if (Tags == null)
            {
                Tags = new List<string>();
            }
            else
            {
                var cleanTags = new List<string>();
                for (int i = 0; i < Tags.Count && cleanTags.Count < 5; i++)
                {
                    string t = Tags[i];
                    if (string.IsNullOrWhiteSpace(t)) continue;
                    t = t.Trim();
                    if (t.StartsWith("#")) t = t.Substring(1).Trim();
                    if (t.Length == 0) continue;
                    if (t.Length > 12) t = t.Substring(0, 12);
                    bool exists = false;
                    for (int j = 0; j < cleanTags.Count; j++)
                    {
                        if (string.Equals(cleanTags[j], t, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists) cleanTags.Add(t);
                }
                Tags = cleanTags;
            }

            if (string.IsNullOrEmpty(CreatedAtUtc))
            {
                CreatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            }
        }

        public TaskItem Clone()
        {
            var copy = (TaskItem)MemberwiseClone();
            copy.Tags = Tags != null ? new List<string>(Tags) : new List<string>();
            return copy;
        }
    }
}
