using System;
using System.Collections.Generic;
using PomodoroFocus.Core.Engine;
using PomodoroFocus.Core.Models;

namespace PomodoroFocus.Core.Services
{
    public sealed class DeletedTaskRecord
    {
        public TaskItem Task { get; private set; }
        public int OriginalIndex { get; private set; }
        public bool WasActiveTask { get; private set; }

        public DeletedTaskRecord(TaskItem task, int originalIndex, bool wasActiveTask)
        {
            Task = task;
            OriginalIndex = originalIndex;
            WasActiveTask = wasActiveTask;
        }
    }

    public sealed class TaskService
    {
        private readonly List<TaskItem> _tasks;
        private DeletedTaskRecord _lastDeleted;

        public TaskService(IEnumerable<TaskItem> initialTasks)
        {
            _tasks = new List<TaskItem>();
            if (initialTasks != null)
            {
                foreach (var t in initialTasks)
                {
                    if (t != null)
                    {
                        var clone = t.Clone();
                        clone.Sanitize();
                        if (!string.IsNullOrEmpty(clone.Title))
                        {
                            _tasks.Add(clone);
                        }
                    }
                }
            }
        }

        public IList<TaskItem> GetAllSnapshot()
        {
            var list = new List<TaskItem>(_tasks.Count);
            for (int i = 0; i < _tasks.Count; i++)
            {
                list.Add(_tasks[i].Clone());
            }
            return list;
        }

        public void ReplaceAll(IEnumerable<TaskItem> items)
        {
            _tasks.Clear();
            _lastDeleted = null;
            if (items != null)
            {
                foreach (var t in items)
                {
                    if (t != null)
                    {
                        var clone = t.Clone();
                        clone.Sanitize();
                        if (!string.IsNullOrEmpty(clone.Title))
                        {
                            _tasks.Add(clone);
                        }
                    }
                }
            }
        }

