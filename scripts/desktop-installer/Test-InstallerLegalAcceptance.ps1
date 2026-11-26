[CmdletBinding()]
param(
    [ValidateSet('Prepare','VerifyFirst','VerifySame','VerifyChangedBlocked','VerifyDeclined','VerifyChangedAccepted','VerifyIncompleteReceipt')]
    [string]$Phase = 'Prepare',
    [string]$RunRoot,
    [string]$InnoSetupCompiler = (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
function Assert-LegalTest([bool]$Value,[string]$Message) { if (-not $Value) { throw $Message } }
function Run-SyntheticSilent([string]$File,[string]$Log) {
    $p = Start-Process -FilePath $File -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+$Log+'"')) -WindowStyle Hidden -PassThru -Wait
    return $p.ExitCode
}
function Receipt([string]$Revision,[string]$Hash) { return 'HKCU:\' + $state.RegistryKey + '\Accepted\' + $Revision + '\' + $Hash }
function Has-Receipt([string]$Revision,[string]$Hash) {
    # Registry-provider Test-Path can return true for an inaccessible key in a
    # restricted host. Fail on denied access instead of treating it as evidence.
    $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey(($state.RegistryKey+'\Accepted\'+$Revision+'\'+$Hash))
    if ($null -eq $key) { return $false }
    $key.Dispose()
    return $true
}
function Assert-OriginalState {
    Assert-LegalTest ((Get-FileHash -LiteralPath (Join-Path $RunRoot 'installed\payload.txt') -Algorithm SHA256).Hash -ceq $state.FirstPayloadSHA256) 'A refused update changed the payload.'
    Assert-LegalTest ([IO.File]::ReadAllText((Join-Path $RunRoot 'existing-service-sentinel.txt')) -ceq 'UNCHANGED') 'Synthetic existing-service sentinel changed.'
}

if ($Phase -eq 'Prepare') {
    if ([string]::IsNullOrWhiteSpace($RunRoot)) { $RunRoot = Join-Path $repo ('.test-output\issue69-legal-' + [guid]::NewGuid().ToString('N').Substring(0,8)) }
    if (Test-Path -LiteralPath $RunRoot) { throw 'Test output already exists; preserve it.' }
    New-Item -ItemType Directory -Path $RunRoot | Out-Null
    $id = [guid]::NewGuid().ToString('N')
    $state = [ordered]@{RegistryKey=('Software\Avelri\LegalAcceptanceTests\'+$id);Revision='1.0';RunRoot=$RunRoot}
    [IO.File]::WriteAllText((Join-Path $RunRoot 'existing-service-sentinel.txt'),'UNCHANGED')
    Import-Module (Join-Path $PSScriptRoot 'DistributionLegal.psm1') -Force
    $blocked = $false
    try { Get-AvelriApprovedDistributionLegalInputs -ApprovalPath (Join-Path $repo 'docs\distribution\legal-approval.DRAFT.json') | Out-Null }
    catch { if ($_.Exception.Message -notlike '*draft commercial use is blocked*') { throw }; $blocked = $true }
    Assert-LegalTest $blocked 'The commercial draft was not blocked.'
    Write-Output 'DRAFT_APPROVAL_GATE=PASS'

    $guardLog = Join-Path $RunRoot 'production-draft-block.log'
    & $InnoSetupCompiler (Join-Path $PSScriptRoot 'AvelriGestao-Production.iss') *> $guardLog
    Assert-LegalTest ($LASTEXITCODE -ne 0 -and [IO.File]::ReadAllText($guardLog).Contains('Draft commercial use is blocked')) 'Production preprocessing did not fail at the legal gate.'
    Write-Output 'PRODUCTION_DEFAULT_COMPILE_GATE=PASS'

    $template = @'
#define AppVersion "{VERSION}"
#define BuildNumber "{BUILD}"
#define AvelriTermsRevision "1.0"
#define AvelriTermsSha256 "{HASH}"
#define AvelriLegalRegistryKey "{REGISTRY}"
#define AvelriTermsFile "{TERMS}"
#define AvelriThirdPartyNoticesDir "{NOTICES}"
[Setup]
AppId=AvelriLegalTest-{TEST_APP_ID}
AppName=Avelri Teste Sintetico de Aceite
AppVersion={#AppVersion}
DefaultDirName={TARGET}
PrivilegesRequired=lowest
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableWelcomePage=yes
Uninstallable=no
CreateUninstallRegKey=no
CloseApplications=no
RestartApplications=no
OutputDir={OUT}
OutputBaseFilename={NAME}
LicenseFile={#AvelriTermsFile}
WizardStyle=modern
[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
[Files]
Source: "{PAYLOAD}"; DestDir: "{app}"; DestName: "payload.txt"; Flags: ignoreversion
{LEGAL_FILES}
[Code]
#include "{POLICY}"
function InitializeSetup: Boolean;
begin Result := AvelriLegalInitializeSetup; end;
procedure InitializeWizard;
begin AvelriLegalInitializeWizard; end;
function ShouldSkipPage(PageID: Integer): Boolean;
begin Result := AvelriLegalShouldSkipPage(PageID); end;
function NextButtonClick(CurPageID: Integer): Boolean;
begin Result := AvelriLegalNextButtonClick(CurPageID); end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := AvelriLegalPrepareToInstall;
  if Result <> '' then Exit;
  SaveStringToFile(ExpandConstant('{app}') + '.preflight.txt', 'AUTHORIZED', False);
end;
procedure CurStepChanged(CurStep: TSetupStep);
begin if CurStep = ssDone then AvelriLegalRecordSuccessfulInstall; end;
'@
    $production = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'AvelriGestao-Production.iss'))
    $legalFiles = @($production -split "`r?`n" | Where-Object { $_ -match '^Source:.*(AvelriTermsFile|AvelriThirdPartyNoticesDir)' }) -join "`n"
    Assert-LegalTest (($legalFiles -split "`n").Count -eq 5) 'Expected five actual Production legal file entries.'
    foreach ($case in @(@('A','1.0.1','2','A'),@('A-update','1.0.2','3','A'),@('B','1.0.3','4','B'))) {
        $name=$case[0];$terms=Join-Path $RunRoot ($name+'-terms.txt');$payload=Join-Path $RunRoot ($name+'-payload.txt')
        [IO.File]::WriteAllText($terms,('DOCUMENTO SINTÉTICO SEM EFEITO CONTRATUAL. Termos '+$case[3]+'. Aceite apenas para teste automatizado.'))
        [IO.File]::WriteAllText($payload,('SYNTHETIC_PAYLOAD_'+$case[3]))
        $hash=(Get-FileHash -LiteralPath $terms -Algorithm SHA256).Hash
        if($name -eq 'A'){$state.HashA=$hash;$state.FirstPayloadSHA256=(Get-FileHash -LiteralPath $payload -Algorithm SHA256).Hash}
        if($name -eq 'A-update'){Assert-LegalTest ($hash -ceq $state.HashA) 'Identical synthetic terms changed hash.'}
        if($name -eq 'B'){$state.HashB=$hash}
        $values=@{VERSION=$case[1];BUILD=$case[2];HASH=$hash;REGISTRY=$state.RegistryKey;TERMS=$terms;NOTICES=(Join-Path $repo 'docs\distribution\desktop-1.0.1+2');TEST_APP_ID=$id;TARGET=(Join-Path $RunRoot 'installed');OUT=$RunRoot;NAME=$name;PAYLOAD=$payload;LEGAL_FILES=$legalFiles;POLICY=(Join-Path $PSScriptRoot 'LicenseAcceptance.iss')}
        $text=$template
        foreach($key in $values.Keys){$text=$text.Replace(('{'+$key+'}'),[string]$values[$key])}
        $iss=Join-Path $RunRoot ($name+'.iss');[IO.File]::WriteAllText($iss,$text)
        & $InnoSetupCompiler $iss *> (Join-Path $RunRoot ($name+'-compile.log'))
        Assert-LegalTest ($LASTEXITCODE -eq 0) ('Synthetic Inno compilation failed: '+$name)
    }
    $state | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $RunRoot 'test-state.json') -Encoding utf8
    $code=Run-SyntheticSilent (Join-Path $RunRoot 'A.exe') (Join-Path $RunRoot 'missing-acceptance.log')
    Assert-LegalTest ($code -ne 0) 'Silent initial installation proceeded without acceptance.'
    Assert-LegalTest (-not(Test-Path -LiteralPath (Join-Path $RunRoot 'installed'))) 'Missing acceptance created an app directory.'
    Assert-LegalTest (-not(Test-Path -LiteralPath (Join-Path $RunRoot 'installed.preflight.txt'))) 'Missing acceptance reached prerequisites.'
    Assert-LegalTest (-not(Test-Path -LiteralPath ('HKCU:\'+$state.RegistryKey))) 'Missing acceptance created a receipt.'
    Write-Output 'INITIAL_SILENT_WITHOUT_ACCEPTANCE_BLOCKED=PASS'
    Write-Output ('RUN_ROOT='+$RunRoot)
    Write-Output ('SYNTHETIC_GUI_A='+ (Join-Path $RunRoot 'A.exe'))
    exit
}

$state=Get-Content -LiteralPath (Join-Path $RunRoot 'test-state.json') -Raw|ConvertFrom-Json
switch($Phase){
 'VerifyFirst' {
    $r=Get-ItemProperty -LiteralPath (Receipt '1.0' $state.HashA)
    Assert-LegalTest ($r.Revision -ceq '1.0' -and $r.SHA256 -ceq $state.HashA -and $r.Completed -ceq '1' -and $r.AcceptedAtUtc -match '^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$') 'The successful explicit acceptance receipt is invalid.'
    Assert-OriginalState
    foreach($name in @('AVELRI-LICENSE.pt-BR.txt','THIRD-PARTY-NOTICES.txt','COMPONENT-LICENSES.txt','FLUTTER-ENGINE-LICENSE.txt','inventory.json')){Assert-LegalTest (Test-Path -LiteralPath (Join-Path $RunRoot ('installed\legal\'+$name))) ('Missing installed legal file: '+$name)}
    [IO.File]::WriteAllText((Join-Path $RunRoot 'first-accepted-utc.txt'),$r.AcceptedAtUtc)
    Write-Output 'EXPLICIT_FIRST_ACCEPTANCE_AND_RECEIPT=PASS; ORIGINAL_NOTICES_INSTALLED=PASS'
 }
 'VerifySame' {
    $code=Run-SyntheticSilent (Join-Path $RunRoot 'A-update.exe') (Join-Path $RunRoot 'same-terms-update.log')
    Assert-LegalTest ($code -eq 0) 'Update with identical terms failed.'
    $r=Get-ItemProperty -LiteralPath (Receipt '1.0' $state.HashA)
    Assert-LegalTest ($r.AcceptedAtUtc -ceq [IO.File]::ReadAllText((Join-Path $RunRoot 'first-accepted-utc.txt'))) 'An update invented a new acceptance.'
    Assert-OriginalState
    Write-Output 'IDENTICAL_TERMS_UPDATE_WITHOUT_NEW_ACCEPTANCE=PASS'
 }
 'VerifyChangedBlocked' {
    $before=(Get-Item -LiteralPath (Join-Path $RunRoot 'installed.preflight.txt')).LastWriteTimeUtc
    $code=Run-SyntheticSilent (Join-Path $RunRoot 'B.exe') (Join-Path $RunRoot 'changed-terms-blocked.log')
    Assert-LegalTest ($code -ne 0) 'Changed terms proceeded without a new acceptance.'
    Assert-LegalTest ((Get-Item -LiteralPath (Join-Path $RunRoot 'installed.preflight.txt')).LastWriteTimeUtc -eq $before) 'Changed terms reached prerequisites.'
    Assert-LegalTest (-not(Has-Receipt '1.0' $state.HashB)) 'Changed terms gained a false receipt.'
    Assert-OriginalState
    Write-Output 'CHANGED_SHA256_WITHOUT_ACCEPTANCE_BLOCKED=PASS'
 }
 'VerifyDeclined' {
    Assert-LegalTest (-not(Has-Receipt '1.0' $state.HashB)) 'A declined/cancelled update created a receipt.'
    Assert-OriginalState
    Write-Output 'DECLINED_CHANGED_TERMS_PRESERVE_INSTALLATION=PASS'
 }
 'VerifyChangedAccepted' {
    $r=Get-ItemProperty -LiteralPath (Receipt '1.0' $state.HashB)
    Assert-LegalTest ($r.SHA256 -ceq $state.HashB -and $r.Completed -ceq '1') 'Changed terms receipt missing.'
    Assert-LegalTest (Has-Receipt '1.0' $state.HashA) 'Previous receipt was discarded.'
    Assert-LegalTest ([IO.File]::ReadAllText((Join-Path $RunRoot 'installed\payload.txt')) -ceq 'SYNTHETIC_PAYLOAD_B') 'Accepted synthetic update did not install.'
    Write-Output 'NEW_EXPLICIT_ACCEPTANCE_AND_HISTORY=PASS'
 }
 'VerifyIncompleteReceipt' {
    $receipt=Receipt '1.0' $state.HashB
    $before=(Get-Item -LiteralPath (Join-Path $RunRoot 'installed.preflight.txt')).LastWriteTimeUtc
    $original=Get-ItemProperty -LiteralPath $receipt -ErrorAction Stop
    Assert-LegalTest ($original.Completed -ceq '1') 'Expected completed synthetic B receipt.'
    try {
        Set-ItemProperty -LiteralPath $receipt -Name Completed -Value '0'
        $code=Run-SyntheticSilent (Join-Path $RunRoot 'B.exe') (Join-Path $RunRoot 'incomplete-receipt-blocked.log')
        Assert-LegalTest ($code -ne 0) 'Incomplete receipt bypassed mandatory acceptance.'
        Assert-LegalTest ((Get-Item -LiteralPath (Join-Path $RunRoot 'installed.preflight.txt')).LastWriteTimeUtc -eq $before) 'Incomplete receipt reached prerequisites.'
        Assert-LegalTest ([IO.File]::ReadAllText((Join-Path $RunRoot 'installed\payload.txt')) -ceq 'SYNTHETIC_PAYLOAD_B') 'Incomplete receipt changed installed payload.'
    } finally { Set-ItemProperty -LiteralPath $receipt -Name Completed -Value $original.Completed }
    Write-Output 'INCOMPLETE_RECEIPT_BLOCKED_BEFORE_PREREQUISITES=PASS'
 }
}
