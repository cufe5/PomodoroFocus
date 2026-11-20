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
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public event EventHandler<int> VisibleWeeksChanged;

        private const double CellSize = 13.0;
        private const double CellGap = 3.0;
        private const double Step = CellSize + CellGap;
        private const double LeftAxisWidth = 34.0;
        private const double TopAxisHeight = 20.0;

        private int _lastComputedWeeks = 26;
        private int _hoverCellIndex = -1;

        public FocusHeatmapControl()
        {
            Height = TopAxisHeight + 7 * Step + 6.0;
            SizeChanged += OnControlSizeChanged;
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

        public int ComputedWeeks
        {
            get { return _lastComputedWeeks; }
        }

        private void OnControlSizeChanged(object sender, SizeChangedEventArgs e)
        {
            int cols = (int)Math.Floor((ActualWidth - LeftAxisWidth - 4.0) / Step);
            if (cols < 12) cols = 12;
            if (cols > 52) cols = 52;
            if (cols != _lastComputedWeeks)
            {
                _lastComputedWeeks = cols;
                var handler = VisibleWeeksChanged;
                if (handler != null) handler(this, cols);
            }
            InvalidateVisual();
        }

        private void OnHeatmapMouseMove(object sender, MouseEventArgs e)
        {
            IList<HeatmapCell> cells = Cells;
            if (cells == null || cells.Count == 0) return;

            Point pt = e.GetPosition(this);
            int col = (int)Math.Floor((pt.X - LeftAxisWidth) / Step);
            int row = (int)Math.Floor((pt.Y - TopAxisHeight) / Step);
            if (col < 0 || row < 0 || row >= 7)
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

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var typeface = new Typeface(new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            Brush labelBrush = GetResBrush("Text.Tertiary", Brushes.Gray);

            Brush[] levelBrushes = new Brush[]
            {
                GetResBrush("Heat.L0", new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF))),
                GetResBrush("Heat.L1", new SolidColorBrush(Color.FromArgb(0x47, 0xFF, 0x6B, 0x5E))),
                GetResBrush("Heat.L2", new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0x6B, 0x5E))),
                GetResBrush("Heat.L3", new SolidColorBrush(Color.FromArgb(0xBF, 0xFF, 0x6B, 0x5E))),
                GetResBrush("Heat.L4", new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x6B, 0x5E)))
            };

            // Left weekday labels: 周一 / 周三 / 周五 / 周日
            int[] rows = new int[] { 0, 2, 4, 6 };
            string[] rowNames = new string[] { "周一", "周三", "周五", "周日" };
            for (int i = 0; i < rows.Length; i++)
            {
                var ft = new FormattedText(rowNames[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10.5, labelBrush, dpi);
                double y = TopAxisHeight + rows[i] * Step + (CellSize - ft.Height) * 0.5;
                dc.DrawText(ft, new Point(0, y));
            }

            int lastMonth = -1;
            int weeks = cells.Count / 7;
            for (int w = 0; w < weeks; w++)
            {
                double x = LeftAxisWidth + w * Step;
                if (x + CellSize > ActualWidth + 2) break;

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

                    double y = TopAxisHeight + r * Step;
                    dc.DrawRoundedRectangle(levelBrushes[lvl], null, new Rect(x, y, CellSize, CellSize), 3.0, 3.0);
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
