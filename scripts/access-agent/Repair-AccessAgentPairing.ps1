[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ApiBaseUrl
)

$ErrorActionPreference = "Stop"

$serviceName =
    "AvelriAccessAgent"

$installDirectory =
    Join-Path `
        $env:ProgramFiles `
        "Avelri\AccessAgent"

$exePath =
    Join-Path `
        $installDirectory `
        "GymFlow.AccessAgent.exe"


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
        throw "Execute este script em um PowerShell aberto como Administrador."
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


Assert-Administrator

$normalizedApiBaseUrl =
    Get-NormalizedApiBaseUrl(
        $ApiBaseUrl
    )

if (-not (Test-Path $exePath)) {
    throw "Access Agent não instalado em $installDirectory"
}

$service =
    Get-Service `
        -Name $serviceName `
        -ErrorAction SilentlyContinue

if (-not $service) {
    throw "Windows Service $serviceName não encontrado."
}

if ($service.Status -ne "Stopped") {
    Stop-Service `
        -Name $serviceName `
        -Force

    $service =
        Get-Service `
            -Name $serviceName

    $service.WaitForStatus(
        "Stopped",
        [TimeSpan]::FromSeconds(20)
    )
}

Write-Host ""
Write-Host "O serviço está parado."
Write-Host "Iniciando reparo de pareamento..."
Write-Host ""

& $exePath `
    --pair `
    "--AvelriApi:BaseUrl=$normalizedApiBaseUrl"

if ($LASTEXITCODE -ne 0) {
    throw "O reparo falhou. O serviço permanecerá parado."
}

& sc.exe config `
    $serviceName `
    start= delayed-auto |
    Out-Null

if ($LASTEXITCODE -ne 0) {
    throw "O pareamento foi concluído, mas não foi possível configurar a inicialização automática."
}

Start-Service `
    -Name $serviceName

$service =
    Get-Service `
        -Name $serviceName

$service.WaitForStatus(
    "Running",
    [TimeSpan]::FromSeconds(20)
)

Write-Host ""
Write-Host "Repareamento concluído."
Write-Host "O Windows Service voltou a executar."