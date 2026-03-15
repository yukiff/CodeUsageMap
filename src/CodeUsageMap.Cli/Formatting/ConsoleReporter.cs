using CodeUsageMap.Contracts.Analysis;

namespace CodeUsageMap.Cli.Formatting;

public sealed class ConsoleReporter
{
    public void WriteSummary(AnalysisResult result, string outputPath)
    {
        Console.WriteLine($"Nodes: {result.Graph.Nodes.Count}");
        Console.WriteLine($"Edges: {result.Graph.Edges.Count}");
        Console.WriteLine($"Output: {outputPath}");

        if (result.SymbolResolution.Candidates.Count > 1)
        {
            Console.WriteLine($"Symbol resolution: {result.SymbolResolution.Status}");
            foreach (var candidate in result.SymbolResolution.Candidates)
            {
                Console.WriteLine(
                    $"  [{candidate.Index}] {candidate.DisplayName}  Project={candidate.ProjectName}  Match={candidate.MatchKind}");
            }
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            Console.WriteLine($"[{diagnostic.Confidence}] {diagnostic.Code}: {diagnostic.Message}");
        }
    }
}
