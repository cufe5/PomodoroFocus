using System;
using System.Collections.Generic;
using System.Globalization;
using PomodoroFocus.Core.Engine;
using PomodoroFocus.Core.Models;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.Tests
{
    public sealed class StatsServiceTests
    {
        public void KpiTrendHourlySplitDistributionHeatmapAndCsv()
        {
            TimeZoneInfo tz = TimeZoneInfo.CreateCustomTimeZone("TestUTC+8", TimeSpan.FromHours(8), "UTC+8", "UTC+8");
            var settings = new AppSettings { FocusMinutes = 25, DailyGoal = 8, DayStartHour = 0 };
            // Thursday 2026-11-19 16:00 local
            DateTime localNow = new DateTime(2026, 11, 19, 16, 0, 0, DateTimeKind.Unspecified);

            var sessions = new List<FocusSession>();
            // Today: 2 completed pomodoro (one crossing 09:45 - 10:15 => 15m in hour 9, 15m in hour 10) + 1 interrupted
            sessions.Add(MakeSession(tz, new DateTime(2026, 11, 19, 9, 45, 0), 30 * 60, "Pomodoro", "Completed", "任务A", new string[] { "架构" }));
            sessions.Add(MakeSession(tz, new DateTime(2026, 11, 19, 14, 0, 0), 25 * 60, "Pomodoro", "Completed", "任务B", new string[] { "工作" }));
            sessions.Add(MakeSession(tz, new DateTime(2026, 11, 19, 15, 0, 0), 10 * 60, "Pomodoro", "Interrupted", "任务B", new string[] { "工作" }));

            // Yesterday (2026-11-18) & 2 days ago (2026-11-17) => 3-day streak
            sessions.Add(MakeSession(tz, new DateTime(2026, 11, 18, 10, 0, 0), 25 * 60, "Pomodoro", "Completed", "任务C", new string[] { "学习" }));
            sessions.Add(MakeSession(tz, new DateTime(2026, 11, 17, 10, 0, 0), 25 * 60, "Pomodoro", "Completed", "任务D", new string[] { "设计" }));

            // Additional distinct tags to trigger Top 5 + 其他
            sessions.Add(MakeSession(tz, new DateTime(2026, 11, 12, 10, 0, 0), 25 * 60, "Pomodoro", "Completed", "任务E", new string[] { "测试" }));
            sessions.Add(MakeSession(tz, new DateTime(2026, 11, 11, 10, 0, 0), 25 * 60, "Pomodoro", "Completed", "任务F", new string[] { "运维" }));
            sessions.Add(MakeSession(tz, new DateTime(2026, 11, 10, 10, 0, 0), 25 * 60, "Pomodoro", "Completed", "任务G", new string[] { "文档" }));

            StatsKpiSummary kpi = StatsService.ComputeKpi(sessions, settings, localNow, tz);
            AssertEx.Equal(2, kpi.TodayPomodoros, "Today pomodoros");
            AssertEx.Equal(55 * 60, kpi.TodayFocusSeconds, "Today focus seconds");
            AssertEx.Equal(3, kpi.CurrentStreakDays, "Current streak 3 days");
            AssertEx.Equal(7, kpi.TotalCompletedFocusCount, "7 completed");
            AssertEx.Equal(1, kpi.TotalInterruptedFocusCount, "1 interrupted");
            AssertEx.Equal(88, kpi.CompletionRatePercent, "7/8 = 87.5 -> 88%");

            // 7-day trend
            List<TrendDayPoint> trend7 = StatsService.ComputeTrend(sessions, settings, localNow, tz, 7);
            AssertEx.Equal(7, trend7.Count, "7 trend points");
            AssertEx.IsTrue(trend7[6].IsToday, "Last point is today");
            AssertEx.Equal(55, trend7[6].Minutes, "Today minutes 55");

            // Cross-hour proportional split verification
            HourlyDistributionResult hourly = StatsService.ComputeHourlyDistribution(sessions, localNow, 0, tz, 1);
            AssertEx.InRange(hourly.HourMinutes[9], 14.9, 15.1, "15 min in hour 9");
            AssertEx.InRange(hourly.HourMinutes[10], 14.9, 15.1, "15 min in hour 10");
            AssertEx.InRange(hourly.HourMinutes[14], 24.9, 25.1, "25 min in hour 14");

            // Distribution Top 5 + 其他
            List<DistributionSlice> dist = StatsService.ComputeDistribution(sessions, true);
            AssertEx.Equal(6, dist.Count, "Top 5 + 其他 = 6 slices");
            AssertEx.Equal("其他", dist[5].Name, "Last slice is 其他");

            // Heatmap levels
            AssertEx.Equal(0, StatsService.MapHeatmapLevel(0), "L0");
            AssertEx.Equal(1, StatsService.MapHeatmapLevel(2), "L1");
            AssertEx.Equal(2, StatsService.MapHeatmapLevel(4), "L2");
            AssertEx.Equal(3, StatsService.MapHeatmapLevel(6), "L3");
            AssertEx.Equal(4, StatsService.MapHeatmapLevel(8), "L4");

            List<HeatmapCell> hm = StatsService.ComputeHeatmap(sessions, settings, localNow, tz, 12);
            AssertEx.Equal(12 * 7, hm.Count, "12 weeks * 7 rows");

            // CSV export
            string csv = StatsService.ExportCsv(sessions, tz);
            AssertEx.IsTrue(csv.StartsWith("日期,开始时间,结束时间,类型,模式,结果,计划分钟,实际分钟,任务,标签"), "CSV header matches spec");
            AssertEx.IsTrue(csv.Contains("任务A"), "CSV contains task title");
        }

        private static FocusSession MakeSession(
            TimeZoneInfo tz,
            DateTime startLocal,
            int seconds,
            string mode,
            string outcome,
            string taskTitle,
            string[] tags)
        {
            DateTime endLocal = startLocal.AddSeconds(seconds);
            DateTime startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified), tz);
            DateTime endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endLocal, DateTimeKind.Unspecified), tz);
            return new FocusSession
            {
                Id = Guid.NewGuid().ToString("N"),
                Mode = mode,
                Phase = "Focus",
                Outcome = outcome,
                StartedAtUtc = PomodoroEngine.FormatUtc(startUtc),
                EndedAtUtc = PomodoroEngine.FormatUtc(endUtc),
                LocalDate = startLocal.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                PlannedSeconds = seconds,
                EffectiveSeconds = seconds,
                TaskTitleSnapshot = taskTitle,
                TagsSnapshot = new List<string>(tags)
            };
        }
    }
}
