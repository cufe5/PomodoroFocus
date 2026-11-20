using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PomodoroFocus.App.Controls.Charts
{
    public sealed class HourlyAreaChart : FrameworkElement
    {
        public static readonly DependencyProperty HourMinutesProperty = DependencyProperty.Register(
            "HourMinutes", typeof(double[]), typeof(HourlyAreaChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        private int _hoverHour = -1;

        public HourlyAreaChart()
        {
            MinHeight = 148;
            MouseMove += OnChartMouseMove;
            MouseLeave += delegate
            {
                if (_hoverHour != -1)
                {
                    _hoverHour = -1;
                    ToolTip = null;
                    InvalidateVisual();
                }
            };
        }

        public double[] HourMinutes
        {
            get { return (double[])GetValue(HourMinutesProperty); }
            set { SetValue(HourMinutesProperty, value); }
        }

        private void OnChartMouseMove(object sender, MouseEventArgs e)
        {
            double[] data = HourMinutes;
            if (data == null || data.Length < 24 || ActualWidth < 60) return;

            const double leftPad = 34.0;
            const double rightPad = 14.0;
            double plotW = ActualWidth - leftPad - rightPad;
            Point pt = e.GetPosition(this);
            if (pt.X < leftPad || pt.X > leftPad + plotW)
            {
                if (_hoverHour != -1)
                {
                    _hoverHour = -1;
                    ToolTip = null;
                    InvalidateVisual();
                }
                return;
            }

            int h = (int)Math.Round(((pt.X - leftPad) / plotW) * 23.0);
            if (h < 0) h = 0;
            if (h > 23) h = 23;
            if (h != _hoverHour)
            {
                _hoverHour = h;
                ToolTip = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:D2}:00 \u2013 {1:D2}:00 · {2:0} 分钟",
                    h, (h + 1) % 24, Math.Round(data[h]));
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 60 || h < 60) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var typeface = new Typeface(new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            Brush gridBrush = GetResBrush("Chart.Grid", new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)));
            Brush labelBrush = GetResBrush("Text.Tertiary", Brushes.Gray);
            Brush phaseStart = GetResBrush("Phase.StartBrush", Brushes.Coral);
            Color strokeColor = (phaseStart is SolidColorBrush) ? ((SolidColorBrush)phaseStart).Color : Color.FromRgb(0xFF, 0x6B, 0x5E);

            const double leftPad = 34.0;
            const double rightPad = 14.0;
            const double topPad = 12.0;
            const double bottomPad = 24.0;
            double plotW = w - leftPad - rightPad;
            double plotH = h - topPad - bottomPad;
            if (plotW <= 10 || plotH <= 10) return;

            double[] data = HourMinutes;
            if (data == null || data.Length < 24) data = new double[24];

            double maxVal = 20.0;
            int peakHour = -1;
            double peakVal = 0.0;
            for (int i = 0; i < 24; i++)
            {
                if (data[i] > maxVal) maxVal = data[i];
                if (data[i] > peakVal)
                {
                    peakVal = data[i];
                    peakHour = i;
                }
            }
            maxVal = Math.Ceiling(maxVal / 10.0) * 10.0;

            var gridPen = new Pen(gridBrush, 1.0);
            gridPen.DashStyle = new DashStyle(new double[] { 3, 3 }, 0);
            for (int row = 0; row <= 2; row++)
            {
                double val = maxVal * row * 0.5;
                double y = topPad + plotH * (1.0 - row * 0.5);
                dc.DrawLine(gridPen, new Point(leftPad, y), new Point(leftPad + plotW, y));
                var ft = new FormattedText(
                    ((int)Math.Round(val)).ToString(CultureInfo.InvariantCulture),
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    typeface, 11.0, labelBrush, dpi);
                dc.DrawText(ft, new Point(leftPad - ft.Width - 6, y - ft.Height * 0.5));
            }

            var points = new Point[24];
            for (int i = 0; i < 24; i++)
            {
                double x = leftPad + (plotW * i) / 23.0;
                double y = topPad + plotH * (1.0 - (data[i] / maxVal));
                points[i] = new Point(x, y);
            }

            // Build Catmull-Rom smooth Bezier curve
            var lineFig = new PathFigure { StartPoint = points[0], IsClosed = false };
            var areaFig = new PathFigure { StartPoint = new Point(points[0].X, topPad + plotH), IsClosed = true };
            areaFig.Segments.Add(new LineSegment(points[0], false));

            for (int i = 0; i < 23; i++)
            {
                Point p0 = i > 0 ? points[i - 1] : points[i];
                Point p1 = points[i];
                Point p2 = points[i + 1];
                Point p3 = i + 2 < 24 ? points[i + 2] : points[i + 1];

                Point c1 = new Point(
                    p1.X + (p2.X - p0.X) / 6.0,
                    ClampY(p1.Y + (p2.Y - p0.Y) / 6.0, topPad, topPad + plotH));
                Point c2 = new Point(
                    p2.X - (p3.X - p1.X) / 6.0,
                    ClampY(p2.Y - (p3.Y - p1.Y) / 6.0, topPad, topPad + plotH));

                lineFig.Segments.Add(new BezierSegment(c1, c2, p2, true));
                areaFig.Segments.Add(new BezierSegment(c1, c2, p2, true));
            }
            areaFig.Segments.Add(new LineSegment(new Point(points[23].X, topPad + plotH), false));

            var areaGeo = new PathGeometry();
            areaGeo.Figures.Add(areaFig);
            var areaBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            areaBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x59, strokeColor.R, strokeColor.G, strokeColor.B), 0.0));
            areaBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, strokeColor.R, strokeColor.G, strokeColor.B), 1.0));
            dc.DrawGeometry(areaBrush, null, areaGeo);

            var lineGeo = new PathGeometry();
            lineGeo.Figures.Add(lineFig);
            var strokePen = new Pen(new SolidColorBrush(strokeColor), 2.0);
            strokePen.LineJoin = PenLineJoin.Round;
            dc.DrawGeometry(null, strokePen, lineGeo);

            // Peak dot
            if (peakHour >= 0 && peakVal > 0.1)
            {
                dc.DrawEllipse(new SolidColorBrush(strokeColor), new Pen(Brushes.White, 1.5), points[peakHour], 4.0, 4.0);
            }

            // Hover indicator: vertical dashed crosshair + glowing dot + floating glass pill
            if (_hoverHour >= 0 && _hoverHour < 24)
            {
                Point hp = points[_hoverHour];
                var hPen = new Pen(new SolidColorBrush(Color.FromArgb(0x88, strokeColor.R, strokeColor.G, strokeColor.B)), 1.0);
                hPen.DashStyle = new DashStyle(new double[] { 3, 3 }, 0);
                dc.DrawLine(hPen, new Point(hp.X, topPad), new Point(hp.X, topPad + plotH));

                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x38, strokeColor.R, strokeColor.G, strokeColor.B)), null, hp, 8.5, 8.5);
                dc.DrawEllipse(Brushes.White, new Pen(new SolidColorBrush(strokeColor), 2.0), hp, 5.0, 5.0);

                string tipText = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:D2}:00 \u2013 {1:D2}:00 · {2:0} 分钟",
                    _hoverHour, (_hoverHour + 1) % 24, Math.Round(data[_hoverHour]));
                var pillTypeface = new Typeface(new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
                Brush pillBg = GetResBrush("Surface.Solid", new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x24)));
                Brush pillBorder = GetResBrush("Stroke.Strong", new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)));
                Brush pillTextBrush = GetResBrush("Text.Primary", Brushes.White);

                var tipFt = new FormattedText(
                    tipText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    pillTypeface, 11.0, pillTextBrush, dpi);
                double pillW = tipFt.Width + 16.0;
                double pillH = tipFt.Height + 8.0;
                double px = hp.X - pillW * 0.5;
                if (px < 4.0) px = 4.0;
                if (px + pillW > w - 4.0) px = w - pillW - 4.0;
                double py = hp.Y - pillH - 10.0;
                if (py < 2.0) py = 2.0;

                dc.DrawRoundedRectangle(pillBg, new Pen(pillBorder, 1.0), new Rect(px, py, pillW, pillH), 7.0, 7.0);
                dc.DrawText(tipFt, new Point(px + 8.0, py + 4.0));
            }

            // X-axis labels: 00:00, 06:00, 12:00, 18:00, 23:00
            int[] xHours = new int[] { 0, 6, 12, 18, 23 };
            for (int i = 0; i < xHours.Length; i++)
            {
                int hr = xHours[i];
                var ft = new FormattedText(
                    string.Format(CultureInfo.InvariantCulture, "{0:D2}:00", hr),
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    typeface, 11.0, labelBrush, dpi);
                double x = points[hr].X - ft.Width * 0.5;
                if (x < leftPad) x = leftPad;
                if (x + ft.Width > leftPad + plotW) x = leftPad + plotW - ft.Width;
                dc.DrawText(ft, new Point(x, topPad + plotH + 6));
            }
        }

        private static double ClampY(double y, double min, double max)
        {
            if (y < min) return min;
            if (y > max) return max;
            return y;
        }

        private Brush GetResBrush(string key, Brush fallback)
        {
            object res = TryFindResource(key);
            return (res as Brush) ?? fallback;
        }
    }
}
