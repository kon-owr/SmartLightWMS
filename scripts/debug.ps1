# WMSApp Debug Startup Script
# Start WebAPI first, then start WMSApp.Desktop

param(
    [switch]$WebAPIOnly,
    [switch]$DesktopOnly,
    [int]$Delay = 3
)

$ErrorActionPreference = "Stop"

$webapiPath = Join-Path $PSScriptRoot "..\..\SmartFactoryWebApi\SmartFactoryWebApi\SmartFactoryWebApi.csproj"
$desktopPath = Join-Path $PSScriptRoot "..\WMSApp.Desktop\WMSApp.Desktop.csproj"

function Start-WebAPI {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Starting WebAPI (Development)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    $env:ASPNETCORE_ENVIRONMENT = "Development"

    Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", $webapiPath -NoNewWindow
    Write-Host "WebAPI starting... (http://localhost:5067)" -ForegroundColor Green
}

function Start-Desktop {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Starting WMSApp.Desktop (Development)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    $env:DOTNET_ENVIRONMENT = "Development"

    Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", $desktopPath
    Write-Host "WMSApp.Desktop starting..." -ForegroundColor Green
}

# Main logic
if ($WebAPIOnly) {
    Start-WebAPI
}
elseif ($DesktopOnly) {
    Start-Desktop
}
else {
    # Start WebAPI first
    Start-WebAPI

    # Wait for WebAPI to start
    Write-Host ""
    Write-Host "Waiting for WebAPI to start ($Delay seconds)..." -ForegroundColor Yellow
    Start-Sleep -Seconds $Delay

    # Then start Desktop
    Start-Desktop

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Debug environment started!" -ForegroundColor Green
    Write-Host "WebAPI: http://localhost:5067" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
}
