using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ursa.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;
using WMSApp.DTO;
using WMSApp.Services;

namespace WMSApp.ViewModels
{
    /// <summary>
    /// 管理首页导航入口和应用启动后的更新检查提示。
    /// </summary>
    public partial class HomeViewModel : ViewModelBase
    {
        /// <summary>
        /// 用于解析主导航 ViewModel 并触发页面切换。
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 提供应用更新检查的后端 API 客户端。
        /// </summary>
        private readonly IUpdateApiService _updateApiService;

        /// <summary>
        /// 保存本次会话的更新检查状态，避免首页重复弹窗。
        /// </summary>
        private readonly IUpdateSessionState _updateSessionState;

        /// <summary>
        /// 负责下载并安装更新包的平台代理实现。
        /// </summary>
        private readonly IAppUpdateInstaller _appUpdateInstaller;

        /// <summary>
        /// 缓存待安装的更新信息，供用户点击下载时使用。
        /// </summary>
        private UpdateCheckResponse? _pendingUpdate;

        /// <summary>
        /// 首页展示的当前程序版本文本。
        /// </summary>
        [ObservableProperty]
        private string _appVersionText = "程序版本：V1.0.0";

        /// <summary>
        /// 控制更新提示弹层是否显示。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowUpdateCancelButton))]
        private bool _isUpdatePromptVisible;

        /// <summary>
        /// 标记当前更新是否为强制更新，决定是否允许用户取消。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowUpdateCancelButton))]
        private bool _isForceUpdate;

        /// <summary>
        /// 更新提示弹层标题。
        /// </summary>
        [ObservableProperty]
        private string _updatePromptTitle = "发现新版本";

        /// <summary>
        /// 更新提示弹层正文，包含版本号和更新说明。
        /// </summary>
        [ObservableProperty]
        private string _updatePromptMessage = string.Empty;

        public bool ShowUpdateCancelButton => IsUpdatePromptVisible && !IsForceUpdate;

        /// <summary>
        /// 初始化首页依赖并计算当前应用版本展示文本。
        /// </summary>
        public HomeViewModel(
            IServiceProvider serviceProvider,
            IUpdateApiService updateApiService,
            IUpdateSessionState updateSessionState,
            IAppUpdateInstaller appUpdateInstaller)
        {
            _serviceProvider = serviceProvider;
            _updateApiService = updateApiService;
            _updateSessionState = updateSessionState;
            _appUpdateInstaller = appUpdateInstaller;

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                AppVersionText = $"程序版本：V{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
            }
        }

        /// <summary>
        /// 首页进入时执行一次更新检查，并按结果展示可选或强制更新提示。
        /// </summary>
        public async Task OnHomeEnteredAsync()
        {
            if (_updateSessionState.IsChecking || _updateSessionState.HasCheckedOnHomeEnter)
            {
                return;
            }

            _updateSessionState.IsChecking = true;
            try
            {
                var currentVersionCode = GetCurrentVersionCode();
                var result = await _updateApiService.CheckAsync("wmsapp", "android", currentVersionCode);

                _updateSessionState.HasCheckedOnHomeEnter = true;
                if (!result.Success || result.Data == null)
                {
                    await MessageBox.ShowOverlayAsync(result.Message ?? "检查更新失败", "提示", null, MessageBoxIcon.Warning);
                    return;
                }

                _updateSessionState.LastResult = result.Data;
                if (!result.Data.HasUpdate)
                {
                    return;
                }

                if (!result.Data.ForceUpdate && _updateSessionState.OptionalUpdateDismissed)
                {
                    return;
                }

                _pendingUpdate = result.Data;
                IsForceUpdate = result.Data.ForceUpdate;
                UpdatePromptTitle = result.Data.ForceUpdate ? "发现强制更新" : "发现新版本";
                UpdatePromptMessage = BuildPromptMessage(result.Data);
                IsUpdatePromptVisible = true;
            }
            finally
            {
                _updateSessionState.IsChecking = false;
            }
        }

        /// <summary>
        /// 从首页导航到普通拣货页面。
        /// </summary>
        [RelayCommand]
        private void NavigateToPickingCode()
        {
            var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
            mainVm.NavigateToPickingCode();
        }

        /// <summary>
        /// 从首页导航到普通入库页面。
        /// </summary>
        [RelayCommand]
        public void NavigateToEntryCode()
        {
            var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
            mainVm.NavigateToEntryCode();
        }

        /// <summary>
        /// 从首页导航到感应入库页面。
        /// </summary>
        [RelayCommand]
        public void NavigateToInductionEntry()
        {
            var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
            mainVm.NavigateToInductionEntry();
        }

        /// <summary>
        /// 从首页导航到感应拣货页面。
        /// </summary>
        [RelayCommand]
        public void NavigateToInductionPick()
        {
            var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
            mainVm.NavigateToInductionPick();
        }

        /// <summary>
        /// 关闭可选更新提示，并记录本次会话已忽略。
        /// </summary>
        [RelayCommand]
        private void CancelUpdatePrompt()
        {
            if (IsForceUpdate)
            {
                return;
            }

            _updateSessionState.OptionalUpdateDismissed = true;
            IsUpdatePromptVisible = false;
        }

        /// <summary>
        /// 下载并安装当前待处理更新，失败时展示提示。
        /// </summary>
        [RelayCommand]
        private async Task DownloadUpdateAsync()
        {
            if (_pendingUpdate == null)
            {
                await MessageBox.ShowOverlayAsync("更新信息不存在，请稍后重试。", "提示", null, MessageBoxIcon.Warning);
                return;
            }

            var result = await _appUpdateInstaller.DownloadAndInstallAsync(_pendingUpdate);
            if (!result.Success)
            {
                await MessageBox.ShowOverlayAsync(result.Message ?? "下载失败", "更新失败", null, MessageBoxIcon.Warning);
                return;
            }

            await MessageBox.ShowOverlayAsync(result.Message ?? "已开始下载并安装。", "更新提示", null, MessageBoxIcon.Information);
            if (!IsForceUpdate)
            {
                IsUpdatePromptVisible = false;
            }
        }

        /// <summary>
        /// 将程序集版本转换为后端用于比较的整数版本号。
        /// </summary>
        private int GetCurrentVersionCode()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null)
            {
                return 0;
            }

            var major = Math.Max(version.Major, 0);
            var minor = Math.Max(version.Minor, 0);
            var build = Math.Max(version.Build, 0);
            return (major * 10000) + (minor * 100) + build;
        }

        /// <summary>
        /// 根据更新响应生成弹层正文。
        /// </summary>
        private static string BuildPromptMessage(UpdateCheckResponse response)
        {
            var notes = string.IsNullOrWhiteSpace(response.ReleaseNotes)
                ? "暂无更新说明"
                : response.ReleaseNotes;

            var prefix = response.ForceUpdate
                ? "当前版本过低，必须更新后才能继续使用。"
                : "检测到新版本，建议尽快更新。";

            return $"{prefix}\n版本：{response.LatestVersionName} ({response.LatestVersionCode})\n说明：{notes}";
        }
    }
}
