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
using System.Threading;
using System.Threading.Tasks;
using WMSApp.DTO;
using WMSApp.Models;
using WMSApp.Services;

namespace WMSApp.ViewModels
{
    public enum InductionPickState { Idle, Queried, Picking }

    public enum InductionPickFocusTarget { ItemNoBox }

    /// <summary>
    /// 管理感应拣货页面的料号查询、预览亮灯、拣货启动和回调更新。
    /// </summary>
    public partial class InductionPickViewModel : ViewModelBase, IPageLifecycleAware
    {
        /// <summary>
        /// 提供感应拣货查询、启动、取消和料号联想接口。
        /// </summary>
        private readonly IInductionPickApiService _pickApiService;

        /// <summary>
        /// 接收感应拣货回调的 SignalR 客户端。
        /// </summary>
        private readonly IInductionHubService _hubService;

        /// <summary>
        /// 保存当前查询到的待拣条码，驱动列表、进度和回调匹配。
        /// </summary>
        private readonly ObservableCollection<InductionPickItem> _pickItems = new();

        /// <summary>
        /// 缓存允许操作的仓库编码，用于查询和启动拣货前校验。
        /// </summary>
        private readonly HashSet<string> _allowedWarehouses;

        /// <summary>
        /// 保存料号输入的自动补全候选项。
        /// </summary>
        private readonly ObservableCollection<string> _itemSuggestions = new();

        /// <summary>
        /// 控制自动补全请求取消，避免旧请求覆盖新输入结果。
        /// </summary>
        private CancellationTokenSource? _suggestionCts;

        /// <summary>
        /// 标记页面是否打开，防止关闭后继续处理回调或联想请求。
        /// </summary>
        private bool _isPageActive;

        /// <summary>
        /// 标记 Hub 事件是否已经订阅，避免重复绑定。
        /// </summary>
        private bool _hubHandlersAttached;

        /// <summary>
        /// 选择自动补全项时临时抑制再次触发联想查询。
        /// </summary>
        private bool _suppressSuggestionLookup;

        /// <summary>
        /// 保存当前感应拣货预览和启动时使用的灯光颜色。
        /// </summary>
        private LightColorCode LightColor { get; set; } = LightColorCode.Cyan;

        /// <summary>
        /// 料号查询输入框内容，同时驱动自动补全。
        /// </summary>
        [ObservableProperty]
        private string _searchItemNo = string.Empty;

        /// <summary>
        /// 用户输入的需求数量文本，查询时转换为可选数量条件。
        /// </summary>
        [ObservableProperty]
        private string _requiredQtyText = string.Empty;

        /// <summary>
        /// 当前感应拣货仓库编码，用于查询、启动和仓库隔离校验。
        /// </summary>
        [ObservableProperty]
        private string _warehouseLocation = string.Empty;

        /// <summary>
        /// 页面业务状态，控制查询、启动、取消、进度和联想区域可见性。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsIdle))]
        [NotifyPropertyChangedFor(nameof(IsQueried))]
        [NotifyPropertyChangedFor(nameof(IsPicking))]
        [NotifyPropertyChangedFor(nameof(CanStartPick))]
        [NotifyPropertyChangedFor(nameof(CanCancelPick))]
        [NotifyPropertyChangedFor(nameof(ShowProgress))]
        [NotifyPropertyChangedFor(nameof(HasSuggestions))]
        private InductionPickState _currentState = InductionPickState.Idle;

        /// <summary>
        /// 页面提示正文，展示查询、拣货、取消和回调处理结果。
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
        /// 控制页面消息区域是否展示，并联动刷新组合文本。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMessage))]
        [NotifyPropertyChangedFor(nameof(StatusDisplayText))]
        private bool _isMessageVisible;

        /// <summary>
        /// 标记命令请求是否执行中，用于阻止重复操作。
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
        [NotifyCanExecuteChangedFor(nameof(StartPickCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelPickCommand))]
        private bool _isBusy;

        /// <summary>
        /// 当前查询结果总条码数。
        /// </summary>
        [ObservableProperty]
        private int _totalItemCount;

        /// <summary>
        /// 已收到成功出库回调的条码数。
        /// </summary>
        [ObservableProperty]
        private int _pickedItemCount;

        /// <summary>
        /// 已收到非法出库回调的条码数。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasIllegalItems))]
        private int _illegalItemCount;

        /// <summary>
        /// 控制自动补全面板是否显示。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSuggestions))]
        private bool _isSuggestionVisible;

