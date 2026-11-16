; Production Windows installer for the Flutter Desktop Release folder.
; Build only from the exact reviewed Git commit using Build-ProductionInstaller.ps1.

#ifndef AppSource
  #error AppSource must point to the clean Flutter Windows Release directory.
#endif
#ifndef BuildMetadata
  #error BuildMetadata must point to the generated build manifest.
#endif
#ifndef InstallerOutput
  #error InstallerOutput must point to a new, exclusive output directory.
#endif
#ifndef AppVersion
  #error AppVersion is required.
#endif
#ifndef BuildNumber
  #error BuildNumber is required.
#endif
#ifndef CommitShort
  #error CommitShort is required.
#endif
#ifndef VCRedistPath
  #error VCRedistPath must point to the verified Microsoft x64 redistributable.
#endif

#define AppName "Avelri Gestão"
#define AppExecutable "avelri_gestao.exe"
; Update these pins only after reviewing a new official Microsoft package.
#define VCRedistMajor 14
#define VCRedistMinor 52
#define VCRedistBuild 37029
#define VCRedistRevision 0
#define VCRedistSha256 "9C02C9ED120F671B900E6B9471D890E3B2414117A7E53F4AE2E855466FA2B7C9"
#define VCHelperSha256 GetSHA256OfFile(SourcePath + "\Install-VCRedistElevated.ps1")

#ifndef MicrosoftRedistributionRightsConfirmed
  #error Licensed Microsoft Visual Studio redistribution rights must be explicitly confirmed.
#endif
#if MicrosoftRedistributionRightsConfirmed != "1"
  #error Microsoft Visual C++ redistribution is blocked until all applicable license requirements are met.
#endif

[Setup]
; Stable Production identity. Deliberately distinct from the homologation AppId.
AppId={{B46B7D1A-6E54-4C0A-A1E2-5F41C6D92837}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}+{#BuildNumber}
AppPublisher=Avelri
AppCopyright=Copyright (C) Avelri
DefaultDirName={localappdata}\Programs\Avelri\Gestao
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Uninstallable=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExecutable}
OutputDir={#InstallerOutput}
OutputBaseFilename=AvelriGestao-Production-{#AppVersion}+{#BuildNumber}-{#CommitShort}
VersionInfoVersion={#AppVersion}.{#BuildNumber}
VersionInfoProductVersion={#AppVersion}.{#BuildNumber}
VersionInfoCompany=Avelri
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Production build {#AppVersion}+{#BuildNumber} ({#CommitShort})
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos adicionais:"; Flags: unchecked

[Files]
Source: "{#VCRedistPath}"; DestName: "vc_redist.x64.exe"; Flags: dontcopy noencryption
Source: "Install-VCRedistElevated.ps1"; Flags: dontcopy noencryption
Source: "{#AppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.ilk,*.obj,*.exp,*.lib,*.map"
Source: "{#BuildMetadata}"; DestDir: "{app}"; DestName: "Avelri-Build-Info.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Avelri Gestão"; Filename: "{app}\{#AppExecutable}"
Name: "{autodesktop}\Avelri Gestão"; Filename: "{app}\{#AppExecutable}"; Tasks: desktopicon

; No [Run] or [UninstallDelete]: setup never launches the app and uninstall
; does not remove application-support files or credentials outside {app}.

[Code]
const
  VCRuntimeKey = 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64';

function HasRequiredVCRuntime: Boolean;
var
  Installed, Major, Minor, Build, Revision: Cardinal;
begin
  Result := False;
  { Microsoft's runtime registration is in the 32-bit registry view on x64 Windows. }
  if not RegQueryDWordValue(HKLM32, VCRuntimeKey, 'Installed', Installed) or
     (Installed <> 1) or
     not RegQueryDWordValue(HKLM32, VCRuntimeKey, 'Major', Major) or
     not RegQueryDWordValue(HKLM32, VCRuntimeKey, 'Minor', Minor) or
     not RegQueryDWordValue(HKLM32, VCRuntimeKey, 'Bld', Build) or
     not RegQueryDWordValue(HKLM32, VCRuntimeKey, 'Rbld', Revision) then
    Exit;

  Result := (Major > {#VCRedistMajor}) or
    ((Major = {#VCRedistMajor}) and ((Minor > {#VCRedistMinor}) or
    ((Minor = {#VCRedistMinor}) and ((Build > {#VCRedistBuild}) or
    ((Build = {#VCRedistBuild}) and (Revision >= {#VCRedistRevision}))))));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  RedistPath, HelperPath, Parameters: String;
  ExitCode: Integer;
begin
  Result := '';
  if HasRequiredVCRuntime then
    Exit;

  try
    ExtractTemporaryFile('vc_redist.x64.exe');
    ExtractTemporaryFile('Install-VCRedistElevated.ps1');
    RedistPath := ExpandConstant('{tmp}\vc_redist.x64.exe');
    HelperPath := ExpandConstant('{tmp}\Install-VCRedistElevated.ps1');
    if (CompareText(GetSHA256OfFile(RedistPath), '{#VCRedistSha256}') <> 0) or
       (CompareText(GetSHA256OfFile(HelperPath), '{#VCHelperSha256}') <> 0) then
    begin
      Result := 'O pré-requisito Microsoft Visual C++ x64 não passou na verificação de integridade. Nenhum arquivo do Avelri Gestão foi instalado.';
      Exit;
    end;
  except
    Result := 'Não foi possível extrair ou verificar o pré-requisito Microsoft Visual C++ x64. Nenhum arquivo do Avelri Gestão foi instalado.';
    Exit;
  end;

  Parameters := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
    HelperPath + '" -RedistributablePath "' + RedistPath + '"';
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      Parameters, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    Result := 'Não foi possível iniciar o instalador do pré-requisito Microsoft Visual C++ x64 (erro ' + IntToStr(ExitCode) + ').';
    Exit;
  end;

  if (ExitCode = 3010) or (ExitCode = 1641) then
  begin
    NeedsRestart := True;
    Result := 'O Microsoft Visual C++ x64 foi instalado, mas o Windows precisa reiniciar antes da instalação do Avelri Gestão. Reinicie e execute este instalador novamente.';
    Exit;
  end;
  if (ExitCode = 1223) or (ExitCode = 1602) then
  begin
    Result := 'A autorização administrativa ou a instalação do Microsoft Visual C++ x64 foi cancelada. O Avelri Gestão não foi instalado.';
    Exit;
  end;
  if ExitCode <> 0 then
  begin
    Result := 'A instalação do Microsoft Visual C++ x64 falhou (código ' + IntToStr(ExitCode) + '). O Avelri Gestão não foi instalado.';
    Exit;
  end;
  if not HasRequiredVCRuntime then
    Result := 'O Microsoft Visual C++ x64 não foi confirmado após a instalação. O Avelri Gestão não foi instalado.';
end;
