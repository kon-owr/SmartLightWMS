# WMSApp

智能仓储管理系统（WMS）跨平台客户端，基于 Avalonia UI 构建，支持 Android PDA 和 Windows Desktop。

## 技术栈

| 技术 | 版本/说明 |
|------|-----------|
| .NET | 9.0 |
| UI 框架 | Avalonia UI + Semi.Avalonia 主题 |
| MVVM | CommunityToolkit.Mvvm |
| 平台 | Android (arm64) / Windows Desktop |
| 后端 API | [SmartFactoryWebApi](../SmartFactoryWebApi/) |

## 项目结构

```
WMSApp/
├── WMSApp/                    # 核心共享库（ViewModels, Views, Services, DTO）
├── WMSApp.Android/            # Android 平台入口
├── WMSApp.Desktop/            # Windows 桌面平台入口
├── scripts/                   # 发版自动化脚本
└── docs/                      # 项目文档
```

## 核心功能

### 入库（Entry）
- 输入库位号并扫描条码/托盘码，系统自动识别所属料架并分配库位（支持升序/降序、大盘货架步长）
- 智能货架亮灯引导放置
- 二次确认防误操作
- 批量提交入库（事务性：更新库存、标记库位占用）

### 拣货（Picking）
- 按领料单号查询，FIFO 先进先出条码分配
- 自动锁定条码（Serializable 隔离级别，幂等安全）
- 智能货架亮灯引导拣货
- 完成拣货自动释放库位、更新条码出库状态

### 感应式出入库（Induction）
- 基于 SignalR 实时通信的感应式货架交互
- 入库：感应货架自动识别托盘 → 自动分配库位 → 亮灯引导
- 出库：扫码领料单 → 感应货架自动亮灯引导拣货
- 支持页面生命周期管理（`IPageLifecycleAware`），进出页面自动连接/断开 Hub
- 实时推送亮灯指令，替代手动触发

### 自动更新
- 启动时自动检查版本更新
- 支持强制更新/可选更新
- Android APK 下载安装（SHA256 校验）
- 更新说明中文展示

## 配置

### appsettings.json（嵌入资源）

```json
{
  "Warehouses": {
    "Current": "616",
    "Allowed": ["601", "616", "621"]
  },
  "Api": {
    "SmartFactory": {
      "BaseAddress": "http://10.50.77.246:5067"
    },
    "LightService": {
      "BaseAddress": "http://10.50.77.246:8091"
    }
  }
}
```

### 环境配置

| 文件 | 环境 | 说明 |
|------|------|------|
| `appsettings.json` | 基础 | 生产环境地址 |
| `appsettings.Development.json` | Development | localhost 调试地址 |

环境通过 `DOTNET_ENVIRONMENT` 环境变量切换。

## 快速开始

### 前置条件

- .NET 9 SDK
- Android SDK（Android 开发）
- Visual Studio 2022 / VS Code + Avalonia 扩展

### 调试运行

```powershell
# 一键调试（WebAPI + Desktop）
powershell -File scripts/debug.ps1

# 仅 Desktop
powershell -File scripts/debug.ps1 -DesktopOnly

# 仅 WebAPI
powershell -File scripts/debug.ps1 -WebAPIOnly
```

### 发布

```powershell
# 1. 发布 Android（自动更新 WebAPI 配置）
powershell -File scripts/publish.ps1 -ReleaseNotes "更新说明" -IsMandatory $true

# 2. 发布 WebAPI
powershell -File scripts/publish-webapi-simple.ps1
```

> **必须先发 Android 再发 WebAPI**，Android 发布会更新 WebAPI 的版本配置。

详见 [scripts/README.md](scripts/README.md)。

## API 依赖

| 服务 | 地址 | 说明 |
|------|------|------|
| SmartFactoryWebApi | http://10.50.77.246:5067 | 主后端 API |
| LightService | http://10.50.77.246:8091 | 智能货架灯光控制 |

## 文档

| 文档 | 说明 |
|------|------|
| [docs/requirements.md](docs/requirements.md) | 需求规格说明书 |
| [docs/CHANGELOG.md](docs/CHANGELOG.md) | 版本变更日志 |
| [docs/technical-implementation.zh-CN.md](docs/technical-implementation.zh-CN.md) | 技术实现文档 |
| [docs/induction-inout-test-plan.zh-CN.md](docs/induction-inout-test-plan.zh-CN.md) | 感应式出入库测试计划 |
| [docs/commenting-guidelines.zh-CN.md](docs/commenting-guidelines.zh-CN.md) | 代码注释规范 |
| [scripts/README.md](scripts/README.md) | 发版脚本使用说明 |

## 版本

当前版本：**1.0.13**

版本号规则：`major.minor.patch`，VersionCode = `major * 10000 + minor * 100 + patch`
