; ==============================================================================
; SCRIPT OFICIAL DE INSTALACIÓN INNO SETUP PARA PARKFLOW DESKTOP (WPF .NET 10)
; ==============================================================================

#define MyAppName "ParkFlow Desktop"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ParkFlow Systems"
#define MyAppURL "https://parking-flow.com"
#define MyAppExeName "Parking.exe"

[Setup]
AppId={{D37E6B81-542B-4A1B-8B2E-9C8F5B8D12A4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
; Instalación a nivel de usuario para permitir auto-actualizaciones sin elevación de UAC en cada release
DefaultDirName={localappdata}\Programs\ParkFlow
DefaultGroupName=ParkFlow
DisableProgramGroupPage=yes
OutputBaseFilename=ParkFlow_Setup_v{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Dirs]
Name: "{localappdata}\Programs\ParkFlow"
Name: "{localappdata}\ParkFlow\Data"
Name: "{localappdata}\ParkFlow\Backups"
Name: "{localappdata}\ParkFlow\Temp"

[Files]
; Binarios de la aplicación compilada (Se reemplazan en la carpeta Programs)
Source: "..\Releases\v{#MyAppVersion}\Staging\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
