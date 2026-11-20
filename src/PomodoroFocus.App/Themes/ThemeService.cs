using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using PomodoroFocus.App.Interop;
using PomodoroFocus.Core.Models;

namespace PomodoroFocus.App.Themes
{
    public enum MotionToken
    {
        Fast,
        Normal,
        Slow,
        Phase,
        Chart
    }

    public static class AnimationHelper
    {
        public static bool ReduceMotionSetting { get; set; }

        public static bool IsReducedMotion
        {
            get
            {
                return ReduceMotionSetting || !SystemParameters.ClientAreaAnimation;
            }
        }

        public static TimeSpan Duration(MotionToken token)
        {
            if (IsReducedMotion) return TimeSpan.Zero;
            switch (token)
            {
                case MotionToken.Fast: return TimeSpan.FromMilliseconds(120);
                case MotionToken.Normal: return TimeSpan.FromMilliseconds(200);
                case MotionToken.Slow: return TimeSpan.FromMilliseconds(300);
                case MotionToken.Phase: return TimeSpan.FromMilliseconds(600);
                case MotionToken.Chart: return TimeSpan.FromMilliseconds(500);
                default: return TimeSpan.FromMilliseconds(200);
            }
        }
    }

    public static class ControlProps
    {
        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
            "Placeholder",
            typeof(string),
            typeof(ControlProps),
            new PropertyMetadata(string.Empty));

        public static string GetPlaceholder(DependencyObject obj)
        {
            return (string)obj.GetValue(PlaceholderProperty);
        }

