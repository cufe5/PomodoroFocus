using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.Core.Models
{
    [DataContract]
    public sealed class AppSettings
    {
        [DataMember] public int SchemaVersion { get; set; }
        [DataMember] public string Theme { get; set; }
        [DataMember] public bool ReduceMotion { get; set; }

        [DataMember] public int FocusMinutes { get; set; }
        [DataMember] public int ShortBreakMinutes { get; set; }
        [DataMember] public int LongBreakMinutes { get; set; }
        [DataMember] public int LongBreakInterval { get; set; }
        [DataMember] public int DailyGoal { get; set; }
        [DataMember] public int DayStartHour { get; set; }

        [DataMember] public bool AutoStartBreaks { get; set; }
        [DataMember] public bool AutoStartFocus { get; set; }
        [DataMember] public bool RestoreTimerOnLaunch { get; set; }
        [DataMember] public bool HideMainOnMini { get; set; }
        [DataMember] public double MiniWindowOpacity { get; set; }

        [DataMember] public string NotificationSound { get; set; }
        [DataMember] public int NotificationVolume { get; set; }
        [DataMember] public bool OneMinuteWarning { get; set; }
        [DataMember] public bool EnableDesktopNotification { get; set; }
        [DataMember] public bool EnableInAppToast { get; set; }

        [DataMember] public int AmbientMasterVolume { get; set; }
        [DataMember] public bool AmbientOnlyDuringFocus { get; set; }
        [DataMember] public bool AmbientMuteOnPause { get; set; }
        [DataMember] public Dictionary<string, int> AmbientTrackVolumes { get; set; }
        [DataMember] public List<string> AmbientActiveTracks { get; set; }

        [DataMember] public bool EnableGlobalHotkeys { get; set; }
        [DataMember] public string HotkeyToggle { get; set; }
        [DataMember] public string HotkeySkip { get; set; }
        [DataMember] public string HotkeyMini { get; set; }
        [DataMember] public string HotkeyMain { get; set; }

        [DataMember] public bool LaunchAtStartup { get; set; }
        [DataMember] public bool ShowTrayIcon { get; set; }
        [DataMember] public bool MinimizeToTrayOnClose { get; set; }
        [DataMember] public bool AlwaysOnTop { get; set; }

        [DataMember] public double MainWindowLeft { get; set; }
        [DataMember] public double MainWindowTop { get; set; }
        [DataMember] public double MainWindowWidth { get; set; }
        [DataMember] public double MainWindowHeight { get; set; }
        [DataMember] public bool MainWindowMaximized { get; set; }
        [DataMember] public double MiniWindowLeft { get; set; }
        [DataMember] public double MiniWindowTop { get; set; }
        [DataMember] public string LastActiveView { get; set; }

        [DataMember] public string ActiveTaskId { get; set; }
        [DataMember] public bool TrayFirstHideTipShown { get; set; }

        public AppSettings()
        {
            ResetToDefaults();
        }

        public void ResetToDefaults()
        {
            SchemaVersion = 1;
            Theme = "midnight";
            ReduceMotion = false;

            FocusMinutes = 25;
            ShortBreakMinutes = 5;
            LongBreakMinutes = 15;
            LongBreakInterval = 4;
            DailyGoal = 8;
            DayStartHour = 0;

            AutoStartBreaks = false;
            AutoStartFocus = false;
            RestoreTimerOnLaunch = true;
            HideMainOnMini = true;
            MiniWindowOpacity = 0.95;

            NotificationSound = "chime";
            NotificationVolume = 70;
            OneMinuteWarning = false;
            EnableDesktopNotification = true;
            EnableInAppToast = true;

            AmbientMasterVolume = 75;
            AmbientOnlyDuringFocus = true;
            AmbientMuteOnPause = true;
            AmbientTrackVolumes = new Dictionary<string, int>();
            for (int i = 0; i < SoundCatalog.AllSoundIds.Length; i++)
            {
                AmbientTrackVolumes[SoundCatalog.AllSoundIds[i]] = 60;
            }
            AmbientActiveTracks = new List<string>();

            EnableGlobalHotkeys = true;
            HotkeyToggle = "Ctrl+Alt+Space";
            HotkeySkip = "Ctrl+Alt+Right";
            HotkeyMini = "Ctrl+Alt+M";
            HotkeyMain = "Ctrl+Alt+P";

            LaunchAtStartup = false;
            ShowTrayIcon = true;
            MinimizeToTrayOnClose = true;
            AlwaysOnTop = false;

            MainWindowLeft = double.NaN;
            MainWindowTop = double.NaN;
            MainWindowWidth = 1080.0;
            MainWindowHeight = 720.0;
            MainWindowMaximized = false;
            MiniWindowLeft = double.NaN;
            MiniWindowTop = double.NaN;
            LastActiveView = "focus";

            ActiveTaskId = null;
            TrayFirstHideTipShown = false;
        }

        public void Clamp()
        {
            SchemaVersion = 1;
            if (!SoundCatalog.IsValidThemeKey(Theme))
            {
                Theme = "midnight";
            }

            FocusMinutes = ClampInt(FocusMinutes, 1, 180);
            ShortBreakMinutes = ClampInt(ShortBreakMinutes, 1, 60);
            LongBreakMinutes = ClampInt(LongBreakMinutes, 1, 120);
            LongBreakInterval = ClampInt(LongBreakInterval, 2, 12);
            DailyGoal = ClampInt(DailyGoal, 1, 24);
            DayStartHour = ClampInt(DayStartHour, 0, 6);

            if (double.IsNaN(MiniWindowOpacity) || double.IsInfinity(MiniWindowOpacity))
            {
                MiniWindowOpacity = 0.95;
            }
            else if (MiniWindowOpacity < 0.70)
            {
                MiniWindowOpacity = 0.70;
            }
            else if (MiniWindowOpacity > 1.00)
            {
                MiniWindowOpacity = 1.00;
            }

            if (!SoundCatalog.IsValidNotificationKey(NotificationSound))
            {
                NotificationSound = "chime";
            }
            NotificationVolume = ClampInt(NotificationVolume, 0, 100);
            AmbientMasterVolume = ClampInt(AmbientMasterVolume, 0, 100);

            if (AmbientTrackVolumes == null)
            {
                AmbientTrackVolumes = new Dictionary<string, int>();
            }
            for (int i = 0; i < SoundCatalog.AllSoundIds.Length; i++)
            {
                string id = SoundCatalog.AllSoundIds[i];
                int vol;
                if (!AmbientTrackVolumes.TryGetValue(id, out vol))
                {
                    AmbientTrackVolumes[id] = 60;
                }
                else
                {
                    AmbientTrackVolumes[id] = ClampInt(vol, 0, 100);
                }
            }

            if (AmbientActiveTracks == null)
            {
                AmbientActiveTracks = new List<string>();
            }
            else
            {
                var cleaned = new List<string>();
                for (int i = 0; i < AmbientActiveTracks.Count; i++)
                {
                    string id = AmbientActiveTracks[i];
                    if (SoundCatalog.IsValidSoundId(id) && !cleaned.Contains(id))
                    {
                        cleaned.Add(id);
                    }
                }
                AmbientActiveTracks = cleaned;
            }

            HotkeyGesture parsed;
            if (!HotkeyGesture.TryParse(HotkeyToggle, out parsed)) HotkeyToggle = "Ctrl+Alt+Space";
            if (!HotkeyGesture.TryParse(HotkeySkip, out parsed)) HotkeySkip = "Ctrl+Alt+Right";
            if (!HotkeyGesture.TryParse(HotkeyMini, out parsed)) HotkeyMini = "Ctrl+Alt+M";
            if (!HotkeyGesture.TryParse(HotkeyMain, out parsed)) HotkeyMain = "Ctrl+Alt+P";

            if (double.IsNaN(MainWindowWidth) || double.IsInfinity(MainWindowWidth) || MainWindowWidth < 860.0)
            {
                MainWindowWidth = 1080.0;
            }
            if (double.IsNaN(MainWindowHeight) || double.IsInfinity(MainWindowHeight) || MainWindowHeight < 600.0)
            {
                MainWindowHeight = 720.0;
            }
            if (double.IsInfinity(MainWindowLeft)) MainWindowLeft = double.NaN;
            if (double.IsInfinity(MainWindowTop)) MainWindowTop = double.NaN;
            if (double.IsInfinity(MiniWindowLeft)) MiniWindowLeft = double.NaN;
            if (double.IsInfinity(MiniWindowTop)) MiniWindowTop = double.NaN;

            if (LastActiveView != "focus" && LastActiveView != "tasks" &&
                LastActiveView != "stats" && LastActiveView != "audio" && LastActiveView != "settings")
            {
                LastActiveView = "focus";
            }
        }

        public AppSettings Clone()
        {
            var copy = (AppSettings)MemberwiseClone();
            copy.AmbientTrackVolumes = new Dictionary<string, int>();
            if (AmbientTrackVolumes != null)
            {
                foreach (var kv in AmbientTrackVolumes)
                {
                    copy.AmbientTrackVolumes[kv.Key] = kv.Value;
                }
            }
            copy.AmbientActiveTracks = AmbientActiveTracks != null
                ? new List<string>(AmbientActiveTracks)
                : new List<string>();
            return copy;
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
