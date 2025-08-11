# PowerShell script to run Network Adapter Manager in debug mode with Administrator privileges

Write-Host "Starting Network Adapter Manager in Debug Mode..." -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")

if (-not $isAdmin) {
    Write-Host "Requesting Administrator privileges..." -ForegroundColor Yellow
    
    # Restart script with admin privileges
    $scriptPath = $MyInvocation.MyCommand.Path
    Start-Process PowerShell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`""
    exit
}

Write-Host "Running with Administrator privileges ✓" -ForegroundColor Green

# Navigate to project directory
$projectPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectPath

# Create debug output directory
$debugPath = Join-Path $projectPath "debug_output"
if (-not (Test-Path $debugPath)) {
    New-Item -ItemType Directory -Path $debugPath | Out-Null
    Write-Host "Created debug output directory: $debugPath" -ForegroundColor Gray
}

# Create timestamp for this session
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$sessionLog = Join-Path $debugPath "session_$timestamp.log"

Write-Host "Debug session log: $sessionLog" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
$buildOutput = & dotnet build -c Debug 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Write-Host $buildOutput
    Write-Host ""
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host "Build successful ✓" -ForegroundColor Green
Write-Host ""

# Path to executable
$exePath = Join-Path $projectPath "NA-ManagerShortcut\bin\Debug\net9.0-windows\NA-ManagerShortcut.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "Executable not found at: $exePath" -ForegroundColor Red
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host "Starting application with debug monitoring..." -ForegroundColor Yellow
Write-Host "Press Ctrl+Shift+D in the application to open Debug Window" -ForegroundColor Cyan
Write-Host "Press Ctrl+C here to stop monitoring" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Function to monitor debug output
function Monitor-DebugOutput {
    $claudeOutput = Join-Path $projectPath "claude_output"
    $debugLogs = Join-Path $projectPath "debug_logs"
    
    # Monitor claude_output directory
    if (Test-Path $claudeOutput) {
        $watcher = New-Object System.IO.FileSystemWatcher
        $watcher.Path = $claudeOutput
        $watcher.Filter = "*.json"
        $watcher.NotifyFilter = [System.IO.NotifyFilters]::LastWrite
        $watcher.EnableRaisingEvents = $true
        
        Register-ObjectEvent -InputObject $watcher -EventName "Changed" -Action {
            $path = $Event.SourceEventArgs.FullPath
            $content = Get-Content $path -Raw | ConvertFrom-Json
            Write-Host "[DEBUG EVENT]" -ForegroundColor Yellow -NoNewline
            Write-Host " $($content.Message)" -ForegroundColor White
            
            if ($content.Type -eq "Error") {
                Write-Host "  ERROR DETAILS:" -ForegroundColor Red
                Write-Host "  $($content | ConvertTo-Json -Compress)" -ForegroundColor Gray
            }
        } | Out-Null
    }
}

# Start monitoring
Monitor-DebugOutput

# Run the application
try {
    & $exePath 2>&1 | Tee-Object -FilePath $sessionLog
}
catch {
    Write-Host "Application error: $_" -ForegroundColor Red
}
finally {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Debug session ended" -ForegroundColor Yellow
    Write-Host "Session log saved to: $sessionLog" -ForegroundColor Gray
    
    # Check for errors in debug logs
    $debugLogPath = Join-Path $projectPath "debug_logs"
    if (Test-Path $debugLogPath) {
        $latestLog = Get-ChildItem $debugLogPath -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($latestLog) {
            $errors = Select-String -Path $latestLog.FullName -Pattern "\[Error\]" -SimpleMatch
            if ($errors) {
                Write-Host ""
                Write-Host "Found $($errors.Count) errors in debug log:" -ForegroundColor Red
                $errors | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
            }
        }
    }
    
    Write-Host ""
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}