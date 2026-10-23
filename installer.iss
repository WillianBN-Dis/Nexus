[Setup]
AppId=Nexus.DatabaseStudio
AppName=Nexus
AppVersion=0.1.0
DefaultDirName={localappdata}\Programs\Nexus
DefaultGroupName=Nexus
PrivilegesRequired=lowest
OutputDir=dist
OutputBaseFilename=Nexus-Setup-0.1.0
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\Nexus.exe
[Files]
Source: "dist\Nexus\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Nexus"; Filename: "{app}\Nexus.exe"
Name: "{autodesktop}\Nexus"; Filename: "{app}\Nexus.exe"
[Run]
Filename: "{app}\Nexus.exe"; Description: "Abrir Nexus"; Flags: nowait postinstall skipifsilent
