[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseDirectory,
    [Parameter(Mandatory = $true)]
    [string]$BuildMetadata,
    [Parameter(Mandatory = $true)]
    [string]$VCRedistPath,
    [string]$InnoSetupCompiler,
    [switch]$MicrosoftRedistributionRightsConfirmed
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expected = @{
    APP_ENV = 'development'
    API_BASE_URL = 'http://127.0.0.1:5139'
    AVELRI_UPDATES_ENABLED = 'true'
    AVELRI_UPDATE_APP_ID = 'D68C6C5D-2D87-4CBA-9918-3AD08C0D1C69'
    AVELRI_UPDATE_CHANNEL = 'homologation'
    AVELRI_UPDATE_MANIFEST_URL = 'https://localhost:8443/updates/homologation/release.json'
    AVELRI_UPDATE_SIGNATURE_URL = 'https://localhost:8443/updates/homologation/release.sig'
    AVELRI_UPDATE_DOWNLOAD_ORIGIN = 'https://localhost:8443'
}
$installerScript = Join-Path $PSScriptRoot 'AvelriGestao-Homologacao.iss'
$helperPath = Join-Path $PSScriptRoot 'Install-VCRedistElevated.ps1'
$preflightScript = Join-Path $PSScriptRoot 'Test-VCRedistPackaging.ps1'

if (-not $MicrosoftRedistributionRightsConfirmed) {
    throw 'Review MicrosoftVCRedist-Redistribution-Conditions.md and confirm licensed Visual Studio use and redistribution rights for the exact edition/channel before compiling.'
}
foreach ($path in @($installerScript, $helperPath, $preflightScript, $BuildMetadata,
        (Join-Path $ReleaseDirectory 'avelri_gestao.exe'),
        (Join-Path $ReleaseDirectory 'data\flutter_assets'), $VCRedistPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw ('Required synthetic homologation input is missing: ' + $path)
    }
}

if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler)) {
    $knownCompiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
    if (Test-Path -LiteralPath $knownCompiler -PathType Leaf) {
        $InnoSetupCompiler = $knownCompiler
    } else {
        $InnoSetupCompiler = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
    }
}
if (-not (Test-Path -LiteralPath $InnoSetupCompiler -PathType Leaf)) {
    throw 'Inno Setup 6 ISCC.exe is unavailable. No tool will be installed automatically.'
}

$metadata = @{}
foreach ($line in [IO.File]::ReadAllLines($BuildMetadata)) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $separator = $line.IndexOf('=')
    if ($separator -le 0) { throw 'Synthetic build metadata must use one Key=Value entry per line.' }
    $key = $line.Substring(0, $separator).Trim()
    $value = $line.Substring($separator + 1).Trim()
    if ($metadata.ContainsKey($key)) { throw ('Duplicate build metadata key: ' + $key) }
    $metadata[$key] = $value
}
foreach ($key in $expected.Keys) {
    if (-not $metadata.ContainsKey($key) -or [string]$metadata[$key] -cne $expected[$key]) {
        throw ('Synthetic build metadata is missing or mismatches required isolation setting: ' + $key)
    }
}
foreach ($key in @('AVELRI_UPDATE_KEY_ID', 'AVELRI_UPDATE_PUBLIC_KEY_BASE64',
        'Version', 'BuildNumber', 'CommitShort', 'DesktopExeSHA256')) {
    if (-not $metadata.ContainsKey($key) -or [string]::IsNullOrWhiteSpace([string]$metadata[$key])) {
        throw ('Synthetic build metadata is missing required provenance: ' + $key)
    }
}
$allowedMetadataKeys = @($expected.Keys) + @(
    'AVELRI_UPDATE_KEY_ID', 'AVELRI_UPDATE_PUBLIC_KEY_BASE64',
    'Version', 'BuildNumber', 'CommitShort', 'DesktopExeSHA256')
