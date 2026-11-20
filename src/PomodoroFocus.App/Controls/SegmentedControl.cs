using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PomodoroFocus.App.Themes;

namespace PomodoroFocus.App.Controls
{
    public sealed class SegmentOption
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string AutomationId { get; set; }
    }

    public sealed class SegmentedControl : Border
    {
        public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
            "Items", typeof(IList<SegmentOption>), typeof(SegmentedControl),
            new PropertyMetadata(null, OnItemsChanged));

        public static readonly DependencyProperty SelectedKeyProperty = DependencyProperty.Register(
            "SelectedKey", typeof(string), typeof(SegmentedControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedKeyChanged));

        public static readonly DependencyProperty UsePhaseAccentProperty = DependencyProperty.Register(
            "UsePhaseAccent", typeof(bool), typeof(SegmentedControl),
            new PropertyMetadata(false, OnStyleOptionChanged));

        public static readonly DependencyProperty SegmentHeightProperty = DependencyProperty.Register(
            "SegmentHeight", typeof(double), typeof(SegmentedControl),
            new PropertyMetadata(32.0));

        public event EventHandler<string> SelectionClicked;

        private readonly Grid _rootGrid;
        private readonly Border _indicator;
        private readonly TranslateTransform _indicatorTransform;
        private readonly UniformGrid _buttonsGrid;
        private readonly List<Button> _buttons = new List<Button>();
        private bool _indicatorInitialized;

        public SegmentedControl()
        {
            CornerRadius = new CornerRadius(20);
            Padding = new Thickness(4);
            BorderThickness = new Thickness(1);
            SetResourceReference(BackgroundProperty, "Surface.1");
            SetResourceReference(BorderBrushProperty, "Stroke.Subtle");
            SnapsToDevicePixels = true;

            _rootGrid = new Grid();
            _indicatorTransform = new TranslateTransform(0, 0);
            _indicator = new Border
            {
                CornerRadius = new CornerRadius(16),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                RenderTransform = _indicatorTransform,
                IsHitTestVisible = false
            };
            UpdateIndicatorBrush();

            _buttonsGrid = new UniformGrid { Rows = 1 };
            _rootGrid.Children.Add(_indicator);
            _rootGrid.Children.Add(_buttonsGrid);
            Child = _rootGrid;

            SizeChanged += delegate { UpdateIndicatorPosition(true); };
            Loaded += delegate { UpdateIndicatorPosition(true); };
        }

        public IList<SegmentOption> Items
        {
            get { return (IList<SegmentOption>)GetValue(ItemsProperty); }
            set { SetValue(ItemsProperty, value); }
        }

        public string SelectedKey
        {
            get { return (string)GetValue(SelectedKeyProperty); }
            set { SetValue(SelectedKeyProperty, value); }
        }

        public bool UsePhaseAccent
        {
            get { return (bool)GetValue(UsePhaseAccentProperty); }
            set { SetValue(UsePhaseAccentProperty, value); }
        }

        public double SegmentHeight
        {
            get { return (double)GetValue(SegmentHeightProperty); }
            set { SetValue(SegmentHeightProperty, value); }
        }

        private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SegmentedControl)d).RebuildButtons();
        }

        private static void OnSelectedKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (SegmentedControl)d;
            ctrl.UpdateButtonStyles();
            ctrl.UpdateIndicatorPosition(!ctrl._indicatorInitialized);
        }

        private static void OnStyleOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (SegmentedControl)d;
            ctrl.UpdateIndicatorBrush();
            ctrl.UpdateButtonStyles();
        }

        private void UpdateIndicatorBrush()
        {
            if (UsePhaseAccent)
            {
                _indicator.SetResourceReference(BackgroundProperty, "Phase.SoftBrush");
            }
            else
            {
                _indicator.SetResourceReference(BackgroundProperty, "Surface.3");
            }
        }

        private void RebuildButtons()
        {
            _buttonsGrid.Children.Clear();
            _buttons.Clear();
            _indicatorInitialized = false;

            IList<SegmentOption> list = Items;
            if (list == null || list.Count == 0) return;

            _buttonsGrid.Columns = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                SegmentOption opt = list[i];
                var btn = new Button
                {
                    Content = opt.Label,
                    Tag = opt.Key,
                    Height = SegmentHeight,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    FontSize = SegmentHeight >= 36 ? 13.5 : 12.5,
                    FontWeight = FontWeights.SemiBold,
                    Padding = new Thickness(10, 0, 10, 0),
                    Template = CreatePlainSegmentTemplate()
                };
                if (!string.IsNullOrEmpty(opt.AutomationId))
                {
                    AutomationProperties.SetAutomationId(btn, opt.AutomationId);
                }
                string capturedKey = opt.Key;
                btn.Click += delegate
                {
                    var handler = SelectionClicked;
                    if (handler != null)
                    {
                        handler(this, capturedKey);
                    }
                    else
                    {
                        SelectedKey = capturedKey;
                    }
                };
                _buttons.Add(btn);
                _buttonsGrid.Children.Add(btn);
            }

            UpdateButtonStyles();
            Dispatcher.BeginInvoke(new Action(delegate { UpdateIndicatorPosition(true); }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void UpdateButtonStyles()
        {
            string sel = SelectedKey;
            for (int i = 0; i < _buttons.Count; i++)
            {
                Button btn = _buttons[i];
                bool isSel = string.Equals(btn.Tag as string, sel, StringComparison.Ordinal);
                if (isSel)
                {
                    btn.SetResourceReference(Control.ForegroundProperty, UsePhaseAccent ? "Phase.StartBrush" : "Text.Primary");
                }
                else
                {
                    btn.SetResourceReference(Control.ForegroundProperty, "Text.Secondary");
                }
            }
        }

        private void UpdateIndicatorPosition(bool immediate)
        {
            if (_buttons.Count == 0 || _rootGrid.ActualWidth <= 0) return;

            int selIndex = -1;
            string sel = SelectedKey;
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (string.Equals(_buttons[i].Tag as string, sel, StringComparison.Ordinal))
                {
                    selIndex = i;
                    break;
                }
            }
            if (selIndex < 0) selIndex = 0;

            double segWidth = _rootGrid.ActualWidth / _buttons.Count;
            double targetX = selIndex * segWidth;

            _indicator.Width = Math.Max(0, segWidth);
            TimeSpan dur = (immediate || !_indicatorInitialized) ? TimeSpan.Zero : AnimationHelper.Duration(MotionToken.Slow);
            if (dur == TimeSpan.Zero)
            {
                _indicatorTransform.BeginAnimation(TranslateTransform.XProperty, null);
                _indicatorTransform.X = targetX;
            }
            else
            {
                var anim = new DoubleAnimation(targetX, new Duration(dur));
                anim.EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut };
                _indicatorTransform.BeginAnimation(TranslateTransform.XProperty, anim);
            }
            _indicatorInitialized = true;
        }

        private static ControlTemplate CreatePlainSegmentTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(999));

            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(cp);

            template.VisualTree = borderFactory;
            return template;
        }
    }
}
