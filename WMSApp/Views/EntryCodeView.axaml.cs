using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using WMSApp.ViewModels;

namespace WMSApp.Views;

public partial class EntryCodeView : UserControl
{
    private EntryCodeViewModel? _boundViewModel;

    public EntryCodeView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Dispatcher.UIThread.Post(() => BinBox.Focus());
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_boundViewModel != null)
        {
            _boundViewModel.FocusRequested -= OnFocusRequested;
            _boundViewModel = null;
        }
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_boundViewModel != null)
        {
            _boundViewModel.FocusRequested -= OnFocusRequested;
        }

        _boundViewModel = DataContext as EntryCodeViewModel;
        if (_boundViewModel != null)
        {
            _boundViewModel.FocusRequested += OnFocusRequested;
        }
    }

    private void OnFocusRequested(object? sender, EntryFocusTarget target)
    {
        switch (target)
        {
            case EntryFocusTarget.ConfirmBinBox:
                ConfirmBinBox.Focus();
                break;
            case EntryFocusTarget.BinBox:
                BinBox.Focus();
                break;
            case EntryFocusTarget.CodeBox:
                CodeBox.Focus();
                break;
        }
    }

    private void BinBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        CodeBox.Focus();
    }

    private async void CodeBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;

        if (DataContext is EntryCodeViewModel vm)
        {
            if (vm.LightShelfCommand.CanExecute(null))
            {
                await vm.LightShelfCommand.ExecuteAsync(null);
            }

            if (vm.IsWaitingConfirm)
            {
                ConfirmBinBox.Focus();
            }
        }
    }

    private void ConfirmBinBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        if (DataContext is EntryCodeViewModel vm && vm.ConfirmAndStoreCommand.CanExecute(null))
        {
            vm.ConfirmAndStoreCommand.Execute(null);
        }
    }
}
