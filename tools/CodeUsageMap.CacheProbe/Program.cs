using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Core;

var analyzer = new CSharpUsageAnalyzer();
var request = new AnalyzeRequest
{
    SolutionPath = "CodeUsageMap.sln",
    SymbolName = "CodeUsageMap.Integration.Tests.OutgoingSamples.PipelineStart.Start()",
    Options = new AnalyzeOptions
    {
        Depth = 2,
        WorkspaceLoader = "adhoc",
    },
};

var first = await analyzer.AnalyzeAsync(request, CancellationToken.None);
var second = await analyzer.AnalyzeAsync(request, CancellationToken.None);

Console.WriteLine($"First diagnostics: {string.Join(", ", first.Diagnostics.Select(static diagnostic => diagnostic.Code))}");
Console.WriteLine($"Second diagnostics: {string.Join(", ", second.Diagnostics.Select(static diagnostic => diagnostic.Code))}");
Console.WriteLine(second.Diagnostics.Any(static diagnostic => diagnostic.Code == "analysis_cache_hit")
    ? "CACHE_HIT_CONFIRMED"
    : "CACHE_HIT_MISSING");
