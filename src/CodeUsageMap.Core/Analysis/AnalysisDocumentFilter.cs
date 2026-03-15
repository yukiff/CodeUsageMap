using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Core.Compatibility;

namespace CodeUsageMap.Core.Analysis;

internal static class AnalysisDocumentFilter
{
    private static readonly string[] GeneratedFileSuffixes =
    [
        ".g.cs",
        ".g.i.cs",
        ".generated.cs",
        ".designer.cs",
        ".AssemblyAttributes.cs",
    ];

    public static bool ShouldInclude(string projectName, string? filePath, AnalyzeOptions options)
    {
        Guard.NotNull(options, nameof(options));

        if (options.ExcludeTests && IsTestProject(projectName, filePath))
        {
            return false;
        }

        if (options.ExcludeGenerated && IsGeneratedFile(filePath))
        {
            return false;
        }

        return true;
    }

    private static bool IsTestProject(string projectName, string? filePath)
    {
        if (projectName.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var normalizedPath = filePath.Replace('\\', '/');
        return normalizedPath.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.EndsWith(".Tests.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGeneratedFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var normalizedPath = filePath.Replace('\\', '/');
        if (normalizedPath.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return GeneratedFileSuffixes.Any(
            suffix => normalizedPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
