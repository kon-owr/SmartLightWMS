using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace WMSApp.ViewModels;

/// <summary>
/// 管理主界面的页面导航、返回栈和退出前页面清理。
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    /// <summary>
    /// 用于按需解析各业务页面 ViewModel。
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 保存导航历史，供返回按钮恢复上一页。
    /// </summary>
    private Stack<ViewModelBase> _historyView = new();

    /// <summary>
    /// 当前显示的页面 ViewModel。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand))]
    [NotifyPropertyChangedFor(nameof(ShowBackButton))]
    private ViewModelBase? _currentPage;

    /// <summary>
    /// 提供设计器或框架创建实例时使用的空构造函数。
    /// </summary>
    public MainViewModel()
    {
    }

    /// <summary>
    /// 初始化主导航依赖并进入首页。
    /// </summary>
    public MainViewModel(IServiceProvider serviceProvider):this()
    {
        _serviceProvider = serviceProvider;
        NavigateToHome();
    }

    /// <summary>
    /// 切换当前页面，并在普通导航时记录返回历史。
    /// </summary>
    private void NavigateTo(ViewModelBase newPage, bool isGoingBack = false)
    {
        if (CurrentPage != null && !isGoingBack)
        {
            _historyView.Push(CurrentPage);
        }

        CurrentPage = newPage;
    }

    /// <summary>
    /// 返回首页并清空历史栈，避免首页继续回退到旧业务页。
    /// </summary>
    [RelayCommand]
    public void NavigateToHome()
    {
        _historyView.Clear();
        var homeViewModel = _serviceProvider.GetRequiredService<HomeViewModel>();
        NavigateTo(homeViewModel);
    }


    /// <summary>
    /// 导航到普通拣货页面。
    /// </summary>
    [RelayCommand]
    public void NavigateToPickingCode()
    {
        var pickingCodeViewModel = _serviceProvider.GetRequiredService<PickingCodeViewModel>();
        NavigateTo(pickingCodeViewModel);
    }

    /// <summary>
    /// 导航到普通入库页面。
    /// </summary>
    [RelayCommand]
    public void NavigateToEntryCode()
    {
        var entryCodeViewModel = _serviceProvider.GetRequiredService<EntryCodeViewModel>();
        NavigateTo(entryCodeViewModel);
    }

    /// <summary>
    /// 导航到感应入库页面。
    /// </summary>
    [RelayCommand]
    public void NavigateToInductionEntry()
    {
        var inductionEntryViewModel = _serviceProvider.GetRequiredService<InductionEntryViewModel>();
        NavigateTo(inductionEntryViewModel);
    }

    /// <summary>
    /// 导航到感应拣货页面。
    /// </summary>
    [RelayCommand]
    public void NavigateToInductionPick()
    {
        var inductionPickViewModel = _serviceProvider.GetRequiredService<InductionPickViewModel>();
        NavigateTo(inductionPickViewModel);
    }

    public bool ShowBackButton => _historyView.Count > 0;

    /// <summary>
    /// 判断返回命令是否可用。
    /// </summary>
    private bool CanGoBack() => _historyView.Count > 0;

    /// <summary>
    /// 返回上一页，并避免返回过程再次压入历史栈。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    public void GoBack()
    {
        if (_historyView.TryPop(out var previousPage))
        {
            // 取出上一个页面，并标记 isGoingBack=true 防止死循环压栈
            NavigateTo(previousPage, isGoingBack: true);
        }
    }

    /// <summary>
    /// 应用退出前通知当前页面释放锁定、Hub 连接或灯光等外部状态。
    /// </summary>
    public async Task CleanupForExitAsync()
    {
        if (CurrentPage is IPageLifecycleAware pageLifecycleAware)
        {
            await pageLifecycleAware.OnPageClosedAsync();
        }

        if (CurrentPage is PickingCodeViewModel pickingCodeViewModel)
        {
            await pickingCodeViewModel.ResetCurrentDocAsync();
        }
    }
}
