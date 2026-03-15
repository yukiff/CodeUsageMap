using CodeUsageMap.Contracts.Analysis;
using System.Xml.Linq;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Contracts.Presentation;
using CodeUsageMap.Contracts.Serialization;

namespace CodeUsageMap.Core.Serialization;

public sealed class DgmlExporter : IUsageGraphSerializer
{
    public string ToJson(UsageGraph graph)
    {
        return new UsageGraphJsonSerializer().ToJson(graph);
    }

    public string ToDgml(UsageGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        XNamespace ns = "http://schemas.microsoft.com/vs/2009/dgml";

        var document = new XDocument(
            new XElement(ns + "DirectedGraph",
                new XAttribute("GraphDirection", "LeftToRight"),
                new XElement(ns + "Nodes",
                    graph.Nodes.Select(node =>
                        CreateNodeElement(ns, node))),
                new XElement(ns + "Links",
                    graph.Edges.Select(edge =>
                        CreateLinkElement(ns, edge))),
                new XElement(ns + "Categories",
                    Enum.GetNames<NodeKind>().Select(kind =>
                        new XElement(ns + "Category", new XAttribute("Id", kind))),
                    Enum.GetNames<EdgeKind>().Select(kind =>
                        new XElement(ns + "Category", new XAttribute("Id", kind))))));

        return document.ToString();
    }

    public string ToJsonDocument(AnalysisResult result, AnalyzeRequest request)
    {
        return new UsageGraphJsonSerializer().ToJsonDocument(result, request);
    }

    public string ToViewModelJsonDocument(UsageMapViewModel viewModel, AnalysisResult result, AnalyzeRequest request)
    {
        return new UsageGraphJsonSerializer().ToViewModelJsonDocument(viewModel, result, request);
    }

    public string ToDgmlDocument(AnalysisResult result, AnalyzeRequest request)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(request);

        var metadata = UsageGraphJsonSerializer.CreateMetadata(result, request);
        XNamespace ns = "http://schemas.microsoft.com/vs/2009/dgml";

        var document = new XDocument(
            new XElement(ns + "DirectedGraph",
                new XAttribute("GraphDirection", "LeftToRight"),
                new XAttribute("SchemaVersion", metadata.SchemaVersion),
                new XAttribute("WorkspaceLoader", metadata.WorkspaceLoader),
                new XAttribute("GeneratedAt", metadata.GeneratedAt.ToString("O")),
                new XAttribute("PartialResult", metadata.PartialResult ? "true" : "false"),
                new XAttribute("Depth", metadata.AnalysisOptions.Depth),
                new XAttribute("SymbolIndex", metadata.AnalysisOptions.SymbolIndex?.ToString() ?? string.Empty),
                new XAttribute("ExcludeTests", metadata.AnalysisOptions.ExcludeTests ? "true" : "false"),
                new XAttribute("ExcludeGenerated", metadata.AnalysisOptions.ExcludeGenerated ? "true" : "false"),
                new XAttribute("SymbolName", metadata.AnalysisOptions.SymbolName),
                new XAttribute("SymbolResolutionStatus", metadata.SymbolResolution.Status),
                new XAttribute("SymbolCandidateCount", metadata.SymbolResolution.Candidates.Count),
                new XAttribute("SolutionPath", metadata.AnalysisOptions.SolutionPath),
                new XAttribute("DiagnosticCount", metadata.Diagnostics.Count),
                new XElement(ns + "Nodes",
                    result.Graph.Nodes.Select(node =>
                        CreateNodeElement(ns, node))),
                new XElement(ns + "Links",
                    result.Graph.Edges.Select(edge =>
                        CreateLinkElement(ns, edge))),
                new XElement(ns + "Categories",
                    Enum.GetNames<NodeKind>().Select(kind =>
                        new XElement(ns + "Category", new XAttribute("Id", kind))),
                    Enum.GetNames<EdgeKind>().Select(kind =>
                        new XElement(ns + "Category", new XAttribute("Id", kind))))));

        return document.ToString();
    }

    private static XElement CreateNodeElement(XNamespace ns, GraphNode node)
    {
        var element = new XElement(ns + "Node",
            new XAttribute("Id", node.Id),
            new XAttribute("Label", node.DisplayName),
            new XAttribute("Category", node.Kind));

        AddMetadataAttributes(element, node.Properties);
        return element;
    }

    private static XElement CreateLinkElement(XNamespace ns, GraphEdge edge)
    {
        var element = new XElement(ns + "Link",
            new XAttribute("Source", edge.SourceId),
            new XAttribute("Target", edge.TargetId),
            new XAttribute("Category", edge.Kind),
            new XAttribute("Label", edge.Label),
            new XAttribute("Confidence", edge.Confidence));

        AddMetadataAttributes(element, edge.Properties);
        return element;
    }

    private static void AddMetadataAttributes(XElement element, IReadOnlyDictionary<string, string> properties)
    {
        foreach (var property in properties)
        {
            element.SetAttributeValue(property.Key, property.Value);
        }
    }
}