$unexpectedKeys = @($metadata.Keys | Where-Object { $_ -notin $allowedMetadataKeys })
if ($unexpectedKeys.Count -gt 0) {
    throw ('Unexpected build metadata could introduce an unreviewed setting: ' + ($unexpectedKeys -join ', '))
}
if ([string]$metadata['Version'] -notmatch '^\d+\.\d+\.\d+$' -or
    [string]$metadata['BuildNumber'] -notmatch '^\d+$' -or
    [string]$metadata['CommitShort'] -notmatch '^[0-9a-fA-F]{8}$' -or
    [string]$metadata['DesktopExeSHA256'] -notmatch '^[0-9A-Fa-f]{64}$') {
    throw 'Synthetic build provenance contains an invalid version, commit, or executable hash.'
}
if ([string]$metadata['AVELRI_UPDATE_KEY_ID'] -notmatch '^(?i)(?=.*(?:test|synthetic))[A-Za-z0-9._-]{1,64}$') {
    throw 'The Homologation installer requires a visibly synthetic updater key ID.'
}
try {
    $publicKey = [Convert]::FromBase64String([string]$metadata['AVELRI_UPDATE_PUBLIC_KEY_BASE64'])
} catch {
    throw 'The synthetic updater public key is not valid Base64.'
}
if ($publicKey.Length -ne 32 -or
    [Convert]::ToBase64String($publicKey) -cne [string]$metadata['AVELRI_UPDATE_PUBLIC_KEY_BASE64']) {
    throw 'The synthetic updater public key must be one canonical Ed25519 public key (32 bytes).'
}
$releaseExe = Join-Path $ReleaseDirectory 'avelri_gestao.exe'
$releaseExeHash = (Get-FileHash -LiteralPath $releaseExe -Algorithm SHA256).Hash
$releaseVersion = (Get-Item -LiteralPath $releaseExe).VersionInfo.ProductVersion
if ($releaseExeHash -cne [string]$metadata['DesktopExeSHA256'] -or
    $releaseVersion -cne ([string]$metadata['Version'] + '+' + [string]$metadata['BuildNumber'])) {
    throw 'Prebuilt Desktop Release does not match the synthetic metadata hash and version.'
}

$powerShellExecutable = Join-Path $PSHOME 'powershell.exe'
& $powerShellExecutable -NoProfile -NonInteractive -ExecutionPolicy Bypass `
    -File $preflightScript -VCRedistPath $VCRedistPath
if ($LASTEXITCODE -ne 0) { throw 'Visual C++ prerequisite source-package preflight failed.' }

$commitShort = [string]$metadata['CommitShort']
$appVersion = [string]$metadata['Version']
$buildNumber = [string]$metadata['BuildNumber']
$tempRoot = [IO.Path]::GetFullPath($env:TEMP)
$outputRoot = Join-Path $tempRoot ('issue69-homologation-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$outputItem = Get-Item -LiteralPath $outputRoot -Force
if (($outputItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'Isolated output directory must not be a reparse point.'
}
$installerOutput = Join-Path $outputRoot 'installer'
New-Item -ItemType Directory -Path $installerOutput | Out-Null
$compilerLog = Join-Path $outputRoot 'inno-compile.log'

& $InnoSetupCompiler `
    ('/DAppSource=' + [IO.Path]::GetFullPath($ReleaseDirectory)) `
    ('/DBuildMetadata=' + [IO.Path]::GetFullPath($BuildMetadata)) `
    ('/DInstallerOutput=' + $installerOutput) `
    ('/DAppVersion=' + $appVersion) `
    ('/DBuildNumber=' + $buildNumber) `
    ('/DCommitShort=' + $commitShort) `
    ('/DVCRedistPath=' + [IO.Path]::GetFullPath($VCRedistPath)) `
    '/DMicrosoftRedistributionRightsConfirmed=1' `
    $installerScript *> $compilerLog
$compileExitCode = $LASTEXITCODE
if ($compileExitCode -ne 0) {
    throw ('Homologation installer compilation failed with exit code ' + $compileExitCode + '; log: ' + $compilerLog)
}

$candidate = Join-Path $installerOutput ('AvelriGestao-Homologacao-' + $appVersion + '+' +
    $buildNumber + '-' + $commitShort + '.exe')
if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
    throw ('ISCC returned success but did not produce the expected candidate: ' + $candidate)
}
$candidateHash = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
[IO.File]::WriteAllText((Join-Path $outputRoot 'candidate.sha256.txt'),
    ($candidateHash + '  ' + $candidate + "`r`n"), (New-Object Text.UTF8Encoding($false)))
Write-Output 'HOMOLOGATION_CANDIDATE=PASS'
Write-Output ('AppId=' + $expected.AVELRI_UPDATE_APP_ID)
Write-Output 'APP_ENV=development'
Write-Output ('API_BASE_URL=' + $expected.API_BASE_URL)
Write-Output ('UpdateOrigin=' + $expected.AVELRI_UPDATE_DOWNLOAD_ORIGIN)
Write-Output ('Installer=' + $candidate)
Write-Output ('SHA256=' + $candidateHash)
Write-Output ('Logs=' + $outputRoot)
