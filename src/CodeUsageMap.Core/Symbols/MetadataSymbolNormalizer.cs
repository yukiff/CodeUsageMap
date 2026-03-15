using CodeUsageMap.Core.Compatibility;
using Microsoft.CodeAnalysis;

namespace CodeUsageMap.Core.Symbols;

internal sealed class MetadataSymbolNormalizer
{
    private readonly SameSolutionAssemblyMatcher _assemblyMatcher;

    public MetadataSymbolNormalizer()
        : this(new SameSolutionAssemblyMatcher())
    {
    }

    public MetadataSymbolNormalizer(SameSolutionAssemblyMatcher assemblyMatcher)
    {
        _assemblyMatcher = assemblyMatcher;
    }

    public async Task<NormalizedSymbolInfo> NormalizeAsync(
        ISymbol symbol,
        Solution solution,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(symbol, nameof(symbol));
        Guard.NotNull(solution, nameof(solution));

        if (symbol.Locations.Any(static location => location.IsInSource))
        {
            return new NormalizedSymbolInfo
            {
                Symbol = symbol,
                SymbolOrigin = "source",
                AssemblyIdentity = symbol.ContainingAssembly?.Identity.GetDisplayName() ?? string.Empty,
            };
        }

        if (!symbol.Locations.Any(static location => location.IsInMetadata) || symbol.ContainingAssembly is null)
        {
            return new NormalizedSymbolInfo
            {
                Symbol = symbol,
                SymbolOrigin = "source",
                AssemblyIdentity = symbol.ContainingAssembly?.Identity.GetDisplayName() ?? string.Empty,
            };
        }

        var candidateProjects = await _assemblyMatcher.FindCandidatesAsync(solution, symbol.ContainingAssembly, cancellationToken);
        if (candidateProjects.Count == 0)
        {
            return new NormalizedSymbolInfo
            {
                Symbol = symbol,
                SymbolOrigin = "metadata",
                AssemblyIdentity = symbol.ContainingAssembly.Identity.GetDisplayName(),
            };
        }

        var documentationCommentId = symbol.GetDocumentationCommentId();
        var displayName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        foreach (var project in candidateProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(documentationCommentId))
            {
                var matchedByDocId = EnumerateSymbols(compilation.Assembly.GlobalNamespace)
                    .FirstOrDefault(candidate => candidate.Locations.Any(static location => location.IsInSource) &&
                        string.Equals(candidate.GetDocumentationCommentId(), documentationCommentId, StringComparison.Ordinal));
                if (matchedByDocId is not null)
                {
                    return new NormalizedSymbolInfo
                    {
                        Symbol = matchedByDocId,
                        SymbolOrigin = "source",
                        NormalizedFromMetadata = true,
                        NormalizationStrategy = "documentationCommentId",
                        AssemblyIdentity = symbol.ContainingAssembly.Identity.GetDisplayName(),
                    };
                }
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                var matchedByDisplayName = EnumerateSymbols(compilation.Assembly.GlobalNamespace)
                    .FirstOrDefault(candidate => candidate.Locations.Any(static location => location.IsInSource) &&
                        string.Equals(candidate.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), displayName, StringComparison.Ordinal));
                if (matchedByDisplayName is not null)
                {
                    return new NormalizedSymbolInfo
                    {
                        Symbol = matchedByDisplayName,
                        SymbolOrigin = "source",
                        NormalizedFromMetadata = true,
                        NormalizationStrategy = "displayName",
                        AssemblyIdentity = symbol.ContainingAssembly.Identity.GetDisplayName(),
                    };
                }
            }
        }

        return new NormalizedSymbolInfo
        {
            Symbol = symbol,
            SymbolOrigin = "unresolved_binary_reference",
            AssemblyIdentity = symbol.ContainingAssembly.Identity.GetDisplayName(),
            Limitation = "source_not_resolved_from_binary_reference",
        };
    }

    private static IEnumerable<ISymbol> EnumerateSymbols(INamespaceSymbol @namespace)
    {
        foreach (var member in @namespace.GetMembers())
        {
            if (member is INamespaceSymbol childNamespace)
            {
                foreach (var nested in EnumerateSymbols(childNamespace))
                {
                    yield return nested;
                }

                continue;
            }

            yield return member;

            if (member is INamedTypeSymbol namedType)
            {
                foreach (var nested in EnumerateMembers(namedType))
                {
                    yield return nested;
                }
            }
        }
    }

    private static IEnumerable<ISymbol> EnumerateMembers(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            yield return member;

            if (member is INamedTypeSymbol nestedType)
            {
                foreach (var nested in EnumerateMembers(nestedType))
                {
                    yield return nested;
                }
            }
        }
    }
}
