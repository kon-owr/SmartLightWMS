using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WMSApp.ViewModels;

namespace WMSApp.Views
{
    public partial class InductionPickView : UserControl
    {
        public InductionPickView()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (DataContext is InductionPickViewModel vm)
            {
                vm.FocusRequested += OnFocusRequested;
                _ = vm.OnPageOpenedAsync();
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            if (DataContext is InductionPickViewModel vm)
            {
                vm.FocusRequested -= OnFocusRequested;
                _ = vm.OnPageClosedAsync();
            }
        }

        private void OnFocusRequested(object? sender, InductionPickFocusTarget target)
        {
            TextBox? box = target switch
            {
                InductionPickFocusTarget.ItemNoBox => ItemNoBox,
                _ => null
            };

            box?.Focus();
        }

        private void ItemNoBox_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is InductionPickViewModel vm)
            {
                vm.SearchCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void QtyBox_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is InductionPickViewModel vm)
            {
                vm.SearchCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void SuggestionList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListBox listBox || listBox.SelectedItem is not string itemNo)
            {
                return;
            }

            if (DataContext is InductionPickViewModel vm)
            {
                vm.SelectSuggestionCommand.Execute(itemNo);
            }

            listBox.SelectedItem = null;
        }
    }
}
