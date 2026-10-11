[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ApiBaseUrl
)

$ErrorActionPreference = "Stop"

$serviceName =
    "AvelriAccessAgent"

$displayName =
    "Avelri Access Agent"

$installDirectory =
    Join-Path `
        $env:ProgramFiles `
        "Avelri\AccessAgent"

$dataDirectory =
    Join-Path `
        $env:ProgramData `
        "Avelri\AccessAgent"

$credentialsPath =
    Join-Path `
        $dataDirectory `
        "credentials.dat"


function Assert-Administrator
{
    $identity =
        [System.Security.Principal.WindowsIdentity]::GetCurrent()

    $principal =
        New-Object `
            System.Security.Principal.WindowsPrincipal(
                $identity
            )

    if (-not $principal.IsInRole(
            [System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Execute este instalador em um PowerShell aberto como Administrador."
    }
}


function Get-NormalizedApiBaseUrl(
    [string]$Value
)
{
    $value =
        $Value.Trim()

    $uri =
        $null

    if (-not [System.Uri]::TryCreate(
            $value,
            [System.UriKind]::Absolute,
            [ref]$uri)) {
        throw "A URL da API é inválida."
    }

    $isHttps =
        $uri.Scheme -ieq "https"

    $isLocalHttp =
        $uri.Scheme -ieq "http" -and
        $uri.IsLoopback

    if (-not $isHttps -and
        -not $isLocalHttp) {
        throw "HTTPS é obrigatório fora do ambiente local."
    }

    return $value.TrimEnd("/")
}


function Test-PackageIntegrity
{
    $manifestPath =
        Join-Path `
            $PSScriptRoot `
            "SHA256SUMS.txt"

    if (-not (Test-Path $manifestPath)) {
        throw "SHA256SUMS.txt não encontrado no pacote."
    }

    foreach ($line in
        [System.IO.File]::ReadAllLines(
            $manifestPath
        )) {
        if ([string]::IsNullOrWhiteSpace(
                $line)) {
            continue
        }

        $parts =
            $line -split "\s{2,}", 2

        if ($parts.Count -ne 2) {
            throw "Manifesto SHA-256 inválido."
        }

        $expectedHash =
            $parts[0].Trim()

        $relativePath =
            $parts[1].Trim()

        $filePath =
            Join-Path `
                $PSScriptRoot `
                $relativePath

        if (-not (Test-Path $filePath)) {
            throw "Arquivo ausente no pacote: $relativePath"
        }

        $actualHash =
            (Get-FileHash `
                $filePath `
                -Algorithm SHA256).Hash

        if ($actualHash -ne
            $expectedHash) {
            throw "Falha de integridade no arquivo: $relativePath"
        }
    }
}


function Set-AgentDataAcl
{
    New-Item `
        -ItemType Directory `
        -Path $dataDirectory `
        -Force |
        Out-Null

    $security =
        New-Object `
            System.Security.AccessControl.DirectorySecurity

    # Remove permissões herdadas.
    $security.SetAccessRuleProtection(
        $true,
        $false
    )

    $inheritance =
        [System.Security.AccessControl.InheritanceFlags]::ContainerInherit `
        -bor `
        [System.Security.AccessControl.InheritanceFlags]::ObjectInherit

    $propagation =
        [System.Security.AccessControl.PropagationFlags]::None

    $allow =
        [System.Security.AccessControl.AccessControlType]::Allow

    $systemSid =
        New-Object `
            System.Security.Principal.SecurityIdentifier(
                "S-1-5-18"
            )

    $administratorsSid =
        New-Object `
            System.Security.Principal.SecurityIdentifier(
                "S-1-5-32-544"
            )

    $localServiceSid =
        New-Object `
            System.Security.Principal.SecurityIdentifier(
                "S-1-5-19"
            )

    $systemRule =
        New-Object `
            System.Security.AccessControl.FileSystemAccessRule(
                $systemSid,
                [System.Security.AccessControl.FileSystemRights]::FullControl,
                $inheritance,
                $propagation,
                $allow
            )

    $administratorsRule =
        New-Object `
            System.Security.AccessControl.FileSystemAccessRule(
                $administratorsSid,
                [System.Security.AccessControl.FileSystemRights]::FullControl,
                $inheritance,
                $propagation,
                $allow
            )

    $localServiceRule =
        New-Object `
            System.Security.AccessControl.FileSystemAccessRule(
                $localServiceSid,
                [System.Security.AccessControl.FileSystemRights]::Modify,
                $inheritance,
                $propagation,
                $allow
            )

    $security.AddAccessRule(
        $systemRule
    )

    $security.AddAccessRule(
        $administratorsRule
    )

    $security.AddAccessRule(
        $localServiceRule
    )

    $security.SetOwner(
        $systemSid
    )

    Set-Acl `
        -Path $dataDirectory `
        -AclObject $security
}


function Assert-ScSuccess(
    [string]$Operation
)
{
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao configurar o Windows Service: $Operation"
    }
}


Assert-Administrator

$normalizedApiBaseUrl =
    Get-NormalizedApiBaseUrl(
        $ApiBaseUrl
    )

Test-PackageIntegrity

$packagePath =
    [System.IO.Path]::GetFullPath(
        $PSScriptRoot
    )

$targetPath =
    [System.IO.Path]::GetFullPath(
        $installDirectory
    )

if ($packagePath.TrimEnd("\") -ieq
    $targetPath.TrimEnd("\")) {
    throw "Execute o instalador a partir do pacote de distribuição, não da pasta final em Program Files."
}

$existingService =
    Get-Service `
        -Name $serviceName `
        -ErrorAction SilentlyContinue

if ($existingService -and
    $existingService.Status -ne "Stopped") {
    Write-Host "Parando serviço existente..."

    Stop-Service `
        -Name $serviceName `
        -Force

    $existingService =
        Get-Service `
            -Name $serviceName

    $existingService.WaitForStatus(
        "Stopped",
        [TimeSpan]::FromSeconds(20)
    )
}

