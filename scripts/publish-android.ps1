param(
    [string]$VersionName,
    [string]$Configuration = "Release",
    [string]$Framework = "net9.0-android",
    [string]$RuntimeIdentifier = "android-arm64",
    [string]$AppId = "wmsapp",
    [string]$Platform = "android",
    [string]$Channel = "prod",
    [string]$ReleaseNotes = "Routine release",
    [int]$MinSupportedVersionCode = -1,
    [bool]$IsMandatory = $false,
    [string]$DownloadUrlBase = "",
    [switch]$SkipPublish,
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
        throw "Core version must be major.minor.patch format, e.g. 1.0.0"
    }

    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2] + 1
    return "$major.$minor.$patch"
}

function Convert-VersionNameToCode {
    param([string]$InputVersion)

    $parts = $InputVersion.Split('.')
    if ($parts.Count -lt 3) {
        throw "VersionName must be major.minor.patch format, e.g. 1.0.1"
    }

    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2]
    return ($major * 10000) + ($minor * 100) + $patch
}

function Set-CoreVersion {
    param(
        [string]$CoreProjectPath,
        [string]$TargetVersionName,
        [bool]$IsDryRun
    )

    [xml]$xml = Get-Content -Path $CoreProjectPath -Raw
    $propertyGroup = $xml.Project.PropertyGroup | Select-Object -First 1
    if (-not $propertyGroup) {
        throw "No PropertyGroup found in core project file."
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
        Write-Host "[DryRun] Core version will be updated to $TargetVersionName"
        return
    }

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.OmitXmlDeclaration = $true
    $settings.NewLineChars = "`r`n"
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)

    $writer = [System.Xml.XmlWriter]::Create($CoreProjectPath, $settings)
    $xml.Save($writer)
    $writer.Dispose()
}

function Ensure-AppUpdateShape {
    param(
        [object]$Json,
        [string]$DefaultChannel
    )

    if (-not $Json.PSObject.Properties.Match('AppUpdate').Count) {
        $Json | Add-Member -MemberType NoteProperty -Name AppUpdate -Value ([pscustomobject]@{})
    }

    if (-not $Json.AppUpdate.PSObject.Properties.Match('Enabled').Count) {
        $Json.AppUpdate | Add-Member -MemberType NoteProperty -Name Enabled -Value $true
    }

    if (-not $Json.AppUpdate.PSObject.Properties.Match('DefaultChannel').Count) {
        $Json.AppUpdate | Add-Member -MemberType NoteProperty -Name DefaultChannel -Value $DefaultChannel
    }

    if (-not $Json.AppUpdate.PSObject.Properties.Match('Releases').Count) {
        $Json.AppUpdate | Add-Member -MemberType NoteProperty -Name Releases -Value @()
    }

    if ($null -eq $Json.AppUpdate.Releases) {
        $Json.AppUpdate.Releases = @()
    }
}

function Update-AppSettings {
    param(
        [string]$ConfigPath,
        [string]$TargetVersionName,
        [int]$TargetVersionCode,
        [string]$TargetDownloadUrlBase,
        [string]$TargetDownloadUrl,
        [string]$TargetSha256,
        [string]$TargetPublishedAt,
        [bool]$IsDryRun
    )

    $raw = [System.IO.File]::ReadAllText($ConfigPath, [System.Text.Encoding]::UTF8)
    $json = $raw | ConvertFrom-Json
    Ensure-AppUpdateShape -Json $json -DefaultChannel $Channel

    $effectiveMinSupported = if ($MinSupportedVersionCode -ge 0) { $MinSupportedVersionCode } else { $TargetVersionCode }

    # 统一历史下载地址基址，避免旧配置仍指向错误端口
    foreach ($release in @($json.AppUpdate.Releases)) {
        if ($null -eq $release -or [string]::IsNullOrWhiteSpace($release.DownloadUrl)) {
            continue
        }

        $apkName = ""
        try {
            $uri = [Uri]$release.DownloadUrl
            $apkName = [System.IO.Path]::GetFileName($uri.AbsolutePath)
        }
        catch {
            $apkName = [System.IO.Path]::GetFileName([string]$release.DownloadUrl)
        }

        if (-not [string]::IsNullOrWhiteSpace($apkName)) {
            $release.DownloadUrl = "$TargetDownloadUrlBase/$apkName"
        }
    }

    $newRelease = [pscustomobject]@{
        AppId = $AppId
        Platform = $Platform
        Channel = $Channel
        VersionName = $TargetVersionName
        VersionCode = $TargetVersionCode
        MinSupportedVersionCode = $effectiveMinSupported
        IsMandatory = $IsMandatory
        DownloadUrl = $TargetDownloadUrl
        Sha256 = $TargetSha256
        ReleaseNotes = $ReleaseNotes
        PublishedAt = $TargetPublishedAt
    }

    $existing = @($json.AppUpdate.Releases)
    $filtered = $existing | Where-Object {
        -not (
            $_.AppId -eq $AppId -and
            $_.Platform -eq $Platform -and
            $_.Channel -eq $Channel -and
            [int]$_.VersionCode -eq $TargetVersionCode
        )
    }

    $json.AppUpdate.Releases = @((@($filtered) + @($newRelease)) | Sort-Object { [int]$_.VersionCode })

    if ($IsDryRun) {
        Write-Host "[DryRun] Config will be updated: $ConfigPath"
        return
    }

    $jsonText = $json | ConvertTo-Json -Depth 30
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($ConfigPath, $jsonText, $utf8NoBom)
}

