using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using PomodoroFocus.Core.Engine;
using PomodoroFocus.Core.Models;

namespace PomodoroFocus.Core.Services
{
    public sealed class StorageService : IDisposable
    {
        private static readonly DataContractJsonSerializerSettings JsonSettings = new DataContractJsonSerializerSettings
        {
            UseSimpleDictionaryFormat = true
        };

        private readonly string _dataDir;
        private readonly object _ioLock = new object();
        private readonly object _debounceLock = new object();
        private readonly Dictionary<string, Action> _pendingWrites = new Dictionary<string, Action>();
        private Timer _debounceTimer;
        private bool _disposed;

        public string DataDirectory
        {
            get { return _dataDir; }
        }

        public bool IsReadOnlyMode { get; private set; }
        public string RecoveryNotice { get; private set; }

        public StorageService(string dataDirectory)
        {
            if (string.IsNullOrWhiteSpace(dataDirectory))
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                _dataDir = Path.Combine(localApp, "PomodoroFocus");
            }
            else
            {
                _dataDir = dataDirectory;
            }

            IsReadOnlyMode = !ProbeWritable(_dataDir);
            if (!IsReadOnlyMode)
            {
                CleanupOldLogs();
            }
        }

        public static bool ProbeWritable(string dir)
        {
            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string probeFile = Path.Combine(dir, ".probe_" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probeFile, "ok", Encoding.UTF8);
                File.Delete(probeFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public AppSettings LoadSettings()
        {
            AppSettings s = LoadJson<AppSettings>("settings.json", delegate { return new AppSettings(); });
            s.Clamp();
            return s;
        }

        public List<TaskItem> LoadTasks()
        {
            List<TaskItem> tasks = LoadJson<List<TaskItem>>("tasks.json", delegate { return new List<TaskItem>(); });
            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i] != null) tasks[i].Sanitize();
            }
            return tasks;
        }

        public List<FocusSession> LoadSessions()
        {
            return LoadJson<List<FocusSession>>("sessions.json", delegate { return new List<FocusSession>(); });
        }

        public TimerPersistedState LoadTimerState()
        {
            return LoadJson<TimerPersistedState>("timer_state.json", delegate { return null; });
        }

        public void SaveSettings(AppSettings settings, bool immediate)
        {
            if (settings == null || IsReadOnlyMode) return;
            AppSettings copy = settings.Clone();
            ScheduleOrWrite("settings.json", delegate { WriteJsonAtomic("settings.json", copy); }, immediate);
        }

        public void SaveTasks(IList<TaskItem> tasks, bool immediate)
        {
            if (tasks == null || IsReadOnlyMode) return;
            var copy = new List<TaskItem>(tasks.Count);
            for (int i = 0; i < tasks.Count; i++) copy.Add(tasks[i].Clone());
            ScheduleOrWrite("tasks.json", delegate { WriteJsonAtomic("tasks.json", copy); }, immediate);
        }

        public void SaveSessions(IList<FocusSession> sessions, bool immediate)
        {
            if (sessions == null || IsReadOnlyMode) return;
            var copy = new List<FocusSession>(sessions);
            ScheduleOrWrite("sessions.json", delegate { WriteJsonAtomic("sessions.json", copy); }, immediate);
        }

        public void SaveTimerState(TimerPersistedState state, bool immediate)
        {
            if (state == null || IsReadOnlyMode) return;
            ScheduleOrWrite("timer_state.json", delegate { WriteJsonAtomic("timer_state.json", state); }, immediate);
        }

        public void ExportBackupToFile(string filePath, BackupBundle bundle)
        {
            if (bundle == null) throw new ArgumentNullException("bundle");
            bundle.SchemaVersion = 1;
            bundle.ExportedAtUtc = PomodoroEngine.FormatUtc(DateTime.UtcNow);
            byte[] bytes = SerializeToUtf8Bytes(bundle);
            File.WriteAllBytes(filePath, bytes);
        }

        public BackupBundle ImportBackupFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("找不到备份文件", filePath);
            }
            byte[] bytes = File.ReadAllBytes(filePath);
            BackupBundle bundle = DeserializeFromUtf8Bytes<BackupBundle>(bytes);
            if (bundle == null || bundle.SchemaVersion != 1)
            {
                throw new InvalidDataException("备份文件格式或版本不受支持（需要 SchemaVersion = 1）。");
            }
            if (bundle.Settings == null) bundle.Settings = new AppSettings();
            bundle.Settings.Clamp();
            if (bundle.Tasks == null) bundle.Tasks = new List<TaskItem>();
            for (int i = 0; i < bundle.Tasks.Count; i++)
            {
                if (bundle.Tasks[i] != null) bundle.Tasks[i].Sanitize();
            }
            if (bundle.Sessions == null) bundle.Sessions = new List<FocusSession>();
            return bundle;
        }

        public void WriteLog(string level, string message)
        {
            if (IsReadOnlyMode) return;
            try
            {
                string logDir = Path.Combine(_dataDir, "logs");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                string file = Path.Combine(logDir, "app-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
                string line = string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0:HH:mm:ss.fff}] [{1}] {2}{3}",
                    DateTime.Now,
                    level,
                    message,
                    Environment.NewLine);
                lock (_ioLock)
                {
                    File.AppendAllText(file, line, Encoding.UTF8);
                }
            }
            catch
            {
                // Ignore logging errors
            }
        }

        public void Flush()
        {
            List<Action> actions = null;
            lock (_debounceLock)
            {
                if (_debounceTimer != null)
                {
                    _debounceTimer.Dispose();
                    _debounceTimer = null;
                }
                if (_pendingWrites.Count > 0)
                {
                    actions = new List<Action>(_pendingWrites.Values);
                    _pendingWrites.Clear();
                }
            }
            if (actions != null)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    try { actions[i](); }
                    catch (Exception ex) { WriteLog("ERROR", "Flush write failed: " + ex.Message); }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Flush();
        }

        private void ScheduleOrWrite(string key, Action writeAction, bool immediate)
        {
            if (immediate)
            {
                lock (_debounceLock)
                {
                    _pendingWrites.Remove(key);
                }
                writeAction();
                return;
            }

            lock (_debounceLock)
            {
                if (_disposed) return;
                _pendingWrites[key] = writeAction;
                if (_debounceTimer == null)
                {
                    _debounceTimer = new Timer(OnDebounceElapsed, null, 500, Timeout.Infinite);
                }
                else
                {
                    _debounceTimer.Change(500, Timeout.Infinite);
                }
            }
        }

        private void OnDebounceElapsed(object state)
        {
            Flush();
        }

        private T LoadJson<T>(string fileName, Func<T> fallbackFactory) where T : class
        {
            string mainPath = Path.Combine(_dataDir, fileName);
            string bakPath = mainPath + ".bak";

            if (!File.Exists(mainPath))
            {
                if (File.Exists(bakPath))
                {
                    T fromBak = TryReadFile<T>(bakPath);
                    if (fromBak != null)
                    {
                        RecoveryNotice = "数据文件损坏，已从备份恢复";
                        return fromBak;
                    }
                }
                return fallbackFactory();
            }

            T loaded = TryReadFile<T>(mainPath);
            if (loaded != null)
            {
                return loaded;
            }

            // Main file is corrupt
            BackupCorruptFile(mainPath);

            if (File.Exists(bakPath))
            {
                T fromBak = TryReadFile<T>(bakPath);
                if (fromBak != null)
                {
                    RecoveryNotice = "数据文件损坏，已从备份恢复";
                    if (!IsReadOnlyMode)
                    {
                        WriteJsonAtomic(fileName, fromBak);
                    }
                    return fromBak;
                }
            }

            RecoveryNotice = "数据文件损坏，已重置为默认值";
            return fallbackFactory();
        }

        private T TryReadFile<T>(string path) where T : class
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length == 0) return null;
                return DeserializeFromUtf8Bytes<T>(bytes);
            }
            catch
            {
                return null;
            }
        }

        private void BackupCorruptFile(string path)
        {
            if (IsReadOnlyMode) return;
            try
            {
                string dir = Path.GetDirectoryName(path);
                string baseName = Path.GetFileNameWithoutExtension(path);
                string corruptName = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.corrupt-{1:yyyyMMddHHmmss}.json",
                    baseName,
                    DateTime.Now);
                string dest = Path.Combine(dir, corruptName);
                if (File.Exists(dest)) File.Delete(dest);
                try
                {
                    File.Move(path, dest);
                }
                catch (UnauthorizedAccessException)
                {
                    File.Copy(path, dest, true);
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore rename failure
            }
        }

        private void WriteJsonAtomic<T>(string fileName, T data)
        {
            if (IsReadOnlyMode) return;
            lock (_ioLock)
            {
                if (!Directory.Exists(_dataDir))
                {
                    Directory.CreateDirectory(_dataDir);
                }
                string targetPath = Path.Combine(_dataDir, fileName);
                string tmpPath = targetPath + ".tmp";
                string bakPath = targetPath + ".bak";

                byte[] bytes = SerializeToUtf8Bytes(data);
                File.WriteAllBytes(tmpPath, bytes);

                if (File.Exists(targetPath))
                {
                    try
                    {
                        File.Replace(tmpPath, targetPath, bakPath, true);
                    }
                    catch (Exception)
                    {
                        File.Copy(targetPath, bakPath, true);
                        File.Copy(tmpPath, targetPath, true);
                        try { File.Delete(tmpPath); } catch { }
                    }
                }
                else
                {
                    try
                    {
                        File.Move(tmpPath, targetPath);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        File.Copy(tmpPath, targetPath, true);
                        try { File.Delete(tmpPath); } catch { }
                    }
                }
            }
        }

        private void CleanupOldLogs()
        {
            try
            {
                string logDir = Path.Combine(_dataDir, "logs");
                if (!Directory.Exists(logDir)) return;
                DateTime cutoff = DateTime.Now.AddDays(-7);
                string[] files = Directory.GetFiles(logDir, "app-*.log");
                for (int i = 0; i < files.Length; i++)
                {
                    DateTime wt = File.GetLastWriteTime(files[i]);
                    if (wt < cutoff)
                    {
                        File.Delete(files[i]);
                    }
                }
            }
            catch
            {
                // Ignore cleanup failure
            }
        }

        public static byte[] SerializeToUtf8Bytes<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T), JsonSettings);
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, value);
                return ms.ToArray();
            }
        }

        public static T DeserializeFromUtf8Bytes<T>(byte[] bytes) where T : class
        {
            if (bytes == null || bytes.Length == 0) return null;
            int offset = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                offset = 3;
            }
            var serializer = new DataContractJsonSerializer(typeof(T), JsonSettings);
            using (var ms = new MemoryStream(bytes, offset, bytes.Length - offset))
            {
                return (T)serializer.ReadObject(ms);
            }
        }
    }

    public static class DemoDataSeeder
    {
        public static void Seed(StorageService storage, DateTime localNow, TimeZoneInfo tz)
        {
            if (storage == null) return;
            if (tz == null) tz = TimeZoneInfo.Local;

            var settings = new AppSettings();
            settings.Theme = "midnight";
            settings.FocusMinutes = 25;
            settings.ShortBreakMinutes = 5;
            settings.LongBreakMinutes = 15;
            settings.LongBreakInterval = 4;
            settings.DailyGoal = 8;
            settings.AmbientMasterVolume = 75;
            settings.AmbientOnlyDuringFocus = true;
            settings.AmbientMuteOnPause = true;
            settings.AmbientTrackVolumes["rain"] = 70;
            settings.AmbientTrackVolumes["fire"] = 35;
            settings.AmbientTrackVolumes["clock"] = 20;
            settings.AmbientActiveTracks = new List<string> { "rain", "fire", "clock" };

            var t1 = new TaskItem
            {
                Id = "demo_task_01",
                Title = "撰写系统架构规格书",
                Notes = "梳理核心计时状态机、音频合成管线与四套主题 Design Tokens",
                Priority = (int)TaskPriority.High,
                Tags = new List<string> { "架构", "核心" },
                EstimatedPomodoros = 4,
                CompletedPomodoros = 2,
                IsCompleted = false,
                CreatedAtUtc = PomodoroEngine.FormatUtc(DateTime.UtcNow.AddDays(-5))
            };
            var t2 = new TaskItem
            {
                Id = "demo_task_02",
                Title = "音频合成器滤波器单元测试",
                Notes = "验证 9 种环境音与 4 种提示音的 RMS 范围与峰值削波",
                Priority = (int)TaskPriority.High,
                Tags = new List<string> { "工作", "音频" },
                EstimatedPomodoros = 3,
                CompletedPomodoros = 1,
                IsCompleted = false,
                CreatedAtUtc = PomodoroEngine.FormatUtc(DateTime.UtcNow.AddDays(-4))
            };
            var t3 = new TaskItem
            {
                Id = "demo_task_03",
                Title = "整理周会演示文稿",
                Notes = "汇总本周迭代进度、性能基准数据与下阶段里程碑",
                Priority = (int)TaskPriority.Medium,
                Tags = new List<string> { "工作" },
                EstimatedPomodoros = 2,
                CompletedPomodoros = 0,
                IsCompleted = false,
                CreatedAtUtc = PomodoroEngine.FormatUtc(DateTime.UtcNow.AddDays(-3))
            };
            var t4 = new TaskItem
            {
                Id = "demo_task_04",
                Title = "研读 WPF 渲染管线与自定义图表优化",
                Notes = "OnRender 矢量绘图与 175% DPI 对齐细节",
                Priority = (int)TaskPriority.Low,
                Tags = new List<string> { "学习" },
                EstimatedPomodoros = 3,
                CompletedPomodoros = 1,
                IsCompleted = false,
                CreatedAtUtc = PomodoroEngine.FormatUtc(DateTime.UtcNow.AddDays(-2))
            };
            var t5 = new TaskItem
            {
                Id = "demo_task_05",
                Title = "搭建 MSBuild 离线编译工程",
                Notes = "配置 C# 5、UTF-8 CodePage 65001 与零警告构建",
                Priority = (int)TaskPriority.High,
                Tags = new List<string> { "架构" },
                EstimatedPomodoros = 2,
                CompletedPomodoros = 2,
                IsCompleted = true,
                CreatedAtUtc = PomodoroEngine.FormatUtc(DateTime.UtcNow.AddDays(-6)),
                CompletedAtUtc = PomodoroEngine.FormatUtc(DateTime.UtcNow.AddDays(-1))
            };
            var t6 = new TaskItem
            {
                Id = "demo_task_06",
                Title = "完成设计系统色彩对比度校验",
                Notes = "检查深邃夜空、晨雾浅色、暖阳日落、静谧森林四套主题",
                Priority = (int)TaskPriority.Medium,
                Tags = new List<string> { "设计", "学习" },
                EstimatedPomodoros = 1,
                CompletedPomodoros = 1,
                IsCompleted = true,
                CreatedAtUtc = PomodoroEngine.FormatUtc(DateTime.UtcNow.AddDays(-7)),
                CompletedAtUtc = PomodoroEngine.FormatUtc(DateTime.UtcNow.AddDays(-2))
            };

            settings.ActiveTaskId = t1.Id;
            var tasks = new List<TaskItem> { t1, t2, t3, t4, t5, t6 };

            // Generate 65 deterministic sessions over the last 28 days
            var sessions = new List<FocusSession>();
            DateTime today = localNow.Date;

            // Deterministic per-day session counts for past 27 days (summing to 61) + 4 today = 65 total
            int[] pastCounts = new int[]
            {
                2, 3, 1, 4, 2, 0, 2,
                3, 4, 2, 3, 1, 2, 0,
                3, 5, 2, 4, 3, 1, 2,
                2, 3, 4, 2, 3, 2
            };
            // Verify sum: 2+3+1+4+2+0+2=14; 3+4+2+3+1+2+0=15; 3+5+2+4+3+1+2=20; 2+3+4+2+3+2=16 => 14+15+20+16 = 65? Wait: 14+15=29, +20=49, +16=65!
            // Adjust so past 27 days = 61 and today = 4 => total 65!
            pastCounts[2] = 0; // -1 -> 64
            pastCounts[11] = 0; // -1 -> 63
            pastCounts[19] = 0; // -1 -> 62
            pastCounts[21] = 1; // -1 -> 61

            int[] startHours = new int[] { 9, 10, 14, 15, 16, 20 };
            int seq = 0;

            for (int dayOffset = 27; dayOffset >= 1; dayOffset--)
            {
                DateTime day = today.AddDays(-dayOffset);
                int count = pastCounts[27 - dayOffset];
                for (int k = 0; k < count; k++)
                {
                    int hour = startHours[(k + dayOffset) % startHours.Length];
                    int min = ((k * 17 + dayOffset * 7) % 25);
                    DateTime startLocal = new DateTime(day.Year, day.Month, day.Day, hour, min, 0, DateTimeKind.Local);
                    DateTime endLocal = startLocal.AddMinutes(25);
                    TaskItem task = tasks[seq % tasks.Count];
                    bool isInterrupted = (seq % 13 == 11);

                    sessions.Add(BuildDemoSession(
                        startLocal,
                        isInterrupted ? startLocal.AddMinutes(12) : endLocal,
                        day,
                        25 * 60,
                        isInterrupted ? 12 * 60 : 25 * 60,
                        isInterrupted ? SessionOutcome.Interrupted : SessionOutcome.Completed,
                        task,
                        tz));
                    seq++;
                }
            }

            // Today: 4 realistic sessions before current time (or morning/early afternoon)
            int baseHour = localNow.Hour >= 16 ? 9 : Math.Max(8, localNow.Hour - 4);
            var todayTasks = new TaskItem[] { t1, t2, t1, t4 };
            int[] todayHourOffsets = new int[] { 0, 1, 2, 3 };
            for (int i = 0; i < 4; i++)
            {
                int h = (baseHour + todayHourOffsets[i]) % 23;
                DateTime startLocal = new DateTime(today.Year, today.Month, today.Day, h, 10 + i * 5, 0, DateTimeKind.Local);
                DateTime endLocal = startLocal.AddMinutes(25);
                sessions.Add(BuildDemoSession(
                    startLocal,
                    endLocal,
                    today,
                    25 * 60,
                    25 * 60,
                    SessionOutcome.Completed,
                    todayTasks[i],
                    tz));
            }

            // Sort newest first for convenience
            sessions.Sort(delegate(FocusSession a, FocusSession b)
            {
                return string.CompareOrdinal(b.StartedAtUtc, a.StartedAtUtc);
            });

            storage.SaveSettings(settings, true);
            storage.SaveTasks(tasks, true);
            storage.SaveSessions(sessions, true);
        }

        private static FocusSession BuildDemoSession(
            DateTime startLocal,
            DateTime endLocal,
            DateTime logicalDate,
            int plannedSec,
            int effectiveSec,
            SessionOutcome outcome,
            TaskItem task,
            TimeZoneInfo tz)
        {
            DateTime startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified), tz);
            DateTime endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endLocal, DateTimeKind.Unspecified), tz);

            return new FocusSession
            {
                Id = Guid.NewGuid().ToString("N"),
                Mode = "Pomodoro",
                Phase = "Focus",
                Outcome = outcome.ToString(),
                StartedAtUtc = PomodoroEngine.FormatUtc(startUtc),
                EndedAtUtc = PomodoroEngine.FormatUtc(endUtc),
                LocalDate = logicalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                PlannedSeconds = plannedSec,
                EffectiveSeconds = effectiveSec,
                TaskId = task != null ? task.Id : null,
                TaskTitleSnapshot = task != null ? task.Title : null,
                TagsSnapshot = (task != null && task.Tags != null) ? new List<string>(task.Tags) : new List<string>()
            };
        }
    }
}
