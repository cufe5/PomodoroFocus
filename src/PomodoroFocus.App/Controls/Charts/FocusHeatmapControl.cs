using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.App.Controls.Charts
{
    public sealed class FocusHeatmapControl : FrameworkElement
    {
        public static readonly DependencyProperty CellsProperty = DependencyProperty.Register(
            "Cells", typeof(IList<HeatmapCell>), typeof(FocusHeatmapControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TargetWeeksProperty = DependencyProperty.Register(
            "TargetWeeks", typeof(int), typeof(FocusHeatmapControl),
            new FrameworkPropertyMetadata(20, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        private const double LeftAxisWidth = 34.0;
        private const double TopAxisHeight = 20.0;

        private int _hoverCellIndex = -1;

        public FocusHeatmapControl()
        {
            SizeChanged += delegate
            {
                InvalidateMeasure();
                InvalidateVisual();
            };
            MouseMove += OnHeatmapMouseMove;
            MouseLeave += delegate
            {
                _hoverCellIndex = -1;
                ToolTip = null;
            };
        }

        public IList<HeatmapCell> Cells
        {
            get { return (IList<HeatmapCell>)GetValue(CellsProperty); }
            set { SetValue(CellsProperty, value); }
        }

        public int TargetWeeks
        {
            get { return (int)GetValue(TargetWeeksProperty); }
            set { SetValue(TargetWeeksProperty, value); }
        }

        private int GetEffectiveWeeks()
        {
            IList<HeatmapCell> cells = Cells;
            if (cells != null && cells.Count >= 7)
            {
                return Math.Max(1, cells.Count / 7);
            }
            int tw = TargetWeeks;
            return tw > 0 ? tw : 20;
        }

        private static void ComputeGridMetrics(
            double totalWidth,
            int weeks,
            out double cellSize,
            out double cellGap,
            out double radius,
            out double labelX,
            out double gridStartX)
        {
            if (weeks < 1) weeks = 1;
            double safeWidth = double.IsNaN(totalWidth) || double.IsInfinity(totalWidth) || totalWidth < 200.0
                ? 760.0
                : totalWidth;
            double availW = Math.Max(120.0, safeWidth - LeftAxisWidth - 4.0);

            if (weeks <= 24)
            {
                cellGap = 4.0;
                cellSize = Math.Floor((availW - (weeks - 1) * cellGap) / weeks);
                if (cellSize < 16.0) cellSize = 16.0;
                if (cellSize > 26.0) cellSize = 26.0;
                radius = 4.5;
            }
            else
            {
                cellGap = 3.0;
                cellSize = Math.Floor((availW - (weeks - 1) * cellGap) / weeks);
                if (cellSize < 10.0) cellSize = 10.0;
                if (cellSize > 14.0) cellSize = 14.0;
                radius = 3.0;
            }

            double gridW = weeks * cellSize + Math.Max(0, weeks - 1) * cellGap;
            double blockW = LeftAxisWidth + gridW;
            if (safeWidth > blockW + 8.0)
            {
                labelX = Math.Floor((safeWidth - blockW) * 0.5);
                gridStartX = labelX + LeftAxisWidth;
            }
            else
            {
                labelX = 0.0;
                gridStartX = LeftAxisWidth;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            int weeks = GetEffectiveWeeks();
            double w = (!double.IsInfinity(availableSize.Width) && !double.IsNaN(availableSize.Width) && availableSize.Width > 0)
                ? availableSize.Width
                : (ActualWidth > 0 ? ActualWidth : 760.0);

            double cellSize;
            double cellGap;
            double radius;
            double labelX;
            double gridStartX;
            ComputeGridMetrics(w, weeks, out cellSize, out cellGap, out radius, out labelX, out gridStartX);

            double h = TopAxisHeight + 7.0 * (cellSize + cellGap) + 6.0;
            double desiredW = (!double.IsInfinity(availableSize.Width) && !double.IsNaN(availableSize.Width))
                ? availableSize.Width
                : (LeftAxisWidth + weeks * (cellSize + cellGap));
            return new Size(desiredW, h);
        }

        private void OnHeatmapMouseMove(object sender, MouseEventArgs e)
        {
            IList<HeatmapCell> cells = Cells;
            if (cells == null || cells.Count == 0) return;

            int weeks = GetEffectiveWeeks();
            double cellSize;
            double cellGap;
            double radius;
            double labelX;
            double gridStartX;
            ComputeGridMetrics(ActualWidth, weeks, out cellSize, out cellGap, out radius, out labelX, out gridStartX);
            double step = cellSize + cellGap;

            Point pt = e.GetPosition(this);
            int col = (int)Math.Floor((pt.X - gridStartX) / step);
            int row = (int)Math.Floor((pt.Y - TopAxisHeight) / step);
            if (col < 0 || col >= weeks || row < 0 || row >= 7)
            {
                if (_hoverCellIndex != -1)
                {
                    _hoverCellIndex = -1;
                    ToolTip = null;
                }
                return;
            }

            int idx = col * 7 + row;
            if (idx >= 0 && idx < cells.Count && !cells[idx].IsFuture)
            {
                if (idx != _hoverCellIndex)
                {
                    _hoverCellIndex = idx;
                    HeatmapCell c = cells[idx];
                    ToolTip = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}：{1} 个番茄 · {2}",
                        Formatters.FormatDateWithWeekday(c.Date, DateTime.Now.Year),
                        c.Pomodoros,
                        c.Minutes > 0 ? Formatters.FormatDurationChinese(c.Minutes * 60) : "0 分钟");
                }
            }
            else if (_hoverCellIndex != -1)
            {
                _hoverCellIndex = -1;
                ToolTip = null;
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            IList<HeatmapCell> cells = Cells;
            if (cells == null || cells.Count == 0) return;

            int weeks = Math.Max(1, cells.Count / 7);
            double cellSize;
            double cellGap;
            double radius;
            double labelX;
            double gridStartX;
            ComputeGridMetrics(ActualWidth, weeks, out cellSize, out cellGap, out radius, out labelX, out gridStartX);
            double step = cellSize + cellGap;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var typeface = new Typeface(new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            Brush labelBrush = GetResBrush("Text.Tertiary", Brushes.Gray);

            Brush[] levelBrushes = new Brush[]
            {
                GetResBrush("Heat.L0", new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF))),
                GetResBrush("Heat.L1", new SolidColorBrush(Color.FromArgb(0x47, 0x30, 0xD1, 0x58))),
                GetResBrush("Heat.L2", new SolidColorBrush(Color.FromArgb(0x80, 0x30, 0xD1, 0x58))),
                GetResBrush("Heat.L3", new SolidColorBrush(Color.FromArgb(0xBF, 0x30, 0xD1, 0x58))),
                GetResBrush("Heat.L4", new SolidColorBrush(Color.FromArgb(0xFF, 0x30, 0xD1, 0x58)))
            };

            // Left weekday labels: 周一 / 周三 / 周五 / 周日
            int[] rows = new int[] { 0, 2, 4, 6 };
            string[] rowNames = new string[] { "周一", "周三", "周五", "周日" };
            for (int i = 0; i < rows.Length; i++)
            {
                var ft = new FormattedText(rowNames[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10.5, labelBrush, dpi);
                double y = TopAxisHeight + rows[i] * step + (cellSize - ft.Height) * 0.5;
                dc.DrawText(ft, new Point(labelX, y));
            }

            int lastMonth = -1;
            for (int w = 0; w < weeks; w++)
            {
                double x = gridStartX + w * step;
                if (x + cellSize > ActualWidth + 2.0) break;

                HeatmapCell monday = cells[w * 7];
                if (monday.Date.Month != lastMonth)
                {
                    lastMonth = monday.Date.Month;
                    var mFt = new FormattedText(
                        string.Format(CultureInfo.InvariantCulture, "{0}月", monday.Date.Month),
                        CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        typeface, 10.5, labelBrush, dpi);
                    dc.DrawText(mFt, new Point(x, 2));
                }

                for (int r = 0; r < 7; r++)
                {
                    HeatmapCell cell = cells[w * 7 + r];
                    if (cell.IsFuture) continue;
                    int lvl = cell.Level;
                    if (lvl < 0) lvl = 0;
                    if (lvl > 4) lvl = 4;

                    double y = TopAxisHeight + r * step;
                    dc.DrawRoundedRectangle(levelBrushes[lvl], null, new Rect(x, y, cellSize, cellSize), radius, radius);
                }
            }
        }

        private Brush GetResBrush(string key, Brush fallback)
        {
            object res = TryFindResource(key);
            return (res as Brush) ?? fallback;
        }
    }
}
