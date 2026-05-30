using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using WMSApp.DTO;
using WMSApp.Models;
using WMSApp.Services;

namespace WMSApp.ViewModels
{
    public enum MessageSeverity { Success, Warning, Error, Info }

    public enum EntryFlowState { Idle, WaitingConfirm }

    public enum EntryFocusTarget { ConfirmBinBox, BinBox, CodeBox }

    /// <summary>
    /// 管理普通入库页面的扫码分配、亮灯、确认入库和取消流程。
    /// </summary>
    partial class EntryCodeViewModel : ViewModelBase
    {
        /// <summary>
        /// 负责普通料架库位亮灯和熄灯。
        /// </summary>
        private readonly IWMSLightService _lightService;

        /// <summary>
        /// 负责普通入库分配和提交接口调用。
        /// </summary>
        private readonly IEntryApiService _entryApiService;

        /// <summary>
        /// 保存当前待确认入库的条码与目标库位关系。
        /// </summary>
        private readonly ObservableCollection<PalletBarRelation> _tokens = new();

        /// <summary>
        /// 保存当前入库亮灯使用的颜色。
        /// </summary>
        private LightColorCode LightColor { get; set; } = LightColorCode.Green;

        /// <summary>
        /// 起始库位输入框内容，用于请求后端预分配库位。
        /// </summary>
        [ObservableProperty]
        public string _inputBinNo = string.Empty;

        /// <summary>
        /// 后端分配结果对应的料架号，用于页面展示。
        /// </summary>
        [ObservableProperty]
        public string _resolvedShelfNo = string.Empty;

        /// <summary>
        /// 条码或托盘码输入框内容，用于普通入库分配。
        /// </summary>
        [ObservableProperty]
        public string _inputCode = string.Empty;

        /// <summary>
        /// 当前普通入库仓库编码，用于提交入库时做仓库隔离。
        /// </summary>
        [ObservableProperty]
        public string _warehouseLocation = "601";

        /// <summary>
        /// 二次确认扫描的目标库位号。
        /// </summary>
        [ObservableProperty]
        public string _confirmBinNo = string.Empty;

        /// <summary>
        /// 普通入库流程状态，控制是否等待确认库位。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsWaitingConfirm))]
        [NotifyPropertyChangedFor(nameof(CanInputNewCode))]
        public EntryFlowState _currentFlowState = EntryFlowState.Idle;

