<#
.SYNOPSIS
    Copies lookup/rules table data from a source Azure SQL DB to a target Azure SQL DB,
    preserving primary keys and foreign key relationships.

.DESCRIPTION
    This script:
    1. Installs Microsoft.Data.SqlClient (required for Linux/PowerShell Core on Azure SQL)
    2. Reads connection strings from environment variables (SQLCONN_DEV, SQLCONN_QA, etc.)
    3. Connects to both source and target Azure SQL databases
    4. Discovers identity columns and FK constraints for the specified tables
    5. Disables FK constraints on the target DB
    6. Deletes target table data in REVERSE dependency order (child tables first)
    7. Copies data from source to target using batched INSERT with IDENTITY_INSERT ON
    8. Re-enables FK constraints
    9. Validates that row counts match between source and target

    Supported paths: QA->DEV, QA->UAT, UAT->QA, UAT->PROD.

.PARAMETER SourceEnv
    Source environment name (QA or UAT). Used to look up env var SQLCONN_{SourceEnv}.

.PARAMETER TargetEnv
    Target environment name (DEV, QA, UAT, or PROD). Used to look up env var SQLCONN_{TargetEnv}.

.PARAMETER TableList
    Comma-separated list of table names in DEPENDENCY ORDER (parent tables first).

.PARAMETER SchemaName
    Database schema name (default: dbo).

.PARAMETER DryRun
    If true, logs all SQL that would be executed but makes no changes.

.PARAMETER BatchSize
    Number of rows per INSERT batch (default: 500).
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceEnv,

    [Parameter(Mandatory = $true)]
    [string]$TargetEnv,

    [Parameter(Mandatory = $true)]
    [string]$TableList,

    [Parameter(Mandatory = $false)]
    [string]$SchemaName = "dbo",

    [Parameter(Mandatory = $false)]
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [int]$BatchSize = 500
)

# ─────────────────────────────────────────────────────────────────────────────
# STRICT ERROR HANDLING
# ─────────────────────────────────────────────────────────────────────────────
$ErrorActionPreference = "Stop"

# ─────────────────────────────────────────────────────────────────────────────
# STEP 0: INSTALL Microsoft.Data.SqlClient
# ─────────────────────────────────────────────────────────────────────────────
# System.Data.SqlClient does not work reliably on Linux/PowerShell Core with
# Azure SQL. Microsoft.Data.SqlClient is the supported cross-platform driver.
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "── Step 0: Loading SQL Client driver ─────────────────────────"

# Check if Microsoft.Data.SqlClient is already available
$sqlClientLoaded = $false
try {
    [void][Microsoft.Data.SqlClient.SqlConnection]
    $sqlClientLoaded = $true
    Write-Host "  Microsoft.Data.SqlClient already loaded."
}
catch {
    Write-Host "  Microsoft.Data.SqlClient not found, installing via NuGet..."
}

