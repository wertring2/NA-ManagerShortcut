@echo off
echo Creating Simple Self-Extracting Installer...
echo.

set SOURCE_DIR=NA-ManagerShortcut\bin\Release\net9.0-windows
set OUTPUT_FILE=NetworkAdapterManager-Setup.exe
set INSTALL_DIR=%ProgramFiles%\Network Adapter Manager

REM Create a self-extracting archive using built-in Windows tools
echo Creating installation package...

REM Create temporary batch file for installation
echo @echo off > install.bat
echo echo Installing Network Adapter Manager... >> install.bat
echo echo. >> install.bat
echo net session ^>nul 2^>^&1 >> install.bat
echo if %%errorLevel%% neq 0 ( >> install.bat
echo     echo Please run as Administrator! >> install.bat
echo     pause >> install.bat
echo     exit /b 1 >> install.bat
echo ) >> install.bat
echo echo Creating directory... >> install.bat
echo mkdir "%INSTALL_DIR%" 2^>nul >> install.bat
echo echo Copying files... >> install.bat
echo xcopy /E /I /Y "*.exe" "%INSTALL_DIR%" >> install.bat
echo xcopy /E /I /Y "*.dll" "%INSTALL_DIR%" >> install.bat
echo xcopy /E /I /Y "*.json" "%INSTALL_DIR%" >> install.bat
echo echo Creating shortcuts... >> install.bat
echo powershell -Command "$WshShell = New-Object -comObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('%%USERPROFILE%%\Desktop\Network Adapter Manager.lnk'); $Shortcut.TargetPath = '%%INSTALL_DIR%%\NA-ManagerShortcut.exe'; $Shortcut.Save()" >> install.bat
echo echo. >> install.bat
echo echo Installation completed! >> install.bat
echo echo Starting Network Adapter Manager... >> install.bat
echo start "" "%INSTALL_DIR%\NA-ManagerShortcut.exe" >> install.bat
echo pause >> install.bat

REM Create ZIP file with installation files
echo Creating package...
powershell -Command "Compress-Archive -Path '%SOURCE_DIR%\*', 'install.bat' -DestinationPath 'setup-package.zip' -Force"

REM Create self-extracting EXE
echo Creating self-extracting installer...
echo This will create a basic installer package.
echo.

if exist setup-package.zip (
    echo Package created: setup-package.zip
    echo.
    echo To complete installation:
    echo 1. Extract setup-package.zip
    echo 2. Run install.bat as Administrator
    echo.
    del install.bat
) else (
    echo Failed to create package!
)

pause