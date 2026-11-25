[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageArchivePath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$ExpectedPackageSha256
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$serviceName = 'AvelriAccessAgent'
$displayName = 'Avelri Access Agent'
$installParent = Join-Path $env:ProgramFiles 'Avelri'
$installDirectory = Join-Path $installParent 'AccessAgent'
$dataDirectory = Join-Path $env:ProgramData 'Avelri\AccessAgent'

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'A instalação do serviço AccessAgent requer autorização administrativa.'
    }
}

function Assert-NoReparsePoint([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    $current = $resolved
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Um caminho do AccessAgent aponta para um reparse point; instalação bloqueada.'
            }
        }

        $parent = Split-Path -Parent $current
        if ($parent -eq $current) { break }
        $current = $parent
    }
}

function Assert-PackageIntegrity([string]$ExtractedDirectory) {
    $manifestPath = Join-Path $ExtractedDirectory 'SHA256SUMS.txt'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw 'O pacote AccessAgent não contém o manifesto SHA-256.'
    }

    $manifestLines = @([IO.File]::ReadAllLines($manifestPath) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($manifestLines.Count -eq 0) { throw 'O manifesto SHA-256 do AccessAgent está vazio.' }

    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($line in $manifestLines) {
        if ($line -notmatch '^([0-9A-Fa-f]{64})\s{2,}(.+)$') {
            throw 'O manifesto SHA-256 do AccessAgent tem um formato inválido.'
        }

        $expectedHash = $Matches[1]
        $relativePath = $Matches[2].Trim()
        if ([IO.Path]::IsPathRooted($relativePath) -or $relativePath -match '(^|[\\/])\.\.([\\/]|$)') {
            throw 'O manifesto SHA-256 contém um caminho inválido.'
        }
        if (-not $seen.Add($relativePath)) { throw 'O manifesto SHA-256 contém caminhos duplicados.' }

        $filePath = [IO.Path]::GetFullPath((Join-Path $ExtractedDirectory $relativePath))
        $prefix = [IO.Path]::GetFullPath($ExtractedDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $filePath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
            throw 'O pacote AccessAgent não corresponde ao manifesto SHA-256.'
        }

        $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash
        if ($actualHash -ine $expectedHash) { throw 'Um arquivo do pacote AccessAgent falhou na verificação SHA-256.' }
    }

    $actualFiles = @(Get-ChildItem -LiteralPath $ExtractedDirectory -File -Force -Recurse)
    if ($actualFiles.Count -ne ($manifestLines.Count + 1)) {
        throw 'O pacote AccessAgent contém arquivos ausentes ou não manifestados.'
    }
}

