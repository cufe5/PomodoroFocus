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
    public sealed class HistoryRowViewModel
    {
        public string Id { get; set; }
        public string TimeRange { get; set; }
        public string PhaseBadge { get; set; }
        public bool IsFocus { get; set; }
        public string TaskTitle { get; set; }
        public string DurationText { get; set; }
        public string OutcomeText { get; set; }
        public bool IsInterrupted { get; set; }
    }

    public sealed class HistoryDayGroupViewModel
    {
        public string HeaderText { get; set; }
        public List<HistoryRowViewModel> Items { get; set; }

        public HistoryDayGroupViewModel()
        {
            Items = new List<HistoryRowViewModel>();
        }
    }

    public sealed class StatsViewModel : ObservableObject
    {
        private readonly MainViewModel _main;
        private readonly List<SegmentOption> _trendRangeOptions;
        private readonly List<SegmentOption> _distributionModeOptions;
        private readonly List<SegmentOption> _historyFilterOptions;
        private readonly List<SegmentOption> _heatmapRangeOptions;

        private string _trendRangeKey = "7";
        private string _distributionModeKey = "tag";
        private string _historyFilterKey = "all";
        private string _heatmapRangeKey = "20";
        private int _heatmapWeeks = 20;
        private int _historyLimit = 50;

        private string _todayPomodorosBig = "0 个番茄";
        private string _todayDurationSub = "0 分钟";
        private string _weekHoursBig = "0.0";
        private string _weekDeltaSub = "较上周持平";
        private string _weekDeltaBadgeText = "\u2197 持平";
        private bool _isWeekDeltaPositive = true;
        private string _streakDaysBig = "0 天";
        private string _longestStreakSub = "最长 0 天";
        private string _totalHoursBig = "0.0";
        private string _completionRateSub = "完成率 0% · 0 个番茄";
        private string _completionRateBadgeText = "完成率 0%";
        private string _totalPomodorosSubText = "0 个番茄";

        private IList<TrendDayPoint> _trendPoints;
        private int _goalMinutes = 200;
        private double[] _hourlyMinutes = new double[24];
        private string _hourlyPeakSubtitle = "暂无高效时段";
        private IList<DistributionSlice> _distributionSlices;
        private IList<HeatmapCell> _heatmapCells;
        private string _heatmapTitle = "专注热力图（近 20 周）";

        private bool _hasAnyData;
        private bool _hasMoreHistory;

        public StatsViewModel(MainViewModel main)
        {
            _main = main;
            _trendRangeOptions = new List<SegmentOption>
            {
                new SegmentOption { Key = "7", Label = "近 7 天" },
                new SegmentOption { Key = "30", Label = "近 30 天" }
            };
            _distributionModeOptions = new List<SegmentOption>
            {
                new SegmentOption { Key = "tag", Label = "标签" },
                new SegmentOption { Key = "task", Label = "任务" }
            };
            _historyFilterOptions = new List<SegmentOption>
            {
                new SegmentOption { Key = "all", Label = "全部" },
                new SegmentOption { Key = "focus", Label = "专注" },
                new SegmentOption { Key = "break", Label = "休息" }
            };
            _heatmapRangeOptions = new List<SegmentOption>
            {
                new SegmentOption { Key = "20", Label = "近 20 周" },
                new SegmentOption { Key = "52", Label = "全年" }
            };

            HistoryGroups = new ObservableCollection<HistoryDayGroupViewModel>();
            ExportCsvCommand = new RelayCommand(delegate { _main.ExportCsvInteractive(); });
            LoadMoreHistoryCommand = new RelayCommand(delegate
            {
                _historyLimit += 50;
                RefreshHistory();
            });
            DeleteSessionCommand = new RelayCommand<string>(delegate(string id) { _main.DeleteSession(id); });
        }

        public MainViewModel Main { get { return _main; } }
        public IList<SegmentOption> TrendRangeOptions { get { return _trendRangeOptions; } }
        public IList<SegmentOption> DistributionModeOptions { get { return _distributionModeOptions; } }
        public IList<SegmentOption> HistoryFilterOptions { get { return _historyFilterOptions; } }
        public IList<SegmentOption> HeatmapRangeOptions { get { return _heatmapRangeOptions; } }
        public ObservableCollection<HistoryDayGroupViewModel> HistoryGroups { get; private set; }

        public ICommand ExportCsvCommand { get; private set; }
        public ICommand LoadMoreHistoryCommand { get; private set; }
        public ICommand DeleteSessionCommand { get; private set; }

        public string TrendRangeKey
        {
            get { return _trendRangeKey; }
            set
            {
                if (SetField(ref _trendRangeKey, value, "TrendRangeKey"))
                {
                    RefreshCharts();
                }
            }
        }

        public string DistributionModeKey
        {
            get { return _distributionModeKey; }
            set
            {
                if (SetField(ref _distributionModeKey, value, "DistributionModeKey"))
                {
                    RefreshDistribution();
                }
            }
        }

        public string HistoryFilterKey
        {
            get { return _historyFilterKey; }
            set
            {
                if (SetField(ref _historyFilterKey, value, "HistoryFilterKey"))
                {
                    _historyLimit = 50;
                    RefreshHistory();
                }
            }
        }

        public string HeatmapRangeKey
        {
            get { return _heatmapRangeKey; }
            set
            {
                if (SetField(ref _heatmapRangeKey, value, "HeatmapRangeKey"))
                {
                    int weeks = (value == "52") ? 52 : 20;
                    SetHeatmapWeeks(weeks);
                }
            }
        }

        public int HeatmapWeeksCount { get { return _heatmapWeeks; } }

        public string TodayPomodorosBig { get { return _todayPomodorosBig; } private set { SetField(ref _todayPomodorosBig, value, "TodayPomodorosBig"); } }
        public string TodayDurationSub { get { return _todayDurationSub; } private set { SetField(ref _todayDurationSub, value, "TodayDurationSub"); } }
        public string WeekHoursBig { get { return _weekHoursBig; } private set { SetField(ref _weekHoursBig, value, "WeekHoursBig"); } }
        public string WeekDeltaSub { get { return _weekDeltaSub; } private set { SetField(ref _weekDeltaSub, value, "WeekDeltaSub"); } }
        public string WeekDeltaBadgeText { get { return _weekDeltaBadgeText; } private set { SetField(ref _weekDeltaBadgeText, value, "WeekDeltaBadgeText"); } }
        public bool IsWeekDeltaPositive { get { return _isWeekDeltaPositive; } private set { SetField(ref _isWeekDeltaPositive, value, "IsWeekDeltaPositive"); } }
        public string StreakDaysBig { get { return _streakDaysBig; } private set { SetField(ref _streakDaysBig, value, "StreakDaysBig"); } }
        public string LongestStreakSub { get { return _longestStreakSub; } private set { SetField(ref _longestStreakSub, value, "LongestStreakSub"); } }
        public string TotalHoursBig { get { return _totalHoursBig; } private set { SetField(ref _totalHoursBig, value, "TotalHoursBig"); } }
        public string CompletionRateSub { get { return _completionRateSub; } private set { SetField(ref _completionRateSub, value, "CompletionRateSub"); } }
        public string CompletionRateBadgeText { get { return _completionRateBadgeText; } private set { SetField(ref _completionRateBadgeText, value, "CompletionRateBadgeText"); } }
        public string TotalPomodorosSubText { get { return _totalPomodorosSubText; } private set { SetField(ref _totalPomodorosSubText, value, "TotalPomodorosSubText"); } }

        public IList<TrendDayPoint> TrendPoints { get { return _trendPoints; } private set { SetField(ref _trendPoints, value, "TrendPoints"); } }
        public int GoalMinutes { get { return _goalMinutes; } private set { SetField(ref _goalMinutes, value, "GoalMinutes"); } }
        public double[] HourlyMinutes { get { return _hourlyMinutes; } private set { SetField(ref _hourlyMinutes, value, "HourlyMinutes"); } }
        public string HourlyPeakSubtitle { get { return _hourlyPeakSubtitle; } private set { SetField(ref _hourlyPeakSubtitle, value, "HourlyPeakSubtitle"); } }
        public IList<DistributionSlice> DistributionSlices { get { return _distributionSlices; } private set { SetField(ref _distributionSlices, value, "DistributionSlices"); } }
        public IList<HeatmapCell> HeatmapCells { get { return _heatmapCells; } private set { SetField(ref _heatmapCells, value, "HeatmapCells"); } }
        public string HeatmapTitle { get { return _heatmapTitle; } private set { SetField(ref _heatmapTitle, value, "HeatmapTitle"); } }

        public bool HasAnyData { get { return _hasAnyData; } private set { SetField(ref _hasAnyData, value, "HasAnyData"); } }
        public bool HasMoreHistory { get { return _hasMoreHistory; } private set { SetField(ref _hasMoreHistory, value, "HasMoreHistory"); } }

        public void SetHeatmapWeeks(int weeks)
        {
            if (weeks < 12) weeks = 12;
            if (weeks > 52) weeks = 52;
            if (weeks != _heatmapWeeks)
            {
                _heatmapWeeks = weeks;
                RaisePropertyChanged("HeatmapWeeksCount");
                UpdateHeatmapTitle();
                HeatmapCells = StatsService.ComputeHeatmap(_main.Sessions, _main.Settings, DateTime.Now, TimeZoneInfo.Local, _heatmapWeeks);
            }
        }

        private void UpdateHeatmapTitle()
        {
            if (_heatmapWeeks >= 52)
            {
                HeatmapTitle = "专注热力图（全年 52 周）";
            }
            else
            {
                HeatmapTitle = string.Format(CultureInfo.InvariantCulture, "专注热力图（近 {0} 周）", _heatmapWeeks);
            }
        }

        public void RefreshAll()
        {
            IList<FocusSession> sessions = _main.Sessions;
            AppSettings settings = _main.Settings;
            HasAnyData = sessions != null && sessions.Count > 0;

            StatsKpiSummary kpi = StatsService.ComputeKpi(sessions, settings, DateTime.Now, TimeZoneInfo.Local);
            TodayPomodorosBig = string.Format(CultureInfo.InvariantCulture, "{0} 个番茄", kpi.TodayPomodoros);
            TodayDurationSub = kpi.TodayFocusSeconds > 0 ? Formatters.FormatDurationChinese(kpi.TodayFocusSeconds) : "0 分钟";

            WeekHoursBig = Formatters.FormatHoursKpiValue(kpi.WeekFocusSeconds);
            WeekDeltaSub = Formatters.FormatPercentDelta(kpi.WeekFocusSeconds, kpi.PrevWeekSamePeriodSeconds);
            if (kpi.PrevWeekSamePeriodSeconds <= 0)
            {
                WeekDeltaBadgeText = kpi.WeekFocusSeconds > 0 ? "\u2197 +100%" : "\u2197 持平";
                IsWeekDeltaPositive = true;
            }
            else
            {
                int deltaPct = (int)Math.Round(((double)(kpi.WeekFocusSeconds - kpi.PrevWeekSamePeriodSeconds) / kpi.PrevWeekSamePeriodSeconds) * 100.0);
                if (deltaPct > 0)
                {
                    WeekDeltaBadgeText = string.Format(CultureInfo.InvariantCulture, "\u2197 +{0}%", deltaPct);
                    IsWeekDeltaPositive = true;
                }
                else if (deltaPct < 0)
                {
                    WeekDeltaBadgeText = string.Format(CultureInfo.InvariantCulture, "\u2198 {0}%", deltaPct);
                    IsWeekDeltaPositive = false;
                }
                else
                {
                    WeekDeltaBadgeText = "\u2197 持平";
                    IsWeekDeltaPositive = true;
                }
            }

            StreakDaysBig = string.Format(CultureInfo.InvariantCulture, "{0} 天", kpi.CurrentStreakDays);
            LongestStreakSub = string.Format(CultureInfo.InvariantCulture, "最长 {0} 天", kpi.LongestStreakDays);

            TotalHoursBig = Formatters.FormatHoursKpiValue(kpi.TotalFocusSeconds);
            CompletionRateBadgeText = string.Format(CultureInfo.InvariantCulture, "完成率 {0}%", kpi.CompletionRatePercent);
            TotalPomodorosSubText = string.Format(CultureInfo.InvariantCulture, "{0} 个番茄", kpi.TotalCompletedFocusCount);
            CompletionRateSub = string.Format(
                CultureInfo.InvariantCulture,
                "完成率 {0}% · {1} 个番茄",
                kpi.CompletionRatePercent,
                kpi.TotalCompletedFocusCount);

            GoalMinutes = settings.DailyGoal * settings.FocusMinutes;
            RefreshCharts();
            RefreshDistribution();
            UpdateHeatmapTitle();
            HeatmapCells = StatsService.ComputeHeatmap(sessions, settings, DateTime.Now, TimeZoneInfo.Local, _heatmapWeeks);
            RefreshHistory();
        }

        private void RefreshCharts()
        {
            int days = (_trendRangeKey == "30") ? 30 : 7;
            TrendPoints = StatsService.ComputeTrend(_main.Sessions, _main.Settings, DateTime.Now, TimeZoneInfo.Local, days);
            HourlyDistributionResult hourly = StatsService.ComputeHourlyDistribution(
                _main.Sessions, DateTime.Now, _main.Settings.DayStartHour, TimeZoneInfo.Local, days);
            HourlyMinutes = hourly.HourMinutes;
            HourlyPeakSubtitle = hourly.PeakWindowText;
        }

        private void RefreshDistribution()
        {
            bool byTag = (_distributionModeKey == "tag");
            DistributionSlices = StatsService.ComputeDistribution(_main.Sessions, byTag);
        }

        private void RefreshHistory()
        {
            HistoryGroups.Clear();
            IList<FocusSession> all = _main.Sessions;
            if (all == null || all.Count == 0)
            {
                HasMoreHistory = false;
                return;
            }

            var filtered = new List<FocusSession>();
            for (int i = 0; i < all.Count; i++)
            {
                FocusSession s = all[i];
                if (s == null) continue;
                if (_historyFilterKey == "focus" && !s.IsFocus) continue;
                if (_historyFilterKey == "break" && s.IsFocus) continue;
                filtered.Add(s);
            }

            filtered.Sort(delegate(FocusSession a, FocusSession b)
            {
                return string.CompareOrdinal(b.StartedAtUtc ?? string.Empty, a.StartedAtUtc ?? string.Empty);
            });

            HasMoreHistory = filtered.Count > _historyLimit;
            int take = Math.Min(filtered.Count, _historyLimit);
            int curYear = DateTime.Now.Year;
            int focusUnit = Math.Max(1, _main.Settings.FocusMinutes) * 60;

            var groupMap = new Dictionary<string, List<FocusSession>>();
            var groupOrder = new List<string>();
            for (int i = 0; i < take; i++)
            {
                FocusSession s = filtered[i];
                string dk = s.LocalDate ?? "未知日期";
                List<FocusSession> bucket;
                if (!groupMap.TryGetValue(dk, out bucket))
                {
                    bucket = new List<FocusSession>();
                    groupMap[dk] = bucket;
                    groupOrder.Add(dk);
                }
                bucket.Add(s);
            }

            for (int g = 0; g < groupOrder.Count; g++)
            {
                string dk = groupOrder[g];
                List<FocusSession> list = groupMap[dk];

                int dayPoms = 0;
                int daySec = 0;
                var rows = new List<HistoryRowViewModel>();
                for (int i = 0; i < list.Count; i++)
                {
                    FocusSession s = list[i];
                    if (s.IsFocus && s.IsCompleted)
                    {
                        daySec += s.EffectiveSeconds;
                        dayPoms += s.IsStopwatch ? (s.EffectiveSeconds / focusUnit) : 1;
                    }

                    DateTime? st = PomodoroEngine.TryParseUtc(s.StartedAtUtc);
                    DateTime? en = PomodoroEngine.TryParseUtc(s.EndedAtUtc);
                    string range = (st.HasValue && en.HasValue)
                        ? Formatters.FormatTimeRange(
                            TimeZoneInfo.ConvertTimeFromUtc(st.Value, TimeZoneInfo.Local),
                            TimeZoneInfo.ConvertTimeFromUtc(en.Value, TimeZoneInfo.Local))
                        : "--:--";

                    string badge = s.Phase == "ShortBreak" ? "短休" : (s.Phase == "LongBreak" ? "长休" : (s.IsStopwatch ? "正计时" : "专注"));
                    string title = s.IsFocus
                        ? (string.IsNullOrWhiteSpace(s.TaskTitleSnapshot) ? "自由专注" : s.TaskTitleSnapshot)
                        : (s.Phase == "LongBreak" ? "长休息" : "短休息");

                    rows.Add(new HistoryRowViewModel
                    {
                        Id = s.Id,
                        TimeRange = range,
                        PhaseBadge = badge,
                        IsFocus = s.IsFocus,
                        TaskTitle = title,
                        DurationText = Formatters.FormatDurationChinese(s.EffectiveSeconds),
                        OutcomeText = s.IsCompleted ? "完成" : "中断",
                        IsInterrupted = !s.IsCompleted
                    });
                }

                DateTime parsedDate;
                string dateLabel = DateTime.TryParseExact(dk, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate)
                    ? Formatters.FormatDateWithWeekday(parsedDate, curYear)
                    : dk;

                string header = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} · {1} 个番茄 · {2}",
                    dateLabel,
                    dayPoms,
                    daySec > 0 ? Formatters.FormatDurationChinese(daySec) : "0 分钟");

                HistoryGroups.Add(new HistoryDayGroupViewModel
                {
                    HeaderText = header,
                    Items = rows
                });
            }
        }
    }
}
