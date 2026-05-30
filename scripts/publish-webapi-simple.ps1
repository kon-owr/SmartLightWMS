# PowerShell entry point for WebAPI publish
# Avoids shell escaping issues when called from Bash/Git Bash
param(
    [string]$VersionName,
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $false,
    [switch]$DryRun
)

# Forward all parameters to the main script
$params = @{}

if ($VersionName) { $params['VersionName'] = $VersionName }
if ($Configuration) { $params['Configuration'] = $Configuration }
if ($Runtime) { $params['Runtime'] = $Runtime }
if ($SelfContained) { $params['SelfContained'] = $SelfContained }
if ($DryRun) { $params['DryRun'] = $DryRun }

& "$PSScriptRoot/publish-webapi.ps1" @params
