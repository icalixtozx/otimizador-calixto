#define ProductVersion "0.6.0-experimental-preview-20261019"
#define ProductName "Calixto Optimizer Preview 20261019"
#define PublishDirectory SourcePath + "\..\artifacts\preview-v0.6.0-experimental-selfcontained-20261019\CalixtoOptimizer"
#define InstallerOutputDirectory SourcePath + "\..\artifacts\installer-preview-20261019"

[Setup]
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
UninstallDisplayName={#ProductName} ({#ProductVersion}, experimental)
UninstallDisplayIcon={app}\Optimizer.App.exe
OutputDir={#InstallerOutputDirectory}
OutputBaseFilename=CalixtoOptimizer-0.6.0-experimental-preview-20261019-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
DisableWelcomePage=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
; Esta publicação autocontida já inclui os runtimes .NET 10 necessários ao WPF.
; O instalador é isolado da identidade e do diretório dos pacotes anteriores.
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Calixto Optimizer Preview 20261019"; Filename: "{app}\Optimizer.App.exe"; WorkingDir: "{app}"
