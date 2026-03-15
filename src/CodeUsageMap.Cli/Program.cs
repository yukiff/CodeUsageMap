using CodeUsageMap.Cli.Commands;
using CodeUsageMap.Cli.Options;

return await ProgramEntry.RunAsync(args);

internal static class ProgramEntry
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            WriteHelp();
            return 0;
        }

        if (!string.Equals(args[0], "analyze", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Unknown command: {args[0]}");
            WriteHelp();
            return 1;
        }

        var options = ParseAnalyzeOptions(args.Skip(1).ToArray());
        if (options is null)
        {
            WriteHelp();
            return 1;
        }

        var command = new CliAnalyzeCommand();
        return await command.ExecuteAsync(options, CancellationToken.None);
    }

    private static AnalyzeCommandOptions? ParseAnalyzeOptions(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var current = args[index];
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"Unexpected argument: {current}");
                return null;
            }

            if (current is "--exclude-tests" or "--exclude-generated")
            {
                flags.Add(current);
                continue;
            }

            if (index + 1 >= args.Length)
            {
                Console.Error.WriteLine($"Missing value for {current}");
                return null;
            }

            values[current] = args[index + 1];
            index++;
        }

        if (!values.TryGetValue("--solution", out var solutionPath) ||
            !values.TryGetValue("--symbol", out var symbolName) ||
            !values.TryGetValue("--format", out var format) ||
            !values.TryGetValue("--output", out var outputPath))
        {
            Console.Error.WriteLine("Missing required options.");
            return null;
        }

        var depth = 1;
        if (values.TryGetValue("--depth", out var depthValue) && !int.TryParse(depthValue, out depth))
        {
            Console.Error.WriteLine($"Invalid depth: {depthValue}");
            return null;
        }

        int? symbolIndex = null;
        if (values.TryGetValue("--symbol-index", out var symbolIndexValue))
        {
            if (!int.TryParse(symbolIndexValue, out var parsedSymbolIndex))
            {
                Console.Error.WriteLine($"Invalid symbol index: {symbolIndexValue}");
                return null;
            }

            symbolIndex = parsedSymbolIndex;
        }

        return new AnalyzeCommandOptions
        {
            SolutionPath = solutionPath,
            SymbolName = symbolName,
            Format = format,
            OutputPath = outputPath,
            Depth = depth,
            SymbolIndex = symbolIndex,
            ExcludeTests = flags.Contains("--exclude-tests"),
            ExcludeGenerated = flags.Contains("--exclude-generated"),
            WorkspaceLoader = values.TryGetValue("--workspace-loader", out var workspaceLoader) ? workspaceLoader : string.Empty,
        };
    }

    private static void WriteHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  analyze --solution <path> --symbol <name> --format <json|dgml|viewmodel-json> --output <path> [--depth <n>] [--symbol-index <n>] [--exclude-tests] [--exclude-generated] [--workspace-loader <adhoc|msbuild>]");
    }
}