if (-not $sqlClientLoaded) {
    # Register NuGet if needed
    $nuget = Get-PackageProvider -Name NuGet -ErrorAction SilentlyContinue
    if (-not $nuget) {
        Write-Host "  Installing NuGet package provider..."
        Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope CurrentUser | Out-Null
    }

    # Install the package
    Write-Host "  Installing Microsoft.Data.SqlClient..."
    $pkg = Install-Package Microsoft.Data.SqlClient -Source 'https://www.nuget.org/api/v2' `
        -Force -Scope CurrentUser -SkipDependencies -ErrorAction Stop

    # Find and load the DLL
    $pkgPath = ($pkg).Source | Split-Path
    # Look for the netcore/net6+ compatible DLL
    $dllPath = Get-ChildItem -Path $pkgPath -Recurse -Filter "Microsoft.Data.SqlClient.dll" |
        Where-Object { $_.FullName -match "net[6-9]|netcoreapp|netstandard" } |
        Sort-Object { $_.FullName } -Descending |
        Select-Object -First 1

    if (-not $dllPath) {
        # Fallback: any DLL
        $dllPath = Get-ChildItem -Path $pkgPath -Recurse -Filter "Microsoft.Data.SqlClient.dll" |
            Select-Object -First 1
    }

    if (-not $dllPath) {
        Write-Error "Could not find Microsoft.Data.SqlClient.dll after installation."
        exit 1
    }

    Write-Host "  Loading from: $($dllPath.FullName)"
    Add-Type -Path $dllPath.FullName
}

Write-Host "  SQL Client driver ready."
Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# RESOLVE CONNECTION STRINGS FROM ENVIRONMENT VARIABLES
# ─────────────────────────────────────────────────────────────────────────────
$sourceEnvVar = "SQLCONN_$SourceEnv"
$targetEnvVar = "SQLCONN_$TargetEnv"

$SourceConnectionString = [System.Environment]::GetEnvironmentVariable($sourceEnvVar)
$TargetConnectionString = [System.Environment]::GetEnvironmentVariable($targetEnvVar)

if ([string]::IsNullOrWhiteSpace($SourceConnectionString)) {
    Write-Error "Source connection string is empty. Environment variable '$sourceEnvVar' was not set. Check that your Variable Group contains 'SqlConnection_$SourceEnv' and that the pipeline 'env:' block maps it to '$sourceEnvVar'."
    exit 1
}

if ([string]::IsNullOrWhiteSpace($TargetConnectionString)) {
    Write-Error "Target connection string is empty. Environment variable '$targetEnvVar' was not set. Check that your Variable Group contains 'SqlConnection_$TargetEnv' and that the pipeline 'env:' block maps it to '$targetEnvVar'."
    exit 1
}

Write-Host "Connection strings loaded from environment variables."
Write-Host "  Source: $sourceEnvVar ($(($SourceConnectionString).Length) chars)"
Write-Host "  Target: $targetEnvVar ($(($TargetConnectionString).Length) chars)"

# ─────────────────────────────────────────────────────────────────────────────
# HELPER: Execute SQL and return results (or just execute)
# Uses Microsoft.Data.SqlClient (cross-platform, Azure SQL compatible)
# ─────────────────────────────────────────────────────────────────────────────
function Invoke-Sql {
    param(
        [string]$ConnectionString,
        [string]$Query,
        [switch]$NonQuery,
        [int]$Timeout = 120
    )

    $connection = New-Object Microsoft.Data.SqlClient.SqlConnection($ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $Query
        $command.CommandTimeout = $Timeout

        if ($NonQuery) {
            $rowsAffected = $command.ExecuteNonQuery()
            return $rowsAffected
        }
        else {
            $adapter = New-Object Microsoft.Data.SqlClient.SqlDataAdapter($command)
            $dataSet = New-Object System.Data.DataSet
            [void]$adapter.Fill($dataSet)
            return $dataSet.Tables[0]
        }
    }
    finally {
        if ($connection.State -eq 'Open') { $connection.Close() }
        $connection.Dispose()
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# HELPER: Execute SQL with a transaction (for multi-statement data operations)
# ─────────────────────────────────────────────────────────────────────────────
function Invoke-SqlTransaction {
    param(
        [string]$ConnectionString,
        [string[]]$Queries,
        [int]$Timeout = 300
    )

    $connection = New-Object Microsoft.Data.SqlClient.SqlConnection($ConnectionString)
    $transaction = $null
    try {
        $connection.Open()
        $transaction = $connection.BeginTransaction()

        foreach ($query in $Queries) {
            if ([string]::IsNullOrWhiteSpace($query)) { continue }
            $command = $connection.CreateCommand()
            $command.Transaction = $transaction
            $command.CommandText = $query
            $command.CommandTimeout = $Timeout
            [void]$command.ExecuteNonQuery()
        }

        $transaction.Commit()
    }
    catch {
        if ($transaction) {
            try { $transaction.Rollback() } catch { }
        }
        throw
    }
    finally {
        if ($connection.State -eq 'Open') { $connection.Close() }
        $connection.Dispose()
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# BANNER
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════════╗"
Write-Host "║           DATA PROPAGATION: $SourceEnv → $TargetEnv              ║"
Write-Host "╠═══════════════════════════════════════════════════════════════╣"
if ($DryRun) {
    Write-Host "║  *** DRY RUN MODE — NO CHANGES WILL BE MADE ***            ║"
}
Write-Host "╚═══════════════════════════════════════════════════════════════╝"
Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# PARSE TABLE LIST
# ─────────────────────────────────────────────────────────────────────────────
$tables = $TableList.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
$tablesReversed = [System.Collections.ArrayList]::new($tables)
$tablesReversed.Reverse()

Write-Host "Tables (dependency order):"
for ($i = 0; $i -lt $tables.Count; $i++) {
    Write-Host "  $($i + 1). $($tables[$i])"
}
Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# STEP 1: TEST CONNECTIONS
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "── Step 1: Testing database connections ──────────────────────"

try {
    $sourceTest = Invoke-Sql -ConnectionString $SourceConnectionString -Query "SELECT DB_NAME() AS DbName, @@SERVERNAME AS ServerName"
    Write-Host "  Source ($SourceEnv): Connected to [$($sourceTest.Rows[0].DbName)] on [$($sourceTest.Rows[0].ServerName)]"
}
catch {
    Write-Error "Failed to connect to SOURCE ($SourceEnv): $_"
    exit 1
}

try {
    $targetTest = Invoke-Sql -ConnectionString $TargetConnectionString -Query "SELECT DB_NAME() AS DbName, @@SERVERNAME AS ServerName"
    Write-Host "  Target ($TargetEnv): Connected to [$($targetTest.Rows[0].DbName)] on [$($targetTest.Rows[0].ServerName)]"
}
catch {
    Write-Error "Failed to connect to TARGET ($TargetEnv): $_"
    exit 1
}
Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# STEP 2: DISCOVER IDENTITY COLUMNS
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "── Step 2: Discovering identity columns ─────────────────────"

$identityQuery = @"
SELECT TABLE_NAME, COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = '$SchemaName'
  AND COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1
  AND TABLE_NAME IN ('$($tables -join "','")')
"@

$identityColumns = Invoke-Sql -ConnectionString $TargetConnectionString -Query $identityQuery
$identityTables = @{}
foreach ($row in $identityColumns.Rows) {
    $identityTables[$row.TABLE_NAME] = $row.COLUMN_NAME
    Write-Host "  $($row.TABLE_NAME) → Identity column: $($row.COLUMN_NAME)"
}

if ($identityTables.Count -eq 0) {
    Write-Host "  (No identity columns found — IDENTITY_INSERT will be skipped)"
}
Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# STEP 3: DISCOVER COLUMNS PER TABLE (from source)
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "── Step 3: Discovering table columns ────────────────────────"

$tableColumns = @{}
foreach ($table in $tables) {
    $colQuery = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = '$SchemaName' AND TABLE_NAME = '$table'
ORDER BY ORDINAL_POSITION
"@
    $cols = Invoke-Sql -ConnectionString $SourceConnectionString -Query $colQuery
    $colNames = @()
    foreach ($row in $cols.Rows) {
        $colNames += $row.COLUMN_NAME
    }
    $tableColumns[$table] = $colNames
    Write-Host "  $table → $($colNames.Count) columns"
}

# Also check for computed columns on the target and exclude them
$computedQuery = @"
SELECT t.name AS TABLE_NAME, c.name AS COLUMN_NAME
FROM sys.computed_columns c
JOIN sys.tables t ON c.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = '$SchemaName'
  AND t.name IN ('$($tables -join "','")')
"@
$computedCols = Invoke-Sql -ConnectionString $TargetConnectionString -Query $computedQuery
$computedMap = @{}
foreach ($row in $computedCols.Rows) {
    if (-not $computedMap.ContainsKey($row.TABLE_NAME)) {
        $computedMap[$row.TABLE_NAME] = @()
    }
    $computedMap[$row.TABLE_NAME] += $row.COLUMN_NAME
}

# Filter out computed columns
foreach ($table in $tables) {
    if ($computedMap.ContainsKey($table)) {
        $tableColumns[$table] = $tableColumns[$table] | Where-Object { $_ -notin $computedMap[$table] }
        Write-Host "  $table → Excluded computed columns: $($computedMap[$table] -join ', ')"
    }
}
Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# STEP 4: GET SOURCE ROW COUNTS (for validation later)
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "── Step 4: Source row counts ─────────────────────────────────"

$sourceRowCounts = @{}
foreach ($table in $tables) {
    $countResult = Invoke-Sql -ConnectionString $SourceConnectionString `
        -Query "SELECT COUNT(*) AS Cnt FROM [$SchemaName].[$table]"
    $sourceRowCounts[$table] = [int]$countResult.Rows[0].Cnt
    Write-Host "  $table → $($sourceRowCounts[$table]) rows"
}
Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# STEP 5: DISCOVER FK CONSTRAINTS BETWEEN OUR TABLES ON TARGET
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "── Step 5: Discovering FK constraints on target ──────────────"

