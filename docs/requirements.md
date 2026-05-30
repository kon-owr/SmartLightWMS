# WMSApp 仓库管理系统 - 方案需求文档

> 版本：1.0
> 最后更新：2026-03-25
> 文档性质：逆向工程生成（基于已实现代码）

---

## 目录

1. [项目概述](#1-项目概述)
2. [系统架构](#2-系统架构)
3. [功能需求](#3-功能需求)
4. [数据模型](#4-数据模型)
5. [API 接口规范](#5-api-接口规范)
6. [业务流程](#6-业务流程)
7. [非功能需求](#7-非功能需求)
8. [部署说明](#8-部署说明)

---

## 1. 项目概述

### 1.1 项目背景

WMSApp 是一套智能仓库管理系统（Warehouse Management System），用于支持智能货架环境下的入库和拣货作业。系统通过智能亮灯引导操作员快速定位库位，提高仓库作业效率和准确性。

### 1.2 系统目标

- **提高入库效率**：自动分配库位，亮灯引导放置
- **优化拣货流程**：按领料单自动锁定条码，防止并发冲突
- **增强操作准确性**：二次确认机制，防止误操作
- **支持移动作业**：Android 手持设备 + 桌面客户端

### 1.3 适用范围

| 仓库 | 编码 | 说明 |
|------|------|------|
| 601 仓库 | 601 | 主仓库 |
| 616/617 仓库 | 616, 617 | 辅助仓库 |
| 621 仓库 | 621 | 辅助仓库 |

---

## 2. 系统架构

### 2.1 整体架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        客户端层                                  │
├─────────────┬─────────────┬─────────────┬─────────────────────────┤
│   Android   │   Desktop   │   Browser   │         iOS            │
│  (手持PDA)  │  (Windows)  │  (WebAssembly)│      (iPad)          │
└──────┬──────┴──────┬──────┴──────┬──────┴───────────┬───────────┘
       │             │             │                  │
       └─────────────┴──────┬──────┴──────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                    WMSApp (Avalonia UI)                         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ HomeViewModel│  │EntryViewModel│  │PickingViewModel│         │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              API Services Layer                           │  │
│  │  EntryApiService | PickingApiService | UpdateApiService  │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────┬───────────────────────────────────┘
                              │ HTTP/REST
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  SmartFactoryWebApi                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │EntryController│ │PickController│  │UpdateController│         │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              Business Services Layer                      │  │
│  │  EntryDetailService | PickDetailService | AppUpdateService│  │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              Data Access Layer (Entity Framework)         │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────┬───────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      数据库层                                    │
│  ┌────────────────────────────────────────────────────────────┐│
│  │  SQL Server (sWMS_Production)                              ││
│  │  - WMS_BAR_DETAIL (条码明细)                                ││
│  │  - WMS_PICKING_APPLY_DETAIL (领料明细)                      ││
│  │  - WMS_SHELF_DETAIL (货架库位)                              ││
│  │  - CUS_PICKING_LOCK_BARNO (锁定条码)                        ││
│  │  - WMS_ITEM_STOCK (库存)                                    ││
│  └────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    外部服务                                      │
│  ┌────────────────────────────────────────────────────────────┐│
│  │  LightServiceApi (智能货架亮灯控制)                         ││
│  │  BaseAddress: http://10.50.77.246:8091                     ││
│  └────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 技术栈

#### 客户端 (WMSApp)

| 组件 | 技术 | 版本 |
|------|------|------|
| 运行时 | .NET | 9.0 |
| UI 框架 | Avalonia UI | - |
| MVVM | CommunityToolkit.Mvvm | - |
| 主题 | Semi.Avalonia + Irihi.Ursa | - |
| 依赖注入 | Microsoft.Extensions.DependencyInjection | - |
| HTTP 客户端 | Microsoft.Extensions.Http | - |
| 配置 | Microsoft.Extensions.Configuration | - |

#### 后端 (SmartFactoryWebApi)

| 组件 | 技术 | 版本 |
|------|------|------|
| 运行时 | .NET | 9.0 |
| Web 框架 | ASP.NET Core | 9.0 |
| ORM | Entity Framework Core | 9.0 |
| 数据库 | SQL Server | - |

### 2.3 项目结构

```
WMSApp/
├── WMSApp/                    # 核心共享库
│   ├── App.axaml.cs          # 应用入口、服务注册
│   ├── ViewModels/           # 视图模型
│   │   ├── MainViewModel.cs  # 导航管理
│   │   ├── HomeViewModel.cs  # 首页
│   │   ├── EntryCodeViewModel.cs    # 入库
│   │   └── PickingCodeViewModel.cs  # 拣货
│   ├── Views/                # 视图
│   ├── Services/             # API 服务
│   ├── DTO/                  # 数据传输对象
│   ├── Models/               # 数据模型
│   └── appsettings.json      # 配置文件
├── WMSApp.Android/           # Android 平台
├── WMSApp.Desktop/           # 桌面平台
├── WMSApp.Browser/           # WebAssembly
└── scripts/                  # 发布脚本

SmartFactoryWebApi/
├── Controllers/              # API 控制器
│   ├── EntryController.cs    # 入库 API
│   ├── PickController.cs     # 拣货 API
│   └── UpdateController.cs   # 更新检查 API
├── Services/                 # 业务服务
│   ├── EntryDetailService.cs # 入库逻辑
│   ├── PickDetailService.cs  # 拣货逻辑
│   └── AppUpdateService.cs   # 更新检查逻辑
├── Models/                   # 数据模型
├── DTO/                      # 数据传输对象
├── Data/                     # 数据库上下文
└── sql/                      # SQL 迁移脚本
```

---

## 3. 功能需求

### 3.1 入库模块（Entry）

#### 3.1.1 功能概述

入库模块支持操作员将货物放置到指定库位，系统根据起始库位自动识别所属料架，并通过亮灯引导和二次确认确保放置准确性。

#### 3.1.2 功能清单

| 功能编号 | 功能名称 | 说明 |
|---------|---------|------|
| E-001 | 条码扫描 | 支持托盘码和散件条码扫描 |
| E-002 | 库位自动分配 | 根据起始库位自动识别所属料架并分配连续库位 |
| E-003 | 智能亮灯 | 分配成功后亮绿灯引导放置 |
| E-004 | 二次确认 | 扫描库位号确认放置位置正确 |
| E-005 | 取消操作 | 支持取消当前操作，熄灯并重置状态 |
| E-006 | 颜色切换 | 支持多种亮灯颜色（红/绿/蓝/黄/粉/青/白） |
| E-007 | 大盘货架支持 | 支持大盘货架按步长+3分配 |

#### 3.1.3 输入字段

| 字段 | 类型 | 必填 | 说明 | 示例 |
|------|------|------|------|------|
| 库位号 | 文本 | 是 | 起始库位号 | L005A1001 |
| 所属料架 | 只读文本 | 否 | 系统根据起始库位自动识别 | L005A |
| 条码 | 文本 | 是 | 托盘码或散件条码 | TP001 或 SN12345 |
| 二次确认库位 | 文本 | 是 | 验证放置位置 | L005A1001 |
| 仓库位置 | 选择 | 是 | 仓库编码 | 601 |

#### 3.1.4 状态机

```
┌─────────────┐    分配亮灯成功    ┌──────────────────┐
│    Idle     │ ─────────────────> │  WaitingConfirm  │
│  (空闲状态)  │                    │  (等待二次确认)   │
└─────────────┘ <───────────────── └──────────────────┘
                      入库成功/取消
```

| 状态 | 二次确认框 | 分配亮灯按钮 | 确认入库按钮 |
|------|-----------|-------------|-------------|
| Idle | 隐藏 | 启用 | 禁用 |
| WaitingConfirm | 显示 | 禁用 | 启用 |

#### 3.1.5 业务规则

1. **条码类型识别**
   - 优先按托盘码查询（PalletNo）
   - 托盘码不存在时按散件条码查询（BarNo）
   - 两者都不存在返回错误

2. **重复入库检测**
   - 条码已入库（IsRack = 'Y'）时拒绝操作
   - 防止同一货物重复入库

3. **库位分配规则**
   - 普通货架：按列号+1递增分配
   - 大盘货架：按列号+3递增分配（BinSize = 3）
   - 库位必须可用（IsEnable = 'Y'）

4. **二次确认验证**
   - 扫描的库位号必须在分配列表中
   - 验证忽略大小写

5. **入库事务**
   - 更新条码库位和入库标记
   - 扣减原库位库存
   - 新增目标库位库存
   - 标记库位为已占用

---

### 3.2 拣货模块（Picking）

#### 3.2.1 功能概述

拣货模块支持操作员按领料单进行拣货作业，系统自动分配条码、锁定防止冲突、亮灯引导拣货。

#### 3.2.2 功能清单

| 功能编号 | 功能名称 | 说明 |
|---------|---------|------|
| P-001 | 领料单查询 | 按领料单号查询待拣货物 |
| P-002 | 条码自动锁定 | 查询时自动锁定条码，防止他人操作 |
| P-003 | 自动亮灯 | 查询成功后自动亮红灯引导 |
| P-004 | 单行亮灯 | 支持单独亮某个库位 |
| P-005 | 颜色切换 | 支持多种亮灯颜色 |
| P-006 | 拣货完成 | 完成拣货，释放库位和锁定 |
| P-007 | 仓库隔离 | 不同仓库的条码独立锁定 |

#### 3.2.3 输入字段

| 字段 | 类型 | 必填 | 说明 | 示例 |
|------|------|------|------|------|
| 领料单号 | 文本 | 是 | 领料单编号 | MO20260325001 |
| 仓库位置 | 选择 | 是 | 仓库编码 | 601 |

#### 3.2.4 锁定机制

```
┌─────────────────────────────────────────────────────────────┐
│                    条码锁定流程                              │
├─────────────────────────────────────────────────────────────┤
│  1. 操作员A 在 601 仓库扫描领料单 → 条码被锁定（601维度）    │
│  2. 操作员B 在 617 仓库扫描同一领料单 → 获得不同的条码      │
│  3. 操作员A 完成拣货 → 只释放 601 仓库的锁定               │
└─────────────────────────────────────────────────────────────┘
```

**锁定表结构**：`CUS_PICKING_LOCK_BARNO`

| 字段 | 说明 |
|------|------|
| DocNo | 领料单号 |
| BarNo | 条码 |
| WarehouseLocation | 仓库编码（隔离维度） |
| CreateTime | 锁定时间 |

#### 3.2.5 业务规则

1. **查询即锁定**
   - 查询领料单时自动锁定分配的条码
   - 已锁定的条码对其他操作员不可见

2. **FIFO 分配**
   - 按入库时间（InstockDate）升序分配
   - 先入库的条码优先分配

3. **仓库隔离**
   - 锁定记录包含 WarehouseLocation
   - 所有查询按 DocNo + WarehouseLocation 过滤

4. **拣货完成处理**
   - 解除库位占用（IsEnable = 'Y'）
   - 清理锁定记录
   - 更新条码状态为已出库（IsRack = 'N'）

---

### 3.3 应用更新模块

#### 3.3.1 功能概述

支持客户端自动检查更新，提供强制更新和可选更新两种模式。

#### 3.3.2 功能清单

| 功能编号 | 功能名称 | 说明 |
|---------|---------|------|
| U-001 | 自动检查更新 | 进入首页时自动检查 |
| U-002 | 强制更新 | 必须更新才能继续使用 |
| U-003 | 可选更新 | 可取消，本会话不再提示 |
| U-004 | APK 下载 | Android 平台下载 APK |
| U-005 | SHA256 校验 | 下载后校验文件完整性 |
| U-006 | 系统安装 | 调用 Android 系统安装 |

#### 3.3.3 更新检查逻辑

```
┌─────────────────────────────────────────────────────────────┐
│                    更新检查流程                              │
├─────────────────────────────────────────────────────────────┤
│  1. 进入首页 → 调用 GET /api/update/check                   │
│  2. 服务端比较 VersionCode                                  │
│  3. 返回更新信息（hasUpdate, forceUpdate, downloadUrl）     │
│  4. 有更新 → 显示更新通知层                                 │
│     - forceUpdate=true: 仅显示"立即下载"                    │
│     - forceUpdate=false: 显示"取消"+"立即下载"              │
└─────────────────────────────────────────────────────────────┘
```

#### 3.3.4 版本号规则

- **VersionName**: `major.minor.patch`（如 1.0.10）
- **VersionCode**: `major × 10000 + minor × 100 + patch`（如 10010）

---

## 4. 数据模型

### 4.1 核心表结构

#### 4.1.1 条码明细表 (WMS_BAR_DETAIL)

| 字段 | 类型 | 说明 |
|------|------|------|
| BarNo | NVARCHAR | 条码（主键） |
| ItemGuid | NVARCHAR | 物料GUID |
| BarQty | DECIMAL | 条码数量 |
| BinNo | NVARCHAR | 当前库位 |
| WarehouseNo | NVARCHAR | 仓库编码 |
| IsRack | CHAR(1) | 是否在智能货架（Y/N） |
| EnableFlag | CHAR(1) | 是否有效 |
| InstockDate | DATETIME | 入库时间 |
| LotNo | NVARCHAR | 批次号 |

#### 4.1.2 领料明细表 (WMS_PICKING_APPLY_DETAIL)

| 字段 | 类型 | 说明 |
|------|------|------|
| Guid | NVARCHAR | 主键 |
| FromGuid | NVARCHAR | 领料单GUID |
| ItemGuid | NVARCHAR | 物料GUID |
| ApplyQty | DECIMAL | 申请数量 |
| PickingQty | DECIMAL | 已拣数量 |

#### 4.1.3 锁定条码表 (CUS_PICKING_LOCK_BARNO)

| 字段 | 类型 | 说明 |
|------|------|------|
| GUID | NVARCHAR | 主键 |
| DOC_NO | NVARCHAR | 领料单号 |
| BAR_NO | NVARCHAR | 条码 |
| PRODUCT_NO | NVARCHAR | 物料编号 |
| BAR_QTY | DECIMAL | 条码数量 |
| REQUIRED_QTY | DECIMAL | 需求数量 |
| BIN_NO | NVARCHAR | 库位 |
| WAREHOUSE_LOCATION | NVARCHAR | 仓库编码 |
| CREATE_TIME | DATETIME | 创建时间 |

#### 4.1.4 货架库位表 (WMS_SHELF_DETAIL)

| 字段 | 类型 | 说明 |
|------|------|------|
| BinNo | NVARCHAR | 库位号 |
| ShelfNo | NVARCHAR | 货架号 |
| Row | INT | 行号 |
| Column | INT | 列号 |
| BinSize | INT | 库位大小（1=普通，3=大盘） |
| IsEnable | CHAR(1) | 是否可用 |
| WarehouseNo | NVARCHAR | 仓库编码 |

### 4.2 DTO 定义

#### 4.2.1 Result<T> - 通用响应包装

```csharp
public class Result<T>
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }
}
```

#### 4.2.2 PalletBarRelation - 入库关联

```csharp
public class PalletBarRelation
{
    public string? PalletNo { get; init; }  // 托盘码
    public string? BarNo { get; init; }     // 条码
    public string? BinNo { get; init; }     // 库位号
    public string? ShelfNo { get; init; }   // 货架号
}
```

#### 4.2.3 VariableItem - 拣货条码项

```csharp
public class VariableItem
{
    public string? ProductNo { get; set; }    // 物料编号
    public string? BarNo { get; set; }        // 条码
    public decimal? BarQty { get; set; }      // 条码数量
    public decimal RequiredQty { get; set; }  // 需求数量
    public string? BinNo { get; set; }        // 库位号
}
```

#### 4.2.4 UpdateCheckResponse - 更新检查响应

```csharp
public class UpdateCheckResponse
{
    public bool HasUpdate { get; set; }
    public bool ForceUpdate { get; set; }
    public string LatestVersionName { get; set; }
    public int LatestVersionCode { get; set; }
    public int MinSupportedVersionCode { get; set; }
    public string DownloadUrl { get; set; }
    public string Sha256 { get; set; }
    public string? ReleaseNotes { get; set; }
}
```

---

## 5. API 接口规范

### 5.1 基础信息

| 项目 | 值 |
|------|------|
| 基地址 | http://10.50.77.246:5067 |
| 数据格式 | JSON |
| 认证方式 | 无（内网环境） |

### 5.2 入库 API

#### POST /api/entry/allocate

**描述**：分配库位

**请求**：
```json
{
  "barCode": "SN12345",
  "binNo": "L005A1001"
}
```

**响应**：
```json
{
  "success": true,
  "message": "分配成功",
  "data": [
    {
      "palletNo": null,
      "barNo": "SN12345",
      "binNo": "L005A1001",
      "shelfNo": "L005A"
    }
  ]
}
```

#### POST /api/entry/commit

**描述**：确认入库

**请求**：
```json
{
  "items": [
    {
      "barNo": "SN12345",
      "binNo": "L005A1001",
      "shelfNo": "L005A"
    }
  ],
  "warehouseLocation": "601"
}
```

**响应**：
```json
{
  "success": true,
  "message": "入库成功",
  "data": [...]
}
```

### 5.3 拣货 API

#### POST /api/pick/reserve

**描述**：查询并锁定条码

**请求**：
```json
{
  "docNo": "MO20260325001",
  "warehouseLocation": "601"
}
```

**响应**：
```json
{
  "success": true,
  "message": "查询并锁定成功，共5条",
  "data": [
    {
      "productNo": "PN001",
      "barNo": "SN12345",
      "barQty": 10,
      "requiredQty": 5,
      "binNo": "L005A1001"
    }
  ]
}
```

#### POST /api/pick/lock

**描述**：手动锁定条码

**请求**：
```json
{
  "docNo": "MO20260325001",
  "warehouseLocation": "601",
  "barNolist": [
    {
      "productNo": "PN001",
      "barNo": "SN12345",
      "barQty": 10,
      "requiredQty": 5,
      "binNo": "L005A1001"
    }
  ]
}
```

#### POST /api/pick/unlock

**描述**：解锁条码

**请求**：同 lock

#### POST /api/pick/complete

**描述**：拣货完成

**请求**：
```json
{
  "docNo": "MO20260325001",
  "warehouseLocation": "601",
  "binNos": ["L005A1001", "L005A1002"]
}
```

#### POST /api/pick/lockedbarcode

**描述**：查询已锁定条码

**请求**：
```json
{
  "docNo": "MO20260325001",
  "warehouseLocation": "601"
}
```

### 5.4 更新检查 API

#### GET /api/update/check

**描述**：检查更新

**参数**：
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| appId | string | 是 | 应用ID（wmsapp） |
| platform | string | 是 | 平台（android） |
| currentVersionCode | int | 是 | 当前版本码 |
| channel | string | 否 | 渠道（prod） |

**响应**：
```json
{
  "hasUpdate": true,
  "forceUpdate": false,
  "latestVersionName": "1.0.10",
  "latestVersionCode": 10010,
  "minSupportedVersionCode": 10000,
  "downloadUrl": "http://10.50.77.246:5067/wmsapp-1.0.10-10010.apk",
  "sha256": "701C4D9332457E9F1437193C856552BF4A40EE3F551E8B5168FD9A0DC2945412",
  "releaseNotes": "拣货完成后自动更新条码状态为已出库"
}
```

### 5.5 亮灯服务 API

#### POST /api/services/app/LightBarOtherRuleService/LightUpSomeLampBeads

**描述**：控制库位亮灯

**请求**：
```json
{
  "jsonData": [
    { "location": "L005A1001", "color": 2 },
    { "location": "L005A1002", "color": 2 }
  ]
}
```

**颜色编码**：
| 值 | 颜色 |
|----|------|
| 0 | 熄灯 |
| 1 | 红色 |
| 2 | 绿色 |
| 3 | 蓝色 |
| 4 | 黄色 |
| 5 | 粉色 |
| 6 | 青色 |
| 7 | 白色 |

---

## 6. 业务流程

### 6.1 入库流程

```
┌─────────────────────────────────────────────────────────────────┐
│                        入库操作流程                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  1. 输入库位号 → Enter → 焦点跳转到条码输入框                    │
│                         │                                        │
│                         ▼                                        │
│  2. 扫描条码 → Enter → 调用 POST /api/entry/allocate             │
│                         │                                        │
│           ┌─────────────┼─────────────┐                         │
│           ▼             ▼             ▼                         │
│        [成功]        [失败]       [已入库]                       │
│           │             │             │                         │
│           ▼             ▼             ▼                         │
│ 自动识别料架并亮绿灯  显示错误     显示"条码已入库"                 │
│      进入等待确认    保持空闲     保持空闲                        │
│           │                                                      │
│           ▼                                                      │
│  3. 扫描库位号二次确认 → Enter                                   │
│                         │                                        │
│           ┌─────────────┼─────────────┐                         │
│           ▼             ▼                                        │
│       [验证成功]     [验证失败]                                   │
│           │             │                                        │
│           ▼             ▼                                        │
│  调用 POST /api/entry/commit  显示错误                           │
│           │             保持等待确认                              │
│           ▼                                                      │
│  4. 入库成功 → 熄灯 → 清空输入 → 焦点回到库位号                   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 6.2 拣货流程

```
┌─────────────────────────────────────────────────────────────────┐
│                        拣货操作流程                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  1. 输入领料单号 → 点击查询/Enter                                │
│                         │                                        │
│                         ▼                                        │
│  2. 调用 POST /api/pick/reserve                                  │
│                         │                                        │
│           ┌─────────────┼─────────────┐                         │
│           ▼             ▼             ▼                         │
│        [成功]        [失败]       [已有锁定]                     │
│           │             │             │                         │
│           ▼             ▼             ▼                         │
│      锁定条码       显示错误     加载已有锁定                     │
│      自动亮红灯     保持空闲      自动亮红灯                       │
│           │                                                      │
│           ▼                                                      │
│  3. 按亮灯位置拣货（可单行亮灯、切换颜色）                        │
│                         │                                        │
│                         ▼                                        │
│  4. 点击"拣货完成" → 调用 POST /api/pick/complete                │
│                         │                                        │
│           ┌─────────────┼─────────────┐                         │
│           ▼             ▼                                        │
│        [成功]        [失败]                                       │
│           │             │                                        │
│           ▼             ▼                                        │
│      解除库位占用    显示错误                                     │
│      清理锁定记录    保持状态                                     │
│      更新条码已出库                                               │
│      熄灯、清空列表                                               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 7. 非功能需求

### 7.1 性能要求

| 指标 | 要求 |
|------|------|
| API 响应时间 | < 2 秒（正常网络） |
| 亮灯响应时间 | < 1 秒 |
| 条码扫描识别 | < 500ms |
| 并发用户数 | 支持 10+ 操作员同时操作 |

### 7.2 可用性要求

| 指标 | 要求 |
|------|------|
| 系统可用性 | 99%（工作时间内） |
| 故障恢复时间 | < 30 分钟 |

### 7.3 安全要求

| 要求 | 说明 |
|------|------|
| 网络隔离 | 仅内网访问 |
| 并发控制 | 数据库事务 + 乐观锁 |
| 数据备份 | 每日自动备份 |

### 7.4 兼容性要求

| 平台 | 版本要求 |
|------|---------|
| Android | Android 7.0+ |
| Windows | Windows 10+ |
| 浏览器 | Chrome 90+, Edge 90+ |

---

## 8. 部署说明

### 8.1 服务端部署

#### 8.1.1 环境要求

| 组件 | 版本 |
|------|------|
| 操作系统 | Windows Server 2016+ |
| .NET Runtime | .NET 9.0 |
| 数据库 | SQL Server 2016+ |
| IIS | IIS 10+ |

#### 8.1.2 配置文件

**appsettings.json**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=10.10.5.170;Initial Catalog=sWMS_Production;..."
  },
  "AppUpdate": {
    "Enabled": true,
    "DefaultChannel": "prod",
    "Releases": [...]
  }
}
```

#### 8.1.3 数据库迁移

执行顺序：
1. `sql/20260313_pick_lock_indexes.sql` - 添加索引
2. `sql/20260319_add_bin_size.sql` - 添加 BinSize 字段
3. `sql/20260320_add_warehouse_to_locked_barno.sql` - 添加仓库隔离字段

### 8.2 客户端部署

#### 8.2.1 Android 部署

1. 发布 APK 到 `artifacts/android-release/<版本>/`
2. 上传 APK 到内网 releases 目录
3. 更新 appsettings.json 中的 Releases 配置
4. 重启 SmartFactoryWebApi 服务

#### 8.2.2 桌面部署

1. 发布到 `artifacts/desktop-release/<版本>/`
2. 复制到客户端机器
3. 运行 WMSApp.Desktop.exe

### 8.3 发布流程

```bash
# 1. 先发 Android（会更新 WebAPI 的 appsettings）
powershell -File scripts/publish.ps1 -ReleaseNotes "更新说明" -IsMandatory $true

# 2. 再发 WebAPI（包含最新的版本配置）
powershell -File scripts/publish-webapi-simple.ps1

# 3. 部署
# - 复制 webapi 发布文件到服务器
# - 上传 APK 到内网 releases 目录
# - 重启服务
```

---

## 附录

### A. 术语表

| 术语 | 说明 |
|------|------|
| 条码 | 货物的唯一标识，可扫描识别 |
| 托盘码 | 整托盘货物的标识，包含多个条码 |
| 库位 | 货架上的具体存放位置 |
| 领料单 | 生产部门发起的物料需求单据 |
| FIFO | 先进先出（First In First Out） |
| 大盘货架 | 单个库位跨越多个物理空间的货架 |

### B. 参考文档

- [CHANGELOG.md](CHANGELOG.md) - 版本更新日志
- [technical-implementation.zh-CN.md](technical-implementation.zh-CN.md) - 技术实现文档
- [picking-lock-button-guide.zh-CN.md](picking-lock-button-guide.zh-CN.md) - 拣货锁定功能说明
