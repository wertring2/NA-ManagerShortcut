# Download NSIS from GitHub mirror
$nsisVersion = "3.09"
$githubUrl = "https://github.com/kichik/nsis/releases/download/v$nsisVersion/nsis-$nsisVersion.zip"
$nsisZip = "nsis-$nsisVersion.zip"
$nsisDir = "nsis-$nsisVersion"

Write-Host "Downloading NSIS v$nsisVersion from GitHub..." -ForegroundColor Cyan

# Clean up
if (Test-Path $nsisZip) { Remove-Item $nsisZip -Force }
if (Test-Path $nsisDir) { Remove-Item $nsisDir -Recurse -Force }

# Download from GitHub
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $ProgressPreference = 'SilentlyContinue'
    
    Write-Host "Downloading from GitHub releases..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $githubUrl -OutFile $nsisZip
    
    $fileSize = (Get-Item $nsisZip).Length / 1MB
    Write-Host "Downloaded $([math]::Round($fileSize, 2)) MB" -ForegroundColor Green
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    
    # Try alternative download using curl
    Write-Host "Trying with curl..." -ForegroundColor Yellow
    & curl.exe -L -o $nsisZip $githubUrl
}

# Check file
if (!(Test-Path $nsisZip)) {
    Write-Host "Download failed!" -ForegroundColor Red
    exit 1
}

$fileSize = (Get-Item $nsisZip).Length
if ($fileSize -lt 1000000) {
    Write-Host "File too small: $fileSize bytes" -ForegroundColor Red
    exit 1
}

Write-Host "Download successful! Size: $([math]::Round($fileSize/1MB, 2)) MB" -ForegroundColor Green

# Extract
Write-Host "Extracting NSIS..." -ForegroundColor Cyan
try {
    Expand-Archive -Path $nsisZip -DestinationPath "." -Force
    Write-Host "Extraction completed!" -ForegroundColor Green
} catch {
    Write-Host "Extraction error: $_" -ForegroundColor Red
    Write-Host "Trying 7-Zip..." -ForegroundColor Yellow
    
    # Try with 7zip if available
    if (Test-Path "C:\Program Files\7-Zip\7z.exe") {
        & "C:\Program Files\7-Zip\7z.exe" x $nsisZip -y
    } else {
        # Try with tar (Windows 10+)
        & tar -xf $nsisZip
    }
}

# Find and run makensis
$makensis = Get-ChildItem -Path "." -Filter "makensis.exe" -Recurse | Select-Object -First 1
if ($makensis) {
    Write-Host "Found makensis at: $($makensis.FullName)" -ForegroundColor Green
    Write-Host "Building installer..." -ForegroundColor Cyan
    
    & $makensis.FullName /V2 "installer-simple.nsi"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "SUCCESS! Installer created!" -ForegroundColor Green
        Write-Host "File: NetworkAdapterManager-Setup-Simple.exe" -ForegroundColor Yellow
        Write-Host "========================================" -ForegroundColor Green
    } else {
        Write-Host "Build failed with code: $LASTEXITCODE" -ForegroundColor Red
    }
} else {
    Write-Host "makensis.exe not found!" -ForegroundColor Red
}