$fkQuery = @"
SELECT
    fk.name AS FK_Name,
    tp.name AS ParentTable,
    tr.name AS ReferencedTable
FROM sys.foreign_keys fk
JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
JOIN sys.schemas sp ON tp.schema_id = sp.schema_id
WHERE sp.name = '$SchemaName'
  AND (tp.name IN ('$($tables -join "','")') OR tr.name IN ('$($tables -join "','")'))
"@

$foreignKeys = Invoke-Sql -ConnectionString $TargetConnectionString -Query $fkQuery
$fkList = @()
foreach ($row in $foreignKeys.Rows) {
    $fkList += @{ Name = $row.FK_Name; Parent = $row.ParentTable; Referenced = $row.ReferencedTable }
    Write-Host "  $($row.FK_Name): $($row.ParentTable) → $($row.ReferencedTable)"
}

if ($fkList.Count -eq 0) {
    Write-Host "  (No FK constraints found between listed tables)"
}
Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# STEP 6: DISABLE FK CONSTRAINTS ON TARGET
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "── Step 6: Disabling FK constraints on target ────────────────"

foreach ($fk in $fkList) {
    $disableSql = "ALTER TABLE [$SchemaName].[$($fk.Parent)] NOCHECK CONSTRAINT [$($fk.Name)]"
    Write-Host "  NOCHECK: $($fk.Name) on $($fk.Parent)"
    if (-not $DryRun) {
        Invoke-Sql -ConnectionString $TargetConnectionString -Query $disableSql -NonQuery
    }
}
Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# STEP 7: DELETE TARGET TABLE DATA (reverse dependency order)
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "── Step 7: Deleting target table data (reverse order) ────────"

