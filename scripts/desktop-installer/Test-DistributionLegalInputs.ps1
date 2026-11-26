[CmdletBinding()]
param(
    [string]$RunRoot,
    [string]$InnoSetupCompiler = (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $RunRoot) { $RunRoot = Join-Path $repo ('.test-output\issue69-legal-gates-' + [guid]::NewGuid().ToString('N').Substring(0,8)) }
if (Test-Path -LiteralPath $RunRoot) { throw 'Preserve existing test evidence; choose a new directory.' }
New-Item -ItemType Directory -Path $RunRoot | Out-Null
Import-Module (Join-Path $PSScriptRoot 'DistributionLegal.psm1') -Force

# Every approval in this fixture concerns this nonbinding synthetic text only.
# No Microsoft attestation, Production payload, or executable is generated.
$terms = Join-Path $RunRoot 'synthetic.txt'
[IO.File]::WriteAllText($terms, 'DOCUMENTO SINTETICO SEM EFEITO CONTRATUAL. TESTE DE PREPROCESSAMENTO.')
$termsHash = (Get-FileHash -LiteralPath $terms -Algorithm SHA256).Hash
$noticeHashes = @{}
foreach ($name in @('THIRD-PARTY-NOTICES.txt','COMPONENT-LICENSES.txt','FLUTTER-ENGINE-LICENSE.txt')) {
    [IO.File]::WriteAllText((Join-Path $RunRoot $name), ('SYNTHETIC NOTICE '+$name))
    $noticeHashes[$name] = (Get-FileHash -LiteralPath (Join-Path $RunRoot $name) -Algorithm SHA256).Hash
}
@{ source_commit=('f'*40); generated_notice_files=$noticeHashes } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $RunRoot 'inventory.json') -Encoding utf8
$approval = [ordered]@{
    schemaVersion=1;status='APPROVED';commercialUseApproved=$true
    licensorIdentityCompleted=$true;technicalValidityApproved=$true
    revision='1.0';language='pt-BR';termsFile='synthetic.txt';termsSha256=$termsHash
    thirdPartyNoticesDirectory='.'
}
$approvalPath = Join-Path $RunRoot 'SYNTHETIC-approval.json'
function Write-SyntheticApproval { $approval | ConvertTo-Json | Set-Content -LiteralPath $approvalPath -Encoding utf8 }
function Expect-Rejection([string]$Case,[string]$Expected) {
    $message=''
    try { Get-AvelriApprovedDistributionLegalInputs -ApprovalPath $approvalPath | Out-Null }
    catch { $message=$_.Exception.Message }
    if (-not $message.Contains($Expected)) { throw "$Case failed: $message" }
    Write-Output ($Case+'=PASS')
}
Write-SyntheticApproval
$value = Get-AvelriApprovedDistributionLegalInputs -ApprovalPath $approvalPath
if ($value.SHA256 -cne $termsHash -or $value.NoticesSourceCommit -cne ('f'*40)) { throw 'Synthetic approval was not resolved correctly.' }
Write-Output 'SYNTHETIC_APPROVAL_INPUTS=PASS'
$approval.termsFile=$terms
$approval.thirdPartyNoticesDirectory=$RunRoot
Write-SyntheticApproval
$absoluteValue=Get-AvelriApprovedDistributionLegalInputs -ApprovalPath $approvalPath
if ($absoluteValue.TermsPath -cne [IO.Path]::GetFullPath($terms) -or
    $absoluteValue.NoticesDirectory -cne [IO.Path]::GetFullPath($RunRoot)) { throw 'Absolute private input paths were not resolved independently of the approval directory.' }
