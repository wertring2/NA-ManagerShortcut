# RunAsAdmin.ps1
# Script to run the Network Adapter Manager with Administrator privileges

$appPath = ".\NA-ManagerShortcut\bin\Debug\net9.0-windows\NA-ManagerShortcut.exe"

if (Test-Path $appPath) {
    Start-Process -FilePath $appPath -Verb RunAs
} else {
    Write-Host "Application not found. Please build the project first." -ForegroundColor Red
    Write-Host "Run: dotnet build" -ForegroundColor Yellow
}