Write-Host "Copiando arquivos..."

if (Test-Path $installDirectory) {
    Remove-Item `
        $installDirectory `
        -Recurse `
        -Force
}

New-Item `
    -ItemType Directory `
    -Path $installDirectory `
    -Force |
    Out-Null

$excludedFiles = @(
    "Publish-AccessAgent.ps1",
    "Install-AccessAgent.ps1",
    "Install-AccessAgent.cmd",
    "Repair-AccessAgentPairing.ps1",
    "Repair-AccessAgentPairing.cmd",
    "Uninstall-AccessAgent.ps1",
    "Uninstall-AccessAgent.cmd",
    "SHA256SUMS.txt"
)

Get-ChildItem `
    $PSScriptRoot `
    -Force |
Where-Object {
    $_.Name -notin $excludedFiles
} |
ForEach-Object {
    Copy-Item `
        $_.FullName `
        $installDirectory `
        -Recurse `
        -Force
}

$exePath =
    Join-Path `
        $installDirectory `
        "GymFlow.AccessAgent.exe"

if (-not (Test-Path $exePath)) {
    throw "Executável do Access Agent não encontrado após a cópia."
}

Write-Host "Aplicando ACL segura em ProgramData..."

Set-AgentDataAcl

$existingService =
    Get-Service `
        -Name $serviceName `
        -ErrorAction SilentlyContinue

if (-not $existingService) {
    Write-Host "Criando Windows Service..."

    New-Service `
        -Name $serviceName `
        -BinaryPathName "`"$exePath`"" `
        -DisplayName $displayName `
        -StartupType Manual |
        Out-Null
}

# Mantém o serviço parado até o pareamento terminar.
& sc.exe config `
    $serviceName `
    binPath= "`"$exePath`"" `
    obj= "NT AUTHORITY\LocalService" `
    start= demand |
    Out-Null

Assert-ScSuccess "conta LocalService / inicialização manual"

& sc.exe description `
    $serviceName `
    "Agente local do Avelri para controle de acesso físico." |
    Out-Null

Assert-ScSuccess "descrição"

Write-Host ""
Write-Host "Serviço instalado e PARADO."
Write-Host "O próximo passo é o pareamento."
Write-Host ""

& $exePath `
    --pair `
    "--AvelriApi:BaseUrl=$normalizedApiBaseUrl"

if ($LASTEXITCODE -ne 0) {
    throw "O pareamento falhou. O serviço permanecerá parado e com inicialização manual."
}

if (-not (Test-Path $credentialsPath)) {
    throw "O pareamento terminou sem criar credentials.dat. O serviço permanecerá parado."
}

# Reaplica a ACL após a criação da credencial.
Set-AgentDataAcl

Write-Host ""
Write-Host "Pareamento concluído."
Write-Host "Configurando inicialização automática..."

& sc.exe config `
    $serviceName `
    start= delayed-auto |
    Out-Null

Assert-ScSuccess "inicialização automática atrasada"

& sc.exe failure `
    $serviceName `
    reset= 86400 `
    actions= "restart/5000/restart/15000/restart/60000" |
    Out-Null

Assert-ScSuccess "recuperação automática"

try {
    Start-Service `
        -Name $serviceName

    $service =
        Get-Service `
            -Name $serviceName

    $service.WaitForStatus(
        "Running",
        [TimeSpan]::FromSeconds(20)
    )
}
catch {
    & sc.exe config `
        $serviceName `
        start= demand |
        Out-Null

    throw "O serviço não conseguiu iniciar. Ele foi devolvido ao modo manual. $($_.Exception.Message)"
}

Write-Host ""
Write-Host "Avelri Access Agent instalado com sucesso."
Write-Host "Serviço: $serviceName"
Write-Host "Conta: NT AUTHORITY\LocalService"
Write-Host "Binários: $installDirectory"
Write-Host "Dados protegidos: $dataDirectory"