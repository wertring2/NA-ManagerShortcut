; Network Adapter Manager NSIS Installer Script
; Copyright (c) Q WAVE COMPANY LIMITED

!define PRODUCT_NAME "Network Adapter Manager"
!define PRODUCT_VERSION "1.0.0"
!define PRODUCT_PUBLISHER "Q WAVE COMPANY LIMITED"
!define PRODUCT_WEB_SITE "https://qwave.co.th"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\NA-ManagerShortcut.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"

; MUI 2.0 compatible
!include "MUI2.nsh"
!include "x64.nsh"

; MUI Settings
!define MUI_ABORTWARNING
!define MUI_ICON "NA-ManagerShortcut\favicon.ico"
!define MUI_UNICON "NA-ManagerShortcut\favicon.ico"

; Welcome page
!insertmacro MUI_PAGE_WELCOME
; License page
!insertmacro MUI_PAGE_LICENSE "LICENSE.txt"
; Directory page
!insertmacro MUI_PAGE_DIRECTORY
; Instfiles page
!insertmacro MUI_PAGE_INSTFILES
; Finish page
!define MUI_FINISHPAGE_RUN "$INSTDIR\NA-ManagerShortcut.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch Network Adapter Manager"
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED
!insertmacro MUI_PAGE_FINISH

; Uninstaller pages
!insertmacro MUI_UNPAGE_INSTFILES

; Language files
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Thai"

; MUI end

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "NetworkAdapterManager-Setup-${PRODUCT_VERSION}.exe"
InstallDir "$PROGRAMFILES64\Network Adapter Manager"
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show
RequestExecutionLevel admin

Section "MainSection" SEC01
    SetOutPath "$INSTDIR"
    SetOverwrite ifnewer
    
    ; Main application files
    File "NA-ManagerShortcut\bin\Release\net9.0-windows\NA-ManagerShortcut.exe"
    File "NA-ManagerShortcut\bin\Release\net9.0-windows\NA-ManagerShortcut.dll"
    File "NA-ManagerShortcut\bin\Release\net9.0-windows\NA-ManagerShortcut.runtimeconfig.json"
    File "NA-ManagerShortcut\bin\Release\net9.0-windows\NA-ManagerShortcut.deps.json"
    
    ; Dependencies
    File "NA-ManagerShortcut\bin\Release\net9.0-windows\Hardcodet.NotifyIcon.Wpf.dll"
    File "NA-ManagerShortcut\bin\Release\net9.0-windows\Newtonsoft.Json.dll"
    File "NA-ManagerShortcut\bin\Release\net9.0-windows\System.Management.dll"
    
    ; Include all other DLL dependencies
    File "NA-ManagerShortcut\bin\Release\net9.0-windows\*.dll"
    
    ; Create directories for profiles and logs
    CreateDirectory "$INSTDIR\Profiles"
    CreateDirectory "$INSTDIR\Logs"
    CreateDirectory "$INSTDIR\debug_logs"
    CreateDirectory "$INSTDIR\claude_output"
SectionEnd

Section -AdditionalIcons
    CreateDirectory "$SMPROGRAMS\Network Adapter Manager"
    CreateShortCut "$SMPROGRAMS\Network Adapter Manager\Network Adapter Manager.lnk" "$INSTDIR\NA-ManagerShortcut.exe"
    CreateShortCut "$SMPROGRAMS\Network Adapter Manager\Uninstall.lnk" "$INSTDIR\uninst.exe"
    CreateShortCut "$DESKTOP\Network Adapter Manager.lnk" "$INSTDIR\NA-ManagerShortcut.exe"
SectionEnd

Section -Post
    WriteUninstaller "$INSTDIR\uninst.exe"
    WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\NA-ManagerShortcut.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninst.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\NA-ManagerShortcut.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
    
    ; Add to Windows startup (optional - commented out by default)
    ; WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "NetworkAdapterManager" "$INSTDIR\NA-ManagerShortcut.exe"
SectionEnd

Function un.onUninstSuccess
    HideWindow
    MessageBox MB_ICONINFORMATION|MB_OK "$(^Name) was successfully removed from your computer."
FunctionEnd

Function un.onInit
    MessageBox MB_ICONQUESTION|MB_YESNO|MB_DEFBUTTON2 "Are you sure you want to completely remove $(^Name) and all of its components?" IDYES +2
    Abort
FunctionEnd

Section Uninstall
    ; Kill running process if exists
    ExecWait "taskkill /F /IM NA-ManagerShortcut.exe"
    
    ; Remove files
    Delete "$INSTDIR\*.*"
    
    ; Remove directories
    RMDir /r "$INSTDIR\Profiles"
    RMDir /r "$INSTDIR\Logs"
    RMDir /r "$INSTDIR\debug_logs"
    RMDir /r "$INSTDIR\claude_output"
    RMDir "$INSTDIR"
    
    ; Remove shortcuts
    Delete "$SMPROGRAMS\Network Adapter Manager\Uninstall.lnk"
    Delete "$SMPROGRAMS\Network Adapter Manager\Network Adapter Manager.lnk"
    Delete "$DESKTOP\Network Adapter Manager.lnk"
    RMDir "$SMPROGRAMS\Network Adapter Manager"
    
    ; Remove registry entries
    DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
    DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "NetworkAdapterManager"
    
    SetAutoClose true
SectionEnd