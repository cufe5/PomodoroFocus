using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PomodoroFocus.App.Themes;

namespace PomodoroFocus.App.Controls
{
    public sealed class CircularTimerRing : FrameworkElement
    {
        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
            "Progress", typeof(double), typeof(CircularTimerRing),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsRunningProperty = DependencyProperty.Register(
            "IsRunning", typeof(bool), typeof(CircularTimerRing),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnIsRunningChanged));

        public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
            "TrackBrush", typeof(Brush), typeof(CircularTimerRing),
            new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TickBrushProperty = DependencyProperty.Register(
            "TickBrush", typeof(Brush), typeof(CircularTimerRing),
            new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PhaseStartBrushProperty = DependencyProperty.Register(
            "PhaseStartBrush", typeof(Brush), typeof(CircularTimerRing),
            new FrameworkPropertyMetadata(Brushes.Coral, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PhaseEndBrushProperty = DependencyProperty.Register(
            "PhaseEndBrush", typeof(Brush), typeof(CircularTimerRing),
            new FrameworkPropertyMetadata(Brushes.Orange, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PhaseGradientProperty = DependencyProperty.Register(
            "PhaseGradient", typeof(Brush), typeof(CircularTimerRing),
            new FrameworkPropertyMetadata(Brushes.Coral, FrameworkPropertyMetadataOptions.AffectsRender));

        private static readonly DependencyProperty BreathFactorProperty = DependencyProperty.Register(
            "BreathFactor", typeof(double), typeof(CircularTimerRing),
            new FrameworkPropertyMetadata(0.05, FrameworkPropertyMetadataOptions.AffectsRender));

        private static readonly DependencyProperty ParticleProgressProperty = DependencyProperty.Register(
            "ParticleProgress", typeof(double), typeof(CircularTimerRing),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        private bool _particlesActive;
        private readonly double[] _particleSpeeds = new double[18];
        private readonly double[] _particleRadii = new double[18];

        public CircularTimerRing()
        {
            IsHitTestVisible = false;
            var rng = new Random(20261119);
            for (int i = 0; i < 18; i++)
            {
                _particleSpeeds[i] = 28.0 + rng.NextDouble() * 20.0;
                _particleRadii[i] = 2.5 + rng.NextDouble() * 1.5;
            }
        }

        public double Progress
        {
            get { return (double)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }

        public bool IsRunning
        {
            get { return (bool)GetValue(IsRunningProperty); }
            set { SetValue(IsRunningProperty, value); }
        }

        public Brush TrackBrush
        {
            get { return (Brush)GetValue(TrackBrushProperty); }
            set { SetValue(TrackBrushProperty, value); }
        }

        public Brush TickBrush
        {
            get { return (Brush)GetValue(TickBrushProperty); }
            set { SetValue(TickBrushProperty, value); }
        }

        public Brush PhaseStartBrush
        {
            get { return (Brush)GetValue(PhaseStartBrushProperty); }
            set { SetValue(PhaseStartBrushProperty, value); }
        }

        public Brush PhaseEndBrush
        {
            get { return (Brush)GetValue(PhaseEndBrushProperty); }
            set { SetValue(PhaseEndBrushProperty, value); }
        }

        public Brush PhaseGradient
        {
            get { return (Brush)GetValue(PhaseGradientProperty); }
            set { SetValue(PhaseGradientProperty, value); }
        }

        private double BreathFactor
        {
            get { return (double)GetValue(BreathFactorProperty); }
            set { SetValue(BreathFactorProperty, value); }
        }

        private double ParticleProgress
        {
            get { return (double)GetValue(ParticleProgressProperty); }
            set { SetValue(ParticleProgressProperty, value); }
        }

        private static void OnIsRunningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ring = (CircularTimerRing)d;
            ring.UpdateBreathingAnimation();
        }

        private void UpdateBreathingAnimation()
        {
            if (IsRunning && !AnimationHelper.IsReducedMotion)
            {
                var anim = new DoubleAnimation
                {
                    From = 0.06,
                    To = 0.14,
                    Duration = new Duration(TimeSpan.FromSeconds(1.5)),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                BeginAnimation(BreathFactorProperty, anim);
            }
            else
            {
                BeginAnimation(BreathFactorProperty, null);
                BreathFactor = IsRunning ? 0.10 : 0.05;
            }
        }

        public void TriggerCelebration()
        {
            if (AnimationHelper.IsReducedMotion) return;
            _particlesActive = true;
            var anim = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(650)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            anim.Completed += delegate
            {
                _particlesActive = false;
                BeginAnimation(ParticleProgressProperty, null);
                ParticleProgress = 0.0;
                InvalidateVisual();
            };
            BeginAnimation(ParticleProgressProperty, anim);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 40 || h <= 40) return;

            double size = Math.Min(w, h);
            double cx = w * 0.5;
            double cy = h * 0.5;
            double r = size * 0.5 - 22.0;
            if (r <= 10.0) return;

            double p = Progress;
            if (double.IsNaN(p) || p < 0.0) p = 0.0;
            if (p > 1.0) p = 1.0;

            Color startColor = ExtractColor(PhaseStartBrush, Color.FromRgb(0xFF, 0x6B, 0x5E));
            Color endColor = ExtractColor(PhaseEndBrush, Color.FromRgb(0xFF, 0x9F, 0x68));
            Color tickBaseColor = ExtractColor(TickBrush, Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));

            // 1. Ambient soft glow ring
            byte glowAlpha = (byte)Math.Max(0, Math.Min(255, (int)(BreathFactor * 255.0)));
            var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(glowAlpha, startColor.R, startColor.G, startColor.B)), 24.0);
            dc.DrawEllipse(null, glowPen, new Point(cx, cy), r, r);

            // 2. Outer 60 ticks
            double progressDeg = p * 360.0;
            var majorPen = new Pen(new SolidColorBrush(tickBaseColor), 1.5);
            majorPen.StartLineCap = PenLineCap.Round;
            majorPen.EndLineCap = PenLineCap.Round;
            var minorPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(tickBaseColor.A * 0.5), tickBaseColor.R, tickBaseColor.G, tickBaseColor.B)), 1.0);
            minorPen.StartLineCap = PenLineCap.Round;
            minorPen.EndLineCap = PenLineCap.Round;

            Color passedColor = Color.FromArgb(0x88, startColor.R, startColor.G, startColor.B);
            var passedMajorPen = new Pen(new SolidColorBrush(passedColor), 1.5);
            passedMajorPen.StartLineCap = PenLineCap.Round;
            passedMajorPen.EndLineCap = PenLineCap.Round;
            var passedMinorPen = new Pen(new SolidColorBrush(Color.FromArgb(0x55, startColor.R, startColor.G, startColor.B)), 1.0);
            passedMinorPen.StartLineCap = PenLineCap.Round;
            passedMinorPen.EndLineCap = PenLineCap.Round;

            for (int i = 0; i < 60; i++)
            {
                double deg = i * 6.0;
                double rad = (deg - 90.0) * Math.PI / 180.0;
                bool isMajor = (i % 5 == 0);
                bool isPassed = (p > 0.005 && deg <= progressDeg);

                double innerR = r + 13.0;
                double outerR = isMajor ? (innerR + 5.0) : (innerR + 2.5);
                double cos = Math.Cos(rad);
                double sin = Math.Sin(rad);

                var p1 = new Point(cx + innerR * cos, cy + innerR * sin);
                var p2 = new Point(cx + outerR * cos, cy + outerR * sin);

                Pen tickPen = isPassed
                    ? (isMajor ? passedMajorPen : passedMinorPen)
                    : (isMajor ? majorPen : minorPen);
                dc.DrawLine(tickPen, p1, p2);
            }

            // 3. Track base ring
            var trackPen = new Pen(TrackBrush ?? Brushes.Gray, 10.0);
            dc.DrawEllipse(null, trackPen, new Point(cx, cy), r, r);

            // 4. Main progress arc
            double theta = p * 360.0;
            Brush arcBrush = PhaseGradient ?? PhaseStartBrush ?? Brushes.Coral;
            var arcPen = new Pen(arcBrush, 10.0);
            arcPen.StartLineCap = PenLineCap.Round;
            arcPen.EndLineCap = PenLineCap.Round;

            if (theta >= 359.9)
            {
                dc.DrawEllipse(null, arcPen, new Point(cx, cy), r, r);
            }
            else if (theta >= 0.5)
            {
                double startRad = -Math.PI * 0.5;
                double endRad = (theta - 90.0) * Math.PI / 180.0;
                var startPt = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
                var endPt = new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));

