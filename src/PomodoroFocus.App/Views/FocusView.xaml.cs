using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PomodoroFocus.App.Themes;
using PomodoroFocus.App.ViewModels;

namespace PomodoroFocus.App.Views
{
    public partial class FocusView : UserControl
    {
        private FocusViewModel _vm;

        public FocusView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            SizeChanged += OnViewSizeChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null && _vm.Main != null)
            {
                _vm.Main.PropertyChanged -= OnMainPropertyChanged;
                _vm.Main.CelebrationTriggered -= OnCelebrationTriggered;
            }
            _vm = DataContext as FocusViewModel;
            if (_vm != null && _vm.Main != null)
            {
                _vm.Main.PropertyChanged += OnMainPropertyChanged;
                _vm.Main.CelebrationTriggered += OnCelebrationTriggered;
                ApplyResponsiveLayout();
            }
        }

        private void OnMainPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsCompactLayout" || e.PropertyName == "IsZenMode")
            {
                ApplyResponsiveLayout();
            }
        }

        private void OnViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            bool compact = _vm != null && _vm.Main != null && _vm.Main.IsCompactLayout;
            bool zen = _vm != null && _vm.Main != null && _vm.Main.IsZenMode;
            double availH = ActualHeight;
            bool tightVertical = compact || (availH > 0 && availH < 580);

            if (compact)
            {
                RightRailCol.Width = new GridLength(0);
                RightRailPanel.Visibility = Visibility.Collapsed;
                CompactBottomCards.Visibility = Visibility.Visible;
                CompactBottomCards.Height = 82;
                CompactBottomCards.Margin = new Thickness(0, 8, 0, 0);
                CenterStageGrid.Margin = new Thickness(0);
            }
            else if (zen)
            {
                RightRailCol.Width = new GridLength(0);
                RightRailPanel.Visibility = Visibility.Collapsed;
                CompactBottomCards.Visibility = Visibility.Collapsed;
                CenterStageGrid.Margin = new Thickness(0);
            }
            else
            {
                RightRailCol.Width = new GridLength(288);
                RightRailPanel.Visibility = Visibility.Visible;
                CompactBottomCards.Visibility = Visibility.Collapsed;
                CenterStageGrid.Margin = new Thickness(0, 0, 16, 0);
            }

            if (tightVertical)
            {
                RingStageGrid.Width = 224;
                RingStageGrid.Height = 224;
                RingInnerStack.LayoutTransform = new ScaleTransform(0.76, 0.76);
                ModeSegmentControl.Margin = new Thickness(0, 0, 0, 8);
                ControlBarPanel.Margin = new Thickness(0, 10, 0, 0);
                PrimaryTimerButton.Width = 60;
                PrimaryTimerButton.Height = 60;
                TaskSelectorRow.Margin = new Thickness(0, 10, 0, 0);
                TaskSelectorPill.Height = 34;
                TaskSelectorPill.CornerRadius = new CornerRadius(17);
            }
            else
            {
                RingStageGrid.Width = 308;
                RingStageGrid.Height = 308;
                RingInnerStack.LayoutTransform = Transform.Identity;
                ModeSegmentControl.Margin = new Thickness(0, 0, 0, 18);
                ControlBarPanel.Margin = new Thickness(0, 18, 0, 0);
                PrimaryTimerButton.Width = 72;
                PrimaryTimerButton.Height = 72;
                TaskSelectorRow.Margin = new Thickness(0, 18, 0, 0);
                TaskSelectorPill.Height = 40;
                TaskSelectorPill.CornerRadius = new CornerRadius(20);
            }
        }

        private void OnModeSegmentClicked(object sender, string key)
        {
            if (_vm != null)
            {
                _vm.HandleModeSegmentClicked(key);
            }
        }

        private void OnCelebrationTriggered(object sender, EventArgs e)
        {
            TimerRing.TriggerCelebration();
            if (!AnimationHelper.IsReducedMotion)
            {
                var bounce = new DoubleAnimationUsingKeyFrames();
                bounce.KeyFrames.Add(new EasingDoubleKeyFrame(1.06, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
                bounce.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));
                TimerDigitsScale.BeginAnimation(ScaleTransform.ScaleXProperty, bounce);
                TimerDigitsScale.BeginAnimation(ScaleTransform.ScaleYProperty, bounce);
            }
        }
    }
}
