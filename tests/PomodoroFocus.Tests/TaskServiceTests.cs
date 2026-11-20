using System;
using System.Collections.Generic;
using PomodoroFocus.Core.Models;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.Tests
{
    public sealed class TaskServiceTests
    {
        public void CrudFilterSortAndDeleteUndoWorkEndToEnd()
        {
            var svc = new TaskService(null);
            DateTime t0 = new DateTime(2026, 11, 19, 1, 0, 0, DateTimeKind.Utc);

            TaskItem a = svc.AddFromQuickInput("低优先级任务 #学习 !低 ~1", t0);
            TaskItem b = svc.AddFromQuickInput("高优先级核心任务 #工作 #架构 !高 ~4", t0.AddMinutes(1));
            TaskItem c = svc.AddFromQuickInput("中优先级周报 #工作 !中 ~2", t0.AddMinutes(2));

            AssertEx.Equal(3, svc.TotalCount, "3 tasks added");
            AssertEx.Equal(3, svc.ActiveCount, "3 active");

            // Smart sort: active first, priority desc, created asc
            List<TaskItem> smart = svc.Query(TaskFilterMode.Active, null, null, TaskSortMode.Smart);
            AssertEx.Equal(b.Id, smart[0].Id, "High priority first");
            AssertEx.Equal(c.Id, smart[1].Id, "Medium priority second");
            AssertEx.Equal(a.Id, smart[2].Id, "Low priority third");

            // CreatedNewest sort
            List<TaskItem> newest = svc.Query(TaskFilterMode.All, null, null, TaskSortMode.CreatedNewest);
            AssertEx.Equal(c.Id, newest[0].Id, "Newest first");

            // RemainingPomodoros sort
            svc.IncrementCompletedPomodoros(b.Id, 1); // b remaining = 3
            List<TaskItem> rem = svc.Query(TaskFilterMode.All, null, null, TaskSortMode.RemainingPomodoros);
            AssertEx.Equal(b.Id, rem[0].Id, "Highest remaining first");

            // Tag OR filter
            List<TaskItem> tagFiltered = svc.Query(TaskFilterMode.All, new List<string> { "学习", "架构" }, null, TaskSortMode.Smart);
            AssertEx.Equal(2, tagFiltered.Count, "OR tag filter matches 2");

            // Search case-insensitive across title/tags
            List<TaskItem> searchRes = svc.Query(TaskFilterMode.All, null, "周报", TaskSortMode.Smart);
            AssertEx.Equal(1, searchRes.Count, "Search matches 1");
            AssertEx.Equal(c.Id, searchRes[0].Id, "Matched c");

            // Toggle complete
            TaskItem toggled = svc.ToggleComplete(c.Id, t0.AddMinutes(10));
            AssertEx.IsTrue(toggled.IsCompleted, "c completed");
            AssertEx.Equal(2, svc.ActiveCount, "2 active");
            AssertEx.Equal(1, svc.CompletedCount, "1 completed");

            // Delete & undo
            DeletedTaskRecord del = svc.DeleteTask(b.Id, b.Id);
            AssertEx.NotNull(del, "Deleted record");
            AssertEx.IsTrue(del.WasActiveTask, "Recorded was active");
            AssertEx.Equal(2, svc.TotalCount, "2 left");

            DeletedTaskRecord restored = svc.UndoLastDelete();
            AssertEx.NotNull(restored, "Undo restored");
            AssertEx.Equal(3, svc.TotalCount, "3 after undo");
            AssertEx.NotNull(svc.FindById(b.Id), "b restored");
        }
    }
}
