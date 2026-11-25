Set-StrictMode -Version Latest

$script:AvelriExpectedToolsetVersion = '14.50.35717'
$script:AvelriExpectedCompilerVersion = '19.50.35740.0'
$script:AvelriExpectedGenerator = 'Visual Studio 18 2026'
$script:AvelriExpectedGeneratorToolset = 'v145,version=14.50.35717,host=x64'

function Get-AvelriFullPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
}

function Test-AvelriPathEqual {
    param(
        [Parameter(Mandatory = $true)][string]$Left,
        [Parameter(Mandatory = $true)][string]$Right
    )

    return [string]::Equals((Get-AvelriFullPath $Left), (Get-AvelriFullPath $Right), [StringComparison]::OrdinalIgnoreCase)
}

function Select-AvelriStableVisualStudioInstance {
    [CmdletBinding()]
    param([AllowEmptyCollection()][object[]]$Instances = @())

    $eligible = @($Instances | Where-Object {
        $_.productId -ceq 'Microsoft.VisualStudio.Product.Community' -and
        $_.channelId -ceq 'VisualStudio.18.Release' -and
        $_.catalog.buildBranch -ceq 'stable' -and
        $_.isPrerelease -eq $false -and
        $_.isComplete -eq $true -and
        $_.isLaunchable -eq $true -and
        $_.installationVersion -match '^18\.' -and
        -not [string]::IsNullOrWhiteSpace([string]$_.installationPath)
    })

    if ($eligible.Count -ne 1) {
        throw ('Expected exactly one complete, launchable Visual Studio 18 Community Stable instance; found ' + $eligible.Count + '.')
    }

    return $eligible[0]
}

function Get-AvelriVsWherePath {
    $roots = @()
    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $roots += Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    }
    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $roots += Join-Path $env:ProgramFiles 'Microsoft Visual Studio\Installer\vswhere.exe'
    }

    foreach ($candidate in $roots) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }

    throw 'vswhere.exe is unavailable; a Stable Visual Studio instance cannot be selected safely.'
}

function Invoke-AvelriVsWhereJson {
    param(
        [Parameter(Mandatory = $true)][string]$VsWherePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $output = @(& $VsWherePath @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw ('vswhere failed with exit code ' + $exitCode + '.')
    }

    $text = (@($output) | ForEach-Object { $_.ToString() }) -join "`n"
    if ([string]::IsNullOrWhiteSpace($text)) { return @() }

    try { return @($text | ConvertFrom-Json) }
    catch { throw 'vswhere returned invalid JSON; Visual Studio selection was aborted.' }
}

function Resolve-AvelriStableVisualStudio {
    [CmdletBinding()]
    param()

    $vswhere = Get-AvelriVsWherePath
    $baseArguments = @('-all', '-products', '*', '-version', '[18.0,19.0)', '-format', 'json', '-utf8')
    $requiredComponents = @(
        'Microsoft.VisualStudio.Workload.NativeDesktop',
        'Microsoft.VisualStudio.Component.VC.Tools.x86.x64',
        'Microsoft.VisualStudio.Component.VC.CMake.Project',
        'Microsoft.VisualStudio.Component.Windows11SDK.26100'
    )
    $stableArguments = $baseArguments + @('-requires') + $requiredComponents
    $stableInstances = Invoke-AvelriVsWhereJson -VsWherePath $vswhere -Arguments $stableArguments
    $selected = Select-AvelriStableVisualStudioInstance -Instances $stableInstances

    $allArguments = @('-all', '-prerelease', '-products', '*', '-version', '[18.0,19.0)', '-format', 'json', '-utf8')
    $allInstances = Invoke-AvelriVsWhereJson -VsWherePath $vswhere -Arguments $allArguments
    $selectedPath = Get-AvelriFullPath ([string]$selected.installationPath)
    $matchingRecords = @($allInstances | Where-Object {
        -not [string]::IsNullOrWhiteSpace([string]$_.installationPath) -and
        (Test-AvelriPathEqual -Left ([string]$_.installationPath) -Right $selectedPath)
    })
    if ($matchingRecords.Count -ne 1 -or
        $matchingRecords[0].isPrerelease -ne $false -or
        $matchingRecords[0].channelId -cne 'VisualStudio.18.Release') {
        throw 'The selected Visual Studio directory is not uniquely confirmed as a non-prerelease Stable installation.'
    }

    $selected | Add-Member -NotePropertyName AvelriVsWherePath -NotePropertyValue $vswhere -Force
    return $selected
}

function Assert-AvelriCompilerIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$ExpectedCompilerPath,
        [Parameter(Mandatory = $true)][string]$ActualCompilerPath,
        [Parameter(Mandatory = $true)][string]$ExpectedCompilerVersion,
        [Parameter(Mandatory = $true)][string]$ActualCompilerVersion,
        [Parameter(Mandatory = $true)][string]$ExpectedToolsetVersion,
        [Parameter(Mandatory = $true)][string]$ActualToolsetVersion
    )

    if (-not (Test-AvelriPathEqual -Left $ExpectedCompilerPath -Right $ActualCompilerPath) -or
        $ActualCompilerVersion -cne $ExpectedCompilerVersion -or
        $ActualToolsetVersion -cne $ExpectedToolsetVersion) {
        throw 'The selected compiler path, file version, or MSVC toolset version differs from the pinned Stable MSVC 14.50 toolchain.'
    }
}

