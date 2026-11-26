// Shared runtime policy. Production and synthetic tests use this same code.
// The registry scope is per Windows user, not a statement of tenant-wide consent.
type
  TAvelriLegalSystemTime = record
    Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds: Word;
  end;

procedure AvelriLegalGetSystemTime(var Value: TAvelriLegalSystemTime);
  external 'GetSystemTime@kernel32.dll stdcall';

var
  AvelriExplicitAcceptanceThisRun: Boolean;

function AvelriLegalReceiptKey: String;
begin
  Result := '{#AvelriLegalRegistryKey}\Accepted\{#AvelriTermsRevision}\{#AvelriTermsSha256}';
end;

function AvelriHasCurrentLegalAcceptance: Boolean;
var
  Revision, Hash, Completed, AcceptedAt: String;
begin
  Result :=
    RegQueryStringValue(HKCU, AvelriLegalReceiptKey, 'Completed', Completed) and
    (Completed = '1') and
    RegQueryStringValue(HKCU, AvelriLegalReceiptKey, 'Revision', Revision) and
    (Revision = '{#AvelriTermsRevision}') and
    RegQueryStringValue(HKCU, AvelriLegalReceiptKey, 'SHA256', Hash) and
    (CompareText(Hash, '{#AvelriTermsSha256}') = 0) and
    RegQueryStringValue(HKCU, AvelriLegalReceiptKey, 'AcceptedAtUtc', AcceptedAt) and
    (AcceptedAt <> '');
end;

function AvelriLegalInitializeSetup: Boolean;
begin
  AvelriExplicitAcceptanceThisRun := False;
  Result := not WizardSilent or AvelriHasCurrentLegalAcceptance;
  if not Result then
    Log('AVELRI_LEGAL_BLOCKED: mandatory acceptance cannot be collected in silent mode.');
end;

procedure AvelriLegalInitializeWizard;
begin
  WizardForm.LicenseNotAcceptedRadio.Checked := True;
end;

function AvelriLegalShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = wpLicense) and AvelriHasCurrentLegalAcceptance;
  if Result then Log('AVELRI_LEGAL_REUSED: identical revision and SHA-256.');
end;

function AvelriLegalNextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpLicense then
  begin
    { Do not treat silent simulated Next clicks as a user's manifestation. }
    AvelriExplicitAcceptanceThisRun := not WizardSilent and
      WizardForm.LicenseAcceptedRadio.Checked;
    Result := AvelriExplicitAcceptanceThisRun;
  end;
end;

function AvelriLegalPrepareToInstall: String;
begin
  Result := '';
  if not AvelriHasCurrentLegalAcceptance and
     not AvelriExplicitAcceptanceThisRun then
    Result := 'A instalação não foi autorizada: é necessário aceitar os termos apresentados. Nenhum pré-requisito ou serviço foi alterado.';
end;

function AvelriLegalUtcNow: String;
var
  Value: TAvelriLegalSystemTime;
begin
  AvelriLegalGetSystemTime(Value);
  Result := Format('%.4d-%.2d-%.2dT%.2d:%.2d:%.2dZ', [
    Value.Year, Value.Month, Value.Day, Value.Hour, Value.Minute, Value.Second]);
end;

procedure AvelriLegalRecordSuccessfulInstall;
var
  Key: String;
begin
  if AvelriHasCurrentLegalAcceptance then Exit;
  if not AvelriExplicitAcceptanceThisRun then
    RaiseException('Não foi possível registrar o aceite: manifestação explícita ausente.');
  Key := AvelriLegalReceiptKey;
  { Completed is the final commit marker; an incomplete record is never reused. }
  if not RegWriteStringValue(HKCU, Key, 'Revision', '{#AvelriTermsRevision}') or
     not RegWriteStringValue(HKCU, Key, 'SHA256', '{#AvelriTermsSha256}') or
     not RegWriteStringValue(HKCU, Key, 'AcceptedAtUtc', AvelriLegalUtcNow) or
     not RegWriteStringValue(HKCU, Key, 'InstallerVersion', '{#AppVersion}+{#BuildNumber}') or
     not RegWriteStringValue(HKCU, Key, 'Language', 'pt-BR') or
     not RegWriteStringValue(HKCU, Key, 'Scope', 'CurrentWindowsUser') or
     not RegWriteStringValue(HKCU, Key, 'Completed', '1') then
    RaiseException('A instalação terminou, mas o registro local de aceite falhou. Os termos serão solicitados novamente na próxima instalação.');
  Log('AVELRI_LEGAL_RECORDED: explicit acceptance persisted after successful installation.');
end;
