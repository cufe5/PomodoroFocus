using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using PomodoroFocus.App.Controls;
using PomodoroFocus.App.Interop;
using PomodoroFocus.App.Themes;
using PomodoroFocus.App.Views;
using PomodoroFocus.Core.Audio;
using PomodoroFocus.Core.Engine;
using PomodoroFocus.Core.Models;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.App.ViewModels
{
    public sealed class MainViewModel : ObservableObject, IDisposable
    {
        private readonly DispatcherTimer _tickTimer;
        private readonly DispatcherTimer _snackbarTimer;
        private readonly List<FocusSession> _sessions;

        private FocusSession _lastDeletedSession;
        private int _lastDeletedSessionIndex = -1;
        private Action _snackbarUndoAction;

        private string _activeViewKey = "focus";
        private bool _isCompactLayout;
        private bool _isZenMode;
        private bool _isAlwaysOnTop;
        private bool _isMiniWindowOpen;

        private string _infoBarMessage = string.Empty;
        private bool _hasInfoBar;

        private string _snackbarMessage = string.Empty;
        private string _snackbarActionText = string.Empty;
        private bool _isSnackbarVisible;

        private int _sidebarActiveTaskCount;
        private string _sidebarGoalCountText = "0/8";
        private double _sidebarGoalPercent;
        private string _sidebarTodayDurationText = "已专注 0 分钟";

        private bool _showTitleBarTimerPill;
        private string _titleBarTimerPillText = string.Empty;

        // Task Edit Modal state
        private bool _isTaskModalOpen;
        private string _taskModalDialogTitle = "新建任务";
        private string _editingTaskId;
        private string _taskDraftTitle = string.Empty;
        private string _taskDraftNotes = string.Empty;
        private string _taskDraftPriorityKey = "0";
        private int _taskDraftEstimate = 1;
        private string _taskDraftTagInput = string.Empty;
        private string _taskDraftError = string.Empty;
        private readonly List<SegmentOption> _priorityOptions;

        // Confirm Modal state
        private bool _isConfirmModalOpen;
        private string _confirmTitle = string.Empty;
        private string _confirmMessage = string.Empty;
        private string _confirmCancelText = "取消";
        private string _confirmOkText = "确定";
        private bool _confirmIsDanger;
        private Action _confirmOkAction;

        // Clear Data Modal state
        private bool _isClearDataModalOpen;
        private string _clearDataInput = string.Empty;

        private string _dailyGoalNotifiedDate;
        private Window _mainWindow;
        private System.Windows.Controls.Image _crossfadeImage;
        private MiniTimerWindow _miniWindow;
        private ToastWindow _activeToast;
        private bool _disposed;

        public StorageService Storage { get; private set; }
        public AppSettings Settings { get; private set; }
        public PomodoroEngine Engine { get; private set; }
        public TaskService TaskService { get; private set; }
        public AmbientMixer Mixer { get; private set; }
        public WaveOutPlayer WaveOutPlayer { get; private set; }
        public ThemeService ThemeService { get; private set; }
        public TrayIconService TrayService { get; private set; }
        public HotkeyManager Hotkeys { get; private set; }

        public FocusViewModel FocusVM { get; private set; }
        public TasksViewModel TasksVM { get; private set; }
        public StatsViewModel StatsVM { get; private set; }
        public AmbientAudioViewModel AudioVM { get; private set; }
        public SettingsViewModel SettingsVM { get; private set; }

        public IList<FocusSession> Sessions { get { return _sessions; } }
        public ObservableCollection<string> TaskDraftTags { get; private set; }
        public IList<SegmentOption> PriorityOptions { get { return _priorityOptions; } }

        public event EventHandler CelebrationTriggered;
        public event EventHandler FocusQuickAddRequested;
        public event EventHandler FocusTaskSearchRequested;
        public event EventHandler ExitApplicationRequested;

        public MainViewModel(
            string dataDir,
            bool seedDemo,
            bool noTray,
            bool noHotkeys,
            bool noSound,
            string themeOverride)
        {
            Storage = new StorageService(dataDir);
            if (seedDemo && !Storage.IsReadOnlyMode)
            {
                DemoDataSeeder.Seed(Storage, DateTime.Now, TimeZoneInfo.Local);
            }

            Settings = Storage.LoadSettings();
            if (!string.IsNullOrEmpty(themeOverride) && SoundCatalog.IsValidThemeKey(themeOverride))
            {
                Settings.Theme = themeOverride;
            }
            AnimationHelper.ReduceMotionSetting = Settings.ReduceMotion;

            List<TaskItem> loadedTasks = Storage.LoadTasks();
            TaskService = new TaskService(loadedTasks);
            _sessions = Storage.LoadSessions();

            if (!string.IsNullOrEmpty(Settings.ActiveTaskId))
            {
                TaskItem active = TaskService.FindById(Settings.ActiveTaskId);
                if (active == null || active.IsCompleted)
                {
                    Settings.ActiveTaskId = null;
                }
            }

            if (Storage.IsReadOnlyMode)
            {
                InfoBarMessage = string.Format("数据目录不可写，本次更改不会被保存（{0}）", Storage.DataDirectory);
                HasInfoBar = true;
            }
            else if (!string.IsNullOrEmpty(Storage.RecoveryNotice))
            {
                InfoBarMessage = Storage.RecoveryNotice;
                HasInfoBar = true;
            }

            ThemeService = new ThemeService();
            Mixer = new AmbientMixer();
            Mixer.SleepTimerExpired += delegate
            {
                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        AudioVM.OnSleepTimerExpiredFromMixer();
                    }));
                }
            };
            WaveOutPlayer = new WaveOutPlayer(Mixer, noSound);
            Hotkeys = new HotkeyManager(noHotkeys);
            TrayService = new TrayIconService(noTray);

            Engine = new PomodoroEngine(new SystemClock(), Settings);
            Engine.SetActiveTaskProvider(delegate { return TaskService.FindById(Settings.ActiveTaskId); });
            Engine.Tick += OnEngineTick;
            Engine.PhaseCompleted += OnEnginePhaseCompleted;
            Engine.OneMinuteLeft += OnEngineOneMinuteLeft;
            Engine.SessionInterrupted += OnEngineSessionInterrupted;

            _priorityOptions = new List<SegmentOption>
            {
                new SegmentOption { Key = "0", Label = "无" },
                new SegmentOption { Key = "1", Label = "低" },
                new SegmentOption { Key = "2", Label = "中" },
                new SegmentOption { Key = "3", Label = "高" }
            };
            TaskDraftTags = new ObservableCollection<string>();

            FocusVM = new FocusViewModel(this);
            TasksVM = new TasksViewModel(this);
            StatsVM = new StatsViewModel(this);
            AudioVM = new AmbientAudioViewModel(this);
            SettingsVM = new SettingsViewModel(this);

            NavigateCommand = new RelayCommand<string>(NavigateTo);
            ToggleZenModeCommand = new RelayCommand(delegate { IsZenMode = !IsZenMode; });
            ToggleAlwaysOnTopCommand = new RelayCommand(delegate { IsAlwaysOnTop = !IsAlwaysOnTop; });
            ToggleMiniModeCommand = new RelayCommand(ToggleMiniWindow);
            DismissInfoBarCommand = new RelayCommand(delegate { HasInfoBar = false; });
            SnackbarActionCommand = new RelayCommand(ExecuteSnackbarAction);
            CloseAnyModalCommand = new RelayCommand(CloseAllModals);

            SaveTaskModalCommand = new RelayCommand(SaveTaskModal);
            AddDraftTagCommand = new RelayCommand(AddDraftTag);
            RemoveDraftTagCommand = new RelayCommand<string>(RemoveDraftTag);
            ConfirmModalOkCommand = new RelayCommand(ExecuteConfirmOk);
            ConfirmClearDataCommand = new RelayCommand(ExecuteClearAllData, delegate { return CanConfirmClearData; });

            // Crash / restart timer state restore
            if (!seedDemo && Settings.RestoreTimerOnLaunch)
            {
                TimerPersistedState saved = Storage.LoadTimerState();
                if (saved != null)
                {
                    FocusSession recovered = Engine.RestoreState(saved);
                    if (recovered != null)
                    {
                        RecordCompletedSession(recovered, false);
                    }
                }
            }

            _isAlwaysOnTop = Settings.AlwaysOnTop;
            _activeViewKey = Settings.LastActiveView ?? "focus";

            AudioVM.LoadFromSettings(Settings);
            SettingsVM.LoadFromSettings(Settings);
            RefreshAllDataViews();
            FocusVM.UpdateFromSnapshot(Engine.Snapshot);

            WireTrayEvents();

            _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _tickTimer.Tick += delegate
            {
                Engine.AdvanceTo(DateTime.UtcNow);
                AudioVM.UpdateSleepCountdown();
            };
            _tickTimer.Start();

            _snackbarTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            _snackbarTimer.Tick += delegate
            {
                _snackbarTimer.Stop();
                IsSnackbarVisible = false;
                _snackbarUndoAction = null;
            };
        }

        public ICommand NavigateCommand { get; private set; }
        public ICommand ToggleZenModeCommand { get; private set; }
        public ICommand ToggleAlwaysOnTopCommand { get; private set; }
        public ICommand ToggleMiniModeCommand { get; private set; }
        public ICommand DismissInfoBarCommand { get; private set; }
        public ICommand SnackbarActionCommand { get; private set; }
        public ICommand CloseAnyModalCommand { get; private set; }
        public ICommand SaveTaskModalCommand { get; private set; }
        public ICommand AddDraftTagCommand { get; private set; }
        public ICommand RemoveDraftTagCommand { get; private set; }
        public ICommand ConfirmModalOkCommand { get; private set; }
        public RelayCommand ConfirmClearDataCommand { get; private set; }

        public string ActiveViewKey
        {
            get { return _activeViewKey; }
            set
            {
                if (SetField(ref _activeViewKey, value, "ActiveViewKey"))
                {
                    RaisePropertyChanged("IsFocusView");
                    RaisePropertyChanged("IsTasksView");
                    RaisePropertyChanged("IsStatsView");
                    RaisePropertyChanged("IsAudioView");
                    RaisePropertyChanged("IsSettingsView");
                    RaisePropertyChanged("ShowZenToggleButton");
                    UpdateTitleBarPill();
                    Settings.LastActiveView = value;
                    SaveSettingsDebounced();
                    if (value == "stats") StatsVM.RefreshAll();
                    else if (value == "tasks") TasksVM.RefreshAll();
                }
            }
        }

        public bool IsFocusView { get { return _activeViewKey == "focus"; } }
        public bool IsTasksView { get { return _activeViewKey == "tasks"; } }
        public bool IsStatsView { get { return _activeViewKey == "stats"; } }
        public bool IsAudioView { get { return _activeViewKey == "audio"; } }
        public bool IsSettingsView { get { return _activeViewKey == "settings"; } }

        public bool IsCompactLayout
        {
            get { return _isCompactLayout; }
            set
            {
                if (SetField(ref _isCompactLayout, value, "IsCompactLayout"))
                {
                    RaisePropertyChanged("SidebarWidth");
                    RaisePropertyChanged("PageHorizontalPadding");
                    RaisePropertyChanged("ShowZenToggleButton");
                }
            }
        }

        public bool IsZenMode
        {
            get { return _isZenMode; }
            set
            {
                if (SetField(ref _isZenMode, value, "IsZenMode"))
                {
                    RaisePropertyChanged("ShowZenToggleButton");
                }
            }
        }

        public bool ShowZenToggleButton { get { return IsFocusView && !_isCompactLayout; } }

        public double SidebarWidth { get { return _isCompactLayout ? 68.0 : 204.0; } }
        public Thickness PageHorizontalPadding { get { return _isCompactLayout ? new Thickness(24, 20, 24, 20) : new Thickness(32, 24, 32, 24); } }

        public bool IsAlwaysOnTop
        {
            get { return _isAlwaysOnTop; }
            set
            {
                if (SetField(ref _isAlwaysOnTop, value, "IsAlwaysOnTop"))
                {
                    Settings.AlwaysOnTop = value;
                    if (_mainWindow != null) _mainWindow.Topmost = value;
                    SaveSettingsDebounced();
                }
            }
        }

        public bool IsMiniWindowOpen
        {
            get { return _isMiniWindowOpen; }
            private set { SetField(ref _isMiniWindowOpen, value, "IsMiniWindowOpen"); }
        }

        public string InfoBarMessage { get { return _infoBarMessage; } private set { SetField(ref _infoBarMessage, value, "InfoBarMessage"); } }
        public bool HasInfoBar { get { return _hasInfoBar; } private set { SetField(ref _hasInfoBar, value, "HasInfoBar"); } }

        public string SnackbarMessage { get { return _snackbarMessage; } private set { SetField(ref _snackbarMessage, value, "SnackbarMessage"); } }
        public string SnackbarActionText { get { return _snackbarActionText; } private set { SetField(ref _snackbarActionText, value, "SnackbarActionText"); } }
        public bool HasSnackbarAction { get { return !string.IsNullOrEmpty(_snackbarActionText); } }
        public bool IsSnackbarVisible { get { return _isSnackbarVisible; } private set { SetField(ref _isSnackbarVisible, value, "IsSnackbarVisible"); } }

        public int SidebarActiveTaskCount
        {
            get { return _sidebarActiveTaskCount; }
            private set
            {
                if (SetField(ref _sidebarActiveTaskCount, value, "SidebarActiveTaskCount"))
                {
                    RaisePropertyChanged("HasSidebarActiveTasks");
                }
            }
        }
        public bool HasSidebarActiveTasks { get { return _sidebarActiveTaskCount > 0; } }

        public string SidebarGoalCountText { get { return _sidebarGoalCountText; } private set { SetField(ref _sidebarGoalCountText, value, "SidebarGoalCountText"); } }
        public double SidebarGoalPercent { get { return _sidebarGoalPercent; } private set { SetField(ref _sidebarGoalPercent, value, "SidebarGoalPercent"); } }
        public string SidebarTodayDurationText { get { return _sidebarTodayDurationText; } private set { SetField(ref _sidebarTodayDurationText, value, "SidebarTodayDurationText"); } }

        public bool ShowTitleBarTimerPill { get { return _showTitleBarTimerPill; } private set { SetField(ref _showTitleBarTimerPill, value, "ShowTitleBarTimerPill"); } }
        public string TitleBarTimerPillText { get { return _titleBarTimerPillText; } private set { SetField(ref _titleBarTimerPillText, value, "TitleBarTimerPillText"); } }

        public bool HasAnyModalOpen
        {
            get { return _isTaskModalOpen || _isConfirmModalOpen || _isClearDataModalOpen; }
        }

        public bool IsTaskModalOpen
        {
            get { return _isTaskModalOpen; }
            private set
            {
                if (SetField(ref _isTaskModalOpen, value, "IsTaskModalOpen")) RaisePropertyChanged("HasAnyModalOpen");
            }
        }

        public string TaskModalDialogTitle { get { return _taskModalDialogTitle; } private set { SetField(ref _taskModalDialogTitle, value, "TaskModalDialogTitle"); } }
        public string TaskDraftTitle { get { return _taskDraftTitle; } set { if (SetField(ref _taskDraftTitle, value, "TaskDraftTitle")) TaskDraftError = string.Empty; } }
        public string TaskDraftNotes { get { return _taskDraftNotes; } set { SetField(ref _taskDraftNotes, value, "TaskDraftNotes"); } }
        public string TaskDraftPriorityKey { get { return _taskDraftPriorityKey; } set { SetField(ref _taskDraftPriorityKey, value, "TaskDraftPriorityKey"); } }
        public int TaskDraftEstimate { get { return _taskDraftEstimate; } set { SetField(ref _taskDraftEstimate, value, "TaskDraftEstimate"); } }
        public string TaskDraftTagInput { get { return _taskDraftTagInput; } set { SetField(ref _taskDraftTagInput, value, "TaskDraftTagInput"); } }
        public string TaskDraftError { get { return _taskDraftError; } private set { if (SetField(ref _taskDraftError, value, "TaskDraftError")) RaisePropertyChanged("HasTaskDraftError"); } }
        public bool HasTaskDraftError { get { return !string.IsNullOrEmpty(_taskDraftError); } }

        public bool IsConfirmModalOpen
        {
            get { return _isConfirmModalOpen; }
            private set
            {
                if (SetField(ref _isConfirmModalOpen, value, "IsConfirmModalOpen")) RaisePropertyChanged("HasAnyModalOpen");
            }
        }
        public string ConfirmTitle { get { return _confirmTitle; } private set { SetField(ref _confirmTitle, value, "ConfirmTitle"); } }
        public string ConfirmMessage { get { return _confirmMessage; } private set { SetField(ref _confirmMessage, value, "ConfirmMessage"); } }
        public string ConfirmCancelText { get { return _confirmCancelText; } private set { SetField(ref _confirmCancelText, value, "ConfirmCancelText"); } }
        public string ConfirmOkText { get { return _confirmOkText; } private set { SetField(ref _confirmOkText, value, "ConfirmOkText"); } }
        public bool ConfirmIsDanger { get { return _confirmIsDanger; } private set { SetField(ref _confirmIsDanger, value, "ConfirmIsDanger"); } }

        public bool IsClearDataModalOpen
        {
            get { return _isClearDataModalOpen; }
            private set
            {
                if (SetField(ref _isClearDataModalOpen, value, "IsClearDataModalOpen")) RaisePropertyChanged("HasAnyModalOpen");
            }
        }
        public string ClearDataInput
        {
            get { return _clearDataInput; }
            set
            {
                if (SetField(ref _clearDataInput, value, "ClearDataInput"))
                {
                    RaisePropertyChanged("CanConfirmClearData");
                    ConfirmClearDataCommand.RaiseCanExecuteChanged();
                }
            }
        }
        public bool CanConfirmClearData { get { return string.Equals(_clearDataInput != null ? _clearDataInput.Trim() : string.Empty, "清空", StringComparison.Ordinal); } }

        public void AttachMainWindow(Window window, System.Windows.Controls.Image crossfadeImage)
        {
            _mainWindow = window;
            _crossfadeImage = crossfadeImage;
            _mainWindow.Topmost = _isAlwaysOnTop;

            SwitchTheme(Settings.Theme, true);
            ThemeService.AnimatePhaseBrush(Engine.Snapshot.Phase, true);

            Hotkeys.Attach(window);
            Hotkeys.SetAction(HotkeyManager.IdToggle, delegate { ToggleTimer(); });
            Hotkeys.SetAction(HotkeyManager.IdSkip, delegate { SkipTimer(); });
            Hotkeys.SetAction(HotkeyManager.IdMini, delegate { ToggleMiniWindow(); });
            Hotkeys.SetAction(HotkeyManager.IdMain, delegate { ToggleMainWindowVisibility(); });
            RegisterConfiguredHotkeys();
            SettingsVM.RefreshHotkeyStatuses();

            UpdateTrayVisibility();
            SyncTrayTheme();
            TrayService.UpdateAmbientTracks(Settings.AmbientActiveTracks);
            TrayService.UpdateTimerStatus(Engine.Snapshot, ThemeService.ToDrawingColor(ThemeService.CurrentPhaseStartColor), IsMiniWindowOpen);
        }

        public void RegisterConfiguredHotkeys()
        {
            bool en = Settings.EnableGlobalHotkeys;
            Hotkeys.Register(HotkeyManager.IdToggle, Settings.HotkeyToggle, en);
            Hotkeys.Register(HotkeyManager.IdSkip, Settings.HotkeySkip, en);
            Hotkeys.Register(HotkeyManager.IdMini, Settings.HotkeyMini, en);
            Hotkeys.Register(HotkeyManager.IdMain, Settings.HotkeyMain, en);
        }

        public void SwitchTheme(string themeKey, bool immediate)
        {
            Settings.Theme = themeKey;
            ThemeService.ApplyTheme(themeKey, _mainWindow, _crossfadeImage, immediate);
            SettingsVM.SyncSelectedThemeCard(themeKey);
            SyncTrayTheme();
            SaveSettingsDebounced();
        }

        public void NavigateTo(string viewKey)
        {
            if (string.IsNullOrEmpty(viewKey)) return;
            ActiveViewKey = viewKey;
        }

        public void TriggerNewTaskShortcut()
        {
            NavigateTo("tasks");
            var h = FocusQuickAddRequested;
            if (h != null) h(this, EventArgs.Empty);
        }

        public void TriggerSearchTaskShortcut()
        {
            NavigateTo("tasks");
            var h = FocusTaskSearchRequested;
            if (h != null) h(this, EventArgs.Empty);
        }

        public void ToggleTimer()
        {
            Engine.Toggle();
            PersistTimerStateNow();
            AudioVM.SyncMixerGateAndStatus();
        }

        public void SkipTimer()
        {
            Engine.Skip();
            ThemeService.AnimatePhaseBrush(Engine.Snapshot.Phase, false);
            PersistTimerStateNow();
            AudioVM.SyncMixerGateAndStatus();
        }

        public void AddFiveMinutes()
        {
            if (Engine.AddFiveMinutes())
            {
                PersistTimerStateNow();
            }
        }

        public void RequestResetTimer()
        {
            TimerSnapshot snap = Engine.Snapshot;
            if (snap.Mode == TimerMode.Stopwatch && snap.State != TimerRunState.Idle && snap.ElapsedEffectiveSeconds >= PomodoroEngine.MinRecordableFocusSeconds)
            {
                OpenConfirmModal(
                    "放弃本次计时？",
                    string.Format("已计时 {0}，放弃后不会记录。", Formatters.FormatDurationChinese(snap.ElapsedEffectiveSeconds)),
                    "取消",
                    "放弃",
                    true,
                    delegate
                    {
                        Engine.Reset();
                        PersistTimerStateNow();
                        AudioVM.SyncMixerGateAndStatus();
                    });
                return;
            }

            Engine.Reset();
            PersistTimerStateNow();
            AudioVM.SyncMixerGateAndStatus();
        }

        public void RequestModeAndPhaseSwitch(TimerMode targetMode, TimerPhase targetPhase)
        {
            TimerSnapshot cur = Engine.Snapshot;
            if (cur.Mode == targetMode && cur.Phase == targetPhase)
            {
                FocusVM.UpdateFromSnapshot(cur);
                return;
            }

            if (Engine.NeedsInterruptConfirmation)
            {
                string dur = Formatters.FormatDurationChinese(Engine.CurrentEffectiveSeconds);
                OpenConfirmModal(
                    "要中断当前专注吗？",
                    string.Format("本次已专注 {0}，切换后将记为中断。", dur),
                    "继续专注",
                    "中断并切换",
                    true,
                    delegate
                    {
                        Engine.SelectModeAndPhase(targetMode, targetPhase, true);
                        ThemeService.AnimatePhaseBrush(Engine.Snapshot.Phase, false);
                        PersistTimerStateNow();
                        AudioVM.SyncMixerGateAndStatus();
                    });
                FocusVM.UpdateFromSnapshot(cur);
                return;
            }

            Engine.SelectModeAndPhase(targetMode, targetPhase, true);
            ThemeService.AnimatePhaseBrush(Engine.Snapshot.Phase, false);
            PersistTimerStateNow();
            AudioVM.SyncMixerGateAndStatus();
        }

        public void SetActiveTask(string taskId)
        {
            Settings.ActiveTaskId = taskId;
            SaveSettingsDebounced();
            RefreshAllDataViews();
        }

        public TaskItem AddTaskQuick(string input)
        {
            TaskItem item = TaskService.AddFromQuickInput(input, DateTime.UtcNow);
            if (item == null) return null;
            Storage.SaveTasks(TaskService.GetAllSnapshot(), false);
            RefreshAllDataViews();
            return item;
        }

        public void ToggleTaskComplete(string id)
        {
            TaskItem toggled = TaskService.ToggleComplete(id, DateTime.UtcNow);
            if (toggled == null) return;
            if (toggled.IsCompleted && Settings.ActiveTaskId == toggled.Id)
            {
                Settings.ActiveTaskId = null;
                SaveSettingsDebounced();
                ShowSnackbar(string.Format("「{0}」已完成，已自动取消专注绑定", toggled.Title), null, null);
            }
            Storage.SaveTasks(TaskService.GetAllSnapshot(), false);
            RefreshAllDataViews();
        }

        public void StartFocusOnTask(string id)
        {
            TaskItem task = TaskService.FindById(id);
            if (task == null || task.IsCompleted) return;
            Settings.ActiveTaskId = task.Id;
            SaveSettingsDebounced();
            NavigateTo("focus");
            if (Engine.Snapshot.State == TimerRunState.Idle && Engine.Snapshot.Phase == TimerPhase.Focus)
            {
                Engine.Start();
                PersistTimerStateNow();
                AudioVM.SyncMixerGateAndStatus();
            }
            RefreshAllDataViews();
        }

        public void DeleteTask(string id)
        {
            DeletedTaskRecord rec = TaskService.DeleteTask(id, Settings.ActiveTaskId);
            if (rec == null) return;
            if (rec.WasActiveTask)
            {
                Settings.ActiveTaskId = null;
                SaveSettingsDebounced();
            }
            Storage.SaveTasks(TaskService.GetAllSnapshot(), false);
            RefreshAllDataViews();

            ShowSnackbar(
                string.Format("已删除「{0}」", rec.Task.Title),
                "撤销",
                delegate
                {
                    DeletedTaskRecord restored = TaskService.UndoLastDelete();
                    if (restored != null)
                    {
                        if (restored.WasActiveTask)
                        {
                            Settings.ActiveTaskId = restored.Task.Id;
                            SaveSettingsDebounced();
                        }
                        Storage.SaveTasks(TaskService.GetAllSnapshot(), false);
                        RefreshAllDataViews();
                    }
                });
        }

        public void DeleteSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return;
            int idx = -1;
            for (int i = 0; i < _sessions.Count; i++)
            {
                if (_sessions[i].Id == sessionId) { idx = i; break; }
            }
            if (idx < 0) return;

            _lastDeletedSession = _sessions[idx];
            _lastDeletedSessionIndex = idx;
            _sessions.RemoveAt(idx);
            Storage.SaveSessions(_sessions, false);
            RefreshAllDataViews();

            ShowSnackbar(
                "已删除一条专注记录",
                "撤销",
                delegate
                {
                    if (_lastDeletedSession != null)
                    {
                        int pos = Math.Max(0, Math.Min(_sessions.Count, _lastDeletedSessionIndex));
                        _sessions.Insert(pos, _lastDeletedSession);
                        _lastDeletedSession = null;
                        Storage.SaveSessions(_sessions, false);
                        RefreshAllDataViews();
                    }
                });
        }

        public void OpenNewTaskDialog()
        {
            CloseAllModals();
            _editingTaskId = null;
            TaskModalDialogTitle = "新建任务";
            TaskDraftTitle = string.Empty;
            TaskDraftNotes = string.Empty;
            TaskDraftPriorityKey = "0";
            TaskDraftEstimate = 1;
            TaskDraftTagInput = string.Empty;
            TaskDraftError = string.Empty;
            TaskDraftTags.Clear();
            IsTaskModalOpen = true;
        }

        public void OpenEditTaskDialog(string id)
        {
            TaskItem task = TaskService.FindById(id);
            if (task == null) return;
            CloseAllModals();
            _editingTaskId = task.Id;
            TaskModalDialogTitle = "编辑任务";
            TaskDraftTitle = task.Title;
            TaskDraftNotes = task.Notes ?? string.Empty;
            TaskDraftPriorityKey = task.Priority.ToString(CultureInfo.InvariantCulture);
            TaskDraftEstimate = task.EstimatedPomodoros;
            TaskDraftTagInput = string.Empty;
            TaskDraftError = string.Empty;
            TaskDraftTags.Clear();
            if (task.Tags != null)
            {
                for (int i = 0; i < task.Tags.Count; i++) TaskDraftTags.Add(task.Tags[i]);
            }
            IsTaskModalOpen = true;
        }

        public void AddDraftTag()
        {
            if (string.IsNullOrWhiteSpace(TaskDraftTagInput)) return;
            if (TaskDraftTags.Count >= 5) return;
            string t = TaskDraftTagInput.Trim();
            if (t.StartsWith("#")) t = t.Substring(1).Trim();
            if (t.Length == 0) return;
            if (t.Length > 12) t = t.Substring(0, 12);

            for (int i = 0; i < TaskDraftTags.Count; i++)
            {
                if (string.Equals(TaskDraftTags[i], t, StringComparison.OrdinalIgnoreCase))
                {
                    TaskDraftTagInput = string.Empty;
                    return;
                }
            }
            TaskDraftTags.Add(t);
            TaskDraftTagInput = string.Empty;
        }

        private void RemoveDraftTag(string tag)
        {
            if (tag != null) TaskDraftTags.Remove(tag);
        }

        private void SaveTaskModal()
        {
            if (string.IsNullOrWhiteSpace(TaskDraftTitle))
            {
                TaskDraftError = "请输入任务标题";
                return;
            }
            AddDraftTag();

            int prio;
            if (!int.TryParse(TaskDraftPriorityKey, out prio)) prio = 0;

            var item = new TaskItem
            {
                Id = _editingTaskId ?? Guid.NewGuid().ToString("N"),
                Title = TaskDraftTitle.Trim(),
                Notes = TaskDraftNotes ?? string.Empty,
                Priority = prio,
                EstimatedPomodoros = TaskDraftEstimate,
                Tags = new List<string>(TaskDraftTags)
            };

            if (_editingTaskId == null)
            {
                TaskService.AddTask(item);
            }
            else
            {
                TaskService.UpdateTask(item);
            }

            Storage.SaveTasks(TaskService.GetAllSnapshot(), false);
            IsTaskModalOpen = false;
            RefreshAllDataViews();
        }

        public void OpenConfirmModal(string title, string message, string cancelText, string okText, bool isDanger, Action onOk)
        {
            CloseAllModals();
            ConfirmTitle = title;
            ConfirmMessage = message;
            ConfirmCancelText = cancelText;
            ConfirmOkText = okText;
            ConfirmIsDanger = isDanger;
            _confirmOkAction = onOk;
            IsConfirmModalOpen = true;
        }

        private void ExecuteConfirmOk()
        {
            Action act = _confirmOkAction;
            _confirmOkAction = null;
            IsConfirmModalOpen = false;
            if (act != null) act();
        }

        public void OpenClearDataModal()
        {
            CloseAllModals();
            ClearDataInput = string.Empty;
            IsClearDataModalOpen = true;
        }

        private void ExecuteClearAllData()
        {
            if (!CanConfirmClearData) return;
            IsClearDataModalOpen = false;

            TaskService.ReplaceAll(null);
            _sessions.Clear();
            Settings.ActiveTaskId = null;
            Storage.SaveTasks(TaskService.GetAllSnapshot(), true);
            Storage.SaveSessions(_sessions, true);
            Storage.SaveSettings(Settings, true);
            RefreshAllDataViews();
            ShowSnackbar("已清空所有任务与专注记录", null, null);
        }

        public void RequestRestoreDefaultSettings()
        {
            OpenConfirmModal(
                "恢复默认设置？",
                "所有计时、提醒、外观与快捷键设置将恢复为出厂默认值，任务与历史记录不受影响。",
                "取消",
                "恢复默认",
                false,
                delegate
                {
                    string keepActiveTask = Settings.ActiveTaskId;
                    Settings.ResetToDefaults();
                    Settings.ActiveTaskId = keepActiveTask;
                    AnimationHelper.ReduceMotionSetting = Settings.ReduceMotion;
                    Engine.UpdateSettings(Settings);
                    SwitchTheme(Settings.Theme, false);
                    AudioVM.LoadFromSettings(Settings);
                    SettingsVM.LoadFromSettings(Settings);
                    RegisterConfiguredHotkeys();
                    SettingsVM.RefreshHotkeyStatuses();
                    SaveSettingsDebounced();
                    RefreshAllDataViews();
                    ShowSnackbar("已恢复默认设置", null, null);
                });
        }

        public void CloseAllModals()
        {
            IsTaskModalOpen = false;
            IsConfirmModalOpen = false;
            IsClearDataModalOpen = false;
            _confirmOkAction = null;
        }

        public void ShowSnackbar(string message, string actionText, Action action)
        {
            _snackbarTimer.Stop();
            SnackbarMessage = message;
            SnackbarActionText = actionText ?? string.Empty;
            RaisePropertyChanged("HasSnackbarAction");
            _snackbarUndoAction = action;
            IsSnackbarVisible = true;
            _snackbarTimer.Start();
        }

        private void ExecuteSnackbarAction()
        {
            _snackbarTimer.Stop();
            IsSnackbarVisible = false;
            Action act = _snackbarUndoAction;
            _snackbarUndoAction = null;
            if (act != null) act();
        }

        public void ToggleMiniWindow()
        {
            if (_miniWindow != null && _miniWindow.IsVisible)
            {
                CloseMiniWindow(true);
            }
            else
            {
                OpenMiniWindow(Settings.HideMainOnMini);
            }
        }

        public MiniTimerWindow OpenMiniWindow(bool hideMain)
        {
            if (_miniWindow == null)
            {
                _miniWindow = new MiniTimerWindow(this);
                _miniWindow.Closed += delegate
                {
                    _miniWindow = null;
                    IsMiniWindowOpen = false;
                    TrayService.UpdateTimerStatus(Engine.Snapshot, ThemeService.ToDrawingColor(ThemeService.CurrentPhaseStartColor), false);
                };
            }
            _miniWindow.ApplyOpacitySetting(Settings.MiniWindowOpacity);
            _miniWindow.Show();
            _miniWindow.Activate();
            IsMiniWindowOpen = true;

            if (hideMain && _mainWindow != null && _mainWindow.IsVisible)
            {
                _mainWindow.Hide();
            }
            TrayService.UpdateTimerStatus(Engine.Snapshot, ThemeService.ToDrawingColor(ThemeService.CurrentPhaseStartColor), true);
            return _miniWindow;
        }

        public void CloseMiniWindow(bool restoreMain)
        {
            if (_miniWindow != null)
            {
                _miniWindow.Close();
                _miniWindow = null;
            }
            IsMiniWindowOpen = false;
            if (restoreMain)
            {
                ShowAndActivateMainWindow();
            }
            TrayService.UpdateTimerStatus(Engine.Snapshot, ThemeService.ToDrawingColor(ThemeService.CurrentPhaseStartColor), false);
        }

        public void ApplyMiniWindowOpacity()
        {
            if (_miniWindow != null)
            {
                _miniWindow.ApplyOpacitySetting(Settings.MiniWindowOpacity);
            }
        }

        public void ShowAndActivateMainWindow()
        {
            if (_mainWindow == null) return;
            if (!_mainWindow.IsVisible) _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }

        public void ToggleMainWindowVisibility()
        {
            if (_miniWindow != null && _miniWindow.IsVisible)
            {
                CloseMiniWindow(true);
                return;
            }
            if (_mainWindow == null) return;
            if (_mainWindow.IsVisible && _mainWindow.WindowState != WindowState.Minimized)
            {
                _mainWindow.Hide();
            }
            else
            {
                ShowAndActivateMainWindow();
            }
        }

        public void UpdateTrayVisibility()
        {
            TrayService.IsVisible = Settings.ShowTrayIcon;
        }

        public void NotifyAmbientStateChanged()
        {
            TrayService.UpdateAmbientTracks(Settings.AmbientActiveTracks);
        }

        public void RefreshAllDataViews()
        {
            TaskItem activeTask = TaskService.FindById(Settings.ActiveTaskId);
            FocusVM.RefreshActiveTaskAndToday(activeTask, _sessions, Settings);
            TasksVM.RefreshAll();
            StatsVM.RefreshAll();

            SidebarActiveTaskCount = TaskService.ActiveCount;
            StatsKpiSummary kpi = StatsService.ComputeKpi(_sessions, Settings, DateTime.Now, TimeZoneInfo.Local);
            int goal = Settings.DailyGoal > 0 ? Settings.DailyGoal : 8;
            SidebarGoalCountText = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", kpi.TodayPomodoros, goal);
            SidebarGoalPercent = Math.Min(100.0, (kpi.TodayPomodoros * 100.0) / goal);
            SidebarTodayDurationText = "已专注 " + (kpi.TodayFocusSeconds > 0 ? Formatters.FormatDurationChinese(kpi.TodayFocusSeconds) : "0 分钟");
        }

        public void SaveSettingsDebounced()
        {
            Storage.SaveSettings(Settings, false);
        }

        public void PersistTimerStateNow()
        {
            Storage.SaveTimerState(Engine.ExportState(), false);
        }

        public void OpenDataFolder()
        {
            try
            {
                if (!Directory.Exists(Storage.DataDirectory))
                {
                    Directory.CreateDirectory(Storage.DataDirectory);
                }
                Process.Start("explorer.exe", Storage.DataDirectory);
            }
            catch (Exception ex)
            {
                ShowSnackbar("无法打开数据文件夹：" + ex.Message, null, null);
            }
        }

        public void ExportCsvInteractive()
        {
            var dlg = new SaveFileDialog
            {
                Title = "导出专注记录 CSV",
                Filter = "CSV 文件 (*.csv)|*.csv",
                FileName = string.Format(CultureInfo.InvariantCulture, "番茄专注记录_{0:yyyyMMdd}.csv", DateTime.Now)
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string csv = StatsService.ExportCsv(_sessions, TimeZoneInfo.Local);
                    File.WriteAllText(dlg.FileName, csv, new UTF8Encoding(true));
                    ShowSnackbar("已成功导出 CSV 记录", null, null);
                }
                catch (Exception ex)
                {
                    ShowSnackbar("导出 CSV 失败：" + ex.Message, null, null);
                }
            }
        }

        public void ExportBackupInteractive()
        {
            var dlg = new SaveFileDialog
            {
                Title = "导出全部数据备份",
                Filter = "JSON 备份文件 (*.json)|*.json",
                FileName = string.Format(CultureInfo.InvariantCulture, "番茄专注备份_{0:yyyyMMdd}.json", DateTime.Now)
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var bundle = new BackupBundle
                    {
                        Settings = Settings.Clone(),
                        Tasks = new List<TaskItem>(TaskService.GetAllSnapshot()),
                        Sessions = new List<FocusSession>(_sessions)
                    };
                    Storage.ExportBackupToFile(dlg.FileName, bundle);
                    ShowSnackbar("备份导出成功", null, null);
                }
                catch (Exception ex)
                {
                    ShowSnackbar("导出备份失败：" + ex.Message, null, null);
                }
            }
        }

        public void ImportBackupInteractive()
        {
            var dlg = new OpenFileDialog
            {
                Title = "导入数据备份",
                Filter = "JSON 备份文件 (*.json)|*.json"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                BackupBundle bundle = Storage.ImportBackupFromFile(dlg.FileName);
                OpenConfirmModal(
                    "确认导入此备份？",
                    string.Format("将导入 {0} 项任务和 {1} 条专注记录，并覆盖当前数据。", bundle.Tasks.Count, bundle.Sessions.Count),
                    "取消",
                    "确认覆盖导入",
                    true,
                    delegate
                    {
                        Settings = bundle.Settings;
                        Settings.Clamp();
                        TaskService.ReplaceAll(bundle.Tasks);
                        _sessions.Clear();
                        _sessions.AddRange(bundle.Sessions);

                        Storage.SaveSettings(Settings, true);
                        Storage.SaveTasks(TaskService.GetAllSnapshot(), true);
                        Storage.SaveSessions(_sessions, true);

                        Engine.UpdateSettings(Settings);
                        SwitchTheme(Settings.Theme, false);
                        AudioVM.LoadFromSettings(Settings);
                        SettingsVM.LoadFromSettings(Settings);
                        RefreshAllDataViews();
                        ShowSnackbar("备份导入完成", null, null);
                    });
            }
            catch (Exception ex)
            {
                ShowSnackbar("导入备份失败：" + ex.Message, null, null);
            }
        }

        private void OnEngineTick(object sender, TimerSnapshot snap)
        {
            FocusVM.UpdateFromSnapshot(snap);
            UpdateTitleBarPill();
            TrayService.UpdateTimerStatus(snap, ThemeService.ToDrawingColor(ThemeService.CurrentPhaseStartColor), IsMiniWindowOpen);
        }

        private void OnEngineOneMinuteLeft(object sender, TimerSnapshot snap)
        {
            Mixer.PlayNotification("tick", Settings.NotificationVolume);
            WaveOutPlayer.Wake();
        }

        private void OnEngineSessionInterrupted(object sender, FocusSession session)
        {
            if (session == null) return;
            _sessions.Insert(0, session);
            Storage.SaveSessions(_sessions, false);
            RefreshAllDataViews();
        }

        private void OnEnginePhaseCompleted(object sender, PhaseCompletedEventArgs e)
        {
            ThemeService.AnimatePhaseBrush(e.NextPhase, false);
            PersistTimerStateNow();
            AudioVM.SyncMixerGateAndStatus();

            if (e.Session != null)
            {
                RecordCompletedSession(e.Session, true);
            }

            // Play notification sound
            if (!string.IsNullOrEmpty(Settings.NotificationSound) && Settings.NotificationSound != "none")
            {
                Mixer.PlayNotification(Settings.NotificationSound, Settings.NotificationVolume);
                WaveOutPlayer.Wake();
            }

            // Trigger ring celebration animation
            var cel = CelebrationTriggered;
            if (cel != null) cel(this, EventArgs.Empty);

            // Build notifications
            if (e.CompletedPhase == TimerPhase.Focus)
            {
                int breakMin = e.NextPhase == TimerPhase.LongBreak ? Settings.LongBreakMinutes : Settings.ShortBreakMinutes;
                TaskItem task = TaskService.FindById(Settings.ActiveTaskId);
                string body = task != null
                    ? string.Format("已完成「{0}」的第 {1} 个番茄，休息 {2} 分钟吧。", task.Title, task.CompletedPomodoros, breakMin)
                    : string.Format("又完成了一个番茄，休息 {0} 分钟吧。", breakMin);

                ShowCompletionNotification(
                    "专注完成",
                    body,
                    e.AutoStartedNext ? null : "开始休息",
                    e.AutoStartedNext ? null : (Action)delegate { ToggleTimer(); },
                    "跳过休息",
                    delegate { SkipTimer(); });

                CheckDailyGoalAndTaskEstimateNotifications(task);
            }
            else
            {
                ShowCompletionNotification(
                    "休息结束",
                    "准备好开始下一个番茄了吗？",
                    e.AutoStartedNext ? null : "开始专注",
                    e.AutoStartedNext ? null : (Action)delegate { ToggleTimer(); },
                    "稍后",
                    null);
            }
        }

        private void RecordCompletedSession(FocusSession session, bool incrementBoundTask)
        {
            _sessions.Insert(0, session);
            if (incrementBoundTask && session.IsFocus && session.IsCompleted && !string.IsNullOrEmpty(session.TaskId))
            {
                int delta = session.IsStopwatch
                    ? Math.Max(1, session.EffectiveSeconds / (Math.Max(1, Settings.FocusMinutes) * 60))
                    : 1;
                TaskService.IncrementCompletedPomodoros(session.TaskId, delta);
                Storage.SaveTasks(TaskService.GetAllSnapshot(), false);
            }
            Storage.SaveSessions(_sessions, false);
            RefreshAllDataViews();
        }

        private void CheckDailyGoalAndTaskEstimateNotifications(TaskItem task)
        {
            string todayKey = StatsService.GetLogicalDate(DateTime.Now, Settings.DayStartHour).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            StatsKpiSummary kpi = StatsService.ComputeKpi(_sessions, Settings, DateTime.Now, TimeZoneInfo.Local);

            if (kpi.TodayPomodoros >= Settings.DailyGoal && _dailyGoalNotifiedDate != todayKey)
            {
                _dailyGoalNotifiedDate = todayKey;
                string body = string.Format("今天已完成 {0} 个番茄，做得很好。", kpi.TodayPomodoros);
                if (Settings.EnableDesktopNotification)
                {
                    TrayService.ShowBalloon("今日目标达成", body);
                }
            }

            if (task != null && task.EstimatedPomodoros > 0 && task.CompletedPomodoros == task.EstimatedPomodoros)
            {
                string taskId = task.Id;
                string title = task.Title;
                int count = task.CompletedPomodoros;
                string body = string.Format("「{0}」已完成 {1} 个番茄。", title, count);
                ShowCompletionNotification(
                    "已达到预估番茄数",
                    body,
                    "标记完成",
                    delegate { ToggleTaskComplete(taskId); },
                    "继续专注",
                    null);
            }
        }

        private void ShowCompletionNotification(
            string title,
            string body,
            string primaryText,
            Action primaryAction,
            string secondaryText,
            Action secondaryAction)
        {
            if (Settings.EnableDesktopNotification)
            {
                TrayService.ShowBalloon(title, body);
            }
            if (Settings.EnableInAppToast)
            {
                try
                {
                    if (_activeToast != null)
                    {
                        _activeToast.Close();
                        _activeToast = null;
                    }
                    _activeToast = new ToastWindow(title, body, primaryText, primaryAction, secondaryText, secondaryAction);
                    _activeToast.Closed += delegate { _activeToast = null; };
                    _activeToast.Show();
                }
                catch
                {
                    // Ignore toast creation failure in headless environment
                }
            }
        }

        private void UpdateTitleBarPill()
        {
            TimerSnapshot snap = Engine.Snapshot;
            bool show = (_activeViewKey != "focus") && (snap.State != TimerRunState.Idle);
            ShowTitleBarTimerPill = show;
            if (show)
            {
                string label = snap.State == TimerRunState.Paused
                    ? "已暂停"
                    : (snap.Mode == TimerMode.Stopwatch ? "正计时" : (snap.Phase == TimerPhase.ShortBreak ? "短休息" : (snap.Phase == TimerPhase.LongBreak ? "长休息" : "专注中")));
                TitleBarTimerPillText = string.Format("{0} · {1}", label, Formatters.FormatTimer(snap.DisplaySeconds));
            }
        }

        private void SyncTrayTheme()
        {
            var bg = ThemeService.ToDrawingColor(ThemeService.GetCurrentThemeColor("Surface.Solid.Color", System.Windows.Media.Color.FromRgb(22, 27, 38)));
            var border = ThemeService.ToDrawingColor(ThemeService.GetCurrentThemeColor("DwmBorder.Color", System.Windows.Media.Color.FromRgb(42, 48, 60)));
            var hover = ThemeService.IsDark ? System.Drawing.Color.FromArgb(38, 45, 62) : System.Drawing.Color.FromArgb(236, 240, 248);
            var text = ThemeService.ToDrawingColor(ThemeService.GetCurrentThemeColor("Text.Primary.Color", System.Windows.Media.Color.FromRgb(242, 244, 248)));
            var muted = ThemeService.ToDrawingColor(ThemeService.GetCurrentThemeColor("Text.Tertiary.Color", System.Windows.Media.Color.FromRgb(104, 112, 131)));
            TrayService.ApplyTheme(ThemeService.IsDark, bg, border, hover, text, muted);
        }

        private void WireTrayEvents()
        {
            TrayService.LeftClicked += delegate
            {
                if (_miniWindow != null && _miniWindow.IsVisible)
                {
                    _miniWindow.Activate();
                }
                else
                {
                    ToggleMainWindowVisibility();
                }
            };
            TrayService.ToggleRequested += delegate { ToggleTimer(); };
            TrayService.SkipRequested += delegate { SkipTimer(); };
            TrayService.ResetRequested += delegate { RequestResetTimer(); };
            TrayService.MiniToggleRequested += delegate { ToggleMiniWindow(); };
            TrayService.ShowMainRequested += delegate { ShowAndActivateMainWindow(); };
            TrayService.AmbientTrackToggleRequested += delegate(object s, string id) { AudioVM.ToggleTrackById(id); };
            TrayService.AmbientStopAllRequested += delegate { AudioVM.StopAll(); };
            TrayService.OpenSettingsRequested += delegate
            {
                ShowAndActivateMainWindow();
                NavigateTo("settings");
            };
            TrayService.ExitRequested += delegate
            {
                var h = ExitApplicationRequested;
                if (h != null) h(this, EventArgs.Empty);
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _tickTimer.Stop();
            _snackbarTimer.Stop();
            if (_activeToast != null) { _activeToast.Close(); _activeToast = null; }
            if (_miniWindow != null) { _miniWindow.Close(); _miniWindow = null; }
            Storage.SaveTimerState(Engine.ExportState(), true);
            Storage.SaveSettings(Settings, true);
            Storage.SaveTasks(TaskService.GetAllSnapshot(), true);
            Storage.SaveSessions(_sessions, true);
            Hotkeys.Dispose();
            TrayService.Dispose();
            WaveOutPlayer.Dispose();
            Storage.Dispose();
        }
    }
}
