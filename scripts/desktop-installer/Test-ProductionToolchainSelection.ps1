[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'ProductionToolchain.psm1') -Force

function Assert-True {
    param([Parameter(Mandatory = $true)][bool]$Condition, [Parameter(Mandatory = $true)][string]$Message)
    if (-not $Condition) { throw $Message }
}

function Assert-Throws {
    param([Parameter(Mandatory = $true)][scriptblock]$Action, [Parameter(Mandatory = $true)][string]$Message)
    $threw = $false
    try { & $Action }
    catch { $threw = $true }
    if (-not $threw) { throw $Message }
}

$stable = Resolve-AvelriStableVisualStudio
$toolchain = Get-AvelriStableToolchain -VisualStudioInstance $stable
Assert-True ($stable.isPrerelease -eq $false -and $stable.channelId -ceq 'VisualStudio.18.Release') 'The selected instance is not Visual Studio 18 Stable.'
Assert-True ($toolchain.ToolsetVersion -ceq '14.50.35717' -and $toolchain.CompilerVersion -ceq '19.50.35740.0') 'The physical Stable MSVC toolset/compiler pin failed.'
Assert-True (Test-Path -LiteralPath $toolchain.AtlHeader -PathType Leaf) 'The MSVC 14.50 ATL header is missing.'
Assert-True (Test-Path -LiteralPath $toolchain.CMakePath -PathType Leaf) 'Stable CMake is missing.'
Assert-True (Test-Path -LiteralPath $toolchain.MSBuildPath -PathType Leaf) 'Stable MSBuild is missing.'
Assert-True ($toolchain.WindowsSdkVersions.Count -gt 0) 'No usable Windows SDK was found.'
Write-Output 'STABLE_INSTANCE_AND_COMPONENTS=PASS'

$stableFixture = [pscustomobject]@{
    productId = 'Microsoft.VisualStudio.Product.Community'
    channelId = 'VisualStudio.18.Release'
    catalog = [pscustomobject]@{ buildBranch = 'stable' }
    isPrerelease = $false
    isComplete = $true
    isLaunchable = $true
    installationVersion = '18.12.1.0'
    installationPath = 'C:\Fixture\Community'
}
$insidersFixture = [pscustomobject]@{
    productId = 'Microsoft.VisualStudio.Product.Community'
    channelId = 'VisualStudio.18.Preview'
    catalog = [pscustomobject]@{ buildBranch = 'insiders' }
    isPrerelease = $true
    isComplete = $true
    isLaunchable = $true
    installationVersion = '18.13.0.0'
    installationPath = 'C:\Fixture\Insiders'
}
$selectedFixture = Select-AvelriStableVisualStudioInstance -Instances @($insidersFixture, $stableFixture)
Assert-True ($selectedFixture.installationPath -ceq $stableFixture.installationPath) 'Stable instance selection chose the wrong Visual Studio instance.'
Assert-Throws { Select-AvelriStableVisualStudioInstance -Instances @($insidersFixture) | Out-Null } 'Insiders-only selection should have been rejected.'
Write-Output 'INSIDERS_ONLY_SELECTION_REJECTION=PASS'

$expectedCompiler = Join-Path $toolchain.VisualStudioRoot 'VC\Tools\MSVC\14.50.35717\bin\Hostx64\x64\cl.exe'
Assert-Throws {
    Assert-AvelriCompilerIdentity `
        -ExpectedCompilerPath $expectedCompiler `
        -ActualCompilerPath ($expectedCompiler -replace '14\.50\.35717', '14.52.37025') `
        -ExpectedCompilerVersion '19.50.35740.0' `
        -ActualCompilerVersion '19.52.37025.0' `
        -ExpectedToolsetVersion '14.50.35717' `
        -ActualToolsetVersion '14.52.37025'
} 'An MSVC 14.52 compiler/toolset should have been rejected.'
Write-Output 'WRONG_TOOLSET_REJECTION=PASS'

