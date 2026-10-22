; Script de instalador para Nova Player
; Generado con Inno Setup 6

#define MyAppName "Nova Player"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Chris Escalantech"
#define MyAppURL "https://github.com/chrisantropo/NovaPlayer"
#define MyAppExeName "NovaPlayer.exe"
#define MyAppAppUserModelID "NovaPlayer.App"

[Setup]
AppId={{97C23A4E-7C21-4E65-B6FD-89F19E98D3B7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\NovaPlayer
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=InstallerOutput
OutputBaseFilename=NovaPlayer-Setup
SetupIconFile=NovaPlayer.ico
UninstallDisplayIcon={app}\NovaPlayer.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ChangesAssociations=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "contextmenu"; Description: "Añadir 'Abrir con Nova Player' al menú contextual de archivos de vídeo"; GroupDescription: "Integración con Windows:"
Name: "fileassoc"; Description: "Registrar Nova Player como reproductor multimedia admitido en Windows (Default Apps)"; GroupDescription: "Integración con Windows:"

[Files]
Source: "bin\Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "NovaPlayer.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "NovaPlayer.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\NovaPlayer.ico"; AppUserModelID: "{#MyAppAppUserModelID}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\NovaPlayer.ico"; AppUserModelID: "{#MyAppAppUserModelID}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
; App Paths (permite ejecutar "NovaPlayer" desde Win+R / Ejecutar y consola)
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}"; ValueType: string; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}"; ValueType: string; ValueName: "Path"; ValueData: "{app}"; Flags: uninsdeletekey

; Menú contextual: Clic derecho en cualquier archivo de vídeo
Root: HKCR; Subkey: "SystemFileAssociations\video\shell\NovaPlayer"; ValueType: string; ValueData: "Abrir con Nova Player"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\video\shell\NovaPlayer"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\NovaPlayer.ico"""; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\video\shell\NovaPlayer\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: contextmenu

; Menú contextual: Clic derecho en cualquier archivo de audio
Root: HKCR; Subkey: "SystemFileAssociations\audio\shell\NovaPlayer"; ValueType: string; ValueData: "Abrir con Nova Player"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\audio\shell\NovaPlayer"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\NovaPlayer.ico"""; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\audio\shell\NovaPlayer\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: contextmenu

; Menú contextual: Clic derecho en carpetas (reproducir carpeta)
Root: HKCR; Subkey: "Directory\shell\NovaPlayer"; ValueType: string; ValueData: "Reproducir con Nova Player"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\NovaPlayer"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\NovaPlayer.ico"""; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\NovaPlayer\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: contextmenu

; Registro de capacidades de la aplicación (Default Programs / Aplicaciones predeterminadas de Windows 10/11)
Root: HKLM; Subkey: "SOFTWARE\RegisteredApplications"; ValueType: string; ValueName: "NovaPlayer"; ValueData: "SOFTWARE\NovaPlayer\Capabilities"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Reproductor multimedia de escritorio de alto rendimiento para Windows"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: "{app}\NovaPlayer.ico,0"; Flags: uninsdeletekey; Tasks: fileassoc

; ProgID base
Root: HKCR; Subkey: "NovaPlayer.MediaFile"; ValueType: string; ValueData: "Archivo multimedia de Nova Player"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: "NovaPlayer.MediaFile\DefaultIcon"; ValueType: string; ValueData: "{app}\NovaPlayer.ico,0"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: "NovaPlayer.MediaFile\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: fileassoc

; Asociaciones de extensiones de vídeo en Capabilities
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mkv"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mp4"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".avi"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".webm"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mov"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wmv"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".flv"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ts"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m2ts"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".vob"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".3gp"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ogv"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mp3"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".flac"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wav"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aac"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKLM; Subkey: "SOFTWARE\NovaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m4a"; ValueData: "NovaPlayer.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc

; OpenWithProgids para que aparezca en "Abrir con..." de Windows
Root: HKCR; Subkey: ".mkv\OpenWithProgids"; ValueType: string; ValueName: "NovaPlayer.MediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".mp4\OpenWithProgids"; ValueType: string; ValueName: "NovaPlayer.MediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".avi\OpenWithProgids"; ValueType: string; ValueName: "NovaPlayer.MediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".webm\OpenWithProgids"; ValueType: string; ValueName: "NovaPlayer.MediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".mov\OpenWithProgids"; ValueType: string; ValueName: "NovaPlayer.MediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".wmv\OpenWithProgids"; ValueType: string; ValueName: "NovaPlayer.MediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".flv\OpenWithProgids"; ValueType: string; ValueName: "NovaPlayer.MediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".ts\OpenWithProgids"; ValueType: string; ValueName: "NovaPlayer.MediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".m2ts\OpenWithProgids"; ValueType: string; ValueName: "NovaPlayer.MediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".vob\OpenWithProgids"; ValueType: string; ValueName: "NovaPlayer.MediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".3gp\OpenWithProgids"; ValueType: string; ValueName: "NovaPlayer.MediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".ogv\OpenWithProgids"; ValueType: string; ValueName: "NovaPlayer.MediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc

; OpenWithList
Root: HKCR; Subkey: ".mkv\OpenWithList\{#MyAppExeName}"; ValueType: string; ValueData: ""; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: ".mp4\OpenWithList\{#MyAppExeName}"; ValueType: string; ValueData: ""; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: ".avi\OpenWithList\{#MyAppExeName}"; ValueType: string; ValueData: ""; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: ".webm\OpenWithList\{#MyAppExeName}"; ValueType: string; ValueData: ""; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: ".mov\OpenWithList\{#MyAppExeName}"; ValueType: string; ValueData: ""; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: ".wmv\OpenWithList\{#MyAppExeName}"; ValueType: string; ValueData: ""; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: ".flv\OpenWithList\{#MyAppExeName}"; ValueType: string; ValueData: ""; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: ".ts\OpenWithList\{#MyAppExeName}"; ValueType: string; ValueData: ""; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: ".m2ts\OpenWithList\{#MyAppExeName}"; ValueType: string; ValueData: ""; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: ".vob\OpenWithList\{#MyAppExeName}"; ValueType: string; ValueData: ""; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: ".3gp\OpenWithList\{#MyAppExeName}"; ValueType: string; ValueData: ""; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: ".ogv\OpenWithList\{#MyAppExeName}"; ValueType: string; ValueData: ""; Flags: uninsdeletekey; Tasks: fileassoc

[Code]
// Notificar al shell de Windows que las asociaciones de archivo han cambiado
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, 0, 0)
    // Inno Setup maneja automáticamente ChangesAssociations=yes
  end;
end;
