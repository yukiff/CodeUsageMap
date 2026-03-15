using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Xml.Linq;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Diagnostics;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Contracts.Presentation;
using CodeUsageMap.Core.Presentation;
using CodeUsageMap.Core.Serialization;

var rootPath = ResolveRootPath();
var snapshotsDir = Path.Combine(rootPath, "tests", "snapshots", "serialization");

if (args.Length == 2 && string.Equals(args[0], "--dump", StringComparison.Ordinal))
{
    Console.Write(GetNormalizedOutput(args[1], rootPath));
    return;
}

Directory.CreateDirectory(snapshotsDir);

CompareSnapshot(rootPath, "graph.json", GetNormalizedOutput("graph", rootPath));
CompareSnapshot(rootPath, "viewmodel.json", GetNormalizedOutput("viewmodel", rootPath));
CompareSnapshot(rootPath, "graph.dgml", GetNormalizedOutput("dgml", rootPath));

Console.WriteLine("SNAPSHOT_REGRESSION_CONFIRMED");

static string ResolveRootPath()
{
    return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}

static string GetNormalizedOutput(string kind, string rootPath)
{
    var (result, request, viewModel) = BuildFixture();
    var serializer = new UsageGraphJsonSerializer();

    return kind switch
    {
        "graph" => NormalizeJson(serializer.ToJsonDocument(result, request)),
        "viewmodel" => NormalizeJson(serializer.ToViewModelJsonDocument(viewModel, result, request)),
        "dgml" => NormalizeDgml(serializer.ToDgmlDocument(result, request)),
        _ => throw new InvalidOperationException($"Unsupported dump kind: {kind}"),
    };
}

static (AnalysisResult Result, AnalyzeRequest Request, UsageMapViewModel ViewModel) BuildFixture()
{
    var graph = new UsageGraph();
    graph.Nodes.Add(new GraphNode
    {
        Id = "root-node",
        DisplayName = "Root.Method()",
        Kind = NodeKind.Method,
        ProjectName = "CodeUsageMap.Core",
        FilePath = "/tmp/Root.cs",
        LineNumber = 12,
        SymbolKey = "M:Root.Method",
        Properties = new Dictionary<string, string>
        {
            ["symbolOrigin"] = "source",
            ["normalizedFromMetadata"] = "false",
            ["assemblyIdentity"] = "CodeUsageMap.Core, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
        },
    });
    graph.Nodes.Add(new GraphNode
    {
        Id = "target-node",
        DisplayName = "Dependency.Run()",
        Kind = NodeKind.Method,
        ProjectName = "CodeUsageMap.Contracts",
        FilePath = "/tmp/Dependency.cs",
        LineNumber = 24,
        SymbolKey = "M:Dependency.Run",
        Properties = new Dictionary<string, string>
        {
            ["symbolOrigin"] = "source",
            ["normalizedFromMetadata"] = "true",
            ["normalizationStrategy"] = "documentationCommentId",
            ["assemblyIdentity"] = "CodeUsageMap.Contracts, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
        },
    });
    graph.Edges.Add(new GraphEdge
    {
        SourceId = "root-node",
        TargetId = "target-node",
        Kind = EdgeKind.DirectCall,
        Label = "DirectCall",
        Confidence = 1.0d,
        Properties = new Dictionary<string, string>
        {
            ["symbolOrigin"] = "source",
            ["normalizedFromMetadata"] = "true",
            ["normalizationStrategy"] = "documentationCommentId",
            ["assemblyIdentity"] = "CodeUsageMap.Contracts, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
            ["referenceText"] = "Run",
        },
    });

    var result = new AnalysisResult
    {
        Graph = graph,
        SymbolResolution = new SymbolResolutionInfo
        {
            Status = SymbolResolutionStatus.Resolved,
            RequestedSymbolName = "Root.Method",
        },
        Diagnostics =
        [
            new AnalysisDiagnostic
            {
                Code = "unresolved_binary_reference",
                Message = "Synthetic unresolved binary reference for snapshot regression.",
                Confidence = AnalysisConfidence.High,
            },
        ],
    };

    var request = new AnalyzeRequest
    {
        SolutionPath = "/tmp/CodeUsageMap.sln",
        SymbolName = "Root.Method",
        Options = new AnalyzeOptions
        {
            Depth = 2,
            ExcludeGenerated = true,
            WorkspaceLoader = "adhoc",
        },
    };

    var viewModel = new UsageMapViewModelBuilder().Build(result);
    return (result, request, viewModel);
}

static string NormalizeJson(string json)
{
    using var document = JsonDocument.Parse(json);
    var normalized = NormalizeElement(document.RootElement);
    return JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });
}

static object? NormalizeElement(JsonElement element)
{
    return element.ValueKind switch
    {
        JsonValueKind.Object => NormalizeObject(element),
        JsonValueKind.Array => element.EnumerateArray().Select(NormalizeElement).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt32(out var intValue)
            ? intValue
            : element.TryGetDouble(out var doubleValue)
                ? doubleValue
                : element.GetRawText(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText(),
    };
}

static IDictionary<string, object?> NormalizeObject(JsonElement element)
{
    var dictionary = new SortedDictionary<string, object?>(StringComparer.Ordinal);
    foreach (var property in element.EnumerateObject())
    {
        dictionary[property.Name] = property.Name switch
        {
            "generatedAt" => "<generated-at>",
            _ => NormalizeElement(property.Value),
        };
    }

    return dictionary;
}

static string NormalizeDgml(string dgml)
{
    var document = XDocument.Parse(dgml);
    var root = document.Root ?? throw new InvalidOperationException("DGML root was missing.");
    root.SetAttributeValue("GeneratedAt", "<generated-at>");
    return document.ToString(SaveOptions.None);
}

static void CompareSnapshot(string rootPath, string fileName, string actual)
{
    var path = Path.Combine(rootPath, "tests", "snapshots", "serialization", fileName);
    if (!File.Exists(path))
    {
        Fail($"SNAPSHOT_MISSING:{fileName}");
    }

    var expected = NormalizeText(File.ReadAllText(path));
    var normalizedActual = NormalizeText(actual);
    if (!string.Equals(expected, normalizedActual, StringComparison.Ordinal))
    {
        Fail($"SNAPSHOT_MISMATCH:{fileName}");
    }
}

static string NormalizeText(string value)
{
    return value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
}

[DoesNotReturn]
static void Fail(string message)
{
    Console.WriteLine(message);
    Environment.Exit(1);
}
