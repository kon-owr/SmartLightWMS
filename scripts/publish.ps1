# PowerShell entry point for Android publish
# Avoids shell escaping issues when called from Bash/Git Bash
param(
    [string]$VersionName,
    [string]$ReleaseNotes = "Routine release",
    [int]$MinSupportedVersionCode,
    [bool]$IsMandatory = $false,
    [switch]$DryRun,
    [switch]$SkipPublish
)

# Forward all parameters to the main script
$params = @{}

if ($VersionName) { $params['VersionName'] = $VersionName }
if ($ReleaseNotes) { $params['ReleaseNotes'] = $ReleaseNotes }
if ($MinSupportedVersionCode) { $params['MinSupportedVersionCode'] = $MinSupportedVersionCode }
if ($IsMandatory) { $params['IsMandatory'] = $IsMandatory }
if ($DryRun) { $params['DryRun'] = $DryRun }
if ($SkipPublish) { $params['SkipPublish'] = $SkipPublish }

& "$PSScriptRoot/publish-android.ps1" @params
