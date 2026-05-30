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
    /// <summary>
    /// 负责拣货页的查询、锁定、亮灯和完结流程。
    /// </summary>
    public partial class PickingCodeViewModel : ViewModelBase
    {
        /// <summary>
        /// 保存当前领料单查询并锁定到的条码行，直接驱动列表展示和批量操作。
        /// </summary>
        private readonly ObservableCollection<VariableItem> _tokens = new();

        /// <summary>
        /// 保留应用服务容器引用，兼容页面初始化时已有的依赖注入签名。
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 负责普通拣货库位的亮灯和熄灯调用。
        /// </summary>
        private readonly IWMSLightService _lightService;

        /// <summary>
        /// 负责普通拣货查询、锁定、解锁和完成接口调用。
        /// </summary>
        private readonly IPickingApiService _pickingApiService;

        /// <summary>
        /// 标记当前锁定状态变更来自程序同步，避免触发重复锁定或解锁请求。
        /// </summary>
        private bool _suppressLockChange;

        /// <summary>
        /// 记录当前已经锁定的领料单号，用于切单、完结和释放锁定。
        /// </summary>
        private string _currentDocNo = string.Empty;

        /// <summary>
        /// 保存当前批量亮灯使用的颜色。
        /// </summary>
        private LightColorCode LightColor { get; set; } = LightColorCode.Red;

        /// <summary>
        /// 拣货条码列表的分组视图，按物料和需求数量聚合展示。
        /// </summary>
        public DataGridCollectionView GridData { get; set; }

        /// <summary>
        /// 页面查询框输入的原始领料单号或扫码值。
        /// </summary>
        [ObservableProperty]
        private string _searchText = string.Empty;

        /// <summary>
        /// 标记查询流程是否正在执行，用于禁用重复提交。
        /// </summary>
        [ObservableProperty]
        private bool _isSearching;

        /// <summary>
        /// 表示当前条码列表是否处于后端锁定状态。
        /// </summary>
        [ObservableProperty]
        private bool _isLocked;

        /// <summary>
        /// 当前拣货仓库编码，用于查询、锁定、解锁和完结时做仓库隔离。
        /// </summary>
        [ObservableProperty]
        public string _warehouseLocation = "601";

        /// <summary>
        /// 页面消息正文，配合严重级别和可见性生成最终提示文本。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMessage))]
        [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
        public string _statusMessage = string.Empty;

        /// <summary>
        /// 页面消息级别，用于提示框区分提示、成功、警告和错误。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
        public MessageSeverity _messageSeverity = MessageSeverity.Info;

        /// <summary>
        /// 控制页面消息区域是否显示，并联动刷新组合后的提示文本。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMessage))]
        [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
        public bool _isMessageVisible;

        public bool CanToggleLock => _tokens.Count > 0;
        public bool HasMessage => IsMessageVisible && !string.IsNullOrWhiteSpace(StatusMessage);
        public string StatusDisplayText => HasMessage ? $"[{GetSeverityLabel(MessageSeverity)}] {StatusMessage}" : string.Empty;

        /// <summary>
        /// 初始化拣货页依赖、列表分组和条码集合变更监听。
        /// </summary>
        public PickingCodeViewModel(
            IServiceProvider serviceProvider,
            IWMSLightService lightService,
            IPickingApiService pickingApiService)
        {
            _serviceProvider = serviceProvider;
            _lightService = lightService;
            _pickingApiService = pickingApiService;

            _tokens.CollectionChanged += OnTokensChanged;

            GridData = new DataGridCollectionView(_tokens);
            GridData.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(VariableItem.ProductNo)));
            GridData.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(VariableItem.RequiredQty)));
        }

        /// <summary>
        /// 仅更新锁定开关的 UI 状态，不触发实际的锁定/解锁请求。
        /// </summary>
        public void SetIsLockedSilently(bool value)
        {
            _suppressLockChange = true;
            try
            {
                IsLocked = value;
            }
            finally
            {
                _suppressLockChange = false;
            }
        }

        /// <summary>
        /// 响应锁定开关变化，并在用户手动切换时启动后端锁定状态同步。
        /// </summary>
        partial void OnIsLockedChanged(bool value)
        {
            if (_suppressLockChange)
            {
                return;
            }

            _ = ToggleLockStatusAsync(value);
        }

        /// <summary>
        /// 手动切换锁定状态时，需要把 UI 选中态和后端锁定结果保持一致。
        /// </summary>
        private async Task ToggleLockStatusAsync(bool isLocked)
        {
            try
            {
                if (_tokens.Count == 0)
                {
                    ShowMessage("没有可操作的条码。", MessageSeverity.Warning);
                    return;
                }

                var targetDocNo = GetNormalizedDocNo();
                if (isLocked)
                {
                    var result = await _pickingApiService.LockBarsAsync(_tokens.ToList(), targetDocNo, WarehouseLocation);
                    if (!result.Success)
                    {
                        ShowMessage(result.Message ?? "没有可锁定的条码。", MessageSeverity.Warning);
                        SetIsLockedSilently(false);
                        return;
                    }

                    ShowMessage("条码已锁定，其他人无法查询。", MessageSeverity.Success);
                    return;
                }

                var unlockResult = await _pickingApiService.UnLockBarsAsync(_tokens.ToList(), targetDocNo, WarehouseLocation);
                if (!unlockResult.Success)
                {
                    ShowMessage(unlockResult.Message ?? "没有可解锁的条码。", MessageSeverity.Warning);
                    SetIsLockedSilently(true);
                    return;
                }

                ShowMessage("条码已解锁。", MessageSeverity.Info);
            }
            catch (Exception ex)
            {
                SetIsLockedSilently(!isLocked);
                ShowMessage($"操作失败: {ex.Message}", MessageSeverity.Error);
            }
        }

        /// <summary>
        /// 查询领料单并预锁定可出库条码，成功后自动亮灯提示拣货库位。
        /// </summary>
        [RelayCommand]
        public async Task SearchCode()
        {
            if (IsSearching || string.IsNullOrWhiteSpace(SearchText))
            {
                return;
            }

            IsSearching = true;
            ClearMessage();

            try
            {
                var docNo = GetNormalizedDocNo();
                if (string.IsNullOrWhiteSpace(docNo))
                {
                    ShowMessage("领料单号不能为空", MessageSeverity.Warning);
                    return;
                }

                // 重新查询前先释放上一次结果占用的灯光和锁定状态，避免跨单号串联。
                await ResetCurrentDocAsync(clearSearchText: false, clearMessage: false, unlockLocks: true);

                var reserveResult = await _pickingApiService.ReserveBarsByDocNoAsync(docNo, WarehouseLocation);
                if (!reserveResult.Success)
                {
                    var message = string.IsNullOrWhiteSpace(reserveResult.Message)
                        ? $"领料单:{docNo} 没有匹配到条码"
                        : reserveResult.Message;
                    ShowMessage(message, MessageSeverity.Warning);
                    return;
                }

                var reservedItems = reserveResult.Data ?? new List<VariableItem>();
                foreach (var item in reservedItems)
                {
                    _tokens.Add(new VariableItem(
                        item.ProductNo,
                        item.BarNo,
                        item.BarQty,
                        item.RequiredQty,
                        item.BinNo));
                }

                _currentDocNo = docNo;
                SetIsLockedSilently(true);

                await AutoLightAfterSearchAsync(
                    reserveResult.Message ?? $"领料单:{docNo} 查询并锁定成功，共计{_tokens.Count}条。",
                    MessageSeverity.Success);
            }
            finally
            {
                IsSearching = false;
            }
        }

        /// <summary>
        /// 按当前条码列表批量切换库位灯颜色，用于亮灯、熄灯和颜色切换后的同步。
        /// </summary>
        [RelayCommand]
        public async Task AllLight(LightColorCode? code = null)
        {
            var colorToUse = code ?? LightColor;
            var result = await ChangeAllLightsAsync(colorToUse);
            if (!result.Success)
            {
                ShowMessage(result.Message ?? "没有可亮灯的条码数据", MessageSeverity.Warning);
                return;
            }

            ShowMessage($"亮灯结果:{result.Data}", MessageSeverity.Success);
        }

        /// <summary>
        /// 对单个库位执行亮灯，供列表行上的独立定位操作使用。
        /// </summary>
        [RelayCommand]
        public async Task LightOne(string? binNo)
        {
            if (string.IsNullOrWhiteSpace(binNo))
            {
                return;
            }

            try
            {
                var result = await _lightService.ChangeBinNoLightStatus(new List<string> { binNo }, LightColor);
                ShowMessage($"亮灯结果:{result}", MessageSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowMessage($"亮灯失败:{ex.Message}", MessageSeverity.Error);
            }
        }

        /// <summary>
        /// 处理颜色标签点击，切换后续亮灯使用的颜色。
        /// </summary>
        [RelayCommand]
        public async Task TagClick(string color)
        {
            if (Enum.TryParse<LightColorCode>(color, true, out var code))
            {
                LightColor = code;
                ShowMessage($"已切换颜色为: {GetColorDisplayName(code)}", MessageSeverity.Success);
                return;
            }

            ShowMessage($"未知颜色: {color}", MessageSeverity.Warning);
        }

        /// <summary>
        /// 完成当前领料单拣货，通知后端提交并清理客户端锁定、灯光和列表状态。
        /// </summary>
        [RelayCommand]
        public async Task CompletePicking()
        {
            if (_tokens.Count == 0)
            {
                ShowMessage("没有可完成的拣货记录", MessageSeverity.Warning);
                return;
            }

            var binNos = _tokens
                .Where(x => !string.IsNullOrWhiteSpace(x.BinNo))
                .Select(x => x.BinNo!)
                .Distinct()
                .ToList();

            if (binNos.Count == 0)
            {
                ShowMessage("没有有效的库位信息", MessageSeverity.Warning);
                return;
            }

            try
            {
                var result = await _pickingApiService.CompletePickingAsync(_currentDocNo, binNos, WarehouseLocation);
                if (!result.Success)
                {
                    ShowMessage(result.Message ?? "拣货完成失败", MessageSeverity.Error);
                    return;
                }

                // 拣货完成后，先统一熄灯，再清理客户端缓存状态。
                await AllLight(LightColorCode.Grey);
                _tokens.Clear();
                _currentDocNo = string.Empty;
                SetIsLockedSilently(false);

                ShowMessage(result.Message ?? "拣货完成", MessageSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowMessage($"拣货完成失败：{ex.Message}", MessageSeverity.Error);
            }
        }

        /// <summary>
        /// 查询文本变化时规范化扫码结果，避免前后空格参与领料单匹配。
        /// </summary>
        partial void OnSearchTextChanged(string value)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (!string.Equals(trimmed, value, StringComparison.Ordinal))
            {
                SearchText = trimmed;
            }
        }

        /// <summary>
        /// 条码集合变化时刷新锁定开关可用性。
        /// </summary>
        private void OnTokensChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CanToggleLock));
        }

        /// <summary>
        /// 页面卸载或切单时统一释放当前文档状态。
        /// </summary>
        public async Task ResetCurrentDocAsync()
        {
            await ResetCurrentDocAsync(clearSearchText: true, clearMessage: false, unlockLocks: true);
        }

        /// <summary>
        /// 按调用场景清理当前文档，必要时释放后端锁定并保留或清空页面提示。
        /// </summary>
        private async Task ResetCurrentDocAsync(bool clearSearchText, bool clearMessage, bool unlockLocks)
        {
            if (unlockLocks)
            {
                await UnlockCurrentDocAsync();
            }

            await ClearTokens();
            SetIsLockedSilently(false);
            _currentDocNo = string.Empty;

            if (clearSearchText)
            {
                SearchText = string.Empty;
            }

            if (clearMessage)
            {
                ClearMessage();
            }
        }

        /// <summary>
        /// 查询结果切换前主动释放当前文档的锁定记录，避免残留锁影响其他终端。
        /// </summary>
        private async Task UnlockCurrentDocAsync()
        {
            var targetDocNo = GetNormalizedDocNo();
            if (string.IsNullOrWhiteSpace(targetDocNo) || _tokens.Count == 0)
            {
                return;
            }

            var unlockItems = _tokens
                .Where(x => !string.IsNullOrWhiteSpace(x.BarNo))
                .ToList();

            if (unlockItems.Count == 0)
            {
                return;
            }

            var unlockResult = await _pickingApiService.UnLockBarsAsync(unlockItems, targetDocNo, WarehouseLocation);
            if (!unlockResult.Success)
            {
                ShowMessage(unlockResult.Message ?? "释放锁定失败", MessageSeverity.Warning);
            }
        }

        /// <summary>
        /// 清空当前条码列表，并同步熄灭相关库位灯。
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

        /// <summary>
        /// 查询成功后自动亮灯，如果亮灯失败则保留查询结果并给出明确提示。
        /// </summary>
        private async Task AutoLightAfterSearchAsync(string baseMessage, MessageSeverity successSeverity)
        {
            var result = await ChangeAllLightsAsync(LightColor);
            if (!result.Success)
            {
                ShowMessage($"{baseMessage}，但自动亮灯失败：{result.Message}", MessageSeverity.Warning);
                return;
            }

            ShowMessage($"{baseMessage} 已自动亮灯。{result.Data}", successSeverity);
        }

        /// <summary>
        /// 汇总当前条码的有效库位并调用灯光服务，统一返回可展示的亮灯结果。
        /// </summary>
        private async Task<Result<string>> ChangeAllLightsAsync(LightColorCode colorToUse)
        {
            if (_tokens.Count == 0)
            {
                return Result<string>.Fail("没有可亮灯的条码数据");
            }

            var binNoList = _tokens
                .Select(t => t.BinNo)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();

            if (binNoList.Count == 0)
            {
                return Result<string>.Fail("没有可亮灯的库位数据");
            }

            try
            {
                var result = await _lightService.ChangeBinNoLightStatus(binNoList, colorToUse);
                return Result<string>.Ok(result, "亮灯成功");
            }
            catch (Exception ex)
            {
                return Result<string>.Fail($"亮灯失败:{ex.Message}");
            }
        }

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
        /// 写入页面消息并显示提示框。
        /// </summary>
        private void ShowMessage(string message, MessageSeverity severity)
        {
            StatusMessage = message;
            MessageSeverity = severity;
            IsMessageVisible = true;
        }

        /// <summary>
        /// 清空页面消息并隐藏提示框。
        /// </summary>
        private void ClearMessage()
        {
            StatusMessage = string.Empty;
            IsMessageVisible = false;
        }

        /// <summary>
        /// 将消息级别转换为提示框前缀文本。
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
        /// 扫描值支持前缀场景，最终只保留最后一个连字符后的真实领料单号。
        /// </summary>
        private string GetNormalizedDocNo()
        {
            if (!string.IsNullOrWhiteSpace(_currentDocNo))
            {
                return _currentDocNo;
            }

            var source = SearchText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            return source[(source.LastIndexOf("-") + 1)..];
        }
    }
}
