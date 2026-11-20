using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PomodoroFocus.App.Themes;

namespace PomodoroFocus.App.Views
{
    public partial class ToastWindow : Window
    {
        private readonly Action _primaryAction;
        private readonly Action _secondaryAction;
        private readonly DispatcherTimer _autoCloseTimer;

        public ToastWindow(
            string title,
            string body,
            string primaryText,
            Action primaryAction,
            string secondaryText,
            Action secondaryAction)
        {
            InitializeComponent();
            _primaryAction = primaryAction;
            _secondaryAction = secondaryAction;

            ToastTitleText.Text = title;
            ToastBodyText.Text = body;

            if (string.IsNullOrEmpty(primaryText))
            {
                PrimaryBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                PrimaryBtn.Content = primaryText;
            }

            if (string.IsNullOrEmpty(secondaryText))
            {
                SecondaryBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                SecondaryBtn.Content = secondaryText;
            }

            Rect wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 14;
            Top = wa.Bottom - Height - 14;

            _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _autoCloseTimer.Tick += delegate
            {
                _autoCloseTimer.Stop();
                FadeOutAndClose();
            };

            Loaded += OnToastLoaded;
        }

        private void OnToastLoaded(object sender, RoutedEventArgs e)
        {
            if (AnimationHelper.IsReducedMotion)
            {
                SlideTransform.X = 0;
                ToastCard.Opacity = 1.0;
            }
            else
            {
                ToastCard.Opacity = 0.0;
                var slide = new DoubleAnimation(40.0, 0.0, new Duration(TimeSpan.FromMilliseconds(220)));
                slide.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                var fade = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(220)));
                SlideTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
                ToastCard.BeginAnimation(UIElement.OpacityProperty, fade);
            }
            _autoCloseTimer.Start();
        }

        private void OnCardMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _autoCloseTimer.Stop();
        }

        private void OnCardMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _autoCloseTimer.Start();
        }

        private void OnPrimaryClick(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer.Stop();
            Close();
            if (_primaryAction != null) _primaryAction();
        }

        private void OnSecondaryClick(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer.Stop();
            Close();
            if (_secondaryAction != null) _secondaryAction();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer.Stop();
            FadeOutAndClose();
        }

        private void FadeOutAndClose()
        {
            if (AnimationHelper.IsReducedMotion)
            {
                Close();
                return;
            }
            var fade = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(180)));
            fade.Completed += delegate { Close(); };
            ToastCard.BeginAnimation(UIElement.OpacityProperty, fade);
        }
    }
}
