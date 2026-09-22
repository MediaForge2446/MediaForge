; MediaForge standard Windows installer for WinGet/GitHub Releases.
#define MyAppName "MediaForge"
#define MyAppPublisher "MediaForge"
#define MyAppURL "https://github.com/MediaForge2446/MediaForge"
#define MyAppExeName "MediaForge.exe"

#ifndef MyAppVersion
  #define MyAppVersion "0.2.0"
#endif

[Setup]
AppId={{B4BDA0DA-5FE3-4A28-9B53-3E2CF8A6A9E7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\MediaForge
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern windows11 dynamic includetitlebar
WizardKeepAspectRatio=yes
WizardSizePercent=100
WizardImageFile=assets\WizardImage.png
WizardSmallImageFile=assets\MediaForge.png
SetupIconFile=assets\MediaForge.ico
DefaultDialogFontName=Segoe UI
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductVersion={#MyAppVersion}
VersionInfoDescription=MediaForge setup
VersionInfoProductName=MediaForge
VersionInfoCompany=MediaForge
VersionInfoTextVersion={#MyAppVersion}
VersionInfoCopyright=Copyright 2026 MediaForge
AllowNoIcons=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "hebrew"; MessagesFile: "compiler:Languages\Hebrew.isl"

[Messages]
english.WelcomeLabel1=Welcome to MediaForge Setup
english.WelcomeLabel2=Install MediaForge to download, convert and organize your media in one clean workspace.%n%nThe installation is designed to be quick, lightweight and easy to understand.
hebrew.WelcomeLabel1=ברוכים הבאים להתקנת MediaForge
hebrew.WelcomeLabel2=התקינו את MediaForge כדי להוריד, להמיר ולנהל את המדיה שלכם במקום אחד.%n%nההתקנה פשוטה, מהירה וברורה.

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion createallsubdirs

[Icons]
Name: "{group}\MediaForge"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\MediaForge"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch MediaForge"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\MediaForge"