Write-Output 'SYNTHETIC_ABSOLUTE_PRIVATE_PATHS=PASS'
$approval.termsFile='synthetic.txt'
$approval.thirdPartyNoticesDirectory='.'
$approval.status='DRAFT';Write-SyntheticApproval
Expect-Rejection 'DRAFT_STATUS_REJECTED' 'draft commercial use is blocked'
$approval.status='APPROVED';$approval.technicalValidityApproved=$false;Write-SyntheticApproval
Expect-Rejection 'UNAPPROVED_VALIDITY_REJECTED' 'draft commercial use is blocked'
$approval.technicalValidityApproved=$true;$approval.termsSha256=('0'*64);Write-SyntheticApproval
Expect-Rejection 'MODIFIED_TERMS_REJECTED' 'hash does not match'
$approval.termsSha256=$termsHash;Write-SyntheticApproval
[IO.File]::AppendAllText($terms, ' AVELRI_LEGAL_DRAFT_DO_NOT_DISTRIBUTE')
$approval.termsSha256=(Get-FileHash -LiteralPath $terms -Algorithm SHA256).Hash;Write-SyntheticApproval
Expect-Rejection 'DRAFT_MARKER_REJECTED' 'Draft markers'
[IO.File]::WriteAllText($terms, 'DOCUMENTO SINTETICO SEM EFEITO CONTRATUAL. TESTE DE PREPROCESSAMENTO.')
$approval.termsSha256=$termsHash;Write-SyntheticApproval
[IO.File]::AppendAllText((Join-Path $RunRoot 'COMPONENT-LICENSES.txt'), ' TAMPERED')
Expect-Rejection 'MODIFIED_SYNTHETIC_NOTICE_REJECTED' 'original third-party notice'

# Deliberate #error after the real include proves the preprocessor gate reached
# the next statement. These fixtures cannot produce an installer.
$template = @'
#define AvelriTermsApproved "1"
#define AvelriTermsRevision "1.0"
#define AvelriTermsSha256 "{HASH}"
#define AvelriTermsFile "{TERMS}"
#define AvelriThirdPartyNoticesDir "{NOTICES}"
#include "{GATE}"
#error SYNTHETIC_GATE_REACHED_NO_INSTALLER
'@
foreach($case in @('valid','wrong-hash','draft-marker')) {
    $caseTerms=Join-Path $RunRoot ($case+'.txt')
    $text='SYNTHETIC NONBINDING PREPROCESSOR TEST ONLY.'
    if($case -eq 'draft-marker'){$text+=' AVELRI_LEGAL_DRAFT_DO_NOT_DISTRIBUTE'}
    [IO.File]::WriteAllText($caseTerms,$text)
    $hash=(Get-FileHash -LiteralPath $caseTerms -Algorithm SHA256).Hash
    if($case -eq 'wrong-hash'){$hash='0'*64}
    $fixture=$template.Replace('{HASH}',$hash).Replace('{TERMS}',$caseTerms).Replace('{NOTICES}',$RunRoot).Replace('{GATE}',(Join-Path $PSScriptRoot 'ProductionLegalInputs.iss'))
    $iss=Join-Path $RunRoot ($case+'.iss');$log=Join-Path $RunRoot ($case+'.log')
    [IO.File]::WriteAllText($iss,$fixture)
    & $InnoSetupCompiler $iss *> $log
    $output=[IO.File]::ReadAllText($log)
    $expected=switch($case){'valid'{'SYNTHETIC_GATE_REACHED_NO_INSTALLER'} 'wrong-hash'{'Approved terms SHA-256 does not match'} 'draft-marker'{'Draft commercial use is blocked even when'}}
    if($LASTEXITCODE -eq 0 -or -not $output.Contains($expected)){throw "Preprocessor case $case failed; see $log"}
    Write-Output ('INNO_LEGAL_GATE_'+$case.ToUpperInvariant().Replace('-','_')+'=PASS')
}
if (@(Get-ChildItem -LiteralPath $RunRoot -Recurse -Filter '*.exe').Count -ne 0) { throw 'A gate fixture unexpectedly produced an executable.' }
Write-Output ('RUN_ROOT='+$RunRoot)
