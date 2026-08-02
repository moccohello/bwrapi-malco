; Compile with:
;   ISCC.exe /DPreparedRoot=<absolute-prepared-root> /DAppVersion=<version> \
;     /DInstallerOutput=<absolute-output-directory> Malco.iss

#ifndef PreparedRoot
  #error PreparedRoot must name the complete prepared Malco install root.
#endif

#define AppName "Malco"
#define LauncherName "Malco.Launcher.exe"

#if !DirExists(PreparedRoot)
  #error PreparedRoot does not exist or is not a directory.
#endif

#if !FileExists(PreparedRoot + "\" + LauncherName)
  #error PreparedRoot does not contain Malco.Launcher.exe at its root.
#endif

#if !FileExists(PreparedRoot + "\launcher-policy.json")
  #error PreparedRoot does not contain launcher-policy.json at its root.
#endif

#if !FileExists(PreparedRoot + "\state\install-state.json")
  #error PreparedRoot does not contain the initial atomic launcher state.
#endif

#ifndef AppVersion
  #error AppVersion must identify the prepared Malco release.
#endif

#ifndef InstallerOutput
  #define InstallerOutput "."
#endif

#ifndef DotNetMinimumVersion
  #error DotNetMinimumVersion must identify the required .NET Desktop Runtime.
#endif

#ifndef DotNetInstallerFileName
  #error DotNetInstallerFileName must identify the official runtime installer.
#endif

#ifndef DotNetDownloadUrl
  #error DotNetDownloadUrl must identify the official runtime download.
#endif

#ifndef DotNetInstallerSha256
  #error DotNetInstallerSha256 must identify the approved runtime installer.
#endif

[Languages]
Name: "en_us"; MessagesFile: "compiler:Default.isl,language-en-US.isl"; LicenseFile: "terms-en.txt"
Name: "ko_kr"; MessagesFile: "compiler:Default.isl,compiler:Languages\Korean.isl,language-ko-KR.isl"; LicenseFile: "terms-ko.txt"

[CustomMessages]
en_us.StartMalco=Start Malco
en_us.SilentUnsupported=Malco requires the user to review and accept the data collection terms, so unattended installation is not supported.
en_us.UnsafeInstallPath=Setup refuses to use a reparse-point install directory.
en_us.MarkerFailure=Could not create the fixed Malco install-root marker.
en_us.LanguageFailure=Could not save the selected Malco language.
en_us.DotNetDownloadCaption=Downloading required component
en_us.DotNetDownloadDescription=Malco requires Microsoft .NET Desktop Runtime %1 or later.
en_us.DotNetInstallCaption=Installing required component
en_us.DotNetInstallDescription=Windows may ask for administrator approval.
en_us.DotNetInstallProgress=Installing Microsoft .NET Desktop Runtime...
en_us.DotNetDownloadFailure=The required Microsoft .NET Desktop Runtime could not be downloaded. Check your internet connection and try again.
en_us.DotNetIntegrityFailure=The downloaded Microsoft .NET Desktop Runtime installer failed integrity verification. Run Setup again.
en_us.DotNetLaunchFailure=The Microsoft .NET Desktop Runtime installer could not be started: %1
en_us.DotNetInstallFailure=Microsoft .NET Desktop Runtime installation failed with exit code %1.
en_us.DotNetVerificationFailure=Microsoft .NET Desktop Runtime was not detected after installation.
ko_kr.StartMalco=Malco 시작
ko_kr.SilentUnsupported=Malco를 설치하려면 정보 수집 약관을 검토하고 동의해야 하므로 무인 설치는 지원하지 않습니다.
ko_kr.UnsafeInstallPath=재분석 지점으로 연결된 폴더에는 Malco를 설치할 수 없습니다.
ko_kr.MarkerFailure=Malco 설치 폴더 표식을 만들지 못했습니다.
ko_kr.LanguageFailure=선택한 Malco 언어를 저장하지 못했습니다.
ko_kr.DotNetDownloadCaption=필수 구성 요소 다운로드
ko_kr.DotNetDownloadDescription=Malco를 실행하려면 Microsoft .NET Desktop Runtime %1 이상이 필요합니다.
ko_kr.DotNetInstallCaption=필수 구성 요소 설치
ko_kr.DotNetInstallDescription=Windows에서 관리자 권한 승인을 요청할 수 있습니다.
ko_kr.DotNetInstallProgress=Microsoft .NET Desktop Runtime을 설치하는 중입니다...
ko_kr.DotNetDownloadFailure=필수 Microsoft .NET Desktop Runtime을 다운로드하지 못했습니다. 인터넷 연결을 확인하고 다시 시도해 주세요.
ko_kr.DotNetIntegrityFailure=다운로드한 Microsoft .NET Desktop Runtime 설치 파일의 무결성을 확인하지 못했습니다. 설치를 다시 실행해 주세요.
ko_kr.DotNetLaunchFailure=Microsoft .NET Desktop Runtime 설치 프로그램을 시작하지 못했습니다: %1
ko_kr.DotNetInstallFailure=Microsoft .NET Desktop Runtime 설치가 실패했습니다. 종료 코드: %1
ko_kr.DotNetVerificationFailure=설치 후 Microsoft .NET Desktop Runtime을 확인하지 못했습니다.

