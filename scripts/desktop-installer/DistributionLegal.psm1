Set-StrictMode -Version Latest

function Resolve-AvelriLegalInputPath {
    param([string]$BaseDirectory, [string]$Path)
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $BaseDirectory $Path))
}

function Get-AvelriApprovedDistributionLegalInputs {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$ApprovalPath)

    $approvalFile = Get-Item -LiteralPath $ApprovalPath -ErrorAction Stop
    $approval = [IO.File]::ReadAllText($approvalFile.FullName) | ConvertFrom-Json
    if ($approval.schemaVersion -ne 1 -or $approval.status -cne 'APPROVED' -or
        $approval.commercialUseApproved -isnot [bool] -or
        $approval.licensorIdentityCompleted -isnot [bool] -or
        $approval.technicalValidityApproved -isnot [bool] -or
        $approval.commercialUseApproved -ne $true -or
        $approval.licensorIdentityCompleted -ne $true -or
        $approval.technicalValidityApproved -ne $true) {
        throw 'Final human approval, private licensor identification and technical-validity approval are required; draft commercial use is blocked.'
    }
    if ($approval.revision -cnotmatch '^\d+(\.\d+)+$' -or $approval.language -cne 'pt-BR' -or
        $approval.termsSha256 -cnotmatch '^[0-9A-F]{64}$') {
        throw 'Invalid approved terms revision, language or SHA-256.'
    }
    $termsPath = Resolve-AvelriLegalInputPath -BaseDirectory $approvalFile.Directory.FullName -Path $approval.termsFile
    $text = [IO.File]::ReadAllText($termsPath)
    if ($text -match '(?i)AVELRI_LEGAL_DRAFT_DO_NOT_DISTRIBUTE|MINUTA|\[PREENCHER|\[IDENTIFICA') {
        throw 'Draft markers or pending private identification remain in the terms.'
    }
    $hash = (Get-FileHash -LiteralPath $termsPath -Algorithm SHA256).Hash
    if ($hash -cne $approval.termsSha256) { throw 'The approved legal document hash does not match.' }
    $noticesDirectory = Resolve-AvelriLegalInputPath -BaseDirectory $approvalFile.Directory.FullName -Path $approval.thirdPartyNoticesDirectory
    $inventoryPath = Join-Path $noticesDirectory 'inventory.json'
    $inventory = [IO.File]::ReadAllText($inventoryPath) | ConvertFrom-Json
    foreach ($name in @('THIRD-PARTY-NOTICES.txt','COMPONENT-LICENSES.txt','FLUTTER-ENGINE-LICENSE.txt')) {
        $expected = [string]$inventory.generated_notice_files.$name
        if ($expected -cnotmatch '^[0-9A-F]{64}$' -or
            (Get-FileHash -LiteralPath (Join-Path $noticesDirectory $name) -Algorithm SHA256).Hash -cne $expected) {
            throw 'A required original third-party notice is missing or differs from its inventory.'
        }
    }
    return [pscustomobject]@{
        TermsPath = $termsPath; Revision = $approval.revision; SHA256 = $hash
        ApprovalSHA256 = (Get-FileHash -LiteralPath $approvalFile.FullName -Algorithm SHA256).Hash
        NoticesDirectory = $noticesDirectory; NoticesSourceCommit = [string]$inventory.source_commit
        NoticesInventorySHA256 = (Get-FileHash -LiteralPath $inventoryPath -Algorithm SHA256).Hash
    }
}

Export-ModuleMember -Function Get-AvelriApprovedDistributionLegalInputs
