using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Media;

namespace PomodoroFocus.UITests
{
    internal static class Program
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        private static readonly int[] RequiredFluentCodePoints = new int[]
        {
            0xF5B0, 0xF8AE, 0xE777, 0xE893, 0xE916, 0xE917,
            0xE9D5, 0xE9D2, 0xE7F6, 0xE713, 0xE710, 0xE74D,
            0xE70F, 0xE721, 0xEA3A, 0xEC61, 0xE73E, 0xE711,
            0xE921, 0xE922, 0xE923, 0xE8BB, 0xE718, 0xE77A,
            0xEE49, 0xEE47, 0xE8EC, 0xE7C1, 0xE8CB, 0xE787,
            0xE767, 0xE74F, 0xEA8F, 0xE790, 0xE765, 0xE7E8,
            0xE838, 0xEDE1, 0xE8B5, 0xE946, 0xE7BA, 0xE70D,
            0xE76C, 0xE712
        };

        private static readonly string[] ExpectedScreenshots = new string[]
        {
            "01_focus_midnight_running.png",
            "02_focus_shortbreak.png",
            "03_focus_longbreak.png",
            "04_tasks_view.png",
            "05_task_edit_dialog.png",
            "06_stats_view.png",
            "07_ambient_audio_view.png",
            "08_settings_mist_light.png",
            "09_focus_sunset_compact.png",
            "10_mini_floating_window.png"
        };

        [STAThread]
        private static int Main(string[] args)
        {
            string outRoot = Path.Combine(Path.GetTempPath(), "PomodoroFocus.UITests");
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    outRoot = args[++i];
                }
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string runDir = Path.Combine(outRoot, stamp);
            string dataDir = Path.Combine(runDir, "data");
            string shotsDir = Path.Combine(runDir, "shots");