        public bool IsIdle => CurrentState == InductionPickState.Idle;
        public bool IsQueried => CurrentState == InductionPickState.Queried;
        public bool IsPicking => CurrentState == InductionPickState.Picking;
        public bool ShowProgress => CurrentState == InductionPickState.Queried || CurrentState == InductionPickState.Picking;
        public bool CanStartPick => CurrentState == InductionPickState.Queried && _pickItems.Any(x => x.Status == 0);
        public bool CanCancelPick => CurrentState == InductionPickState.Queried || CurrentState == InductionPickState.Picking;
        public bool HasMessage => IsMessageVisible && !string.IsNullOrWhiteSpace(StatusMessage);
        public bool HasIllegalItems => IllegalItemCount > 0;
        public bool HasSuggestions => IsSuggestionVisible && _itemSuggestions.Count > 0 && CurrentState == InductionPickState.Idle;
        public string StatusDisplayText => HasMessage ? $"[{GetSeverityLabel(MessageSeverity)}] {StatusMessage}" : string.Empty;
        public double ProgressPercentage => TotalItemCount > 0 ? (double)PickedItemCount / TotalItemCount * 100 : 0;
        public DataGridCollectionView GridData { get; }
        public IReadOnlyList<string> ItemSuggestions => _itemSuggestions;

        public event EventHandler<InductionPickFocusTarget>? FocusRequested;

        /// <summary>
        /// 初始化感应拣货依赖、仓库配置和列表统计监听。
        /// </summary>
        public InductionPickViewModel(
            IInductionPickApiService pickApiService,
            IInductionHubService hubService,
            IConfiguration configuration)
        {
            _pickApiService = pickApiService;
            _hubService = hubService;
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

            GridData = new DataGridCollectionView(_pickItems);
            _pickItems.CollectionChanged += (_, __) => UpdateCounts();
        }

        /// <summary>
        /// 料号输入变化时触发自动补全刷新。
        /// </summary>
        partial void OnSearchItemNoChanged(string value)
        {
            _ = RefreshSuggestionsAsync(value);
        }

