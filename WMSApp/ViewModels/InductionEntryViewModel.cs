using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using WMSApp.DTO;
using WMSApp.Models;
using WMSApp.Services;

namespace WMSApp.ViewModels
{
    public enum InductionEntryState { Idle, WaitingDeposit }

    public enum InductionEntryFocusTarget { BarcodeBox }

    /// <summary>
    /// 管理感应入库页面的料架验证、条码入库、亮灯和回调状态。
    /// </summary>
    public partial class InductionEntryViewModel : ViewModelBase, IPageLifecycleAware
    {
        /// <summary>
        /// 提供料架验证、入库请求和取消入库的后端 API 客户端。
        /// </summary>
        private readonly IInductionEntryApiService _entryApiService;

        /// <summary>
        /// 接收感应料架入库回调的 SignalR 客户端。
        /// </summary>
        private readonly IInductionHubService _hubService;

        /// <summary>
        /// 控制感应料架空库位亮灯和熄灯。
        /// </summary>
        private readonly IInductionLightService _inductionLightService;

        /// <summary>
        /// 保存本页已完成入库的条码记录，用于列表展示。
        /// </summary>
        private readonly ObservableCollection<DepositedItem> _depositedItems = new();

        /// <summary>
        /// 当前客户端允许操作的仓库集合，用于入库前校验仓库隔离。
        /// </summary>
        private readonly HashSet<string> _allowedWarehouses;

        /// <summary>
        /// 标记页面是否处于打开状态，防止关闭后继续处理 Hub 回调。
        /// </summary>
        private bool _isPageActive;

        /// <summary>
        /// 标记 Hub 回调事件是否已绑定，避免重复订阅。
        /// </summary>
        private bool _hubHandlersAttached;

        /// <summary>
        /// 保存当前空库位提示灯颜色。
        /// </summary>
        private LightColorCode LightColor { get; set; } = LightColorCode.Green;

        /// <summary>
        /// 料架输入框内容，用于发起料架验证。
        /// </summary>
        [ObservableProperty]
        private string _inputShelf = string.Empty;

        /// <summary>
        /// 条码输入框内容，用于发起感应入库。
        /// </summary>
        [ObservableProperty]
        private string _inputBarcode = string.Empty;

        /// <summary>
        /// 当前入库仓库编码，用于后端校验和仓库隔离。
        /// </summary>
        [ObservableProperty]
        private string _warehouseLocation = string.Empty;

        /// <summary>
        /// 页面业务状态，控制料架验证、条码输入和取消按钮可用性。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsWaitingDeposit))]
        [NotifyPropertyChangedFor(nameof(CanInputBarcode))]
        [NotifyPropertyChangedFor(nameof(CanValidateShelf))]
        private InductionEntryState _currentState = InductionEntryState.Idle;

        /// <summary>
        /// 页面提示正文，来自校验、入库、取消和回调结果。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMessage))]
        [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
        private string _statusMessage = string.Empty;

        /// <summary>
        /// 页面提示级别，用于组合提示文本和样式。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
        private MessageSeverity _messageSeverity = MessageSeverity.Info;

        /// <summary>
        /// 控制提示区域是否展示，并刷新组合后的提示文本。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMessage))]
        [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
        private bool _isMessageVisible;

        /// <summary>
        /// 当前验证料架可用的空库位数量。
        /// </summary>
        [ObservableProperty]
        private int _emptyLocationCount;

        /// <summary>
        /// 已通过后端验证的料架号，用于入库和熄灯回滚。
        /// </summary>
        [ObservableProperty]
        private string _validatedShelfCode = string.Empty;

        /// <summary>
        /// 标记接口请求是否执行中，用于阻止重复提交命令。
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ValidateShelfCommand))]
        [NotifyCanExecuteChangedFor(nameof(DepositCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelDepositCommand))]
        private bool _isBusy;

        public bool IsWaitingDeposit => CurrentState == InductionEntryState.WaitingDeposit;
        public bool CanInputBarcode => CurrentState == InductionEntryState.Idle && !string.IsNullOrEmpty(ValidatedShelfCode);
        public bool CanValidateShelf => CurrentState == InductionEntryState.Idle;
        public bool HasMessage => IsMessageVisible && !string.IsNullOrWhiteSpace(StatusMessage);
        public string StatusDisplayText => HasMessage ? $"[{GetSeverityLabel(MessageSeverity)}] {StatusMessage}" : string.Empty;
        public DataGridCollectionView GridData { get; }

