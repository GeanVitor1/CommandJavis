; Vox — instalador premium (Inno Setup 6.7 · modern)
; Geral: .\build.ps1 → publish\ + ISCC.exe installer.iss → install\VoxSetup.exe
; Design: dark editorial, single accent #6C5CE7, radii 12, sem wizard genérico

#define AppName "Vox"
#define AppVersion "1.1.0"
#define Publisher "Vox"
#define AppExe "Vox.exe"
#define AppId "{{8C9F3E2A-5D1B-4E0F-9C2D-VOXVOXVOX01}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
AppPublisherURL=https://github.com/GeanVitor1/CommandJavis
AppSupportURL=https://github.com/GeanVitor1/CommandJavis
AppUpdatesURL=https://github.com/GeanVitor1/CommandJavis/releases
AppCopyright=© 2026 Vox · assistente local
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Vox — atalhos e voz local
VersionInfoProductName=Vox
VersionInfoCopyright=© 2026 Vox
DefaultDirName={localappdata}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=no
AllowNoIcons=yes
OutputDir=install
OutputBaseFilename=VoxSetup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern
WizardSizePercent=115
WizardImageFile=installer\WizardImage.bmp
WizardSmallImageFile=installer\WizardSmallImage.bmp
SetupIconFile=Vox.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
DisableWelcomePage=no
DisableReadyPage=no
ShowLanguageDialog=no
AppMutex=Vox.SingleInstance
SetupMutex=VoxSetupMutex

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
brazilianportuguese.WelcomeLabel1=Bem-vindo ao %1
brazilianportuguese.WelcomeLabel2=Este assistente vai instalar o %1 no seu computador.%n%nVox é seu launcher + voz 100% local — sem nuvem.%n%nRecomendado fechar outros apps antes de continuar.
brazilianportuguese.ClickNext=Clique em Avançar para continuar, ou em Cancelar para sair.
brazilianportuguese.FinishedHeadingLabel=Pronto! %1 instalado
brazilianportuguese.FinishedLabel=O %1 foi instalado com sucesso.%n%nAtalhos criados no Menu Iniciar. Você pode fixar na Barra de Tarefas.
brazilianportuguese.SelectDirLabel3=Onde o %1 deve ser instalado?
brazilianportuguese.SelectStartMenuLabel3=Onde colocar os atalhos no Menu Iniciar?

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"
Name: "autostart"; Description: "Iniciar com o Windows"; GroupDescription: "Inicialização:"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Icon assets já embutidos no exe via ApplicationIcon, mas garante licença/readme se houver
Source: "Vox.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\{#AppExe}"; Comment: "Vox — atalhos e voz"; AppUserModelID: "Vox.App"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"; IconFilename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon; IconFilename: "{app}\{#AppExe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir o {#AppName} agora"; Flags: nowait postinstall skipifsilent unchecked

[UninstallDelete]
Type: filesandordirs; Name: "{app}\voice"
Type: filesandordirs; Name: "{localappdata}\Vox\TtsCache"

[Code]
var
  DarkColor: Integer;
  DarkText: Integer;
  AccentColor: Integer;

procedure ApplyDarkWizard();
var
  i: Integer;
  Comp: TComponent;
begin
  DarkColor := $17110D;
  DarkText := clWhite;
  AccentColor := $E75C6C;
  try WizardForm.Color := DarkColor; except end;
  try WizardForm.MainPanel.Color := DarkColor; except end;
  try WizardForm.WelcomePage.Color := DarkColor; except end;
  try WizardForm.InnerPage.Color := DarkColor; except end;
  try WizardForm.FinishedPage.Color := DarkColor; except end;
  try WizardForm.LicensePage.Color := DarkColor; except end;
  try WizardForm.SelectDirPage.Color := DarkColor; except end;
  try WizardForm.SelectProgramGroupPage.Color := DarkColor; except end;
  try WizardForm.ReadyPage.Color := DarkColor; except end;
  try WizardForm.InstallingPage.Color := DarkColor; except end;
  try WizardForm.DirEdit.Color := $1E1E2A; except end;
  try WizardForm.DirEdit.Font.Color := DarkText; except end;
  try WizardForm.GroupEdit.Color := $1E1E2A; except end;
  try WizardForm.GroupEdit.Font.Color := DarkText; except end;
  try WizardForm.WelcomeLabel1.Font.Color := DarkText; except end;
  try WizardForm.WelcomeLabel2.Font.Color := $CCCCCC; except end;
  try WizardForm.PageNameLabel.Font.Color := DarkText; except end;
  try WizardForm.PageDescriptionLabel.Font.Color := $CCCCCC; except end;
  // Fuerza todos os labels/checks para branco em fundo dark (corrige bug preto-no-preto do print)
  for i := 0 to WizardForm.ComponentCount - 1 do
  begin
    Comp := WizardForm.Components[i];
    try
      if Comp is TLabel then TLabel(Comp).Font.Color := DarkText;
    except end;
    try
      if Comp is TNewStaticText then TNewStaticText(Comp).Font.Color := DarkText;
    except end;
    try
      if Comp is TNewCheckBox then TNewCheckBox(Comp).Font.Color := DarkText;
    except end;
    try
      if Comp is TRadioButton then TRadioButton(Comp).Font.Color := DarkText;
    except end;
  end;
  // Tambem percorre paginas internas
  try
    for i := 0 to WizardForm.SelectProgramGroupPage.ControlCount - 1 do
      try
        if WizardForm.SelectProgramGroupPage.Controls[i] is TLabel then
          TLabel(WizardForm.SelectProgramGroupPage.Controls[i]).Font.Color := DarkText;
        if WizardForm.SelectProgramGroupPage.Controls[i] is TNewStaticText then
          TNewStaticText(WizardForm.SelectProgramGroupPage.Controls[i]).Font.Color := DarkText;
      except end;
  except end;
end;

procedure InitializeWizard();
begin
  ApplyDarkWizard();
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  ApplyDarkWizard();
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  RunKey: string;
  Value: string;
begin
  if CurStep = ssPostInstall then
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

function InitializeSetup(): Boolean;
begin
  Result := True;
end;