            string exePath = FindAppExe();
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                Console.Error.WriteLine("[FAIL] Cannot locate PomodoroFocus.exe");
                return 2;
            }

            Console.WriteLine("App Exe: " + exePath);
            Console.WriteLine("Shots Dir: " + shotsDir);

            try
            {
                VerifyFluentIconsGlyphs();
                Console.WriteLine("[PASS] FluentIconsGlyphCheck (" + RequiredFluentCodePoints.Length + " glyphs verified)");

                RunUiaInteractionTest(exePath, dataDir);
                Console.WriteLine("[PASS] UiaEndToEndInteractionTest");

                RunAndVerifyScreenshotSuite(exePath, dataDir, shotsDir);
                Console.WriteLine("[PASS] ScreenshotSuiteVerification (10/10 PNGs verified)");

                Console.WriteLine("UI TESTS OK");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] " + ex.Message);
                return 1;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(dataDir))
                    {
                        Directory.Delete(dataDir, true);
                    }
                }
                catch
                {
                    // Ignore temp cleanup error
                }
            }
        }

        private static void VerifyFluentIconsGlyphs()
        {
            var ff = new System.Windows.Media.FontFamily("Segoe Fluent Icons");
            GlyphTypeface glyphTypeface = null;
            foreach (Typeface tf in ff.GetTypefaces())
            {
                if (tf.TryGetGlyphTypeface(out glyphTypeface) && glyphTypeface != null)
                {
                    break;
                }
            }
            if (glyphTypeface == null)
            {
                throw new Exception("Segoe Fluent Icons font not available on host.");
            }

            var missing = new List<string>();
            for (int i = 0; i < RequiredFluentCodePoints.Length; i++)
            {
                int cp = RequiredFluentCodePoints[i];
                ushort glyphIdx;
                if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(cp, out glyphIdx))
                {
                    missing.Add(string.Format("U+{0:X4}", cp));
                }
            }
            if (missing.Count > 0)
            {
                throw new Exception("Missing Segoe Fluent Icons glyphs: " + string.Join(", ", missing.ToArray()));
            }
        }

        private static void RunUiaInteractionTest(string exePath, string dataDir)
        {
            Directory.CreateDirectory(dataDir);
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = string.Format("--data-dir \"{0}\" --seed-demo --no-tray --no-hotkeys --no-sound", dataDir),
                UseShellExecute = false
            };

            Process proc = Process.Start(psi);
            if (proc == null) throw new Exception("Failed to start PomodoroFocus.exe");

            try
            {
                AutomationElement mainWin = WaitForWindowByProcessId(proc.Id, 12000);
                if (mainWin == null) throw new Exception("MainWindow did not appear within timeout.");

                // 1. Timer toggle / +5 / reset
                AutomationElement timerText = WaitForAutomationId(mainWin, "TimerDisplayText", 5000);
                AssertNotNull(timerText, "TimerDisplayText");
                string initialTime = timerText.Current.Name;
                Console.WriteLine("  Initial TimerText: " + initialTime);

                InvokeById(mainWin, "TimerAddFiveButton");
                Thread.Sleep(250);
                string afterAddFive = timerText.Current.Name;
                if (afterAddFive == initialTime)
                {
                    throw new Exception("TimerAddFiveButton did not change timer display (still " + afterAddFive + ").");
                }
                Console.WriteLine("  After +5m: " + afterAddFive);

                InvokeById(mainWin, "TimerToggleButton");
                Thread.Sleep(400);
                InvokeById(mainWin, "TimerToggleButton");
                Thread.Sleep(250);
                InvokeById(mainWin, "TimerResetButton");
                Thread.Sleep(250);

                // 2. Navigate Tasks & quick-add task
                InvokeById(mainWin, "NavTasksButton");
                Thread.Sleep(300);

                AutomationElement quickInput = WaitForAutomationId(mainWin, "QuickAddTaskInput", 5000);
                AssertNotNull(quickInput, "QuickAddTaskInput");
                var vp = (ValuePattern)quickInput.GetCurrentPattern(ValuePattern.Pattern);
                vp.SetValue("UIA自动化测试任务 #验收 !高 ~2");
                Thread.Sleep(150);
                InvokeById(mainWin, "QuickAddTaskSubmit");
                Thread.Sleep(350);

                AutomationElement createdText = WaitForTextByName(mainWin, "UIA自动化测试任务", 4000);
                AssertNotNull(createdText, "Created task text in list");
                Console.WriteLine("  Quick-added task verified in UIA tree.");

                // 3. Navigate Stats
                InvokeById(mainWin, "NavStatsButton");
                Thread.Sleep(350);

                // 4. Navigate Ambient Audio & toggle rain card
                InvokeById(mainWin, "NavAudioButton");
                Thread.Sleep(350);
                InvokeById(mainWin, "SoundCard_rain");
                Thread.Sleep(250);

                // 5. Navigate Settings & switch theme mist -> midnight
                InvokeById(mainWin, "NavSettingsButton");
                Thread.Sleep(350);
                InvokeById(mainWin, "ThemeCard_mist");
                Thread.Sleep(350);
                InvokeById(mainWin, "ThemeCard_midnight");
                Thread.Sleep(350);

                // 6. Navigate Focus, open Mini window, verify MiniTimerText & restore
                InvokeById(mainWin, "NavFocusButton");
                Thread.Sleep(250);
                InvokeById(mainWin, "EnterMiniModeButton");
                Thread.Sleep(450);

                AutomationElement miniWin = WaitForWindowByTitleContains(proc.Id, "迷你窗", 5000);
                AssertNotNull(miniWin, "MiniTimerWindow");
                AutomationElement miniTimer = WaitForAutomationId(miniWin, "MiniTimerText", 3000);
                AssertNotNull(miniTimer, "MiniTimerText");
                Console.WriteLine("  MiniTimerWindow verified (" + miniTimer.Current.Name + ").");

                InvokeById(miniWin, "MiniRestoreButton");
                Thread.Sleep(350);
            }
            finally
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.CloseMainWindow();
                        if (!proc.WaitForExit(3000))
                        {
                            proc.Kill();
                        }
                    }
                }
                catch
                {
                    // Ignore process cleanup error
                }
                proc.Dispose();
            }
        }

        private static void RunAndVerifyScreenshotSuite(string exePath, string dataDir, string shotsDir)
        {
            Directory.CreateDirectory(shotsDir);
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = string.Format("--data-dir \"{0}\" --seed-demo --screenshot-suite \"{1}\"", dataDir, shotsDir),
                UseShellExecute = false
            };

            using (Process proc = Process.Start(psi))
            {
                if (proc == null) throw new Exception("Failed to launch screenshot suite.");
                if (!proc.WaitForExit(25000))
                {
                    proc.Kill();
                    throw new Exception("Screenshot suite timed out after 25s.");
                }
                if (proc.ExitCode != 0)
                {
                    throw new Exception("Screenshot suite exited with code " + proc.ExitCode);
                }
            }

            for (int i = 0; i < ExpectedScreenshots.Length; i++)
            {
                string file = Path.Combine(shotsDir, ExpectedScreenshots[i]);
                if (!File.Exists(file))
                {
                    throw new Exception("Missing expected screenshot: " + ExpectedScreenshots[i]);
                }
                var info = new FileInfo(file);
                if (info.Length < 4096)
                {
                    throw new Exception("Screenshot too small (possibly blank): " + ExpectedScreenshots[i] + " (" + info.Length + " bytes)");
                }
                using (var bmp = new Bitmap(file))
                {
                    if (bmp.Width < 300 || bmp.Height < 100)
                    {
                        throw new Exception("Unexpected screenshot dimensions: " + ExpectedScreenshots[i] + " " + bmp.Width + "x" + bmp.Height);
                    }
                    // Verify image is not a single solid color
                    System.Drawing.Color first = bmp.GetPixel(10, 10);
                    bool hasVariance = false;
                    int stepX = Math.Max(1, bmp.Width / 24);
                    int stepY = Math.Max(1, bmp.Height / 16);
                    for (int y = 5; y < bmp.Height - 5 && !hasVariance; y += stepY)
                    {
                        for (int x = 5; x < bmp.Width - 5; x += stepX)
                        {
                            System.Drawing.Color c = bmp.GetPixel(x, y);
                            if (Math.Abs(c.R - first.R) + Math.Abs(c.G - first.G) + Math.Abs(c.B - first.B) > 25)
                            {
                                hasVariance = true;
                                break;
                            }
                        }
                    }
                    if (!hasVariance)
                    {
                        throw new Exception("Screenshot appears completely flat/blank: " + ExpectedScreenshots[i]);
                    }
                }
            }
        }

        private static string FindAppExe()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string config = baseDir.IndexOf("Debug", StringComparison.OrdinalIgnoreCase) >= 0 ? "Debug" : "Release";
            DirectoryInfo cur = new DirectoryInfo(baseDir);
            while (cur != null)
            {
                string candidate = Path.Combine(cur.FullName, "src", "PomodoroFocus.App", "bin", config, "PomodoroFocus.exe");
                if (File.Exists(candidate)) return candidate;
                string alt = Path.Combine(cur.FullName, "src", "PomodoroFocus.App", "bin", "Release", "PomodoroFocus.exe");
                if (File.Exists(alt)) return alt;
                cur = cur.Parent;
            }
            return null;
        }

        private static AutomationElement WaitForWindowByProcessId(int pid, int timeoutMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                IntPtr found = FindVisibleWindowHandle(pid, null);
                if (found != IntPtr.Zero)
                {
                    try
                    {
                        AutomationElement el = AutomationElement.FromHandle(found);
                        if (el != null) return el;
                    }
                    catch
                    {
                    }
                }
                Thread.Sleep(150);
            }
            return null;
        }

        private static AutomationElement WaitForWindowByTitleContains(int pid, string substr, int timeoutMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                IntPtr found = FindVisibleWindowHandle(pid, substr);
                if (found != IntPtr.Zero)
                {
                    try
                    {
                        AutomationElement el = AutomationElement.FromHandle(found);
                        if (el != null) return el;
                    }
                    catch
                    {
                    }
                }
                Thread.Sleep(150);
            }
            return null;
        }

        private static IntPtr FindVisibleWindowHandle(int pid, string titleContains)
        {
            IntPtr result = IntPtr.Zero;
            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (!IsWindowVisible(hWnd)) return true;
                uint winPid;
                GetWindowThreadProcessId(hWnd, out winPid);
                if ((int)winPid != pid) return true;
                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();
                if (string.IsNullOrEmpty(title)) return true;
                if (string.IsNullOrEmpty(titleContains) || title.IndexOf(titleContains, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return result;
        }

        private static AutomationElement WaitForAutomationId(AutomationElement root, string automationId, int timeoutMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            var cond = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                AutomationElement el = root.FindFirst(TreeScope.Descendants, cond);
                if (el != null && !el.Current.IsOffscreen) return el;
                Thread.Sleep(120);
            }
            return root.FindFirst(TreeScope.Descendants, cond);
        }

        private static AutomationElement WaitForTextByName(AutomationElement root, string name, int timeoutMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            var cond = new PropertyCondition(AutomationElement.NameProperty, name);
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                AutomationElement el = root.FindFirst(TreeScope.Descendants, cond);
                if (el != null) return el;
                Thread.Sleep(120);
            }
            return null;
        }

        private static void InvokeById(AutomationElement root, string automationId)
        {
            AutomationElement el = WaitForAutomationId(root, automationId, 5000);
            AssertNotNull(el, automationId);
            var ip = (InvokePattern)el.GetCurrentPattern(InvokePattern.Pattern);
            ip.Invoke();
        }

        private static void AssertNotNull(object obj, string name)
        {
            if (obj == null) throw new Exception("Expected UI element not found: " + name);
        }
    }
}
