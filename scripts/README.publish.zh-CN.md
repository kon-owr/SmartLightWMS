# WMSApp 发版脚本说明

本目录包含 Android 客户端和 WebAPI 后端的发版自动化脚本。

## 脚本文件

| 脚本 | 用途 | 入口 |
|------|------|------|
| `publish.ps1` | Android 发布入口（推荐直接调用） | ✓ |
| `publish-android.ps1` | Android APK 发布（核心逻辑） | - |
| `publish-webapi-simple.ps1` | WebAPI 发布入口（推荐直接调用） | ✓ |
| `publish-webapi.ps1` | SmartFactoryWebApi 发布（核心逻辑） | - |

---

## 重要：发版顺序

**必须先发 Android，再发 WebAPI！**

原因：Android 发版脚本会更新 WebAPI 的 `appsettings.json`（AppUpdate.Releases 配置）。
如果先发 WebAPI，发布包中的配置将不包含最新的版本信息。

**正确流程：**
```powershell
# 1. 先发 Android（会更新 WebAPI 的 appsettings）
powershell -File scripts/publish.ps1 -ReleaseNotes "更新说明" -IsMandatory $true

# 2. 再发 WebAPI（包含最新的 appsettings）
powershell -File scripts/publish-webapi-simple.ps1
```

---

## Android 发布

### 功能

一条命令完成以下动作：

1. 读取并自动升级核心项目 `WMSApp/WMSApp.csproj` 的 `Version`（默认 patch + 1）。
2. 执行 Android 发布命令并在发布时注入版本：
   - `ApplicationDisplayVersion`
   - `ApplicationVersion`
3. 计算最终 APK 的 `SHA256`。
4. 同步更新以下两个配置文件的 `AppUpdate.Releases`（追加历史记录）：
   - `../SmartFactoryWebApi/SmartFactoryWebApi/appsettings.json`
   - `../SmartFactoryWebApi/SmartFactoryWebApi/appsettings.Production.json`
5. 在 `artifacts/android-release/<版本号>/` 产出：
   - 规范命名 APK
   - `release-metadata.json`
   - `checklist.txt`
   - 可直接替换服务端的 `configs/appsettings*.json`

### 快速使用

```powershell
# 从 PowerShell 或 Bash 调用
powershell -File scripts/publish.ps1 -ReleaseNotes "修复扫码稳定性" -IsMandatory $true

# 指定版本号
powershell -File scripts/publish.ps1 -VersionName "1.0.5" -ReleaseNotes "新功能"

# 干跑验证
powershell -File scripts/publish.ps1 -DryRun
```

### 常用参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `-VersionName` | 自动 patch+1 | 目标版本 |
| `-ReleaseNotes` | "Routine release" | 更新说明 |
| `-MinSupportedVersionCode` | 当前版本 | 最小支持版本 |
| `-IsMandatory` | false | 是否强更 |
| `-SkipPublish` | false | 跳过打包 |
| `-DryRun` | false | 干跑模式 |

### 发布后人工步骤

1. 将 `artifacts/android-release/<版本>/` 中的 APK 上传到内网 releases 目录
2. 用 `artifacts/.../configs` 中的配置文件替换服务端配置
3. 重启 SmartFactoryWebApi
4. 调用 `/api/update/check` 验证更新结果

---

## WebAPI 发布

### 功能

一条命令完成以下动作：

1. 读取并自动升级 `SmartFactoryWebApi.csproj` 的 `Version`（默认 patch + 1）。
2. 执行 `dotnet publish` 发布 WebAPI 项目。
3. 复制 `appsettings.Production.json` 到发布目录。
4. 生成发布文件清单。
5. 在 `artifacts/webapi-release/<版本号>/` 产出：
   - `publish/` 发布文件目录
   - `release-metadata.json`
   - `checklist.txt`

### 快速使用

```powershell
# 默认发布（patch+1）
powershell -File scripts/publish-webapi-simple.ps1

# 指定版本号
powershell -File scripts/publish-webapi-simple.ps1 -VersionName "1.0.2"

# 干跑验证
powershell -File scripts/publish-webapi-simple.ps1 -DryRun
```

### 常用参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `-VersionName` | 自动 patch+1 | 目标版本 |
| `-Configuration` | Release | 构建配置 |
| `-Runtime` | win-x64 | 目标运行时 |
| `-SelfContained` | false | 是否自包含 |
| `-DryRun` | false | 干跑模式 |

### 发布后人工步骤

1. 将 `artifacts/webapi-release/<版本>/publish/` 部署到服务器
2. 确认 `appsettings.Production.json` 配置正确
3. 重启 SmartFactoryWebApi 服务（或 IIS 站点）
4. 调用 `/api/update/check` 验证服务正常

---

## 版本规则

### 语义化版本格式

- 格式：`major.minor.patch`（如 `1.0.4`）
- 脚本默认自动递增 patch 版本

### Android VersionCode 计算

```
VersionCode = major * 10000 + minor * 100 + patch
```

示例：`1.0.4` → `10004`

---

## 产出物目录结构

```
artifacts/
├── android-release/
│   └── 1.0.5/
│       ├── wmsapp-1.0.5-10005.apk
│       ├── release-metadata.json
│       ├── checklist.txt
│       └── configs/
│           ├── appsettings.json
│           └── appsettings.Production.json
└── webapi-release/
    └── 1.0.2/
        ├── publish/
        │   ├── SmartFactoryWebApi.dll
        │   ├── appsettings.Production.json
        │   └── ...
        ├── release-metadata.json
        └── checklist.txt
```
