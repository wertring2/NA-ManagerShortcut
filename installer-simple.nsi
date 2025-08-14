; Simple Network Adapter Manager Installer
; Minimal version without MUI

!define PRODUCT_NAME "Network Adapter Manager"
!define PRODUCT_VERSION "1.0.0"
!define PRODUCT_PUBLISHER "Q WAVE COMPANY LIMITED"

Name "${PRODUCT_NAME}"
OutFile "NetworkAdapterManager-Setup-Simple.exe"
InstallDir "$PROGRAMFILES64\Network Adapter Manager"
RequestExecutionLevel admin
Icon "NA-ManagerShortcut\favicon.ico"

Page directory
Page instfiles

Section
    SetOutPath "$INSTDIR"
    
    ; Copy all files from Release folder
    File /r "NA-ManagerShortcut\bin\Release\net9.0-windows\*.*"
    
    ; Create shortcuts
    CreateDirectory "$SMPROGRAMS\Network Adapter Manager"
    CreateShortCut "$SMPROGRAMS\Network Adapter Manager\Network Adapter Manager.lnk" "$INSTDIR\NA-ManagerShortcut.exe"
    CreateShortCut "$DESKTOP\Network Adapter Manager.lnk" "$INSTDIR\NA-ManagerShortcut.exe"
    
    ; Write uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    
    ; Registry entries for uninstall
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\NetworkAdapterManager" "DisplayName" "${PRODUCT_NAME}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\NetworkAdapterManager" "Publisher" "${PRODUCT_PUBLISHER}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\NetworkAdapterManager" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\NetworkAdapterManager" "UninstallString" "$INSTDIR\Uninstall.exe"
    
    MessageBox MB_OK "Installation completed successfully!"
SectionEnd

Section "Uninstall"
    ; Kill process if running
    ExecWait "taskkill /F /IM NA-ManagerShortcut.exe"
    
    ; Remove files and folders
    RMDir /r "$INSTDIR"
    
    ; Remove shortcuts
    Delete "$DESKTOP\Network Adapter Manager.lnk"
    RMDir /r "$SMPROGRAMS\Network Adapter Manager"
    
    ; Remove registry entries
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\NetworkAdapterManager"
SectionEnd