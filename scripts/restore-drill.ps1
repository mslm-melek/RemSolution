<#
.SYNOPSIS
    Restores the RemSolution database from its automated backups into a fresh
    copy, checks the copy is usable, and reports how long it took.

.DESCRIPTION
    Point-in-time restore into a NEW database (the live one is never touched),
    then a count of what came back and a block to paste into the restore log in
    docs/RUNBOOK_Base_De_Donnees.md. Run it after any change to the database tier
    or backup settings, and quarterly otherwise; the figure that matters is how
    long a restore takes, and it grows with the database.

    Requires the Azure CLI (already logged in). Row verification also needs the
    SqlServer module (Invoke-Sqlcmd); without it, only the timing is reported.

.PARAMETER ResourceGroup
    Resource group holding the SQL server.

.PARAMETER ServerName
    Azure SQL logical server name (without .database.windows.net).

.PARAMETER DatabaseName
    The live database to restore FROM. It is only read.

.PARAMETER RestorePointUtc
    Point in time to restore to, UTC. Defaults to 10 minutes ago.

.PARAMETER KeepRestore
    Leave the restored copy in place. It is deleted by default: a second
    full-size database costs real money.

.PARAMETER SqlAdminUser
    Login used for the verification queries. Omit to skip verification.

.PARAMETER SqlAdminPassword
    Password for -SqlAdminUser. Read from the Key Vault secret
    'sqlAdminPassword' if -KeyVaultName is given instead.

.PARAMETER KeyVaultName
    Key Vault holding 'sqlAdminPassword', so it stays out of the shell history.

.EXAMPLE
    ./restore-drill.ps1 -ResourceGroup rg-remsolution-prod -ServerName sql-abc123 `
        -DatabaseName sqldb-abc123 -SqlAdminUser sqlAdmin -KeyVaultName kv-abc123
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ResourceGroup,
    [Parameter(Mandatory = $true)][string]$ServerName,
    [Parameter(Mandatory = $true)][string]$DatabaseName,
    [datetime]$RestorePointUtc = (Get-Date).ToUniversalTime().AddMinutes(-10),
    [switch]$KeepRestore,
    [string]$SqlAdminUser,
    [string]$SqlAdminPassword,
    [string]$KeyVaultName
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$message) {
    Write-Host ""
    Write-Host "== $message" -ForegroundColor Cyan
}

# ---------------------------------------------------------------- preflight

Write-Step "Checking the source database and its restorable window"

$database = az sql db show `
    --resource-group $ResourceGroup --server $ServerName --name $DatabaseName `
    --query "{sku:sku.name, tier:sku.tier, maxSizeBytes:maxSizeBytes, earliestRestore:earliestRestoreDate}" `
    -o json | ConvertFrom-Json

if (-not $database) {
    throw "Database '$DatabaseName' was not found on server '$ServerName'."
}

$earliest = [datetime]::Parse($database.earliestRestore).ToUniversalTime()
$sizeGb = [math]::Round($database.maxSizeBytes / 1GB, 1)

Write-Host "  tier              : $($database.tier) / $($database.sku)"
Write-Host "  max size          : $sizeGb GB"
Write-Host "  earliest restore  : $($earliest.ToString('u'))"
Write-Host "  restoring to      : $($RestorePointUtc.ToString('u'))"

if ($RestorePointUtc -lt $earliest) {
    throw "The requested restore point is older than the retained window (earliest: $($earliest.ToString('u')))."
}

$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmm')
$targetName = "$DatabaseName-drill-$stamp"

# ------------------------------------------------------------------ restore

Write-Step "Restoring into '$targetName' (the live database is not touched)"

$clock = [System.Diagnostics.Stopwatch]::StartNew()

az sql db restore `
    --resource-group $ResourceGroup --server $ServerName --name $DatabaseName `
    --dest-name $targetName `
    --time $RestorePointUtc.ToString('yyyy-MM-ddTHH:mm:ss') `
    --output none

$clock.Stop()
$elapsed = $clock.Elapsed

Write-Host "  restore completed in $($elapsed.ToString('hh\:mm\:ss'))" -ForegroundColor Green

# ------------------------------------------------------------- verification

$verification = "skipped (no -SqlAdminUser)"

if ($SqlAdminUser) {
    Write-Step "Checking the restored copy holds the data"

    if (-not $SqlAdminPassword -and $KeyVaultName) {
        $SqlAdminPassword = az keyvault secret show `
            --vault-name $KeyVaultName --name sqlAdminPassword --query value -o tsv
    }

    if (-not $SqlAdminPassword) {
        throw "No password for '$SqlAdminUser': pass -SqlAdminPassword or -KeyVaultName."
    }

    if (-not (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue)) {
        Write-Warning "Invoke-Sqlcmd is unavailable (Install-Module SqlServer). The restore was timed; the row counts were not checked."
        $verification = "skipped (Invoke-Sqlcmd unavailable)"
    }
    else {
        # Counts, plus the migration the schema is at: one short means the wrong
        # point in time.
        $query = @"
SELECT
    (SELECT COUNT(*) FROM Agencies)                     AS Agencies,
    (SELECT COUNT(*) FROM Cars)                         AS Cars,
    (SELECT COUNT(*) FROM Clients)                      AS Clients,
    (SELECT COUNT(*) FROM Rentings)                     AS Rentings,
    (SELECT COUNT(*) FROM Payments)                     AS Payments,
    (SELECT COUNT(*) FROM AspNetUsers)                  AS Users,
    (SELECT MAX(MigrationId) FROM __EFMigrationsHistory) AS Schema_At;
"@

        $counts = Invoke-Sqlcmd `
            -ServerInstance "$ServerName.database.windows.net" `
            -Database $targetName `
            -Username $SqlAdminUser -Password $SqlAdminPassword `
            -Query $query -TrustServerCertificate

        $verification = ($counts | Format-List | Out-String).Trim()
        Write-Host $verification
    }
}

# ------------------------------------------------------------------ cleanup

if ($KeepRestore) {
    Write-Step "Leaving '$targetName' in place (-KeepRestore). Delete it when done — it bills like any other database."
}
else {
    Write-Step "Deleting the restored copy"
    az sql db delete --resource-group $ResourceGroup --server $ServerName --name $targetName --yes --output none
    Write-Host "  deleted '$targetName'"
}

# ------------------------------------------------------------------- report

Write-Step "Paste this into the restore log in docs/RUNBOOK_Base_De_Donnees.md"

@"
| $(Get-Date -Format 'yyyy-MM-dd') | $($RestorePointUtc.ToString('u')) | $($elapsed.ToString('hh\:mm\:ss')) | $sizeGb GB | $($env:USERNAME) | |
"@ | Write-Host

Write-Host ""
Write-Host "Restore drill finished. Time to a usable copy: $($elapsed.ToString('hh\:mm\:ss'))." -ForegroundColor Green
