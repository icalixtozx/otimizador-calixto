#define ProductVersion "0.6.0-experimental-preview-20261020-r4"
#define ProductName "Calixto Optimizer Preview 20261020 R4"
#define PublishDirectory SourcePath + "\..\artifacts\preview-v0.6.0-experimental-selfcontained-20261020-r4\CalixtoOptimizer-Preview-20261020-R4"
#define InstallerOutputDirectory SourcePath + "\..\artifacts\installer-preview-20261020-r4"

[Setup]
; R4 uses its own identity and install directory; it does not upgrade or overwrite R2/R3.
AppId={{BAFE80BE-766C-42B9-B132-9822A9F15C12}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppVerName={#ProductName} ({#ProductVersion})
AppPublisher=Calixto Optimizer
DefaultDirName={localappdata}\Programs\CalixtoOptimizer-Preview-20261020-R4
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Uninstallable=yes
UninstallDisplayName={#ProductName} ({#ProductVersion}, experimental)
UninstallDisplayIcon={app}\Optimizer.App.exe
OutputDir={#InstallerOutputDirectory}
OutputBaseFilename=CalixtoOptimizer-Preview-0.6.0-experimental-20261020-r4-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no
DisableWelcomePage=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
; Installer and portable ZIP use the same self-contained publish directory.
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Calixto Optimizer Preview 20261020 R4"; Filename: "{app}\Optimizer.App.exe"; WorkingDir: "{app}"
