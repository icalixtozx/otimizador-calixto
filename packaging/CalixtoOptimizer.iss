#define ProductVersion "0.5.0-experimental"
#define PublishDirectory SourcePath + "\..\artifacts\release-v0.5.0-experimental\CalixtoOptimizer"
#define InstallerOutputDirectory SourcePath + "\..\artifacts\installer"

[Setup]
AppId={{C4D0DB64-59A0-4C7E-B8B8-342122D076D1}
AppName=Calixto Optimizer
AppVersion={#ProductVersion}
AppVerName=Calixto Optimizer {#ProductVersion}
AppPublisher=Calixto Optimizer
DefaultDirName={localappdata}\Programs\CalixtoOptimizer
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
Uninstallable=yes
UninstallDisplayName=Calixto Optimizer {#ProductVersion} (experimental)
UninstallDisplayIcon={app}\Optimizer.App.exe
OutputDir={#InstallerOutputDirectory}
OutputBaseFilename=CalixtoOptimizer-{#ProductVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
DisableWelcomePage=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
; Instala somente o app publicado e seu runtime. Nenhum worker elevado está incluído.
; Um futuro worker privilegiado não pode ser instalado sob esta pasta modificável pelo usuário.
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Calixto Optimizer"; Filename: "{app}\Optimizer.App.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Calixto Optimizer"; Filename: "{app}\Optimizer.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon
