using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PomodoroFocus.Core.Engine;
using PomodoroFocus.Core.Models;

namespace PomodoroFocus.Core.Services
{
    public sealed class StatsKpiSummary
    {
        public int TodayPomodoros { get; set; }
        public int TodayFocusSeconds { get; set; }
        public int WeekFocusSeconds { get; set; }
        public int PrevWeekSamePeriodSeconds { get; set; }
        public int CurrentStreakDays { get; set; }
        public int LongestStreakDays { get; set; }
        public int TotalFocusSeconds { get; set; }
        public int TotalCompletedFocusCount { get; set; }
        public int TotalInterruptedFocusCount { get; set; }
        public int CompletionRatePercent { get; set; }
    }

    public sealed class TrendDayPoint
    {
        public DateTime Date { get; set; }
        public string ShortLabel { get; set; }
        public string TooltipTitle { get; set; }
        public int Minutes { get; set; }
        public int Pomodoros { get; set; }
        public bool IsToday { get; set; }
    }

    public sealed class HourlyDistributionResult
    {
        public double[] HourMinutes { get; set; }
        public int PeakStartHour { get; set; }
        public int PeakSingleHour { get; set; }
        public string PeakWindowText { get; set; }

        public HourlyDistributionResult()
        {
            HourMinutes = new double[24];
            PeakWindowText = "暂无高效时段";
        }
    }

    public sealed class DistributionSlice
    {
        public string Name { get; set; }
        public int Seconds { get; set; }
        public double Percentage { get; set; }
    }

    public sealed class HeatmapCell
    {
        public DateTime Date { get; set; }
        public int WeekIndex { get; set; }
        public int DayOfWeekRow { get; set; } // 0=Mon .. 6=Sun
        public int Pomodoros { get; set; }
        public int Minutes { get; set; }
        public int Level { get; set; }        // 0..4
        public bool IsFuture { get; set; }
    }

    public static class StatsService
    {
        public static DateTime GetLogicalDate(DateTime localTime, int dayStartHour)
        {
            return localTime.AddHours(-dayStartHour).Date;
        }

        public static int ComputePomodoroCountForSessions(IEnumerable<FocusSession> sessions, int focusMinutes)
        {
            int unitSeconds = (focusMinutes > 0 ? focusMinutes : 25) * 60;
            int count = 0;
            if (sessions == null) return 0;

            foreach (var s in sessions)
            {
                if (s == null || !s.IsFocus || !s.IsCompleted) continue;
                if (s.IsStopwatch)
                {
                    count += s.EffectiveSeconds / unitSeconds;
                }
                else
                {
                    count += 1;
                }
            }
            return count;
        }

        public static StatsKpiSummary ComputeKpi(
            IList<FocusSession> sessions,
            AppSettings settings,
            DateTime localNow,
            TimeZoneInfo tz)
        {
            var summary = new StatsKpiSummary();
            if (sessions == null || settings == null) return summary;

            int dayStart = settings.DayStartHour;
            int focusUnit = (settings.FocusMinutes > 0 ? settings.FocusMinutes : 25) * 60;
            DateTime today = GetLogicalDate(localNow, dayStart);
            string todayKey = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            // Monday as start of week
            int diffFromMon = ((int)today.DayOfWeek + 6) % 7;
            DateTime weekStart = today.AddDays(-diffFromMon);
            DateTime prevWeekStart = weekStart.AddDays(-7);
            DateTime prevWeekSameEnd = prevWeekStart.AddDays(diffFromMon);

            var activeDays = new HashSet<DateTime>();

            for (int i = 0; i < sessions.Count; i++)
            {
                FocusSession s = sessions[i];
                if (s == null || !s.IsFocus) continue;

                if (s.IsCompleted)
                {
                    summary.TotalCompletedFocusCount++;
                    summary.TotalFocusSeconds += s.EffectiveSeconds;

                    DateTime d;
                    if (!TryGetSessionLogicalDate(s, dayStart, tz, out d)) continue;
                    activeDays.Add(d);

                    if (d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) == todayKey)
                    {
                        summary.TodayFocusSeconds += s.EffectiveSeconds;
                        summary.TodayPomodoros += s.IsStopwatch ? (s.EffectiveSeconds / focusUnit) : 1;
                    }

                    if (d >= weekStart && d <= today)
                    {
                        summary.WeekFocusSeconds += s.EffectiveSeconds;
                    }
                    else if (d >= prevWeekStart && d <= prevWeekSameEnd)
                    {
                        summary.PrevWeekSamePeriodSeconds += s.EffectiveSeconds;
                    }
                }
                else
                {
                    summary.TotalInterruptedFocusCount++;
                }
            }

