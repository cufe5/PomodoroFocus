using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PomodoroFocus.App.Themes;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.App.Controls.Charts
{
    public sealed class TrendBarChart : FrameworkElement
    {
        public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
            "Points", typeof(IList<TrendDayPoint>), typeof(TrendBarChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnDataChanged));

        public static readonly DependencyProperty GoalMinutesProperty = DependencyProperty.Register(
            "GoalMinutes", typeof(int), typeof(TrendBarChart),
            new FrameworkPropertyMetadata(200, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AnimationProgressProperty = DependencyProperty.Register(
            "AnimationProgress", typeof(double), typeof(TrendBarChart),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        private int _hoverIndex = -1;
        private readonly List<Rect> _barRects = new List<Rect>();

        public TrendBarChart()
        {
            MinHeight = 190;
            MouseMove += OnChartMouseMove;
            MouseLeave += delegate
            {
                if (_hoverIndex != -1)
                {
                    _hoverIndex = -1;
                    ToolTip = null;
                    InvalidateVisual();
                }
            };
        }

        public IList<TrendDayPoint> Points
        {
            get { return (IList<TrendDayPoint>)GetValue(PointsProperty); }
            set { SetValue(PointsProperty, value); }
        }

        public int GoalMinutes
        {
            get { return (int)GetValue(GoalMinutesProperty); }
            set { SetValue(GoalMinutesProperty, value); }
        }

        public double AnimationProgress
        {
            get { return (double)GetValue(AnimationProgressProperty); }
            set { SetValue(AnimationProgressProperty, value); }
        }

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var chart = (TrendBarChart)d;
            chart._hoverIndex = -1;
            TimeSpan dur = AnimationHelper.Duration(MotionToken.Chart);
            if (dur == TimeSpan.Zero)
            {
                chart.BeginAnimation(AnimationProgressProperty, null);
                chart.AnimationProgress = 1.0;
            }
            else
            {
                var anim = new DoubleAnimation(0.0, 1.0, new Duration(dur));
                anim.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                chart.BeginAnimation(AnimationProgressProperty, anim);
            }
            chart.InvalidateVisual();
        }

        private void OnChartMouseMove(object sender, MouseEventArgs e)
        {
            Point pt = e.GetPosition(this);
            int hit = -1;
            for (int i = 0; i < _barRects.Count; i++)
            {
                Rect r = _barRects[i];
                if (pt.X >= r.Left - 3 && pt.X <= r.Right + 3 && pt.Y >= 8 && pt.Y <= ActualHeight - 16)
                {
                    hit = i;
                    break;
                }
            }
            if (hit != _hoverIndex)
            {
                _hoverIndex = hit;
                IList<TrendDayPoint> pts = Points;
                if (hit >= 0 && pts != null && hit < pts.Count)
                {
                    ToolTip = pts[hit].TooltipTitle;
                }
                else
                {
                    ToolTip = null;
                }
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            _barRects.Clear();

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 60 || h < 60) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var typeface = new Typeface(new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            Brush gridBrush = GetResBrush("Chart.Grid", new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)));
            Brush labelBrush = GetResBrush("Text.Tertiary", Brushes.Gray);
            Brush accentBrush = GetResBrush("Accent", Brushes.Coral);
            Brush phaseStart = GetResBrush("Phase.StartBrush", Brushes.Coral);
            Brush phaseGrad = GetResBrush("Phase.Gradient", phaseStart);

            Color startColor = (phaseStart is SolidColorBrush) ? ((SolidColorBrush)phaseStart).Color : Color.FromRgb(0xFF, 0x6B, 0x5E);
            var normalBarBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            normalBarBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0xCC, startColor.R, startColor.G, startColor.B), 0.0));
            normalBarBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x55, startColor.R, startColor.G, startColor.B), 1.0));
            var topHighlightPen = new Pen(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)), 1.0);

            const double leftPad = 36.0;
            const double rightPad = 14.0;
            const double topPad = 14.0;
            const double bottomPad = 24.0;
            double plotW = w - leftPad - rightPad;
            double plotH = h - topPad - bottomPad;
            if (plotW <= 10 || plotH <= 10) return;

            IList<TrendDayPoint> pts = Points;
            int maxData = 60;
            if (pts != null)
            {
                for (int i = 0; i < pts.Count; i++)
                {
                    if (pts[i].Minutes > maxData) maxData = pts[i].Minutes;
                }
            }
            int goal = GoalMinutes > 0 ? GoalMinutes : 200;
            int yMax = Math.Max(maxData, goal);
            yMax = ((yMax + 29) / 30) * 30;
            if (yMax < 60) yMax = 60;

            // 3 horizontal gridlines (0, mid, max)
            var gridPen = new Pen(gridBrush, 1.0);
            gridPen.DashStyle = new DashStyle(new double[] { 3, 3 }, 0);
            int[] yTicks = new int[] { 0, yMax / 2, yMax };
            for (int i = 0; i < yTicks.Length; i++)
            {
                double y = topPad + plotH * (1.0 - (double)yTicks[i] / yMax);
                dc.DrawLine(gridPen, new Point(leftPad, y), new Point(leftPad + plotW, y));

                var ft = new FormattedText(
                    yTicks[i].ToString(CultureInfo.InvariantCulture),
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    typeface, 11.0, labelBrush, dpi);
                dc.DrawText(ft, new Point(leftPad - ft.Width - 6, y - ft.Height * 0.5));
            }

            // Goal dashed line
            double goalY = topPad + plotH * (1.0 - (double)goal / yMax);
            if (goalY >= topPad && goalY <= topPad + plotH)
            {
                var goalPen = new Pen(accentBrush, 1.2);
                goalPen.DashStyle = new DashStyle(new double[] { 4, 4 }, 0);
                dc.DrawLine(goalPen, new Point(leftPad, goalY), new Point(leftPad + plotW, goalY));

                var goalLabel = new FormattedText(
                    "目标",
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    typeface, 10.5, accentBrush, dpi);
                dc.DrawText(goalLabel, new Point(leftPad + plotW - goalLabel.Width - 2, goalY - goalLabel.Height - 2));
            }

            if (pts == null || pts.Count == 0) return;

            int count = pts.Count;
            double slotW = plotW / count;
            double barW = count <= 7 ? Math.Min(30.0, slotW * 0.48) : Math.Max(4.0, Math.Min(12.0, slotW * 0.62));
            int labelStep = count <= 7 ? 1 : (count <= 15 ? 2 : 5);
            double anim = AnimationProgress;

            for (int i = 0; i < count; i++)
            {
                TrendDayPoint pt = pts[i];
                double cx = leftPad + slotW * (i + 0.5);
                double rawH = ((double)pt.Minutes / yMax) * plotH * anim;
                if (pt.Minutes > 0 && rawH < 4.0) rawH = 4.0;
                double x = cx - barW * 0.5;
                double y = topPad + plotH - rawH;

                var rect = new Rect(x, y, barW, Math.Max(0, rawH));
                _barRects.Add(rect);

                if (rawH > 0.5)
                {
                    bool isEmphasized = pt.IsToday || i == _hoverIndex;
                    Brush fill = isEmphasized ? phaseGrad : normalBarBrush;
                    double rad = Math.Min(6.0, Math.Min(barW * 0.5, rawH * 0.5));
                    Geometry barGeo = CreateTopRoundedRect(rect, rad);
                    dc.DrawGeometry(fill, isEmphasized ? topHighlightPen : null, barGeo);
                }

                if (i % labelStep == 0 || i == count - 1)
                {
                    Brush lblBrush = pt.IsToday ? GetResBrush("Text.Primary", Brushes.White) : labelBrush;
                    var xFt = new FormattedText(
                        pt.ShortLabel ?? string.Empty,
                        CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        typeface, 11.0, lblBrush, dpi);
                    dc.DrawText(xFt, new Point(cx - xFt.Width * 0.5, topPad + plotH + 6));
                }
            }

            // On-canvas floating glass pill tooltip when hovering a bar
            if (_hoverIndex >= 0 && _hoverIndex < pts.Count && _hoverIndex < _barRects.Count)
            {
                string tipText = pts[_hoverIndex].TooltipTitle ?? string.Empty;
                if (tipText.Length > 0)
                {
                    var pillTypeface = new Typeface(new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
                    Brush pillBg = GetResBrush("Surface.Solid", new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x24)));
                    Brush pillBorder = GetResBrush("Stroke.Strong", new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)));
                    Brush pillTextBrush = GetResBrush("Text.Primary", Brushes.White);

                    var tipFt = new FormattedText(
                        tipText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        pillTypeface, 11.0, pillTextBrush, dpi);
                    double pillW = tipFt.Width + 16.0;
                    double pillH = tipFt.Height + 8.0;
                    Rect barR = _barRects[_hoverIndex];
                    double px = barR.Left + (barR.Width - pillW) * 0.5;
                    if (px < 4.0) px = 4.0;
                    if (px + pillW > w - 4.0) px = w - pillW - 4.0;
                    double py = barR.Top - pillH - 6.0;
                    if (py < 2.0) py = 2.0;

                    dc.DrawRoundedRectangle(pillBg, new Pen(pillBorder, 1.0), new Rect(px, py, pillW, pillH), 7.0, 7.0);
                    dc.DrawText(tipFt, new Point(px + 8.0, py + 4.0));
                }
            }
        }

        private static Geometry CreateTopRoundedRect(Rect r, double radius)
        {
            var fig = new PathFigure { StartPoint = new Point(r.Left, r.Bottom), IsClosed = true };
            fig.Segments.Add(new LineSegment(new Point(r.Left, r.Top + radius), true));
            fig.Segments.Add(new ArcSegment(new Point(r.Left + radius, r.Top), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            fig.Segments.Add(new LineSegment(new Point(r.Right - radius, r.Top), true));
            fig.Segments.Add(new ArcSegment(new Point(r.Right, r.Top + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            fig.Segments.Add(new LineSegment(new Point(r.Right, r.Bottom), true));
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            return geo;
        }

        private Brush GetResBrush(string key, Brush fallback)
        {
            object res = TryFindResource(key);
            return (res as Brush) ?? fallback;
        }
    }
}