        public event EventHandler<InductionEntryFocusTarget>? FocusRequested;

        /// <summary>
        /// 初始化感应入库依赖、仓库配置和入库记录列表视图。
        /// </summary>
        public InductionEntryViewModel(
            IInductionEntryApiService entryApiService,
            IInductionHubService hubService,
            IInductionLightService inductionLightService,
            IConfiguration configuration)
        {
            _entryApiService = entryApiService;
            _hubService = hubService;
            _inductionLightService = inductionLightService;
            _allowedWarehouses = configuration
                .GetSection("Warehouses:Allowed")
                .GetChildren()
                .Select(section => section.Value?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            WarehouseLocation = configuration["Warehouses:Current"]?.Trim()
                ?? _allowedWarehouses.FirstOrDefault()
                ?? string.Empty;

            GridData = new DataGridCollectionView(_depositedItems);
        }

        /// <summary>
        /// 页面打开时连接 Hub、绑定回调，并恢复当前已验证料架的空位亮灯。
        /// </summary>
        public async Task OnPageOpenedAsync()
        {
            if (_isPageActive)
            {
                return;
            }

            _isPageActive = true;
            AttachHubHandlers();

            try
            {
                await _hubService.StartAsync();

                if (!EnsureWarehouseConfigured())
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(ValidatedShelfCode))
                {
                    await ApplyShelfLightAsync(ValidatedShelfCode, (int)LightColor);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"连接感应回调失败：{ex.Message}", MessageSeverity.Error);
            }
        }

        /// <summary>
        /// 页面关闭时解绑 Hub 回调、熄灭当前料架灯光并关闭连接。
        /// </summary>
        public async Task OnPageClosedAsync()
        {
            if (!_isPageActive)
            {
                return;
            }

            _isPageActive = false;
            DetachHubHandlers();

            try
            {
                if (!string.IsNullOrWhiteSpace(ValidatedShelfCode))
                {
                    await _inductionLightService.LightOffAllEmptyLocationAsync(ValidatedShelfCode);
                }
            }
            finally
            {
                if (_hubService.IsConnected)
                {
                    await _hubService.StopAsync();
                }
            }
        }

        /// <summary>
        /// 绑定入库回调事件，确保页面打开期间能收到料架结果。
        /// </summary>
        private void AttachHubHandlers()
        {
            if (_hubHandlersAttached)
            {
                return;
            }

            _hubService.DepositCallbackReceived += OnDepositCallbackReceived;
            _hubHandlersAttached = true;
        }

        /// <summary>
        /// 解绑入库回调事件，避免页面关闭后继续更新 UI。
        /// </summary>
        private void DetachHubHandlers()
        {
            if (!_hubHandlersAttached)
            {
                return;
            }

            _hubService.DepositCallbackReceived -= OnDepositCallbackReceived;
            _hubHandlersAttached = false;
        }

