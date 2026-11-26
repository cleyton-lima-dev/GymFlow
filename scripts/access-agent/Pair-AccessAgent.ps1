[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ApiBaseUrl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$serviceName = 'AvelriAccessAgent'
$dataDirectory = Join-Path $env:ProgramData 'Avelri\AccessAgent'
$credentialsPath = Join-Path $dataDirectory 'credentials.dat'

function Get-NormalizedApiBaseUrl([string]$Value) {
    $uri = $null
    $candidate = $Value.Trim()
    if (-not [Uri]::TryCreate($candidate, [UriKind]::Absolute, [ref]$uri)) {
        throw 'A URL da API é inválida.'
    }
    $isHttps = $uri.Scheme -ieq 'https'
    $isLocalHttp = $uri.Scheme -ieq 'http' -and $uri.IsLoopback
    if (-not $isHttps -and -not $isLocalHttp) {
        throw 'HTTPS é obrigatório fora do ambiente local.'
    }
    return $candidate.TrimEnd('/')
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Set-ServiceStartDisabled {
    & sc.exe config $serviceName start= disabled | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'O serviço foi mantido parado, mas não foi possível confirmar o modo desativado.' }
}

$normalizedApiBaseUrl = Get-NormalizedApiBaseUrl -Value $ApiBaseUrl
if (-not (Test-IsAdministrator)) {
    $powershellPath = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $arguments = '-NoExit -NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath + '" -ApiBaseUrl "' + $normalizedApiBaseUrl + '"'
    try {
        $elevated = Start-Process -FilePath $powershellPath -Verb RunAs -ArgumentList $arguments -PassThru -Wait
        exit $elevated.ExitCode
    }
    catch {
        Write-Error 'A autorização administrativa foi cancelada; o AccessAgent permaneceu inalterado.'
        exit 1223
    }
}

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($null -eq $service) { throw 'O serviço AccessAgent ainda não foi provisionado pelo instalador.' }
if ($service.Status -ne 'Stopped') {
    throw 'O serviço AccessAgent está em execução. O pareamento foi bloqueado sem interromper o agente.'
}
if (Test-Path -LiteralPath $credentialsPath) {
    throw 'Já existe credencial do AccessAgent. Nenhuma credencial ou dado foi substituído.'
}
$serviceInfo = Get-CimInstance -ClassName Win32_Service -Filter "Name='$serviceName'"
if ($null -eq $serviceInfo -or $serviceInfo.StartMode -ne 'Disabled') {
    throw 'O serviço não está no estado inicial desativado; o pareamento foi bloqueado.'
}

# Accept the registered image from either supported install directory.
$executablePath = $null
foreach ($root in @($env:ProgramW6432, [Environment]::GetEnvironmentVariable('ProgramFiles(x86)'))) {
    if ([string]::IsNullOrWhiteSpace($root)) { continue }
    $candidate = Join-Path $root 'Avelri\AccessAgent\GymFlow.AccessAgent.exe'
    if ($serviceInfo.PathName -ieq $candidate -or $serviceInfo.PathName -ieq ('"' + $candidate + '"')) {
        $executablePath = $candidate
        break
    }
}
if ($null -eq $executablePath -or -not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw 'O executável registrado do AccessAgent não foi encontrado em um diretório de instalação suportado.'
}

Write-Host 'Pareamento do Avelri AccessAgent'
Write-Host 'Gere um código temporário no Avelri Gestão e digite-o somente neste terminal.'
Write-Host 'O pareamento não configura nem habilita a comunicação física com a catraca.'

& $executablePath --pair "--AvelriApi:BaseUrl=$normalizedApiBaseUrl"
$pairExitCode = $LASTEXITCODE
if ($pairExitCode -ne 0) {
    Set-ServiceStartDisabled
    throw 'O pareamento não foi concluído; o serviço continua desativado e parado.'
}
if (-not (Test-Path -LiteralPath $credentialsPath -PathType Leaf)) {
    Set-ServiceStartDisabled
    throw 'O pareamento não criou a credencial protegida; o serviço continua desativado e parado.'
}

& sc.exe config $serviceName start= delayed-auto | Out-Null
if ($LASTEXITCODE -ne 0) {
    Set-ServiceStartDisabled
    throw 'A credencial pareada foi preservada; o serviço continua parado até a inicialização ser corrigida.'
}
& sc.exe failure $serviceName reset= 86400 actions= 'restart/5000/restart/15000/restart/60000' | Out-Null
if ($LASTEXITCODE -ne 0) {
    Set-ServiceStartDisabled
    throw 'A credencial pareada foi preservada; o serviço continua parado porque a recuperação automática não foi configurada.'
}

try {
    Start-Service -Name $serviceName
    $service = Get-Service -Name $serviceName
    $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(20))
}
catch {
    Set-ServiceStartDisabled
    throw 'A credencial pareada foi preservada, mas o serviço não iniciou; ele permaneceu desativado.'
}

Write-Output 'ACCESS_AGENT_PAIRING=PASS'
Write-Output 'SERVICE=RUNNING_AFTER_ADMIN_PAIRING'
Write-Output 'PHYSICAL_DEVICE_CONFIGURATION=UNCHANGED'
