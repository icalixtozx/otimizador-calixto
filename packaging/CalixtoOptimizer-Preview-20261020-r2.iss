#define ProductVersion "0.6.0-experimental-preview-20261020-r2"
#define ProductName "Calixto Optimizer Preview 20261020 R2"
#define PublishDirectory SourcePath + "\..\artifacts\preview-v0.6.0-experimental-selfcontained-20261020-r2\CalixtoOptimizer-Preview-20261020"
#define InstallerOutputDirectory SourcePath + "\..\artifacts\installer-preview-20261020-r2"

[Setup]
; Preserve the established AppId and per-user installation path to perform a real upgrade.
AppId={{8201DBD7-6BB8-479A-AFB8-210057FEDCA0}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppVerName={#ProductName} ({#ProductVersion})
AppPublisher=Calixto Optimizer
DefaultDirName={localappdata}\Programs\CalixtoOptimizer-Preview-20261019
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Uninstallable=yes
UninstallDisplayName={#ProductName} ({#ProductVersion})
UninstallDisplayIcon={app}\Optimizer.App.exe
OutputDir={#InstallerOutputDirectory}
OutputBaseFilename=CalixtoOptimizer-Preview-0.6.0-experimental-20261020-r2-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no
DisableWelcomePage=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
; The installer consumes the exact self-contained publish used by the portable ZIP.
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Remove only the prior shortcut from the Preview being upgraded.
Type: files; Name: "{autoprograms}\Calixto Optimizer Preview 20261019.lnk"

[Icons]
Name: "{autoprograms}\Calixto Optimizer Preview 20261020"; Filename: "{app}\Optimizer.App.exe"; WorkingDir: "{app}"
