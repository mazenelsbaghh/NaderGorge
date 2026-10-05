#define MyAppName "Massar Exam Room"
#define MyAppVersion "0.1.0"

[Setup]
AppId={{F3D9B4A4-8717-4D87-A038-451986E08FC2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Massar
DefaultDirName={autopf}\Massar Exam Room
DefaultGroupName=Massar Exam Room
OutputDir=..\dist
OutputBaseFilename=Massar-Exam-Room-Windows-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\Massar Exam Room.exe
DisableProgramGroupPage=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\dist\Massar Exam Room\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\Massar Exam Room"; Filename: "{app}\Massar Exam Room.exe"
Name: "{autodesktop}\Massar Exam Room"; Filename: "{app}\Massar Exam Room.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Massar Exam Room.exe"; Description: "Open Massar Exam Room"; Flags: nowait postinstall skipifsilent
