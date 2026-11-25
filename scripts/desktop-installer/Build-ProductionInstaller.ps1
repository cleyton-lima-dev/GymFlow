# Supply an unmodified x64 package from https://aka.ms/vc14/vc_redist.x64.exe.
# Bundling it requires licensed Visual Studio redistribution rights; the caller
# must explicitly confirm those rights. The pinned hash/version are in the .iss.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$SourceCommit,
    [string]$FlutterExecutable = 'C:\flutter\bin\flutter.bat',
    [string]$InnoSetupCompiler,
    [Parameter(Mandatory = $true)]
    [string]$VCRedistPath,

    [Parameter(Mandatory = $true)]
    [string]$AccessAgentPackagePath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$ExpectedAccessAgentPackageSha256,

    [switch]$MicrosoftRedistributionRightsConfirmed
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'ProductionToolchain.psm1') -Force
$visualStudioInstance = Resolve-AvelriStableVisualStudio
$stableToolchain = Get-AvelriStableToolchain -VisualStudioInstance $visualStudioInstance
$cmakeEnvironment = Get-AvelriPinnedCMakeEnvironment -Toolchain $stableToolchain

if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler)) {
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        throw 'LOCALAPPDATA is unavailable; specify the installed Inno Setup compiler path explicitly.'
    }
    $InnoSetupCompiler = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
}

$expectedApiBaseUrl = 'https://gymflow-api-prod.fly.dev'
$expectedProductionAppId = 'B46B7D1A-6E54-4C0A-A1E2-5F41C6D92837'
$expectedMsvcToolsetVersion = '14.50.35717'
$expectedMsvcCompilerVersion = '19.50.35740.0'
$expectedMsvcCompilerPath = $stableToolchain.CompilerPath
$updateDownloadOrigin = 'https://avelri-gestao-downloads.pages.dev'
$updateManifestUrl = $updateDownloadOrigin + '/updates/production/release.json'
$updateSignatureUrl = $updateDownloadOrigin + '/updates/production/release.sig'
$updateKeyId = 'avelri-gestao-prod-20261114-v1'
$updatePublicKeyBase64 = 'EssnxwxKSm9+zqKPWSOcc0bxZrxNtMIJ4CDt0BUaFkE='
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$productionIss = Join-Path $PSScriptRoot 'AvelriGestao-Production.iss'
$homologationIss = Join-Path $PSScriptRoot 'AvelriGestao-Homologacao.iss'
$vcRedistHelper = Join-Path $PSScriptRoot 'Install-VCRedistElevated.ps1'
$redistributionTermsDoc = Join-Path $PSScriptRoot 'MicrosoftVCRedist-Redistribution-Conditions.md'
$accessAgentProvisioner = Join-Path $repoRoot 'scripts\access-agent\Provision-AccessAgentFromBundle.ps1'
$accessAgentPairingAction = Join-Path $repoRoot 'scripts\access-agent\Pair-AccessAgent.ps1'

if ($SourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'SourceCommit must be an explicit full lowercase Git commit SHA.'
}
if (-not (Test-Path -LiteralPath $productionIss -PathType Leaf)) {
    throw 'Production Inno Setup script is missing.'
}
if (-not (Test-Path -LiteralPath $homologationIss -PathType Leaf)) {
    throw 'Homologation Inno Setup script is missing; Production AppId uniqueness cannot be confirmed.'
}
if (-not (Test-Path -LiteralPath $vcRedistHelper -PathType Leaf)) {
    throw 'The Visual C++ prerequisite elevation helper is missing.'
}
if (-not (Test-Path -LiteralPath $redistributionTermsDoc -PathType Leaf)) {
    throw 'The Microsoft Visual C++ redistribution conditions checklist is missing.'
}
foreach ($agentScript in @($accessAgentProvisioner, $accessAgentPairingAction)) {
    if (-not (Test-Path -LiteralPath $agentScript -PathType Leaf)) {
        throw 'An AccessAgent installation or pairing helper is missing.'
    }
}
if (-not $MicrosoftRedistributionRightsConfirmed) {
    throw 'Confirm licensed Visual Studio use and redistribution rights for the exact edition/channel (including Insiders, if applicable) before bundling the Microsoft Visual C++ Redistributable.'
}
if (-not (Test-Path -LiteralPath $VCRedistPath -PathType Leaf)) {
    throw 'The explicitly supplied offline Microsoft Visual C++ x64 Redistributable is missing.'
}
if (-not (Test-Path -LiteralPath $AccessAgentPackagePath -PathType Leaf)) {
    throw 'The validated self-contained win-x64 AccessAgent package is missing.'
}
$AccessAgentPackagePath = (Get-Item -LiteralPath $AccessAgentPackagePath).FullName
$accessAgentPackageHash = (Get-FileHash -LiteralPath $AccessAgentPackagePath -Algorithm SHA256).Hash
if ($accessAgentPackageHash -ine $ExpectedAccessAgentPackageSha256) {
    throw 'The AccessAgent package does not match its independently recorded SHA-256.'
}
if (-not (Test-Path -LiteralPath $FlutterExecutable -PathType Leaf)) {
    throw 'The explicitly configured Flutter launcher is unavailable.'
}
if (-not (Test-Path -LiteralPath $InnoSetupCompiler -PathType Leaf)) {
    throw 'The explicitly configured Inno Setup compiler is unavailable.'
}
Assert-AvelriCompilerIdentity `
    -ExpectedCompilerPath $expectedMsvcCompilerPath `
    -ActualCompilerPath $stableToolchain.CompilerPath `
    -ExpectedCompilerVersion $expectedMsvcCompilerVersion `
    -ActualCompilerVersion $stableToolchain.CompilerVersion `
    -ExpectedToolsetVersion $expectedMsvcToolsetVersion `
    -ActualToolsetVersion $stableToolchain.ToolsetVersion
