[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$VCRedistPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expectedVersion = '14.52.37029.0'
$expectedHash = '9C02C9ED120F671B900E6B9471D890E3B2414117A7E53F4AE2E855466FA2B7C9'
$expectedProductionAppId = 'B46B7D1A-6E54-4C0A-A1E2-5F41C6D92837'
$expectedHomologationAppId = 'D68C6C5D-2D87-4CBA-9918-3AD08C0D1C69'
$productionPath = Join-Path $PSScriptRoot 'AvelriGestao-Production.iss'
$homologationPath = Join-Path $PSScriptRoot 'AvelriGestao-Homologacao.iss'
$helperPath = Join-Path $PSScriptRoot 'Install-VCRedistElevated.ps1'
$termsPath = Join-Path $PSScriptRoot 'MicrosoftVCRedist-Redistribution-Conditions.md'

foreach ($requiredPath in @($productionPath, $homologationPath, $helperPath, $termsPath, $VCRedistPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw ('Required packaging input is missing: ' + $requiredPath)
    }
}

$redist = Get-Item -LiteralPath $VCRedistPath
$actualHash = (Get-FileHash -LiteralPath $redist.FullName -Algorithm SHA256).Hash
$signature = Get-AuthenticodeSignature -LiteralPath $redist.FullName
$signer = if ($null -ne $signature.SignerCertificate) {
    $signature.SignerCertificate.GetNameInfo(
        [Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)
} else { '' }
if ($redist.Length -le 0 -or
    $redist.VersionInfo.FileVersion -cne $expectedVersion -or
    $actualHash -cne $expectedHash -or
    $signature.Status.ToString() -cne 'Valid' -or
    $signer -cne 'Microsoft Corporation') {
    throw 'The offline Microsoft x64 Redistributable failed the pinned size, version, SHA-256, or Authenticode check.'
}

$production = [IO.File]::ReadAllText($productionPath)
$homologation = [IO.File]::ReadAllText($homologationPath)
$helper = [IO.File]::ReadAllText($helperPath)
$terms = [IO.File]::ReadAllText($termsPath)
$installerSpecs = @(
    @{ Name = 'Production'; Text = $production; AppId = $expectedProductionAppId },
    @{ Name = 'Homologation'; Text = $homologation; AppId = $expectedHomologationAppId }
)

foreach ($spec in $installerSpecs) {
    $text = [string]$spec.Text
    if ($text -notmatch '(?m)^#define VCRedistMajor 14\s*$' -or
        $text -notmatch '(?m)^#define VCRedistMinor 52\s*$' -or
        $text -notmatch '(?m)^#define VCRedistBuild 37029\s*$' -or
        $text -notmatch '(?m)^#define VCRedistRevision 0\s*$' -or
        $text -notmatch ('(?m)^#define VCRedistSha256 "' + $expectedHash + '"\s*$')) {
        throw ($spec.Name + ' script does not pin the approved runtime version and hash.')
    }
    $appIdMatch = [regex]::Match($text, '(?m)^AppId=\{\{(?<id>[0-9A-Fa-f-]{36})\}')
    if (-not $appIdMatch.Success -or $appIdMatch.Groups['id'].Value -cne $spec.AppId) {
        throw ($spec.Name + ' installer identity is absent or differs from its approved AppId.')
    }
    if ($text -notmatch '(?m)^PrivilegesRequired=lowest\s*$' -or
        $text -match '(?m)^\[(Run|UninstallDelete)\]\s*$' -or
        $text -notmatch '(?m)^Source: "\{#VCRedistPath\}"; DestName: "vc_redist\.x64\.exe"; Flags: dontcopy noencryption\s*$' -or
        $text -notmatch '(?m)^Source: "Install-VCRedistElevated\.ps1"; Flags: dontcopy noencryption\s*$' -or
        $text -notmatch '(?m)^function PrepareToInstall\(' -or
        $text -notmatch 'HasRequiredVCRuntime' -or
        $text -notmatch 'GetSHA256OfFile\(RedistPath\)' -or
        $text -notmatch '(?m)^#if MicrosoftRedistributionRightsConfirmed != 1\s*$') {
        throw ($spec.Name + ' installer does not include the guarded per-user prerequisite package flow.')
    }
}

$productionCode = [regex]::Match($production, '(?ms)^\[Code\]\r?\n(?<body>.*)$')
$homologationCode = [regex]::Match($homologation, '(?ms)^\[Code\]\r?\n(?<body>.*)$')
if (-not $productionCode.Success -or -not $homologationCode.Success -or
    ($productionCode.Groups['body'].Value -replace "`r`n", "`n") -cne
    ($homologationCode.Groups['body'].Value -replace "`r`n", "`n")) {
    throw 'Production and Homologation must use identical Visual C++ detection, UAC, error, and restart handling.'
}

if ($production -match '(?m)^AppId=\{\{' + [regex]::Escape($expectedHomologationAppId) -or
    $homologation -match '(?m)^AppId=\{\{' + [regex]::Escape($expectedProductionAppId)) {
    throw 'Production and Homologation identities are crossed.'
}
if ($helper -notmatch '(?m)-Verb RunAs' -or
    $helper -notmatch "'/install', '/passive', '/norestart'" -or
    $terms -notmatch 'Authenticode' -or
    $terms -notmatch 'individual developer' -or
    $terms -notmatch 'Insiders' -or
    $terms -notmatch 'pre-release' -or
    $terms -notmatch 'unmodified') {
    throw 'UAC helper or redistribution checklist is incomplete.'
}

Write-Output 'VCREDIST_SOURCE_PACKAGE_CHECK=PASS'
Write-Output ('VCRedistVersion=' + $expectedVersion)
Write-Output ('VCRedistSHA256=' + $actualHash)
Write-Output ('VCRedistAuthenticode=' + $signature.Status + ' (' + $signer + ')')
Write-Output ('ProductionAppId=' + $expectedProductionAppId)
Write-Output ('HomologationAppId=' + $expectedHomologationAppId)
Write-Output 'PrerequisitePayload=Present in both Inno Setup source scripts'
Write-Output 'InstallerCompilation=Not checked by this source-package preflight'
