[Setup]
AppId=Nexus.DatabaseStudio
AppName=Nexus
AppVersion=0.4.0
DefaultDirName={localappdata}\Programs\Nexus
DefaultGroupName=Nexus
PrivilegesRequired=lowest
OutputDir=dist
OutputBaseFilename=Nexus-Setup-0.4.0
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\Nexus.exe
SetupIconFile=assets\nexus.ico
WizardStyle=modern
[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked
[Files]
Source: "dist\Nexus\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Nexus"; Filename: "{app}\Nexus.exe"
Name: "{autodesktop}\Nexus"; Filename: "{app}\Nexus.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\Nexus.exe"; Description: "Abrir Nexus"; Flags: nowait postinstall skipifsilent
