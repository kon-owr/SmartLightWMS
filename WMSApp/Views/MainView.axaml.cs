using Avalonia.Controls;
using Avalonia.Controls;
using Avalonia.Interactivity;
using WMSApp.ViewModels;

namespace WMSApp.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    // 1. 当页面加载到屏幕时，订阅系统返回键事件
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // 获取当前控件所在的顶层窗口（在 Android 上对应 Activity）
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            topLevel.BackRequested += OnBackRequested;
        }
    }

    // 2. 当页面卸载时，取消订阅（防止内存泄漏）
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            topLevel.BackRequested -= OnBackRequested;
        }
    }

    // 3. 真正的处理逻辑
    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        // 尝试获取绑定的 ViewModel
        if (DataContext is MainViewModel vm)
        {
            // 检查此时此刻，能不能“后退”？
            // 这一步利用了我们在上一问中写的 CanGoBack 逻辑
            if (vm.GoBackCommand.CanExecute(null))
            {
                // 执行后退
                vm.GoBackCommand.Execute(null);

                // 🚨 关键一步：标记事件为“已处理”
                // 如果不写这行，Android 系统会认为你没响应，默认行为是“退出/最小化 App”
                e.Handled = true;
            }
            // 如果不能后退（比如已经在首页了），我们就不设置 Handled
            // 这样系统就会执行默认操作：把 App 挂起到后台
        }
    }
}