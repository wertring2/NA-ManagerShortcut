# Download NSIS Portable and create installer
$nsisVersion = "3.10"
$nsisUrl = "https://prdownloads.sourceforge.net/nsis/nsis-$nsisVersion.zip?download"
$nsisZip = "nsis-$nsisVersion.zip"
$nsisDir = "nsis-$nsisVersion"

Write-Host "Downloading NSIS v$nsisVersion..." -ForegroundColor Cyan

# Clean up old files
if (Test-Path "nsis-portable.zip") { Remove-Item "nsis-portable.zip" -Force }
if (Test-Path "nsis-portable") { Remove-Item "nsis-portable" -Recurse -Force }

# Download NSIS using Invoke-WebRequest
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $ProgressPreference = 'SilentlyContinue'
    
    # Direct download URL
    $directUrl = "https://sourceforge.net/projects/nsis/files/NSIS%203/$nsisVersion/nsis-$nsisVersion.zip/download"
    
    Write-Host "Downloading from SourceForge..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $directUrl -OutFile $nsisZip -UserAgent "Mozilla/5.0" -MaximumRedirection 5
    
    Write-Host "Download completed!" -ForegroundColor Green
} catch {
    Write-Host "Error downloading: $_" -ForegroundColor Red
    exit 1
}

# Check if download was successful
if ((Get-Item $nsisZip).Length -lt 1000000) {
    Write-Host "Download appears incomplete. File too small." -ForegroundColor Red
    exit 1
}

# Extract NSIS
Write-Host "Extracting NSIS..." -ForegroundColor Cyan
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory((Get-Location).Path + "\$nsisZip", (Get-Location).Path)
    Write-Host "Extraction completed!" -ForegroundColor Green
} catch {
    Write-Host "Error extracting: $_" -ForegroundColor Red
    exit 1
}

# Verify makensis exists
$makensis = "$nsisDir\makensis.exe"
if (Test-Path $makensis) {
    Write-Host "NSIS ready at: $makensis" -ForegroundColor Green
    Write-Host "Building installer..." -ForegroundColor Cyan
    
    # Run makensis
    & $makensis /V2 "installer-simple.nsi"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "Installer created successfully!" -ForegroundColor Green
        Write-Host "File: NetworkAdapterManager-Setup-Simple.exe" -ForegroundColor Yellow
        Write-Host "========================================" -ForegroundColor Green
    } else {
        Write-Host "Failed to create installer. Error code: $LASTEXITCODE" -ForegroundColor Red
    }
} else {
    Write-Host "makensis.exe not found at expected location!" -ForegroundColor Red
    Write-Host "Looking for it..." -ForegroundColor Yellow
    Get-ChildItem -Path $nsisDir -Filter "makensis.exe" -Recurse
}