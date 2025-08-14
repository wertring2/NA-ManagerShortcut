# Download and setup portable NSIS
$nsisVersion = "3.09"
$nsisUrl = "https://sourceforge.net/projects/nsis/files/NSIS%203/$nsisVersion/nsis-$nsisVersion.zip/download"
$nsisZip = "nsis-portable.zip"
$nsisDir = "nsis-portable"

Write-Host "Downloading NSIS Portable v$nsisVersion..." -ForegroundColor Cyan

# Download NSIS
if (!(Test-Path $nsisZip)) {
    try {
        Invoke-WebRequest -Uri $nsisUrl -OutFile $nsisZip -UserAgent "Mozilla/5.0"
        Write-Host "Download completed!" -ForegroundColor Green
    } catch {
        Write-Host "Failed to download from SourceForge. Trying alternative mirror..." -ForegroundColor Yellow
        $altUrl = "https://github.com/kichik/nsis/releases/download/v$nsisVersion/nsis-$nsisVersion.zip"
        try {
            Invoke-WebRequest -Uri $altUrl -OutFile $nsisZip
            Write-Host "Download completed from GitHub!" -ForegroundColor Green
        } catch {
            Write-Host "Error downloading NSIS: $_" -ForegroundColor Red
            exit 1
        }
    }
}

# Extract NSIS
if (!(Test-Path $nsisDir)) {
    Write-Host "Extracting NSIS..." -ForegroundColor Cyan
    Expand-Archive -Path $nsisZip -DestinationPath "." -Force
    # Rename extracted folder
    if (Test-Path "nsis-$nsisVersion") {
        Rename-Item "nsis-$nsisVersion" $nsisDir
    }
    Write-Host "Extraction completed!" -ForegroundColor Green
}

# Build installer
$makensis = "$nsisDir\makensis.exe"
if (Test-Path $makensis) {
    Write-Host "Building installer..." -ForegroundColor Cyan
    & $makensis "installer-simple.nsi"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Installer created successfully!" -ForegroundColor Green
        Write-Host "File: NetworkAdapterManager-Setup-Simple.exe" -ForegroundColor Yellow
    } else {
        Write-Host "Failed to create installer" -ForegroundColor Red
    }
} else {
    Write-Host "makensis.exe not found!" -ForegroundColor Red
}