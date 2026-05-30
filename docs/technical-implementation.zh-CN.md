# WMSApp 技术实现文档

本文档记录关键功能的技术实现细节，供开发参考。

> 版本更新记录请参阅 [CHANGELOG.md](CHANGELOG.md)

---

## 目录

1. [自动更新检查机制](#自动更新检查机制)
2. [入库流程状态机](#入库流程状态机)
3. [代码规范要点](#代码规范要点)
4. [拣货锁定机制](#拣货锁定机制)
5. [仓库隔离设计](#仓库隔离设计)
6. [大盘货架分配逻辑](#大盘货架分配逻辑)

---

## 自动更新检查机制

### 架构设计

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   HomeView      │────>│  HomeViewModel   │────>│ UpdateApiService│
│  (Loaded事件)   │     │  (检查逻辑)       │     │  (API调用)      │
└─────────────────┘     └──────────────────┘     └─────────────────┘
                               │
                               ▼
                        ┌──────────────────┐
                        │ UpdateSessionState│
                        │  (会话状态)       │
                        └──────────────────┘
```

### 核心文件

| 文件 | 说明 |
|------|------|
| `DTO/UpdateCheckResponse.cs` | 更新检查响应结构 |
| `Services/Interfaces/IUpdateApiService.cs` | 更新检查服务接口 |
| `Services/Interfaces/IUpdateSessionState.cs` | 会话状态接口 |
| `Services/Interfaces/IAppUpdateInstaller.cs` | 下载安装抽象 |
| `Services/UpdateApiService.cs` | 复用 SmartFactoryApi 调用 |
| `Services/UpdateSessionState.cs` | 会话状态管理 |
| `Services/BrowserUpdateInstaller.cs` | 非 Android 回退实现 |
| `WMSApp.Android/Services/AndroidAppUpdateInstaller.cs` | Android APK 安装 |

### 行为规则

1. Home 首次进入时执行检查（会话内仅一次）
2. 检查失败轻提示，不阻断业务
3. 无更新不展示通知
4. 有更新时：
   - `forceUpdate = true`：仅"立即下载"
   - `forceUpdate = false`：提供"取消"，取消后本会话不再弹

### VersionCode 计算规则

```csharp
// 客户端程序集版本转换
VersionCode = major * 10000 + minor * 100 + build
```

---

## 入库流程状态机

### 状态定义

```csharp
public enum EntryFlowState
{
    Idle,            // 空闲状态
    WaitingConfirm   // 等待二次确认
}
```

### 状态流转

```
┌─────────────┐    LightShelf() 成功    ┌──────────────────┐
│    Idle     │ ───────────────────────> │  WaitingConfirm  │
│  (空闲状态)  │                          │  (等待二次确认)   │
└─────────────┘ <─────────────────────── └──────────────────┘
                      ConfirmAndStore() 成功
```

### 状态与 UI 对应

| 状态 | 二次确认输入框 | 分配亮灯按钮 | 确认入库按钮 |
|------|---------------|-------------|-------------|
| Idle | 隐藏 | 启用 | 禁用 |
| WaitingConfirm | 显示 | 禁用 | 启用 |

### 二次确认验证逻辑

```csharp
// 验证扫描的库位号是否在 Tokens 列表中（忽略大小写）
var validBinNos = Tokens
    .Select(t => t.BinNo)
    .Where(b => !string.IsNullOrWhiteSpace(b))
    .Select(b => b!.ToUpperInvariant())
    .ToList();

if (!validBinNos.Contains(confirmBinNo.ToUpperInvariant()))
{
    ShowMessage($"库位号 {confirmBinNo} 不在分配列表中，请确认", MessageSeverity.Error);
    return;
}
```

---

## 代码规范要点

### 1. 不要使用静态可变集合

```csharp
// ❌ 错误：静态集合可能跨页面串数据
private static ObservableCollection<TokenItem> _tokens = new();

// ✅ 正确：实例级集合
private ObservableCollection<TokenItem> _tokens = new();
```

### 2. 异步命令防重入

```csharp
[RelayCommand(CanExecute = nameof(CanExecuteLight))]
private async Task LightShelfAsync()
{
    if (_isBusy) return;
    _isBusy = true;
    try
    {
        // 业务逻辑
    }
    finally
    {
        _isBusy = false;
    }
}

private bool CanExecuteLight() => !_isBusy;
```

### 3. 服务层空响应兜底

```csharp
// ❌ 错误：可能返回 null
var result = await response.Content.ReadFromJsonAsync<Result>();

// ✅ 正确：空值兜底
var result = await response.Content.ReadFromJsonAsync<Result>();
    ?? Result.Fail("服务返回空响应");
```

### 4. 属性联动通知

```csharp
// 计算属性
public bool HasMessage => IsMessageVisible && !string.IsNullOrWhiteSpace(StatusMessage);

// 被依赖的属性必须通知计算属性变更
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(HasMessage))]  // 关键！
[NotifyPropertyChangedFor(nameof(StatusDisplayText))]
public bool _isMessageVisible;
```

### 5. 强类型焦点路由

```csharp
// ❌ 错误：字符串魔法值
FocusRequested?.Invoke(this, "ConfirmBinBox");

// ✅ 正确：枚举强类型
public enum EntryFocusTarget { BinBox, CodeBox, ConfirmBinBox }
FocusRequested?.Invoke(this, EntryFocusTarget.ConfirmBinBox);
```

---

## 拣货锁定机制

> 详细功能说明请参阅 [picking-lock-button-guide.zh-CN.md](picking-lock-button-guide.zh-CN.md)

### 核心表结构

```sql
-- CUS_PICKING_LOCK_BARNO
GUID             UNIQUEIDENTIFIER PRIMARY KEY,
PRODUCT_NO       NVARCHAR(50),
BAR_NO           NVARCHAR(100),
BAR_QTY          INT,
REQUIRED_QTY     INT,
BIN_NO           NVARCHAR(50),
DOC_NO           NVARCHAR(50),
WAREHOUSE_LOCATION NVARCHAR(50),  -- 仓库隔离字段
CREATE_TIME      DATETIME,
CREATOR          NVARCHAR(50)
```

### API 端点

| 端点 | 说明 |
|------|------|
| `POST /api/pick/reserve` | 查询并锁定 |
| `POST /api/pick/lock` | 手动锁定 |
| `POST /api/pick/unlock` | 手动解锁 |

---

## 仓库隔离设计

### 问题描述

操作员在 601 仓库扫描领料单分配条码，换到 617 仓库再次扫描同一领料单时：
- 返回 601 仓库已锁定的条码（错误）
- 亮灯可能亮到错误仓库的库位
- 完成拣货可能清理掉其他仓库的锁定记录

### 解决方案

所有锁定相关操作按 `DocNo + WarehouseLocation` 复合键过滤：

```csharp
// 改前：只按 DocNo 过滤
.Where(x => x.DocNo == docNo)

// 改后：按 DocNo + 仓库 过滤
.Where(x => x.DocNo == docNo && x.WarehouseLocation == warehouseLocation)
```

---

## 大盘货架分配逻辑

### 问题描述

大盘货架（如 L110A）的物理标签存在 L110A1001、L110A1002、L110A1003，但数据库只存在 L110A1001 一行记录，代表跨越3个库位空间。

### 解决方案

1. 使用 `BIN_NO` 精确匹配 `WMS_SHELF_DETAIL`，自动识别所属 `ShelfNo`
2. 分配时按 `ShelfNo + Row + SortDirection + BinSize` 继续推导同一行目标库位
3. 不再要求操作员手工输入料架号

### 起始库位定位

```csharp
/// <summary>
/// 根据扫描到的 BIN_NO 精确查找起始库位
/// </summary>
var startBin = await _context.ShelfDetails
    .Where(x => x.BinNo == binNo
        && x.DeleteFlag == "N")
    .SingleOrDefaultAsync();

if (startBin == null)
{
    return Fail("输入储位不存在");
}
```

### 步长分配

```csharp
int binSize = startBin.BinSize > 0 ? startBin.BinSize : 1;

// 按 BinSize 步长筛选
var allocated = binDetailList
    .Where(x => (x.Column - startColumn) % binSize == 0)
    .Take(requiredQty)
    .Select(x => x.BinNo)
    .ToList();
```

### 分配示例

| 货架类型 | BinSize | 用户输入库位 | 分配结果 |
|---------|---------|---------|---------|
| 普通货架 L005A | 1 | L005A1001 | L005A1001, L005A1002, L005A1003... |
| 大盘货架 L110A | 3 | L110A1001 | L110A1001, L110A1004, L110A1007... |