        public static void SetPlaceholder(DependencyObject obj, string value)
        {
            obj.SetValue(PlaceholderProperty, value);
        }

        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
            "CornerRadius",
            typeof(CornerRadius),
            typeof(ControlProps),
            new PropertyMetadata(new CornerRadius(10)));

        public static CornerRadius GetCornerRadius(DependencyObject obj)
        {
            return (CornerRadius)obj.GetValue(CornerRadiusProperty);
        }

        public static void SetCornerRadius(DependencyObject obj, CornerRadius value)
        {
            obj.SetValue(CornerRadiusProperty, value);
        }
    }

    public sealed class ThemeService
    {
        private ResourceDictionary _activeThemeDict;

        // Non-frozen dynamic phase brushes
        private SolidColorBrush _phaseStartBrush;
        private SolidColorBrush _phaseEndBrush;
        private LinearGradientBrush _phaseGradient;
        private GradientStop _gradStop0;
        private GradientStop _gradStop1;
        private SolidColorBrush _phaseSoftBrush;
        private SolidColorBrush _phaseMiniBorderBrush;
        private RadialGradientBrush _phaseGlowBrush;
        private GradientStop _glowStop0;
        private GradientStop _glowStop1;
        private RadialGradientBrush _bgGlow2Brush;
        private GradientStop _glow2Stop0;

        public string CurrentThemeKey { get; private set; }
        public TimerPhase CurrentPhase { get; private set; }
        public bool IsDark { get; private set; }
        public Color CurrentPhaseStartColor { get; private set; }
        public Color CurrentPhaseEndColor { get; private set; }

        public event EventHandler ThemeChanged;
        public event EventHandler PhaseColorsChanged;

        public ThemeService()
        {
            CurrentThemeKey = "midnight";
            CurrentPhase = TimerPhase.Focus;
            IsDark = true;
            InitializeDynamicBrushes();
        }

        private void InitializeDynamicBrushes()
        {
            Color start = Color.FromRgb(0xFF, 0x6B, 0x5E);
            Color end = Color.FromRgb(0xFF, 0x9F, 0x68);
            CurrentPhaseStartColor = start;
            CurrentPhaseEndColor = end;

            _phaseStartBrush = new SolidColorBrush(start);
            _phaseEndBrush = new SolidColorBrush(end);

            _gradStop0 = new GradientStop(start, 0.0);
            _gradStop1 = new GradientStop(end, 1.0);
            _phaseGradient = new LinearGradientBrush();
            _phaseGradient.StartPoint = new Point(0, 0);
            _phaseGradient.EndPoint = new Point(1, 1);
            _phaseGradient.GradientStops.Add(_gradStop0);
            _phaseGradient.GradientStops.Add(_gradStop1);

            _phaseSoftBrush = new SolidColorBrush(WithAlpha(start, 0x24));
            _phaseMiniBorderBrush = new SolidColorBrush(WithAlpha(start, 0x59));

            _glowStop0 = new GradientStop(WithAlpha(start, (byte)(255 * 0.22)), 0.0);
            _glowStop1 = new GradientStop(WithAlpha(start, (byte)(255 * 0.05)), 0.55);
            _phaseGlowBrush = new RadialGradientBrush();
            _phaseGlowBrush.Center = new Point(0.5, 0.5);
            _phaseGlowBrush.GradientOrigin = new Point(0.5, 0.5);
            _phaseGlowBrush.RadiusX = 0.5;
            _phaseGlowBrush.RadiusY = 0.5;
            _phaseGlowBrush.GradientStops.Add(_glowStop0);
            _phaseGlowBrush.GradientStops.Add(_glowStop1);
            _phaseGlowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, start.R, start.G, start.B), 1.0));

            _glow2Stop0 = new GradientStop(Color.FromArgb(0x24, 0x4F, 0x46, 0xE5), 0.0);
            _bgGlow2Brush = new RadialGradientBrush();
            _bgGlow2Brush.Center = new Point(0.5, 0.5);
            _bgGlow2Brush.GradientOrigin = new Point(0.5, 0.5);
            _bgGlow2Brush.RadiusX = 0.5;
            _bgGlow2Brush.RadiusY = 0.5;
            _bgGlow2Brush.GradientStops.Add(_glow2Stop0);
            _bgGlow2Brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0x4F, 0x46, 0xE5), 0.65));

            RegisterDynamicResources();
        }

        private void RegisterDynamicResources()
        {
            if (Application.Current == null) return;
            ResourceDictionary res = Application.Current.Resources;
            res["Phase.StartBrush"] = _phaseStartBrush;
            res["Phase.EndBrush"] = _phaseEndBrush;
            res["Phase.Gradient"] = _phaseGradient;
            res["Phase.SoftBrush"] = _phaseSoftBrush;
            res["Phase.MiniBorderBrush"] = _phaseMiniBorderBrush;
            res["Phase.GlowBrush"] = _phaseGlowBrush;
            res["Phase.Color"] = CurrentPhaseStartColor;
            res["Bg.Glow2Brush"] = _bgGlow2Brush;
        }

        public void ApplyTheme(string themeKey, Window mainWindow, Image crossfadeImage, bool immediate)
        {
            if (!SoundCatalog.IsValidThemeKey(themeKey))
            {
                themeKey = "midnight";
            }

            bool doCrossfade = !immediate
                && !AnimationHelper.IsReducedMotion
                && mainWindow != null
                && crossfadeImage != null
                && mainWindow.IsVisible
                && mainWindow.ActualWidth > 10
                && mainWindow.ActualHeight > 10;

            if (doCrossfade)
            {
                try
                {
                    var visual = mainWindow.Content as Visual;
                    if (visual != null)
                    {
                        var rtb = new RenderTargetBitmap(
                            (int)Math.Ceiling(mainWindow.ActualWidth),
                            (int)Math.Ceiling(mainWindow.ActualHeight),
                            96.0, 96.0, PixelFormats.Pbgra32);
                        rtb.Render(visual);
                        crossfadeImage.Source = rtb;
                        crossfadeImage.Opacity = 1.0;
                        crossfadeImage.Visibility = Visibility.Visible;
                    }
                }
                catch
                {
                    doCrossfade = false;
                }
            }

            string uriStr = string.Format("Themes/Tokens.{0}.xaml", themeKey);
            var newDict = new ResourceDictionary { Source = new Uri(uriStr, UriKind.Relative) };

            if (Application.Current != null)
            {
                var merged = Application.Current.Resources.MergedDictionaries;
                if (_activeThemeDict != null)
                {
                    int idx = merged.IndexOf(_activeThemeDict);
                    if (idx >= 0)
                    {
                        merged[idx] = newDict;
                    }
                    else
                    {
                        merged.Add(newDict);
                    }
                }
                else
                {
                    merged.Add(newDict);
                }
                _activeThemeDict = newDict;
            }

            CurrentThemeKey = themeKey;
            IsDark = GetBoolResource(newDict, "IsDark", true);

            Color glow2 = GetColorResource(newDict, "Bg.Glow2.Color", Color.FromArgb(0x24, 0x4F, 0x46, 0xE5));
            _glow2Stop0 = new GradientStop(glow2, 0.0);
            _bgGlow2Brush = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            _bgGlow2Brush.GradientStops.Add(_glow2Stop0);
            _bgGlow2Brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, glow2.R, glow2.G, glow2.B), 0.65));
            if (Application.Current != null)
            {
                Application.Current.Resources["Bg.Glow2Brush"] = _bgGlow2Brush;
            }

            AnimatePhaseBrush(CurrentPhase, true);

            if (mainWindow != null)
            {
                Color dwmBorder = GetColorResource(newDict, "DwmBorder.Color", Color.FromRgb(0x2A, 0x30, 0x3C));
                DwmInterop.ApplyWindowChromeAttributes(mainWindow, IsDark, dwmBorder);
            }

            if (doCrossfade && crossfadeImage != null)
            {
                var anim = new DoubleAnimation(1.0, 0.0, AnimationHelper.Duration(MotionToken.Slow));
                anim.EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut };
                anim.Completed += delegate
                {
                    crossfadeImage.Visibility = Visibility.Collapsed;
                    crossfadeImage.Source = null;
                };
                crossfadeImage.BeginAnimation(UIElement.OpacityProperty, anim);
            }

            var handler = ThemeChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void AnimatePhaseBrush(TimerPhase phase, bool immediate)
        {
            Color prevStart = CurrentPhaseStartColor;
            Color prevEnd = CurrentPhaseEndColor;
            double prevGlowAlpha = GetDoubleResource(_activeThemeDict, "Bg.Glow1.Alpha", 0.22);

            CurrentPhase = phase;
            ResourceDictionary dict = _activeThemeDict;

            string startKey = "Phase.Focus.Start";
            string endKey = "Phase.Focus.End";
            if (phase == TimerPhase.ShortBreak)
            {
                startKey = "Phase.Short.Start";
                endKey = "Phase.Short.End";
            }
            else if (phase == TimerPhase.LongBreak)
            {
                startKey = "Phase.Long.Start";
                endKey = "Phase.Long.End";
            }

            Color startColor = GetColorResource(dict, startKey, Color.FromRgb(0xFF, 0x6B, 0x5E));
            Color endColor = GetColorResource(dict, endKey, Color.FromRgb(0xFF, 0x9F, 0x68));
            double glowAlpha = GetDoubleResource(dict, "Bg.Glow1.Alpha", 0.22);

            CurrentPhaseStartColor = startColor;
            CurrentPhaseEndColor = endColor;

            Color softColor = WithAlpha(startColor, 0x24);
            Color miniBorderColor = WithAlpha(startColor, 0x59);
            Color glowCenter = WithAlpha(startColor, (byte)Math.Max(0, Math.Min(255, (int)(glowAlpha * 255.0))));
            Color glowMid = WithAlpha(startColor, (byte)Math.Max(0, Math.Min(255, (int)(glowAlpha * 0.26 * 255.0))));

            TimeSpan dur = immediate ? TimeSpan.Zero : AnimationHelper.Duration(MotionToken.Phase);

            // Always create fresh mutable brushes before registering so WPF Style sealing never throws on frozen instances.
            if (dur == TimeSpan.Zero)
            {
                _phaseStartBrush = new SolidColorBrush(startColor);
                _phaseEndBrush = new SolidColorBrush(endColor);

                _gradStop0 = new GradientStop(startColor, 0.0);
                _gradStop1 = new GradientStop(endColor, 1.0);
                _phaseGradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                _phaseGradient.GradientStops.Add(_gradStop0);
                _phaseGradient.GradientStops.Add(_gradStop1);

                _phaseSoftBrush = new SolidColorBrush(softColor);
                _phaseMiniBorderBrush = new SolidColorBrush(miniBorderColor);

                _glowStop0 = new GradientStop(glowCenter, 0.0);
                _glowStop1 = new GradientStop(glowMid, 0.55);
                _phaseGlowBrush = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5),
                    GradientOrigin = new Point(0.5, 0.5),
                    RadiusX = 0.5,
                    RadiusY = 0.5
                };
                _phaseGlowBrush.GradientStops.Add(_glowStop0);
                _phaseGlowBrush.GradientStops.Add(_glowStop1);
                _phaseGlowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, startColor.R, startColor.G, startColor.B), 1.0));
            }
            else
            {
                Color prevSoft = WithAlpha(prevStart, 0x24);
                Color prevMini = WithAlpha(prevStart, 0x59);
                Color prevGlowCenter = WithAlpha(prevStart, (byte)Math.Max(0, Math.Min(255, (int)(prevGlowAlpha * 255.0))));
                Color prevGlowMid = WithAlpha(prevStart, (byte)Math.Max(0, Math.Min(255, (int)(prevGlowAlpha * 0.26 * 255.0))));

                _phaseStartBrush = new SolidColorBrush(prevStart);
                _phaseEndBrush = new SolidColorBrush(prevEnd);

                _gradStop0 = new GradientStop(prevStart, 0.0);
                _gradStop1 = new GradientStop(prevEnd, 1.0);
                _phaseGradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                _phaseGradient.GradientStops.Add(_gradStop0);
                _phaseGradient.GradientStops.Add(_gradStop1);

                _phaseSoftBrush = new SolidColorBrush(prevSoft);
                _phaseMiniBorderBrush = new SolidColorBrush(prevMini);

                _glowStop0 = new GradientStop(prevGlowCenter, 0.0);
                _glowStop1 = new GradientStop(prevGlowMid, 0.55);
                _phaseGlowBrush = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5),
                    GradientOrigin = new Point(0.5, 0.5),
                    RadiusX = 0.5,
                    RadiusY = 0.5
                };
                _phaseGlowBrush.GradientStops.Add(_glowStop0);
                _phaseGlowBrush.GradientStops.Add(_glowStop1);
                _phaseGlowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, startColor.R, startColor.G, startColor.B), 1.0));

                var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
                AnimateColor(_phaseStartBrush, SolidColorBrush.ColorProperty, startColor, dur, ease);
                AnimateColor(_phaseEndBrush, SolidColorBrush.ColorProperty, endColor, dur, ease);
                AnimateColor(_gradStop0, GradientStop.ColorProperty, startColor, dur, ease);
                AnimateColor(_gradStop1, GradientStop.ColorProperty, endColor, dur, ease);
                AnimateColor(_phaseSoftBrush, SolidColorBrush.ColorProperty, softColor, dur, ease);
                AnimateColor(_phaseMiniBorderBrush, SolidColorBrush.ColorProperty, miniBorderColor, dur, ease);
                AnimateColor(_glowStop0, GradientStop.ColorProperty, glowCenter, dur, ease);
                AnimateColor(_glowStop1, GradientStop.ColorProperty, glowMid, dur, ease);
            }

            RegisterDynamicResources();

            var handler = PhaseColorsChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public Color GetCurrentThemeColor(string key, Color fallback)
        {
            return GetColorResource(_activeThemeDict, key, fallback);
        }

        public static System.Drawing.Color ToDrawingColor(Color c)
        {
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }

        private static void AnimateColor(Animatable target, DependencyProperty prop, Color to, TimeSpan dur, IEasingFunction ease)
        {
            var anim = new ColorAnimation
            {
                To = to,
                Duration = new Duration(dur),
                EasingFunction = ease
            };
            target.BeginAnimation(prop, anim);
        }

        private static Color WithAlpha(Color c, byte alpha)
        {
            return Color.FromArgb(alpha, c.R, c.G, c.B);
        }

        private static Color GetColorResource(ResourceDictionary dict, string key, Color fallback)
        {
            if (dict != null && dict.Contains(key) && dict[key] is Color)
            {
                return (Color)dict[key];
            }
            if (Application.Current != null && Application.Current.Resources.Contains(key))
            {
                object val = Application.Current.Resources[key];
                if (val is Color) return (Color)val;
                var scb = val as SolidColorBrush;
                if (scb != null) return scb.Color;
            }
            return fallback;
        }

        private static bool GetBoolResource(ResourceDictionary dict, string key, bool fallback)
        {
            if (dict != null && dict.Contains(key) && dict[key] is bool)
            {
                return (bool)dict[key];
            }
            return fallback;
        }

        private static double GetDoubleResource(ResourceDictionary dict, string key, double fallback)
        {
            if (dict != null && dict.Contains(key) && dict[key] is double)
            {
                return (double)dict[key];
            }
            return fallback;
        }
    }
}
