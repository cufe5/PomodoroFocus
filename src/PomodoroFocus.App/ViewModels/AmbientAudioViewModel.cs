using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using PomodoroFocus.Core.Models;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.App.ViewModels
{
    public sealed class SoundTrackViewModel : ObservableObject
    {
        private readonly AmbientAudioViewModel _parent;
        private bool _isActive;
        private bool _isActivelyPlaying;
        private int _volume;
        private string _statusText = "点击播放";

        public string Id { get; private set; }
        public string Name { get; private set; }
        public string ShortName { get; private set; }
        public string GeometryKey { get; private set; }
        public string AutomationId { get; private set; }
        public Color TintColor { get; private set; }
        public SolidColorBrush TintBrush { get; private set; }
        public SolidColorBrush TintSoftBrush { get; private set; }
        public SolidColorBrush TintBorderBrush { get; private set; }
        public LinearGradientBrush ActiveBgBrush { get; private set; }

        public SoundTrackViewModel(AmbientAudioViewModel parent, string id, string name, string geoKey, string tintHex)
        {
            _parent = parent;
            Id = id;
            Name = name;
            ShortName = id == "forest" ? "森林" : (id == "cafe" ? "咖啡" : name);
            GeometryKey = geoKey;
            AutomationId = "SoundCard_" + id;
            TintColor = (Color)ColorConverter.ConvertFromString(tintHex);
            TintBrush = new SolidColorBrush(TintColor);
            TintSoftBrush = new SolidColorBrush(Color.FromArgb(0x2E, TintColor.R, TintColor.G, TintColor.B));
            TintBorderBrush = new SolidColorBrush(Color.FromArgb(0x55, TintColor.R, TintColor.G, TintColor.B));

            var grad = new LinearGradientBrush { StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 1) };
            grad.GradientStops.Add(new GradientStop(Color.FromArgb(0x1E, TintColor.R, TintColor.G, TintColor.B), 0.0));
            grad.GradientStops.Add(new GradientStop(Color.FromArgb(0x0A, TintColor.R, TintColor.G, TintColor.B), 1.0));
            ActiveBgBrush = grad;
        }

        public bool IsActive
        {
            get { return _isActive; }
            set
            {
                if (SetField(ref _isActive, value, "IsActive"))
                {
                    RefreshStatusText();
                }
            }
        }

        public bool IsActivelyPlaying
        {
            get { return _isActivelyPlaying; }
            set
            {
                if (SetField(ref _isActivelyPlaying, value, "IsActivelyPlaying"))
                {
                    RefreshStatusText();
                }
            }
        }

        public int Volume
        {
            get { return _volume; }
            set
            {
                int clamped = value < 0 ? 0 : (value > 100 ? 100 : value);
                if (SetField(ref _volume, clamped, "Volume"))
                {
                    RefreshStatusText();
                    _parent.OnTrackVolumeChanged(this);
                }
            }
        }

        public string StatusText
        {
            get { return _statusText; }
            private set { SetField(ref _statusText, value, "StatusText"); }
        }

        public void SetVolumeSilently(int vol)
        {
            _volume = vol < 0 ? 0 : (vol > 100 ? 100 : vol);
            RaisePropertyChanged("Volume");
            RefreshStatusText();
        }

        public void RefreshStatusText()
        {
            if (!_isActive)
            {
                StatusText = "点击播放";
            }
            else if (_isActivelyPlaying)
            {
                StatusText = string.Format(CultureInfo.InvariantCulture, "播放中 · {0}%", _volume);
            }
            else
            {
                StatusText = string.Format(CultureInfo.InvariantCulture, "将在专注开始时播放 · {0}%", _volume);
            }
        }
    }

    public sealed class SleepTimerOption
    {
        public int Minutes { get; set; }
        public string Label { get; set; }
        public override string ToString() { return Label; }
    }

    public sealed class AmbientPresetViewModel : ObservableObject
    {
        private bool _isActive;
        public string Key { get; set; }
        public string Label { get; set; }
        public Dictionary<string, int> Tracks { get; set; }

        public bool IsActive
        {
            get { return _isActive; }
            set { SetField(ref _isActive, value, "IsActive"); }
        }
    }

    public sealed class AmbientAudioViewModel : ObservableObject
    {
        private readonly MainViewModel _main;
        private bool _suppressPersist;

        private int _masterVolume = 75;
        private bool _isMasterMuted;
        private bool _onlyDuringFocus = true;
        private bool _muteOnPause = true;
        private SleepTimerOption _selectedSleepOption;
        private string _sleepRemainingText = string.Empty;
        private bool _isAudioDeviceAvailable = true;
        private string _audioDeviceError = string.Empty;
        private int _activeTrackCount;
        private bool _isAudibleNow;
        private string _quickStatusSummary = "未开启";

        public AmbientAudioViewModel(MainViewModel main)
        {
            _main = main;
            Tracks = new ObservableCollection<SoundTrackViewModel>();
            QuickTracks = new ObservableCollection<SoundTrackViewModel>();
            Presets = new ObservableCollection<AmbientPresetViewModel>();

            var defs = new string[][]
            {
                new string[] { "rain", "细雨", "Geo.rain", "#0A84FF" },
                new string[] { "ocean", "海浪", "Geo.waves", "#32ADE6" },
                new string[] { "forest", "林间鸟鸣", "Geo.tree", "#30D158" },
                new string[] { "cafe", "咖啡馆", "Geo.coffee", "#AC8E68" },
                new string[] { "fire", "篝火", "Geo.flame", "#FF9F0A" },
                new string[] { "wind", "微风", "Geo.wind", "#5E5CE6" },
                new string[] { "white", "白噪音", "Geo.wave", "#8E8E93" },
                new string[] { "brown", "棕噪音", "Geo.moon", "#BF5AF2" },
                new string[] { "clock", "时钟滴答", "Geo.clock", "#FF375F" }
            };
            for (int i = 0; i < defs.Length; i++)
            {
                var vm = new SoundTrackViewModel(this, defs[i][0], defs[i][1], defs[i][2], defs[i][3]);
                Tracks.Add(vm);
                if (i < 4) QuickTracks.Add(vm);
            }

            Presets.Add(new AmbientPresetViewModel
            {
                Key = "rainy_study",
                Label = "雨夜书房",
                Tracks = new Dictionary<string, int> { { "rain", 70 }, { "fire", 35 }, { "clock", 20 } }
            });
            Presets.Add(new AmbientPresetViewModel
            {
                Key = "forest_morning",
                Label = "林间晨光",
                Tracks = new Dictionary<string, int> { { "forest", 70 }, { "wind", 30 } }
            });
            Presets.Add(new AmbientPresetViewModel
            {
                Key = "seaside_cafe",
                Label = "海边咖啡",
                Tracks = new Dictionary<string, int> { { "ocean", 50 }, { "cafe", 55 } }
            });
            Presets.Add(new AmbientPresetViewModel
            {
                Key = "deep_focus",
                Label = "深度专注",
                Tracks = new Dictionary<string, int> { { "brown", 60 } }
            });

            SleepTimerOptions = new List<SleepTimerOption>
            {
                new SleepTimerOption { Minutes = 0, Label = "不限" },
                new SleepTimerOption { Minutes = 15, Label = "15 分钟" },
                new SleepTimerOption { Minutes = 30, Label = "30 分钟" },
                new SleepTimerOption { Minutes = 60, Label = "60 分钟" },
                new SleepTimerOption { Minutes = 90, Label = "90 分钟" }
            };
            _selectedSleepOption = SleepTimerOptions[0];

            ToggleTrackCommand = new RelayCommand<SoundTrackViewModel>(ToggleTrack);
            ApplyPresetCommand = new RelayCommand<AmbientPresetViewModel>(ApplyPreset);
            StopAllCommand = new RelayCommand(StopAll);
            ToggleMuteCommand = new RelayCommand(delegate { IsMasterMuted = !IsMasterMuted; });
        }

        public ObservableCollection<SoundTrackViewModel> Tracks { get; private set; }
        public ObservableCollection<SoundTrackViewModel> QuickTracks { get; private set; }
        public ObservableCollection<AmbientPresetViewModel> Presets { get; private set; }
        public IList<SleepTimerOption> SleepTimerOptions { get; private set; }

        public ICommand ToggleTrackCommand { get; private set; }
        public ICommand ApplyPresetCommand { get; private set; }
        public ICommand StopAllCommand { get; private set; }
        public ICommand ToggleMuteCommand { get; private set; }

        public int MasterVolume
        {
            get { return _masterVolume; }
            set
            {
                int v = value < 0 ? 0 : (value > 100 ? 100 : value);
                if (SetField(ref _masterVolume, v, "MasterVolume"))
                {
                    RaisePropertyChanged("MasterVolumeText");
                    if (v > 0 && _isMasterMuted)
                    {
                        _isMasterMuted = false;
                        RaisePropertyChanged("IsMasterMuted");
                    }
                    PushToMixerAndSave();
                }
            }
        }

        public string MasterVolumeText
        {
            get { return string.Format(CultureInfo.InvariantCulture, "{0}%", _masterVolume); }
        }

        public bool IsMasterMuted
        {
            get { return _isMasterMuted; }
            set
            {
                if (SetField(ref _isMasterMuted, value, "IsMasterMuted"))
                {
                    PushToMixerAndSave();
                }
            }
        }

        public bool OnlyDuringFocus
        {
            get { return _onlyDuringFocus; }
            set
            {
                if (SetField(ref _onlyDuringFocus, value, "OnlyDuringFocus"))
                {
                    PushToMixerAndSave();
                }
            }
        }

        public bool MuteOnPause
        {
            get { return _muteOnPause; }
            set
            {
                if (SetField(ref _muteOnPause, value, "MuteOnPause"))
                {
                    PushToMixerAndSave();
                }
            }
        }

        public SleepTimerOption SelectedSleepOption
        {
            get { return _selectedSleepOption; }
            set
            {
                if (SetField(ref _selectedSleepOption, value, "SelectedSleepOption") && value != null)
                {
                    _main.Mixer.SetSleepTimerMinutes(value.Minutes);
                    UpdateSleepCountdown();
                }
            }
        }

        public string SleepRemainingText
        {
            get { return _sleepRemainingText; }
            private set
            {
                if (SetField(ref _sleepRemainingText, value, "SleepRemainingText"))
                {
                    RaisePropertyChanged("HasSleepRemaining");
                }
            }
        }

        public bool HasSleepRemaining { get { return !string.IsNullOrEmpty(_sleepRemainingText); } }

        public bool IsAudioDeviceAvailable
        {
            get { return _isAudioDeviceAvailable; }
            private set { SetField(ref _isAudioDeviceAvailable, value, "IsAudioDeviceAvailable"); }
        }

        public string AudioDeviceError
        {
            get { return _audioDeviceError; }
            private set { SetField(ref _audioDeviceError, value, "AudioDeviceError"); }
        }

        public int ActiveTrackCount
        {
            get { return _activeTrackCount; }
            private set { SetField(ref _activeTrackCount, value, "ActiveTrackCount"); }
        }

        public bool IsAudibleNow
        {
            get { return _isAudibleNow; }
            private set { SetField(ref _isAudibleNow, value, "IsAudibleNow"); }
        }

        public string QuickStatusSummary
        {
            get { return _quickStatusSummary; }
            private set { SetField(ref _quickStatusSummary, value, "QuickStatusSummary"); }
        }

        public void LoadFromSettings(AppSettings settings)
        {
            _suppressPersist = true;
            try
            {
                IsAudioDeviceAvailable = _main.WaveOutPlayer.IsAudioDeviceAvailable;
                AudioDeviceError = _main.WaveOutPlayer.DeviceErrorMessage ?? string.Empty;

                _masterVolume = settings.AmbientMasterVolume;
                _onlyDuringFocus = settings.AmbientOnlyDuringFocus;
                _muteOnPause = settings.AmbientMuteOnPause;
                RaisePropertyChanged("MasterVolume");
                RaisePropertyChanged("MasterVolumeText");
                RaisePropertyChanged("OnlyDuringFocus");
                RaisePropertyChanged("MuteOnPause");

                for (int i = 0; i < Tracks.Count; i++)
                {
                    SoundTrackViewModel t = Tracks[i];
                    int vol;
                    if (!settings.AmbientTrackVolumes.TryGetValue(t.Id, out vol)) vol = 60;
                    t.SetVolumeSilently(vol);
                    t.IsActive = settings.AmbientActiveTracks != null && settings.AmbientActiveTracks.Contains(t.Id);
                }
            }
            finally
            {
                _suppressPersist = false;
            }
            SyncMixerGateAndStatus();
            UpdatePresetHighlights();
        }

        public void ToggleTrackById(string id)
        {
            for (int i = 0; i < Tracks.Count; i++)
            {
                if (Tracks[i].Id == id)
                {
                    ToggleTrack(Tracks[i]);
                    return;
                }
            }
        }

        public void ToggleTrackFromQuickBar(string id)
        {
            ToggleTrackById(id);
        }

        public void ToggleTrack(SoundTrackViewModel track)
        {
            if (track == null || !IsAudioDeviceAvailable) return;
            track.IsActive = !track.IsActive;
            if (track.IsActive && track.Volume == 0)
            {
                track.SetVolumeSilently(60);
            }
            PushToMixerAndSave();
        }

        public void OnTrackVolumeChanged(SoundTrackViewModel track)
        {
            if (_suppressPersist || track == null) return;
            if (!track.IsActive && track.Volume > 0)
            {
                track.IsActive = true;
            }
            PushToMixerAndSave();
        }

        public void ApplyPreset(AmbientPresetViewModel preset)
        {
            if (preset == null || !IsAudioDeviceAvailable) return;
            _suppressPersist = true;
            try
            {
                for (int i = 0; i < Tracks.Count; i++)
                {
                    SoundTrackViewModel t = Tracks[i];
                    int vol;
                    if (preset.Tracks.TryGetValue(t.Id, out vol))
                    {
                        t.SetVolumeSilently(vol);
                        t.IsActive = true;
                    }
                    else
                    {
                        t.IsActive = false;
                    }
                }
            }
            finally
            {
                _suppressPersist = false;
            }
            PushToMixerAndSave();
        }

        public void StopAll()
        {
            _suppressPersist = true;
            try
            {
                for (int i = 0; i < Tracks.Count; i++)
                {
                    Tracks[i].IsActive = false;
                }
            }
            finally
            {
                _suppressPersist = false;
            }
            _main.Mixer.StopAllTracks();
            PushToMixerAndSave();
        }

        public void OnSleepTimerExpiredFromMixer()
        {
            _suppressPersist = true;
            try
            {
                for (int i = 0; i < Tracks.Count; i++)
                {
                    Tracks[i].IsActive = false;
                }
                _selectedSleepOption = SleepTimerOptions[0];
                RaisePropertyChanged("SelectedSleepOption");
                SleepRemainingText = string.Empty;
            }
            finally
            {
                _suppressPersist = false;
            }
            PushToMixerAndSave();
        }

        public void UpdateSleepCountdown()
        {
            int rem = _main.Mixer.GetSleepTimerRemainingSeconds();
            if (rem > 0)
            {
                SleepRemainingText = Formatters.FormatTimer(rem) + " 后停止";
            }
            else
            {
                SleepRemainingText = string.Empty;
            }
        }

        public void SyncMixerGateAndStatus()
        {
            TimerSnapshot snap = _main.Engine.Snapshot;
            bool gateOpen = true;
            if (_onlyDuringFocus && snap.Phase != TimerPhase.Focus)
            {
                gateOpen = false;
            }
            if (_onlyDuringFocus && _muteOnPause && snap.State != TimerRunState.Running)
            {
                gateOpen = false;
            }
            else if (_muteOnPause && snap.State == TimerRunState.Paused)
            {
                gateOpen = false;
            }

            _main.Mixer.SetPlaybackGate(gateOpen);
            _main.Mixer.SetMasterVolume(_masterVolume, _isMasterMuted);

            int activeCount = 0;
            string activePresetName = null;
            for (int i = 0; i < Tracks.Count; i++)
            {
                SoundTrackViewModel t = Tracks[i];
                _main.Mixer.SetTrackState(t.Id, t.IsActive, t.Volume);
                t.IsActivelyPlaying = t.IsActive && gateOpen && !_isMasterMuted && _masterVolume > 0;
                if (t.IsActive) activeCount++;
            }

            ActiveTrackCount = activeCount;
            IsAudibleNow = activeCount > 0 && gateOpen && !_isMasterMuted && _masterVolume > 0;

            UpdatePresetHighlights();
            for (int i = 0; i < Presets.Count; i++)
            {
                if (Presets[i].IsActive)
                {
                    activePresetName = Presets[i].Label;
                    break;
                }
            }

            if (activeCount == 0)
            {
                QuickStatusSummary = "点击下方开启";
            }
            else if (activePresetName != null)
            {
                QuickStatusSummary = activePresetName;
            }
            else
            {
                QuickStatusSummary = string.Format(CultureInfo.InvariantCulture, "已选 {0} 路混音", activeCount);
            }

            if (IsAudibleNow)
            {
                _main.WaveOutPlayer.Wake();
            }

            _main.NotifyAmbientStateChanged();
        }

        private void PushToMixerAndSave()
        {
            if (!_suppressPersist)
            {
                AppSettings s = _main.Settings;
                s.AmbientMasterVolume = _masterVolume;
                s.AmbientOnlyDuringFocus = _onlyDuringFocus;
                s.AmbientMuteOnPause = _muteOnPause;
                s.AmbientActiveTracks = new List<string>();
                for (int i = 0; i < Tracks.Count; i++)
                {
                    s.AmbientTrackVolumes[Tracks[i].Id] = Tracks[i].Volume;
                    if (Tracks[i].IsActive) s.AmbientActiveTracks.Add(Tracks[i].Id);
                }
                _main.SaveSettingsDebounced();
            }
            SyncMixerGateAndStatus();
        }

        private void UpdatePresetHighlights()
        {
            for (int p = 0; p < Presets.Count; p++)
            {
                AmbientPresetViewModel preset = Presets[p];
                bool match = true;
                for (int i = 0; i < Tracks.Count; i++)
                {
                    SoundTrackViewModel t = Tracks[i];
                    int expectedVol;
                    bool inPreset = preset.Tracks.TryGetValue(t.Id, out expectedVol);
                    if (inPreset)
                    {
                        if (!t.IsActive || t.Volume != expectedVol) { match = false; break; }
                    }
                    else
                    {
                        if (t.IsActive) { match = false; break; }
                    }
                }
                preset.IsActive = match;
            }
        }
    }
}
