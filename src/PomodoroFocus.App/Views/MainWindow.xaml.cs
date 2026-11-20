using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PomodoroFocus.App.Interop;
using PomodoroFocus.App.Themes;
using PomodoroFocus.App.ViewModels;

namespace PomodoroFocus.App.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private bool _forceExit;

        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;

            RestoreWindowBounds();

            SourceInitialized += OnWindowSourceInitialized;
            SizeChanged += OnWindowSizeChanged;
            StateChanged += OnWindowStateChanged;
            PreviewKeyDown += OnWindowPreviewKeyDown;
            Closing += OnWindowClosing;

            _vm.PropertyChanged += OnVmPropertyChanged;
            _vm.FocusQuickAddRequested += delegate { TasksViewControl.FocusQuickAddBox(); };
            _vm.FocusTaskSearchRequested += delegate { TasksViewControl.FocusSearchBox(); };
            _vm.ExitApplicationRequested += delegate
            {
                _forceExit = true;
                Close();
            };
        }

        public void ForceExitClose()
        {
            _forceExit = true;
            Close();
        }

        public Image CrossfadeImage
        {
            get { return ThemeCrossfadeImage; }
        }

        private void OnWindowSourceInitialized(object sender, EventArgs e)
        {
            _vm.AttachMainWindow(this, ThemeCrossfadeImage);
            UpdateCompactState();
            UpdateMaximizedChromeMargin();
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCompactState();
        }

        private void UpdateCompactState()
        {
            _vm.IsCompactLayout = ActualWidth < 980.0;
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            UpdateMaximizedChromeMargin();
        }

        private void UpdateMaximizedChromeMargin()
        {
            if (WindowState == WindowState.Maximized)
            {
                RootHost.Margin = new Thickness(7);
                MaxRestoreButton.Content = TryFindResource("Icon.CaptionRestore");
                MaxRestoreButton.ToolTip = "还原";
            }
            else
            {
                RootHost.Margin = new Thickness(0);
                MaxRestoreButton.Content = TryFindResource("Icon.CaptionMaximize");
                MaxRestoreButton.ToolTip = "最大化";
            }
        }

        private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ActiveViewKey")
            {
                PlayViewTransitionAnimation();
            }
        }

        private void PlayViewTransitionAnimation()
        {
            TimeSpan dur = AnimationHelper.Duration(MotionToken.Normal);
            if (dur == TimeSpan.Zero)
            {
                ViewStageHost.Opacity = 1.0;
                ViewStageTransform.Y = 0;
                return;
            }
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var fade = new DoubleAnimation(0.0, 1.0, new Duration(dur)) { EasingFunction = ease };
            var slide = new DoubleAnimation(8.0, 0.0, new Duration(dur)) { EasingFunction = ease };
            ViewStageHost.BeginAnimation(OpacityProperty, fade);
            ViewStageTransform.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_vm.HasAnyModalOpen)
                {
                    _vm.CloseAllModals();
                    e.Handled = true;
                    return;
                }
                if (_vm.FocusVM.IsCustomDurationPopupOpen)
                {
                    _vm.FocusVM.IsCustomDurationPopupOpen = false;
                    e.Handled = true;
                    return;
                }
                if (_vm.FocusVM.IsTaskPopupOpen)
                {
                    _vm.FocusVM.IsTaskPopupOpen = false;
                    e.Handled = true;
                    return;
                }
            }

            if (_vm.HasAnyModalOpen) return;

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (ctrl)
            {
                if (e.Key == Key.D1) { _vm.NavigateTo("focus"); e.Handled = true; return; }
                if (e.Key == Key.D2) { _vm.NavigateTo("tasks"); e.Handled = true; return; }
                if (e.Key == Key.D3) { _vm.NavigateTo("stats"); e.Handled = true; return; }
                if (e.Key == Key.D4) { _vm.NavigateTo("audio"); e.Handled = true; return; }
                if (e.Key == Key.D5) { _vm.NavigateTo("settings"); e.Handled = true; return; }
                if (e.Key == Key.N) { _vm.TriggerNewTaskShortcut(); e.Handled = true; return; }
                if (e.Key == Key.F) { _vm.TriggerSearchTaskShortcut(); e.Handled = true; return; }
                return;
            }

            // Do not hijack single-key shortcuts when typing in TextBox or HotkeyRecorderBox
            if (Keyboard.FocusedElement is TextBoxBase || Keyboard.FocusedElement is Controls.HotkeyRecorderBox)
            {
                return;
            }

            if (e.Key == Key.Space) { _vm.ToggleTimer(); e.Handled = true; }
            else if (e.Key == Key.R) { _vm.RequestResetTimer(); e.Handled = true; }
            else if (e.Key == Key.S) { _vm.SkipTimer(); e.Handled = true; }
            else if (e.Key == Key.OemPlus || e.Key == Key.Add) { _vm.AddFiveMinutes(); e.Handled = true; }
            else if (e.Key == Key.M) { _vm.ToggleMiniWindow(); e.Handled = true; }
        }

        private void RestoreWindowBounds()
        {
            double w = _vm.Settings.MainWindowWidth;
            double h = _vm.Settings.MainWindowHeight;
            if (w >= 860 && w <= 3840) Width = w;
            if (h >= 600 && h <= 2160) Height = h;

            double l = _vm.Settings.MainWindowLeft;
            double t = _vm.Settings.MainWindowTop;
            Rect vScreen = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

            if (!double.IsNaN(l) && !double.IsNaN(t) &&
                l + 100 > vScreen.Left && l < vScreen.Right - 100 &&
                t + 60 > vScreen.Top && t < vScreen.Bottom - 60)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = l;
                Top = t;
            }

            if (_vm.Settings.MainWindowMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void SaveWindowBounds()
        {
            _vm.Settings.MainWindowMaximized = (WindowState == WindowState.Maximized);
            Rect bounds = (WindowState == WindowState.Normal)
                ? new Rect(Left, Top, Width, Height)
                : RestoreBounds;

            if (bounds.Width >= 860 && bounds.Height >= 600)
            {
                _vm.Settings.MainWindowLeft = bounds.Left;
                _vm.Settings.MainWindowTop = bounds.Top;
                _vm.Settings.MainWindowWidth = bounds.Width;
                _vm.Settings.MainWindowHeight = bounds.Height;
            }
            _vm.SaveSettingsDebounced();
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaxRestoreClick(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            SaveWindowBounds();
            if (!_forceExit && _vm.Settings.ShowTrayIcon && _vm.Settings.MinimizeToTrayOnClose && _vm.TrayService.IsVisible)
            {
                e.Cancel = true;
                Hide();
                if (!_vm.Settings.TrayFirstHideTipShown)
                {
                    _vm.Settings.TrayFirstHideTipShown = true;
                    _vm.SaveSettingsDebounced();
                    _vm.TrayService.ShowBalloon("番茄专注", "番茄专注仍在后台运行，可从托盘图标恢复");
                }
                return;
            }
        }

        private void OnOverlayBackdropClick(object sender, MouseButtonEventArgs e)
        {
            _vm.CloseAllModals();
        }

        private void OnModalCardMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void OnModalTagInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _vm.AddDraftTag();
                e.Handled = true;
            }
        }
    }
}
