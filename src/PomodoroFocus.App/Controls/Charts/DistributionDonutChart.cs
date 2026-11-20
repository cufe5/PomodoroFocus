using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.App.Controls.Charts
{
    public sealed class DistributionDonutChart : FrameworkElement
    {
        public static readonly DependencyProperty SlicesProperty = DependencyProperty.Register(
            "Slices", typeof(IList<DistributionSlice>), typeof(DistributionDonutChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        private static readonly Color[] Palette = new Color[]
        {
            Color.FromRgb(0xFF, 0x6B, 0x5E),
            Color.FromRgb(0x2D, 0xD4, 0xBF),
            Color.FromRgb(0x7C, 0x83, 0xFF),
            Color.FromRgb(0xFB, 0xBF, 0x24),
            Color.FromRgb(0x60, 0xA5, 0xFA),
            Color.FromRgb(0x94, 0xA3, 0xB8)
        };

        public DistributionDonutChart()
        {
            MinHeight = 172;
        }

        public IList<DistributionSlice> Slices
        {
            get { return (IList<DistributionSlice>)GetValue(SlicesProperty); }
            set { SetValue(SlicesProperty, value); }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 120 || h < 120) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var uiFont = new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI");
            var displayFont = new FontFamily("Segoe UI Variable Display, Microsoft YaHei UI");
            var boldTypeface = new Typeface(displayFont, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var regTypeface = new Typeface(uiFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            Brush primaryBrush = GetResBrush("Text.Primary", Brushes.White);
            Brush secondaryBrush = GetResBrush("Text.Secondary", Brushes.LightGray);
            Brush tertiaryBrush = GetResBrush("Text.Tertiary", Brushes.Gray);
            Brush trackBrush = GetResBrush("Ring.Track", new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)));

            double ringSize = Math.Min(148.0, Math.Min(h - 12.0, w * 0.48));
            double r = ringSize * 0.5 - 10.0;
            double strokeW = 16.0;
            double cx = ringSize * 0.5 + 4.0;
            double cy = h * 0.5;

            IList<DistributionSlice> slices = Slices;
            if (slices == null || slices.Count == 0)
            {
                dc.DrawEllipse(null, new Pen(trackBrush, strokeW), new Point(cx, cy), r, r);
                var emptyFt = new FormattedText("暂无数据", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, regTypeface, 12.0, tertiaryBrush, dpi);
                dc.DrawText(emptyFt, new Point(cx - emptyFt.Width * 0.5, cy - emptyFt.Height * 0.5));
                return;
            }

            int totalSec = 0;
            for (int i = 0; i < slices.Count; i++) totalSec += slices[i].Seconds;

            double angle = -90.0;
            double gap = slices.Count > 1 ? 2.5 : 0.0;

            for (int i = 0; i < slices.Count; i++)
            {
                double sweep = (slices[i].Percentage / 100.0) * 360.0;
                double drawSweep = sweep - gap;
                if (drawSweep < 1.0) drawSweep = Math.Max(0.5, sweep);

                Color c = Palette[i % Palette.Length];
                var pen = new Pen(new SolidColorBrush(c), strokeW);
                pen.StartLineCap = PenLineCap.Flat;
                pen.EndLineCap = PenLineCap.Flat;

                if (drawSweep >= 359.5)
                {
                    dc.DrawEllipse(null, pen, new Point(cx, cy), r, r);
                }
                else
                {
                    double a0 = angle * Math.PI / 180.0;
                    double a1 = (angle + drawSweep) * Math.PI / 180.0;
                    var p0 = new Point(cx + r * Math.Cos(a0), cy + r * Math.Sin(a0));
                    var p1 = new Point(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1));

                    var fig = new PathFigure { StartPoint = p0, IsClosed = false };
                    fig.Segments.Add(new ArcSegment(p1, new Size(r, r), 0, drawSweep > 180.0, SweepDirection.Clockwise, true));
                    var geo = new PathGeometry();
                    geo.Figures.Add(fig);
                    dc.DrawGeometry(null, pen, geo);
                }

                angle += sweep;
            }

            // Center total text
            string hoursText = totalSec >= 3600
                ? (totalSec / 3600.0).ToString("0.0", CultureInfo.InvariantCulture) + "h"
                : ((int)Math.Round(totalSec / 60.0)).ToString(CultureInfo.InvariantCulture) + "m";
            var centerVal = new FormattedText(hoursText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, boldTypeface, 20.0, primaryBrush, dpi);
            var centerSub = new FormattedText("总专注", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, regTypeface, 11.0, tertiaryBrush, dpi);
            dc.DrawText(centerVal, new Point(cx - centerVal.Width * 0.5, cy - centerVal.Height * 0.72));
            dc.DrawText(centerSub, new Point(cx - centerSub.Width * 0.5, cy + centerVal.Height * 0.22));

            // Right legend
            double legX = ringSize + 18.0;
            double legW = w - legX - 4.0;
            if (legW < 70.0) return;

            double rowH = 24.0;
            double totalLegH = slices.Count * rowH;
            double startY = Math.Max(4.0, (h - totalLegH) * 0.5);

            for (int i = 0; i < slices.Count; i++)
            {
                DistributionSlice sl = slices[i];
                double ry = startY + i * rowH;
                Color c = Palette[i % Palette.Length];

                dc.DrawEllipse(new SolidColorBrush(c), null, new Point(legX + 4.5, ry + rowH * 0.5), 4.0, 4.0);

                string pctText = string.Format(CultureInfo.InvariantCulture, "{0:0}%", Math.Round(sl.Percentage));
                var pctFt = new FormattedText(pctText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, regTypeface, 12.0, secondaryBrush, dpi);
                dc.DrawText(pctFt, new Point(legX + legW - pctFt.Width, ry + (rowH - pctFt.Height) * 0.5));

                double maxNameW = Math.Max(20.0, legW - pctFt.Width - 20.0);
                var nameFt = new FormattedText(sl.Name ?? string.Empty, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, regTypeface, 12.0, primaryBrush, dpi);
                nameFt.MaxTextWidth = maxNameW;
                nameFt.MaxLineCount = 1;
                nameFt.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(nameFt, new Point(legX + 14.0, ry + (rowH - nameFt.Height) * 0.5));
            }
        }

        private Brush GetResBrush(string key, Brush fallback)
        {
            object res = TryFindResource(key);
            return (res as Brush) ?? fallback;
        }
    }
}
