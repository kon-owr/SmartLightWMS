using Avalonia.Controls;
using Avalonia.Input;
using WMSApp.ViewModels;

namespace WMSApp.Views;

public partial class PickingCodeView : UserControl
{
    public PickingCodeView()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 页面卸载时释放当前文档状态，避免残留锁和亮灯记录。
    /// </summary>
    private async void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is PickingCodeViewModel vm)
        {
            await vm.ResetCurrentDocAsync();
        }
    }

    /// <summary>
    /// 在搜索框按下回车时直接触发查询命令。
    /// </summary>
    private async void SearchTextBox_OnClick(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        if (DataContext is PickingCodeViewModel vm && vm.SearchCodeCommand.CanExecute(null))
        {
            await vm.SearchCodeCommand.ExecuteAsync(null);
        }
    }
}
