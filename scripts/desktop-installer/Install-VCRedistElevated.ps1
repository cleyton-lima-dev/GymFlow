[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RedistributablePath
)

$ErrorActionPreference = 'Stop'
$exitCode = 1
try {
    if (-not (Test-Path -LiteralPath $RedistributablePath -PathType Leaf)) {
        $exitCode = 2
    }
    else {
        # Only this child process asks for UAC; the Inno installer remains per-user.
        $process = Start-Process -FilePath $RedistributablePath `
            -ArgumentList @('/install', '/passive', '/norestart') `
            -Verb RunAs -Wait -PassThru -ErrorAction Stop
        $exitCode = [int]$process.ExitCode
    }
}
catch {
    $cause = $_.Exception
    while ($null -ne $cause) {
        if ($cause -is [System.ComponentModel.Win32Exception] -and
            $cause.NativeErrorCode -eq 1223) {
            $exitCode = 1223
            break
        }
        $cause = $cause.InnerException
    }
}
exit $exitCode