function Get-AvelriStableToolchain {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][object]$VisualStudioInstance)

    $stableRoot = Get-AvelriFullPath ([string]$VisualStudioInstance.installationPath)
    $toolsetRoot = Join-Path $stableRoot ('VC\Tools\MSVC\' + $script:AvelriExpectedToolsetVersion)
    $compilerPath = Join-Path $toolsetRoot 'bin\Hostx64\x64\cl.exe'
    $atlHeader = Join-Path $toolsetRoot 'atlmfc\include\atlstr.h'
    $vcvars = Join-Path $stableRoot 'VC\Auxiliary\Build\vcvars64.bat'
    $cmake = Join-Path $stableRoot 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
    $msbuild = Join-Path $stableRoot 'MSBuild\Current\Bin\MSBuild.exe'

    foreach ($requiredFile in @($compilerPath, $atlHeader, $vcvars, $cmake, $msbuild)) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw ('Required component is missing from the selected Stable Visual Studio installation: ' + $requiredFile)
        }
    }

    $compilerFileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($compilerPath).FileVersion
    Assert-AvelriCompilerIdentity `
        -ExpectedCompilerPath $compilerPath `
        -ActualCompilerPath $compilerPath `
        -ExpectedCompilerVersion $script:AvelriExpectedCompilerVersion `
        -ActualCompilerVersion $compilerFileVersion `
        -ExpectedToolsetVersion $script:AvelriExpectedToolsetVersion `
        -ActualToolsetVersion $script:AvelriExpectedToolsetVersion

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\Include'
    $sdkVersions = @()
    if (Test-Path -LiteralPath $kitsRoot -PathType Container) {
        $sdkVersions = @(Get-ChildItem -LiteralPath $kitsRoot -Directory | Where-Object {
            $_.Name -match '^10\.0\.\d+\.0$' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'um\Windows.h') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'shared\winapifamily.h') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'ucrt\stdio.h') -PathType Leaf)
        } | Sort-Object { [Version]$_.Name } -Descending | ForEach-Object { $_.Name })
    }
    if ($sdkVersions.Count -eq 0) {
        throw 'No complete Windows 10/11 SDK include tree was found; Stable toolchain validation failed.'
    }

    return [pscustomobject]@{
        VisualStudioInstance = $VisualStudioInstance
        VisualStudioRoot = $stableRoot
        ToolsetVersion = $script:AvelriExpectedToolsetVersion
        CompilerVersion = $compilerFileVersion
        CompilerPath = (Get-AvelriFullPath $compilerPath)
        AtlHeader = (Get-AvelriFullPath $atlHeader)
        CMakePath = (Get-AvelriFullPath $cmake)
        MSBuildPath = (Get-AvelriFullPath $msbuild)
        WindowsSdkVersions = $sdkVersions
    }
}

function Get-AvelriPinnedCMakeEnvironment {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][object]$Toolchain)

    return [ordered]@{
        CMAKE_GENERATOR = $script:AvelriExpectedGenerator
        CMAKE_GENERATOR_INSTANCE = [string]$Toolchain.VisualStudioRoot
        CMAKE_GENERATOR_TOOLSET = $script:AvelriExpectedGeneratorToolset
    }
}

