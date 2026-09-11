#define AppName "MediaForge"
#define AppVersion "2.0.0"
#define AppPublisher "MediaForge"
#define AppExe "MediaForge.exe"
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif
#ifndef VCRedistPath
  #define VCRedistPath "..\artifacts\vc_redist.x64.exe"
#endif

[Setup]
AppId={{4D2E38D2-84E4-4AF1-9A4D-0D1E4A6F8B91}
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
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
CloseApplications=force
RestartApplications=no
Uninstallable=yes

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#VCRedistPath}"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{autoprograms}\MediaForge"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExe}"
Name: "{autodesktop}\MediaForge"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExe}"

[Run]
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Microsoft Visual C++ Runtime..."; Flags: waituntilterminated runhidden
Filename: "{app}\{#AppExe}"; Description: "Launch MediaForge"; Flags: nowait postinstall skipifsilent
