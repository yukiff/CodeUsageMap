using CodeUsageMap.Core.Analysis;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;

namespace CodeUsageMap.Core.References
{

public sealed class RoslynReferenceCollector
{
    public async Task<IReadOnlyList<ReferenceInfo>> CollectAsync(
        ISymbol symbol,
        Solution solution,
        AnalyzeOptions options,
        CancellationToken cancellationToken)
    {
        var results = new List<ReferenceInfo>();
        var references = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken);

        foreach (var referencedSymbol in references)
        {
            foreach (var location in referencedSymbol.Locations.Where(static item => item.Location.IsInSource))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var document = solution.GetDocument(location.Document.Id);
                if (document is null)
                {
                    continue;
                }

                var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                if (syntaxRoot is null || semanticModel is null)
                {
                    continue;
                }

                var node = syntaxRoot.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
                var enclosingSymbol = semanticModel.GetEnclosingSymbol(location.Location.SourceSpan.Start, cancellationToken);
                var lineSpan = location.Location.GetLineSpan();
                var filePath = document.FilePath ?? document.Name;
                if (!AnalysisDocumentFilter.ShouldInclude(document.Project.Name, filePath, options))
                {
                    continue;
                }

                results.Add(new ReferenceInfo
                {
                    ContainingSymbol = enclosingSymbol?.ToDisplayString() ?? document.Name,
                    ContainingSymbolKind = ClassifyContainingSymbolKind(enclosingSymbol),
                    FilePath = filePath,
                    ProjectName = document.Project.Name,
                    NamespaceName = enclosingSymbol?.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                    Accessibility = AccessibilityDisplay.ToDisplayValue(enclosingSymbol),
                    LineNumber = lineSpan.StartLinePosition.Line + 1,
                    Kind = ReferenceClassifier.Classify(node, semanticModel, cancellationToken),
                    SyntaxKind = node.Kind().ToString(),
                    ReferenceText = node.ToString(),
                });
            }
        }

        return results;
    }

    private static NodeKind ClassifyContainingSymbolKind(ISymbol? symbol)
    {
        return symbol switch
        {
            IMethodSymbol => NodeKind.Method,
            IPropertySymbol => NodeKind.Property,
            IEventSymbol => NodeKind.Event,
            INamedTypeSymbol namedType when namedType.TypeKind == TypeKind.Interface => NodeKind.Interface,
            INamedTypeSymbol => NodeKind.Class,
            _ => NodeKind.Unknown,
        };
    }
}
}