function Get-AvelriCMakeCacheValue {
    param(
        [Parameter(Mandatory = $true)][string]$CacheText,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $namePattern = [regex]::Escape($Name)
    $match = [regex]::Match($CacheText, '(?m)^' + $namePattern + ':[^=\r\n]*=(?<value>[^\r\n]*)\s*$')
    if (-not $match.Success) { return $null }
    return $match.Groups['value'].Value
}

function Assert-AvelriCMakeProvenance {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$CMakeCachePath,
        [Parameter(Mandatory = $true)][string]$CompilerInfoPath,
        [Parameter(Mandatory = $true)][string]$FreshBuildRoot,
        [Parameter(Mandatory = $true)][object]$Toolchain
    )

    foreach ($path in @($CMakeCachePath, $CompilerInfoPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw ('CMake compiler provenance is incomplete; missing file: ' + $path)
        }
        $fullPath = Get-AvelriFullPath $path
        $rootPath = Get-AvelriFullPath $FreshBuildRoot
        if (-not $fullPath.StartsWith(($rootPath + [IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase)) {
            throw 'CMake provenance files are outside the newly isolated Windows build tree.'
        }
    }

    $cache = [IO.File]::ReadAllText($CMakeCachePath)
    $expectedCacheValues = [ordered]@{
        CMAKE_GENERATOR = $script:AvelriExpectedGenerator
        CMAKE_GENERATOR_INSTANCE = [string]$Toolchain.VisualStudioRoot
        CMAKE_GENERATOR_TOOLSET = $script:AvelriExpectedGeneratorToolset
        CMAKE_GENERATOR_PLATFORM = 'x64'
        CMAKE_VS_PLATFORM_TOOLSET = 'v145'
        CMAKE_VS_PLATFORM_TOOLSET_VERSION = $script:AvelriExpectedToolsetVersion
    }
    foreach ($name in $expectedCacheValues.Keys) {
        $actual = Get-AvelriCMakeCacheValue -CacheText $cache -Name $name
        if ($name -eq 'CMAKE_GENERATOR_INSTANCE' -and -not [string]::IsNullOrWhiteSpace($actual)) {
            if (-not (Test-AvelriPathEqual -Left $actual -Right ([string]$expectedCacheValues[$name]))) {
                throw ('CMake cache value ' + $name + ' does not match the pinned Stable instance.')
            }
        }
        elseif ($actual -cne [string]$expectedCacheValues[$name]) {
            throw ('CMake cache value ' + $name + ' differs from the pinned Stable generator/toolset configuration.')
        }
    }

    $compilerInfo = [IO.File]::ReadAllText($CompilerInfoPath)
    $idMatch = [regex]::Match($compilerInfo, '(?m)^set\(CMAKE_CXX_COMPILER_ID "(?<value>[^"]+)"\)\s*$')
    $versionMatch = [regex]::Match($compilerInfo, '(?m)^set\(CMAKE_CXX_COMPILER_VERSION "(?<value>[^"]+)"\)\s*$')
    $pathMatch = [regex]::Match($compilerInfo, '(?m)^set\(CMAKE_CXX_COMPILER "(?<value>[^"]+)"\)\s*$')
    if (-not $idMatch.Success -or $idMatch.Groups['value'].Value -cne 'MSVC' -or
        -not $versionMatch.Success -or -not $pathMatch.Success) {
        throw 'CMake compiler identity is missing or is not MSVC.'
    }

    $actualCompilerPath = $pathMatch.Groups['value'].Value -replace '/', '\'
    Assert-AvelriCompilerIdentity `
        -ExpectedCompilerPath $Toolchain.CompilerPath `
        -ActualCompilerPath $actualCompilerPath `
        -ExpectedCompilerVersion $script:AvelriExpectedCompilerVersion `
        -ActualCompilerVersion $versionMatch.Groups['value'].Value `
        -ExpectedToolsetVersion $script:AvelriExpectedToolsetVersion `
        -ActualToolsetVersion ([string]$expectedCacheValues['CMAKE_VS_PLATFORM_TOOLSET_VERSION'])

    return [pscustomobject]@{
        Generator = $script:AvelriExpectedGenerator
        Instance = [string]$Toolchain.VisualStudioRoot
        Toolset = $script:AvelriExpectedGeneratorToolset
        Compiler = (Get-AvelriFullPath $actualCompilerPath)
        CompilerVersion = $versionMatch.Groups['value'].Value
        BuildRoot = (Get-AvelriFullPath $FreshBuildRoot)
    }
}

Export-ModuleMember -Function @(
    'Select-AvelriStableVisualStudioInstance',
    'Resolve-AvelriStableVisualStudio',
    'Test-AvelriPathEqual',
    'Assert-AvelriCompilerIdentity',
    'Get-AvelriStableToolchain',
    'Get-AvelriPinnedCMakeEnvironment',
    'Assert-AvelriCMakeProvenance'
)
