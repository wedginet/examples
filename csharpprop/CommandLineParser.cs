namespace DataPropagation;

/// <summary>
/// Parsed configuration from command-line arguments.
/// </summary>
public sealed class PropagationConfig
{
    public required string SourceEnv { get; init; }
    public required string TargetEnv { get; init; }
    public required List<string> Tables { get; init; }
    public string Schema { get; init; } = "dbo";
    public bool DryRun { get; init; }
    public int BatchSize { get; init; } = 500;
}

/// <summary>
/// Parses command-line arguments into a PropagationConfig.
/// </summary>
public static class CommandLineParser
{
    private static readonly HashSet<string> ValidEnvironments = ["DEV", "QA", "UAT", "PROD"];

    private static readonly HashSet<string> ValidPaths =
    [
        "QA->DEV",
        "QA->UAT",
        "UAT->QA",
        "UAT->PROD"
    ];

    public static PropagationConfig? Parse(string[] args)
    {
        string? source = null;
        string? target = null;
        string? tables = null;
        string schema = "dbo";
        bool dryRun = false;
        int batchSize = 500;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--source":
                    source = GetNextArg(args, i++)?.ToUpper();
                    break;
                case "--target":
                    target = GetNextArg(args, i++)?.ToUpper();
                    break;
                case "--tables":
                    tables = GetNextArg(args, i++);
                    break;
                case "--schema":
                    schema = GetNextArg(args, i++) ?? "dbo";
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--batch-size":
                    if (int.TryParse(GetNextArg(args, i++), out var bs))
                        batchSize = bs;
                    break;
            }
        }

        // ── Validate required args ────────────────────────────────────
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(tables))
        {
            PrintUsage();
            return null;
        }

        if (!ValidEnvironments.Contains(source))
        {
            Console.Error.WriteLine($"ERROR: Invalid source environment '{source}'. Must be one of: {string.Join(", ", ValidEnvironments)}");
            return null;
        }

        if (!ValidEnvironments.Contains(target))
        {
            Console.Error.WriteLine($"ERROR: Invalid target environment '{target}'. Must be one of: {string.Join(", ", ValidEnvironments)}");
            return null;
        }

        if (source == target)
        {
            Console.Error.WriteLine("ERROR: Source and target environments cannot be the same.");
            return null;
        }

        var path = $"{source}->{target}";
        if (!ValidPaths.Contains(path))
        {
            Console.Error.WriteLine($"ERROR: Invalid propagation path: {path}. Valid paths: {string.Join(", ", ValidPaths)}");
            return null;
        }

        var tableList = tables
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (tableList.Count == 0)
        {
            Console.Error.WriteLine("ERROR: Table list is empty.");
            return null;
        }

        return new PropagationConfig
        {
            SourceEnv = source,
            TargetEnv = target,
            Tables = tableList,
            Schema = schema,
            DryRun = dryRun,
            BatchSize = batchSize
        };
    }

    private static string? GetNextArg(string[] args, int currentIndex)
    {
        var next = currentIndex + 1;
        return next < args.Length ? args[next] : null;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: DataPropagation --source QA --target DEV --tables \"Table1,Table2\" [--schema dbo] [--dry-run] [--batch-size 500]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Valid propagation paths: QA->DEV, QA->UAT, UAT->QA, UAT->PROD");
    }
}
