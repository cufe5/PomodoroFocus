using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PomodoroFocus.App.ViewModels;

namespace PomodoroFocus.App.Views
{
    public partial class StatsView : UserControl
    {
        private StatsViewModel _vm;

        public StatsView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null && _vm.Main != null)
            {
                _vm.Main.PropertyChanged -= OnMainPropertyChanged;
            }
            _vm = DataContext as StatsViewModel;
            if (_vm != null && _vm.Main != null)
            {
                _vm.Main.PropertyChanged += OnMainPropertyChanged;
                ApplyResponsiveLayout();
            }
        }

        private void OnMainPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsCompactLayout")
            {
                ApplyResponsiveLayout();
            }
        }

        private void ApplyResponsiveLayout()
        {
            bool compact = _vm != null && _vm.Main != null && _vm.Main.IsCompactLayout;
            if (compact)
            {
                DonutGapCol.Width = new GridLength(0);
                DonutCol.Width = new GridLength(0);
                Grid.SetRow(DonutCard, 1);
                Grid.SetColumn(DonutCard, 0);
                DonutCard.Margin = new Thickness(0, 16, 0, 0);
            }
            else
            {
                DonutGapCol.Width = new GridLength(16);
                DonutCol.Width = new GridLength(320);
                Grid.SetRow(DonutCard, 0);
                Grid.SetColumn(DonutCard, 2);
                DonutCard.Margin = new Thickness(0);
            }
        }
    }
}
