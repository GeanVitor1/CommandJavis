; Vox - instalador (Inno Setup 6)
; Compile: ISCC.exe installer.iss  (gera install\VoxSetup.exe)
; Antes: rode .\build.ps1 para gerar ./publish atualizado.

#define AppName "Vox"
#define AppVersion "1.0.0"
#define Publisher "Vox"
#define AppExe "Vox.exe"

[Setup]
AppId={{8C9F3E2A-5D1B-4E0F-9C2D-VOXVOXVOX01}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={localappdata}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=install
OutputBaseFilename=VoxSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "autostart"; Description: "Iniciar com o Windows"; GroupDescription: "Inicialização:"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir o {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\voice"
Type: filesandordirs; Name: "{localappdata}\Vox\TtsCache"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  RunKey: string;
  Value: string;
begin
  if CurStep = CurStepPostInstall then
  begin
    if WizardIsTaskSelected('autostart') then
    begin
      RunKey := 'Software\Microsoft\Windows\CurrentVersion\Run';
      Value := '"' + ExpandConstant('{app}') + '\{#AppExe}"';
      RegWriteStringValue(HKCU, RunKey, '{#AppName}', Value);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}');
end;