        /// <summary>
        /// 页面提示正文，展示分配、亮灯、确认和取消结果。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMessage))]
        [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
        public string _statusMessage = string.Empty;

        /// <summary>
        /// 标记接口或灯控操作是否执行中，防止重复提交。
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LightShelfCommand))]
        [NotifyCanExecuteChangedFor(nameof(ConfirmAndStoreCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelOperationCommand))]
        private bool _isBusy;

        /// <summary>
        /// 页面提示级别，用于组合提示文本和样式。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
        public MessageSeverity _messageSeverity = MessageSeverity.Info;

        /// <summary>
        /// 控制页面提示区域是否显示。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMessage))]
        [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
        public bool _isMessageVisible;

        public bool IsWaitingConfirm => CurrentFlowState == EntryFlowState.WaitingConfirm;
        public bool CanInputNewCode => CurrentFlowState == EntryFlowState.Idle;
        public bool HasMessage => IsMessageVisible && !string.IsNullOrWhiteSpace(StatusMessage);
        public string StatusDisplayText => HasMessage ? $"[{GetSeverityLabel(MessageSeverity)}] {StatusMessage}" : string.Empty;
        public bool CanToggleLock => _tokens.Count > 0;
        public DataGridCollectionView GridData { get; }

        public event EventHandler<EntryFocusTarget>? FocusRequested;

        /// <summary>
        /// 初始化普通入库依赖、列表视图和条码集合变化监听。
        /// </summary>
        public EntryCodeViewModel(IWMSLightService lightService, IEntryApiService entryApiService)
        {
            _lightService = lightService;
            _entryApiService = entryApiService;

            GridData = new DataGridCollectionView(_tokens);
            _tokens.CollectionChanged += OnTokensChanged;
            GridData.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(PalletBarRelation.PalletNo)));
        }

        /// <summary>
        /// 根据起始库位和扫码值请求分配库位，成功后点亮目标库位并进入待确认状态。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExecuteOperation))]
        public async Task LightShelf()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            ClearMessage();

            try
            {
                var code = InputCode?.Trim();
                var binNo = InputBinNo?.Trim();

                var resultAllocate = await _entryApiService.AllocateAsync(code, binNo);
                if (!resultAllocate.Success || resultAllocate.Data == null)
                {
                    ResolvedShelfNo = string.Empty;
                    var errorMsg = resultAllocate.Message ?? "分配失败";
                    if (errorMsg.Contains("未找到条码") || errorMsg.Contains("未找到对应条码信息"))
                    {
                        ShowMessage($"条码/托盘码 [{code}] 不存在，请确认输入是否正确。", MessageSeverity.Warning);
                    }
                    else if (errorMsg.Contains("参数不完整"))
                    {
                        ShowMessage("请填写完整的库位号和条码信息。", MessageSeverity.Warning);
                    }
                    else
                    {
                        ShowMessage(errorMsg, MessageSeverity.Warning);
                    }

                    return;
                }

                await ClearTokens();
                foreach (var item in resultAllocate.Data)
                {
                    _tokens.Add(item);
                }

                ResolvedShelfNo = resultAllocate.Data
                    .Select(item => item.ShelfNo)
                    .FirstOrDefault(shelfNo => !string.IsNullOrWhiteSpace(shelfNo))
                    ?? string.Empty;

                var binNos = resultAllocate.Data
                    .Where(r => !string.IsNullOrWhiteSpace(r.BinNo))
                    .Select(r => r.BinNo!)
                    .ToList();

                if (binNos.Count == 0)
                {
                    ShowMessage("无可亮灯库位。", MessageSeverity.Warning);
                    return;
                }

                var result = await _lightService.ChangeBinNoLightStatus(binNos, LightColor);
                ShowMessage($"分配成功，已亮灯: {result}", MessageSeverity.Success);

                CurrentFlowState = EntryFlowState.WaitingConfirm;
                ConfirmBinNo = string.Empty;
                FocusRequested?.Invoke(this, EntryFocusTarget.ConfirmBinBox);
            }
            catch (Exception ex)
            {
                ResolvedShelfNo = string.Empty;
                ShowMessage($"操作失败：{ex.Message}", MessageSeverity.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 对列表中的单个库位重新亮灯，便于人工定位。
        /// </summary>
        [RelayCommand]
        public async Task LightOne(string? binNo)
        {
            if (string.IsNullOrWhiteSpace(binNo))
            {
                return;
            }

            var result = await _lightService.ChangeBinNoLightStatus(new List<string> { binNo }, LightColor);
            ShowMessage($"亮灯结果: {result}", MessageSeverity.Success);
        }

        /// <summary>
        /// 切换普通入库后续亮灯使用的颜色。
        /// </summary>
        [RelayCommand]
        public Task TagClick(string color)
        {
            if (Enum.TryParse<LightColorCode>(color, true, out var code))
            {
                LightColor = code;
                ShowMessage($"已切换颜色为: {GetColorDisplayName(code)}", MessageSeverity.Success);
                return Task.CompletedTask;
            }

            ShowMessage($"未知颜色: {color}", MessageSeverity.Warning);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 校验二次扫描库位并提交入库事务，成功后清理列表和输入状态。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExecuteOperation))]
        public async Task ConfirmAndStore()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            var confirmBinNo = ConfirmBinNo?.Trim();

            try
            {
                if (string.IsNullOrWhiteSpace(confirmBinNo))
                {
                    ShowMessage("请扫描库位号确认。", MessageSeverity.Warning);
                    return;
                }

                var validBinNos = _tokens
                    .Select(t => t.BinNo)
                    .Where(b => !string.IsNullOrWhiteSpace(b))
                    .Select(b => b!.ToUpperInvariant())
                    .ToList();

                if (!validBinNos.Contains(confirmBinNo.ToUpperInvariant()))
                {
                    ShowMessage($"库位号 {confirmBinNo} 不在分配列表中，请确认。", MessageSeverity.Error);
                    return;
                }

                var result = await _entryApiService.CommitAsync(_tokens.ToList(), WarehouseLocation);
                if (!result.Success)
                {
                    ShowMessage($"入库失败：{result.Message}", MessageSeverity.Error);
                    return;
                }

                await ClearTokens();
                InputBinNo = string.Empty;
                ResolvedShelfNo = string.Empty;
                InputCode = string.Empty;
                ConfirmBinNo = string.Empty;
                CurrentFlowState = EntryFlowState.Idle;

                ShowMessage("入库成功", MessageSeverity.Success);
                FocusRequested?.Invoke(this, EntryFocusTarget.BinBox);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 取消当前普通入库流程，熄灭已分配库位并恢复扫码状态。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExecuteOperation))]
        public async Task CancelOperation()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await ClearTokens();

                ResolvedShelfNo = string.Empty;
                InputCode = string.Empty;
                ConfirmBinNo = string.Empty;
                CurrentFlowState = EntryFlowState.Idle;

                ShowMessage("已取消，可重新扫描条码。", MessageSeverity.Info);
                FocusRequested?.Invoke(this, EntryFocusTarget.CodeBox);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 判断普通入库命令是否可执行。
        /// </summary>
        private bool CanExecuteOperation() => !IsBusy;

        /// <summary>
        /// 获取灯光颜色的中文显示名，用于颜色切换提示。
        /// </summary>
        private string GetColorDisplayName(LightColorCode code)
        {
            var field = typeof(LightColorCode).GetField(code.ToString());
            var description = field?.GetCustomAttribute<DescriptionAttribute>();
            return description?.Description ?? code.ToString();
        }

        /// <summary>
        /// 条码集合变化时刷新锁定开关等派生状态。
        /// </summary>
        private void OnTokensChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CanToggleLock));
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
        /// 清空页面提示并隐藏消息区域。
        /// </summary>
        private void ClearMessage()
        {
            StatusMessage = string.Empty;
            IsMessageVisible = false;
        }

        /// <summary>
        /// 熄灭当前列表涉及的库位灯光，并清空待确认入库列表。
        /// </summary>
        public async Task ClearTokens()
        {
            if (_tokens.Count == 0)
            {
                return;
            }

            var binNoList = _tokens
                .Select(t => t.BinNo)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();

            if (binNoList.Count > 0)
            {
                await _lightService.ChangeBinNoLightStatus(binNoList, LightColorCode.Grey);
            }

            _tokens.Clear();
        }
    }
}
