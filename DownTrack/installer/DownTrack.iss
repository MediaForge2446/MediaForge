#define AppName "DownTrack"
#define AppVersion "0.3.0-alpha.1"
#define AppPublisher "MediaForge2446"
#define AppExeName "DownTrack.App.exe"

[Setup]
AppId={{D43A4D0F-79D0-4E4B-A687-B5B3BB05A8D4}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\DownTrack
DefaultGroupName={#AppName}
OutputDir=..\artifacts\installer
OutputBaseFilename=Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
