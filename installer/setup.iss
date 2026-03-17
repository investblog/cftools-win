; CFTools for Windows - InnoSetup Script
; Compile with: ISCC.exe setup.iss

#define MyAppName "CFTools"
#define MyAppVersion "1.1.1"
#define MyAppPublisher "301.st"
#define MyAppURL "https://301.st"
#define MyAppExeName "CFTools.exe"
#define BuildDir "..\src\CFTools\bin\x64\Release\net8.0-windows10.0.19041.0"

[Setup]
AppId={{7B2F4E8A-CF01-4D5B-9A3E-2F1C8D6E9B0A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\CFTools
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\temp
OutputBaseFilename=CFTools-v{#MyAppVersion}-x64-setup
SetupIconFile=..\src\CFTools\Assets\app.ico
UninstallDisplayIcon={app}\Assets\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
PrivilegesRequired=lowest
MinVersion=10.0.19041

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