[Setup]
AppId={{76D40A9B-231C-4FA4-9274-607E7A9A76E4}
AppName={#AppName}
AppVersion={#AppVersion}
VersionInfoProductTextVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Malco
SetupIconFile=malco.ico
ShowLanguageDialog=yes
UsePreviousLanguage=no
LanguageDetectionMethod=none
DefaultDirName={localappdata}\Programs\Malco
DefaultGroupName=Malco
DisableDirPage=yes
DisableProgramGroupPage=yes
UsePreviousAppDir=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
AppMutex=Local\Malco.Desktop.SingleInstance.v1,Local\Malco.Desktop.UpdateLauncher.v1
CloseApplications=no
RestartApplications=no
Uninstallable=yes
CreateUninstallRegKey=yes
UninstallDisplayIcon={app}\{#LauncherName}
OutputDir={#InstallerOutput}
OutputBaseFilename=Malco-{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
DiskSpanning=no
WizardStyle=modern
SetupLogging=yes
[Files]
Source: "{#PreparedRoot}\*"; DestDir: "{app}"; Excludes: "data\*,state\*,cache\*,staging\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PreparedRoot}\state\install-state.json"; DestDir: "{app}\state"; Flags: ignoreversion

[Dirs]
Name: "{app}\data"
Name: "{app}\state"
Name: "{app}\cache"
Name: "{app}\staging"
Name: "{app}\versions"

[Icons]
Name: "{userprograms}\Malco\Malco"; Filename: "{app}\{#LauncherName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#LauncherName}"

[Run]
Filename: "{app}\{#LauncherName}"; Description: "{cm:StartMalco}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent; Check: CanLaunchMalcoAfterInstall

[UninstallDelete]
Type: files; Name: "{userdocs}\StarCraft\Malco\hud-layout.json"
Type: files; Name: "{userdocs}\StarCraft\Malco\hud-layout.json.tmp"
Type: files; Name: "{userdocs}\StarCraft\Malco\hud-layout.json.saving.*.tmp"
Type: files; Name: "{userdocs}\StarCraft\Malco\overlay-hud-metrics.json"
Type: files; Name: "{userdocs}\StarCraft\Malco\.malco-migration-*.tmp"
Type: dirifempty; Name: "{userdocs}\StarCraft\Malco"
Type: files; Name: "{app}\data\hud-layout.json"
Type: files; Name: "{app}\data\hud-layout.json.tmp"
Type: files; Name: "{app}\data\hud-layout.json.saving.*.tmp"
Type: files; Name: "{app}\data\overlay-hud-metrics.json"
Type: files; Name: "{app}\data\.migration-v1.complete"
Type: files; Name: "{app}\data\.malco-migration-*.tmp"
Type: files; Name: "{app}\data\telemetry-queue.json"
Type: files; Name: "{app}\data\game-start-telemetry-queue.json"
Type: files; Name: "{app}\data\game-start-telemetry-queue.json.*.tmp"
Type: files; Name: "{app}\data\telemetry-installation-id.txt"
Type: files; Name: "{app}\data\telemetry-installation-id.txt.*.tmp"
Type: files; Name: "{app}\data\installer-language.txt"
Type: dirifempty; Name: "{app}\data"
Type: filesandordirs; Name: "{app}\state"
Type: filesandordirs; Name: "{app}\cache"
Type: filesandordirs; Name: "{app}\staging"
Type: filesandordirs; Name: "{app}\versions"
Type: files; Name: "{app}\.malco-install-root"
Type: dirifempty; Name: "{app}"

[Code]
const
  DotNetRegistryKey = 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  DotNetMinimumVersion = '{#DotNetMinimumVersion}';
  DotNetInstallerFileName = '{#DotNetInstallerFileName}';
  DotNetDownloadUrl = '{#DotNetDownloadUrl}';
  DotNetInstallerSha256 = '{#DotNetInstallerSha256}';

var
  RuntimeRestartRequired: Boolean;

function WinGetFileAttributes(FileName: String): Integer;
  external 'GetFileAttributesW@kernel32.dll stdcall';

function RegistryHasRequiredDesktopRuntime(RootKey: Integer): Boolean;
var
  ValueNames: TArrayOfString;
  Index: Integer;
  InstalledValue: Cardinal;
  InstalledVersion: Int64;
  MinimumVersion: Int64;
begin
  Result := False;
  if not StrToVersion(DotNetMinimumVersion + '.0', MinimumVersion) then
    Exit;
  if not RegGetValueNames(RootKey, DotNetRegistryKey, ValueNames) then
    Exit;
  for Index := 0 to GetArrayLength(ValueNames) - 1 do
  begin
    if (Pos('10.0.', ValueNames[Index]) = 1) and
       StrToVersion(ValueNames[Index] + '.0', InstalledVersion) and
       (ComparePackedVersion(InstalledVersion, MinimumVersion) >= 0) and
       RegQueryDWordValue(RootKey, DotNetRegistryKey, ValueNames[Index], InstalledValue) and
       (InstalledValue = 1) then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function HasRequiredDesktopRuntime(): Boolean;
begin
  Result :=
    RegistryHasRequiredDesktopRuntime(HKLM32) or
    RegistryHasRequiredDesktopRuntime(HKLM64);
end;

function CanLaunchMalcoAfterInstall(): Boolean;
begin
  Result := not RuntimeRestartRequired;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  DownloadPage: TDownloadWizardPage;
  InstallPage: TOutputProgressWizardPage;
  InstallerPath: String;
  InstallerLock: TFileStream;
  ResultCode: Integer;
begin
  Result := '';
  if HasRequiredDesktopRuntime() then
    Exit;

  DownloadPage := CreateDownloadPage(
    CustomMessage('DotNetDownloadCaption'),
    FmtMessage(CustomMessage('DotNetDownloadDescription'), [DotNetMinimumVersion]),
    nil);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
  DownloadPage.Clear;
  DownloadPage.Add(
    DotNetDownloadUrl,
    DotNetInstallerFileName,
    DotNetInstallerSha256);
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      Log('Microsoft .NET Desktop Runtime download failed: ' + GetExceptionMessage);
      Result := CustomMessage('DotNetDownloadFailure');
      Exit;
    end;
  finally
    DownloadPage.Hide;
  end;

  InstallerPath := ExpandConstant('{tmp}\') + DotNetInstallerFileName;
  try
    InstallerLock := TFileStream.Create(
      InstallerPath,
      fmOpenRead or fmShareDenyWrite);
  except
    Log('Could not lock the downloaded Microsoft .NET Desktop Runtime installer: ' +
      GetExceptionMessage);
    Result := CustomMessage('DotNetIntegrityFailure');
    Exit;
  end;
  try
    if CompareText(GetSHA256OfFile(InstallerPath), DotNetInstallerSha256) <> 0 then
    begin
      Log('The downloaded Microsoft .NET Desktop Runtime installer changed after download.');
      Result := CustomMessage('DotNetIntegrityFailure');
      Exit;
    end;

    InstallPage := CreateOutputProgressPage(
      CustomMessage('DotNetInstallCaption'),
      CustomMessage('DotNetInstallDescription'));
    InstallPage.SetText(CustomMessage('DotNetInstallProgress'), '');
    InstallPage.SetProgress(1, 2);
    InstallPage.Show;
    try
      if not ShellExec(
          'runas',
          InstallerPath,
          '/install /quiet /norestart',
          '',
          SW_SHOWNORMAL,
          ewWaitUntilTerminated,
          ResultCode) then
      begin
        Result := FmtMessage(
          CustomMessage('DotNetLaunchFailure'), [SysErrorMessage(ResultCode)]);
        Exit;
      end;
      if (ResultCode <> 0) and (ResultCode <> 3010) then
      begin
        Result := FmtMessage(
          CustomMessage('DotNetInstallFailure'), [IntToStr(ResultCode)]);
        Exit;
      end;
      InstallPage.SetProgress(2, 2);
    finally
      InstallPage.Hide;
    end;
  finally
    InstallerLock.Free;
  end;

  if not HasRequiredDesktopRuntime() then
  begin
    Result := CustomMessage('DotNetVerificationFailure');
    Exit;
  end;
  RuntimeRestartRequired := ResultCode = 3010;
  if RuntimeRestartRequired then
    NeedsRestart := True;
end;

function InitializeSetup(): Boolean;
var
  InstallPath: String;
  InstallAttributes: Integer;
begin
  if WizardSilent then
  begin
    MsgBox(
      ExpandConstant('{cm:SilentUnsupported}'),
      mbError,
      MB_OK);
    Result := False;
    Exit;
  end;
  InstallPath := ExpandConstant('{localappdata}\Programs\Malco');
  InstallAttributes := WinGetFileAttributes(InstallPath);
  if (InstallAttributes <> -1) and ((InstallAttributes and $400) <> 0) then
  begin
    MsgBox(ExpandConstant('{cm:UnsafeInstallPath}'), mbError, MB_OK);
    Result := False;
    Exit;
  end;
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  MarkerPath: String;
  LanguagePath: String;
  LanguageCode: String;
begin
  if CurStep <> ssPostInstall then
    Exit;

  MarkerPath := ExpandConstant('{app}\.malco-install-root');
  if not SaveStringToFile(MarkerPath, 'malco-install-root=1' + #13#10, False) then
    RaiseException(ExpandConstant('{cm:MarkerFailure}'));

  if CompareText(ActiveLanguage, 'ko_kr') = 0 then
    LanguageCode := 'ko-KR'
  else
    LanguageCode := 'en-US';
  LanguagePath := ExpandConstant('{app}\data\installer-language.txt');
  if not SaveStringToFile(LanguagePath, LanguageCode + #13#10, False) then
    RaiseException(ExpandConstant('{cm:LanguageFailure}'));
end;
