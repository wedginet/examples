using System.Data;
using Microsoft.Data.SqlClient;

namespace DataPropagation;

/// <summary>
/// Lightweight SQL helper for executing queries and non-queries against Azure SQL.
/// Uses Microsoft.Data.SqlClient for cross-platform Azure SQL compatibility.
/// </summary>
public static class SqlHelper
{
    /// <summary>
    /// Executes a query and returns a DataTable with results.
    /// </summary>
    public static async Task<DataTable> QueryAsync(string connectionString, string sql, int timeoutSeconds = 120)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = timeoutSeconds;

        using var adapter = new SqlDataAdapter(command);
        var dataSet = new DataSet();
        adapter.Fill(dataSet);

        return dataSet.Tables[0];
    }

    /// <summary>
    /// Executes a non-query (INSERT, UPDATE, DELETE, ALTER, etc.) and returns rows affected.
    /// </summary>
    public static async Task<int> ExecuteNonQueryAsync(string connectionString, string sql, int timeoutSeconds = 120)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = timeoutSeconds;

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Executes multiple SQL statements within a single transaction.
    /// Rolls back on any failure.
    /// </summary>
    public static async Task ExecuteInTransactionAsync(string connectionString, IReadOnlyList<string> statements, int timeoutSeconds = 300)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var sql in statements)
            {
                if (string.IsNullOrWhiteSpace(sql)) continue;

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.CommandTimeout = timeoutSeconds;
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { /* swallow rollback errors */ }
            throw;
        }
    }
}
