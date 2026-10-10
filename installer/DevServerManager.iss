#ifndef AppVersion
  #error AppVersion must be supplied by scripts/package.ps1
#endif
#ifndef PublishDir
  #error PublishDir must be supplied by scripts/package.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by scripts/package.ps1
#endif

[Setup]
AppId={{4E370EB0-4AE4-41AC-818D-67FF3298AD8A}
AppName=Dev Server Manager
AppVersion={#AppVersion}
AppPublisher=ntoussaintdev
AppPublisherURL=https://github.com/ntoussaintdev/node-server-manager
DefaultDirName={localappdata}\Programs\DevServerManager
DefaultGroupName=Dev Server Manager
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=DevServerManager-{#AppVersion}-Setup-x64
SetupIconFile=..\DevServerManager\Assets\devflow-node.ico
UninstallDisplayIcon={app}\DevServerManager.exe
LicenseFile=..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
AppMutex=DevServerManager.4E370EB0-4AE4-41AC-818D-67FF3298AD8A
CloseApplications=no
RestartApplications=no
SetupLogging=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "portable.flag,*.db,*.db-*,*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Dev Server Manager"; Filename: "{app}\DevServerManager.exe"
Name: "{autodesktop}\Dev Server Manager"; Filename: "{app}\DevServerManager.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DevServerManager.exe"; Description: "Launch Dev Server Manager"; Flags: nowait postinstall skipifsilent

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if FileExists(ExpandConstant('{app}\portable.flag')) then
    Result := 'This folder contains a portable copy. Choose a different installation folder to preserve its configuration.';
end;
