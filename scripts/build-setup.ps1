# Automated Build & Packaging Script for ChattyWidgets and ChattyWidgetsSetup
$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Building ChattyWidgets & Setup v1.0.2  " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$rootDir = Split-Path -Parent $PSScriptRoot
Set-Location $rootDir

$appPublishDir = Join-Path $rootDir "bin\Publish\app"
$setupPublishDir = Join-Path $rootDir "bin\Publish\setup"
$setupZipPath = Join-Path $rootDir "Setup\app.zip"
$otaZipPath = Join-Path $setupPublishDir "ChattyWidgets-Update.zip"
$finalSetupExe = Join-Path $setupPublishDir "ChattyWidgetsSetup.exe"

# 1. Clean previous build artifacts
Write-Host "`n[1/4] Cleaning previous output directories..." -ForegroundColor Yellow
if (Test-Path $appPublishDir) { Remove-Item -Recurse -Force $appPublishDir }
if (Test-Path $setupZipPath) { Remove-Item -Force $setupZipPath }
New-Item -ItemType Directory -Force -Path $setupPublishDir | Out-Null

# 2. Publish Main Application
Write-Host "`n[2/4] Publishing ChattyWidgets (Release)..." -ForegroundColor Yellow
dotnet publish ChattyWidgets.csproj -c Release -o $appPublishDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish ChattyWidgets.csproj"
}

# 3. Create app.zip payload
Write-Host "`n[3/4] Creating application payload archive (app.zip)..." -ForegroundColor Yellow
Compress-Archive -Path "$appPublishDir\*" -DestinationPath $setupZipPath -Force
Copy-Item -Path $setupZipPath -Destination $otaZipPath -Force
$zipSize = (Get-Item $setupZipPath).Length / 1MB
Write-Host ("Archive created: {0:N2} MB" -f $zipSize) -ForegroundColor Green

# 4. Publish Setup Single-File
Write-Host "`n[4/4] Publishing ChattyWidgetsSetup.exe..." -ForegroundColor Yellow
dotnet publish Setup/ChattyWidgets.Setup.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $setupPublishDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish Setup/ChattyWidgets.Setup.csproj"
}

if (Test-Path $finalSetupExe) {
    $exeSize = (Get-Item $finalSetupExe).Length / 1MB
    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($finalSetupExe)
    Write-Host "`n==========================================" -ForegroundColor Green
    Write-Host "BUILD SUCCESSFUL!" -ForegroundColor Green
    Write-Host ("Installer: {0} ({1:N2} MB, v{2})" -f $finalSetupExe, $exeSize, $versionInfo.FileVersion) -ForegroundColor Green
    Write-Host ("OTA Zip:   {0} ({1:N2} MB)" -f $otaZipPath, $zipSize) -ForegroundColor Green
    Write-Host "==========================================" -ForegroundColor Green
} else {
    Write-Error "Setup executable not found at $finalSetupExe"
}
