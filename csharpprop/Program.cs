using DataPropagation;

// ─────────────────────────────────────────────────────────────────────────────
// PARSE COMMAND-LINE ARGUMENTS
// ─────────────────────────────────────────────────────────────────────────────
// Usage:
//   dotnet run -- --source QA --target DEV --tables "Table1,Table2" [--schema dbo] [--dry-run] [--batch-size 500]
// ─────────────────────────────────────────────────────────────────────────────

var config = CommandLineParser.Parse(args);

if (config is null)
{
    return 1;
}

// ─────────────────────────────────────────────────────────────────────────────
// RESOLVE CONNECTION STRINGS FROM ENVIRONMENT VARIABLES
// ─────────────────────────────────────────────────────────────────────────────
// The YAML pipeline maps secret Variable Group values into env vars:
//   SQLCONN_DEV, SQLCONN_QA, SQLCONN_UAT, SQLCONN_PROD
// ─────────────────────────────────────────────────────────────────────────────

var sourceEnvVar = $"SQLCONN_{config.SourceEnv}";
var targetEnvVar = $"SQLCONN_{config.TargetEnv}";

var sourceConnStr = Environment.GetEnvironmentVariable(sourceEnvVar);
var targetConnStr = Environment.GetEnvironmentVariable(targetEnvVar);

if (string.IsNullOrWhiteSpace(sourceConnStr))
{
    Console.Error.WriteLine($"ERROR: Source connection string is empty. Environment variable '{sourceEnvVar}' was not set.");
    Console.Error.WriteLine($"Check that your Variable Group contains 'SqlConnection_{config.SourceEnv}' and the pipeline maps it to '{sourceEnvVar}'.");
    return 1;
}

if (string.IsNullOrWhiteSpace(targetConnStr))
{
    Console.Error.WriteLine($"ERROR: Target connection string is empty. Environment variable '{targetEnvVar}' was not set.");
    Console.Error.WriteLine($"Check that your Variable Group contains 'SqlConnection_{config.TargetEnv}' and the pipeline maps it to '{targetEnvVar}'.");
    return 1;
}

Console.WriteLine("Connection strings loaded from environment variables.");
Console.WriteLine($"  Source: {sourceEnvVar} ({sourceConnStr.Length} chars)");
Console.WriteLine($"  Target: {targetEnvVar} ({targetConnStr.Length} chars)");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────────
// RUN PROPAGATION
// ─────────────────────────────────────────────────────────────────────────────

var propagator = new DataPropagator(
    sourceConnectionString: sourceConnStr,
    targetConnectionString: targetConnStr,
    sourceEnv: config.SourceEnv,
    targetEnv: config.TargetEnv,
    tables: config.Tables,
    schemaName: config.Schema,
    dryRun: config.DryRun,
    batchSize: config.BatchSize
);

return await propagator.RunAsync();
