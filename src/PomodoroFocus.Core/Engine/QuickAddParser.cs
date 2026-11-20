using System;
using System.Collections.Generic;
using PomodoroFocus.Core.Models;

namespace PomodoroFocus.Core.Engine
{
    public sealed class QuickAddResult
    {
        public string Title { get; set; }
        public TaskPriority Priority { get; set; }
        public int EstimatedPomodoros { get; set; }
        public List<string> Tags { get; set; }

        public QuickAddResult()
        {
            Title = string.Empty;
            Priority = TaskPriority.None;
            EstimatedPomodoros = 0;
            Tags = new List<string>();
        }
    }

    public static class QuickAddParser
    {
        public static QuickAddResult Parse(string input)
        {
            var result = new QuickAddResult();
            if (string.IsNullOrWhiteSpace(input))
            {
                return result;
            }

            string[] tokens = input.Trim().Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var titleParts = new List<string>();

            for (int i = 0; i < tokens.Length; i++)
            {
                string tok = tokens[i];
                if (tok.Length > 1 && (tok[0] == '#' || tok[0] == '\uFF03'))
                {
                    string tag = tok.Substring(1).Trim();
                    if (tag.Length > 0 && result.Tags.Count < 5)
                    {
                        if (tag.Length > 12) tag = tag.Substring(0, 12);
                        bool dup = false;
                        for (int j = 0; j < result.Tags.Count; j++)
                        {
                            if (string.Equals(result.Tags[j], tag, StringComparison.OrdinalIgnoreCase))
                            {
                                dup = true;
                                break;
                            }
                        }
                        if (!dup) result.Tags.Add(tag);
                    }
                    continue;
                }

                if (tok.Length > 1 && (tok[0] == '!' || tok[0] == '\uFF01'))
                {
                    string p = tok.Substring(1).Trim().ToLowerInvariant();
                    if (p == "高" || p == "3" || p == "high" || p == "h")
                    {
                        result.Priority = TaskPriority.High;
                        continue;
                    }
                    if (p == "中" || p == "2" || p == "medium" || p == "med" || p == "m")
                    {
                        result.Priority = TaskPriority.Medium;
                        continue;
                    }
                    if (p == "低" || p == "1" || p == "low" || p == "l")
                    {
                        result.Priority = TaskPriority.Low;
                        continue;
                    }
                    if (p == "无" || p == "0" || p == "none")
                    {
                        result.Priority = TaskPriority.None;
                        continue;
                    }
                }

                if (tok.Length > 1 && (tok[0] == '~' || tok[0] == '*' || tok[0] == '\uFF5E'))
                {
                    int est;
                    if (int.TryParse(tok.Substring(1), out est))
                    {
                        if (est < 0) est = 0;
                        if (est > 20) est = 20;
                        result.EstimatedPomodoros = est;
                        continue;
                    }
                }

                titleParts.Add(tok);
            }

            string title = string.Join(" ", titleParts.ToArray()).Trim();
            if (title.Length > 80) title = title.Substring(0, 80);
            result.Title = title;
            return result;
        }
    }
}
