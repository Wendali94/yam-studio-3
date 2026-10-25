; Inno Setup script — builds Setup.exe from the published (self-contained) app
#define AppName "YAM Studio Manager"
#define AppExe "YamStudio.exe"

[Setup]
AppId={{6B1F2C9E-3A4D-4E57-9C61-5B7A0D2E8F14}
AppName={#AppName}
AppVersion=1.0.0
AppPublisher=YAM PICTURE
DefaultDirName={autopf}\YamStudio
DefaultGroupName=YAM Studio
OutputDir=..\artifacts
OutputBaseFilename=YamStudio-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\YAM Studio"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\YAM Studio"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
; Allow the camera (FTP) and PC2 (LAN) to reach this program through Windows Firewall
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""YAM Studio"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""YAM Studio"" dir=in action=allow program=""{app}\{#AppExe}"" enable=yes profile=any"; Flags: runhidden
Filename: "{app}\{#AppExe}"; Description: "Start YAM Studio"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""YAM Studio"""; Flags: runhidden
