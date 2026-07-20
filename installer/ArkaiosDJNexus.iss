#define AppName "Arkaios DJ Nexus"
#define AppVersion "1.2.0"
#define AppPublisher "Arkaios"
#define AppExeName "ArkaiosDJ.exe"

[Setup]
AppId={{A9BBDA1E-8547-4A7C-8B25-E1104E95926E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\Arkaios DJ Nexus
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=ArkaiosDJ_Nexus_Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Files]
Source: "payload\ArkaiosDJ.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "payload\yt-dlp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "payload\config.txt"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "payload\Validate-ArkaiosLicense.ps1"; Flags: dontcopy

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Abrir {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  LicensePage: TInputQueryWizardPage;
  RegisterButton: TNewButton;
  HwidLabel: TNewStaticText;
  InstallerHwid: String;

function GetInstallerHardwareId: String;
var
  ResultCode: Integer;
  OutputFile: String;
  Args: String;
  Lines: TArrayOfString;
begin
  Result := 'HWID_NOT_FOUND';
  OutputFile := ExpandConstant('{tmp}\arkaios-installer-hwid.txt');
  if FileExists(OutputFile) then
    DeleteFile(OutputFile);

  Args := '-NoProfile -ExecutionPolicy Bypass -Command "' +
    '$n=[System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | Where-Object { $_.OperationalStatus -eq ''Up'' -and $_.NetworkInterfaceType -ne ''Loopback'' } | Select-Object -First 1; ' +
    'if ($n) { $n.GetPhysicalAddress().ToString() } else { ''HWID_NOT_FOUND_'' + $env:COMPUTERNAME } | Out-File -LiteralPath ''' + OutputFile + ''' -Encoding ascii"';

  if Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Args, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if LoadStringsFromFile(OutputFile, Lines) and (GetArrayLength(Lines) > 0) then
      Result := Trim(Lines[0]);
  end;
end;

procedure RegisterButtonClick(Sender: TObject);
var
  ResultCode: Integer;
  Url: String;
begin
  Url := 'https://arkaios-world.web.app/dj-license?hwid=' + InstallerHwid;
  ShellExec('open', Url, '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
end;

procedure InitializeWizard;
begin
  InstallerHwid := GetInstallerHardwareId();
  LicensePage := CreateInputQueryPage(
    wpWelcome,
    'Activacion de Arkaios DJ Nexus',
    'Valida tu usuario/licencia antes de instalar',
    'Si todavia no tienes licencia, abre Arkaios World, registra tu usuario y pega aqui el serial generado.'
  );
  LicensePage.Add('Serial / licencia:', False);

  HwidLabel := TNewStaticText.Create(WizardForm);
  HwidLabel.Parent := LicensePage.Surface;
  HwidLabel.Left := LicensePage.Edits[0].Left;
  HwidLabel.Top := LicensePage.Edits[0].Top + LicensePage.Edits[0].Height + ScaleY(8);
  HwidLabel.Width := ScaleX(420);
  HwidLabel.Height := ScaleY(32);
  HwidLabel.Caption := 'Hardware ID de este equipo: ' + InstallerHwid;

  RegisterButton := TNewButton.Create(WizardForm);
  RegisterButton.Parent := LicensePage.Surface;
  RegisterButton.Left := LicensePage.Edits[0].Left;
  RegisterButton.Top := HwidLabel.Top + HwidLabel.Height + ScaleY(8);
  RegisterButton.Width := ScaleX(190);
  RegisterButton.Height := ScaleY(28);
  RegisterButton.Caption := 'Abrir Arkaios World';
  RegisterButton.OnClick := @RegisterButtonClick;
end;

function ReadValidationMessage(ResultFile: String): String;
var
  Lines: TArrayOfString;
  Raw: String;
  PipePos: Integer;
begin
  Result := 'No se pudo validar la licencia.';
  if LoadStringsFromFile(ResultFile, Lines) and (GetArrayLength(Lines) > 0) then
  begin
    Raw := Lines[0];
    PipePos := Pos('|', Raw);
    if PipePos > 0 then
      Result := Copy(Raw, PipePos + 1, Length(Raw))
    else
      Result := Raw;
  end;
end;

function ValidateArkaiosLicense(Key: String): Boolean;
var
  KeyFile: String;
  ResultFile: String;
  Args: String;
  ResultCode: Integer;
  MessageText: String;
begin
  Result := False;
  KeyFile := ExpandConstant('{tmp}\arkaios-license-input.txt');
  ResultFile := ExpandConstant('{tmp}\arkaios-license-result.txt');

  ExtractTemporaryFile('Validate-ArkaiosLicense.ps1');
  SaveStringToFile(KeyFile, Key, False);
  if FileExists(ResultFile) then
    DeleteFile(ResultFile);

  Args :=
    '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{tmp}\Validate-ArkaiosLicense.ps1') +
    '" -KeyFile "' + KeyFile + '" -ResultFile "' + ResultFile + '"';

  WizardForm.NextButton.Enabled := False;
  WizardForm.NextButton.Caption := 'Validando...';
  try
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Args, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      MsgBox('No se pudo iniciar PowerShell para validar la licencia.', mbError, MB_OK);
      exit;
    end;
  finally
    WizardForm.NextButton.Caption := SetupMessage(msgButtonNext);
    WizardForm.NextButton.Enabled := True;
  end;

  if ResultCode = 0 then
  begin
    Result := True;
    exit;
  end;

  MessageText := ReadValidationMessage(ResultFile);
  MsgBox(MessageText + #13#10#13#10 + 'Puedes crear o recuperar tu usuario en https://arkaios-world.web.app/', mbError, MB_OK);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Key: String;
begin
  Result := True;
  if CurPageID = LicensePage.ID then
  begin
    Key := Trim(LicensePage.Values[0]);
    if Key = '' then
    begin
      MsgBox('Pega tu serial/licencia para continuar. Si no tienes uno, abre Arkaios World.', mbInformation, MB_OK);
      Result := False;
      exit;
    end;

    Result := ValidateArkaiosLicense(Key);
  end;
end;
