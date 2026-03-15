using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Core.Analysis;
using CodeUsageMap.Core.Compatibility;
using CodeUsageMap.Core.Symbols;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace CodeUsageMap.Core.References
{

public sealed class RoslynOutgoingCallCollector
{
    private readonly MetadataSymbolNormalizer _metadataSymbolNormalizer;

    public RoslynOutgoingCallCollector()
        : this(new MetadataSymbolNormalizer())
    {
    }

    internal RoslynOutgoingCallCollector(MetadataSymbolNormalizer metadataSymbolNormalizer)
    {
        _metadataSymbolNormalizer = metadataSymbolNormalizer;
    }

    public async Task<IReadOnlyList<OutgoingCallInfo>> CollectAsync(
        ISymbol symbol,
        Solution solution,
        AnalyzeOptions options,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(symbol, nameof(symbol));
        Guard.NotNull(solution, nameof(solution));
        Guard.NotNull(options, nameof(options));

        var results = new Dictionary<string, OutgoingCallInfo>(StringComparer.Ordinal);

        foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var syntax = await syntaxReference.GetSyntaxAsync(cancellationToken);
            var document = solution.GetDocument(syntax.SyntaxTree);
            if (document is null || !AnalysisDocumentFilter.ShouldInclude(document.Project.Name, document.FilePath, options))
            {
                continue;
            }

            var resolvedDocument = document;
            var semanticModel = await resolvedDocument.GetSemanticModelAsync(cancellationToken);
            if (semanticModel is null)
            {
                continue;
            }

            foreach (var node in syntax.DescendantNodesAndSelf())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var operation = semanticModel.GetOperation(node, cancellationToken);
                if (operation is null)
                {
                    continue;
                }

                switch (operation)
                {
                    case IInvocationOperation invocation when invocation.TargetMethod is not null:
                        await AddTargetAsync(
                            invocation.TargetMethod,
                            invocation.Syntax,
                            invocation.TargetMethod.ContainingType?.TypeKind == TypeKind.Interface
                                ? EdgeKind.InterfaceDispatch
                                : EdgeKind.DirectCall);
                        break;
                    case IDynamicInvocationOperation dynamicInvocation:
                        AddDynamicTarget(dynamicInvocation);
                        break;
                    case IObjectCreationOperation objectCreation when objectCreation.Constructor is not null:
                        await AddTargetAsync(objectCreation.Constructor, objectCreation.Syntax, EdgeKind.InstantiatedBy);
                        break;
                    case IPropertyReferenceOperation propertyReference when propertyReference.Property is not null:
                        await AddTargetAsync(propertyReference.Property, propertyReference.Syntax, EdgeKind.Reference);
                        break;
                    case IEventReferenceOperation eventReference when eventReference.Event is not null:
                        await AddTargetAsync(eventReference.Event, eventReference.Syntax, EdgeKind.Reference);
                        break;
                }
            }

            void AddDynamicTarget(IDynamicInvocationOperation dynamicInvocation)
            {
                var lineSpan = dynamicInvocation.Syntax.GetLocation().GetLineSpan();
                var symbolKey = CreateLocationSymbolKey(
                    dynamicInvocation.Syntax.ToString(),
                    resolvedDocument.FilePath ?? dynamicInvocation.Syntax.SyntaxTree.FilePath ?? resolvedDocument.Name,
                    lineSpan.StartLinePosition.Line + 1);
                if (string.IsNullOrWhiteSpace(symbolKey) || string.Equals(symbolKey, CreateSymbolKey(symbol), StringComparison.Ordinal))
                {
                    return;
                }

                var filePath = resolvedDocument.FilePath
                    ?? dynamicInvocation.Syntax.SyntaxTree.FilePath
                    ?? string.Empty;
                var referenceText = dynamicInvocation.Operation is IDynamicMemberReferenceOperation memberReference
                    ? memberReference.MemberName
                    : dynamicInvocation.Syntax.ToString();

                results[symbolKey] = new OutgoingCallInfo
                {
                    DisplayName = dynamicInvocation.Syntax.ToString(),
                    SymbolKey = symbolKey,
                    TargetKind = NodeKind.Method,
                    ProjectName = resolvedDocument.Project.Name,
                    NamespaceName = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                    Accessibility = string.Empty,
                    FilePath = filePath,
                    LineNumber = lineSpan.StartLinePosition.Line + 1,
                    Kind = EdgeKind.UnknownDynamicDispatch,
                    ReferenceText = referenceText,
                    SymbolOrigin = "dynamic",
                    NormalizedFromMetadata = false,
                    NormalizationStrategy = string.Empty,
                    AssemblyIdentity = string.Empty,
                    Limitation = "dynamic_dispatch_not_resolved",
                    ExcludedFromGraph = false,
                };
            }

            async Task AddTargetAsync(ISymbol targetSymbol, SyntaxNode targetSyntax, EdgeKind kind)
            {
                var normalized = await _metadataSymbolNormalizer.NormalizeAsync(targetSymbol, solution, cancellationToken);
                var effectiveSymbol = normalized.Symbol;
                var symbolKey = CreateSymbolKey(effectiveSymbol);
                if (string.IsNullOrWhiteSpace(symbolKey) || string.Equals(symbolKey, CreateSymbolKey(symbol), StringComparison.Ordinal))
                {
                    return;
                }

                var projectName = effectiveSymbol.ContainingAssembly?.Name ?? resolvedDocument.Project.Name;
                var sourceLocation = effectiveSymbol.Locations.FirstOrDefault(static location => location.IsInSource);
                var lineNumber = sourceLocation?.GetLineSpan().StartLinePosition.Line + 1
                    ?? targetSyntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var filePath = sourceLocation?.SourceTree?.FilePath
                    ?? resolvedDocument.FilePath
                    ?? targetSyntax.SyntaxTree.FilePath
                    ?? string.Empty;
                var info = new OutgoingCallInfo
                {
                    DisplayName = effectiveSymbol.ToDisplayString(),
                    SymbolKey = symbolKey,
                    TargetKind = InferKind(effectiveSymbol),
                    ProjectName = projectName,
                    NamespaceName = effectiveSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                    Accessibility = AccessibilityDisplay.ToDisplayValue(effectiveSymbol),
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    Kind = kind,
                    ReferenceText = effectiveSymbol.Name,
                    SymbolOrigin = normalized.SymbolOrigin,
                    NormalizedFromMetadata = normalized.NormalizedFromMetadata,
                    NormalizationStrategy = normalized.NormalizationStrategy,
                    AssemblyIdentity = normalized.AssemblyIdentity,
                    Limitation = normalized.Limitation,
                    ExcludedFromGraph = !string.IsNullOrWhiteSpace(normalized.Limitation),
                };

                results[symbolKey] = info;
            }
        }

        return results.Values.ToArray();
    }

    private static string CreateSymbolKey(ISymbol symbol)
    {
        return symbol.GetDocumentationCommentId()
            ?? symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    private static string CreateLocationSymbolKey(string displayName, string filePath, int lineNumber)
    {
        return $"{displayName}@{filePath}:{lineNumber}";
    }

    private static NodeKind InferKind(ISymbol symbol)
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
