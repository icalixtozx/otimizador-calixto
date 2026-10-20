#define ProductVersion "0.6.0-experimental-preview-20261020-r3"
#define ProductName "Calixto Optimizer Preview 20261020 R3"
#define PublishDirectory SourcePath + "\..\artifacts\preview-v0.6.0-experimental-selfcontained-20261020-r3\CalixtoOptimizer-Preview-20261020-R3"
#define InstallerOutputDirectory SourcePath + "\..\artifacts\installer-preview-20261020-r3"

[Setup]
; R3 has a distinct AppId and install directory; it cannot silently upgrade or overwrite R2.
AppId={{6E91C771-05A2-4B08-9AF0-1BB0A1989D05}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppVerName={#ProductName} ({#ProductVersion})
AppPublisher=Calixto Optimizer
DefaultDirName={localappdata}\Programs\CalixtoOptimizer-Preview-20261020-R3
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Uninstallable=yes
UninstallDisplayName={#ProductName} ({#ProductVersion}, experimental)
UninstallDisplayIcon={app}\Optimizer.App.exe
OutputDir={#InstallerOutputDirectory}
OutputBaseFilename=CalixtoOptimizer-Preview-0.6.0-experimental-20261020-r3-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no
DisableWelcomePage=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
; The installer consumes the same self-contained publish that is placed in the portable ZIP.
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Calixto Optimizer Preview 20261020 R3"; Filename: "{app}\Optimizer.App.exe"; WorkingDir: "{app}"
