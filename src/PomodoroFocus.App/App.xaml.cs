using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PomodoroFocus.App.Themes;
using PomodoroFocus.App.ViewModels;
using PomodoroFocus.App.Views;
using PomodoroFocus.Core.Models;

namespace PomodoroFocus.App
{
    public partial class App : Application
    {
        private Mutex _instanceMutex;
        private EventWaitHandle _activateEvent;
        private Thread _activateListenerThread;
        private volatile bool _stopActivateListener;

        private MainViewModel _mainVm;
        private MainWindow _mainWindow;

        private sealed class CliOptions
        {
            public string DataDir;
            public bool SeedDemo;
            public string Theme;
            public string View;
            public string TimerState;
            public bool Mini;
            public string Dialog;
            public bool NoTray;
            public bool NoHotkeys;
            public bool NoSound;
            public bool Minimized;
            public string ScreenshotPath;
            public string ScreenshotSuiteDir;
        }

        private string _resolvedDataDirForCrash;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            CliOptions opts = ParseArgs(e.Args);

            bool isScreenshotMode = !string.IsNullOrEmpty(opts.ScreenshotPath) || !string.IsNullOrEmpty(opts.ScreenshotSuiteDir);
            if (isScreenshotMode)
            {
                opts.NoTray = true;
                opts.NoHotkeys = true;
                opts.NoSound = true;
            }

            string resolvedDataDir = string.IsNullOrWhiteSpace(opts.DataDir)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PomodoroFocus")
                : opts.DataDir;
            _resolvedDataDirForCrash = resolvedDataDir;

            if (!isScreenshotMode)
            {
                if (!AcquireSingleInstance(resolvedDataDir))
                {
                    Shutdown(0);
                    return;
                }
            }

            _mainVm = new MainViewModel(
                resolvedDataDir,
                opts.SeedDemo,
                opts.NoTray,
                opts.NoHotkeys,
                opts.NoSound,
                opts.Theme);

            ApplyTimerStateCli(opts.TimerState);
            if (!string.IsNullOrEmpty(opts.View))
            {
                _mainVm.NavigateTo(opts.View.ToLowerInvariant());
            }

            _mainWindow = new MainWindow(_mainVm);
            MainWindow = _mainWindow;
            _mainWindow.Closed += delegate
            {
                Shutdown(0);
            };

            if (!string.IsNullOrEmpty(opts.ScreenshotSuiteDir))
            {
                RunScreenshotSuite(opts.ScreenshotSuiteDir);
                return;
            }

            if (!string.IsNullOrEmpty(opts.ScreenshotPath))
            {
                RunSingleScreenshot(opts);
                return;
            }

            if (opts.Minimized && _mainVm.Settings.ShowTrayIcon && !opts.NoTray)
            {
                // Initialize handle/tray without showing window
                _mainWindow.Show();
                _mainWindow.Hide();
            }
            else
            {
                _mainWindow.Show();
            }

            if (opts.Mini)
            {
                _mainVm.OpenMiniWindow(_mainVm.Settings.HideMainOnMini);
            }

            if (string.Equals(opts.Dialog, "new-task", StringComparison.OrdinalIgnoreCase))
            {
                _mainVm.OpenNewTaskDialog();
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WriteCrashLog(e.Exception);
            e.Handled = true;
            Environment.Exit(99);
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteCrashLog(e.ExceptionObject as Exception);
        }

