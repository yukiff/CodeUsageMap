using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Xml.Linq;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Diagnostics;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Contracts.Presentation;
using CodeUsageMap.Core.Presentation;
using CodeUsageMap.Core.Serialization;

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
            Message = "Synthetic unresolved binary reference for serializer probe.",
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
        ExcludeTests = false,
        WorkspaceLoader = "adhoc",
    },
};

var viewModelBuilder = new UsageMapViewModelBuilder();
var viewModel = viewModelBuilder.Build(result);
var serializer = new UsageGraphJsonSerializer();

var json = serializer.ToJsonDocument(result, request);
var viewModelJson = serializer.ToViewModelJsonDocument(viewModel, result, request);
var dgml = serializer.ToDgmlDocument(result, request);

ValidateJson(json);
ValidateViewModelJson(viewModelJson);
ValidateDgml(dgml);

Console.WriteLine("SERIALIZATION_PROBE_CONFIRMED");

static void ValidateJson(string json)
{
    using var document = JsonDocument.Parse(json);
    var root = document.RootElement;
    if (!root.TryGetProperty("metadata", out var metadata))
    {
        Fail("JSON_METADATA_MISSING");
    }

    if (!root.TryGetProperty("graph", out var graph))
    {
        Fail("JSON_GRAPH_MISSING");
    }

    if (metadata.GetProperty("workspaceLoader").GetString() != "adhoc")
    {
        Fail("JSON_WORKSPACE_LOADER_MISMATCH");
    }

    if (!metadata.GetProperty("partialResult").GetBoolean())
    {
        Fail("JSON_PARTIAL_RESULT_MISMATCH");
    }

    var edgeProperties = graph.GetProperty("edges")[0].GetProperty("properties");
    if (edgeProperties.GetProperty("normalizedFromMetadata").GetString() != "true")
    {
        Fail("JSON_EDGE_METADATA_MISSING");
    }
}

static void ValidateViewModelJson(string json)
{
    using var document = JsonDocument.Parse(json);
    var root = document.RootElement;
    if (!root.TryGetProperty("metadata", out var metadata))
    {
        Fail("VIEWMODEL_METADATA_MISSING");
    }

    if (!root.TryGetProperty("viewModel", out var viewModel))
    {
        Fail("VIEWMODEL_PAYLOAD_MISSING");
    }

    if (metadata.GetProperty("symbolResolution").GetProperty("status").GetInt32() != (int)SymbolResolutionStatus.Resolved)
    {
        Fail("VIEWMODEL_SYMBOL_RESOLUTION_MISMATCH");
    }

    var rootNode = viewModel.GetProperty("rootNode");
    if (rootNode.GetProperty("displayName").GetString() != "Root.Method()")
    {
        Fail("VIEWMODEL_ROOT_NODE_MISMATCH");
    }

    var outgoing = viewModel.GetProperty("outgoingRelations");
    if (outgoing.GetArrayLength() != 1)
    {
        Fail("VIEWMODEL_OUTGOING_RELATION_MISMATCH");
    }
}

static void ValidateDgml(string dgml)
{
    var document = XDocument.Parse(dgml);
    var root = document.Root;
    if (root is null)
    {
        Fail("DGML_ROOT_MISSING");
    }

    var dgmlRoot = root;
    if ((string?)dgmlRoot.Attribute("WorkspaceLoader") != "adhoc")
    {
        Fail("DGML_WORKSPACE_LOADER_MISMATCH");
    }

    if ((string?)dgmlRoot.Attribute("PartialResult") != "true")
    {
        Fail("DGML_PARTIAL_RESULT_MISMATCH");
    }

    var link = dgmlRoot.Descendants().FirstOrDefault(element => element.Name.LocalName == "Link");
    if (link is null)
    {
        Fail("DGML_LINK_MISSING");
    }

    var dgmlLink = link;
    if ((string?)dgmlLink.Attribute("normalizedFromMetadata") != "true")
    {
        Fail("DGML_LINK_METADATA_MISMATCH");
    }
}

[DoesNotReturn]
static void Fail(string code)
{
    Console.WriteLine(code);
    Environment.Exit(1);
}
