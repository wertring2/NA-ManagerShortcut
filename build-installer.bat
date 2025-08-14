@echo off
echo Building Network Adapter Manager Installer...
echo.

REM Build the application in Release mode
echo Step 1: Building Release version...
dotnet build -c Release
if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b %errorlevel%
)

echo.
echo Step 2: Creating installer with NSIS...
REM Check if NSIS is installed
if exist "%ProgramFiles(x86)%\NSIS\makensis.exe" (
    "%ProgramFiles(x86)%\NSIS\makensis.exe" installer.nsi
) else if exist "%ProgramFiles%\NSIS\makensis.exe" (
    "%ProgramFiles%\NSIS\makensis.exe" installer.nsi
) else (
    echo NSIS not found! Please install NSIS from https://nsis.sourceforge.io/
    echo Or add makensis.exe to your PATH
    pause
    exit /b 1
)

if %errorlevel% neq 0 (
    echo Installer creation failed!
    pause
    exit /b %errorlevel%
)

echo.
echo ========================================
echo Installer created successfully!
echo File: NetworkAdapterManager-Setup-1.0.0.exe
echo ========================================
pause