foreach ($table in $tablesReversed) {
    $deleteSql = "DELETE FROM [$SchemaName].[$table]"
    Write-Host "  DELETE FROM [$SchemaName].[$table]"
    if (-not $DryRun) {
        $deleted = Invoke-Sql -ConnectionString $TargetConnectionString -Query $deleteSql -NonQuery -Timeout 300
        Write-Host "    → $deleted rows deleted"
    }
}
Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# STEP 8: COPY DATA TABLE BY TABLE (dependency order)
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "── Step 8: Copying data from $SourceEnv to $TargetEnv ────────"

foreach ($table in $tables) {
    Write-Host ""
    Write-Host "  ┌─ $table ($($sourceRowCounts[$table]) rows) ──────────────"

    $columns = $tableColumns[$table]
    $columnListBracketed = ($columns | ForEach-Object { "[$_]" }) -join ", "
    $hasIdentity = $identityTables.ContainsKey($table)

    if ($sourceRowCounts[$table] -eq 0) {
        Write-Host "  │  Skipped (0 rows in source)"
        Write-Host "  └─────────────────────────────────────────"
        continue
    }

    # ── Read ALL data from source ──
    $selectSql = "SELECT $columnListBracketed FROM [$SchemaName].[$table] ORDER BY 1"
    Write-Host "  │  Reading from source..."
    $sourceData = Invoke-Sql -ConnectionString $SourceConnectionString -Query $selectSql -Timeout 300

    # ── Build and execute batched INSERTs ──
    $totalRows = $sourceData.Rows.Count
    $batchCount = [math]::Ceiling($totalRows / $BatchSize)
    Write-Host "  │  Inserting $totalRows rows in $batchCount batch(es) of $BatchSize..."

    for ($batch = 0; $batch -lt $batchCount; $batch++) {
        $startIdx = $batch * $BatchSize
        $endIdx = [math]::Min(($batch + 1) * $BatchSize - 1, $totalRows - 1)

        $valuesClauses = @()
        for ($rowIdx = $startIdx; $rowIdx -le $endIdx; $rowIdx++) {
            $row = $sourceData.Rows[$rowIdx]
            $values = @()
            foreach ($col in $columns) {
                $val = $row[$col]
                if ($val -is [System.DBNull] -or $null -eq $val) {
                    $values += "NULL"
                }
                elseif ($val -is [System.Boolean]) {
                    $values += if ($val) { "1" } else { "0" }
                }
                elseif ($val -is [System.DateTime]) {
                    $values += "'" + $val.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "'"
                }
                elseif ($val -is [System.Int32] -or $val -is [System.Int64] -or
                         $val -is [System.Int16] -or $val -is [System.Decimal] -or
                         $val -is [System.Double] -or $val -is [System.Single] -or
                         $val -is [System.Byte]) {
                    $values += $val.ToString()
                }
                elseif ($val -is [System.Guid]) {
                    $values += "'" + $val.ToString() + "'"
                }
                elseif ($val -is [byte[]]) {
                    # Binary data — convert to hex literal
                    $hex = [System.BitConverter]::ToString($val).Replace("-", "")
                    $values += "0x$hex"
                }
                else {
                    # String — escape single quotes
                    $escaped = $val.ToString().Replace("'", "''")
                    $values += "N'" + $escaped + "'"
                }
            }
            $valuesClauses += "(" + ($values -join ", ") + ")"
        }

        $insertSql = "INSERT INTO [$SchemaName].[$table] ($columnListBracketed) VALUES`n" +
                      ($valuesClauses -join ",`n")

        $queries = @()
        if ($hasIdentity) {
            $queries += "SET IDENTITY_INSERT [$SchemaName].[$table] ON"
        }
        $queries += $insertSql
        if ($hasIdentity) {
            $queries += "SET IDENTITY_INSERT [$SchemaName].[$table] OFF"
        }

        $batchDisplay = $batch + 1
        $rowsInBatch = $endIdx - $startIdx + 1
        Write-Host "  │  Batch $batchDisplay/$batchCount ($rowsInBatch rows)..."

        if (-not $DryRun) {
            try {
                Invoke-SqlTransaction -ConnectionString $TargetConnectionString -Queries $queries -Timeout 300
            }
            catch {
                Write-Error "  │  ✗ FAILED on batch $batchDisplay for table $table : $_"
                throw
            }
        }
    }

    Write-Host "  │  ✓ $totalRows rows copied"
    Write-Host "  └─────────────────────────────────────────"
}
Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# STEP 9: RE-ENABLE FK CONSTRAINTS
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "── Step 9: Re-enabling FK constraints ────────────────────────"