$productionIssText = [IO.File]::ReadAllText($productionIss)
$homologationIssText = [IO.File]::ReadAllText($homologationIss)
$vcRedistVersionParts = @('Major', 'Minor', 'Build', 'Revision') | ForEach-Object {
    $partMatch = [regex]::Match($productionIssText, '(?m)^#define VCRedist' + $_ + '\s+(?<value>\d+)\s*$')
    if (-not $partMatch.Success) { throw ('Production installer has no pinned VCRedist' + $_ + ' version component.') }
    [int]$partMatch.Groups['value'].Value
}
$homologationVersionParts = @('Major', 'Minor', 'Build', 'Revision') | ForEach-Object {
    $partMatch = [regex]::Match($homologationIssText, '(?m)^#define VCRedist' + $_ + '\s+(?<value>\d+)\s*$')
    if (-not $partMatch.Success) { throw ('Homologation installer has no pinned VCRedist' + $_ + ' version component.') }
    [int]$partMatch.Groups['value'].Value
}
$vcRedistHashMatch = [regex]::Match($productionIssText, '(?m)^#define VCRedistSha256 "(?<value>[0-9A-Fa-f]{64})"\s*$')
$homologationHashMatch = [regex]::Match($homologationIssText, '(?m)^#define VCRedistSha256 "(?<value>[0-9A-Fa-f]{64})"\s*$')
if (-not $vcRedistHashMatch.Success) {
    throw 'The Production installer must pin the exact Visual C++ Redistributable SHA-256.'
}
$vcRedistVersion = [string]::Join('.', $vcRedistVersionParts)
$vcRedistExpectedHash = $vcRedistHashMatch.Groups['value'].Value
if ([string]::Join('.', $homologationVersionParts) -cne $vcRedistVersion -or
    -not $homologationHashMatch.Success -or
    $homologationHashMatch.Groups['value'].Value -cne $vcRedistExpectedHash -or
    $vcRedistVersion -cne '14.52.37029.0' -or
    $vcRedistExpectedHash -cne '9C02C9ED120F671B900E6B9471D890E3B2414117A7E53F4AE2E855466FA2B7C9') {
    throw 'Production and Homologation must both pin the approved Microsoft Visual C++ x64 14.52.37029.0 package.'
}
foreach ($installerScript in @($productionIssText, $homologationIssText)) {
    if ($installerScript -notmatch '(?m)^PrivilegesRequired=lowest\s*$' -or
        $installerScript -notmatch '(?m)^Source: "\{#VCRedistPath\}"; DestName: "vc_redist\.x64\.exe"; Flags: dontcopy noencryption\s*$' -or
        $installerScript -notmatch '(?m)^Source: "Install-VCRedistElevated\.ps1"; Flags: dontcopy noencryption\s*$' -or
        $installerScript -notmatch '(?m)^function PrepareToInstall\(' -or
        $installerScript -notmatch 'HasRequiredVCRuntime' -or
        $installerScript -notmatch 'ExtractTemporaryFile\(' -or
        $installerScript -notmatch 'GetSHA256OfFile\(RedistPath\)') {
        throw 'Both installer identities must carry the same fail-closed, per-user Visual C++ prerequisite flow.'
    }
    if ($installerScript -notmatch '(?m)^#if MicrosoftRedistributionRightsConfirmed != "1"\s*$') {
        throw 'Each installer requires a compile-time legal-redistribution attestation.'
    }
}
$vcRedistItem = Get-Item -LiteralPath $VCRedistPath
$VCRedistPath = $vcRedistItem.FullName
$vcRedistActualHash = (Get-FileHash -LiteralPath $VCRedistPath -Algorithm SHA256).Hash
$vcRedistSignature = Get-AuthenticodeSignature -LiteralPath $VCRedistPath
if ($vcRedistItem.Length -eq 0 -or
    $vcRedistActualHash -cne $vcRedistExpectedHash -or
    $vcRedistItem.VersionInfo.FileVersion -cne $vcRedistVersion -or
    $vcRedistSignature.Status.ToString() -cne 'Valid' -or
    $null -eq $vcRedistSignature.SignerCertificate -or
    $vcRedistSignature.SignerCertificate.GetNameInfo(
        [Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false) -cne 'Microsoft Corporation') {
    throw 'The Visual C++ Redistributable failed pinned SHA-256, version, or Microsoft Authenticode verification.'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$agentArchive = [IO.Compression.ZipFile]::OpenRead($AccessAgentPackagePath)
try {
    $agentEntries = @($agentArchive.Entries)
    $agentManifestEntry = $agentArchive.GetEntry('SHA256SUMS.txt')
    $agentBuildInfoEntry = $agentArchive.GetEntry('BUILD-INFO.txt')
    $agentSettingsEntry = $agentArchive.GetEntry('appsettings.json')
    if ($null -eq $agentManifestEntry -or $null -eq $agentBuildInfoEntry -or $null -eq $agentSettingsEntry -or
        $null -ne $agentArchive.GetEntry('appsettings.Development.json')) {
        throw 'The AccessAgent archive lacks its manifest or operational files, or includes Development settings.'
    }

    $manifestReader = [IO.StreamReader]::new($agentManifestEntry.Open())
    try { $agentManifestLines = @($manifestReader.ReadToEnd() -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) }
    finally { $manifestReader.Dispose() }
    if ($agentManifestLines.Count -eq 0 -or $agentEntries.Count -ne ($agentManifestLines.Count + 1)) {
        throw 'The AccessAgent archive file list differs from its SHA-256 manifest.'
    }

    $manifestPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($line in $agentManifestLines) {
        if ($line -notmatch '^([0-9A-Fa-f]{64})\s{2,}(.+)$') { throw 'The AccessAgent package manifest is malformed.' }
        $expectedHash = $Matches[1]
        $relativePath = $Matches[2].Trim().Replace('\', '/')
        if ([string]::IsNullOrWhiteSpace($relativePath) -or $relativePath.StartsWith('/') -or
            $relativePath -match '(^|/)\.\.(/|$)' -or -not $manifestPaths.Add($relativePath)) {
            throw 'The AccessAgent package manifest contains an unsafe or duplicate path.'
        }
        $entry = $agentArchive.GetEntry($relativePath)
        if ($null -eq $entry -or $entry.FullName.TrimEnd('/') -cne $relativePath) {
            throw 'An AccessAgent file listed in the manifest is absent from the archive.'
        }
        $entryStream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $actualHash = [BitConverter]::ToString($sha.ComputeHash($entryStream)).Replace('-', '') }
        finally { $sha.Dispose(); $entryStream.Dispose() }
        if ($actualHash -ine $expectedHash) { throw 'An AccessAgent archive entry failed its SHA-256 check.' }
    }

    $buildInfoReader = [IO.StreamReader]::new($agentBuildInfoEntry.Open())
    try { $agentBuildInfo = $buildInfoReader.ReadToEnd() }
    finally { $buildInfoReader.Dispose() }
    $agentVersionMatch = [regex]::Match($agentBuildInfo, '(?m)^AssemblyFileVersion=(?<value>\d+\.\d+\.\d+\.\d+)\s*$')
    if (-not $agentVersionMatch.Success -or
        $agentBuildInfo -notmatch '(?m)^TargetFramework=net10\.0\s*$' -or
        $agentBuildInfo -notmatch '(?m)^RuntimeIdentifier=win-x64\s*$' -or
        $agentBuildInfo -notmatch '(?m)^Deployment=self-contained\s*$' -or
        $agentBuildInfo -notmatch '(?m)^ApiBaseUrl=specified at installation; not embedded\s*$') {
        throw 'The bundled AccessAgent is not the expected self-contained win-x64 production package.'
    }

    $settingsReader = [IO.StreamReader]::new($agentSettingsEntry.Open())
    try { $agentSettings = $settingsReader.ReadToEnd() | ConvertFrom-Json }
    finally { $settingsReader.Dispose() }
    if ($agentSettings.Devices.ToletusLiteNet2.EnablePhysicalPassageConfirmation -ne $false) {
        throw 'The bundled AccessAgent must keep physical passage confirmation disabled by default.'
    }

    $accessAgentVersion = $agentVersionMatch.Groups['value'].Value
    $accessAgentPackageBytes = (Get-Item -LiteralPath $AccessAgentPackagePath).Length
}
finally { $agentArchive.Dispose() }

$productionAppIdMatch = [regex]::Match($productionIssText, '(?m)^AppId=\{\{(?<id>[0-9A-Fa-f-]{36})\}')
$homologationAppIdMatch = [regex]::Match($homologationIssText, '(?m)^AppId=\{\{(?<id>[0-9A-Fa-f-]{36})\}')
if (-not $productionAppIdMatch.Success -or
    $productionAppIdMatch.Groups['id'].Value -cne $expectedProductionAppId -or
    -not $homologationAppIdMatch.Success -or
    $homologationAppIdMatch.Groups['id'].Value -ceq $productionAppIdMatch.Groups['id'].Value) {
    throw 'Production AppId is missing, changed, or not distinct from Homologation.'
}
if ($productionIssText -notmatch '(?m)^ArchitecturesAllowed=x64compatible\s*$' -or
    $productionIssText -notmatch '(?m)^ArchitecturesInstallIn64BitMode=x64compatible\s*$' -or
    $productionIssText -match '(?m)^\[(Run|UninstallDelete)\]\s*$') {
    throw 'Production installer architecture or no-auto-run/data-preservation rules changed.'
}

$resolvedCommit = (& git -C $repoRoot rev-parse --verify ($SourceCommit + '^{commit}')).Trim()
if ($LASTEXITCODE -ne 0 -or $resolvedCommit -cne $SourceCommit) {
    throw 'The explicitly selected source commit does not resolve to that exact Git commit.'
}

$deploymentDoc = & git -C $repoRoot show ($SourceCommit + ':docs/operations/deployment.md')
$deploymentDocExitCode = $LASTEXITCODE
$environmentDoc = & git -C $repoRoot show ($SourceCommit + ':docs/operations/environments.md')
$environmentDocExitCode = $LASTEXITCODE
$versionedProductionUrl = (@($deploymentDoc) + @($environmentDoc)) -join "`n"
if ($deploymentDocExitCode -ne 0 -or
    $environmentDocExitCode -ne 0 -or
    $versionedProductionUrl -notmatch [regex]::Escape($expectedApiBaseUrl)) {
    throw 'The selected source commit does not document the pinned HTTPS Production API URL.'
}
$apiUri = $null
if (-not [Uri]::TryCreate($expectedApiBaseUrl, [UriKind]::Absolute, [ref]$apiUri) -or
    $apiUri.Scheme -cne 'https' -or
    $apiUri.Host -cne 'gymflow-api-prod.fly.dev' -or
    -not [string]::IsNullOrEmpty($apiUri.UserInfo) -or
    $apiUri.AbsolutePath -cne '/' -or
    -not [string]::IsNullOrEmpty($apiUri.Query) -or
    -not [string]::IsNullOrEmpty($apiUri.Fragment)) {
    throw 'The configured Production API URL is not the explicitly approved HTTPS API origin.'
}

$updateOriginUri = $null
if (-not [Uri]::TryCreate($updateDownloadOrigin, [UriKind]::Absolute, [ref]$updateOriginUri) -or
    $updateOriginUri.Scheme -cne 'https' -or
    $updateOriginUri.Host -cne 'avelri-gestao-downloads.pages.dev' -or
    -not $updateOriginUri.IsDefaultPort -or
    -not [string]::IsNullOrEmpty($updateOriginUri.UserInfo) -or
    $updateOriginUri.AbsolutePath -cne '/' -or
    -not [string]::IsNullOrEmpty($updateOriginUri.Query) -or
    -not [string]::IsNullOrEmpty($updateOriginUri.Fragment) -or
    $updateOriginUri.GetLeftPart([UriPartial]::Authority) -cne $updateDownloadOrigin) {
    throw 'The signed updater download origin must be the approved HTTPS Pages origin.'
}
foreach ($updateFile in @(
    @{ Url = $updateManifestUrl; Path = '/updates/production/release.json' },
    @{ Url = $updateSignatureUrl; Path = '/updates/production/release.sig' }
)) {
    $updateUri = $null
    if (-not [Uri]::TryCreate($updateFile.Url, [UriKind]::Absolute, [ref]$updateUri) -or
        $updateUri.GetLeftPart([UriPartial]::Authority) -cne $updateDownloadOrigin -or
        $updateUri.AbsolutePath -cne $updateFile.Path -or
        -not [string]::IsNullOrEmpty($updateUri.UserInfo) -or
        -not [string]::IsNullOrEmpty($updateUri.Query) -or
        -not [string]::IsNullOrEmpty($updateUri.Fragment)) {
        throw 'The signed updater manifest and signature must use the approved same-origin HTTPS Pages routes.'
    }
}
if ($updateKeyId -cnotmatch '^[A-Za-z0-9._-]{1,64}$') {
    throw 'The signed updater key ID does not meet the Desktop contract.'
}
try { $updatePublicKey = [Convert]::FromBase64String($updatePublicKeyBase64) }
catch [FormatException] { throw 'The signed updater public key is not valid Base64.' }
if ($updatePublicKey.Length -ne 32 -or
    [Convert]::ToBase64String($updatePublicKey) -cne $updatePublicKeyBase64) {
    throw 'The signed updater public key must be canonical Base64 of exactly 32 bytes.'
}
$updateConfigSource = (& git -C $repoRoot show ($SourceCommit + ':desktop/lib/core/update/update_distribution_config.dart')) -join "`n"
if ($LASTEXITCODE -ne 0 -or
    $updateConfigSource -cnotmatch [regex]::Escape($expectedProductionAppId)) {
    throw 'The selected source commit lacks the approved Production signed-updater contract.'
}
foreach ($defineName in @(
    'APP_ENV', 'AVELRI_UPDATES_ENABLED', 'AVELRI_UPDATE_APP_ID', 'AVELRI_UPDATE_CHANNEL',
    'AVELRI_UPDATE_MANIFEST_URL', 'AVELRI_UPDATE_SIGNATURE_URL',
    'AVELRI_UPDATE_DOWNLOAD_ORIGIN', 'AVELRI_UPDATE_KEY_ID',
    'AVELRI_UPDATE_PUBLIC_KEY_BASE64'
)) {
    if ($updateConfigSource -cnotmatch ('fromEnvironment\(\s*''' + [regex]::Escape($defineName) + '''')) {
        throw ('The selected source commit does not consume signed-updater define ' + $defineName + '.')
    }
}
$flutterDartDefines = @(
    '--dart-define=APP_ENV=production'
    ('--dart-define=API_BASE_URL=' + $expectedApiBaseUrl)
    '--dart-define=AVELRI_UPDATES_ENABLED=true'
    ('--dart-define=AVELRI_UPDATE_APP_ID=' + $expectedProductionAppId)
    '--dart-define=AVELRI_UPDATE_CHANNEL=production'
    ('--dart-define=AVELRI_UPDATE_MANIFEST_URL=' + $updateManifestUrl)
    ('--dart-define=AVELRI_UPDATE_SIGNATURE_URL=' + $updateSignatureUrl)
    ('--dart-define=AVELRI_UPDATE_DOWNLOAD_ORIGIN=' + $updateDownloadOrigin)
    ('--dart-define=AVELRI_UPDATE_KEY_ID=' + $updateKeyId)
    ('--dart-define=AVELRI_UPDATE_PUBLIC_KEY_BASE64=' + $updatePublicKeyBase64)
)

$flutterVersionFile = Join-Path (Split-Path -Parent $FlutterExecutable) 'cache\flutter.version.json'
if (-not (Test-Path -LiteralPath $flutterVersionFile -PathType Leaf)) {
    throw 'Flutter SDK version metadata is missing; build provenance cannot be recorded.'
}
$flutterVersionInfo = [IO.File]::ReadAllText($flutterVersionFile) | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace([string]$flutterVersionInfo.frameworkVersion) -or
    [string]::IsNullOrWhiteSpace([string]$flutterVersionInfo.frameworkRevision)) {
    throw 'Flutter SDK version metadata lacks the framework version or revision.'
}
$flutterVersion = [string]$flutterVersionInfo.frameworkVersion
$flutterRevision = [string]$flutterVersionInfo.frameworkRevision
$isccVersionOutput = @(& $InnoSetupCompiler '--version' 2>&1)
$isccVersionExitCode = $LASTEXITCODE
$isccVersionText = (@($isccVersionOutput) | ForEach-Object { $_.ToString() }) -join "`n"
$isccVersionMatch = [regex]::Match($isccVersionText,
    '(?m)(?:Compiler engine version:\s*)?Inno Setup (?<version>\d+\.\d+\.\d+)\b')
if ($isccVersionExitCode -eq 0 -and $isccVersionMatch.Success) {
    $isccVersion = $isccVersionMatch.Groups['version'].Value
}
else {
    # ISCC 6.7.3 rejects --version; use its matching per-user installation record.
    $innoRegistration = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1' -ErrorAction SilentlyContinue
    if ($null -eq $innoRegistration -or
        [string]::IsNullOrWhiteSpace([string]$innoRegistration.InstallLocation) -or
        [string]$innoRegistration.DisplayVersion -notmatch '^\d+\.\d+\.\d+$' -or
        [IO.Path]::GetFullPath((Join-Path $innoRegistration.InstallLocation 'ISCC.exe')) -ine
            [IO.Path]::GetFullPath($InnoSetupCompiler)) {
        throw 'Could not determine the Inno Setup compiler version from ISCC or its matching installation record.'
    }
    $isccVersion = [string]$innoRegistration.DisplayVersion
}
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runId = [guid]::NewGuid().ToString('N')
$outputRoot = 'C:\gf-i69-prod'
if (-not (Test-Path -LiteralPath $outputRoot -PathType Container)) {
    New-Item -ItemType Directory -Path $outputRoot | Out-Null
}
$outputRootItem = Get-Item -LiteralPath $outputRoot -Force
if (($outputRootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'Exclusive short output root must not be a reparse point.'
}
$runRoot = Join-Path $outputRoot ($timestamp + '-' + $runId)
if (Test-Path -LiteralPath $runRoot) { throw 'Exclusive build output path already exists.' }
New-Item -ItemType Directory -Path $runRoot | Out-Null

$sourceArchive = Join-Path $runRoot 'desktop-source.zip'
$sourceRoot = Join-Path $runRoot 'source'
New-Item -ItemType Directory -Path $sourceRoot | Out-Null
$desktopRoot = Join-Path $sourceRoot 'desktop'
$cmakeBuildRoot = Join-Path $desktopRoot 'build\windows'
$configureLog = Join-Path $runRoot 'flutter-configure.log'
$buildLog = Join-Path $runRoot 'flutter-build.log'
$pubLog = Join-Path $runRoot 'flutter-pub-get.log'
$compilerLog = Join-Path $runRoot 'inno-compile.log'
$exitCodePath = Join-Path $runRoot 'process-exit-codes.txt'
$commandPath = Join-Path $runRoot 'build-commands.txt'
[IO.File]::WriteAllText($exitCodePath, '', (New-Object Text.UTF8Encoding($false)))
function Add-ExitCodeEvidence {
    param([Parameter(Mandatory)][string]$Stage, [Parameter(Mandatory)][int]$ExitCode)
    [IO.File]::AppendAllText($exitCodePath, ($Stage + '=' + $ExitCode + "`r`n"), (New-Object Text.UTF8Encoding($false)))
}

try {
    & git -C $repoRoot archive --format=zip --output=$sourceArchive $SourceCommit desktop
    $archiveExitCode = $LASTEXITCODE
    Add-ExitCodeEvidence -Stage 'git_archive' -ExitCode $archiveExitCode
    if ($archiveExitCode -ne 0 -or -not (Test-Path -LiteralPath $sourceArchive -PathType Leaf)) {
        throw 'Could not export the pinned commit as a clean Desktop source archive.'
    }
    Expand-Archive -LiteralPath $sourceArchive -DestinationPath $sourceRoot

    $pubspecPath = Join-Path $desktopRoot 'pubspec.yaml'
    $lockPath = Join-Path $desktopRoot 'pubspec.lock'
    if (-not (Test-Path -LiteralPath $pubspecPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
        throw 'The clean source archive lacks pubspec.yaml or pubspec.lock.'
    }
    if (Test-Path -LiteralPath $cmakeBuildRoot) {
        throw 'The clean source archive contains a Windows CMake build tree.'
    }
    $pubspecText = [IO.File]::ReadAllText($pubspecPath)
    $versionMatch = [regex]::Match($pubspecText, '(?m)^version:\s*(?<name>\d+\.\d+\.\d+)\+(?<number>\d+)\s*$')
    if (-not $versionMatch.Success) { throw 'Desktop version must be an explicit semantic version and build number.' }
    $appVersion = $versionMatch.Groups['name'].Value
    $buildNumber = $versionMatch.Groups['number'].Value
    $commitShort = $SourceCommit.Substring(0, 8)
    $flutterBuildArgs = @('build', 'windows', '--release', '--no-pub',
        ('--build-name=' + $appVersion), ('--build-number=' + $buildNumber)) + $flutterDartDefines
    $flutterConfigureArgs = @($flutterBuildArgs) + '--config-only'
    $commandLines = @(
        'git archive --format=zip --output=<runRoot>\desktop-source.zip ' + $SourceCommit + ' desktop'
        'flutter pub get --offline'
        ('CMAKE_GENERATOR=' + $cmakeEnvironment.CMAKE_GENERATOR)
        ('CMAKE_GENERATOR_INSTANCE=' + $cmakeEnvironment.CMAKE_GENERATOR_INSTANCE)
        ('CMAKE_GENERATOR_TOOLSET=' + $cmakeEnvironment.CMAKE_GENERATOR_TOOLSET)
        ('flutter ' + ($flutterConfigureArgs -join ' '))
        ('flutter ' + ($flutterBuildArgs -join ' '))
        ('ISCC.exe /DAppSource=<ReleaseDir> /DBuildMetadata=<BuildInfo> /DInstallerOutput=<InstallerDir> /DAppVersion=' +
            $appVersion + ' /DBuildNumber=' + $buildNumber + ' /DCommitShort=' + $commitShort +
            ' /DVCRedistPath=<VerifiedOfflineRedistributable> AvelriGestao-Production.iss')
    )
    [IO.File]::WriteAllText($commandPath, ([string]::Join("`r`n", $commandLines) + "`r`n"), (New-Object Text.UTF8Encoding($false)))

    Push-Location $desktopRoot
    try {
        & $FlutterExecutable pub get --offline *> $pubLog
        $pubExitCode = $LASTEXITCODE
        Add-ExitCodeEvidence -Stage 'flutter_pub_get' -ExitCode $pubExitCode
        if ($pubExitCode -ne 0) { throw "Flutter offline dependency resolution failed (exit $pubExitCode); see $pubLog." }

        if (Test-Path -LiteralPath $cmakeBuildRoot) {
            throw 'The isolated CMake build tree appeared before configuration; refusing to reuse a cache.'
        }
        $previousCMakeEnvironment = @{}
        foreach ($environmentName in $cmakeEnvironment.Keys) {
            $previousCMakeEnvironment[$environmentName] = [Environment]::GetEnvironmentVariable($environmentName, 'Process')
        }
        try {
            foreach ($environmentName in $cmakeEnvironment.Keys) {
                [Environment]::SetEnvironmentVariable($environmentName, [string]$cmakeEnvironment[$environmentName], 'Process')
            }

            & $FlutterExecutable @flutterConfigureArgs *> $configureLog
            $configureExitCode = $LASTEXITCODE
            Add-ExitCodeEvidence -Stage 'flutter_windows_configure_release' -ExitCode $configureExitCode
            if ($configureExitCode -ne 0) {
                throw "Flutter Windows Release CMake configuration failed (exit $configureExitCode); see $configureLog."
            }

            $cmakeCacheFiles = @(Get-ChildItem -LiteralPath $cmakeBuildRoot -Filter 'CMakeCache.txt' -File -Recurse -ErrorAction SilentlyContinue)
            $compilerInfoFiles = @(Get-ChildItem -LiteralPath $cmakeBuildRoot -Filter 'CMakeCXXCompiler.cmake' -File -Recurse -ErrorAction SilentlyContinue)
            if ($cmakeCacheFiles.Count -ne 1 -or $compilerInfoFiles.Count -ne 1) {
                throw 'CMake configuration did not produce exactly one isolated cache and C++ compiler provenance file.'
            }
            $configuredCMakeCachePath = $cmakeCacheFiles[0].FullName
            $configuredCompilerInfoPath = $compilerInfoFiles[0].FullName
            $null = Assert-AvelriCMakeProvenance `
                -CMakeCachePath $configuredCMakeCachePath `
                -CompilerInfoPath $configuredCompilerInfoPath `
                -FreshBuildRoot $cmakeBuildRoot `
                -Toolchain $stableToolchain

            & $FlutterExecutable @flutterBuildArgs *> $buildLog
            $buildExitCode = $LASTEXITCODE
            Add-ExitCodeEvidence -Stage 'flutter_build_windows_release' -ExitCode $buildExitCode
            if ($buildExitCode -ne 0) { throw "Flutter Release build failed (exit $buildExitCode); see $buildLog." }
        }
        finally {
            foreach ($environmentName in $cmakeEnvironment.Keys) {
                [Environment]::SetEnvironmentVariable($environmentName, $previousCMakeEnvironment[$environmentName], 'Process')
            }
        }
    }
    finally { Pop-Location }

    $releaseExecutables = @(Get-ChildItem -LiteralPath (Join-Path $desktopRoot 'build\windows') `
        -Filter 'avelri_gestao.exe' -File -Recurse -ErrorAction SilentlyContinue)
    if ($releaseExecutables.Count -ne 1 -or
        $releaseExecutables[0].Directory.Name -cne 'Release' -or
        -not (Test-Path -LiteralPath (Join-Path $releaseExecutables[0].Directory.FullName 'data\flutter_assets') -PathType Container)) {
        throw 'Expected exactly one complete Windows Release output with Flutter assets.'
    }
    $appSource = $releaseExecutables[0].Directory.FullName
    $cmakeCacheFiles = @(Get-ChildItem -LiteralPath $cmakeBuildRoot -Filter 'CMakeCache.txt' -File -Recurse -ErrorAction SilentlyContinue)
    $compilerInfoFiles = @(Get-ChildItem -LiteralPath $cmakeBuildRoot -Filter 'CMakeCXXCompiler.cmake' -File -Recurse -ErrorAction SilentlyContinue)
    if ($cmakeCacheFiles.Count -ne 1 -or $compilerInfoFiles.Count -ne 1 -or
        -not (Test-AvelriPathEqual -Left $cmakeCacheFiles[0].FullName -Right $configuredCMakeCachePath) -or
        -not (Test-AvelriPathEqual -Left $compilerInfoFiles[0].FullName -Right $configuredCompilerInfoPath)) {
        throw 'The CMake cache or compiler provenance changed unexpectedly during the Windows Release build.'
    }
    $buildCMakeProvenance = Assert-AvelriCMakeProvenance `
        -CMakeCachePath $cmakeCacheFiles[0].FullName `
        -CompilerInfoPath $compilerInfoFiles[0].FullName `
        -FreshBuildRoot $cmakeBuildRoot `
        -Toolchain $stableToolchain
    $msvcVersion = [Version]$buildCMakeProvenance.CompilerVersion
    $minimumRuntimeForBuild = New-Object Version(14, $msvcVersion.Minor, $msvcVersion.Build, $msvcVersion.Revision)
    if ([Version]$vcRedistVersion -lt $minimumRuntimeForBuild) {
        throw ('Bundled Visual C++ Redistributable ' + $vcRedistVersion +
            ' is older than the MSVC runtime required by this build: ' + $minimumRuntimeForBuild + '.')
    }
    $buildMetadata = Join-Path $runRoot 'Avelri-Build-Info.txt'
    $metadataLines = @(
        'Product=Avelri Gestao'
        ('SourceCommit=' + $resolvedCommit)
        ('Version=' + $appVersion + '+' + $buildNumber)
        'APP_ENV=production'
        ('API_BASE_URL=' + $expectedApiBaseUrl)
        'AVELRI_UPDATES_ENABLED=true'
        ('AVELRI_UPDATE_APP_ID=' + $expectedProductionAppId)
        'AVELRI_UPDATE_CHANNEL=production'
        ('AVELRI_UPDATE_MANIFEST_URL=' + $updateManifestUrl)
        ('AVELRI_UPDATE_SIGNATURE_URL=' + $updateSignatureUrl)
        ('AVELRI_UPDATE_DOWNLOAD_ORIGIN=' + $updateDownloadOrigin)
        ('AVELRI_UPDATE_KEY_ID=' + $updateKeyId)
        ('AVELRI_UPDATE_PUBLIC_KEY_BASE64=' + $updatePublicKeyBase64)
        ('BuildScriptSHA256=' + (Get-FileHash -LiteralPath $MyInvocation.MyCommand.Path -Algorithm SHA256).Hash)
        ('InstallerScriptSHA256=' + (Get-FileHash -LiteralPath $productionIss -Algorithm SHA256).Hash)
        ('VCRedistHelperSHA256=' + (Get-FileHash -LiteralPath $vcRedistHelper -Algorithm SHA256).Hash)
        ('VCRedistVersion=' + $vcRedistVersion)
        ('VCRedistSHA256=' + $vcRedistActualHash)
        'VCRedistAuthenticode=Valid (Microsoft Corporation)'
        ('VisualStudioInstallationPath=' + $stableToolchain.VisualStudioRoot)
        ('VisualStudioInstallationVersion=' + $visualStudioInstance.installationVersion)
        ('VisualStudioChannel=' + $visualStudioInstance.channelId)
        'VisualStudioIsPrerelease=false'
        ('CMakeGenerator=' + $buildCMakeProvenance.Generator)
        ('CMakeGeneratorInstance=' + $buildCMakeProvenance.Instance)
        ('CMakeGeneratorToolset=' + $buildCMakeProvenance.Toolset)
        ('CMakeCompilerPath=' + $buildCMakeProvenance.Compiler)
        ('CMakeCompilerInfoSHA256=' + (Get-FileHash -LiteralPath $compilerInfoFiles[0].FullName -Algorithm SHA256).Hash)
        ('CMakeCacheSHA256=' + (Get-FileHash -LiteralPath $cmakeCacheFiles[0].FullName -Algorithm SHA256).Hash)
        ('ProductionToolchainModuleSHA256=' + (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'ProductionToolchain.psm1') -Algorithm SHA256).Hash)
        ('MSVCCompilerVersion=' + $msvcVersion)
        ('MSVCToolsetVersion=' + $expectedMsvcToolsetVersion)
        ('FlutterSdkVersion=' + $flutterVersion)
        ('FlutterSdkRevision=' + $flutterRevision)
        ('InnoSetupCompiler=' + $isccVersion)
        ('AccessAgentPackageVersion=' + $accessAgentVersion)
        'AccessAgentPackageRuntime=win-x64 self-contained'
        ('AccessAgentPackageBytes=' + $accessAgentPackageBytes)
        ('AccessAgentPackageSHA256=' + $accessAgentPackageHash)
        ('AccessAgentProvisionerSHA256=' + (Get-FileHash -LiteralPath $accessAgentProvisioner -Algorithm SHA256).Hash)
        ('AccessAgentPairingActionSHA256=' + (Get-FileHash -LiteralPath $accessAgentPairingAction -Algorithm SHA256).Hash)
        'AccessAgentExistingService=preserved without update or restart'
        'AccessAgentNewService=created disabled and unpaired; starts only after Admin pairing'
        ('BuildTimestampLocal=' + (Get-Date -Format 'yyyy-MM-ddTHH:mm:ssK'))
    )
    [IO.File]::WriteAllText($buildMetadata, ([string]::Join("`r`n", $metadataLines) + "`r`n"), (New-Object Text.UTF8Encoding($false)))

    $installerOutput = Join-Path $runRoot 'installer'
    New-Item -ItemType Directory -Path $installerOutput | Out-Null
    & $InnoSetupCompiler `
        ('/DAppSource=' + $appSource) `
        ('/DBuildMetadata=' + $buildMetadata) `
        ('/DInstallerOutput=' + $installerOutput) `
        ('/DAppVersion=' + $appVersion) `
        ('/DBuildNumber=' + $buildNumber) `
        ('/DCommitShort=' + $commitShort) `
        ('/DVCRedistPath=' + $VCRedistPath) `
        ('/DAccessAgentPackagePath="' + $AccessAgentPackagePath + '"') `
        ('/DAccessAgentPackageSha256="' + $accessAgentPackageHash + '"') `
        ('/DAccessAgentProvisionerScriptPath="' + $accessAgentProvisioner + '"') `
        ('/DAccessAgentPairingScriptPath="' + $accessAgentPairingAction + '"') `
        '/DMicrosoftRedistributionRightsConfirmed=1' `
        $productionIss *> $compilerLog
    $compilerExitCode = $LASTEXITCODE
    Add-ExitCodeEvidence -Stage 'inno_setup_compile' -ExitCode $compilerExitCode
    if ($compilerExitCode -ne 0) { throw "Inno Setup compilation failed (exit $compilerExitCode); see $compilerLog." }
    $compilerLogText = [IO.File]::ReadAllText($compilerLog)
    $compiledIsccVersionMatch = [regex]::Match($compilerLogText,
        '(?m)^Compiler engine version:\s*Inno Setup (?<version>\d+\.\d+\.\d+)\s*$')
    if (-not $compiledIsccVersionMatch.Success -or
        $compiledIsccVersionMatch.Groups['version'].Value -cne $isccVersion) {
        throw 'The ISCC build log version does not match the recorded Inno Setup compiler version.'
    }

    $installerPath = Join-Path $installerOutput ('AvelriGestao-Production-' + $appVersion + '+' +
        $buildNumber + '-' + $commitShort + '.exe')
    if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
        throw 'Inno Setup returned success but the expected installer is missing.'
    }
    $installerHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
    $hashPath = Join-Path $runRoot 'installer.sha256.txt'
    [IO.File]::WriteAllText($hashPath, ($installerHash + '  ' + $installerPath + "`r`n"), (New-Object Text.UTF8Encoding($false)))

    Write-Output ('PRODUCTION_PACKAGE=PASS')
    Write-Output ('SourceCommit=' + $resolvedCommit)
    Write-Output ('Version=' + $appVersion + '+' + $buildNumber)
    Write-Output 'APP_ENV=production'
    Write-Output ('API_BASE_URL=' + $expectedApiBaseUrl)
    Write-Output ('Installer=' + $installerPath)
    Write-Output ('SHA256=' + $installerHash)
    Write-Output ('VCRedistVersion=' + $vcRedistVersion)
    Write-Output ('VCRedistSHA256=' + $vcRedistActualHash)
    Write-Output ('AccessAgentPackageVersion=' + $accessAgentVersion)
    Write-Output ('AccessAgentPackageSHA256=' + $accessAgentPackageHash)
    Write-Output ('BuildManifest=' + $buildMetadata)
    Write-Output ('ExitCodes=' + $exitCodePath)
    Write-Output ('BuildCommands=' + $commandPath)
    Write-Output ('Logs=' + $runRoot)
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    [Console]::Error.WriteLine('Partial build artifacts and logs were preserved at: ' + $runRoot)
    exit 1
}
