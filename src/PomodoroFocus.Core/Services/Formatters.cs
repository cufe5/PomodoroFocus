using System;
using System.Globalization;

namespace PomodoroFocus.Core.Services
{
    public static class Formatters
    {
        private static readonly string[] WeekdayNames = new string[]
        {
            "周日", "周一", "周二", "周三", "周四", "周五", "周六"
        };

        public static string FormatTimer(int totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;
            if (hours > 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1:D2}:{2:D2}", hours, minutes, seconds);
            }
            return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", minutes, seconds);
        }

        public static string FormatDurationChinese(int totalSeconds)
        {
            if (totalSeconds < 60)
            {
                return "不足 1 分钟";
            }
            int totalMinutes = totalSeconds / 60;
            if (totalMinutes < 60)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} 分钟", totalMinutes);
            }
            int hours = totalMinutes / 60;
            int mins = totalMinutes % 60;
            return string.Format(CultureInfo.InvariantCulture, "{0} 小时 {1:D2} 分", hours, mins);
        }

        public static string FormatHoursKpiValue(int totalSeconds)
        {
            double hours = totalSeconds / 3600.0;
            return hours.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static string FormatDateWithWeekday(DateTime date, int currentYear)
        {
            string wk = WeekdayNames[(int)date.DayOfWeek];
            if (date.Year != currentYear)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}年{1}月{2}日 {3}", date.Year, date.Month, date.Day, wk);
            }
            return string.Format(CultureInfo.InvariantCulture, "{0}月{1}日 {2}", date.Month, date.Day, wk);
        }

        public static string FormatTimeRange(DateTime startLocal, DateTime endLocal)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:HH:mm} \u2013 {1:HH:mm}",
                startLocal,
                endLocal);
        }

        public static string FormatWeekdayShort(DayOfWeek dow)
        {
            return WeekdayNames[(int)dow];
        }

        public static string FormatPercentDelta(int currentSeconds, int previousSeconds)
        {
            if (previousSeconds <= 0)
            {
                if (currentSeconds <= 0) return "较上周持平";
                return "较上周 +100%";
            }
            int pct = (int)Math.Round(((currentSeconds - previousSeconds) * 100.0) / previousSeconds);
            if (pct == 0) return "较上周持平";
            if (pct > 0) return string.Format(CultureInfo.InvariantCulture, "较上周 +{0}%", pct);
            return string.Format(CultureInfo.InvariantCulture, "较上周 {0}%", pct);
        }
    }
}
