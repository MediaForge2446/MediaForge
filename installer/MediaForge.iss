#define AppName "MediaForge"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "MediaForge"
#define AppExeName "MediaForge.exe"

#ifndef PublishDir
  #error "PublishDir must be supplied to ISCC."
#endif

#ifndef Architecture
  #error "Architecture must be supplied to ISCC."
#endif

#ifndef VCRedistPath
  #error "VCRedistPath must be supplied to ISCC."
#endif

[Setup]
AppId={{D6D01C3B-9BB4-4DD6-8F8F-9A5A3D4E7E52}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\MediaForge
DefaultGroupName=MediaForge
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode={#Architecture}
ArchitecturesAllowed={#Architecture}
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#AppExeName}

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#VCRedistPath}"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{autoprograms}\MediaForge"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"
Name: "{autodesktop}\MediaForge"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Microsoft Visual C++ Runtime..."; Flags: waituntilterminated runhidden
Filename: "{app}\{#AppExeName}"; Description: "Launch MediaForge"; Flags: nowait postinstall skipifsilent
