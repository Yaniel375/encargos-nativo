; Instalador de Encargos para Windows (Inno Setup 6).
; No incluye ningún secreto: el token del bot lo escribe cada usuario dentro de la app y se guarda cifrado con DPAPI.
#define AppVer GetEnv("ENCARGOS_VERSION")
#if AppVer == ""
  #define AppVer "1.0.0"
#endif

[Setup]
AppId={{6B1D4E2A-7C3F-4A8E-9B21-5E0C7D9A1F34}
AppName=Encargos
AppVersion={#AppVer}
AppPublisher=Encargos
DefaultDirName={localappdata}\Programs\Encargos Nativo
DefaultGroupName=Encargos
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\out
OutputBaseFilename=Encargos_Windows_Setup_{#AppVer}
SetupIconFile=Encargos.Win\Assets\icon.ico
UninstallDisplayIcon={app}\Encargos.exe
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "..\out\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Encargos"; Filename: "{app}\Encargos.exe"
Name: "{autodesktop}\Encargos"; Filename: "{app}\Encargos.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Encargos.exe"; Description: "Abrir Encargos"; Flags: nowait postinstall skipifsilent

; Los datos viven en %LOCALAPPDATA%\EncargosNativo y NO se borran al desinstalar.
