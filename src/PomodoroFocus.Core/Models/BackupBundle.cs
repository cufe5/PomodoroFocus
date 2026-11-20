using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PomodoroFocus.Core.Models
{
    [DataContract]
    public sealed class BackupBundle
    {
        [DataMember] public int SchemaVersion { get; set; }
        [DataMember] public string ExportedAtUtc { get; set; }
        [DataMember] public AppSettings Settings { get; set; }
        [DataMember] public List<TaskItem> Tasks { get; set; }
        [DataMember] public List<FocusSession> Sessions { get; set; }

        public BackupBundle()
        {
            SchemaVersion = 1;
            ExportedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            Settings = new AppSettings();
            Tasks = new List<TaskItem>();
            Sessions = new List<FocusSession>();
        }
    }
}
