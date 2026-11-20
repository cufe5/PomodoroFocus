using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PomodoroFocus.App.ViewModels;

namespace PomodoroFocus.App.Views
{
    public partial class TasksView : UserControl
    {
        public TasksView()
        {
            InitializeComponent();
        }

        public void FocusQuickAddBox()
        {
            QuickAddInput.Focus();
            Keyboard.Focus(QuickAddInput);
        }

        public void FocusSearchBox()
        {
            TaskSearchBox.Focus();
            Keyboard.Focus(TaskSearchBox);
        }

        private void OnQuickAddKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as TasksViewModel;
                if (vm != null)
                {
                    vm.QuickAddCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void OnQuickAddFocusChanged(object sender, KeyboardFocusChangedEventArgs e)
        {
            QuickAddSyntaxHint.Visibility = QuickAddInput.IsKeyboardFocused ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
