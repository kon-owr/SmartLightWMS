param(
    [string]$VersionName,
    [string]$Configuration = "Release",
    [string]$Framework = "net9.0",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $false,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# 设置控制台输出编码为 UTF-8（解决中文 Windows 乱码问题）
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

function Get-NextPatchVersion {
    param([string]$CurrentVersion)

    $parts = $CurrentVersion.Split('.')
    if ($parts.Count -lt 3) {
        throw "Version must be major.minor.patch format, e.g. 1.0.0"
    }

    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2] + 1
    return "$major.$minor.$patch"
}

function Set-ProjectVersion {
    param(
        [string]$ProjectPath,
        [string]$TargetVersionName,
        [bool]$IsDryRun
    )

    [xml]$xml = Get-Content -Path $ProjectPath -Raw
    $propertyGroup = $xml.Project.PropertyGroup | Select-Object -First 1
    if (-not $propertyGroup) {
        throw "No PropertyGroup found in project file."
    }

    if (-not $propertyGroup.Version) {
        $versionNode = $xml.CreateElement("Version")
        $versionNode.InnerText = $TargetVersionName
        [void]$propertyGroup.AppendChild($versionNode)
    }
    else {
        $propertyGroup.Version = $TargetVersionName
    }

    if (-not $propertyGroup.InformationalVersion) {
        $infoNode = $xml.CreateElement("InformationalVersion")
        $infoNode.InnerText = '$(Version)'
        [void]$propertyGroup.AppendChild($infoNode)
    }

    if ($IsDryRun) {
        Write-Host "[DryRun] Version will be updated to $TargetVersionName"
        return
    }

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.OmitXmlDeclaration = $true
    $settings.NewLineChars = "`r`n"
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)

    $writer = [System.Xml.XmlWriter]::Create($ProjectPath, $settings)
    $xml.Save($writer)
    $writer.Dispose()
}

function Get-PublishFileManifest {
    param([string]$PublishPath)

    $files = Get-ChildItem -Path $PublishPath -Recurse -File |
        Select-Object -First 100 |
        ForEach-Object {
            $relativePath = $_.FullName.Substring($PublishPath.Length).TrimStart('\', '/')
            [pscustomobject]@{
                Path = $relativePath
                Size = $_.Length
                LastModified = $_.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
            }
        }

    return $files
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$wmsRoot = Resolve-Path (Join-Path $scriptDir "..")
$webApiProjectDir = Join-Path $wmsRoot "..\SmartFactoryWebApi\SmartFactoryWebApi"
$webApiProjectPath = Join-Path $webApiProjectDir "SmartFactoryWebApi.csproj"
$webApiAppsettingsProd = Join-Path $webApiProjectDir "appsettings.Production.json"

if (-not (Test-Path $webApiProjectPath)) {
    throw "WebAPI project file not found: $webApiProjectPath"
}

# 读取当前版本
[xml]$projectXml = Get-Content -Path $webApiProjectPath -Raw
$propertyGroup = $projectXml.Project.PropertyGroup | Select-Object -First 1
$currentVersionName = if ($propertyGroup.Version) { "$($propertyGroup.Version)" } else { "1.0.0" }
$targetVersionName = if ([string]::IsNullOrWhiteSpace($VersionName)) { Get-NextPatchVersion -CurrentVersion $currentVersionName } else { $VersionName }

Write-Host "WebAPI version: $currentVersionName -> $targetVersionName"

# 更新项目版本
Set-ProjectVersion -ProjectPath $webApiProjectPath -TargetVersionName $targetVersionName -IsDryRun:$DryRun

# 创建输出目录
$artifactsRoot = Join-Path $wmsRoot "artifacts\webapi-release\$targetVersionName"
if (-not $DryRun) {
    New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
    $publishOutputPath = Join-Path $artifactsRoot "publish"
    New-Item -ItemType Directory -Path $publishOutputPath -Force | Out-Null
}

# 执行发布
$publishArgs = @(
    "publish",
    $webApiProjectPath,
    "-c", $Configuration,
    "-f", $Framework,
    "-r", $Runtime,
    "-p:SelfContained=$SelfContained",
    "-o", $publishOutputPath
)

Write-Host "Publish command: dotnet $($publishArgs -join ' ')"
if (-not $DryRun) {
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    # 复制生产配置文件到发布目录
    if (Test-Path $webApiAppsettingsProd) {
        Copy-Item -Path $webApiAppsettingsProd -Destination $publishOutputPath -Force
        Write-Host "Copied appsettings.Production.json to publish directory"
    }
}

# 生成文件清单
$fileManifest = @()
if (-not $DryRun) {
    $fileManifest = Get-PublishFileManifest -PublishPath $publishOutputPath
}

$publishedAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:sszzz")

# 生成元数据
$metadata = [pscustomobject]@{
    VersionName = $targetVersionName
    Configuration = $Configuration
    Framework = $Framework
    Runtime = $Runtime
    SelfContained = $SelfContained
    PublishedAt = $publishedAt
    PublishPath = $publishOutputPath
    FileCount = $fileManifest.Count
    Files = $fileManifest
}

# 生成 checklist
$checklistText = @(
    "[Release Checklist - SmartFactoryWebApi v$targetVersionName]",
    "1. Deploy publish folder to server: $artifactsRoot\publish",
    "2. Verify appsettings.Production.json is included",
    "3. Restart SmartFactoryWebApi service (or IIS site)",
    "4. Verify API endpoint: GET /api/update/check?appId=wmsapp&platform=android&currentVersionCode=0&channel=prod",
    "5. Check health endpoint if available",
    "",
    "Build info:",
    "  - Configuration: $Configuration",
    "  - Framework: $Framework",
    "  - Runtime: $Runtime",
    "  - SelfContained: $SelfContained",
    "  - PublishedAt: $publishedAt"
)

if (-not $DryRun) {
    $metadataPath = Join-Path $artifactsRoot "release-metadata.json"
    $checklistPath = Join-Path $artifactsRoot "checklist.txt"

    $metadataJson = $metadata | ConvertTo-Json -Depth 20
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($metadataPath, $metadataJson, $utf8NoBom)
    [System.IO.File]::WriteAllLines($checklistPath, $checklistText, $utf8NoBom)

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "WebAPI Release Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Version: $targetVersionName"
    Write-Host "Output: $artifactsRoot"
    Write-Host "Files: $($fileManifest.Count)"
    Write-Host ""
}
else {
    Write-Host ""
    Write-Host "[DryRun] Script validation complete."
    Write-Host "[DryRun] Version would be: $currentVersionName -> $targetVersionName"
}
