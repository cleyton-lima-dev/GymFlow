[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

$repoRoot =
    [System.IO.Path]::GetFullPath(
        (Join-Path $PSScriptRoot "..\..")
    )

$projectPath =
    Join-Path `
        $repoRoot `
        "src\GymFlow.AccessAgent\GymFlow.AccessAgent.csproj"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath =
        Join-Path `
            $repoRoot `
            "artifacts\access-agent\$Runtime"
}

$OutputPath =
    [System.IO.Path]::GetFullPath(
        $OutputPath
    )

if (Test-Path $OutputPath) {
    Remove-Item `
        $OutputPath `
        -Recurse `
        -Force
}

New-Item `
    -ItemType Directory `
    -Path $OutputPath `
    -Force |
    Out-Null

Write-Host "Publicando Avelri Access Agent..."
Write-Host "Runtime: $Runtime"
Write-Host "Destino: $OutputPath"
Write-Host ""

dotnet publish `
    $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $OutputPath

if ($LASTEXITCODE -ne 0) {
    throw "Falha ao publicar o Access Agent."
}

# A configuração Development aponta para localhost e
# não deve fazer parte do pacote operacional.
$developmentSettings =
    Join-Path `
        $OutputPath `
        "appsettings.Development.json"

if (Test-Path $developmentSettings) {
    Remove-Item `
        $developmentSettings `
        -Force
}

$operationalScripts = @(
    "Install-AccessAgent.ps1",
    "Install-AccessAgent.cmd",
    "Repair-AccessAgentPairing.ps1",
    "Repair-AccessAgentPairing.cmd",
    "Uninstall-AccessAgent.ps1",
    "Uninstall-AccessAgent.cmd"
)

foreach ($scriptName in $operationalScripts) {
    $source =
        Join-Path `
            $PSScriptRoot `
            $scriptName

    if (-not (Test-Path $source)) {
        throw "Script operacional ausente: $scriptName"
    }

    Copy-Item `
        $source `
        (Join-Path $OutputPath $scriptName) `
        -Force
}

$manifestPath =
    Join-Path `
        $OutputPath `
        "SHA256SUMS.txt"

$basePath =
    $OutputPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )

$manifestLines =
    Get-ChildItem `
        $OutputPath `
        -File `
        -Recurse |
    Where-Object {
        $_.FullName -ne $manifestPath
    } |
    Sort-Object FullName |
    ForEach-Object {
        $relativePath =
            $_.FullName.Substring(
                $basePath.Length + 1
            )

        $hash =
            (Get-FileHash `
                $_.FullName `
                -Algorithm SHA256).Hash

        "$hash  $relativePath"
    }

[System.IO.File]::WriteAllLines(
    $manifestPath,
    $manifestLines,
    [System.Text.UTF8Encoding]::new($false)
)

Write-Host ""
Write-Host "Pacote criado com sucesso."
Write-Host "Manifesto SHA-256:"
Write-Host $manifestPath