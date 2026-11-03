[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$SourceCommit,
    [string]$FlutterExecutable = 'C:\flutter\bin\flutter.bat',
    [string]$InnoSetupCompiler
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler)) {
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        throw 'LOCALAPPDATA is unavailable; specify the installed Inno Setup compiler path explicitly.'
    }
    $InnoSetupCompiler = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
}

$expectedApiBaseUrl = 'https://gymflow-api-prod.fly.dev'
$expectedProductionAppId = 'B46B7D1A-6E54-4C0A-A1E2-5F41C6D92837'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$productionIss = Join-Path $PSScriptRoot 'AvelriGestao-Production.iss'
$homologationIss = Join-Path $PSScriptRoot 'AvelriGestao-Homologacao.iss'

if ($SourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'SourceCommit must be an explicit full lowercase Git commit SHA.'
}
if (-not (Test-Path -LiteralPath $productionIss -PathType Leaf)) {
    throw 'Production Inno Setup script is missing.'
}
if (-not (Test-Path -LiteralPath $homologationIss -PathType Leaf)) {
    throw 'Homologation Inno Setup script is missing; Production AppId uniqueness cannot be confirmed.'
}
if (-not (Test-Path -LiteralPath $FlutterExecutable -PathType Leaf)) {
    throw 'The explicitly configured Flutter launcher is unavailable.'
}
if (-not (Test-Path -LiteralPath $InnoSetupCompiler -PathType Leaf)) {
    throw 'The explicitly configured Inno Setup compiler is unavailable.'
}
$productionIssText = [IO.File]::ReadAllText($productionIss)
$homologationIssText = [IO.File]::ReadAllText($homologationIss)
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
$isccFileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($InnoSetupCompiler).FileVersion
if ([string]::IsNullOrWhiteSpace($isccFileVersion)) {
    throw 'Inno Setup executable does not expose a file version; build provenance cannot be recorded.'
}
$isccVersion = $isccFileVersion
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
    $pubspecText = [IO.File]::ReadAllText($pubspecPath)
    $versionMatch = [regex]::Match($pubspecText, '(?m)^version:\s*(?<name>\d+\.\d+\.\d+)\+(?<number>\d+)\s*$')
    if (-not $versionMatch.Success) { throw 'Desktop version must be an explicit semantic version and build number.' }
    $appVersion = $versionMatch.Groups['name'].Value
    $buildNumber = $versionMatch.Groups['number'].Value
    $commitShort = $SourceCommit.Substring(0, 8)
    $commandLines = @(
        'git archive --format=zip --output=<runRoot>\desktop-source.zip ' + $SourceCommit + ' desktop'
        'flutter pub get --offline'
        ('flutter build windows --release --no-pub --build-name=' + $appVersion + ' --build-number=' + $buildNumber +
            ' --dart-define=APP_ENV=production --dart-define=API_BASE_URL=' + $expectedApiBaseUrl)
        ('ISCC.exe /DAppSource=<ReleaseDir> /DBuildMetadata=<BuildInfo> /DInstallerOutput=<InstallerDir> /DAppVersion=' +
            $appVersion + ' /DBuildNumber=' + $buildNumber + ' /DCommitShort=' + $commitShort + ' AvelriGestao-Production.iss')
    )
    [IO.File]::WriteAllText($commandPath, ([string]::Join("`r`n", $commandLines) + "`r`n"), (New-Object Text.UTF8Encoding($false)))

    Push-Location $desktopRoot
    try {
        & $FlutterExecutable pub get --offline *> $pubLog
        $pubExitCode = $LASTEXITCODE
        Add-ExitCodeEvidence -Stage 'flutter_pub_get' -ExitCode $pubExitCode
        if ($pubExitCode -ne 0) { throw "Flutter offline dependency resolution failed (exit $pubExitCode); see $pubLog." }

        & $FlutterExecutable build windows --release --no-pub `
            (('--build-name=' + $appVersion)) `
            (('--build-number=' + $buildNumber)) `
            '--dart-define=APP_ENV=production' `
            ('--dart-define=API_BASE_URL=' + $expectedApiBaseUrl) *> $buildLog
        $buildExitCode = $LASTEXITCODE
        Add-ExitCodeEvidence -Stage 'flutter_build_windows_release' -ExitCode $buildExitCode
        if ($buildExitCode -ne 0) { throw "Flutter Release build failed (exit $buildExitCode); see $buildLog." }
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
    $buildMetadata = Join-Path $runRoot 'Avelri-Build-Info.txt'
    $metadataLines = @(
        'Product=Avelri Gestao'
        ('SourceCommit=' + $resolvedCommit)
        ('Version=' + $appVersion + '+' + $buildNumber)
        'APP_ENV=production'
        ('API_BASE_URL=' + $expectedApiBaseUrl)
        ('BuildScriptSHA256=' + (Get-FileHash -LiteralPath $MyInvocation.MyCommand.Path -Algorithm SHA256).Hash)
        ('InstallerScriptSHA256=' + (Get-FileHash -LiteralPath $productionIss -Algorithm SHA256).Hash)
        ('FlutterSdkVersion=' + $flutterVersion)
        ('FlutterSdkRevision=' + $flutterRevision)
        ('InnoSetupCompiler=' + $isccVersion)
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
        $productionIss *> $compilerLog
    $compilerExitCode = $LASTEXITCODE
    Add-ExitCodeEvidence -Stage 'inno_setup_compile' -ExitCode $compilerExitCode
    if ($compilerExitCode -ne 0) { throw "Inno Setup compilation failed (exit $compilerExitCode); see $compilerLog." }

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
