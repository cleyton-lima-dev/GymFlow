[CmdletBinding()]
param(
    [switch]$PurgeData
)

$ErrorActionPreference = "Stop"

$serviceName =
    "AvelriAccessAgent"

$installDirectory =
    Join-Path `
        $env:ProgramFiles `
        "Avelri\AccessAgent"

$dataDirectory =
    Join-Path `
        $env:ProgramData `
        "Avelri\AccessAgent"


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


Assert-Administrator

$service =
    Get-Service `
        -Name $serviceName `
        -ErrorAction SilentlyContinue

if ($service) {
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

    & sc.exe delete `
        $serviceName |
        Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw "Não foi possível remover o Windows Service."
    }
}

if (Test-Path $installDirectory) {
    Remove-Item `
        $installDirectory `
        -Recurse `
        -Force
}

if ($PurgeData) {
    if (Test-Path $dataDirectory) {
        Remove-Item `
            $dataDirectory `
            -Recurse `
            -Force
    }

    Write-Host "Serviço, binários e dados locais removidos."
}
else {
    Write-Host "Serviço e binários removidos."
    Write-Host "Os dados em ProgramData foram preservados."
    Write-Host "Use -PurgeData somente quando quiser apagar credenciais, cache e fila offline."
}