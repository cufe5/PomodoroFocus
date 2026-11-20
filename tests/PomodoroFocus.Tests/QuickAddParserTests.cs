using PomodoroFocus.Core.Engine;
using PomodoroFocus.Core.Models;

namespace PomodoroFocus.Tests
{
    public sealed class QuickAddParserTests
    {
        public void ParsesFullSyntaxWithTagsPriorityAndEstimate()
        {
            QuickAddResult res = QuickAddParser.Parse("写周报 #工作 #复盘 !高 ~3");
            AssertEx.Equal("写周报", res.Title, "Title");
            AssertEx.Equal(TaskPriority.High, res.Priority, "Priority High");
            AssertEx.Equal(3, res.EstimatedPomodoros, "Estimate 3");
            AssertEx.Equal(2, res.Tags.Count, "Two tags");
            AssertEx.Equal("工作", res.Tags[0], "Tag 0");
            AssertEx.Equal("复盘", res.Tags[1], "Tag 1");
        }

        public void SupportsNumericPriorityAndAsteriskEstimate()
        {
            QuickAddResult res = QuickAddParser.Parse("修复构建告警 !2 *5 #架构");
            AssertEx.Equal("修复构建告警", res.Title, "Title");
            AssertEx.Equal(TaskPriority.Medium, res.Priority, "Priority Medium");
            AssertEx.Equal(5, res.EstimatedPomodoros, "Estimate 5");
            AssertEx.Equal(1, res.Tags.Count, "One tag");
        }

        public void ClampsEstimateAndLimitsTagsToFive()
        {
            QuickAddResult res = QuickAddParser.Parse("测试边界 ~99 #t1 #t2 #t3 #t4 #t5 #t6 !1");
            AssertEx.Equal("测试边界", res.Title, "Title");
            AssertEx.Equal(20, res.EstimatedPomodoros, "Clamped to 20");
            AssertEx.Equal(TaskPriority.Low, res.Priority, "Low");
            AssertEx.Equal(5, res.Tags.Count, "Max 5 tags");
        }

        public void EmptyOrTokensOnlyReturnsEmptyTitle()
        {
            QuickAddResult r1 = QuickAddParser.Parse("   ");
            AssertEx.Equal(string.Empty, r1.Title, "Whitespace empty");

            QuickAddResult r2 = QuickAddParser.Parse("#标签 !高 ~2");
            AssertEx.Equal(string.Empty, r2.Title, "Tokens only gives empty title");
        }
    }
}