function Set-AgentDataAcl([string]$Path) {
    $security = [Security.AccessControl.DirectorySecurity]::new()
    $security.SetAccessRuleProtection($true, $false)

    $inheritance = [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor
        [Security.AccessControl.InheritanceFlags]::ObjectInherit
    $propagation = [Security.AccessControl.PropagationFlags]::None
    $allow = [Security.AccessControl.AccessControlType]::Allow
    $rules = @(
        [Security.AccessControl.FileSystemAccessRule]::new(
            [Security.Principal.SecurityIdentifier]::new('S-1-5-18'),
            [Security.AccessControl.FileSystemRights]::FullControl,
            $inheritance, $propagation, $allow),
        [Security.AccessControl.FileSystemAccessRule]::new(
            [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'),
            [Security.AccessControl.FileSystemRights]::FullControl,
            $inheritance, $propagation, $allow),
        [Security.AccessControl.FileSystemAccessRule]::new(
            [Security.Principal.SecurityIdentifier]::new('S-1-5-19'),
            [Security.AccessControl.FileSystemRights]::Modify,
            $inheritance, $propagation, $allow)
    )

    foreach ($rule in $rules) { [void]$security.AddAccessRule($rule) }
    $security.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-18'))
    Set-Acl -LiteralPath $Path -AclObject $security
}

function Assert-ScSuccess([string]$Operation) {
    if ($LASTEXITCODE -ne 0) { throw "Não foi possível configurar o serviço AccessAgent: $Operation." }
}

Assert-Administrator

$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($null -ne $existingService) {
    Write-Output 'ACCESS_AGENT=EXISTING_SERVICE_PRESERVED'
    exit 10
}

$packagePath = [IO.Path]::GetFullPath($PackageArchivePath)
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    throw 'O pacote AccessAgent incluído no instalador não foi encontrado.'
}
if ((Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash -ine $ExpectedPackageSha256) {
    throw 'O pacote AccessAgent incluído no instalador falhou na verificação SHA-256.'
}

Assert-NoReparsePoint -Path $installParent
Assert-NoReparsePoint -Path $dataDirectory

if (Test-Path -LiteralPath $installDirectory) {
    throw 'Já existe uma pasta de instalação do AccessAgent sem o serviço esperado; os arquivos foram preservados e a instalação foi bloqueada.'
}
if (Test-Path -LiteralPath $dataDirectory) {
    $existingState = @(Get-ChildItem -LiteralPath $dataDirectory -Force -Recurse)
    if ($existingState.Count -gt 0) {
        throw 'Há estado local do AccessAgent sem serviço registrado; credenciais, cache e fila foram preservados e a instalação foi bloqueada.'
    }
    throw 'Já existe uma pasta de dados do AccessAgent sem o serviço esperado; a instalação foi bloqueada para preservar seu estado.'
}

$temporaryRoot = Join-Path $env:TEMP ('Avelri-AccessAgent-' + [Guid]::NewGuid().ToString('N'))
$stagingDirectory = Join-Path $installParent ('.AccessAgent-' + [Guid]::NewGuid().ToString('N') + '.new')
$serviceCreated = $false
$installDirectoryCreated = $false
$dataDirectoryCreated = $false

try {
    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
    Assert-NoReparsePoint -Path $temporaryRoot

    Expand-Archive -LiteralPath $packagePath -DestinationPath $temporaryRoot -Force
    Assert-PackageIntegrity -ExtractedDirectory $temporaryRoot

    $agentExecutable = Join-Path $temporaryRoot 'GymFlow.AccessAgent.exe'
    $settingsPath = Join-Path $temporaryRoot 'appsettings.json'
    if (-not (Test-Path -LiteralPath $agentExecutable -PathType Leaf) -or
        -not (Test-Path -LiteralPath $settingsPath -PathType Leaf) -or
        (Test-Path -LiteralPath (Join-Path $temporaryRoot 'appsettings.Development.json'))) {
        throw 'O pacote AccessAgent não contém os binários operacionais esperados.'
    }

    $settings = [IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json
    if ($settings.Devices.ToletusLiteNet2.EnablePhysicalPassageConfirmation -ne $false) {
        throw 'A confirmação física do AccessAgent não está desativada por padrão.'
    }

    New-Item -ItemType Directory -Path $installParent -Force | Out-Null
    New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
    $excludedFiles = @(
        'BUILD-INFO.txt', 'ON-SITE-INSTALLATION.md', 'SHA256SUMS.txt',
        'Install-AccessAgent.ps1', 'Install-AccessAgent.cmd',
        'Repair-AccessAgentPairing.ps1', 'Repair-AccessAgentPairing.cmd',
        'Uninstall-AccessAgent.ps1', 'Uninstall-AccessAgent.cmd'
    )
    Get-ChildItem -LiteralPath $temporaryRoot -File -Force | Where-Object { $_.Name -notin $excludedFiles } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $stagingDirectory
    }

    $stagedExecutable = Join-Path $stagingDirectory 'GymFlow.AccessAgent.exe'
    if (-not (Test-Path -LiteralPath $stagedExecutable -PathType Leaf)) {
        throw 'O executável do AccessAgent não foi copiado para a área de preparação.'
    }

    New-Item -ItemType Directory -Path $dataDirectory -Force | Out-Null
    $dataDirectoryCreated = $true
    Set-AgentDataAcl -Path $dataDirectory

    Move-Item -LiteralPath $stagingDirectory -Destination $installDirectory
    $installDirectoryCreated = $true
    $installedExecutable = Join-Path $installDirectory 'GymFlow.AccessAgent.exe'
    New-Service -Name $serviceName -BinaryPathName ('"' + $installedExecutable + '"') `
        -DisplayName $displayName -StartupType Disabled | Out-Null
    $serviceCreated = $true

    & sc.exe config $serviceName binPath= ('"' + $installedExecutable + '"') obj= 'NT AUTHORITY\LocalService' start= disabled | Out-Null
    Assert-ScSuccess 'conta LocalService e inicialização desativada'
    & sc.exe description $serviceName 'Agente local do Avelri para controle de acesso físico.' | Out-Null
    Assert-ScSuccess 'descrição'

    Write-Output 'ACCESS_AGENT=PROVISIONED_UNPAIRED'
    Write-Output 'SERVICE_START=DISABLED_UNTIL_ADMIN_PAIRING'
}
catch {
    if ($serviceCreated) {
        & sc.exe delete $serviceName | Out-Null
    }
    if ($installDirectoryCreated -and (Test-Path -LiteralPath $installDirectory)) {
        Remove-Item -LiteralPath $installDirectory -Recurse -Force
    }
    if ($dataDirectoryCreated -and (Test-Path -LiteralPath $dataDirectory) -and
        @(Get-ChildItem -LiteralPath $dataDirectory -Force -Recurse).Count -eq 0) {
        Remove-Item -LiteralPath $dataDirectory -Force
    }
    throw
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
