using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Threading;
using PomodoroFocus.App.Controls;
using PomodoroFocus.Core.Models;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.App.ViewModels
{
    public sealed class TagFilterChipViewModel : ObservableObject
    {
        private bool _isSelected;
        public string TagName { get; set; }
        public string DisplayLabel { get; set; }
        public bool IsAllChip { get; set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetField(ref _isSelected, value, "IsSelected"); }
        }
    }

    public sealed class TomatoDotViewModel
    {
        public bool IsFilled { get; set; }
    }

    public sealed class TaskRowViewModel
    {
        public TaskItem Model { get; set; }
        public string Id { get; set; }
        public string Title { get; set; }
        public string Notes { get; set; }
        public bool HasNotes { get; set; }
        public int Priority { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsActiveFocusTask { get; set; }
        public List<string> Tags { get; set; }
        public List<TomatoDotViewModel> TomatoDots { get; set; }
        public bool ShowDotRow { get; set; }
        public string ProgressSummaryText { get; set; }
        public bool HasProgress { get; set; }
    }

    public sealed class SortOptionItem
    {
        public TaskSortMode Mode { get; set; }
        public string Label { get; set; }
        public override string ToString() { return Label; }
    }

    public sealed class TasksViewModel : ObservableObject
    {
        private readonly MainViewModel _main;
        private readonly DispatcherTimer _searchDebounce;
        private readonly List<string> _selectedTags = new List<string>();

        private string _quickAddText = string.Empty;
        private string _searchText = string.Empty;
        private string _debouncedSearch = string.Empty;
        private string _selectedFilterKey = "active";
        private SortOptionItem _selectedSort;
        private string _subtitleText = "共 0 项 · 已完成 0 项";
        private string _emptyTitle = "没有进行中的任务";
        private string _emptySubtitle = "在上方输入框添加一个吧";
        private bool _isEmpty;

        public TasksViewModel(MainViewModel main)
        {
            _main = main;
            _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _searchDebounce.Tick += delegate
            {
                _searchDebounce.Stop();
                _debouncedSearch = _searchText;
                RefreshList();
            };

            FilterOptions = new ObservableCollection<SegmentOption>();
            TagChips = new ObservableCollection<TagFilterChipViewModel>();
            Tasks = new ObservableCollection<TaskRowViewModel>();

            SortOptions = new List<SortOptionItem>
            {
                new SortOptionItem { Mode = TaskSortMode.Smart, Label = "智能排序" },
                new SortOptionItem { Mode = TaskSortMode.Priority, Label = "优先级" },
                new SortOptionItem { Mode = TaskSortMode.CreatedNewest, Label = "创建时间（新→旧）" },
                new SortOptionItem { Mode = TaskSortMode.RemainingPomodoros, Label = "剩余番茄（多→少）" }
            };
            _selectedSort = SortOptions[0];

            QuickAddCommand = new RelayCommand(SubmitQuickAdd);
            NewTaskDialogCommand = new RelayCommand(delegate { _main.OpenNewTaskDialog(); });
            ClearSearchCommand = new RelayCommand(delegate { SearchText = string.Empty; });
            ToggleTagChipCommand = new RelayCommand<TagFilterChipViewModel>(ToggleTagChip);
            ToggleTaskCompleteCommand = new RelayCommand<string>(delegate(string id) { _main.ToggleTaskComplete(id); });
            StartFocusTaskCommand = new RelayCommand<string>(delegate(string id) { _main.StartFocusOnTask(id); });
            EditTaskCommand = new RelayCommand<string>(delegate(string id) { _main.OpenEditTaskDialog(id); });
            DeleteTaskCommand = new RelayCommand<string>(delegate(string id) { _main.DeleteTask(id); });
        }

        public ObservableCollection<SegmentOption> FilterOptions { get; private set; }
        public ObservableCollection<TagFilterChipViewModel> TagChips { get; private set; }
        public ObservableCollection<TaskRowViewModel> Tasks { get; private set; }
        public IList<SortOptionItem> SortOptions { get; private set; }

        public ICommand QuickAddCommand { get; private set; }
        public ICommand NewTaskDialogCommand { get; private set; }
        public ICommand ClearSearchCommand { get; private set; }
        public ICommand ToggleTagChipCommand { get; private set; }
        public ICommand ToggleTaskCompleteCommand { get; private set; }
        public ICommand StartFocusTaskCommand { get; private set; }
        public ICommand EditTaskCommand { get; private set; }
        public ICommand DeleteTaskCommand { get; private set; }

        public string QuickAddText
        {
            get { return _quickAddText; }
            set { SetField(ref _quickAddText, value, "QuickAddText"); }
        }

        public string SearchText
        {
            get { return _searchText; }
            set
            {
                if (SetField(ref _searchText, value, "SearchText"))
                {
                    RaisePropertyChanged("HasSearchText");
                    _searchDebounce.Stop();
                    _searchDebounce.Start();
                }
            }
        }

        public bool HasSearchText
        {
            get { return !string.IsNullOrEmpty(_searchText); }
        }

        public string SelectedFilterKey
        {
            get { return _selectedFilterKey; }
            set
            {
                if (SetField(ref _selectedFilterKey, value, "SelectedFilterKey"))
                {
                    RefreshList();
                }
            }
        }

        public SortOptionItem SelectedSort
        {
            get { return _selectedSort; }
            set
            {
                if (SetField(ref _selectedSort, value, "SelectedSort"))
                {
                    RefreshList();
                }
            }
        }

        public string SubtitleText
        {
            get { return _subtitleText; }
            private set { SetField(ref _subtitleText, value, "SubtitleText"); }
        }

        public string EmptyTitle
        {
            get { return _emptyTitle; }
            private set { SetField(ref _emptyTitle, value, "EmptyTitle"); }
        }

        public string EmptySubtitle
        {
            get { return _emptySubtitle; }
            private set { SetField(ref _emptySubtitle, value, "EmptySubtitle"); }
        }

        public bool IsEmpty
        {
            get { return _isEmpty; }
            private set { SetField(ref _isEmpty, value, "IsEmpty"); }
        }

        public void RefreshAll()
        {
            int active = _main.TaskService.ActiveCount;
            int completed = _main.TaskService.CompletedCount;
            int total = _main.TaskService.TotalCount;

            SubtitleText = string.Format(CultureInfo.InvariantCulture, "共 {0} 项 · 已完成 {1} 项", total, completed);

            FilterOptions.Clear();
            FilterOptions.Add(new SegmentOption { Key = "active", Label = string.Format("进行中 ({0})", active), AutomationId = "TaskFilter_Active" });
            FilterOptions.Add(new SegmentOption { Key = "completed", Label = string.Format("已完成 ({0})", completed), AutomationId = "TaskFilter_Completed" });
            FilterOptions.Add(new SegmentOption { Key = "all", Label = string.Format("全部 ({0})", total), AutomationId = "TaskFilter_All" });
            RaisePropertyChanged("FilterOptions");

            List<string> allTags = _main.TaskService.GetAllDistinctTags();
            // Prune selected tags that no longer exist
            for (int i = _selectedTags.Count - 1; i >= 0; i--)
            {
                bool found = false;
                for (int j = 0; j < allTags.Count; j++)
                {
                    if (string.Equals(allTags[j], _selectedTags[i], StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) _selectedTags.RemoveAt(i);
            }

            TagChips.Clear();
            TagChips.Add(new TagFilterChipViewModel
            {
                TagName = string.Empty,
                DisplayLabel = "全部标签",
                IsAllChip = true,
                IsSelected = _selectedTags.Count == 0
            });
            for (int i = 0; i < allTags.Count; i++)
            {
                string tag = allTags[i];
                bool isSel = _selectedTags.Contains(tag);
                TagChips.Add(new TagFilterChipViewModel
                {
                    TagName = tag,
                    DisplayLabel = "#" + tag,
                    IsAllChip = false,
                    IsSelected = isSel
                });
            }

            RefreshList();
        }

        public void RefreshList()
        {
            TaskFilterMode filter = TaskFilterMode.Active;
            if (_selectedFilterKey == "completed") filter = TaskFilterMode.Completed;
            else if (_selectedFilterKey == "all") filter = TaskFilterMode.All;

            TaskSortMode sort = _selectedSort != null ? _selectedSort.Mode : TaskSortMode.Smart;
            List<TaskItem> rows = _main.TaskService.Query(filter, _selectedTags, _debouncedSearch, sort);
            string activeId = _main.Settings.ActiveTaskId;

            Tasks.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                TaskItem t = rows[i];
                var dots = new List<TomatoDotViewModel>();
                bool showDots = t.EstimatedPomodoros > 0 && t.EstimatedPomodoros <= 6;
                if (showDots)
                {
                    for (int k = 0; k < t.EstimatedPomodoros; k++)
                    {
                        dots.Add(new TomatoDotViewModel { IsFilled = k < t.CompletedPomodoros });
                    }
                }

                string progressSummary = string.Empty;
                if (t.EstimatedPomodoros > 0)
                {
                    progressSummary = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", t.CompletedPomodoros, t.EstimatedPomodoros);
                }
                else if (t.CompletedPomodoros > 0)
                {
                    progressSummary = string.Format(CultureInfo.InvariantCulture, "\u00D7{0}", t.CompletedPomodoros);
                }

                Tasks.Add(new TaskRowViewModel
                {
                    Model = t,
                    Id = t.Id,
                    Title = t.Title,
                    Notes = t.Notes,
                    HasNotes = !string.IsNullOrWhiteSpace(t.Notes),
                    Priority = t.Priority,
                    IsCompleted = t.IsCompleted,
                    IsActiveFocusTask = (!string.IsNullOrEmpty(activeId) && activeId == t.Id),
                    Tags = t.Tags != null ? new List<string>(t.Tags) : new List<string>(),
                    TomatoDots = dots,
                    ShowDotRow = showDots,
                    ProgressSummaryText = progressSummary,
                    HasProgress = !string.IsNullOrEmpty(progressSummary)
                });
            }

            IsEmpty = Tasks.Count == 0;
            if (!string.IsNullOrWhiteSpace(_debouncedSearch) || _selectedTags.Count > 0)
            {
                EmptyTitle = "没有找到匹配的任务";
                EmptySubtitle = "尝试调整搜索关键词或清除标签筛选";
            }
            else if (filter == TaskFilterMode.Completed)
            {
                EmptyTitle = "还没有完成的任务";
                EmptySubtitle = "完成一个专注任务后将显示在这里";
            }
            else
            {
                EmptyTitle = "没有进行中的任务";
                EmptySubtitle = "在上方输入框添加一个吧";
            }
        }

        private void SubmitQuickAdd()
        {
            if (string.IsNullOrWhiteSpace(QuickAddText)) return;
            TaskItem added = _main.AddTaskQuick(QuickAddText);
            if (added != null)
            {
                QuickAddText = string.Empty;
            }
        }

        private void ToggleTagChip(TagFilterChipViewModel chip)
        {
            if (chip == null) return;
            if (chip.IsAllChip)
            {
                _selectedTags.Clear();
            }
            else
            {
                if (_selectedTags.Contains(chip.TagName))
                {
                    _selectedTags.Remove(chip.TagName);
                }
                else
                {
                    _selectedTags.Add(chip.TagName);
                }
            }

            for (int i = 0; i < TagChips.Count; i++)
            {
                TagFilterChipViewModel c = TagChips[i];
                c.IsSelected = c.IsAllChip ? (_selectedTags.Count == 0) : _selectedTags.Contains(c.TagName);
            }
            RefreshList();
        }
    }
}
