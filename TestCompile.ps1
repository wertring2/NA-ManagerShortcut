# PowerShell script to test compilation and capture errors
Write-Host "Testing Compilation..." -ForegroundColor Yellow
Write-Host "======================" -ForegroundColor Cyan

# Navigate to project directory
$projectPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectPath

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Gray
Remove-Item -Path "NA-ManagerShortcut\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "NA-ManagerShortcut\obj" -Recurse -Force -ErrorAction SilentlyContinue

# Attempt to build
Write-Host "Building project..." -ForegroundColor Yellow
$buildResult = & dotnet build NA-ManagerShortcut\NA-ManagerShortcut.csproj 2>&1

# Check if dotnet is available
if ($LASTEXITCODE -eq 9009) {
    Write-Host "Dotnet SDK not found. Simulating build errors..." -ForegroundColor Red
    
    # Check for common C# errors
    Write-Host ""
    Write-Host "Checking for compilation issues..." -ForegroundColor Yellow
    
    $files = @(
        "NA-ManagerShortcut\MainWindow.xaml.cs",
        "NA-ManagerShortcut\Views\DebugWindow.xaml.cs",
        "NA-ManagerShortcut\Services\NetworkAdapterServiceFixed.cs",
        "NA-ManagerShortcut\ViewModels\MainViewModel.cs"
    )
    
    $errors = @()
    
    foreach ($file in $files) {
        if (Test-Path $file) {
            $content = Get-Content $file -Raw
            
            # Check for missing using statements
            if ($content -match "Task\." -and $content -notmatch "using System\.Threading\.Tasks") {
                $errors += "${file}: Missing 'using System.Threading.Tasks'"
            }
            
            if ($content -match "Dictionary<" -and $content -notmatch "using System\.Collections\.Generic") {
                $errors += "${file}: Missing 'using System.Collections.Generic'"
            }
            
            # Check for undefined types
            if ($content -match "NetworkAdapterService\s" -and $content -notmatch "NetworkAdapterServiceFixed") {
                $errors += "${file}: Still references old NetworkAdapterService"
            }
        }
    }
    
    if ($errors.Count -gt 0) {
        Write-Host ""
        Write-Host "Found compilation issues:" -ForegroundColor Red
        foreach ($error in $errors) {
            Write-Host "  - $error" -ForegroundColor Yellow
        }
    } else {
        Write-Host "No obvious compilation issues found!" -ForegroundColor Green
    }
} elseif ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "BUILD SUCCEEDED!" -ForegroundColor Green
    Write-Host ""
    
    # Check for warnings
    $warnings = $buildResult | Select-String "warning"
    if ($warnings) {
        Write-Host "Warnings found:" -ForegroundColor Yellow
        $warnings | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    }
} else {
    Write-Host ""
    Write-Host "BUILD FAILED!" -ForegroundColor Red
    Write-Host ""
    
    # Parse and display errors
    $errorLines = $buildResult | Select-String "error"
    if ($errorLines) {
        Write-Host "Errors found:" -ForegroundColor Red
        $errorLines | ForEach-Object { 
            $line = $_.ToString()
            if ($line -match "CS\d{4}") {
                Write-Host "  $line" -ForegroundColor Red
            }
        }
    }
    
    Write-Host ""
    Write-Host "Full output:" -ForegroundColor Gray
    Write-Host $buildResult
}

Write-Host ""
Write-Host "======================" -ForegroundColor Cyan
Write-Host "Compilation test complete" -ForegroundColor Yellow