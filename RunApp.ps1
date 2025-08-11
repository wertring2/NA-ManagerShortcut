# PowerShell script to run Network Adapter Manager with Debug Support

Write-Host "Network Adapter Manager - Launcher" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

# Check for Administrator privileges
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")

if (-not $isAdmin) {
    Write-Host "⚠ WARNING: Not running as Administrator" -ForegroundColor Yellow
    Write-Host "Some features will not work without Administrator privileges:" -ForegroundColor Yellow
    Write-Host "  - Enable/Disable network adapters" -ForegroundColor Gray
    Write-Host "  - Change IP configuration" -ForegroundColor Gray
    Write-Host "  - Reset network adapters" -ForegroundColor Gray
    Write-Host ""
    
    $response = Read-Host "Do you want to restart with Administrator privileges? (Y/N)"
    
    if ($response -eq 'Y' -or $response -eq 'y') {
        Write-Host "Restarting with Administrator privileges..." -ForegroundColor Green
        Start-Process PowerShell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
        exit
    }
} else {
    Write-Host "✓ Running with Administrator privileges" -ForegroundColor Green
}

# Navigate to project directory
$projectPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectPath

# Build the project first
Write-Host ""
Write-Host "Building project..." -ForegroundColor Yellow
$buildOutput = & dotnet build -c Release 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Write-Host $buildOutput
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "✓ Build successful" -ForegroundColor Green

# Create debug directories
$debugPath = Join-Path $projectPath "debug_logs"
$claudePath = Join-Path $projectPath "claude_output"

if (-not (Test-Path $debugPath)) {
    New-Item -ItemType Directory -Path $debugPath | Out-Null
}

if (-not (Test-Path $claudePath)) {
    New-Item -ItemType Directory -Path $claudePath | Out-Null
}

# Path to executable
$exePath = Join-Path $projectPath "NA-ManagerShortcut\bin\Release\net9.0-windows\NA-ManagerShortcut.exe"

if (-not (Test-Path $exePath)) {
    # Try debug build
    $exePath = Join-Path $projectPath "NA-ManagerShortcut\bin\Debug\net9.0-windows\NA-ManagerShortcut.exe"
}

if (-not (Test-Path $exePath)) {
    Write-Host "Executable not found!" -ForegroundColor Red
    Write-Host "Expected at: $exePath" -ForegroundColor Gray
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host ""
Write-Host "===================================" -ForegroundColor Cyan
Write-Host "Starting Network Adapter Manager..." -ForegroundColor Green
Write-Host ""
Write-Host "Debug Shortcuts:" -ForegroundColor Yellow
Write-Host "  Ctrl+Shift+D - Open Debug Window" -ForegroundColor Gray
Write-Host "  Win+N        - Show/Hide Main Window" -ForegroundColor Gray
Write-Host ""
Write-Host "Debug output will be saved to:" -ForegroundColor Cyan
Write-Host "  $debugPath" -ForegroundColor Gray
Write-Host "  $claudePath" -ForegroundColor Gray
Write-Host ""
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

# Run the application
try {
    & $exePath
}
catch {
    Write-Host "Application error: $_" -ForegroundColor Red
}
finally {
    Write-Host ""
    Write-Host "Application closed." -ForegroundColor Yellow
    
    # Check for recent errors
    $recentLog = Get-ChildItem $debugPath -Filter "*.log" -ErrorAction SilentlyContinue | 
                 Sort-Object LastWriteTime -Descending | 
                 Select-Object -First 1
                 
    if ($recentLog) {
        $errors = Select-String -Path $recentLog.FullName -Pattern "\[Error\]" -SimpleMatch
        if ($errors) {
            Write-Host ""
            Write-Host "Errors found in session:" -ForegroundColor Red
            $errors | Select-Object -First 5 | ForEach-Object { 
                Write-Host "  $($_.Line)" -ForegroundColor Gray 
            }
        }
    }
}