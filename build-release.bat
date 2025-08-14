@echo off
echo Building Network Adapter Manager (Release)...
echo.

REM Clean previous builds
echo Cleaning previous builds...
dotnet clean -c Release
echo.

REM Restore packages
echo Restoring NuGet packages...
dotnet restore
echo.

REM Build Release version
echo Building Release version...
dotnet build -c Release

if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b %errorlevel%
)

echo.
echo ========================================
echo Build completed successfully!
echo Output: NA-ManagerShortcut\bin\Release\net9.0-windows\
echo ========================================
echo.
echo Run build-installer.bat to create the setup file
pause