        /// <summary>
        /// 处理料架入库回调，按成功或失败结果刷新列表、提示和灯光状态。
        /// </summary>
        private void OnDepositCallbackReceived(object? sender, DepositCallbackMessage message)
        {
            if (!_isPageActive)
            {
                return;
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CurrentState = InductionEntryState.Idle;

                if (message.Success)
                {
                    _depositedItems.Add(new DepositedItem
                    {
                        BarNo = message.LabelId,
                        BinNo = message.Location,
                        DepositTime = DateTime.Now,
                        Status = 1
                    });

                    InputBarcode = string.Empty;
                    ShowMessage($"条码 {message.LabelId} 入库成功，库位：{message.Location}", MessageSeverity.Success);
                    FocusRequested?.Invoke(this, InductionEntryFocusTarget.BarcodeBox);
                    _ = ApplyShelfLightAsync(ValidatedShelfCode, (int)LightColor);
                    return;
                }

                ShowMessage($"入库失败：{message.Message}", MessageSeverity.Error);
                _ = ApplyShelfLightAsync(ValidatedShelfCode, (int)LightColorCode.Red);
            });
        }

        /// <summary>
        /// 验证料架并点亮所有空库位，为后续扫码入库建立目标范围。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanValidateShelfExec))]
        private async Task ValidateShelf()
        {
            if (IsBusy)
            {
                return;
            }

            if (!EnsureWarehouseConfigured())
            {
                return;
            }

            IsBusy = true;
            ClearMessage();

            try
            {
                var shelfCode = InputShelf?.Trim();
                if (string.IsNullOrWhiteSpace(shelfCode))
                {
                    ShowMessage("请输入料架号。", MessageSeverity.Warning);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(ValidatedShelfCode)
                    && !string.Equals(ValidatedShelfCode, shelfCode, StringComparison.OrdinalIgnoreCase))
                {
                    await _inductionLightService.LightOffAllEmptyLocationAsync(ValidatedShelfCode);
                }

                var result = await _entryApiService.ValidateShelfAsync(shelfCode, WarehouseLocation);
                if (!result.Success || result.Data == null)
                {
                    ShowMessage(result.Message ?? "料架验证失败。", MessageSeverity.Error);
                    return;
                }

                if (!result.Data.IsValid)
                {
                    ShowMessage(result.Data.ErrorMessage ?? "料架无效。", MessageSeverity.Error);
                    return;
                }

                ValidatedShelfCode = result.Data.ShelfCode;
                EmptyLocationCount = result.Data.EmptyLocationCount;
                await ApplyShelfLightAsync(ValidatedShelfCode, (int)LightColor);

                ShowMessage($"料架验证成功，空库位数：{EmptyLocationCount}", MessageSeverity.Success);
                FocusRequested?.Invoke(this, InductionEntryFocusTarget.BarcodeBox);
            }
            catch (Exception ex)
            {
                ShowMessage($"验证失败：{ex.Message}", MessageSeverity.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 判断料架验证命令是否可执行。
        /// </summary>
        private bool CanValidateShelfExec() => !IsBusy && CurrentState == InductionEntryState.Idle;

        /// <summary>
        /// 提交条码入库请求，并进入等待料架回调状态。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDepositExec))]
        private async Task Deposit()
        {
            if (IsBusy)
            {
                return;
            }

            if (!EnsureWarehouseConfigured())
            {
                return;
            }

            IsBusy = true;
            ClearMessage();

            try
            {
                var barcode = InputBarcode?.Trim();
                if (string.IsNullOrWhiteSpace(barcode))
                {
                    ShowMessage("请扫描条码。", MessageSeverity.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(ValidatedShelfCode))
                {
                    ShowMessage("请先验证料架。", MessageSeverity.Warning);
                    return;
                }

                var result = await _entryApiService.DepositAsync(barcode, ValidatedShelfCode, WarehouseLocation);
                if (!result.Success)
                {
                    ShowMessage(result.Message ?? "入库请求失败。", MessageSeverity.Error);
                    return;
                }

                CurrentState = InductionEntryState.WaitingDeposit;
                ShowMessage($"条码 {barcode} 已发送入库请求，等待料架响应...", MessageSeverity.Info);
            }
            catch (Exception ex)
            {
                ShowMessage($"入库失败：{ex.Message}", MessageSeverity.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 判断入库命令是否可执行。
        /// </summary>
        private bool CanDepositExec() => !IsBusy && !string.IsNullOrEmpty(ValidatedShelfCode) && CurrentState == InductionEntryState.Idle;

        /// <summary>
        /// 取消当前等待中的入库请求，并恢复扫码焦点。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCancelDepositExec))]
        private async Task CancelDeposit()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var barcode = InputBarcode?.Trim();
                if (!string.IsNullOrWhiteSpace(barcode))
                {
                    var result = await _entryApiService.CancelDepositAsync(barcode);
                    if (!result.Success)
                    {
                        ShowMessage(result.Message ?? "取消入库失败。", MessageSeverity.Error);
                        return;
                    }
                }

                CurrentState = InductionEntryState.Idle;
                InputBarcode = string.Empty;
                ShowMessage("已取消入库。", MessageSeverity.Info);
                FocusRequested?.Invoke(this, InductionEntryFocusTarget.BarcodeBox);
            }
            catch (Exception ex)
            {
                ShowMessage($"取消失败：{ex.Message}", MessageSeverity.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 判断取消入库命令是否可执行。
        /// </summary>
        private bool CanCancelDepositExec() => !IsBusy && CurrentState == InductionEntryState.WaitingDeposit;

        /// <summary>
        /// 切换空库位亮灯颜色，并在已有验证料架时立即重新点亮。
        /// </summary>
        [RelayCommand]
        private async Task TagClick(string color)
        {
            if (!Enum.TryParse<LightColorCode>(color, true, out var code))
            {
                return;
            }

            LightColor = code;
            ShowMessage($"已切换颜色为：{GetColorDisplayName(code)}", MessageSeverity.Success);

            if (!string.IsNullOrWhiteSpace(ValidatedShelfCode))
            {
                await ApplyShelfLightAsync(ValidatedShelfCode, (int)code);
            }
        }

        /// <summary>
        /// 重置页面输入、状态和已入库列表，并关闭当前料架灯光。
        /// </summary>
        [RelayCommand]
        private async Task ResetAll()
        {
            await TurnOffCurrentShelfLightsAsync();

            _depositedItems.Clear();
            ValidatedShelfCode = string.Empty;
            InputShelf = string.Empty;
            InputBarcode = string.Empty;
            EmptyLocationCount = 0;
            CurrentState = InductionEntryState.Idle;
            ClearMessage();
        }

        /// <summary>
        /// 对指定料架的空库位执行亮灯，并将失败信息展示到页面。
        /// </summary>
        private async Task ApplyShelfLightAsync(string shelfCode, int color)
        {
            if (string.IsNullOrWhiteSpace(shelfCode))
            {
                return;
            }

            var response = await _inductionLightService.LightOnAllEmptyLocationAsync(shelfCode, color);
            if (IsFailureMessage(response))
            {
                ShowMessage(response, MessageSeverity.Error);
            }
        }

        /// <summary>
        /// 熄灭当前已验证料架的空库位灯光，用于页面关闭或重置。
        /// </summary>
        private async Task TurnOffCurrentShelfLightsAsync()
        {
            if (string.IsNullOrWhiteSpace(ValidatedShelfCode))
            {
                return;
            }

            var response = await _inductionLightService.LightOffAllEmptyLocationAsync(ValidatedShelfCode);
            if (IsFailureMessage(response))
            {
                ShowMessage(response, MessageSeverity.Error);
            }
        }

        /// <summary>
        /// 校验当前仓库配置是否完整且属于允许列表。
        /// </summary>
        private bool EnsureWarehouseConfigured()
        {
            if (string.IsNullOrWhiteSpace(WarehouseLocation))
            {
                ShowMessage("未配置当前仓库编码。", MessageSeverity.Error);
                return false;
            }

            if (_allowedWarehouses.Count == 0)
            {
                ShowMessage("未配置允许仓库列表。", MessageSeverity.Error);
                return false;
            }

            if (!_allowedWarehouses.Contains(WarehouseLocation))
            {
                ShowMessage($"当前仓库 {WarehouseLocation} 不在允许列表中。", MessageSeverity.Error);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 判断料架接口文本响应是否表示失败或超时。
        /// </summary>
        private static bool IsFailureMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return true;
            }

            return message.Contains("失败", StringComparison.OrdinalIgnoreCase)
                || message.Contains("超时", StringComparison.OrdinalIgnoreCase)
                || message.Contains("fail", StringComparison.OrdinalIgnoreCase)
                || message.Contains("error", StringComparison.OrdinalIgnoreCase)
                || message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 写入页面提示并显示消息区域。
        /// </summary>
        private void ShowMessage(string message, MessageSeverity severity)
        {
            StatusMessage = message;
            MessageSeverity = severity;
            IsMessageVisible = true;
        }

        /// <summary>
        /// 清空页面提示并隐藏消息区域。
        /// </summary>
        private void ClearMessage()
        {
            StatusMessage = string.Empty;
            IsMessageVisible = false;
        }

        /// <summary>
        /// 将消息级别转换为中文提示前缀。
        /// </summary>
        private static string GetSeverityLabel(MessageSeverity severity)
        {
            return severity switch
            {
                MessageSeverity.Success => "成功",
                MessageSeverity.Warning => "警告",
                MessageSeverity.Error => "错误",
                _ => "提示"
            };
        }

        /// <summary>
        /// 获取灯光颜色的中文说明，用于颜色切换反馈。
        /// </summary>
        private string GetColorDisplayName(LightColorCode code)
        {
            var field = typeof(LightColorCode).GetField(code.ToString());
            var description = field?.GetCustomAttribute<DescriptionAttribute>();
            return description?.Description ?? code.ToString();
        }
    }
}
