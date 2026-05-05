#define MyAppName "Markdown Viewer"
#define MyAppVersion "1.0"
#define MyAppExeName "MarkdownViewer.exe"
#define AppSrcDir "..\dist\MarkdownViewer"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Markdown Viewer
DefaultDirName={autopf}\Markdown Viewer
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\dist
OutputBaseFilename=MarkdownViewerSetup
SetupIconFile={#SourcePath}\..\appicon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "fileassoc";   Description: "Associate .md files with Markdown Viewer"; GroupDescription: "File Associations:";
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}";                    GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#AppSrcDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}";    Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCR; Subkey: ".md";                                             ValueType: string; ValueName: ""; ValueData: "MarkdownViewer.MarkdownFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: "MarkdownViewer.MarkdownFile";                     ValueType: string; ValueName: ""; ValueData: "Markdown File";               Flags: uninsdeletekey;  Tasks: fileassoc
Root: HKCR; Subkey: "MarkdownViewer.MarkdownFile\DefaultIcon";         ValueType: string; ValueName: ""; ValueData: "{app}\icon.ico";                                    Tasks: fileassoc
Root: HKCR; Subkey: "MarkdownViewer.MarkdownFile\shell\open\command";  ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1""";                   Tasks: fileassoc

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
