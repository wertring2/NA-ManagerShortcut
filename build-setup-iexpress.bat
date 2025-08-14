@echo off
echo Creating Network Adapter Manager Setup with IExpress...
echo.

REM IExpress is built into Windows
iexpress /N setup-iexpress.sed

if %errorlevel% neq 0 (
    echo Failed to create installer with IExpress
    pause
    exit /b %errorlevel%
)

echo.
echo ========================================
echo Setup created: NetworkAdapterManager-Setup-IExpress.exe
echo ========================================
pause