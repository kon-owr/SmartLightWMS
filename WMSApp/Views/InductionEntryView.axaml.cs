using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WMSApp.ViewModels;

namespace WMSApp.Views
{
    public partial class InductionEntryView : UserControl
    {
        public InductionEntryView()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (DataContext is InductionEntryViewModel vm)
            {
                vm.FocusRequested += OnFocusRequested;
                _ = vm.OnPageOpenedAsync();
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            if (DataContext is InductionEntryViewModel vm)
            {
                vm.FocusRequested -= OnFocusRequested;
                _ = vm.OnPageClosedAsync();
            }
        }

        private void OnFocusRequested(object? sender, InductionEntryFocusTarget target)
        {
            TextBox? box = target switch
            {
                InductionEntryFocusTarget.BarcodeBox => BarcodeBox,
                _ => null
            };

            box?.Focus();
        }

        private void ShelfBox_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is InductionEntryViewModel vm)
            {
                vm.ValidateShelfCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void BarcodeBox_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is InductionEntryViewModel vm)
            {
                vm.DepositCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
