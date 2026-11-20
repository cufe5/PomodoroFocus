using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PomodoroFocus.App.ViewModels;

namespace PomodoroFocus.App.Views
{
    public partial class MiniTimerWindow : Window
    {
        private readonly MainViewModel _main;

        public MiniTimerWindow(MainViewModel main)
        {
            InitializeComponent();
            _main = main;
            DataContext = main;
            _main.FocusVM.PropertyChanged += OnFocusVmChanged;
            UpdateHeaderText();
            RestoreSavedPosition();
        }

        public void ApplyOpacitySetting(double opacity)
        {
            if (opacity < 0.70) opacity = 0.70;
            if (opacity > 1.00) opacity = 1.00;
            RootCard.Opacity = opacity;
        }

        protected override void OnClosed(EventArgs e)
        {
            _main.FocusVM.PropertyChanged -= OnFocusVmChanged;
            SaveCurrentPosition();
            base.OnClosed(e);
        }

        private void OnFocusVmChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "PhaseBadgeText" || e.PropertyName == "ActiveTaskTitle" || e.PropertyName == "HasActiveTask")
            {
                UpdateHeaderText();
            }
        }

        private void UpdateHeaderText()
        {
            string phase = _main.FocusVM.PhaseBadgeText;
            if (_main.FocusVM.HasActiveTask)
            {
                MiniHeaderText.Text = string.Format("{0} · {1}", phase, _main.FocusVM.ActiveTaskTitle);
            }
            else
            {
                MiniHeaderText.Text = phase;
            }
        }

        private void OnSurfaceMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                _main.CloseMiniWindow(true);
                e.Handled = true;
                return;
            }
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                    SnapToWorkAreaEdges();
                    SaveCurrentPosition();
                }
                catch
                {
                    // Ignore DragMove errors
                }
            }
        }

        private void SnapToWorkAreaEdges()
        {
            Rect wa = SystemParameters.WorkArea;
            const double snap = 12.0;

            if (Math.Abs(Left - wa.Left) <= snap) Left = wa.Left;
            else if (Math.Abs((Left + Width) - wa.Right) <= snap) Left = wa.Right - Width;

            if (Math.Abs(Top - wa.Top) <= snap) Top = wa.Top;
            else if (Math.Abs((Top + Height) - wa.Bottom) <= snap) Top = wa.Bottom - Height;
        }

        private void RestoreSavedPosition()
        {
            Rect wa = SystemParameters.WorkArea;
            double left = _main.Settings.MiniWindowLeft;
            double top = _main.Settings.MiniWindowTop;

            if (double.IsNaN(left) || double.IsNaN(top) ||
                left + Width < wa.Left + 20 || left > wa.Right - 20 ||
                top + Height < wa.Top + 20 || top > wa.Bottom - 20)
            {
                Left = wa.Right - Width - 24;
                Top = wa.Bottom - Height - 24;
            }
            else
            {
                Left = left;
                Top = top;
            }
        }

        private void SaveCurrentPosition()
        {
            _main.Settings.MiniWindowLeft = Left;
            _main.Settings.MiniWindowTop = Top;
            _main.SaveSettingsDebounced();
        }

        private void OnRestoreMainClick(object sender, RoutedEventArgs e)
        {
            _main.CloseMiniWindow(true);
        }

        private void OnCloseMiniClick(object sender, RoutedEventArgs e)
        {
            _main.CloseMiniWindow(false);
        }

        private void OnOpacityMenuClick(object sender, RoutedEventArgs e)
        {
            var mi = sender as MenuItem;
            int pct;
            if (mi != null && mi.Tag != null && int.TryParse(mi.Tag.ToString(), out pct))
            {
                _main.SettingsVM.MiniOpacityPercent = pct;
            }
        }
    }
}
