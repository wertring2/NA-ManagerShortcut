[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=0
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=%PostInstallCmd%
AdminQuietInstCmd=%AdminQuietInstCmd%
UserQuietInstCmd=%UserQuietInstCmd%
SourceFiles=SourceFiles
[Strings]
InstallPrompt=Install Network Adapter Manager?
DisplayLicense=
FinishMessage=Network Adapter Manager has been installed successfully!
TargetName=NetworkAdapterManager-Setup-IExpress.exe
FriendlyName=Network Adapter Manager Setup
AppLaunched=cmd /c xcopy /E /I /Y "%TEMP%\IXP000.TMP" "%ProgramFiles%\Network Adapter Manager"
PostInstallCmd=cmd /c "%ProgramFiles%\Network Adapter Manager\NA-ManagerShortcut.exe"
AdminQuietInstCmd=
UserQuietInstCmd=
FILE0="NA-ManagerShortcut.exe"
FILE1="NA-ManagerShortcut.dll"
FILE2="NA-ManagerShortcut.runtimeconfig.json"
FILE3="NA-ManagerShortcut.deps.json"
FILE4="Hardcodet.NotifyIcon.Wpf.dll"
FILE5="Newtonsoft.Json.dll"
FILE6="System.Management.dll"
[SourceFiles]
SourceFiles0=NA-ManagerShortcut\bin\Release\net9.0-windows\
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
%FILE3%=
%FILE4%=
%FILE5%=
%FILE6%=