$temporaryRoot = Join-Path $env:TEMP ('avelri-i69-toolchain-tests-' + [guid]::NewGuid().ToString('N'))
$sourceRoot = Join-Path $temporaryRoot 'cmake-source'
$buildRoot = Join-Path $temporaryRoot 'cmake-build'
New-Item -ItemType Directory -Path $sourceRoot -Force | Out-Null
try {
    $cachePath = Join-Path $buildRoot 'CMakeCache.txt'
    $compilerInfoPath = Join-Path $buildRoot 'CMakeFiles\4.3.1\CMakeCXXCompiler.cmake'
    New-Item -ItemType Directory -Path (Split-Path -Parent $compilerInfoPath) -Force | Out-Null
    $fixtureCache = @(
        'CMAKE_GENERATOR:INTERNAL=Visual Studio 18 2026'
        ('CMAKE_GENERATOR_INSTANCE:INTERNAL=' + $toolchain.VisualStudioRoot)
        'CMAKE_GENERATOR_PLATFORM:INTERNAL=x64'
        'CMAKE_GENERATOR_TOOLSET:INTERNAL=v145,version=14.50.35717,host=x64'
        'CMAKE_VS_PLATFORM_TOOLSET:STRING=v145'
        'CMAKE_VS_PLATFORM_TOOLSET_VERSION:STRING=14.50.35717'
    ) -join "`r`n"
    [IO.File]::WriteAllText($cachePath, $fixtureCache + "`r`n", (New-Object Text.UTF8Encoding($false)))
    $fixtureCompilerPath = $toolchain.CompilerPath -replace '\\', '/'
    $fixtureCompilerInfo = @(
        'set(CMAKE_CXX_COMPILER_ID "MSVC")'
        'set(CMAKE_CXX_COMPILER_VERSION "19.50.35740.0")'
        ('set(CMAKE_CXX_COMPILER "' + $fixtureCompilerPath + '")')
    ) -join "`r`n"
    [IO.File]::WriteAllText($compilerInfoPath, $fixtureCompilerInfo + "`r`n", (New-Object Text.UTF8Encoding($false)))
    $null = Assert-AvelriCMakeProvenance `
        -CMakeCachePath $cachePath `
        -CompilerInfoPath $compilerInfoPath `
        -FreshBuildRoot $buildRoot `
        -Toolchain $toolchain

    $wrongInstanceCache = $fixtureCache.Replace($toolchain.VisualStudioRoot, 'C:\Program Files\Microsoft Visual Studio\18\Insiders')
    [IO.File]::WriteAllText($cachePath, $wrongInstanceCache + "`r`n", (New-Object Text.UTF8Encoding($false)))
    Assert-Throws {
        Assert-AvelriCMakeProvenance -CMakeCachePath $cachePath -CompilerInfoPath $compilerInfoPath -FreshBuildRoot $buildRoot -Toolchain $toolchain | Out-Null
    } 'An incompatible CMake cache pointing at Insiders should have been rejected.'

    $outsideCachePath = Join-Path $temporaryRoot 'outside-CMakeCache.txt'
    [IO.File]::WriteAllText($outsideCachePath, $fixtureCache + "`r`n", (New-Object Text.UTF8Encoding($false)))
    Assert-Throws {
        Assert-AvelriCMakeProvenance -CMakeCachePath $outsideCachePath -CompilerInfoPath $compilerInfoPath -FreshBuildRoot $buildRoot -Toolchain $toolchain | Out-Null
    } 'A CMake cache outside the isolated build tree should have been rejected.'

    $wrongToolsetCache = $fixtureCache.Replace('14.50.35717', '14.52.37025').Replace('19.50.35740.0', '19.52.37025.0')
    [IO.File]::WriteAllText($cachePath, $wrongToolsetCache + "`r`n", (New-Object Text.UTF8Encoding($false)))
    Assert-Throws {
        Assert-AvelriCMakeProvenance -CMakeCachePath $cachePath -CompilerInfoPath $compilerInfoPath -FreshBuildRoot $buildRoot -Toolchain $toolchain | Out-Null
    } 'An incompatible CMake cache selecting MSVC 14.52 should have been rejected.'
    Write-Output 'CMAKE_CACHE_INSTANCE_TOOLSET_AND_ISOLATION_REJECTIONS=PASS'

    $cmakeSource = Join-Path $sourceRoot 'CMakeLists.txt'
    [IO.File]::WriteAllText($cmakeSource, "cmake_minimum_required(VERSION 3.27)`nproject(AvelriStableGeneratorProbe LANGUAGES NONE)`n", (New-Object Text.UTF8Encoding($false)))
    $cmakeProbeBuild = Join-Path $temporaryRoot 'cmake-generator-probe'
    $cmakeEnvironment = Get-AvelriPinnedCMakeEnvironment -Toolchain $toolchain
    $previousEnvironment = @{}
    foreach ($name in $cmakeEnvironment.Keys) {
        $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }
    try {
        foreach ($name in $cmakeEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, [string]$cmakeEnvironment[$name], 'Process')
        }
        $cmakeOutput = @(& $toolchain.CMakePath -S $sourceRoot -B $cmakeProbeBuild -A x64 2>&1)
        $cmakeExitCode = $LASTEXITCODE
        if ($cmakeExitCode -ne 0) { throw ('CMake generator probe failed: ' + (($cmakeOutput | ForEach-Object { $_.ToString() }) -join "`n")) }
    }
    finally {
        foreach ($name in $cmakeEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
        }
    }

    $probeCache = [IO.File]::ReadAllText((Join-Path $cmakeProbeBuild 'CMakeCache.txt'))
    foreach ($expected in @(
        'CMAKE_GENERATOR:INTERNAL=Visual Studio 18 2026'
        ('CMAKE_GENERATOR_INSTANCE:INTERNAL=' + $toolchain.VisualStudioRoot)
        'CMAKE_GENERATOR_PLATFORM:INTERNAL=x64'
        'CMAKE_GENERATOR_TOOLSET:INTERNAL=v145,version=14.50.35717,host=x64'
    )) {
        Assert-True ($probeCache.Contains($expected)) ('CMake did not persist the explicit generator selection: ' + $expected)
    }
    Write-Output 'CMAKE_STABLE_INSTANCE_AND_TOOLSET_CONFIGURATION=PASS'
}
finally {
    if ((Get-Item -LiteralPath $temporaryRoot -Force).FullName.StartsWith([IO.Path]::GetFullPath($env:TEMP), [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $temporaryRoot) -like 'avelri-i69-toolchain-tests-*') {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

$builder = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Build-ProductionInstaller.ps1'))
$productionInstaller = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'AvelriGestao-Production.iss'))
$requiredBuilderEvidence = @(
    'ValidatePattern',
    '[string]$SourceCommit',
    'git -C $repoRoot archive --format=zip --output=$sourceArchive $SourceCommit desktop',
    'MicrosoftRedistributionRightsConfirmed',
    'ExpectedAccessAgentPackageSha256',
    'updatePublicKeyBase64',
    'release.sig'
)
foreach ($evidence in $requiredBuilderEvidence) {
    if (-not $builder.Contains($evidence)) { throw ('A required existing packager guard was not found in the builder: ' + $evidence) }
}
if (-not $productionInstaller.Contains('9C02C9ED120F671B900E6B9471D890E3B2414117A7E53F4AE2E855466FA2B7C9') -or
    -not $builder.Contains('AccessAgentPackageSHA256')) {
    throw 'The pinned VC redistributable or AccessAgent package integrity guard is missing.'
}
Write-Output 'EXISTING_PACKAGER_GUARDS_PRESENT=PASS'
Write-Output 'PRODUCTION_TOOLCHAIN_TESTS=PASS'
