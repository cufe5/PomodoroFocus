using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.App.Controls
{
    public sealed class HotkeyRecorderBox : Border
    {
        public static readonly DependencyProperty GestureTextProperty = DependencyProperty.Register(
            "GestureText", typeof(string), typeof(HotkeyRecorderBox),
            new FrameworkPropertyMetadata("Ctrl+Alt+Space", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnGestureTextChanged));

        public event EventHandler<string> GestureCommitted;

        private readonly StackPanel _capsPanel;
        private readonly TextBlock _promptText;
        private bool _isRecording;

        public HotkeyRecorderBox()
        {
            MinWidth = 176;
            Height = 36;
            CornerRadius = new CornerRadius(10);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(10, 0, 10, 0);
            Cursor = Cursors.Hand;
            Focusable = true;
            SetResourceReference(BackgroundProperty, "Surface.3");
            SetResourceReference(BorderBrushProperty, "Stroke.Subtle");

            var grid = new Grid();
            _capsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _promptText = new TextBlock
            {
                Text = "请按下组合键… (Esc 取消)",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _promptText.SetResourceReference(TextBlock.ForegroundProperty, "Accent");

            grid.Children.Add(_capsPanel);
            grid.Children.Add(_promptText);
            Child = grid;

            MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                BeginRecording();
                e.Handled = true;
            };
            LostKeyboardFocus += delegate { CancelRecording(); };
            PreviewKeyDown += OnRecorderKeyDown;

            RebuildCaps();
        }

        public string GestureText
        {
            get { return (string)GetValue(GestureTextProperty); }
            set { SetValue(GestureTextProperty, value); }
        }

        private static void OnGestureTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((HotkeyRecorderBox)d).RebuildCaps();
        }

        private void BeginRecording()
        {
            _isRecording = true;
            Focus();
            SetResourceReference(BorderBrushProperty, "Accent");
            BorderThickness = new Thickness(1.5);
            _capsPanel.Visibility = Visibility.Collapsed;
            _promptText.Visibility = Visibility.Visible;
        }

        private void CancelRecording()
        {
            if (!_isRecording) return;
            _isRecording = false;
            SetResourceReference(BorderBrushProperty, "Stroke.Subtle");
            BorderThickness = new Thickness(1);
            _promptText.Visibility = Visibility.Collapsed;
            _capsPanel.Visibility = Visibility.Visible;
        }

        private void OnRecorderKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isRecording) return;
            e.Handled = true;

            Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;
            if (key == Key.Escape)
            {
                CancelRecording();
                return;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            HotkeyModifiers mods = HotkeyModifiers.None;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods |= HotkeyModifiers.Control;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods |= HotkeyModifiers.Alt;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods |= HotkeyModifiers.Shift;
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) mods |= HotkeyModifiers.Win;

            if (mods == HotkeyModifiers.None) return;

            int vk = KeyInterop.VirtualKeyFromKey(key);
            HotkeyGesture gesture;
            if (HotkeyGesture.TryFormatFromVirtualKey(mods, vk, out gesture))
            {
                string formatted = gesture.ToDisplayString();
                GestureText = formatted;
                CancelRecording();
                var handler = GestureCommitted;
                if (handler != null) handler(this, formatted);
            }
        }

        private void RebuildCaps()
        {
            _capsPanel.Children.Clear();
            HotkeyGesture g;
            string[] caps = HotkeyGesture.TryParse(GestureText, out g)
                ? g.ToKeyCaps()
                : new string[] { GestureText ?? string.Empty };

            for (int i = 0; i < caps.Length; i++)
            {
                if (i > 0)
                {
                    var plus = new TextBlock
                    {
                        Text = "+",
                        FontSize = 11,
                        Margin = new Thickness(4, 0, 4, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    plus.SetResourceReference(TextBlock.ForegroundProperty, "Text.Tertiary");
                    _capsPanel.Children.Add(plus);
                }

                var capBorder = new Border
                {
                    Height = 22,
                    MinWidth = 24,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6, 0, 6, 0),
                    BorderThickness = new Thickness(1, 1, 1, 2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                capBorder.SetResourceReference(Border.BackgroundProperty, "Surface.2");
                capBorder.SetResourceReference(Border.BorderBrushProperty, "Stroke.Strong");

                var tb = new TextBlock
                {
                    Text = caps[i],
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                tb.SetResourceReference(TextBlock.ForegroundProperty, "Text.Primary");
                capBorder.Child = tb;
                _capsPanel.Children.Add(capBorder);
            }
        }
    }
}
