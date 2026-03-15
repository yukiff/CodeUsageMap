using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Core.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace CodeUsageMap.Core.Implementations
{

public sealed class RoslynImplementationCollector
{
    public async Task<IReadOnlyList<ImplementationInfo>> CollectAsync(
        ISymbol symbol,
        Solution solution,
        AnalyzeOptions options,
        CancellationToken cancellationToken)
    {
        var implementations = await SymbolFinder.FindImplementationsAsync(symbol, solution, cancellationToken: cancellationToken);
        var results = new Dictionary<string, ImplementationInfo>(StringComparer.Ordinal);

        foreach (var implementation in implementations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AddImplementation(symbol, implementation, options, results);
        }

        switch (symbol)
        {
            case IMethodSymbol methodSymbol:
            {
                var overrides = await SymbolFinder.FindOverridesAsync(methodSymbol, solution, cancellationToken: cancellationToken);
                foreach (var implementation in overrides)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AddImplementation(symbol, implementation, options, results);
                }

                break;
            }
            case IPropertySymbol propertySymbol:
            {
                var overrides = await SymbolFinder.FindOverridesAsync(propertySymbol, solution, cancellationToken: cancellationToken);
                foreach (var implementation in overrides)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AddImplementation(symbol, implementation, options, results);
                }

                break;
            }
        }

        return results.Values.ToArray();
    }

    private static void AddImplementation(
        ISymbol sourceSymbol,
        ISymbol implementation,
        AnalyzeOptions options,
        IDictionary<string, ImplementationInfo> results)
    {
        var sourceLocation = implementation.Locations.FirstOrDefault(static location => location.IsInSource);
        var lineSpan = sourceLocation?.GetLineSpan();
        var filePath = sourceLocation?.SourceTree?.FilePath ?? string.Empty;
        var projectName = implementation.ContainingAssembly?.Name ?? string.Empty;
        if (!AnalysisDocumentFilter.ShouldInclude(projectName, filePath, options))
        {
            return;
        }

        var symbolKey = implementation.GetDocumentationCommentId() ?? implementation.ToDisplayString();
        results[symbolKey] = new ImplementationInfo
        {
            DisplayName = implementation.ToDisplayString(),
            SymbolKey = symbolKey,
            ProjectName = projectName,
            NamespaceName = implementation.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            Accessibility = AccessibilityDisplay.ToDisplayValue(implementation),
            FilePath = filePath,
            LineNumber = lineSpan?.StartLinePosition.Line + 1,
            NodeKind = ClassifyNodeKind(implementation),
            Kind = Classify(sourceSymbol, implementation),
            ContainingTypeName = implementation.ContainingType?.ToDisplayString() ?? string.Empty,
            IsOverride = implementation is IMethodSymbol method && method.IsOverride
                || implementation is IPropertySymbol property && property.IsOverride,
        };
    }

    private static EdgeKind Classify(ISymbol symbol, ISymbol implementation)
    {
        return symbol switch
        {
            INamedTypeSymbol sourceType when sourceType.TypeKind == TypeKind.Interface => EdgeKind.Implements,
            IMethodSymbol when implementation is IMethodSymbol method && method.IsOverride => EdgeKind.Overrides,
            IPropertySymbol when implementation is IPropertySymbol property && property.IsOverride => EdgeKind.Overrides,
            _ => EdgeKind.Implements,
        };
    }

    private static NodeKind ClassifyNodeKind(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol namedType when namedType.TypeKind == TypeKind.Interface => NodeKind.Interface,
            INamedTypeSymbol => NodeKind.Class,
            IMethodSymbol => NodeKind.Method,
            IPropertySymbol => NodeKind.Property,
            IEventSymbol => NodeKind.Event,
            _ => NodeKind.Unknown,
        };
    }
}
}
