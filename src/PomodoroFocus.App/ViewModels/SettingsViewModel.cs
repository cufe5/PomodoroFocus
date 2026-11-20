using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;
using PomodoroFocus.App.Interop;
using PomodoroFocus.App.Themes;
using PomodoroFocus.Core.Models;

namespace PomodoroFocus.App.ViewModels
{
    public sealed class ThemeCardItem : ObservableObject
    {
        private bool _isSelected;
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string AutomationId { get; set; }
        public string BgHex { get; set; }
        public string CardHex { get; set; }
        public string AccentHex { get; set; }
        public string TextHex { get; set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetField(ref _isSelected, value, "IsSelected"); }
        }
    }

    public sealed class NamedKeyOption
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public override string ToString() { return Label; }
    }

    public sealed class DayStartHourOption
    {
        public int Hour { get; set; }
        public string Label { get; set; }
        public override string ToString() { return Label; }
    }

    public sealed class SettingsViewModel : ObservableObject
    {
        private readonly MainViewModel _main;
        private bool _loading;

        private bool _reduceMotion;
        private int _miniOpacityPercent = 95;
        private int _focusMinutes = 25;
        private int _shortBreakMinutes = 5;
        private int _longBreakMinutes = 15;
        private int _longBreakInterval = 4;
        private int _dailyGoal = 8;
        private DayStartHourOption _selectedDayStart;
        private bool _showNextPhaseHint;

        private bool _autoStartBreaks;
        private bool _autoStartFocus;
        private bool _restoreTimerOnLaunch = true;
        private bool _hideMainOnMini = true;

        private NamedKeyOption _selectedNotificationSound;
        private int _notificationVolume = 70;
        private bool _oneMinuteWarning;
        private bool _enableDesktopNotification = true;
        private bool _enableInAppToast = true;

        private bool _enableGlobalHotkeys = true;
        private string _hotkeyToggle = "Ctrl+Alt+Space";
        private string _hotkeySkip = "Ctrl+Alt+Right";
        private string _hotkeyMini = "Ctrl+Alt+M";
        private string _hotkeyMain = "Ctrl+Alt+P";
        private bool _hotkeyToggleOk = true;
        private bool _hotkeySkipOk = true;
        private bool _hotkeyMiniOk = true;
        private bool _hotkeyMainOk = true;

        private bool _launchAtStartup;
        private bool _showTrayIcon = true;
        private bool _minimizeToTrayOnClose = true;

        public SettingsViewModel(MainViewModel main)
        {
            _main = main;

            ThemeCards = new List<ThemeCardItem>
            {
                new ThemeCardItem { Key = "midnight", DisplayName = "深邃夜空", AutomationId = "ThemeCard_midnight", BgHex = "#0B0E14", CardHex = "#1A2030", AccentHex = "#FF6B5E", TextHex = "#F2F4F8" },
                new ThemeCardItem { Key = "mist", DisplayName = "晨雾浅色", AutomationId = "ThemeCard_mist", BgHex = "#F3F5F9", CardHex = "#FFFFFF", AccentHex = "#E5484D", TextHex = "#0F172A" },
                new ThemeCardItem { Key = "sunset", DisplayName = "暖阳日落", AutomationId = "ThemeCard_sunset", BgHex = "#170F12", CardHex = "#2A1D22", AccentHex = "#FF8A4C", TextHex = "#FFF4EC" },
                new ThemeCardItem { Key = "forest", DisplayName = "静谧森林", AutomationId = "ThemeCard_forest", BgHex = "#09120F", CardHex = "#152520", AccentHex = "#3DDC97", TextHex = "#EAF5EF" }
            };

            DayStartOptions = new List<DayStartHourOption>();
            for (int h = 0; h <= 6; h++)
            {
                DayStartOptions.Add(new DayStartHourOption
                {
                    Hour = h,
                    Label = string.Format(CultureInfo.InvariantCulture, "{0:D2}:00", h)
                });
            }
            _selectedDayStart = DayStartOptions[0];

            NotificationSoundOptions = new List<NamedKeyOption>
            {
                new NamedKeyOption { Key = "chime", Label = "风铃" },
                new NamedKeyOption { Key = "bowl", Label = "颂钵" },
                new NamedKeyOption { Key = "wood", Label = "木鱼" },
                new NamedKeyOption { Key = "digital", Label = "电子" },
                new NamedKeyOption { Key = "none", Label = "无" }
            };
            _selectedNotificationSound = NotificationSoundOptions[0];

            SelectThemeCommand = new RelayCommand<string>(SelectTheme);
            PreviewSoundCommand = new RelayCommand(PreviewNotificationSound);
            RestoreDefaultHotkeysCommand = new RelayCommand(RestoreDefaultHotkeys);
            OpenDataFolderCommand = new RelayCommand(delegate { _main.OpenDataFolder(); });
            ExportBackupCommand = new RelayCommand(delegate { _main.ExportBackupInteractive(); });
            ImportBackupCommand = new RelayCommand(delegate { _main.ImportBackupInteractive(); });
            ExportCsvCommand = new RelayCommand(delegate { _main.ExportCsvInteractive(); });
            RestoreDefaultSettingsCommand = new RelayCommand(delegate { _main.RequestRestoreDefaultSettings(); });
            ClearAllDataCommand = new RelayCommand(delegate { _main.OpenClearDataModal(); });
        }

        public IList<ThemeCardItem> ThemeCards { get; private set; }
        public IList<DayStartHourOption> DayStartOptions { get; private set; }
        public IList<NamedKeyOption> NotificationSoundOptions { get; private set; }

        public ICommand SelectThemeCommand { get; private set; }
        public ICommand PreviewSoundCommand { get; private set; }
        public ICommand RestoreDefaultHotkeysCommand { get; private set; }
        public ICommand OpenDataFolderCommand { get; private set; }
        public ICommand ExportBackupCommand { get; private set; }
        public ICommand ImportBackupCommand { get; private set; }
        public ICommand ExportCsvCommand { get; private set; }
        public ICommand RestoreDefaultSettingsCommand { get; private set; }
        public ICommand ClearAllDataCommand { get; private set; }

        public string DataDirectoryPath { get { return _main.Storage.DataDirectory; } }

        public bool ReduceMotion
        {
            get { return _reduceMotion; }
            set
            {
                if (SetField(ref _reduceMotion, value, "ReduceMotion"))
                {
                    AnimationHelper.ReduceMotionSetting = value;
                    SaveSettingsField(delegate(AppSettings s) { s.ReduceMotion = value; });
                }
            }
        }

        public int MiniOpacityPercent
        {
            get { return _miniOpacityPercent; }
            set
            {
                int v = value < 70 ? 70 : (value > 100 ? 100 : value);
                if (SetField(ref _miniOpacityPercent, v, "MiniOpacityPercent"))
                {
                    RaisePropertyChanged("MiniOpacityText");
                    SaveSettingsField(delegate(AppSettings s) { s.MiniWindowOpacity = v / 100.0; });
                    _main.ApplyMiniWindowOpacity();
                }
            }
        }

        public string MiniOpacityText { get { return string.Format(CultureInfo.InvariantCulture, "{0}%", _miniOpacityPercent); } }

        public int FocusMinutes
        {
            get { return _focusMinutes; }
            set
            {
                if (SetField(ref _focusMinutes, value, "FocusMinutes"))
                {
                    SaveTimerDurationSetting(delegate(AppSettings s) { s.FocusMinutes = value; });
                }
            }
        }

        public int ShortBreakMinutes
        {
            get { return _shortBreakMinutes; }
            set
            {
                if (SetField(ref _shortBreakMinutes, value, "ShortBreakMinutes"))
                {
                    SaveTimerDurationSetting(delegate(AppSettings s) { s.ShortBreakMinutes = value; });
                }
            }
        }

        public int LongBreakMinutes
        {
            get { return _longBreakMinutes; }
            set
            {
                if (SetField(ref _longBreakMinutes, value, "LongBreakMinutes"))
                {
                    SaveTimerDurationSetting(delegate(AppSettings s) { s.LongBreakMinutes = value; });
                }
            }
        }

        public int LongBreakInterval
        {
            get { return _longBreakInterval; }
            set
            {
                if (SetField(ref _longBreakInterval, value, "LongBreakInterval"))
                {
                    SaveTimerDurationSetting(delegate(AppSettings s) { s.LongBreakInterval = value; });
                }
            }
        }

        public int DailyGoal
        {
            get { return _dailyGoal; }
            set
            {
                if (SetField(ref _dailyGoal, value, "DailyGoal"))
                {
                    SaveSettingsField(delegate(AppSettings s) { s.DailyGoal = value; });
                    _main.RefreshAllDataViews();
                }
            }
        }

        public DayStartHourOption SelectedDayStart
        {
            get { return _selectedDayStart; }
            set
            {
                if (SetField(ref _selectedDayStart, value, "SelectedDayStart") && value != null)
                {
                    SaveSettingsField(delegate(AppSettings s) { s.DayStartHour = value.Hour; });
                    _main.RefreshAllDataViews();
                }
            }
        }

        public bool ShowNextPhaseHint
        {
            get { return _showNextPhaseHint; }
            private set { SetField(ref _showNextPhaseHint, value, "ShowNextPhaseHint"); }
        }

        public bool AutoStartBreaks
        {
            get { return _autoStartBreaks; }
            set { if (SetField(ref _autoStartBreaks, value, "AutoStartBreaks")) SaveSettingsField(delegate(AppSettings s) { s.AutoStartBreaks = value; }); }
        }

        public bool AutoStartFocus
        {
            get { return _autoStartFocus; }
            set { if (SetField(ref _autoStartFocus, value, "AutoStartFocus")) SaveSettingsField(delegate(AppSettings s) { s.AutoStartFocus = value; }); }
        }

        public bool RestoreTimerOnLaunch
        {
            get { return _restoreTimerOnLaunch; }
            set { if (SetField(ref _restoreTimerOnLaunch, value, "RestoreTimerOnLaunch")) SaveSettingsField(delegate(AppSettings s) { s.RestoreTimerOnLaunch = value; }); }
        }

        public bool HideMainOnMini
        {
            get { return _hideMainOnMini; }
            set { if (SetField(ref _hideMainOnMini, value, "HideMainOnMini")) SaveSettingsField(delegate(AppSettings s) { s.HideMainOnMini = value; }); }
        }

        public NamedKeyOption SelectedNotificationSound
        {
            get { return _selectedNotificationSound; }
            set
            {
                if (SetField(ref _selectedNotificationSound, value, "SelectedNotificationSound") && value != null)
                {
                    SaveSettingsField(delegate(AppSettings s) { s.NotificationSound = value.Key; });
                }
            }
        }

        public int NotificationVolume
        {
            get { return _notificationVolume; }
            set
            {
                if (SetField(ref _notificationVolume, value, "NotificationVolume"))
                {
                    RaisePropertyChanged("NotificationVolumeText");
                    SaveSettingsField(delegate(AppSettings s) { s.NotificationVolume = value; });
                }
            }
        }

        public string NotificationVolumeText { get { return string.Format(CultureInfo.InvariantCulture, "{0}%", _notificationVolume); } }

        public bool OneMinuteWarning
        {
            get { return _oneMinuteWarning; }
            set { if (SetField(ref _oneMinuteWarning, value, "OneMinuteWarning")) SaveSettingsField(delegate(AppSettings s) { s.OneMinuteWarning = value; }); }
        }

        public bool EnableDesktopNotification
        {
            get { return _enableDesktopNotification; }
            set { if (SetField(ref _enableDesktopNotification, value, "EnableDesktopNotification")) SaveSettingsField(delegate(AppSettings s) { s.EnableDesktopNotification = value; }); }
        }

        public bool EnableInAppToast
        {
            get { return _enableInAppToast; }
            set { if (SetField(ref _enableInAppToast, value, "EnableInAppToast")) SaveSettingsField(delegate(AppSettings s) { s.EnableInAppToast = value; }); }
        }

        public bool EnableGlobalHotkeys
        {
            get { return _enableGlobalHotkeys; }
            set
            {
                if (SetField(ref _enableGlobalHotkeys, value, "EnableGlobalHotkeys"))
                {
                    SaveSettingsField(delegate(AppSettings s) { s.EnableGlobalHotkeys = value; });
                    ReapplyHotkeys();
                }
            }
        }

        public string HotkeyToggle { get { return _hotkeyToggle; } set { if (SetField(ref _hotkeyToggle, value, "HotkeyToggle")) UpdateHotkey(delegate(AppSettings s) { s.HotkeyToggle = value; }); } }
        public string HotkeySkip { get { return _hotkeySkip; } set { if (SetField(ref _hotkeySkip, value, "HotkeySkip")) UpdateHotkey(delegate(AppSettings s) { s.HotkeySkip = value; }); } }
        public string HotkeyMini { get { return _hotkeyMini; } set { if (SetField(ref _hotkeyMini, value, "HotkeyMini")) UpdateHotkey(delegate(AppSettings s) { s.HotkeyMini = value; }); } }
        public string HotkeyMain { get { return _hotkeyMain; } set { if (SetField(ref _hotkeyMain, value, "HotkeyMain")) UpdateHotkey(delegate(AppSettings s) { s.HotkeyMain = value; }); } }

        public bool HotkeyToggleOk { get { return _hotkeyToggleOk; } private set { SetField(ref _hotkeyToggleOk, value, "HotkeyToggleOk"); } }
        public bool HotkeySkipOk { get { return _hotkeySkipOk; } private set { SetField(ref _hotkeySkipOk, value, "HotkeySkipOk"); } }
        public bool HotkeyMiniOk { get { return _hotkeyMiniOk; } private set { SetField(ref _hotkeyMiniOk, value, "HotkeyMiniOk"); } }
        public bool HotkeyMainOk { get { return _hotkeyMainOk; } private set { SetField(ref _hotkeyMainOk, value, "HotkeyMainOk"); } }

        public bool LaunchAtStartup
        {
            get { return _launchAtStartup; }
            set
            {
                if (_loading)
                {
                    SetField(ref _launchAtStartup, value, "LaunchAtStartup");
                    return;
                }
                if (_launchAtStartup == value) return;
                string err;
                if (!StartupRegistryService.SetEnabled(value, out err))
                {
                    _main.ShowSnackbar(err ?? "设置开机自启失败", null, null);
                    RaisePropertyChanged("LaunchAtStartup");
                    return;
                }
                SetField(ref _launchAtStartup, value, "LaunchAtStartup");
                SaveSettingsField(delegate(AppSettings s) { s.LaunchAtStartup = value; });
            }
        }

        public bool ShowTrayIcon
        {
            get { return _showTrayIcon; }
            set
            {
                if (SetField(ref _showTrayIcon, value, "ShowTrayIcon"))
                {
                    RaisePropertyChanged("CanMinimizeToTray");
                    SaveSettingsField(delegate(AppSettings s) { s.ShowTrayIcon = value; });
                    _main.UpdateTrayVisibility();
                }
            }
        }

        public bool CanMinimizeToTray { get { return _showTrayIcon; } }

        public bool MinimizeToTrayOnClose
        {
            get { return _minimizeToTrayOnClose; }
            set { if (SetField(ref _minimizeToTrayOnClose, value, "MinimizeToTrayOnClose")) SaveSettingsField(delegate(AppSettings s) { s.MinimizeToTrayOnClose = value; }); }
        }

        public void LoadFromSettings(AppSettings s)
        {
            _loading = true;
            try
            {
                SyncSelectedThemeCard(s.Theme);
                ReduceMotion = s.ReduceMotion;
                MiniOpacityPercent = (int)Math.Round(s.MiniWindowOpacity * 100.0);

                FocusMinutes = s.FocusMinutes;
                ShortBreakMinutes = s.ShortBreakMinutes;
                LongBreakMinutes = s.LongBreakMinutes;
                LongBreakInterval = s.LongBreakInterval;
                DailyGoal = s.DailyGoal;
                SelectedDayStart = DayStartOptions[Math.Max(0, Math.Min(6, s.DayStartHour))];

                AutoStartBreaks = s.AutoStartBreaks;
                AutoStartFocus = s.AutoStartFocus;
                RestoreTimerOnLaunch = s.RestoreTimerOnLaunch;
                HideMainOnMini = s.HideMainOnMini;

                for (int i = 0; i < NotificationSoundOptions.Count; i++)
                {
                    if (NotificationSoundOptions[i].Key == s.NotificationSound)
                    {
                        SelectedNotificationSound = NotificationSoundOptions[i];
                        break;
                    }
                }
                NotificationVolume = s.NotificationVolume;
                OneMinuteWarning = s.OneMinuteWarning;
                EnableDesktopNotification = s.EnableDesktopNotification;
                EnableInAppToast = s.EnableInAppToast;

                EnableGlobalHotkeys = s.EnableGlobalHotkeys;
                HotkeyToggle = s.HotkeyToggle;
                HotkeySkip = s.HotkeySkip;
                HotkeyMini = s.HotkeyMini;
                HotkeyMain = s.HotkeyMain;

                LaunchAtStartup = s.LaunchAtStartup;
                ShowTrayIcon = s.ShowTrayIcon;
                MinimizeToTrayOnClose = s.MinimizeToTrayOnClose;
                ShowNextPhaseHint = false;
            }
            finally
            {
                _loading = false;
            }
            RefreshHotkeyStatuses();
        }

        public void SyncSelectedThemeCard(string themeKey)
        {
            for (int i = 0; i < ThemeCards.Count; i++)
            {
                ThemeCards[i].IsSelected = string.Equals(ThemeCards[i].Key, themeKey, StringComparison.OrdinalIgnoreCase);
            }
        }

        public void RefreshHotkeyStatuses()
        {
            if (!_enableGlobalHotkeys)
            {
                HotkeyToggleOk = true;
                HotkeySkipOk = true;
                HotkeyMiniOk = true;
                HotkeyMainOk = true;
                return;
            }
            HotkeyToggleOk = _main.Hotkeys.IsRegistered(HotkeyManager.IdToggle);
            HotkeySkipOk = _main.Hotkeys.IsRegistered(HotkeyManager.IdSkip);
            HotkeyMiniOk = _main.Hotkeys.IsRegistered(HotkeyManager.IdMini);
            HotkeyMainOk = _main.Hotkeys.IsRegistered(HotkeyManager.IdMain);
        }

        private void SelectTheme(string themeKey)
        {
            if (string.IsNullOrEmpty(themeKey)) return;
            SyncSelectedThemeCard(themeKey);
            _main.SwitchTheme(themeKey, false);
        }

        private void PreviewNotificationSound()
        {
            string key = _selectedNotificationSound != null ? _selectedNotificationSound.Key : "chime";
            if (key == "none") return;
            _main.Mixer.PlayNotification(key, _notificationVolume);
            _main.WaveOutPlayer.Wake();
        }

        private void RestoreDefaultHotkeys()
        {
            _loading = true;
            try
            {
                HotkeyToggle = "Ctrl+Alt+Space";
                HotkeySkip = "Ctrl+Alt+Right";
                HotkeyMini = "Ctrl+Alt+M";
                HotkeyMain = "Ctrl+Alt+P";
            }
            finally
            {
                _loading = false;
            }
            _main.Settings.HotkeyToggle = HotkeyToggle;
            _main.Settings.HotkeySkip = HotkeySkip;
            _main.Settings.HotkeyMini = HotkeyMini;
            _main.Settings.HotkeyMain = HotkeyMain;
            _main.SaveSettingsDebounced();
            ReapplyHotkeys();
        }

        private void UpdateHotkey(Action<AppSettings> mutator)
        {
            if (_loading) return;
            mutator(_main.Settings);
            _main.SaveSettingsDebounced();
            ReapplyHotkeys();
        }

        private void ReapplyHotkeys()
        {
            if (_loading) return;
            _main.RegisterConfiguredHotkeys();
            RefreshHotkeyStatuses();
        }

        private void SaveTimerDurationSetting(Action<AppSettings> mutator)
        {
            if (_loading) return;
            mutator(_main.Settings);
            _main.Settings.Clamp();
            _main.Engine.UpdateSettings(_main.Settings);
            ShowNextPhaseHint = _main.Engine.Snapshot.State != TimerRunState.Idle;
            _main.SaveSettingsDebounced();
            _main.RefreshAllDataViews();
        }

        private void SaveSettingsField(Action<AppSettings> mutator)
        {
            if (_loading) return;
            mutator(_main.Settings);
            _main.Settings.Clamp();
            _main.SaveSettingsDebounced();
        }
    }
}
