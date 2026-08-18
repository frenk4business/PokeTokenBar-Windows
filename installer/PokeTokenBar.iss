; PokeTokenBar for Windows Inno Setup script
#define MyAppName "PokeTokenBar"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "PokeTokenBar Windows contributors"
#define MyAppExeName "PokeTokenBar.exe"

[Setup]
AppId={{3FAE5C98-536D-49D8-B2FA-9B02FDF7DF82}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\PokeTokenBar
DefaultGroupName=PokeTokenBar
DisableProgramGroupPage=yes
OutputDir=..\artifacts\v{#MyAppVersion}\installer
OutputBaseFilename=PokeTokenBar-Windows-Setup-v{#MyAppVersion}
SetupIconFile=..\src\PokeTokenBar\Resources\PokeTokenBar.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "..\artifacts\v{#MyAppVersion}\portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\PokeTokenBar"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\PokeTokenBar"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[UninstallDelete]
; User save data in AppData/LocalAppData is intentionally preserved.

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "PokeTokenBar"; Flags: deletevalue uninsdeletevalue
