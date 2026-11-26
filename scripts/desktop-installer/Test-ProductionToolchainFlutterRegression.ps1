[CmdletBinding()]
param(
    [string]$SourceCommit = 'f3a9cdf15cd1f850082e0a5f987f777fb3c3cc27',
    [string]$ApprovedEvidencePath = 'C:\gf-i69-local\20261125-b0074c55\local-build-evidence.json',
    [string]$RejectedBuildRoot = 'C:\gf-i69-local\20261125-874de09f\source\desktop\build\windows',
    [string]$OutputRoot = 'C:\gf-i69-local',
    [string]$FlutterExecutable = 'C:\flutter\bin\flutter.bat'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ProductionToolchain.psm1') -Force
function Assert-Regression([bool]$Value, [string]$Message) { if (-not $Value) { throw $Message } }
function Assert-Rejected([scriptblock]$Action, [string]$ExpectedMessage) {
    try { & $Action | Out-Null }
    catch {
        if ($_.Exception.Message -notlike $ExpectedMessage) { throw }
        return
    }
    throw 'Expected the regression fixture to be rejected.'
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$approved = Get-Content -LiteralPath $ApprovedEvidencePath -Raw | ConvertFrom-Json
Assert-Regression ($approved.Result -ceq 'PASS' -and $approved.SourceCommit -ceq $SourceCommit) 'The approved application build evidence does not match SourceCommit.'
$toolchain = Get-AvelriStableToolchain -VisualStudioInstance (Resolve-AvelriStableVisualStudio)
$rejectedCaches = @(Get-ChildItem -LiteralPath $RejectedBuildRoot -Filter CMakeCache.txt -Recurse -File)
$rejectedInfos = @(Get-ChildItem -LiteralPath $RejectedBuildRoot -Filter CMakeCXXCompiler.cmake -Recurse -File)
Assert-Regression ($rejectedCaches.Count -eq 1 -and $rejectedInfos.Count -eq 1) 'The preserved real fallback fixture is missing.'
Assert-Rejected {
    Assert-AvelriCMakeProvenance -CMakeCachePath $rejectedCaches[0].FullName -CompilerInfoPath $rejectedInfos[0].FullName -FreshBuildRoot $RejectedBuildRoot -Toolchain $toolchain
} '*CMAKE_GENERATOR_INSTANCE*'
Write-Output 'OBSERVED_FLUTTER_INSIDERS_FALLBACK_REJECTED=PASS'

$approvedRoot = Join-Path $approved.RunRoot 'source\desktop\build\windows'
$approvedCache = @(Get-ChildItem -LiteralPath $approvedRoot -Filter CMakeCache.txt -Recurse -File)
$approvedInfo = @(Get-ChildItem -LiteralPath $approvedRoot -Filter CMakeCXXCompiler.cmake -Recurse -File)
$null = Assert-AvelriCMakeProvenance -CMakeCachePath $approvedCache[0].FullName -CompilerInfoPath $approvedInfo[0].FullName -FreshBuildRoot $approvedRoot -Toolchain $toolchain -RequireReleaseBuildEvidence
Write-Output 'APPROVED_REAL_RELEASE_EVIDENCE_REUSED=PASS'

# Extract only the builder's actual configuration-stage command AST. Executing
# this same command tests its parameter wiring without running commercial gates,
# the application compile, ISCC, signing, or any legal attestation.
$tokens = $null; $parseErrors = $null
$builderPath = Join-Path $PSScriptRoot 'Build-ProductionInstaller.ps1'
$ast = [Management.Automation.Language.Parser]::ParseFile($builderPath, [ref]$tokens, [ref]$parseErrors)
Assert-Regression ($parseErrors.Count -eq 0) 'Builder syntax is invalid.'
$calls = @($ast.FindAll({ param($node)
    $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -ceq 'Invoke-AvelriWindowsReleaseConfiguration'
}, $true))
Assert-Regression ($calls.Count -eq 1) 'Builder must use the shared real Flutter configuration stage exactly once.'
$stageCommand = [scriptblock]::Create($calls[0].Extent.Text)
$runRoot = Join-Path $OutputRoot ('flutter-regression-' + [guid]::NewGuid().ToString('N').Substring(0,8))
if (Test-Path -LiteralPath $runRoot) { throw 'Regression output already exists.' }
New-Item -ItemType Directory -Path $runRoot | Out-Null
$archive = Join-Path $runRoot 'desktop-source.zip'
& git -C $repoRoot archive --format=zip --output=$archive $SourceCommit desktop
if ($LASTEXITCODE -ne 0) { throw 'Could not export the regression source snapshot.' }
Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $runRoot 'source')
$desktopRoot = Join-Path $runRoot 'source\desktop'
$cmakeToolchainFile = Join-Path $runRoot 'StableToolchain.cmake'
$configureLog = Join-Path $runRoot 'flutter-configure.log'
$exitCodePath = Join-Path $runRoot 'process-exit-codes.txt'
$stableToolchain = $toolchain
$flutterBuildArgs = @('build', 'windows', '--release', '--no-pub', '--build-name=1.0.1', '--build-number=2')
foreach ($define in $approved.ProductionDefines.PSObject.Properties) { $flutterBuildArgs += '--dart-define=' + $define.Name + '=' + $define.Value }
$lockPath = Join-Path $desktopRoot 'pubspec.lock'
$lockHash = (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash
Push-Location $desktopRoot
try {
    & $FlutterExecutable pub get --offline *> (Join-Path $runRoot 'flutter-pub-get.log')
    if ($LASTEXITCODE -ne 0) { throw 'Offline pub failed for the regression source.' }
}
finally { Pop-Location }
Assert-Regression ((Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash -ceq $lockHash) 'The reviewed lockfile changed.'
$names = @('CMAKE_GENERATOR','CMAKE_GENERATOR_INSTANCE','CMAKE_GENERATOR_TOOLSET','CMAKE_TOOLCHAIN_FILE')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$configuration = & $stageCommand
foreach ($name in $names) { Assert-Regression ([Environment]::GetEnvironmentVariable($name, 'Process') -ceq $previous[$name]) ('Process environment was not restored: ' + $name) }
Assert-Regression ($configuration.Provenance.CompilerVersion -ceq '19.50.35740.0' -and $configuration.Provenance.ResolvedToolsetVersion -ceq '14.50.35717') 'The real Flutter configuration selected a wrong compiler.'
Assert-Regression (-not (Test-Path -LiteralPath (Join-Path $desktopRoot 'build\windows\x64\runner\Release\avelri_gestao.exe'))) 'Configuration regression must not compile the application.'
Assert-Rejected { & $stageCommand } '*build tree already exists*'
Assert-Rejected { New-AvelriCMakeToolchainFile -Toolchain $toolchain -Path $cmakeToolchainFile } '*already exists*'

$configuration | Add-Member -NotePropertyName BuilderSHA256 -NotePropertyValue (Get-FileHash -LiteralPath $builderPath -Algorithm SHA256).Hash
$configuration | Add-Member -NotePropertyName ModuleSHA256 -NotePropertyValue (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'ProductionToolchain.psm1') -Algorithm SHA256).Hash
$configuration | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot 'regression-evidence.json') -Encoding utf8
Write-Output 'BUILDER_ACTUAL_CONFIGURATION_COMMAND=PASS'
Write-Output 'FLUTTER_REAL_CXX_PROJECT_STABLE_14_50=PASS'
Write-Output 'EXISTING_CACHE_AND_TOOLCHAIN_OVERWRITE_REJECTION=PASS'
Write-Output 'PROCESS_ENVIRONMENT_RESTORED=PASS'
Write-Output ('REGRESSION_EVIDENCE=' + (Join-Path $runRoot 'regression-evidence.json'))
Write-Output 'APPLICATION_REBUILD=NOT_RUN; COMMERCIAL_PACKAGING=NOT_RUN'