function Resolve-DownloadUrlBase {
    param(
        [string]$ClientAppsettingsPath
    )

    if (-not (Test-Path $ClientAppsettingsPath)) {
        throw "WMSApp appsettings not found: $ClientAppsettingsPath"
    }

    $raw = [System.IO.File]::ReadAllText($ClientAppsettingsPath, [System.Text.Encoding]::UTF8)
    $json = $raw | ConvertFrom-Json
    $smartFactoryBase = [string]$json.Api.SmartFactory.BaseAddress

    if ([string]::IsNullOrWhiteSpace($smartFactoryBase)) {
        throw "Api:SmartFactory:BaseAddress is missing in $ClientAppsettingsPath"
    }

    $baseWithSlash = if ($smartFactoryBase.EndsWith('/')) { $smartFactoryBase } else { "$smartFactoryBase/" }
    return "${baseWithSlash}releases"
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$wmsRoot = Resolve-Path (Join-Path $scriptDir "..")
$clientAppsettingsPath = Join-Path $wmsRoot "WMSApp\appsettings.json"
$coreProjectPath = Join-Path $wmsRoot "WMSApp\WMSApp.csproj"
$androidProjectPath = Join-Path $wmsRoot "WMSApp.Android\WMSApp.Android.csproj"
$webApiProjectDir = Resolve-Path (Join-Path $wmsRoot "..\SmartFactoryWebApi\SmartFactoryWebApi")
$appsettingsDev = Join-Path $webApiProjectDir "appsettings.json"
$appsettingsProd = Join-Path $webApiProjectDir "appsettings.Production.json"

if (-not (Test-Path $coreProjectPath)) { throw "Core project file not found: $coreProjectPath" }
if (-not (Test-Path $androidProjectPath)) { throw "Android project file not found: $androidProjectPath" }
if (-not (Test-Path $appsettingsDev) -or -not (Test-Path $appsettingsProd)) { throw "SmartFactoryWebApi config files not found." }
if (-not (Test-Path $clientAppsettingsPath)) { throw "WMSApp appsettings file not found: $clientAppsettingsPath" }

if (-not [string]::IsNullOrWhiteSpace($DownloadUrlBase)) {
    Write-Host "[Info] -DownloadUrlBase is ignored. Download base is resolved from WMSApp/appsettings.json Api:SmartFactory:BaseAddress."
}

$resolvedDownloadUrlBase = Resolve-DownloadUrlBase -ClientAppsettingsPath $clientAppsettingsPath

[xml]$coreXml = Get-Content -Path $coreProjectPath -Raw
$coreGroup = $coreXml.Project.PropertyGroup | Select-Object -First 1
$currentVersionName = if ($coreGroup.Version) { "$($coreGroup.Version)" } else { "1.0.0" }
$targetVersionName = if ([string]::IsNullOrWhiteSpace($VersionName)) { Get-NextPatchVersion -CurrentVersion $currentVersionName } else { $VersionName }
$targetVersionCode = Convert-VersionNameToCode -InputVersion $targetVersionName

Write-Host "Core version: $currentVersionName -> $targetVersionName"
Write-Host "Android VersionCode: $targetVersionCode"

Set-CoreVersion -CoreProjectPath $coreProjectPath -TargetVersionName $targetVersionName -IsDryRun:$DryRun

$artifactsRoot = Join-Path $wmsRoot "artifacts\android-release\$targetVersionName"
if (-not $DryRun) {
    New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
}

$apkName = "wmsapp-$targetVersionName-$targetVersionCode.apk"
$downloadUrl = "$resolvedDownloadUrlBase/$apkName"
$publishedAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:sszzz")

if (-not $SkipPublish) {
    $publishArgs = @(
        "publish",
        $androidProjectPath,
        "-c", $Configuration,
        "-f", $Framework,
        "-p:AndroidPackageFormat=apk",
        "-p:RuntimeIdentifier=$RuntimeIdentifier",
        "-p:ApplicationDisplayVersion=$targetVersionName",
        "-p:ApplicationVersion=$targetVersionCode"
    )

    Write-Host "Publish command: dotnet $($publishArgs -join ' ')"
    if (-not $DryRun) {
        & dotnet @publishArgs
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed with exit code $LASTEXITCODE"
        }
    }
}

