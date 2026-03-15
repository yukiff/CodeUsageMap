using System.Text.Json;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Contracts.Presentation;
using CodeUsageMap.Contracts.Serialization;
using CodeUsageMap.Core.Compatibility;

namespace CodeUsageMap.Core.Serialization
{

public sealed class UsageGraphJsonSerializer : IUsageGraphSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string ToJson(UsageGraph graph)
    {
        Guard.NotNull(graph, nameof(graph));
        return JsonSerializer.Serialize(graph, SerializerOptions);
    }

    public string ToDgml(UsageGraph graph)
    {
        return new DgmlExporter().ToDgml(graph);
    }

    public string ToJsonDocument(AnalysisResult result, AnalyzeRequest request)
    {
        Guard.NotNull(result, nameof(result));
        Guard.NotNull(request, nameof(request));

        var document = new UsageGraphDocument
        {
            Metadata = CreateMetadata(result, request),
            Graph = result.Graph,
        };

        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    public string ToViewModelJsonDocument(UsageMapViewModel viewModel, AnalysisResult result, AnalyzeRequest request)
    {
        Guard.NotNull(viewModel, nameof(viewModel));
        Guard.NotNull(result, nameof(result));
        Guard.NotNull(request, nameof(request));

        var document = new UsageMapViewModelDocument
        {
            Metadata = CreateMetadata(result, request),
            ViewModel = viewModel,
        };

        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    public string ToDgmlDocument(AnalysisResult result, AnalyzeRequest request)
    {
        Guard.NotNull(result, nameof(result));
        Guard.NotNull(request, nameof(request));

        return new DgmlExporter().ToDgmlDocument(result, request);
    }

    internal static AnalysisOutputMetadata CreateMetadata(AnalysisResult result, AnalyzeRequest request)
    {
        return new AnalysisOutputMetadata
        {
            AnalysisOptions = new AnalysisOptionsSnapshot
            {
                SolutionPath = request.SolutionPath,
                SymbolName = request.SymbolName,
                SymbolIndex = request.Options.SymbolIndex,
                Depth = request.Options.Depth,
                ExcludeTests = request.Options.ExcludeTests,
                ExcludeGenerated = request.Options.ExcludeGenerated,
            },
            WorkspaceLoader = ResolveWorkspaceLoaderLabel(request.Options.WorkspaceLoader),
            GeneratedAt = DateTimeOffset.UtcNow,
            PartialResult = IsPartialResult(result),
            SymbolResolution = result.SymbolResolution,
            Diagnostics = result.Diagnostics,
        };
    }

    private static bool IsPartialResult(AnalysisResult result)
    {
        return result.Diagnostics.Any(static diagnostic =>
            diagnostic.Code.IndexOf("limit", StringComparison.OrdinalIgnoreCase) >= 0 ||
            diagnostic.Code.IndexOf("partial", StringComparison.OrdinalIgnoreCase) >= 0 ||
            diagnostic.Code.IndexOf("ambiguous", StringComparison.OrdinalIgnoreCase) >= 0 ||
            diagnostic.Code.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0 ||
            diagnostic.Code.IndexOf("unresolved", StringComparison.OrdinalIgnoreCase) >= 0 ||
            diagnostic.Code.IndexOf("not_found", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string ResolveWorkspaceLoaderLabel(string? preferredLoader)
    {
        if (!string.IsNullOrWhiteSpace(preferredLoader))
        {
            return preferredLoader;
        }

        return PlatformSupport.IsWindows() ? "msbuild(default)" : "adhoc(default)";
    }
}
}
