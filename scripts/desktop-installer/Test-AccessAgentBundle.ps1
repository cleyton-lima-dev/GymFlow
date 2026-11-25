[CmdletBinding()]
param(
    [string]$PackagePath = (Join-Path $PSScriptRoot '..\..\artifacts\access-agent\Avelri-AccessAgent-2026.11.24-win-x64.zip'),
    [string]$ExpectedPackageSha256 = '4204750233E9AC59997D6BDD7134873AFC41CC20A791A8883C89B29D2A2176A2'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$productionIssPath = Join-Path $PSScriptRoot 'AvelriGestao-Production.iss'
$buildScriptPath = Join-Path $PSScriptRoot 'Build-ProductionInstaller.ps1'
$provisionerPath = Join-Path $repoRoot 'scripts\access-agent\Provision-AccessAgentFromBundle.ps1'
$pairingActionPath = Join-Path $repoRoot 'scripts\access-agent\Pair-AccessAgent.ps1'
$desktopPubspecPath = Join-Path $repoRoot 'desktop\pubspec.yaml'

foreach ($path in @($PackagePath, $productionIssPath, $buildScriptPath, $provisionerPath, $pairingActionPath, $desktopPubspecPath)) {
    Assert-Condition (Test-Path -LiteralPath $path -PathType Leaf) 'An AccessAgent bundle validation input is missing.'
}

$package = Get-Item -LiteralPath $PackagePath
$packageHash = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash
Assert-Condition ($packageHash -ieq $ExpectedPackageSha256) 'The prevalidated AccessAgent ZIP hash changed.'

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
try {
    $entries = @($archive.Entries)
    $manifestEntry = $archive.GetEntry('SHA256SUMS.txt')
    $settingsEntry = $archive.GetEntry('appsettings.json')
    $buildInfoEntry = $archive.GetEntry('BUILD-INFO.txt')
    Assert-Condition ($null -ne $manifestEntry -and $null -ne $settingsEntry -and $null -ne $buildInfoEntry) 'The Agent ZIP is missing required operational metadata.'
    Assert-Condition ($null -eq $archive.GetEntry('appsettings.Development.json')) 'The Agent ZIP includes Development settings.'

    $reader = [IO.StreamReader]::new($manifestEntry.Open())
    try { $manifestLines = @($reader.ReadToEnd() -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) }
    finally { $reader.Dispose() }
    Assert-Condition ($manifestLines.Count -gt 0 -and $entries.Count -eq ($manifestLines.Count + 1)) 'The Agent ZIP inventory differs from its hash manifest.'

    $manifestPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($line in $manifestLines) {
        Assert-Condition ($line -match '^([0-9A-Fa-f]{64})\s{2,}(.+)$') 'The Agent SHA-256 manifest is malformed.'
        $expectedHash = $Matches[1]
        $relativePath = $Matches[2].Trim().Replace('\', '/')
        Assert-Condition (-not $relativePath.StartsWith('/') -and $relativePath -notmatch '(^|/)\.\.(/|$)') 'The Agent manifest contains an unsafe path.'
        Assert-Condition ($manifestPaths.Add($relativePath)) 'The Agent manifest contains duplicate paths.'
        $entry = $archive.GetEntry($relativePath)
        Assert-Condition ($null -ne $entry -and $entry.FullName -ceq $relativePath) 'An Agent manifest entry is missing from the ZIP.'
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $actualHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $sha.Dispose(); $stream.Dispose() }
        Assert-Condition ($actualHash -ieq $expectedHash) 'An Agent ZIP member failed the SHA-256 check.'
    }

    $settingsReader = [IO.StreamReader]::new($settingsEntry.Open())
    try { $settings = $settingsReader.ReadToEnd() | ConvertFrom-Json }
    finally { $settingsReader.Dispose() }
    Assert-Condition ($settings.Devices.ToletusLiteNet2.EnablePhysicalPassageConfirmation -eq $false) 'Physical passage confirmation is not disabled by default.'

    $buildInfoReader = [IO.StreamReader]::new($buildInfoEntry.Open())
    try { $buildInfo = $buildInfoReader.ReadToEnd() }
    finally { $buildInfoReader.Dispose() }
    Assert-Condition ($buildInfo -match '(?m)^RuntimeIdentifier=win-x64\s*$' -and
        $buildInfo -match '(?m)^Deployment=self-contained\s*$' -and
        $buildInfo -match '(?m)^ApiBaseUrl=specified at installation; not embedded\s*$') 'The Agent package provenance does not match the validated win-x64 distribution.'
}
finally { $archive.Dispose() }

$productionIss = [IO.File]::ReadAllText($productionIssPath)
$buildScript = [IO.File]::ReadAllText($buildScriptPath)
$provisioner = [IO.File]::ReadAllText($provisionerPath)
$pairingAction = [IO.File]::ReadAllText($pairingActionPath)
$pubspec = [IO.File]::ReadAllText($desktopPubspecPath)

Assert-Condition ($productionIss -match '(?m)^AppId=\{\{B46B7D1A-6E54-4C0A-A1E2-5F41C6D92837\}') 'Production AppId changed.'
Assert-Condition ($productionIss -match '(?m)^DefaultDirName=\{localappdata\}\\Programs\\Avelri\\Gestao$') 'Production install directory changed.'
Assert-Condition ($productionIss -match '(?m)^PrivilegesRequired=lowest$' -and $productionIss -match "ShellExec\('runas'") 'The installer privilege boundary or controlled elevation flow changed.'
Assert-Condition ($productionIss -match 'AccessAgentPackageSha256' -and $productionIss -match 'Parear Avelri AccessAgent' -and $productionIss -match '-NoExit') 'The installer does not pin the bundle or provide the post-install Admin pairing action.'
Assert-Condition ($productionIss -notmatch '(?m)^\[(Run|UninstallDelete)\]$') 'The installer must not auto-launch or delete application support data.'

$existingServiceIndex = $provisioner.IndexOf('if ($null -ne $existingService)', [StringComparison]::Ordinal)
$packageVerificationIndex = $provisioner.IndexOf('Get-FileHash -LiteralPath $packagePath', [StringComparison]::Ordinal)
$stoppedServiceIndex = $provisioner.IndexOf('Stop-Service', [StringComparison]::OrdinalIgnoreCase)
Assert-Condition ($existingServiceIndex -ge 0 -and $packageVerificationIndex -gt $existingServiceIndex) 'The provisioner does not preserve an already registered service before processing the bundle.'
Assert-Condition ($stoppedServiceIndex -lt 0 -and
    $provisioner -match '\$dataDirectoryCreated -and \(Test-Path -LiteralPath \$dataDirectory\)' -and
    $provisioner -match 'Get-ChildItem -LiteralPath \$dataDirectory -Force -Recurse\)\.Count -eq 0') 'The provisioner can interrupt or delete existing Agent state.'
Assert-Condition ($provisioner -match 'ACCESS_AGENT=EXISTING_SERVICE_PRESERVED' -and
    $provisioner -match 'ACCESS_AGENT=PROVISIONED_UNPAIRED' -and
    $provisioner -match 'StartupType Disabled' -and
    $provisioner -match 'start= disabled') 'New Agent service is not explicitly provisioned stopped and disabled.'
Assert-Condition ($provisioner -notmatch '--pair') 'The installer provisioner must not pair or call the API.'

$pairIndex = $pairingAction.IndexOf('--pair', [StringComparison]::Ordinal)
$startIndex = $pairingAction.IndexOf('start= delayed-auto', [StringComparison]::Ordinal)
Assert-Condition ($pairIndex -ge 0 -and $startIndex -gt $pairIndex) 'The service can start before the Admin pairing action.'
Assert-Condition ($pairingAction -match 'Test-Path -LiteralPath \$credentialsPath' -and
    $pairingAction -match 'Test-Path -LiteralPath \$executablePath' -and
    $pairingAction -match '\$service\.Status -ne ''Stopped''') 'The pairing action lacks checks against replacing an existing Agent.'
Assert-Condition ($pairingAction -notmatch '(?m)^\s*Stop-Service\b' -and $pairingAction -notmatch '(?m)^\s*Remove-Item\b') 'The pairing action can interrupt or delete existing Agent data.'

Assert-Condition ($buildScript -match '\$ExpectedAccessAgentPackageSha256' -and
    $buildScript -match 'AccessAgentPackageSHA256=' -and
    $buildScript -match 'AccessAgentProvisionerSHA256=') 'The production build manifest does not record the bundled Agent provenance.'
$versionMatch = [regex]::Match($pubspec, '(?m)^version:\s*(?<version>\d+\.\d+\.\d+)\+(?<build>\d+)\s*$')
Assert-Condition ($versionMatch.Success -and
    ([version]$versionMatch.Groups['version'].Value -gt [version]'1.0.0' -or
     ([version]$versionMatch.Groups['version'].Value -eq [version]'1.0.0' -and [int]$versionMatch.Groups['build'].Value -gt 1))) 'The Desktop version does not exceed the published 1.0.0+1 release.'

Write-Output 'ACCESS_AGENT_PACKAGE=PASS; manifest, runtime, API isolation, and defaults verified.'
Write-Output 'NEW_INSTALL_POLICY=PASS; service provision is guarded and remains disabled/unpaired.'
Write-Output 'UPGRADE_WITHOUT_AGENT_POLICY=PASS; AppId and install directory remain stable.'
Write-Output 'EXISTING_AGENT_POLICY=PASS; existing service/data are not updated, stopped, or removed.'
Write-Output 'PREPAIR_COMMUNICATION_POLICY=PASS; service start follows Admin pairing and no device is provisioned here.'
Write-Output 'SOURCE_LEVEL_ONLY=TRUE; no Windows service or real installation was changed.'