foreach ($fk in $fkList) {
    $enableSql = "ALTER TABLE [$SchemaName].[$($fk.Parent)] WITH CHECK CHECK CONSTRAINT [$($fk.Name)]"
    Write-Host "  CHECK: $($fk.Name) on $($fk.Parent)"
    if (-not $DryRun) {
        try {
            Invoke-Sql -ConnectionString $TargetConnectionString -Query $enableSql -NonQuery
        }
        catch {
            Write-Warning "  ⚠ Failed to re-enable $($fk.Name): $_"
            Write-Warning "    This may indicate data integrity issues. Please investigate."
        }
    }
}
Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# STEP 10: VALIDATE ROW COUNTS
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "── Step 10: Validating row counts ────────────────────────────"

$allMatch = $true
$results = @()
foreach ($table in $tables) {
    $targetCount = 0
    if (-not $DryRun) {
        $countResult = Invoke-Sql -ConnectionString $TargetConnectionString `
            -Query "SELECT COUNT(*) AS Cnt FROM [$SchemaName].[$table]"
        $targetCount = [int]$countResult.Rows[0].Cnt
    }
    $sourceCount = $sourceRowCounts[$table]
    $match = ($DryRun) -or ($sourceCount -eq $targetCount)

    $status = if ($DryRun) { "DRY RUN" } elseif ($match) { "✓ MATCH" } else { "✗ MISMATCH" }
    $results += [PSCustomObject]@{
        Table       = $table
        Source      = $sourceCount
        Target      = $targetCount
        Status      = $status
    }

    if (-not $match) { $allMatch = $false }
}

Write-Host ""
Write-Host "  ┌──────────────────────────────────────────────────────────┐"
Write-Host "  │  TABLE                    SOURCE    TARGET    STATUS     │"
Write-Host "  ├──────────────────────────────────────────────────────────┤"
foreach ($r in $results) {
    $tbl = $r.Table.PadRight(24)
    $src = $r.Source.ToString().PadLeft(8)
    $tgt = $r.Target.ToString().PadLeft(8)
    $st  = $r.Status.PadRight(12)
    Write-Host "  │  $tbl $src $tgt    $st │"
}
Write-Host "  └──────────────────────────────────────────────────────────┘"
Write-Host ""

if (-not $allMatch -and -not $DryRun) {
    Write-Error "Row count mismatch detected! Review the table above."
    exit 1
}

# ─────────────────────────────────────────────────────────────────────────────
# DONE
# ─────────────────────────────────────────────────────────────────────────────
if ($DryRun) {
    Write-Host "═══════════════════════════════════════════════════════════════"
    Write-Host "  DRY RUN COMPLETE — No changes were made."
    Write-Host "═══════════════════════════════════════════════════════════════"
}
else {
    Write-Host "═══════════════════════════════════════════════════════════════"
    Write-Host "  DATA PROPAGATION COMPLETE: $SourceEnv → $TargetEnv"
    Write-Host "  All $($tables.Count) tables copied successfully."
    Write-Host "═══════════════════════════════════════════════════════════════"
}

exit 0
