Here's the full C# solution. Your new repo structure would be:
your-repo/
  pipelines/
    data-propagation-pipeline.yml      ← updated YAML (replaces old one)
  tools/
    DataPropagation/
      DataPropagation.csproj           ← .NET 10 console app
      Program.cs                       ← entry point
      CommandLineParser.cs             ← argument parsing + validation
      SqlHelper.cs                     ← lightweight SQL execution (query, non-query, transaction)
      DataPropagator.cs                ← all the propagation logic
You can delete the old pipelines/scripts/Invoke-DataPropagation.ps1 — it's no longer needed.
The C# is structured the way you'd expect: Program.cs is thin (parses args, resolves env vars, hands off to DataPropagator). DataPropagator.RunAsync() orchestrates the same 10 steps the PowerShell did. SqlHelper is a static utility class with QueryAsync, ExecuteNonQueryAsync, and ExecuteInTransactionAsync. It uses Microsoft.Data.SqlClient which works on both Windows and Linux, so if you ever need to go back to the Linux pool, it'll just work.
The YAML now has a UseDotNet@2 task to ensure .NET 10 SDK is available, then DotNetCoreCLI@2 for build and run. Connection strings still flow through the env: block the same way — nothing changes there.
