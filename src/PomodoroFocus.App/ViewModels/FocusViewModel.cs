using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using PomodoroFocus.App.Controls;
using PomodoroFocus.Core.Engine;
using PomodoroFocus.Core.Models;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.App.ViewModels
{
    public sealed class CycleDotViewModel
    {
        public bool IsCompleted { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsUpcoming { get; set; }
    }

    public sealed class TodaySessionItemViewModel
    {
        public string TimeRange { get; set; }
        public string Title { get; set; }
        public string DurationText { get; set; }
        public bool IsInterrupted { get; set; }
    }

    public sealed class FocusViewModel : ObservableObject
    {
        private readonly MainViewModel _main;
        private readonly List<SegmentOption> _modeOptions;

        private string _selectedModeKey = "focus";
        private string _timerText = "25:00";
        private double _timerFontSize = 76.0;
        private string _phaseBadgeText = "准备专注";
        private string _subtitleText = "点击数字可调整本次时长";
        private double _progress;
        private bool _isRunning;
        private bool _isPaused;
        private bool _isIdle = true;
        private bool _isStopwatch;
        private bool _canAdjustCustomDuration = true;
        private bool _canReset;
        private string _primaryButtonIcon = "\uF5B0";
        private string _primaryButtonTooltip = "开始 (空格)";
        private string _skipButtonIcon = "\uE893";
        private string _skipButtonTooltip = "跳过 (S)";

        private int _customMinutesDraft = 25;
        private bool _isCustomDurationPopupOpen;

        private bool _isTaskPopupOpen;
        private string _taskPopupSearch = string.Empty;
        private string _activeTaskTitle = "选择要专注的任务";
        private string _activeTaskProgressText = string.Empty;
        private bool _hasActiveTask;
        private int _activeTaskPriority;

        private string _todayGoalCountText = "0 / 8 个番茄";
        private string _todayGoalSubText = "已专注 0 分钟 · 还差 8 个";
        private double _todayGoalPercent;
        private bool _isTodayGoalReached;
        private string _todayRecordsCountText = "0 项";
        private bool _hasTodayRecords;

        public FocusViewModel(MainViewModel main)
        {
            _main = main;
            _modeOptions = new List<SegmentOption>
            {
                new SegmentOption { Key = "focus", Label = "专注", AutomationId = "ModeSegment_Focus" },
                new SegmentOption { Key = "short", Label = "短休息", AutomationId = "ModeSegment_ShortBreak" },
                new SegmentOption { Key = "long", Label = "长休息", AutomationId = "ModeSegment_LongBreak" },
                new SegmentOption { Key = "stopwatch", Label = "正计时", AutomationId = "ModeSegment_Stopwatch" }
            };

            CycleDots = new ObservableCollection<CycleDotViewModel>();
            TodayRecords = new ObservableCollection<TodaySessionItemViewModel>();
            PopupTasks = new ObservableCollection<TaskItem>();

            ToggleCommand = new RelayCommand(delegate { _main.ToggleTimer(); });
            ResetCommand = new RelayCommand(delegate { _main.RequestResetTimer(); }, delegate { return CanReset; });
            SkipCommand = new RelayCommand(delegate { _main.SkipTimer(); });
            AddFiveCommand = new RelayCommand(delegate { _main.AddFiveMinutes(); });
            OpenCustomDurationCommand = new RelayCommand(OpenCustomDurationPopup);
            ApplyCustomDurationCommand = new RelayCommand(ApplyCustomDuration);
            SetPresetDurationCommand = new RelayCommand<string>(SetPresetDuration);
            OpenTaskSelectorCommand = new RelayCommand(OpenTaskPopup);
            SelectTaskCommand = new RelayCommand<string>(SelectTask);
            ClearActiveTaskCommand = new RelayCommand(delegate { _main.SetActiveTask(null); IsTaskPopupOpen = false; });
            CreateTaskFromPopupCommand = new RelayCommand(delegate
            {
                IsTaskPopupOpen = false;
                _main.OpenNewTaskDialog();
            });
            GoToAudioPageCommand = new RelayCommand(delegate { _main.NavigateTo("audio"); });
            ToggleQuickSoundCommand = new RelayCommand<string>(delegate(string id) { _main.AudioVM.ToggleTrackFromQuickBar(id); });
        }

        public IList<SegmentOption> ModeOptions { get { return _modeOptions; } }
        public ObservableCollection<CycleDotViewModel> CycleDots { get; private set; }
        public ObservableCollection<TodaySessionItemViewModel> TodayRecords { get; private set; }
        public ObservableCollection<TaskItem> PopupTasks { get; private set; }

        public ICommand ToggleCommand { get; private set; }
        public RelayCommand ResetCommand { get; private set; }
        public ICommand SkipCommand { get; private set; }
        public ICommand AddFiveCommand { get; private set; }
        public ICommand OpenCustomDurationCommand { get; private set; }
        public ICommand ApplyCustomDurationCommand { get; private set; }
        public ICommand SetPresetDurationCommand { get; private set; }
        public ICommand OpenTaskSelectorCommand { get; private set; }
        public ICommand SelectTaskCommand { get; private set; }
        public ICommand ClearActiveTaskCommand { get; private set; }
        public ICommand CreateTaskFromPopupCommand { get; private set; }
        public ICommand GoToAudioPageCommand { get; private set; }
        public ICommand ToggleQuickSoundCommand { get; private set; }

        public MainViewModel Main { get { return _main; } }

        public string SelectedModeKey
        {
            get { return _selectedModeKey; }
            set { SetField(ref _selectedModeKey, value, "SelectedModeKey"); }
        }

        public string TimerText
        {
            get { return _timerText; }
            private set { SetField(ref _timerText, value, "TimerText"); }
        }

        public double TimerFontSize
        {
            get { return _timerFontSize; }
            private set { SetField(ref _timerFontSize, value, "TimerFontSize"); }
        }

        public string PhaseBadgeText
        {
            get { return _phaseBadgeText; }
            private set { SetField(ref _phaseBadgeText, value, "PhaseBadgeText"); }
        }

        public string SubtitleText
        {
            get { return _subtitleText; }
            private set { SetField(ref _subtitleText, value, "SubtitleText"); }
        }

        public double Progress
        {
            get { return _progress; }
            private set { SetField(ref _progress, value, "Progress"); }
        }

        public bool IsRunning
        {
            get { return _isRunning; }
            private set { SetField(ref _isRunning, value, "IsRunning"); }
        }

        public bool IsPaused
        {
            get { return _isPaused; }
            private set { SetField(ref _isPaused, value, "IsPaused"); }
        }

        public bool IsIdle
        {
            get { return _isIdle; }
            private set { SetField(ref _isIdle, value, "IsIdle"); }
        }

        public bool IsStopwatch
        {
            get { return _isStopwatch; }
            private set { SetField(ref _isStopwatch, value, "IsStopwatch"); }
        }

        public bool CanAdjustCustomDuration
        {
            get { return _canAdjustCustomDuration; }
            private set { SetField(ref _canAdjustCustomDuration, value, "CanAdjustCustomDuration"); }
        }

        public bool CanReset
        {
            get { return _canReset; }
            private set
            {
                if (SetField(ref _canReset, value, "CanReset"))
                {
                    ResetCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string PrimaryButtonIcon
        {
            get { return _primaryButtonIcon; }
            private set { SetField(ref _primaryButtonIcon, value, "PrimaryButtonIcon"); }
        }

        public string PrimaryButtonTooltip
        {
            get { return _primaryButtonTooltip; }
            private set { SetField(ref _primaryButtonTooltip, value, "PrimaryButtonTooltip"); }
        }

        public string SkipButtonIcon
        {
            get { return _skipButtonIcon; }
            private set { SetField(ref _skipButtonIcon, value, "SkipButtonIcon"); }
        }

        public string SkipButtonTooltip
        {
            get { return _skipButtonTooltip; }
            private set { SetField(ref _skipButtonTooltip, value, "SkipButtonTooltip"); }
        }

        public int CustomMinutesDraft
        {
            get { return _customMinutesDraft; }
            set { SetField(ref _customMinutesDraft, value, "CustomMinutesDraft"); }
        }

        public bool IsCustomDurationPopupOpen
        {
            get { return _isCustomDurationPopupOpen; }
            set { SetField(ref _isCustomDurationPopupOpen, value, "IsCustomDurationPopupOpen"); }
        }

        public bool IsTaskPopupOpen
        {
            get { return _isTaskPopupOpen; }
            set
            {
                if (SetField(ref _isTaskPopupOpen, value, "IsTaskPopupOpen") && value)
                {
                    RefreshPopupTasks();
                }
            }
        }

        public string TaskPopupSearch
        {
            get { return _taskPopupSearch; }
            set
            {
                if (SetField(ref _taskPopupSearch, value, "TaskPopupSearch"))
                {
                    RefreshPopupTasks();
                }
            }
        }

        public string ActiveTaskTitle
        {
            get { return _activeTaskTitle; }
            private set { SetField(ref _activeTaskTitle, value, "ActiveTaskTitle"); }
        }

        public string ActiveTaskProgressText
        {
            get { return _activeTaskProgressText; }
            private set { SetField(ref _activeTaskProgressText, value, "ActiveTaskProgressText"); }
        }

        public bool HasActiveTask
        {
            get { return _hasActiveTask; }
            private set { SetField(ref _hasActiveTask, value, "HasActiveTask"); }
        }

        public int ActiveTaskPriority
        {
            get { return _activeTaskPriority; }
            private set { SetField(ref _activeTaskPriority, value, "ActiveTaskPriority"); }
        }

        public string TodayGoalCountText
        {
            get { return _todayGoalCountText; }
            private set { SetField(ref _todayGoalCountText, value, "TodayGoalCountText"); }
        }

        public string TodayGoalSubText
        {
            get { return _todayGoalSubText; }
            private set { SetField(ref _todayGoalSubText, value, "TodayGoalSubText"); }
        }

        public double TodayGoalPercent
        {
            get { return _todayGoalPercent; }
            private set { SetField(ref _todayGoalPercent, value, "TodayGoalPercent"); }
        }

        public bool IsTodayGoalReached
        {
            get { return _isTodayGoalReached; }
            private set { SetField(ref _isTodayGoalReached, value, "IsTodayGoalReached"); }
        }

        public string TodayRecordsCountText
        {
            get { return _todayRecordsCountText; }
            private set { SetField(ref _todayRecordsCountText, value, "TodayRecordsCountText"); }
        }

        public bool HasTodayRecords
        {
            get { return _hasTodayRecords; }
            private set { SetField(ref _hasTodayRecords, value, "HasTodayRecords"); }
        }

        public void HandleModeSegmentClicked(string key)
        {
            TimerMode targetMode = (key == "stopwatch") ? TimerMode.Stopwatch : TimerMode.Pomodoro;
            TimerPhase targetPhase = TimerPhase.Focus;
            if (key == "short") targetPhase = TimerPhase.ShortBreak;
            else if (key == "long") targetPhase = TimerPhase.LongBreak;

            _main.RequestModeAndPhaseSwitch(targetMode, targetPhase);
        }

        public void UpdateFromSnapshot(TimerSnapshot snap)
        {
            if (snap == null) return;

            int displaySec = snap.DisplaySeconds;
            TimerText = Formatters.FormatTimer(displaySec);
            TimerFontSize = displaySec >= 3600 ? 60.0 : 76.0;
            Progress = snap.Progress;
            IsRunning = snap.State == TimerRunState.Running;
            IsPaused = snap.State == TimerRunState.Paused;
            IsIdle = snap.State == TimerRunState.Idle;
            IsStopwatch = snap.Mode == TimerMode.Stopwatch;
            CanAdjustCustomDuration = IsIdle && !IsStopwatch && snap.Phase == TimerPhase.Focus;
            CanReset = !IsIdle || snap.HasCustomFocusDuration;

            if (snap.Mode == TimerMode.Stopwatch) SelectedModeKey = "stopwatch";
            else if (snap.Phase == TimerPhase.ShortBreak) SelectedModeKey = "short";
            else if (snap.Phase == TimerPhase.LongBreak) SelectedModeKey = "long";
            else SelectedModeKey = "focus";

            if (IsPaused)
            {
                PhaseBadgeText = "已暂停";
                PrimaryButtonIcon = "\uF5B0";
                PrimaryButtonTooltip = "继续 (空格)";
            }
            else if (IsRunning)
            {
                PhaseBadgeText = snap.Mode == TimerMode.Stopwatch
                    ? "正计时"
                    : (snap.Phase == TimerPhase.ShortBreak ? "短休息" : (snap.Phase == TimerPhase.LongBreak ? "长休息" : "专注中"));
                PrimaryButtonIcon = "\uF8AE";
                PrimaryButtonTooltip = "暂停 (空格)";
            }
            else
            {
                PhaseBadgeText = snap.Mode == TimerMode.Stopwatch
                    ? "准备计时"
                    : (snap.Phase == TimerPhase.ShortBreak ? "准备休息" : (snap.Phase == TimerPhase.LongBreak ? "准备长休息" : "准备专注"));
                PrimaryButtonIcon = "\uF5B0";
                PrimaryButtonTooltip = "开始 (空格)";
            }

            if (IsStopwatch)
            {
                SkipButtonIcon = "\uE73E";
                SkipButtonTooltip = "完成本次计时";
                SubtitleText = IsIdle
                    ? "点击开始向上计时（满 1 分钟计入记录）"
                    : "已专注 " + Formatters.FormatDurationChinese(snap.ElapsedEffectiveSeconds);
            }
            else
            {
                SkipButtonIcon = "\uE893";
                SkipButtonTooltip = "跳过 (S)";
                if (IsIdle)
                {
                    SubtitleText = snap.Phase == TimerPhase.Focus
                        ? "点击数字可调整本次时长"
                        : string.Format(CultureInfo.InvariantCulture, "放松 {0} 分钟，保持好状态", snap.PlannedSeconds / 60);
                }
                else
                {
                    int currentNth = Math.Min(snap.LongBreakInterval, snap.CompletedFocusInCycle + 1);
                    if (snap.EstimatedEndLocal.HasValue)
                    {
                        SubtitleText = string.Format(
                            CultureInfo.InvariantCulture,
                            "第 {0} 个番茄 · 预计 {1:HH:mm} 结束",
                            currentNth,
                            snap.EstimatedEndLocal.Value);
                    }
                    else
                    {
                        SubtitleText = string.Format(CultureInfo.InvariantCulture, "第 {0} 个番茄 · 已暂停", currentNth);
                    }
                }
            }

            // Rebuild cycle dots if needed
            int total = Math.Max(2, snap.LongBreakInterval);
            int done = Math.Min(total, snap.CompletedFocusInCycle);
            CycleDots.Clear();
            for (int i = 0; i < total; i++)
            {
                bool completed = i < done;
                bool current = !completed && i == done && snap.Phase == TimerPhase.Focus && snap.State != TimerRunState.Idle;
                CycleDots.Add(new CycleDotViewModel
                {
                    IsCompleted = completed,
                    IsCurrent = current,
                    IsUpcoming = !completed && !current
                });
            }
        }

        public void RefreshActiveTaskAndToday(TaskItem activeTask, IList<FocusSession> allSessions, AppSettings settings)
        {
            if (activeTask != null)
            {
                HasActiveTask = true;
                ActiveTaskTitle = activeTask.Title;
                ActiveTaskPriority = activeTask.Priority;
                if (activeTask.EstimatedPomodoros > 0)
                {
                    ActiveTaskProgressText = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", activeTask.CompletedPomodoros, activeTask.EstimatedPomodoros);
                }
                else if (activeTask.CompletedPomodoros > 0)
                {
                    ActiveTaskProgressText = string.Format(CultureInfo.InvariantCulture, "{0} 个", activeTask.CompletedPomodoros);
                }
                else
                {
                    ActiveTaskProgressText = string.Empty;
                }
            }
            else
            {
                HasActiveTask = false;
                ActiveTaskTitle = "选择要专注的任务";
                ActiveTaskPriority = 0;
                ActiveTaskProgressText = string.Empty;
            }

            StatsKpiSummary kpi = StatsService.ComputeKpi(allSessions, settings, DateTime.Now, TimeZoneInfo.Local);
            int goal = settings.DailyGoal > 0 ? settings.DailyGoal : 8;
            TodayGoalCountText = string.Format(CultureInfo.InvariantCulture, "{0} / {1} 个番茄", kpi.TodayPomodoros, goal);
            TodayGoalPercent = Math.Min(100.0, (kpi.TodayPomodoros * 100.0) / goal);
            IsTodayGoalReached = kpi.TodayPomodoros >= goal;

            string durText = kpi.TodayFocusSeconds > 0 ? Formatters.FormatDurationChinese(kpi.TodayFocusSeconds) : "0 分钟";
            if (IsTodayGoalReached)
            {
                TodayGoalSubText = "今日目标已达成 · 共 " + durText;
            }
            else
            {
                TodayGoalSubText = string.Format(
                    CultureInfo.InvariantCulture,
                    "已专注 {0} · 还差 {1} 个",
                    durText,
                    goal - kpi.TodayPomodoros);
            }

            string todayKey = StatsService.GetLogicalDate(DateTime.Now, settings.DayStartHour).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            TodayRecords.Clear();
            if (allSessions != null)
            {
                for (int i = 0; i < allSessions.Count; i++)
                {
                    FocusSession s = allSessions[i];
                    if (s == null || !s.IsFocus || s.LocalDate != todayKey) continue;

                    DateTime? st = PomodoroEngine.TryParseUtc(s.StartedAtUtc);
                    DateTime? en = PomodoroEngine.TryParseUtc(s.EndedAtUtc);
                    string range = (st.HasValue && en.HasValue)
                        ? Formatters.FormatTimeRange(
                            TimeZoneInfo.ConvertTimeFromUtc(st.Value, TimeZoneInfo.Local),
                            TimeZoneInfo.ConvertTimeFromUtc(en.Value, TimeZoneInfo.Local))
                        : "--:--";

                    TodayRecords.Add(new TodaySessionItemViewModel
                    {
                        TimeRange = range,
                        Title = string.IsNullOrWhiteSpace(s.TaskTitleSnapshot) ? "自由专注" : s.TaskTitleSnapshot,
                        DurationText = Formatters.FormatDurationChinese(s.EffectiveSeconds),
                        IsInterrupted = !s.IsCompleted
                    });
                }
            }
            TodayRecordsCountText = string.Format(CultureInfo.InvariantCulture, "{0} 项", TodayRecords.Count);
            HasTodayRecords = TodayRecords.Count > 0;
        }

        private void OpenCustomDurationPopup()
        {
            if (!CanAdjustCustomDuration) return;
            CustomMinutesDraft = Math.Max(1, _main.Engine.Snapshot.PlannedSeconds / 60);
            IsCustomDurationPopupOpen = true;
        }

        private void SetPresetDuration(string minutesStr)
        {
            int mins;
            if (int.TryParse(minutesStr, out mins))
            {
                CustomMinutesDraft = mins;
                ApplyCustomDuration();
            }
        }

        private void ApplyCustomDuration()
        {
            _main.Engine.SetCustomFocusMinutes(CustomMinutesDraft);
            IsCustomDurationPopupOpen = false;
        }

        private void OpenTaskPopup()
        {
            TaskPopupSearch = string.Empty;
            IsTaskPopupOpen = !IsTaskPopupOpen;
        }

        private void RefreshPopupTasks()
        {
            PopupTasks.Clear();
            List<TaskItem> list = _main.TaskService.Query(TaskFilterMode.Active, null, TaskPopupSearch, TaskSortMode.Smart);
            for (int i = 0; i < list.Count; i++)
            {
                PopupTasks.Add(list[i]);
            }
        }

        private void SelectTask(string taskId)
        {
            _main.SetActiveTask(taskId);
            IsTaskPopupOpen = false;
        }
    }
}
