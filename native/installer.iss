[Setup]
AppId=Nexus.DatabaseStudio
AppName=Nexus
AppVersion=0.6.0
AppVerName=Nexus 0.6.0 — WPF.2
VersionInfoVersion=0.6.0.2
DefaultDirName={localappdata}\Programs\Nexus
DefaultGroupName=Nexus
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=Nexus-Setup-0.6.0-wpf.2
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
Source: "artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Nexus"; Filename: "{app}\Nexus.exe"
Name: "{autodesktop}\Nexus"; Filename: "{app}\Nexus.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\Nexus.exe"; Description: "Abrir Nexus"; Flags: nowait postinstall skipifsilent
