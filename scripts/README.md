# WMSApp 发版脚本

本目录包含 Android 客户端和 WebAPI 后端的发版自动化脚本。

## 脚本清单

| 脚本 | 用途 | 推荐调用方式 |
|------|------|-------------|
| `publish.ps1` | Android 发布入口 | PowerShell/Bash 直接调用 |
| `publish-android.ps1` | Android APK 发布（核心逻辑） | 被 `publish.ps1` 调用 |
| `publish-webapi-simple.ps1` | WebAPI 发布入口 | PowerShell/Bash 直接调用 |
| `publish-webapi.ps1` | WebAPI 发布（核心逻辑） | 被 `publish-webapi-simple.ps1` 调用 |

---

## 快速开始

### 完整发版流程

```bash
# 1. 先发 Android（会自动更新 WebAPI 的 appsettings.json）
powershell -File scripts/publish.ps1 -ReleaseNotes "更新说明" -IsMandatory $true

# 2. 再发 WebAPI（包含最新的版本配置）
powershell -File scripts/publish-webapi-simple.ps1
```

### 仅发布 Android

```bash
powershell -File scripts/publish.ps1 -ReleaseNotes "修复扫码稳定性" -IsMandatory $true
```

### 仅发布 WebAPI

```bash
powershell -File scripts/publish-webapi-simple.ps1
```

---

## 参数说明

### Android 发布参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `-VersionName` | string | 自动 patch+1 | 目标版本（如 1.0.8） |
| `-ReleaseNotes` | string | "Routine release" | 更新说明 |
| `-MinSupportedVersionCode` | int | 当前版本 | 最小支持版本码 |
| `-IsMandatory` | bool | false | 是否强制更新 |
| `-SkipPublish` | switch | false | 跳过打包（仅更新版本和配置） |
| `-DryRun` | switch | false | 干跑模式（验证不执行） |

### WebAPI 发布参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `-VersionName` | string | 自动 patch+1 | 目标版本（如 1.0.6） |
| `-Configuration` | string | Release | 构建配置 |
| `-Runtime` | string | win-x64 | 目标运行时 |
| `-SelfContained` | bool | false | 是否自包含 |
| `-DryRun` | switch | false | 干跑模式 |

---

## 调用方式

### 从 Bash / Git Bash 调用

```bash
powershell -File scripts/publish.ps1 -ReleaseNotes "更新说明" -IsMandatory $true
```

### 从 PowerShell 调用

```powershell
.\scripts\publish.ps1 -ReleaseNotes "更新说明" -IsMandatory $true
```

---

## 产出物

### Android 发布

```
artifacts/android-release/<版本号>/
├── wmsapp-<版本>-<版本码>.apk    # 签名 APK
├── release-metadata.json        # 发布元数据
├── checklist.txt                # 部署检查清单
└── configs/
    ├── appsettings.json         # 已更新的服务端配置
    └── appsettings.Production.json
```

### WebAPI 发布

```
artifacts/webapi-release/<版本号>/
├── publish/                      # 发布文件目录
│   ├── SmartFactoryWebApi.dll
│   ├── appsettings.json
│   ├── appsettings.Production.json
│   └── ...
├── release-metadata.json        # 发布元数据
└── checklist.txt                # 部署检查清单
```

---

## 部署步骤

### 1. 数据库迁移（如有）

执行 `SmartFactoryWebApi/sql/` 目录下的迁移脚本。

### 2. 部署 WebAPI

```bash
# 复制发布文件到服务器
xcopy /E /I artifacts\webapi-release\1.0.6\publish\* \\server\SmartFactoryWebApi\

# 重启服务
# 方式1: IIS 管理器回收应用程序池
# 方式2: 重启 Windows 服务
```

### 3. 部署 Android APK

```bash
# 复制 APK 到内网 releases 目录
copy artifacts\android-release\1.0.8\wmsapp-1.0.8-10008.apk \\server\releases\

# 复制配置文件（如果 WebAPI 未重新部署）
copy artifacts\android-release\1.0.8\configs\* \\server\SmartFactoryWebApi\
```

### 4. 验证更新

```bash
# 调用更新检查接口
curl "http://server:5067/api/update/check?currentVersionCode=10000&channel=prod"
```

---

## 版本规则

### 语义化版本

格式：`major.minor.patch`（如 `1.0.8`）

### Android VersionCode 计算

```
VersionCode = major * 10000 + minor * 100 + patch
```

示例：
- `1.0.8` → `10008`
- `1.1.0` → `10100`
- `2.0.0` → `20000`

---

## 常见问题

### Q: 为什么必须先发 Android 再发 WebAPI？

Android 发布脚本会更新 WebAPI 的 `appsettings.json` 中的 `AppUpdate.Releases` 配置。如果先发 WebAPI，发布包将不包含最新的版本信息。

### Q: 如何查看发布历史？

查看 `artifacts/android-release/` 和 `artifacts/webapi-release/` 目录下的 `release-metadata.json` 文件。

---

## 文件说明

| 文件 | 说明 |
|------|------|
| `README.md` | 本文件，发版脚本使用说明 |
| `README.publish.zh-CN.md` | 详细发版文档（中文） |
