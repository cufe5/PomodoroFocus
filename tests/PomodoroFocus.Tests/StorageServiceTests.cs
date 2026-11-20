using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PomodoroFocus.Core.Models;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.Tests
{
    public sealed class StorageServiceTests
    {
        public void AtomicWriteBakRecoveryAndBackupRoundTrip()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "PomodoroFocus.Tests", Guid.NewGuid().ToString("N"));
            try
            {
                using (var storage = new StorageService(tempDir))
                {
                    AssertEx.IsFalse(storage.IsReadOnlyMode, "Temp dir must be writable");

                    // Write v1 then v2 so .bak holds v1
                    var s1 = new AppSettings { FocusMinutes = 30, Theme = "forest" };
                    storage.SaveSettings(s1, true);

                    var s2 = new AppSettings { FocusMinutes = 45, Theme = "sunset" };
                    storage.SaveSettings(s2, true);

                    string settingsPath = Path.Combine(tempDir, "settings.json");
                    string bakPath = settingsPath + ".bak";
                    AssertEx.IsTrue(File.Exists(settingsPath), "settings.json exists");
                    AssertEx.IsTrue(File.Exists(bakPath), "settings.json.bak exists");

                    // Corrupt main file -> should recover from .bak (FocusMinutes = 30)
                    File.WriteAllText(settingsPath, "{ BROKEN JSON !!!", Encoding.UTF8);
                }

                using (var storage2 = new StorageService(tempDir))
                {
                    AppSettings recovered = storage2.LoadSettings();
                    AssertEx.Equal(30, recovered.FocusMinutes, "Recovered from .bak");
                    AssertEx.Equal("forest", recovered.Theme, "Recovered theme from .bak");
                    AssertEx.NotNull(storage2.RecoveryNotice, "RecoveryNotice populated");

                    // Demo seeder + backup round-trip
                    DemoDataSeeder.Seed(storage2, DateTime.Now, TimeZoneInfo.Local);
                    List<TaskItem> tasks = storage2.LoadTasks();
                    List<FocusSession> sessions = storage2.LoadSessions();
                    AssertEx.Equal(6, tasks.Count, "Demo seeded 6 tasks");
                    AssertEx.Equal(65, sessions.Count, "Demo seeded 65 sessions");

                    string backupFile = Path.Combine(tempDir, "backup.json");
                    var bundle = new BackupBundle
                    {
                        Settings = storage2.LoadSettings(),
                        Tasks = tasks,
                        Sessions = sessions
                    };
                    storage2.ExportBackupToFile(backupFile, bundle);
                    BackupBundle imported = storage2.ImportBackupFromFile(backupFile);
                    AssertEx.Equal(1, imported.SchemaVersion, "SchemaVersion 1");
                    AssertEx.Equal(6, imported.Tasks.Count, "Imported 6 tasks");
                    AssertEx.Equal(65, imported.Sessions.Count, "Imported 65 sessions");
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
