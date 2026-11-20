namespace PomodoroFocus.Core.Models
{
    public enum TimerMode
    {
        Pomodoro = 0,
        Stopwatch = 1
    }

    public enum TimerPhase
    {
        Focus = 0,
        ShortBreak = 1,
        LongBreak = 2
    }

    public enum TimerRunState
    {
        Idle = 0,
        Running = 1,
        Paused = 2
    }

    public enum SessionOutcome
    {
        Completed = 0,
        Interrupted = 1
    }

    public enum TaskPriority
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum ThemeId
    {
        Midnight = 0,
        Mist = 1,
        Sunset = 2,
        Forest = 3
    }

    public enum SoundId
    {
        Rain = 0,
        Ocean = 1,
        Forest = 2,
        Cafe = 3,
        Fire = 4,
        Wind = 5,
        White = 6,
        Brown = 7,
        Clock = 8
    }

    public enum NotificationSoundId
    {
        Chime = 0,
        Bowl = 1,
        Wood = 2,
        Digital = 3,
        None = 4
    }

    public enum TaskFilterMode
    {
        Active = 0,
        Completed = 1,
        All = 2
    }

    public enum TaskSortMode
    {
        Smart = 0,
        Priority = 1,
        CreatedNewest = 2,
        RemainingPomodoros = 3
    }

    public static class SoundCatalog
    {
        public static readonly string[] AllSoundIds = new string[]
        {
            "rain", "ocean", "forest", "cafe", "fire", "wind", "white", "brown", "clock"
        };

        public static readonly string[] AllThemeKeys = new string[]
        {
            "midnight", "mist", "sunset", "forest"
        };

        public static readonly string[] AllNotificationSoundKeys = new string[]
        {
            "chime", "bowl", "wood", "digital", "none"
        };

        public static bool IsValidSoundId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < AllSoundIds.Length; i++)
            {
                if (AllSoundIds[i] == id) return true;
            }
            return false;
        }

        public static bool IsValidThemeKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            for (int i = 0; i < AllThemeKeys.Length; i++)
            {
                if (AllThemeKeys[i] == key) return true;
            }
            return false;
        }

        public static bool IsValidNotificationKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            for (int i = 0; i < AllNotificationSoundKeys.Length; i++)
            {
                if (AllNotificationSoundKeys[i] == key) return true;
            }
            return false;
        }
    }
}
