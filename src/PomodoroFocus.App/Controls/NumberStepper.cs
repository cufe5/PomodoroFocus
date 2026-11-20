using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace PomodoroFocus.App.Controls
{
    public sealed class NumberStepper : Border
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value", typeof(int), typeof(NumberStepper),
            new FrameworkPropertyMetadata(25, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
            "Minimum", typeof(int), typeof(NumberStepper), new PropertyMetadata(1));

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            "Maximum", typeof(int), typeof(NumberStepper), new PropertyMetadata(180));

        public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
            "Step", typeof(int), typeof(NumberStepper), new PropertyMetadata(1));

        private readonly TextBox _inputBox;
        private readonly RepeatButton _minusBtn;
        private readonly RepeatButton _plusBtn;
        private bool _updatingText;

        public NumberStepper()
        {
            Width = 128;
            Height = 36;
            CornerRadius = new CornerRadius(10);
            BorderThickness = new Thickness(1);
            SetResourceReference(BackgroundProperty, "Surface.3");
            SetResourceReference(BorderBrushProperty, "Stroke.Subtle");
            Padding = new Thickness(4, 0, 4, 0);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

            _minusBtn = CreateStepButton("\u2212");
            _minusBtn.Click += delegate { Adjust(-Step); };
            Grid.SetColumn(_minusBtn, 0);

            _inputBox = new TextBox
            {
                Text = "25",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            };
            _inputBox.SetResourceReference(Control.ForegroundProperty, "Text.Primary");
            _inputBox.SetResourceReference(TextBoxBase.CaretBrushProperty, "Accent");
            Typography.SetNumeralAlignment(_inputBox, FontNumeralAlignment.Tabular);
            _inputBox.PreviewTextInput += OnPreviewTextInput;
            _inputBox.LostFocus += delegate { CommitText(); };
            _inputBox.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    CommitText();
                    e.Handled = true;
                }
            };
            Grid.SetColumn(_inputBox, 1);

            _plusBtn = CreateStepButton("+");
            _plusBtn.Click += delegate { Adjust(Step); };
            Grid.SetColumn(_plusBtn, 2);

            grid.Children.Add(_minusBtn);
            grid.Children.Add(_inputBox);
            grid.Children.Add(_plusBtn);
            Child = grid;
        }

        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, Clamp(value)); }
        }

        public int Minimum
        {
            get { return (int)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public int Maximum
        {
            get { return (int)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public int Step
        {
            get { return (int)GetValue(StepProperty); }
            set { SetValue(StepProperty, value); }
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (NumberStepper)d;
            int v = ctrl.Clamp((int)e.NewValue);
            ctrl._updatingText = true;
            ctrl._inputBox.Text = v.ToString(CultureInfo.InvariantCulture);
            ctrl._updatingText = false;
            ctrl._minusBtn.IsEnabled = v > ctrl.Minimum;
            ctrl._plusBtn.IsEnabled = v < ctrl.Maximum;
        }

        private void Adjust(int delta)
        {
            Value = Clamp(Value + delta);
        }

        private void CommitText()
        {
            if (_updatingText) return;
            int parsed;
            if (int.TryParse(_inputBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                Value = Clamp(parsed);
            }
            _updatingText = true;
            _inputBox.Text = Value.ToString(CultureInfo.InvariantCulture);
            _updatingText = false;
        }

        private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            for (int i = 0; i < e.Text.Length; i++)
            {
                if (!char.IsDigit(e.Text[i]))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private int Clamp(int v)
        {
            if (v < Minimum) return Minimum;
            if (v > Maximum) return Maximum;
            return v;
        }

        private RepeatButton CreateStepButton(string label)
        {
            var btn = new RepeatButton
            {
                Content = label,
                Width = 28,
                Height = 28,
                Delay = 400,
                Interval = 80,
                Cursor = Cursors.Hand,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            btn.SetResourceReference(Control.ForegroundProperty, "Text.Secondary");

            var tpl = new ControlTemplate(typeof(RepeatButton));
            var bd = new FrameworkElementFactory(typeof(Border));
            bd.Name = "BtnBd";
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            bd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            tpl.VisualTree = bd;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("Surface.2"), "BtnBd"));
            hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("Text.Primary")));
            tpl.Triggers.Add(hoverTrigger);

            btn.Template = tpl;
            return btn;
        }
    }
}
