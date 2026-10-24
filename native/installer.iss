#ifndef NexusVersion
  #define NexusVersion "1.0.0"
#endif
#ifndef NexusPayload
  #error "Use native/build.ps1 para preparar o pacote do instalador."
#endif
[Setup]
AppId=Nexus.DatabaseStudio
AppName=Nexus
AppVersion={#NexusVersion}
AppVerName=Nexus {#NexusVersion}
VersionInfoVersion={#NexusVersion}.0
DefaultDirName={localappdata}\Programs\Nexus
DefaultGroupName=Nexus
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=Nexus-Setup
SetupIconFile=..\assets\nexus.ico
UninstallDisplayIcon={app}\Nexus.exe
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked
[Files]
Source: "{#NexusPayload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[UninstallDelete]
Type: filesandordirs; Name: "{app}\current"
Type: filesandordirs; Name: "{app}\packages"
[Icons]
Name: "{group}\Nexus"; Filename: "{app}\Nexus.exe"
Name: "{autodesktop}\Nexus"; Filename: "{app}\Nexus.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\Nexus.exe"; Description: "Abrir Nexus"; Flags: nowait postinstall skipifsilent
