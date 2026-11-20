using System;

namespace PomodoroFocus.Core.Engine
{
    public interface IClock
    {
        DateTime UtcNow { get; }
        DateTime LocalNow { get; }
        TimeZoneInfo LocalTimeZone { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }

        public DateTime LocalNow
        {
            get { return DateTime.Now; }
        }

        public TimeZoneInfo LocalTimeZone
        {
            get { return TimeZoneInfo.Local; }
        }
    }
}