                var fig = new PathFigure();
                fig.StartPoint = startPt;
                fig.IsClosed = false;
                fig.Segments.Add(new ArcSegment(
                    endPt,
                    new Size(r, r),
                    0.0,
                    theta > 180.0,
                    SweepDirection.Clockwise,
                    true));
                var geo = new PathGeometry();
                geo.Figures.Add(fig);
                dc.DrawGeometry(null, arcPen, geo);
            }

            // 5. Head Cap glow dot
            if (p > 0.005)
            {
                double tipRad = (theta - 90.0) * Math.PI / 180.0;
                var tipPt = new Point(cx + r * Math.Cos(tipRad), cy + r * Math.Sin(tipRad));

                var tipGlow = new RadialGradientBrush();
                tipGlow.GradientStops.Add(new GradientStop(Color.FromArgb(0x99, endColor.R, endColor.G, endColor.B), 0.0));
                tipGlow.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, endColor.R, endColor.G, endColor.B), 1.0));
                dc.DrawEllipse(tipGlow, null, tipPt, 12.0, 12.0);

                var dotPen = new Pen(new SolidColorBrush(endColor), 2.0);
                dc.DrawEllipse(Brushes.White, dotPen, tipPt, 4.5, 4.5);
            }

            // 6. Completion 18-particle burst
            if (_particlesActive)
            {
                double t = ParticleProgress;
                double alpha = 1.0 - t;
                if (alpha > 0.01)
                {
                    byte a = (byte)(alpha * 255.0);
                    for (int i = 0; i < 18; i++)
                    {
                        double rad = (i * 20.0 - 90.0) * Math.PI / 180.0;
                        double dist = r + _particleSpeeds[i] * t;
                        var pt = new Point(cx + dist * Math.Cos(rad), cy + dist * Math.Sin(rad));
                        Color c = (i % 3 == 0) ? Colors.White : ((i % 3 == 1) ? startColor : endColor);
                        var brush = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));
                        double pr = _particleRadii[i] * (1.0 - 0.35 * t);
                        dc.DrawEllipse(brush, null, pt, pr, pr);
                    }
                }
            }
        }

        private static Color ExtractColor(Brush brush, Color fallback)
        {
            var scb = brush as SolidColorBrush;
            if (scb != null) return scb.Color;
            return fallback;
        }
    }
}
