using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PomodoroFocus.Core.Models;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.App.Interop
{
    public sealed class TrayIconService : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private readonly bool _disabledByCli;
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _headerItem;
        private ToolStripMenuItem _toggleItem;
        private ToolStripMenuItem _skipItem;
        private ToolStripMenuItem _resetItem;
        private ToolStripMenuItem _miniItem;
        private ToolStripMenuItem _showMainItem;
        private ToolStripMenuItem _audioParentItem;
        private readonly Dictionary<string, ToolStripMenuItem> _audioItems = new Dictionary<string, ToolStripMenuItem>();
        private ToolStripMenuItem _settingsItem;
        private ToolStripMenuItem _exitItem;

        private Icon _currentIcon;
        private IntPtr _currentHIcon = IntPtr.Zero;
        private DateTime _lastIconRedrawUtc = DateTime.MinValue;
        private TimerRunState _lastDrawnState = (TimerRunState)(-1);
        private TimerPhase _lastDrawnPhase = (TimerPhase)(-1);

        private Color _menuBg = Color.FromArgb(22, 27, 38);
        private Color _menuBorder = Color.FromArgb(42, 48, 60);
        private Color _menuHover = Color.FromArgb(35, 42, 58);
        private Color _menuText = Color.FromArgb(242, 244, 248);
        private Color _menuMuted = Color.FromArgb(140, 148, 165);

        public event EventHandler LeftClicked;
        public event EventHandler ToggleRequested;
        public event EventHandler SkipRequested;
        public event EventHandler ResetRequested;
        public event EventHandler MiniToggleRequested;
        public event EventHandler ShowMainRequested;
        public event EventHandler<string> AmbientTrackToggleRequested;
        public event EventHandler AmbientStopAllRequested;
        public event EventHandler OpenSettingsRequested;
        public event EventHandler ExitRequested;

        public TrayIconService(bool disabledByCli)
        {
            _disabledByCli = disabledByCli;
            if (_disabledByCli) return;

            BuildContextMenu();
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = "番茄专注 · 准备专注";
            _notifyIcon.ContextMenuStrip = _menu;
            _notifyIcon.MouseClick += OnTrayMouseClick;
            UpdateIcon(TimerRunState.Idle, TimerPhase.Focus, 0.0, Color.FromArgb(255, 107, 94), true);
        }

        public bool IsVisible
        {
            get { return _notifyIcon != null && _notifyIcon.Visible; }
            set
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = value;
                }
            }
        }

        public void ApplyTheme(bool isDark, Color surfaceSolid, Color strokeStrong, Color hoverColor, Color textPrimary, Color textTertiary)
        {
            _menuBg = surfaceSolid;
            _menuBorder = strokeStrong;
            _menuHover = hoverColor;
            _menuText = textPrimary;
            _menuMuted = textTertiary;

            if (_menu == null) return;
            _menu.BackColor = _menuBg;
            _menu.ForeColor = _menuText;
            _menu.Renderer = new ThemedMenuRenderer(_menuBg, _menuBorder, _menuHover, _menuText, _menuMuted, isDark);
            ApplyColorsToItems(_menu.Items);
        }

        public void UpdateTimerStatus(TimerSnapshot snap, Color phaseColor, bool miniOpen)
        {
            if (_notifyIcon == null || snap == null) return;

            string timeStr = Formatters.FormatTimer(snap.DisplaySeconds);
            string phaseName = GetPhaseLabel(snap);
            string tip;
            if (snap.State == TimerRunState.Running)
            {
                tip = string.Format("番茄专注 · {0} {1}", phaseName, timeStr);
                _headerItem.Text = string.Format("{0} · {1}", phaseName, timeStr);
                _toggleItem.Text = "暂停";
            }
            else if (snap.State == TimerRunState.Paused)
            {
                tip = string.Format("番茄专注 · 已暂停 {0}", timeStr);
                _headerItem.Text = string.Format("已暂停 · {0}", timeStr);
                _toggleItem.Text = "继续";
            }
            else
            {
                tip = string.Format("番茄专注 · 准备{0}", snap.Phase == TimerPhase.Focus ? "专注" : "休息");
                _headerItem.Text = string.Format("准备{0} · {1}", snap.Phase == TimerPhase.Focus ? "专注" : "休息", timeStr);
                _toggleItem.Text = "开始专注";
            }

            if (tip.Length > 63) tip = tip.Substring(0, 63);
            _notifyIcon.Text = tip;
            _miniItem.Checked = miniOpen;

            bool force = (snap.State != _lastDrawnState) || (snap.Phase != _lastDrawnPhase)
                || (DateTime.UtcNow - _lastIconRedrawUtc).TotalSeconds >= 10.0;
            if (force)
            {
                UpdateIcon(snap.State, snap.Phase, snap.Progress, phaseColor, false);
            }
        }

        public void UpdateAmbientTracks(IList<string> activeTrackIds)
        {
            if (_menu == null) return;
            foreach (var kv in _audioItems)
            {
                kv.Value.Checked = activeTrackIds != null && activeTrackIds.Contains(kv.Key);
            }
        }

        public void ShowBalloon(string title, string body)
        {
            if (_notifyIcon == null || !_notifyIcon.Visible) return;
            try
            {
                _notifyIcon.ShowBalloonTip(5000, title, body, ToolTipIcon.None);
            }
            catch
            {
                // Ignore balloon errors
            }
        }

        private void UpdateIcon(TimerRunState state, TimerPhase phase, double progress, Color phaseColor, bool force)
        {
            if (_notifyIcon == null) return;
            _lastIconRedrawUtc = DateTime.UtcNow;
            _lastDrawnState = state;
            _lastDrawnPhase = phase;

            Size sz = SystemInformation.SmallIconSize;
            int w = sz.Width > 0 ? sz.Width : 32;
            int h = sz.Height > 0 ? sz.Height : 32;

            using (var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                if (state == TimerRunState.Idle)
                {
                    // Draw compact brand logo
                    float pad = w * 0.06f;
                    float box = w - pad * 2.0f;
                    using (var path = CreateRoundedRect(pad, pad, box, box, box * 0.24f))
                    using (var grad = new LinearGradientBrush(
                        new PointF(0, 0), new PointF(w, h),
                        Color.FromArgb(255, 159, 104), Color.FromArgb(255, 90, 95)))
                    {
                        g.FillPath(grad, path);
                    }
                    float arcPad = w * 0.24f;
                    float arcSize = w - arcPad * 2.0f;
                    using (var whitePen = new Pen(Color.White, Math.Max(2.0f, w * 0.10f)))
                    {
                        whitePen.StartCap = LineCap.Round;
                        whitePen.EndCap = LineCap.Round;
                        g.DrawArc(whitePen, arcPad, arcPad + w * 0.04f, arcSize, arcSize, -60f, 300f);
                    }
                    using (var leafBrush = new SolidBrush(Color.FromArgb(52, 211, 153)))
                    {
                        g.FillEllipse(leafBrush, w * 0.44f, w * 0.14f, w * 0.20f, w * 0.14f);
                    }
                }
                else
                {
                    // Dark translucent circular plate + track ring + phase progress arc
                    float stroke = Math.Max(2.5f, w * 0.13f);
                    float inset = stroke * 0.7f;
                    float diam = w - inset * 2.0f;

                    using (var bgBrush = new SolidBrush(Color.FromArgb(220, 15, 20, 30)))
                    {
                        g.FillEllipse(bgBrush, 1, 1, w - 2, h - 2);
                    }
                    using (var trackPen = new Pen(Color.FromArgb(70, 255, 255, 255), stroke))
                    {
                        g.DrawEllipse(trackPen, inset, inset, diam, diam);
                    }
                    float sweep = (float)(Math.Max(0.04, Math.Min(1.0, progress)) * 360.0);
                    using (var arcPen = new Pen(phaseColor, stroke))
                    {
                        arcPen.StartCap = LineCap.Round;
                        arcPen.EndCap = LineCap.Round;
                        g.DrawArc(arcPen, inset, inset, diam, diam, -90f, sweep);
                    }
                    if (state == TimerRunState.Paused)
                    {
                        float barW = Math.Max(2f, w * 0.10f);
                        float barH = w * 0.32f;
                        float top = (h - barH) * 0.5f;
                        using (var pBrush = new SolidBrush(Color.White))
                        {
                            g.FillRectangle(pBrush, w * 0.36f, top, barW, barH);
                            g.FillRectangle(pBrush, w * 0.54f, top, barW, barH);
                        }
                    }
                }

                IntPtr newHIcon = bmp.GetHicon();
                Icon newIcon = Icon.FromHandle(newHIcon);

                Icon oldIcon = _currentIcon;
                IntPtr oldHIcon = _currentHIcon;

                _currentIcon = newIcon;
                _currentHIcon = newHIcon;
                _notifyIcon.Icon = _currentIcon;

                if (oldIcon != null) oldIcon.Dispose();
                if (oldHIcon != IntPtr.Zero) DestroyIcon(oldHIcon);
            }
        }

        private void BuildContextMenu()
        {
            _menu = new ContextMenuStrip();
            _menu.Font = new Font("Microsoft YaHei UI", 9.0f, FontStyle.Regular);
            _menu.ShowImageMargin = true;
            _menu.ShowCheckMargin = true;

            _headerItem = MakeMenuItem("准备专注 · 25:00", null);
            _headerItem.Enabled = false;
            _toggleItem = MakeMenuItem("开始专注", delegate { Raise(ToggleRequested); });
            _skipItem = MakeMenuItem("跳过当前阶段", delegate { Raise(SkipRequested); });
            _resetItem = MakeMenuItem("重置", delegate { Raise(ResetRequested); });

            _miniItem = MakeMenuItem("迷你窗", delegate { Raise(MiniToggleRequested); });
            _showMainItem = MakeMenuItem("显示主窗口", delegate { Raise(ShowMainRequested); });

            _audioParentItem = MakeMenuItem("环境音", null);
            var soundLabels = new string[][]
            {
                new string[] { "rain", "细雨" },
                new string[] { "ocean", "海浪" },
                new string[] { "forest", "林间鸟鸣" },
                new string[] { "cafe", "咖啡馆" },
                new string[] { "fire", "篝火" },
                new string[] { "wind", "微风" },
                new string[] { "white", "白噪音" },
                new string[] { "brown", "棕噪音" },
                new string[] { "clock", "时钟滴答" }
            };
            for (int i = 0; i < soundLabels.Length; i++)
            {
                string id = soundLabels[i][0];
                string name = soundLabels[i][1];
                ToolStripMenuItem sub = MakeMenuItem(name, delegate
                {
                    var h = AmbientTrackToggleRequested;
                    if (h != null) h(this, id);
                });
                _audioItems[id] = sub;
                _audioParentItem.DropDownItems.Add(sub);
            }
            _audioParentItem.DropDownItems.Add(new ToolStripSeparator());
            _audioParentItem.DropDownItems.Add(MakeMenuItem("全部停止", delegate { Raise(AmbientStopAllRequested); }));

            _settingsItem = MakeMenuItem("设置", delegate { Raise(OpenSettingsRequested); });
            _exitItem = MakeMenuItem("退出", delegate { Raise(ExitRequested); });

            _menu.Items.Add(_headerItem);
            _menu.Items.Add(_toggleItem);
            _menu.Items.Add(_skipItem);
            _menu.Items.Add(_resetItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_miniItem);
            _menu.Items.Add(_showMainItem);
            _menu.Items.Add(_audioParentItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_settingsItem);
            _menu.Items.Add(_exitItem);
        }

        private static ToolStripMenuItem MakeMenuItem(string text, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text);
            item.AutoSize = false;
            item.Height = 32;
            item.Width = 196;
            if (onClick != null) item.Click += onClick;
            return item;
        }

        private void ApplyColorsToItems(ToolStripItemCollection items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                ToolStripItem item = items[i];
                item.BackColor = _menuBg;
                item.ForeColor = item.Enabled ? _menuText : _menuMuted;
                var mi = item as ToolStripMenuItem;
                if (mi != null && mi.DropDownItems.Count > 0)
                {
                    mi.DropDown.BackColor = _menuBg;
                    mi.DropDown.ForeColor = _menuText;
                    ApplyColorsToItems(mi.DropDownItems);
                }
            }
        }

        private void OnTrayMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Raise(LeftClicked);
            }
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private static string GetPhaseLabel(TimerSnapshot snap)
        {
            if (snap.Mode == TimerMode.Stopwatch) return "正计时";
            if (snap.Phase == TimerPhase.ShortBreak) return "短休息";
            if (snap.Phase == TimerPhase.LongBreak) return "长休息";
            return "专注中";
        }

        private static GraphicsPath CreateRoundedRect(float x, float y, float w, float h, float r)
        {
            var p = new GraphicsPath();
            float d = r * 2f;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            if (_menu != null)
            {
                _menu.Dispose();
                _menu = null;
            }
            if (_currentIcon != null)
            {
                _currentIcon.Dispose();
                _currentIcon = null;
            }
            if (_currentHIcon != IntPtr.Zero)
            {
                DestroyIcon(_currentHIcon);
                _currentHIcon = IntPtr.Zero;
            }
        }

        private sealed class ThemedColorTable : ProfessionalColorTable
        {
            private readonly Color _bg;
            private readonly Color _border;
            private readonly Color _hover;

            public ThemedColorTable(Color bg, Color border, Color hover)
            {
                _bg = bg;
                _border = border;
                _hover = hover;
            }

            public override Color ToolStripDropDownBackground { get { return _bg; } }
            public override Color ImageMarginGradientBegin { get { return _bg; } }
            public override Color ImageMarginGradientMiddle { get { return _bg; } }
            public override Color ImageMarginGradientEnd { get { return _bg; } }
            public override Color MenuBorder { get { return _border; } }
            public override Color MenuItemBorder { get { return Color.Transparent; } }
            public override Color MenuItemSelected { get { return _hover; } }
            public override Color MenuItemSelectedGradientBegin { get { return _hover; } }
            public override Color MenuItemSelectedGradientEnd { get { return _hover; } }
            public override Color SeparatorDark { get { return _border; } }
            public override Color SeparatorLight { get { return Color.Transparent; } }
        }

        private sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
        {
            private readonly Color _text;
            private readonly Color _muted;
            private readonly Color _border;

            public ThemedMenuRenderer(Color bg, Color border, Color hover, Color text, Color muted, bool isDark)
                : base(new ThemedColorTable(bg, border, hover))
            {
                _text = text;
                _muted = muted;
                _border = border;
                RoundedEdges = false;
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? _text : _muted;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                int y = e.Item.ContentRectangle.Height / 2;
                using (var pen = new Pen(_border))
                {
                    e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
                }
            }
        }
    }
}