        public TaskItem FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].Id == id) return _tasks[i].Clone();
            }
            return null;
        }

        public TaskItem AddTask(TaskItem item)
        {
            if (item == null) throw new ArgumentNullException("item");
            var clone = item.Clone();
            clone.Sanitize();
            if (string.IsNullOrEmpty(clone.Title))
            {
                throw new ArgumentException("请输入任务标题");
            }
            _tasks.Add(clone);
            return clone.Clone();
        }

        public TaskItem AddFromQuickInput(string input, DateTime utcNow)
        {
            QuickAddResult parsed = QuickAddParser.Parse(input);
            if (string.IsNullOrEmpty(parsed.Title))
            {
                return null;
            }
            var item = new TaskItem();
            item.Title = parsed.Title;
            item.PriorityEnum = parsed.Priority;
            item.EstimatedPomodoros = parsed.EstimatedPomodoros;
            item.Tags = new List<string>(parsed.Tags);
            item.CreatedAtUtc = PomodoroEngine.FormatUtc(utcNow);
            return AddTask(item);
        }

        public bool UpdateTask(TaskItem updated)
        {
            if (updated == null) return false;
            var clone = updated.Clone();
            clone.Sanitize();
            if (string.IsNullOrEmpty(clone.Title)) return false;

            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].Id == clone.Id)
                {
                    clone.CompletedPomodoros = _tasks[i].CompletedPomodoros;
                    clone.CreatedAtUtc = _tasks[i].CreatedAtUtc;
                    _tasks[i] = clone;
                    return true;
                }
            }
            return false;
        }

        public DeletedTaskRecord DeleteTask(string id, string currentActiveTaskId)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].Id == id)
                {
                    TaskItem removed = _tasks[i];
                    _tasks.RemoveAt(i);
                    _lastDeleted = new DeletedTaskRecord(removed.Clone(), i, currentActiveTaskId == id);
                    return _lastDeleted;
                }
            }
            return null;
        }

        public DeletedTaskRecord UndoLastDelete()
        {
            if (_lastDeleted == null) return null;
            DeletedTaskRecord rec = _lastDeleted;
            _lastDeleted = null;

            int idx = rec.OriginalIndex;
            if (idx < 0) idx = 0;
            if (idx > _tasks.Count) idx = _tasks.Count;
            _tasks.Insert(idx, rec.Task.Clone());
            return rec;
        }

        public TaskItem ToggleComplete(string id, DateTime utcNow)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].Id == id)
                {
                    TaskItem t = _tasks[i];
                    t.IsCompleted = !t.IsCompleted;
                    t.CompletedAtUtc = t.IsCompleted ? PomodoroEngine.FormatUtc(utcNow) : null;
                    return t.Clone();
                }
            }
            return null;
        }

        public TaskItem SetCompleted(string id, bool completed, DateTime utcNow)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].Id == id)
                {
                    TaskItem t = _tasks[i];
                    t.IsCompleted = completed;
                    t.CompletedAtUtc = completed ? PomodoroEngine.FormatUtc(utcNow) : null;
                    return t.Clone();
                }
            }
            return null;
        }

        public TaskItem IncrementCompletedPomodoros(string id, int delta)
        {
            if (string.IsNullOrEmpty(id) || delta <= 0) return null;
            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].Id == id)
                {
                    _tasks[i].CompletedPomodoros += delta;
                    return _tasks[i].Clone();
                }
            }
            return null;
        }

        public int ActiveCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < _tasks.Count; i++)
                {
                    if (!_tasks[i].IsCompleted) c++;
                }
                return c;
            }
        }

        public int CompletedCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < _tasks.Count; i++)
                {
                    if (_tasks[i].IsCompleted) c++;
                }
                return c;
            }
        }

        public int TotalCount
        {
            get { return _tasks.Count; }
        }

        public List<string> GetAllDistinctTags()
        {
            var result = new List<string>();
            for (int i = 0; i < _tasks.Count; i++)
            {
                List<string> tags = _tasks[i].Tags;
                if (tags == null) continue;
                for (int j = 0; j < tags.Count; j++)
                {
                    string tag = tags[j];
                    bool exists = false;
                    for (int k = 0; k < result.Count; k++)
                    {
                        if (string.Equals(result[k], tag, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists) result.Add(tag);
                }
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public List<TaskItem> Query(
            TaskFilterMode filter,
            IList<string> selectedTags,
            string searchText,
            TaskSortMode sortMode)
        {
            string search = string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim();
            bool filterTags = selectedTags != null && selectedTags.Count > 0;

            var matched = new List<TaskItem>();
            for (int i = 0; i < _tasks.Count; i++)
            {
                TaskItem t = _tasks[i];
                if (filter == TaskFilterMode.Active && t.IsCompleted) continue;
                if (filter == TaskFilterMode.Completed && !t.IsCompleted) continue;

                if (filterTags)
                {
                    bool anyTag = false;
                    if (t.Tags != null)
                    {
                        for (int ti = 0; ti < t.Tags.Count && !anyTag; ti++)
                        {
                            for (int si = 0; si < selectedTags.Count; si++)
                            {
                                if (string.Equals(t.Tags[ti], selectedTags[si], StringComparison.OrdinalIgnoreCase))
                                {
                                    anyTag = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (!anyTag) continue;
                }

                if (search != null)
                {
                    bool hit = (t.Title != null && t.Title.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (t.Notes != null && t.Notes.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!hit && t.Tags != null)
                    {
                        for (int ti = 0; ti < t.Tags.Count; ti++)
                        {
                            if (t.Tags[ti].IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                hit = true;
                                break;
                            }
                        }
                    }
                    if (!hit) continue;
                }

                matched.Add(t.Clone());
            }

            matched.Sort(delegate(TaskItem a, TaskItem b)
            {
                return CompareTasks(a, b, sortMode);
            });

            return matched;
        }

        private static int CompareTasks(TaskItem a, TaskItem b, TaskSortMode mode)
        {
            if (mode == TaskSortMode.Smart)
            {
                if (a.IsCompleted != b.IsCompleted)
                {
                    return a.IsCompleted ? 1 : -1;
                }
                if (a.Priority != b.Priority)
                {
                    return b.Priority.CompareTo(a.Priority);
                }
                return string.CompareOrdinal(a.CreatedAtUtc ?? string.Empty, b.CreatedAtUtc ?? string.Empty);
            }
            if (mode == TaskSortMode.Priority)
            {
                if (a.Priority != b.Priority)
                {
                    return b.Priority.CompareTo(a.Priority);
                }
                if (a.IsCompleted != b.IsCompleted)
                {
                    return a.IsCompleted ? 1 : -1;
                }
                return string.CompareOrdinal(a.CreatedAtUtc ?? string.Empty, b.CreatedAtUtc ?? string.Empty);
            }
            if (mode == TaskSortMode.CreatedNewest)
            {
                return string.CompareOrdinal(b.CreatedAtUtc ?? string.Empty, a.CreatedAtUtc ?? string.Empty);
            }
            // RemainingPomodoros
            int remCmp = b.RemainingPomodoros.CompareTo(a.RemainingPomodoros);
            if (remCmp != 0) return remCmp;
            if (a.IsCompleted != b.IsCompleted)
            {
                return a.IsCompleted ? 1 : -1;
            }
            return b.Priority.CompareTo(a.Priority);
        }
    }
}
