@echo off
:: RunAsAdmin.bat
:: Script to run the Network Adapter Manager with Administrator privileges

echo Starting Network Adapter Manager with Administrator privileges...
powershell -ExecutionPolicy Bypass -Command "Start-Process -FilePath '.\NA-ManagerShortcut\bin\Debug\net9.0-windows\NA-ManagerShortcut.exe' -Verb RunAs"