$sha256 = ""
$finalApkPath = Join-Path $artifactsRoot $apkName

if (-not $SkipPublish) {
    $publishRoot = Join-Path $wmsRoot "WMSApp.Android\bin\$Configuration\$Framework"
    $apkFile = Get-ChildItem -Path $publishRoot -Filter "*.apk" -Recurse |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $apkFile) {
        throw "No APK found under publish output directory: $publishRoot"
    }

    if (-not $DryRun) {
        Copy-Item -Path $apkFile.FullName -Destination $finalApkPath -Force
        $hash = Get-FileHash -Path $finalApkPath -Algorithm SHA256
        $sha256 = $hash.Hash
    }
    else {
        Write-Host "[DryRun] APK will be copied: $($apkFile.FullName) -> $finalApkPath"
        $sha256 = "DRYRUN_SHA256"
    }
}
else {
    $sha256 = "SKIPPED_SHA256"
}

Update-AppSettings -ConfigPath $appsettingsDev -TargetVersionName $targetVersionName -TargetVersionCode $targetVersionCode -TargetDownloadUrlBase $resolvedDownloadUrlBase -TargetDownloadUrl $downloadUrl -TargetSha256 $sha256 -TargetPublishedAt $publishedAt -IsDryRun:$DryRun
Update-AppSettings -ConfigPath $appsettingsProd -TargetVersionName $targetVersionName -TargetVersionCode $targetVersionCode -TargetDownloadUrlBase $resolvedDownloadUrlBase -TargetDownloadUrl $downloadUrl -TargetSha256 $sha256 -TargetPublishedAt $publishedAt -IsDryRun:$DryRun

$metadata = [pscustomobject]@{
    VersionName = $targetVersionName
    VersionCode = $targetVersionCode
    ApkName = $apkName
    ApkPath = $finalApkPath
    DownloadUrl = $downloadUrl
    Sha256 = $sha256
    AppId = $AppId
    Platform = $Platform
    Channel = $Channel
    MinSupportedVersionCode = if ($MinSupportedVersionCode -ge 0) { $MinSupportedVersionCode } else { $targetVersionCode }
    IsMandatory = $IsMandatory
    ReleaseNotes = $ReleaseNotes
    PublishedAt = $publishedAt
    UpdatedConfigFiles = @($appsettingsDev, $appsettingsProd)
}

$checklistText = @(
    "[Release Checklist]",
    "1. Upload APK to releases folder: $apkName",
    "2. Verify download URL is reachable: $downloadUrl",
    "3. Deploy config files:",
    "   - $appsettingsDev",
    "   - $appsettingsProd",
    "4. Restart SmartFactoryWebApi service",
    "5. Verify endpoint:",
    "   GET /api/update/check?appId=$AppId&platform=$Platform&currentVersionCode=$($targetVersionCode - 1)&channel=$Channel",
    "6. SHA256: $sha256"
)

if (-not $DryRun) {
    $metadataPath = Join-Path $artifactsRoot "release-metadata.json"
    $checklistPath = Join-Path $artifactsRoot "checklist.txt"
    $metadataJson = $metadata | ConvertTo-Json -Depth 20
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($metadataPath, $metadataJson, $utf8NoBom)
    [System.IO.File]::WriteAllLines($checklistPath, $checklistText, $utf8NoBom)

    $configOutDir = Join-Path $artifactsRoot "configs"
    New-Item -ItemType Directory -Path $configOutDir -Force | Out-Null
    Copy-Item $appsettingsDev (Join-Path $configOutDir "appsettings.json") -Force
    Copy-Item $appsettingsProd (Join-Path $configOutDir "appsettings.Production.json") -Force

    Write-Host "Release output: $artifactsRoot"
    Write-Host "APK: $finalApkPath"
    Write-Host "SHA256: $sha256"
}
else {
    Write-Host "[DryRun] Script validation complete."
}
