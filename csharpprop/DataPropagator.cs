using System.Data;
using System.Text;

namespace DataPropagation;

/// <summary>
/// Orchestrates the full data propagation process:
/// discover schema, disable FKs, delete, copy, re-enable FKs, validate.
/// </summary>
public sealed class DataPropagator
{
    private readonly string _sourceConn;
    private readonly string _targetConn;
    private readonly string _sourceEnv;
    private readonly string _targetEnv;
    private readonly List<string> _tables;
    private readonly string _schema;
    private readonly bool _dryRun;
    private readonly int _batchSize;

    public DataPropagator(
        string sourceConnectionString,
        string targetConnectionString,
        string sourceEnv,
        string targetEnv,
        List<string> tables,
        string schemaName,
        bool dryRun,
        int batchSize)
    {
        _sourceConn = sourceConnectionString;
        _targetConn = targetConnectionString;
        _sourceEnv = sourceEnv;
        _targetEnv = targetEnv;
        _tables = tables;
        _schema = schemaName;
        _dryRun = dryRun;
        _batchSize = batchSize;
    }

    public async Task<int> RunAsync()
    {
        PrintBanner();
        PrintTableList();

        try
        {
            // Step 1: Test connections
            await TestConnectionsAsync();

            // Step 2: Discover identity columns
            var identityTables = await DiscoverIdentityColumnsAsync();

            // Step 3: Discover columns per table
            var tableColumns = await DiscoverTableColumnsAsync();

            // Step 4: Get source row counts
            var sourceRowCounts = await GetSourceRowCountsAsync();

            // Step 5: Discover FK constraints
            var foreignKeys = await DiscoverForeignKeysAsync();

            // Step 6: Disable FK constraints
            await DisableForeignKeysAsync(foreignKeys);

            // Step 7: Delete target data (reverse order)
            await DeleteTargetDataAsync();

            // Step 8: Copy data
            await CopyDataAsync(tableColumns, identityTables, sourceRowCounts);

            // Step 9: Re-enable FK constraints
            await EnableForeignKeysAsync(foreignKeys);

            // Step 10: Validate row counts
            var success = await ValidateRowCountsAsync(sourceRowCounts);

            PrintCompletion();
            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"FATAL ERROR: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // STEP 1: TEST CONNECTIONS
    // ═════════════════════════════════════════════════════════════════════════

    private async Task TestConnectionsAsync()
    {
        Console.WriteLine("── Step 1: Testing database connections ──────────────────────");

        var sourceResult = await SqlHelper.QueryAsync(_sourceConn, "SELECT DB_NAME() AS DbName, @@SERVERNAME AS ServerName");
        Console.WriteLine($"  Source ({_sourceEnv}): Connected to [{sourceResult.Rows[0]["DbName"]}] on [{sourceResult.Rows[0]["ServerName"]}]");

        var targetResult = await SqlHelper.QueryAsync(_targetConn, "SELECT DB_NAME() AS DbName, @@SERVERNAME AS ServerName");
        Console.WriteLine($"  Target ({_targetEnv}): Connected to [{targetResult.Rows[0]["DbName"]}] on [{targetResult.Rows[0]["ServerName"]}]");

        Console.WriteLine();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // STEP 2: DISCOVER IDENTITY COLUMNS
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<Dictionary<string, string>> DiscoverIdentityColumnsAsync()
    {
        Console.WriteLine("── Step 2: Discovering identity columns ─────────────────────");

        var tableInClause = string.Join("','", _tables);
        var sql = $@"
            SELECT TABLE_NAME, COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{_schema}'
              AND COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1
              AND TABLE_NAME IN ('{tableInClause}')";

        var result = await SqlHelper.QueryAsync(_targetConn, sql);
        var identityTables = new Dictionary<string, string>();

        foreach (DataRow row in result.Rows)
        {
            var tableName = row["TABLE_NAME"].ToString()!;
            var columnName = row["COLUMN_NAME"].ToString()!;
            identityTables[tableName] = columnName;
            Console.WriteLine($"  {tableName} → Identity column: {columnName}");
        }

        if (identityTables.Count == 0)
        {
            Console.WriteLine("  (No identity columns found — IDENTITY_INSERT will be skipped)");
        }

        Console.WriteLine();
        return identityTables;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // STEP 3: DISCOVER TABLE COLUMNS
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<Dictionary<string, List<string>>> DiscoverTableColumnsAsync()
    {
        Console.WriteLine("── Step 3: Discovering table columns ────────────────────────");

        var tableColumns = new Dictionary<string, List<string>>();

        // Get columns from source
        foreach (var table in _tables)
        {
            var sql = $@"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = '{_schema}' AND TABLE_NAME = '{table}'
                ORDER BY ORDINAL_POSITION";

            var result = await SqlHelper.QueryAsync(_sourceConn, sql);
            var columns = new List<string>();

            foreach (DataRow row in result.Rows)
            {
                columns.Add(row["COLUMN_NAME"].ToString()!);
            }

            tableColumns[table] = columns;
            Console.WriteLine($"  {table} → {columns.Count} columns");
        }

        // Discover and exclude computed columns on target
        var tableInClause = string.Join("','", _tables);
        var computedSql = $@"
            SELECT t.name AS TABLE_NAME, c.name AS COLUMN_NAME
            FROM sys.computed_columns c
            JOIN sys.tables t ON c.object_id = t.object_id
            JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = '{_schema}'
              AND t.name IN ('{tableInClause}')";

        var computedResult = await SqlHelper.QueryAsync(_targetConn, computedSql);

        foreach (DataRow row in computedResult.Rows)
        {
            var tableName = row["TABLE_NAME"].ToString()!;
            var colName = row["COLUMN_NAME"].ToString()!;

            if (tableColumns.TryGetValue(tableName, out var cols))
            {
                cols.Remove(colName);
                Console.WriteLine($"  {tableName} → Excluded computed column: {colName}");
            }
        }

        Console.WriteLine();
        return tableColumns;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // STEP 4: GET SOURCE ROW COUNTS
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<Dictionary<string, int>> GetSourceRowCountsAsync()
    {
        Console.WriteLine("── Step 4: Source row counts ─────────────────────────────────");

        var counts = new Dictionary<string, int>();

        foreach (var table in _tables)
        {
            var result = await SqlHelper.QueryAsync(_sourceConn, $"SELECT COUNT(*) AS Cnt FROM [{_schema}].[{table}]");
            var count = Convert.ToInt32(result.Rows[0]["Cnt"]);
            counts[table] = count;
            Console.WriteLine($"  {table} → {count} rows");
        }

        Console.WriteLine();
        return counts;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // STEP 5: DISCOVER FOREIGN KEY CONSTRAINTS
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<List<ForeignKeyInfo>> DiscoverForeignKeysAsync()
    {
        Console.WriteLine("── Step 5: Discovering FK constraints on target ──────────────");

        var tableInClause = string.Join("','", _tables);
        var sql = $@"
            SELECT fk.name AS FK_Name, tp.name AS ParentTable, tr.name AS ReferencedTable
            FROM sys.foreign_keys fk
            JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
            JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
            JOIN sys.schemas sp ON tp.schema_id = sp.schema_id
            WHERE sp.name = '{_schema}'
              AND (tp.name IN ('{tableInClause}') OR tr.name IN ('{tableInClause}'))";

        var result = await SqlHelper.QueryAsync(_targetConn, sql);
        var foreignKeys = new List<ForeignKeyInfo>();

        foreach (DataRow row in result.Rows)
        {
            var fk = new ForeignKeyInfo
            {
                Name = row["FK_Name"].ToString()!,
                ParentTable = row["ParentTable"].ToString()!,
                ReferencedTable = row["ReferencedTable"].ToString()!
            };
            foreignKeys.Add(fk);
            Console.WriteLine($"  {fk.Name}: {fk.ParentTable} → {fk.ReferencedTable}");
        }

        if (foreignKeys.Count == 0)
        {
            Console.WriteLine("  (No FK constraints found between listed tables)");
        }

        Console.WriteLine();
        return foreignKeys;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // STEP 6: DISABLE FK CONSTRAINTS
    // ═════════════════════════════════════════════════════════════════════════

    private async Task DisableForeignKeysAsync(List<ForeignKeyInfo> foreignKeys)
    {
        Console.WriteLine("── Step 6: Disabling FK constraints on target ────────────────");

        foreach (var fk in foreignKeys)
        {
            var sql = $"ALTER TABLE [{_schema}].[{fk.ParentTable}] NOCHECK CONSTRAINT [{fk.Name}]";
            Console.WriteLine($"  NOCHECK: {fk.Name} on {fk.ParentTable}");

            if (!_dryRun)
            {
                await SqlHelper.ExecuteNonQueryAsync(_targetConn, sql);
            }
        }

        Console.WriteLine();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // STEP 7: DELETE TARGET DATA (reverse dependency order)
    // ═════════════════════════════════════════════════════════════════════════

    private async Task DeleteTargetDataAsync()
    {
        Console.WriteLine("── Step 7: Deleting target table data (reverse order) ────────");

        var reversed = new List<string>(_tables);
        reversed.Reverse();

        foreach (var table in reversed)
        {
            var sql = $"DELETE FROM [{_schema}].[{table}]";
            Console.WriteLine($"  DELETE FROM [{_schema}].[{table}]");

            if (!_dryRun)
            {
                var deleted = await SqlHelper.ExecuteNonQueryAsync(_targetConn, sql, timeoutSeconds: 300);
                Console.WriteLine($"    → {deleted} rows deleted");
            }
        }

        Console.WriteLine();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // STEP 8: COPY DATA (dependency order)
    // ═════════════════════════════════════════════════════════════════════════

    private async Task CopyDataAsync(
        Dictionary<string, List<string>> tableColumns,
        Dictionary<string, string> identityTables,
        Dictionary<string, int> sourceRowCounts)
    {
        Console.WriteLine($"── Step 8: Copying data from {_sourceEnv} to {_targetEnv} ────────");

        foreach (var table in _tables)
        {
            var rowCount = sourceRowCounts[table];
            Console.WriteLine();
            Console.WriteLine($"  ┌─ {table} ({rowCount} rows) ──────────────");

            var columns = tableColumns[table];
            var columnListBracketed = string.Join(", ", columns.Select(c => $"[{c}]"));
            var hasIdentity = identityTables.ContainsKey(table);

            if (rowCount == 0)
            {
                Console.WriteLine("  │  Skipped (0 rows in source)");
                Console.WriteLine("  └─────────────────────────────────────────");
                continue;
            }

            // Read all data from source
            var selectSql = $"SELECT {columnListBracketed} FROM [{_schema}].[{table}] ORDER BY 1";
            Console.WriteLine("  │  Reading from source...");
            var sourceData = await SqlHelper.QueryAsync(_sourceConn, selectSql, timeoutSeconds: 300);

            // Build and execute batched inserts
            var totalRows = sourceData.Rows.Count;
            var batchCount = (int)Math.Ceiling((double)totalRows / _batchSize);
            Console.WriteLine($"  │  Inserting {totalRows} rows in {batchCount} batch(es) of {_batchSize}...");

            for (var batch = 0; batch < batchCount; batch++)
            {
                var startIdx = batch * _batchSize;
                var endIdx = Math.Min((batch + 1) * _batchSize - 1, totalRows - 1);

                var valuesClauses = new List<string>();

                for (var rowIdx = startIdx; rowIdx <= endIdx; rowIdx++)
                {
                    var row = sourceData.Rows[rowIdx];
                    var values = new List<string>();

                    foreach (var col in columns)
                    {
                        values.Add(FormatSqlValue(row[col]));
                    }

                    valuesClauses.Add($"({string.Join(", ", values)})");
                }

                var insertSql = $"INSERT INTO [{_schema}].[{table}] ({columnListBracketed}) VALUES\n" +
                                string.Join(",\n", valuesClauses);

                var statements = new List<string>();

                if (hasIdentity)
                    statements.Add($"SET IDENTITY_INSERT [{_schema}].[{table}] ON");

                statements.Add(insertSql);

                if (hasIdentity)
                    statements.Add($"SET IDENTITY_INSERT [{_schema}].[{table}] OFF");

                var batchDisplay = batch + 1;
                var rowsInBatch = endIdx - startIdx + 1;
                Console.WriteLine($"  │  Batch {batchDisplay}/{batchCount} ({rowsInBatch} rows)...");

                if (!_dryRun)
                {
                    try
                    {
                        await SqlHelper.ExecuteInTransactionAsync(_targetConn, statements, timeoutSeconds: 300);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  │  ✗ FAILED on batch {batchDisplay} for table {table}: {ex.Message}");
                        throw;
                    }
                }
            }

            Console.WriteLine($"  │  ✓ {totalRows} rows copied");
            Console.WriteLine("  └─────────────────────────────────────────");
        }

        Console.WriteLine();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // STEP 9: RE-ENABLE FK CONSTRAINTS
    // ═════════════════════════════════════════════════════════════════════════

    private async Task EnableForeignKeysAsync(List<ForeignKeyInfo> foreignKeys)
    {
        Console.WriteLine("── Step 9: Re-enabling FK constraints ────────────────────────");

        foreach (var fk in foreignKeys)
        {
            var sql = $"ALTER TABLE [{_schema}].[{fk.ParentTable}] WITH CHECK CHECK CONSTRAINT [{fk.Name}]";
            Console.WriteLine($"  CHECK: {fk.Name} on {fk.ParentTable}");

            if (!_dryRun)
            {
                try
                {
                    await SqlHelper.ExecuteNonQueryAsync(_targetConn, sql);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠ Failed to re-enable {fk.Name}: {ex.Message}");
                    Console.WriteLine("    This may indicate data integrity issues. Please investigate.");
                }
            }
        }

        Console.WriteLine();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // STEP 10: VALIDATE ROW COUNTS
    // ═════════════════════════════════════════════════════════════════════════

    private async Task<bool> ValidateRowCountsAsync(Dictionary<string, int> sourceRowCounts)
    {
        Console.WriteLine("── Step 10: Validating row counts ────────────────────────────");
        Console.WriteLine();

        var allMatch = true;
        var results = new List<(string Table, int Source, int Target, string Status)>();

        foreach (var table in _tables)
        {
            var targetCount = 0;

            if (!_dryRun)
            {
                var result = await SqlHelper.QueryAsync(_targetConn, $"SELECT COUNT(*) AS Cnt FROM [{_schema}].[{table}]");
                targetCount = Convert.ToInt32(result.Rows[0]["Cnt"]);
            }

            var sourceCount = sourceRowCounts[table];
            var match = _dryRun || sourceCount == targetCount;
            var status = _dryRun ? "DRY RUN" : (match ? "✓ MATCH" : "✗ MISMATCH");

            if (!match) allMatch = false;

            results.Add((table, sourceCount, targetCount, status));
        }

        // Print results table
        Console.WriteLine("  ┌──────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │  TABLE                    SOURCE    TARGET    STATUS     │");
        Console.WriteLine("  ├──────────────────────────────────────────────────────────┤");

        foreach (var (table, source, target, status) in results)
        {
            Console.WriteLine($"  │  {table,-24} {source,8} {target,8}    {status,-12} │");
        }

        Console.WriteLine("  └──────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        if (!allMatch && !_dryRun)
        {
            Console.Error.WriteLine("ERROR: Row count mismatch detected! Review the table above.");
        }

        return allMatch;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Formats a .NET value as a SQL literal for use in an INSERT VALUES clause.
    /// </summary>
    private static string FormatSqlValue(object value)
    {
        if (value is DBNull || value is null)
            return "NULL";

        return value switch
        {
            bool b => b ? "1" : "0",
            DateTime dt => $"'{dt:yyyy-MM-ddTHH:mm:ss.fff}'",
            int or long or short or decimal or double or float or byte => value.ToString()!,
            Guid g => $"'{g}'",
            byte[] bytes => "0x" + BitConverter.ToString(bytes).Replace("-", ""),
            _ => $"N'{value.ToString()!.Replace("'", "''")}'"
        };
    }

    private void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║           DATA PROPAGATION: {_sourceEnv} → {_targetEnv}              ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");

        if (_dryRun)
        {
            Console.WriteLine("║  *** DRY RUN MODE — NO CHANGES WILL BE MADE ***            ║");
        }

        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    private void PrintTableList()
    {
        Console.WriteLine("Tables (dependency order):");
        for (var i = 0; i < _tables.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {_tables[i]}");
        }
        Console.WriteLine();
    }

    private void PrintCompletion()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");

        if (_dryRun)
        {
            Console.WriteLine("  DRY RUN COMPLETE — No changes were made.");
        }
        else
        {
            Console.WriteLine($"  DATA PROPAGATION COMPLETE: {_sourceEnv} → {_targetEnv}");
            Console.WriteLine($"  All {_tables.Count} tables copied successfully.");
        }

        Console.WriteLine("═══════════════════════════════════════════════════════════════");
    }
}

/// <summary>
/// Represents a foreign key constraint discovered on the target database.
/// </summary>
public sealed class ForeignKeyInfo
{
    public required string Name { get; init; }
    public required string ParentTable { get; init; }
    public required string ReferencedTable { get; init; }
}
