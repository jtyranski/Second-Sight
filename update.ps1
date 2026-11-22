# Updates Second Sight: pulls the latest code and rebuilds C:\SecondSight.
# Quit Claude Desktop first (Windows locks SecondSight.exe while it runs).
#
#   powershell -ExecutionPolicy Bypass -File .\update.ps1
param([string]$Output = "C:\SecondSight")

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (Get-Process -Name SecondSight -ErrorAction SilentlyContinue) {
    Write-Host "SecondSight is still running. Quit Claude Desktop (from the system tray icon), then run this again." -ForegroundColor Yellow
    exit 1
}

Write-Host "Pulling the latest code..."
git checkout main
git pull --ff-only
if ($LASTEXITCODE -ne 0) { throw "git pull failed." }

Write-Host "Building to $Output..."
dotnet publish src/SecondSight -c Release -o $Output
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

Write-Host "Done. Reopen Claude Desktop to use the new version." -ForegroundColor Green