        private void WriteCrashLog(Exception ex)
        {
            string msg = ex != null ? ex.ToString() : "Unknown unhandled exception";
            try
            {
                Console.Error.WriteLine("FATAL: " + msg);
                if (!string.IsNullOrEmpty(_resolvedDataDirForCrash))
                {
                    if (!Directory.Exists(_resolvedDataDirForCrash)) Directory.CreateDirectory(_resolvedDataDirForCrash);
                    File.WriteAllText(Path.Combine(_resolvedDataDirForCrash, "crash.log"), msg, Encoding.UTF8);
                }
            }
            catch
            {
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _stopActivateListener = true;
            if (_activateEvent != null)
            {
                try { _activateEvent.Set(); } catch { }
                _activateEvent.Dispose();
                _activateEvent = null;
            }
            if (_mainVm != null)
            {
                _mainVm.Dispose();
                _mainVm = null;
            }
            if (_instanceMutex != null)
            {
                try { _instanceMutex.ReleaseMutex(); } catch { }
                _instanceMutex.Dispose();
                _instanceMutex = null;
            }
            base.OnExit(e);
        }

        private bool AcquireSingleInstance(string dataDir)
        {
            string hash = ComputeSha1Hex12(dataDir.Trim().ToLowerInvariant());
            string mutexName = "Local\\PomodoroFocus_" + hash;
            string eventName = "Local\\PomodoroFocus_Wake_" + hash;

            bool createdNew;
            _instanceMutex = new Mutex(true, mutexName, out createdNew);
            if (!createdNew)
            {
                try
                {
                    using (EventWaitHandle ev = EventWaitHandle.OpenExisting(eventName))
                    {
                        ev.Set();
                    }
                }
                catch
                {
                    // Ignore wakeup errors
                }
                return false;
            }

            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            _activateListenerThread = new Thread(delegate()
            {
                while (!_stopActivateListener)
                {
                    if (_activateEvent.WaitOne(500) && !_stopActivateListener)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            if (_mainVm != null) _mainVm.ShowAndActivateMainWindow();
                        }));
                    }
                }
            });
            _activateListenerThread.IsBackground = true;
            _activateListenerThread.Start();
            return true;
        }

        private void ApplyTimerStateCli(string timerState)
        {
            if (string.IsNullOrEmpty(timerState)) return;
            switch (timerState.ToLowerInvariant())
            {
                case "idle":
                    _mainVm.Engine.SetDemoState(TimerMode.Pomodoro, TimerPhase.Focus, TimerRunState.Idle, _mainVm.Settings.FocusMinutes * 60, 1);
                    break;
                case "running":
                    _mainVm.Engine.SetDemoState(TimerMode.Pomodoro, TimerPhase.Focus, TimerRunState.Running, 18 * 60 + 42, 1);
                    break;
                case "paused":
                    _mainVm.Engine.SetDemoState(TimerMode.Pomodoro, TimerPhase.Focus, TimerRunState.Paused, 15 * 60, 1);
                    break;
                case "shortbreak":
                    _mainVm.Engine.SetDemoState(TimerMode.Pomodoro, TimerPhase.ShortBreak, TimerRunState.Running, 3 * 60 + 20, 2);
                    break;
                case "longbreak":
                    _mainVm.Engine.SetDemoState(TimerMode.Pomodoro, TimerPhase.LongBreak, TimerRunState.Running, 12 * 60 + 10, 4);
                    break;
                case "stopwatch":
                    _mainVm.Engine.SetDemoState(TimerMode.Stopwatch, TimerPhase.Focus, TimerRunState.Running, 14 * 60 + 28, 0);
                    break;
            }
            _mainVm.ThemeService.AnimatePhaseBrush(_mainVm.Engine.Snapshot.Phase, true);
            _mainVm.AudioVM.SyncMixerGateAndStatus();
        }

        private void RunSingleScreenshot(CliOptions opts)
        {
            AnimationHelper.ReduceMotionSetting = true;
            _mainWindow.Width = 1080;
            _mainWindow.Height = 720;
            _mainWindow.Show();

            MiniTimerWindow miniWin = null;
            if (opts.Mini)
            {
                miniWin = _mainVm.OpenMiniWindow(false);
            }
            if (string.Equals(opts.Dialog, "new-task", StringComparison.OrdinalIgnoreCase))
            {
                _mainVm.OpenNewTaskDialog();
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            timer.Tick += delegate
            {
                timer.Stop();
                Window target = (opts.Mini && miniWin != null) ? (Window)miniWin : _mainWindow;
                CaptureWindowToPng(target, opts.ScreenshotPath, 144.0);
                if (miniWin != null) miniWin.Close();
                _mainWindow.ForceExitClose();
            };
            timer.Start();
        }

        private void RunScreenshotSuite(string outDir)
        {
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }
            AnimationHelper.ReduceMotionSetting = true;

            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Width = 1080;
            _mainWindow.Height = 720;
            _mainWindow.Show();

            var steps = new Action[]
            {
                // 01: Midnight Focus Running 18:42
                delegate
                {
                    _mainVm.CloseAllModals();
                    _mainVm.SwitchTheme("midnight", true);
                    _mainVm.NavigateTo("focus");
                    _mainVm.Engine.SetDemoState(TimerMode.Pomodoro, TimerPhase.Focus, TimerRunState.Running, 18 * 60 + 42, 1);
                    _mainVm.ThemeService.AnimatePhaseBrush(TimerPhase.Focus, true);
                    _mainVm.AudioVM.SyncMixerGateAndStatus();
                    RenderFlushAndSave(_mainWindow, Path.Combine(outDir, "01_focus_midnight_running.png"));
                },
                // 02: Midnight ShortBreak 03:20 (mint)
                delegate
                {
                    _mainVm.Engine.SetDemoState(TimerMode.Pomodoro, TimerPhase.ShortBreak, TimerRunState.Running, 3 * 60 + 20, 2);
                    _mainVm.ThemeService.AnimatePhaseBrush(TimerPhase.ShortBreak, true);
                    _mainVm.AudioVM.SyncMixerGateAndStatus();
                    RenderFlushAndSave(_mainWindow, Path.Combine(outDir, "02_focus_shortbreak.png"));
                },
                // 03: Midnight LongBreak 12:10 (nebula purple)
                delegate
                {
                    _mainVm.Engine.SetDemoState(TimerMode.Pomodoro, TimerPhase.LongBreak, TimerRunState.Running, 12 * 60 + 10, 4);
                    _mainVm.ThemeService.AnimatePhaseBrush(TimerPhase.LongBreak, true);
                    _mainVm.AudioVM.SyncMixerGateAndStatus();
                    RenderFlushAndSave(_mainWindow, Path.Combine(outDir, "03_focus_longbreak.png"));
                },
                // 04: Tasks View (restore Focus running state for active pill)
                delegate
                {
                    _mainVm.Engine.SetDemoState(TimerMode.Pomodoro, TimerPhase.Focus, TimerRunState.Running, 18 * 60 + 42, 1);
                    _mainVm.ThemeService.AnimatePhaseBrush(TimerPhase.Focus, true);
                    _mainVm.AudioVM.SyncMixerGateAndStatus();
                    _mainVm.TasksVM.SelectedFilterKey = "all";
                    _mainVm.NavigateTo("tasks");
                    RenderFlushAndSave(_mainWindow, Path.Combine(outDir, "04_tasks_view.png"));
                },
                // 05: Task Edit Modal Open
                delegate
                {
                    _mainVm.OpenNewTaskDialog();
                    _mainVm.TaskDraftTitle = "优化音频合成器低通滤波器";
                    _mainVm.TaskDraftNotes = "校准 9 路环境音的立体声像与软削波阈值";
                    _mainVm.TaskDraftPriorityKey = "3";
                    _mainVm.TaskDraftEstimate = 3;
                    _mainVm.TaskDraftTags.Add("音频");
                    _mainVm.TaskDraftTags.Add("核心");
                    RenderFlushAndSave(_mainWindow, Path.Combine(outDir, "05_task_edit_dialog.png"));
                },
                // 06: Stats View (expand height temporarily so 4 KPI + Trend + Donut + 24h + Heatmap + History all fit cleanly)
                delegate
                {
                    _mainVm.CloseAllModals();
                    _mainWindow.Height = 1080;
                    _mainVm.NavigateTo("stats");
                    _mainVm.StatsVM.RefreshAll();
                    RenderFlushAndSave(_mainWindow, Path.Combine(outDir, "06_stats_view.png"));
                },
                // 07: Ambient Audio View (restore standard 720 height, Rainy Study active)
                delegate
                {
                    _mainWindow.Height = 720;
                    _mainVm.NavigateTo("audio");
                    _mainVm.AudioVM.ApplyPreset(_mainVm.AudioVM.Presets[0]);
                    RenderFlushAndSave(_mainWindow, Path.Combine(outDir, "07_ambient_audio_view.png"));
                },
                // 08: Settings View with Mist Light Theme
                delegate
                {
                    _mainVm.SwitchTheme("mist", true);
                    _mainVm.NavigateTo("settings");
                    RenderFlushAndSave(_mainWindow, Path.Combine(outDir, "08_settings_mist_light.png"));
                },
                // 09: Focus Sunset Theme at Compact 860x600
                delegate
                {
                    _mainVm.SwitchTheme("sunset", true);
                    _mainVm.NavigateTo("focus");
                    _mainWindow.Width = 860;
                    _mainWindow.Height = 600;
                    _mainVm.IsCompactLayout = true;
                    _mainVm.ThemeService.AnimatePhaseBrush(TimerPhase.Focus, true);
                    RenderFlushAndSave(_mainWindow, Path.Combine(outDir, "09_focus_sunset_compact.png"));
                },
                // 10: Mini Floating Window with Forest Theme (288x92)
                delegate
                {
                    _mainVm.SwitchTheme("forest", true);
                    _mainVm.ThemeService.AnimatePhaseBrush(TimerPhase.Focus, true);
                    MiniTimerWindow mini = _mainVm.OpenMiniWindow(false);
                    mini.ApplyOpacitySetting(1.0);
                    RenderFlushAndSave(mini, Path.Combine(outDir, "10_mini_floating_window.png"));
                    mini.Close();
                }
            };

            int stepIdx = 0;
            var stepTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            stepTimer.Tick += delegate
            {
                if (stepIdx < steps.Length)
                {
                    steps[stepIdx]();
                    stepIdx++;
                }
                else
                {
                    stepTimer.Stop();
                    _mainWindow.ForceExitClose();
                }
            };
            stepTimer.Start();
        }

        private static void RenderFlushAndSave(Window window, string outputPath)
        {
            window.UpdateLayout();
            DispatcherFrame frame = new DispatcherFrame();
            window.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate { frame.Continue = false; }));
            Dispatcher.PushFrame(frame);
            window.UpdateLayout();
            CaptureWindowToPng(window, outputPath, 144.0);
        }

        private static void CaptureWindowToPng(Window window, string outputPath, double dpi)
        {
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var content = window.Content as FrameworkElement;
            if (content == null) return;

            double width = content.ActualWidth > 0 ? content.ActualWidth : window.Width;
            double height = content.ActualHeight > 0 ? content.ActualHeight : window.Height;
            double scale = dpi / 96.0;
            int pxW = Math.Max(1, (int)Math.Round(width * scale));
            int pxH = Math.Max(1, (int)Math.Round(height * scale));

            var rtb = new RenderTargetBitmap(pxW, pxH, dpi, dpi, PixelFormats.Pbgra32);
            rtb.Render(content);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(fs);
            }
        }

        private static CliOptions ParseArgs(string[] args)
        {
            var opts = new CliOptions();
            if (args == null) return opts;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.IsNullOrEmpty(a)) continue;

                switch (a.ToLowerInvariant())
                {
                    case "--data-dir":
                        if (i + 1 < args.Length) opts.DataDir = args[++i];
                        break;
                    case "--seed-demo":
                        opts.SeedDemo = true;
                        break;
                    case "--theme":
                        if (i + 1 < args.Length) opts.Theme = args[++i];
                        break;
                    case "--view":
                        if (i + 1 < args.Length) opts.View = args[++i];
                        break;
                    case "--timer-state":
                        if (i + 1 < args.Length) opts.TimerState = args[++i];
                        break;
                    case "--mini":
                        opts.Mini = true;
                        break;
                    case "--dialog":
                        if (i + 1 < args.Length) opts.Dialog = args[++i];
                        break;
                    case "--no-tray":
                        opts.NoTray = true;
                        break;
                    case "--no-hotkeys":
                        opts.NoHotkeys = true;
                        break;
                    case "--no-sound":
                        opts.NoSound = true;
                        break;
                    case "--minimized":
                        opts.Minimized = true;
                        break;
                    case "--screenshot":
                        if (i + 1 < args.Length) opts.ScreenshotPath = args[++i];
                        break;
                    case "--screenshot-suite":
                        if (i + 1 < args.Length) opts.ScreenshotSuiteDir = args[++i];
                        break;
                }
            }

            return opts;
        }

        private static string ComputeSha1Hex12(string input)
        {
            using (var sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(12);
                for (int i = 0; i < 6; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