            int denom = summary.TotalCompletedFocusCount + summary.TotalInterruptedFocusCount;
            summary.CompletionRatePercent = denom > 0
                ? (int)Math.Round((summary.TotalCompletedFocusCount * 100.0) / denom)
                : 0;

            // Current streak: ending today or yesterday
            int curStreak = 0;
            DateTime cursor = activeDays.Contains(today) ? today : today.AddDays(-1);
            while (activeDays.Contains(cursor))
            {
                curStreak++;
                cursor = cursor.AddDays(-1);
            }
            summary.CurrentStreakDays = curStreak;

            // Longest streak
            if (activeDays.Count > 0)
            {
                var sortedDays = new List<DateTime>(activeDays);
                sortedDays.Sort();
                int best = 1;
                int run = 1;
                for (int i = 1; i < sortedDays.Count; i++)
                {
                    if ((sortedDays[i] - sortedDays[i - 1]).Days == 1)
                    {
                        run++;
                        if (run > best) best = run;
                    }
                    else
                    {
                        run = 1;
                    }
                }
                summary.LongestStreakDays = best;
            }

            return summary;
        }

        public static List<TrendDayPoint> ComputeTrend(
            IList<FocusSession> sessions,
            AppSettings settings,
            DateTime localNow,
            TimeZoneInfo tz,
            int days)
        {
            if (days < 1) days = 7;
            int dayStart = settings != null ? settings.DayStartHour : 0;
            int focusUnit = (settings != null && settings.FocusMinutes > 0 ? settings.FocusMinutes : 25) * 60;
            DateTime today = GetLogicalDate(localNow, dayStart);
            DateTime start = today.AddDays(-(days - 1));

            var secByDate = new Dictionary<DateTime, int>();
            var pomByDate = new Dictionary<DateTime, int>();

            if (sessions != null)
            {
                for (int i = 0; i < sessions.Count; i++)
                {
                    FocusSession s = sessions[i];
                    if (s == null || !s.IsFocus || !s.IsCompleted) continue;
                    DateTime d;
                    if (!TryGetSessionLogicalDate(s, dayStart, tz, out d)) continue;
                    if (d < start || d > today) continue;

                    int curSec;
                    secByDate.TryGetValue(d, out curSec);
                    secByDate[d] = curSec + s.EffectiveSeconds;

                    int curPom;
                    pomByDate.TryGetValue(d, out curPom);
                    pomByDate[d] = curPom + (s.IsStopwatch ? (s.EffectiveSeconds / focusUnit) : 1);
                }
            }

            var list = new List<TrendDayPoint>(days);
            for (int i = 0; i < days; i++)
            {
                DateTime d = start.AddDays(i);
                int sec;
                secByDate.TryGetValue(d, out sec);
                int pom;
                pomByDate.TryGetValue(d, out pom);

                var pt = new TrendDayPoint();
                pt.Date = d;
                pt.IsToday = (d == today);
                pt.Minutes = (int)Math.Round(sec / 60.0);
                pt.Pomodoros = pom;
                if (days <= 7)
                {
                    string wk = Formatters.FormatWeekdayShort(d.DayOfWeek);
                    pt.ShortLabel = wk.Length >= 2 ? wk.Substring(1) : wk;
                }
                else
                {
                    pt.ShortLabel = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", d.Month, d.Day);
                }
                pt.TooltipTitle = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} · {1} 个番茄 · {2} 分钟",
                    Formatters.FormatDateWithWeekday(d, today.Year),
                    pom,
                    pt.Minutes);
                list.Add(pt);
            }
            return list;
        }

        public static HourlyDistributionResult ComputeHourlyDistribution(
            IList<FocusSession> sessions,
            DateTime localNow,
            int dayStartHour,
            TimeZoneInfo tz,
            int days)
        {
            var result = new HourlyDistributionResult();
            if (sessions == null || sessions.Count == 0) return result;
            if (tz == null) tz = TimeZoneInfo.Local;

            DateTime today = GetLogicalDate(localNow, dayStartHour);
            DateTime startDay = today.AddDays(-(days - 1));

            for (int i = 0; i < sessions.Count; i++)
            {
                FocusSession s = sessions[i];
                if (s == null || !s.IsFocus || !s.IsCompleted || s.EffectiveSeconds <= 0) continue;

                DateTime logicalD;
                if (!TryGetSessionLogicalDate(s, dayStartHour, tz, out logicalD)) continue;
                if (logicalD < startDay || logicalD > today) continue;

                DateTime? startUtc = PomodoroEngine.TryParseUtc(s.StartedAtUtc);
                DateTime? endUtc = PomodoroEngine.TryParseUtc(s.EndedAtUtc);
                if (!startUtc.HasValue) continue;

                DateTime startLocal = TimeZoneInfo.ConvertTimeFromUtc(startUtc.Value, tz);
                DateTime endLocal = endUtc.HasValue
                    ? TimeZoneInfo.ConvertTimeFromUtc(endUtc.Value, tz)
                    : startLocal.AddSeconds(s.EffectiveSeconds);

                double spanSeconds = (endLocal - startLocal).TotalSeconds;
                if (spanSeconds <= 0)
                {
                    result.HourMinutes[startLocal.Hour] += s.EffectiveSeconds / 60.0;
                    continue;
                }

                // Scale factor in case session had pauses so total distributed equals EffectiveSeconds
                double ratio = s.EffectiveSeconds / spanSeconds;
                if (ratio > 1.0) ratio = 1.0;

                DateTime cursor = startLocal;
                while (cursor < endLocal)
                {
                    DateTime nextHour = new DateTime(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, 0, 0).AddHours(1);
                    DateTime segEnd = endLocal < nextHour ? endLocal : nextHour;
                    double segSec = (segEnd - cursor).TotalSeconds;
                    if (segSec > 0)
                    {
                        result.HourMinutes[cursor.Hour] += (segSec * ratio) / 60.0;
                    }
                    cursor = segEnd;
                }
            }

            double bestWindow = 0;
            int bestStart = 9;
            double bestSingle = 0;
            int bestHour = 0;

            for (int h = 0; h < 24; h++)
            {
                if (result.HourMinutes[h] > bestSingle)
                {
                    bestSingle = result.HourMinutes[h];
                    bestHour = h;
                }
                double win = result.HourMinutes[h] + result.HourMinutes[(h + 1) % 24];
                if (win > bestWindow)
                {
                    bestWindow = win;
                    bestStart = h;
                }
            }

            result.PeakSingleHour = bestHour;
            result.PeakStartHour = bestStart;
            if (bestWindow > 0.01)
            {
                int endH = (bestStart + 2) % 24;
                result.PeakWindowText = string.Format(
                    CultureInfo.InvariantCulture,
                    "高效时段 {0:D2}:00 \u2013 {1:D2}:00",
                    bestStart,
                    endH);
            }
            else
            {
                result.PeakWindowText = "暂无高效时段";
            }

            return result;
        }

        public static List<DistributionSlice> ComputeDistribution(IList<FocusSession> sessions, bool byTag)
        {
            var totals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            double grandTotal = 0;

            if (sessions != null)
            {
                for (int i = 0; i < sessions.Count; i++)
                {
                    FocusSession s = sessions[i];
                    if (s == null || !s.IsFocus || !s.IsCompleted || s.EffectiveSeconds <= 0) continue;
                    grandTotal += s.EffectiveSeconds;

                    if (byTag)
                    {
                        if (s.TagsSnapshot == null || s.TagsSnapshot.Count == 0)
                        {
                            AddToDict(totals, "未分类", s.EffectiveSeconds);
                        }
                        else
                        {
                            double share = (double)s.EffectiveSeconds / s.TagsSnapshot.Count;
                            for (int ti = 0; ti < s.TagsSnapshot.Count; ti++)
                            {
                                string tag = s.TagsSnapshot[ti];
                                if (string.IsNullOrWhiteSpace(tag)) tag = "未分类";
                                else tag = "#" + tag;
                                AddToDict(totals, tag, share);
                            }
                        }
                    }
                    else
                    {
                        string taskName = string.IsNullOrWhiteSpace(s.TaskTitleSnapshot)
                            ? "未关联任务"
                            : s.TaskTitleSnapshot;
                        AddToDict(totals, taskName, s.EffectiveSeconds);
                    }
                }
            }

            var result = new List<DistributionSlice>();
            if (grandTotal <= 0) return result;

            var entries = new List<KeyValuePair<string, double>>(totals);
            entries.Sort(delegate(KeyValuePair<string, double> a, KeyValuePair<string, double> b)
            {
                return b.Value.CompareTo(a.Value);
            });

            if (entries.Count <= 5)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    result.Add(new DistributionSlice
                    {
                        Name = entries[i].Key,
                        Seconds = (int)Math.Round(entries[i].Value),
                        Percentage = (entries[i].Value * 100.0) / grandTotal
                    });
                }
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    result.Add(new DistributionSlice
                    {
                        Name = entries[i].Key,
                        Seconds = (int)Math.Round(entries[i].Value),
                        Percentage = (entries[i].Value * 100.0) / grandTotal
                    });
                }
                double otherSec = 0;
                for (int i = 5; i < entries.Count; i++)
                {
                    otherSec += entries[i].Value;
                }
                result.Add(new DistributionSlice
                {
                    Name = "其他",
                    Seconds = (int)Math.Round(otherSec),
                    Percentage = (otherSec * 100.0) / grandTotal
                });
            }

            return result;
        }

        public static List<HeatmapCell> ComputeHeatmap(
            IList<FocusSession> sessions,
            AppSettings settings,
            DateTime localNow,
            TimeZoneInfo tz,
            int weeks)
        {
            if (weeks < 12) weeks = 12;
            if (weeks > 53) weeks = 53;

            int dayStart = settings != null ? settings.DayStartHour : 0;
            int focusUnit = (settings != null && settings.FocusMinutes > 0 ? settings.FocusMinutes : 25) * 60;
            DateTime today = GetLogicalDate(localNow, dayStart);

            // End of current week (Sunday)
            int todayMonOffset = ((int)today.DayOfWeek + 6) % 7;
            DateTime currentWeekMonday = today.AddDays(-todayMonOffset);
            DateTime startMonday = currentWeekMonday.AddDays(-7 * (weeks - 1));

            var pomMap = new Dictionary<DateTime, int>();
            var secMap = new Dictionary<DateTime, int>();

            if (sessions != null)
            {
                for (int i = 0; i < sessions.Count; i++)
                {
                    FocusSession s = sessions[i];
                    if (s == null || !s.IsFocus || !s.IsCompleted) continue;
                    DateTime d;
                    if (!TryGetSessionLogicalDate(s, dayStart, tz, out d)) continue;
                    if (d < startMonday || d > today) continue;

                    int curP;
                    pomMap.TryGetValue(d, out curP);
                    pomMap[d] = curP + (s.IsStopwatch ? (s.EffectiveSeconds / focusUnit) : 1);

                    int curS;
                    secMap.TryGetValue(d, out curS);
                    secMap[d] = curS + s.EffectiveSeconds;
                }
            }

            var cells = new List<HeatmapCell>(weeks * 7);
            for (int w = 0; w < weeks; w++)
            {
                for (int r = 0; r < 7; r++)
                {
                    DateTime d = startMonday.AddDays(w * 7 + r);
                    int p;
                    pomMap.TryGetValue(d, out p);
                    int sec;
                    secMap.TryGetValue(d, out sec);

                    cells.Add(new HeatmapCell
                    {
                        Date = d,
                        WeekIndex = w,
                        DayOfWeekRow = r,
                        Pomodoros = p,
                        Minutes = (int)Math.Round(sec / 60.0),
                        Level = MapHeatmapLevel(p),
                        IsFuture = d > today
                    });
                }
            }

            return cells;
        }

        public static int MapHeatmapLevel(int pomodoros)
        {
            if (pomodoros <= 0) return 0;
            if (pomodoros <= 2) return 1;
            if (pomodoros <= 4) return 2;
            if (pomodoros <= 7) return 3;
            return 4;
        }

        public static string ExportCsv(IList<FocusSession> sessions, TimeZoneInfo tz)
        {
            if (tz == null) tz = TimeZoneInfo.Local;
            var sb = new StringBuilder();
            sb.AppendLine("日期,开始时间,结束时间,类型,模式,结果,计划分钟,实际分钟,任务,标签");

            if (sessions != null)
            {
                for (int i = 0; i < sessions.Count; i++)
                {
                    FocusSession s = sessions[i];
                    if (s == null) continue;

                    DateTime? startUtc = PomodoroEngine.TryParseUtc(s.StartedAtUtc);
                    DateTime? endUtc = PomodoroEngine.TryParseUtc(s.EndedAtUtc);
                    string startText = startUtc.HasValue
                        ? TimeZoneInfo.ConvertTimeFromUtc(startUtc.Value, tz).ToString("HH:mm", CultureInfo.InvariantCulture)
                        : string.Empty;
                    string endText = endUtc.HasValue
                        ? TimeZoneInfo.ConvertTimeFromUtc(endUtc.Value, tz).ToString("HH:mm", CultureInfo.InvariantCulture)
                        : string.Empty;

                    string phaseCn = s.Phase == "ShortBreak" ? "短休息" : (s.Phase == "LongBreak" ? "长休息" : "专注");
                    string modeCn = s.IsStopwatch ? "正计时" : "番茄";
                    string outcomeCn = s.IsCompleted ? "完成" : "中断";
                    string plannedMin = (s.PlannedSeconds / 60.0).ToString("0.#", CultureInfo.InvariantCulture);
                    string effectiveMin = (s.EffectiveSeconds / 60.0).ToString("0.#", CultureInfo.InvariantCulture);
                    string taskTitle = s.TaskTitleSnapshot ?? string.Empty;
                    string tags = (s.TagsSnapshot != null && s.TagsSnapshot.Count > 0)
                        ? string.Join(" ", s.TagsSnapshot.ToArray())
                        : string.Empty;

                    sb.Append(EscapeCsv(s.LocalDate ?? string.Empty)).Append(',');
                    sb.Append(EscapeCsv(startText)).Append(',');
                    sb.Append(EscapeCsv(endText)).Append(',');
                    sb.Append(EscapeCsv(phaseCn)).Append(',');
                    sb.Append(EscapeCsv(modeCn)).Append(',');
                    sb.Append(EscapeCsv(outcomeCn)).Append(',');
                    sb.Append(EscapeCsv(plannedMin)).Append(',');
                    sb.Append(EscapeCsv(effectiveMin)).Append(',');
                    sb.Append(EscapeCsv(taskTitle)).Append(',');
                    sb.AppendLine(EscapeCsv(tags));
                }
            }

            return sb.ToString();
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }

        private static void AddToDict(Dictionary<string, double> dict, string key, double amount)
        {
            double cur;
            dict.TryGetValue(key, out cur);
            dict[key] = cur + amount;
        }

        private static bool TryGetSessionLogicalDate(FocusSession s, int dayStartHour, TimeZoneInfo tz, out DateTime date)
        {
            date = DateTime.MinValue;
            if (!string.IsNullOrEmpty(s.LocalDate))
            {
                if (DateTime.TryParseExact(s.LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    return true;
                }
            }
            DateTime? startUtc = PomodoroEngine.TryParseUtc(s.StartedAtUtc);
            if (startUtc.HasValue)
            {
                DateTime local = TimeZoneInfo.ConvertTimeFromUtc(startUtc.Value, tz ?? TimeZoneInfo.Local);
                date = GetLogicalDate(local, dayStartHour);
                return true;
            }
            return false;
        }
    }
}