        /// <summary>
        /// 页面打开时连接 Hub、绑定拣货回调并校验仓库配置。
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
                EnsureWarehouseConfigured();
            }
            catch (Exception ex)
            {
                ShowMessage($"连接感应回调失败：{ex.Message}", MessageSeverity.Error);
            }
        }

        /// <summary>
        /// 页面关闭时取消联想、解绑回调、撤销待拣请求并停止 Hub 连接。
        /// </summary>
        public async Task OnPageClosedAsync()
        {
            if (!_isPageActive)
            {
                return;
            }

            _isPageActive = false;
            CancelSuggestionLookup();
            ClearSuggestions();
            DetachHubHandlers();

            try
            {
                var pendingLabelIds = GetPendingLabelIds();
                if (pendingLabelIds.Count > 0)
                {
                    await _pickApiService.CancelPickAsync(pendingLabelIds);
                }
            }
            finally
            {
                ResetPickState(clearSearchInputs: true, clearMessage: true);

                if (_hubService.IsConnected)
                {
                    await _hubService.StopAsync();
                }
            }
        }

        /// <summary>
        /// 绑定拣货回调事件，确保页面打开期间能接收料架结果。
        /// </summary>
        private void AttachHubHandlers()
        {
            if (_hubHandlersAttached)
            {
                return;
            }

            _hubService.PickCallbackReceived += OnPickCallbackReceived;
            _hubHandlersAttached = true;
        }

        /// <summary>
        /// 解绑拣货回调事件，避免页面关闭后继续刷新 UI。
        /// </summary>
        private void DetachHubHandlers()
        {
            if (!_hubHandlersAttached)
            {
                return;
            }

            _hubService.PickCallbackReceived -= OnPickCallbackReceived;
            _hubHandlersAttached = false;
        }

        /// <summary>
        /// 根据当前列表状态刷新总数、成功数、异常数和进度。
        /// </summary>
        private void UpdateCounts()
        {
            TotalItemCount = _pickItems.Count;
            PickedItemCount = _pickItems.Count(x => x.Status == 1);
            IllegalItemCount = _pickItems.Count(x => x.Status == 2);
            OnPropertyChanged(nameof(ProgressPercentage));
        }

        /// <summary>
        /// 处理料架拣货回调，按条码刷新状态、提示和完成判定。
        /// </summary>
        private void OnPickCallbackReceived(object? sender, PickCallbackMessage message)
        {
            if (!_isPageActive)
            {
                return;
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var item = _pickItems.FirstOrDefault(x => x.BarNo == message.LabelId);
                if (item == null)
                {
                    return;
                }

                if (message.Success)
                {
                    item.Status = 1;
                    ShowMessage($"条码 {message.LabelId} 出库成功。", MessageSeverity.Success);
                }
                else if (message.IsIllegal)
                {
                    item.Status = 2;
                    ShowMessage($"条码 {message.LabelId} 非法出库。", MessageSeverity.Error);
                }
                else
                {
                    ShowMessage($"出库失败：{message.Message}", MessageSeverity.Error);
                }

                GridData.Refresh();
                UpdateCounts();

                if (_pickItems.All(x => x.Status != 0))
                {
                    CurrentState = InductionPickState.Idle;
                    ShowMessage("所有条码已完成处理。", MessageSeverity.Success);
                }
            });
        }

        /// <summary>
        /// 按料号查询可拣条码，并完成预览亮灯。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSearchExec))]
        private async Task Search()
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
            ClearSuggestions();
            CancelSuggestionLookup();

            try
            {
                var itemNo = SearchItemNo?.Trim();
                if (string.IsNullOrWhiteSpace(itemNo))
                {
                    ShowMessage("请输入料号。", MessageSeverity.Warning);
                    return;
                }

                decimal? requiredQty = null;
                if (!string.IsNullOrWhiteSpace(RequiredQtyText))
                {
                    if (!decimal.TryParse(RequiredQtyText, out var qty))
                    {
                        ShowMessage("数量格式无效。", MessageSeverity.Warning);
                        return;
                    }

                    requiredQty = qty;
                }

                var result = await _pickApiService.QueryByItemNoAsync(itemNo, requiredQty, WarehouseLocation, (int)LightColor);
                if (!result.Success || result.Data == null || result.Data.Count == 0)
                {
                    ShowMessage(result.Message ?? "未查询到条码。", MessageSeverity.Warning);
                    return;
                }

                _pickItems.Clear();
                foreach (var item in result.Data)
                {
                    _pickItems.Add(item);
                }

                CurrentState = InductionPickState.Queried;
                UpdateCounts();
                ShowMessage($"查询到 {result.Data.Count} 个条码，已完成预览亮灯。", MessageSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowMessage($"查询失败：{ex.Message}", MessageSeverity.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 判断查询命令是否可执行。
        /// </summary>
        private bool CanSearchExec() => !IsBusy && CurrentState == InductionPickState.Idle;

        /// <summary>
        /// 启动当前查询结果中的待拣条码，等待料架回调。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartPickExec))]
        private async Task StartPick()
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
                var labelIds = GetPendingLabelIds();
                if (labelIds.Count == 0)
                {
                    ShowMessage("没有待出库的条码。", MessageSeverity.Warning);
                    return;
                }

                var result = await _pickApiService.StartPickAsync(labelIds, WarehouseLocation, (int)LightColor);
                if (!result.Success)
                {
                    ShowMessage(result.Message ?? "开始拣货失败。", MessageSeverity.Error);
                    return;
                }

                CurrentState = InductionPickState.Picking;
                ShowMessage("已发送拣货请求，请从亮灯库位取货。", MessageSeverity.Info);
            }
            catch (Exception ex)
            {
                ShowMessage($"拣货失败：{ex.Message}", MessageSeverity.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 判断启动拣货命令是否可执行。
        /// </summary>
        private bool CanStartPickExec() => !IsBusy && CurrentState == InductionPickState.Queried;

        /// <summary>
        /// 取消当前查询或拣货流程，并释放待拣条码。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCancelPickExec))]
        private async Task CancelPick()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var pendingLabelIds = GetPendingLabelIds();
                if (pendingLabelIds.Count > 0)
                {
                    var result = await _pickApiService.CancelPickAsync(pendingLabelIds);
                    if (!result.Success)
                    {
                        ShowMessage(result.Message ?? "取消拣货失败。", MessageSeverity.Error);
                        return;
                    }
                }

                ResetPickState(clearSearchInputs: false, clearMessage: false);
                ShowMessage("已取消拣货。", MessageSeverity.Info);
                FocusRequested?.Invoke(this, InductionPickFocusTarget.ItemNoBox);
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
        /// 判断取消拣货命令是否可执行。
        /// </summary>
        private bool CanCancelPickExec() => !IsBusy && (CurrentState == InductionPickState.Queried || CurrentState == InductionPickState.Picking);

        /// <summary>
        /// 选择自动补全料号并恢复输入焦点。
        /// </summary>
        [RelayCommand]
        private void SelectSuggestion(string itemNo)
        {
            if (string.IsNullOrWhiteSpace(itemNo))
            {
                return;
            }

            _suppressSuggestionLookup = true;
            SearchItemNo = itemNo;
            _suppressSuggestionLookup = false;
            ClearSuggestions();
            FocusRequested?.Invoke(this, InductionPickFocusTarget.ItemNoBox);
        }

        /// <summary>
        /// 切换感应拣货使用的灯光颜色。
        /// </summary>
        [RelayCommand]
        private void TagClick(string color)
        {
            if (!Enum.TryParse<LightColorCode>(color, true, out var code))
            {
                return;
            }

            LightColor = code;
            ShowMessage($"已切换颜色为：{GetColorDisplayName(code)}", MessageSeverity.Success);
        }

        /// <summary>
        /// 清空查询条件、列表、进度和提示，回到初始状态。
        /// </summary>
        [RelayCommand]
        private void ResetAll()
        {
            ResetPickState(clearSearchInputs: true, clearMessage: true);
        }

        /// <summary>
        /// 延迟刷新料号自动补全候选，避免高频输入导致接口抖动。
        /// </summary>
        private async Task RefreshSuggestionsAsync(string keyword)
        {
            if (_suppressSuggestionLookup || !_isPageActive || CurrentState != InductionPickState.Idle)
            {
                return;
            }

            CancelSuggestionLookup();

            var normalizedKeyword = keyword?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedKeyword) || !EnsureWarehouseConfigured(showMessage: false))
            {
                ClearSuggestions();
                return;
            }

            var cts = new CancellationTokenSource();
            _suggestionCts = cts;

            try
            {
                await Task.Delay(180, cts.Token);

                var result = await _pickApiService.GetItemSuggestionsAsync(normalizedKeyword, WarehouseLocation);
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                if (!result.Success || result.Data == null || result.Data.Count == 0)
                {
                    ClearSuggestions();
                    return;
                }

                _itemSuggestions.Clear();
                foreach (var item in result.Data)
                {
                    _itemSuggestions.Add(item);
                }

                IsSuggestionVisible = _itemSuggestions.Count > 0;
                OnPropertyChanged(nameof(ItemSuggestions));
                OnPropertyChanged(nameof(HasSuggestions));
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                ClearSuggestions();
            }
        }

        /// <summary>
        /// 取消正在等待或执行中的自动补全请求。
        /// </summary>
        private void CancelSuggestionLookup()
        {
            _suggestionCts?.Cancel();
            _suggestionCts?.Dispose();
            _suggestionCts = null;
        }

        /// <summary>
        /// 清空自动补全候选并隐藏建议面板。
        /// </summary>
        private void ClearSuggestions()
        {
            _itemSuggestions.Clear();
            IsSuggestionVisible = false;
            OnPropertyChanged(nameof(ItemSuggestions));
            OnPropertyChanged(nameof(HasSuggestions));
        }

        /// <summary>
        /// 获取仍处于待拣状态的条码号，用于启动或取消请求。
        /// </summary>
        private List<string> GetPendingLabelIds()
        {
            return _pickItems
                .Where(item => item.Status == 0)
                .Select(item => item.BarNo)
                .Where(barNo => !string.IsNullOrWhiteSpace(barNo))
                .ToList();
        }

        /// <summary>
        /// 重置感应拣货状态，并按调用场景决定是否清空输入和提示。
        /// </summary>
        private void ResetPickState(bool clearSearchInputs, bool clearMessage)
        {
            _pickItems.Clear();
            CurrentState = InductionPickState.Idle;
            UpdateCounts();
            ClearSuggestions();

            if (clearSearchInputs)
            {
                SearchItemNo = string.Empty;
                RequiredQtyText = string.Empty;
            }

            if (clearMessage)
            {
                ClearMessage();
            }
        }

        /// <summary>
        /// 校验当前仓库配置是否完整并属于允许仓库列表。
        /// </summary>
        private bool EnsureWarehouseConfigured(bool showMessage = true)
        {
            if (string.IsNullOrWhiteSpace(WarehouseLocation))
            {
                if (showMessage)
                {
                    ShowMessage("未配置当前仓库编码。", MessageSeverity.Error);
                }
                return false;
            }

            if (_allowedWarehouses.Count == 0)
            {
                if (showMessage)
                {
                    ShowMessage("未配置允许仓库列表。", MessageSeverity.Error);
                }
                return false;
            }

            if (!_allowedWarehouses.Contains(WarehouseLocation))
            {
                if (showMessage)
                {
                    ShowMessage($"当前仓库 {WarehouseLocation} 不在允许列表中。", MessageSeverity.Error);
                }
                return false;
            }

            return true;
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
