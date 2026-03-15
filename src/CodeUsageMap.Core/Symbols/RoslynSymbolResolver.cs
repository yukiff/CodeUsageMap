using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using Microsoft.CodeAnalysis;

namespace CodeUsageMap.Core.Symbols
{

public sealed class RoslynSymbolResolver
{
    public async Task<SymbolResolutionMatch?> ResolveAsync(
        LoadedSolution loadedSolution,
        string symbolName,
        int? symbolIndex,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var matches = new List<(ISymbol Symbol, SymbolResolutionCandidate Candidate)>();

        foreach (var project in loadedSolution.Solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                continue;
            }

            foreach (var candidate in EnumerateSymbols(compilation.Assembly.GlobalNamespace))
            {
                var matchKind = GetMatchKind(candidate, symbolName);
                if (matchKind is null)
                {
                    continue;
                }

                var symbolKey = CreateSymbolKey(candidate);
                if (matches.Any(existing => string.Equals(existing.Candidate.SymbolKey, symbolKey, StringComparison.Ordinal)))
                {
                    continue;
                }

                var primaryLocation = candidate.Locations.FirstOrDefault(static location => location.IsInSource);
                var syntaxReference = candidate.DeclaringSyntaxReferences.FirstOrDefault();
                var lineNumber = primaryLocation?.GetLineSpan().StartLinePosition.Line + 1
                    ?? (syntaxReference is null
                        ? null
                        : syntaxReference.SyntaxTree.GetLineSpan(syntaxReference.Span).StartLinePosition.Line + 1);
                var filePath = primaryLocation?.SourceTree?.FilePath
                    ?? syntaxReference?.SyntaxTree?.FilePath
                    ?? string.Empty;

                matches.Add((candidate, new SymbolResolutionCandidate
                {
                    DisplayName = candidate.ToDisplayString(),
                    SymbolKey = symbolKey,
                    Kind = InferKind(candidate),
                    ProjectName = candidate.ContainingAssembly?.Name ?? project.Name,
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    MatchKind = matchKind,
                }));
            }
        }

        if (matches.Count == 0)
        {
            return null;
        }

        var orderedMatches = matches
            .OrderBy(static match => GetMatchPriority(match.Candidate.MatchKind))
            .ThenBy(static match => match.Candidate.DisplayName, StringComparer.Ordinal)
            .ThenBy(static match => match.Candidate.ProjectName, StringComparer.Ordinal)
            .ThenBy(static match => match.Candidate.FilePath, StringComparer.Ordinal)
            .ThenBy(static match => match.Candidate.LineNumber ?? 0)
            .Select((match, index) => (
                match.Symbol,
                Candidate: new SymbolResolutionCandidate
                {
                    Index = index + 1,
                    DisplayName = match.Candidate.DisplayName,
                    SymbolKey = match.Candidate.SymbolKey,
                    Kind = match.Candidate.Kind,
                    ProjectName = match.Candidate.ProjectName,
                    FilePath = match.Candidate.FilePath,
                    LineNumber = match.Candidate.LineNumber,
                    MatchKind = match.Candidate.MatchKind,
                },
                Index: index + 1))
            .ToArray();

        var candidates = orderedMatches
            .Select(static match => new SymbolResolutionCandidate
            {
                Index = match.Index,
                DisplayName = match.Candidate.DisplayName,
                SymbolKey = match.Candidate.SymbolKey,
                Kind = match.Candidate.Kind,
                ProjectName = match.Candidate.ProjectName,
                FilePath = match.Candidate.FilePath,
                LineNumber = match.Candidate.LineNumber,
                MatchKind = match.Candidate.MatchKind,
            })
            .ToArray();

        var resolution = new SymbolResolutionInfo
        {
            RequestedSymbolName = symbolName,
            RequestedSymbolIndex = symbolIndex,
            Candidates = candidates,
        };

        if (orderedMatches.Length == 1)
        {
            return CreateMatch(
                orderedMatches[0].Symbol,
                candidates[0],
                new SymbolResolutionInfo
                {
                    RequestedSymbolName = resolution.RequestedSymbolName,
                    RequestedSymbolIndex = resolution.RequestedSymbolIndex,
                    Candidates = resolution.Candidates,
                    Status = SymbolResolutionStatus.Resolved,
                    SelectedSymbolIndex = candidates[0].Index,
                });
        }

        if (symbolIndex is null)
        {
            return new SymbolResolutionMatch
            {
                Symbol = CreateFallback(symbolName),
                RoslynSymbol = orderedMatches[0].Symbol,
                Resolution = new SymbolResolutionInfo
                {
                    RequestedSymbolName = resolution.RequestedSymbolName,
                    RequestedSymbolIndex = resolution.RequestedSymbolIndex,
                    Candidates = resolution.Candidates,
                    Status = SymbolResolutionStatus.Ambiguous,
                },
            };
        }

        var selected = orderedMatches.FirstOrDefault(match => match.Index == symbolIndex.Value);
        if (selected.Symbol is null)
        {
            return new SymbolResolutionMatch
            {
                Symbol = CreateFallback(symbolName),
                RoslynSymbol = orderedMatches[0].Symbol,
                Resolution = new SymbolResolutionInfo
                {
                    RequestedSymbolName = resolution.RequestedSymbolName,
                    RequestedSymbolIndex = resolution.RequestedSymbolIndex,
                    Candidates = resolution.Candidates,
                    Status = SymbolResolutionStatus.InvalidSelection,
                },
            };
        }

        return CreateMatch(
            selected.Symbol,
            candidates.First(candidate => candidate.Index == symbolIndex.Value),
            new SymbolResolutionInfo
            {
                RequestedSymbolName = resolution.RequestedSymbolName,
                RequestedSymbolIndex = resolution.RequestedSymbolIndex,
                Candidates = resolution.Candidates,
                Status = SymbolResolutionStatus.Resolved,
                SelectedSymbolIndex = symbolIndex.Value,
            });
    }

    public ResolvedSymbol CreateFallback(string symbolName)
    {
        return new ResolvedSymbol
        {
            DisplayName = symbolName,
            SymbolKey = symbolName,
            NamespaceName = string.Empty,
            Accessibility = string.Empty,
            Kind = InferKind(symbolName),
        };
    }

    private static NodeKind InferKind(string symbolName)
    {
        if (string.IsNullOrWhiteSpace(symbolName))
        {
            return NodeKind.Unknown;
        }

        if (symbolName.IndexOf("::", StringComparison.Ordinal) >= 0 || symbolName.IndexOf('(') >= 0)
        {
            return NodeKind.Method;
        }

        if (symbolName.EndsWith("Event", StringComparison.Ordinal))
        {
            return NodeKind.Event;
        }

        var simpleName = symbolName
            .Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => part.Trim())
            .LastOrDefault();
        if (!string.IsNullOrEmpty(simpleName) && simpleName[0] == 'I' && simpleName.Length > 1 && char.IsUpper(simpleName[1]))
        {
            return NodeKind.Interface;
        }

        return NodeKind.Class;
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

            if (member is not INamedTypeSymbol namedType)
            {
                continue;
            }

            foreach (var nestedType in EnumerateMembers(namedType))
            {
                yield return nestedType;
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
                foreach (var nestedMember in EnumerateMembers(nestedType))
                {
                    yield return nestedMember;
                }
            }
        }
    }

    private static SymbolResolutionMatch CreateMatch(
        ISymbol symbol,
        SymbolResolutionCandidate candidate,
        SymbolResolutionInfo resolution)
    {
        return new SymbolResolutionMatch
        {
            Symbol = new ResolvedSymbol
            {
                DisplayName = candidate.DisplayName,
                SymbolKey = candidate.SymbolKey,
                NamespaceName = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                Accessibility = Analysis.AccessibilityDisplay.ToDisplayValue(symbol),
                Kind = candidate.Kind,
            },
            RoslynSymbol = symbol,
            Resolution = resolution,
        };
    }

    private static string? GetMatchKind(ISymbol symbol, string symbolName)
    {
        var documentationCommentId = symbol.GetDocumentationCommentId();
        if (string.Equals(documentationCommentId, symbolName, StringComparison.Ordinal))
        {
            return "documentationCommentId";
        }

        var fullName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        if (string.Equals(fullName, symbolName, StringComparison.Ordinal))
        {
            return "fullName";
        }

        var displayName = symbol.ToDisplayString();
        if (string.Equals(displayName, symbolName, StringComparison.Ordinal))
        {
            return "displayName";
        }

        var containingType = symbol.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        if (!string.IsNullOrWhiteSpace(containingType))
        {
            var memberName = $"{containingType}.{symbol.Name}";
            if (string.Equals(memberName, symbolName, StringComparison.Ordinal))
            {
                return "memberName";
            }
        }

        var minimalQualifiedContainingType = symbol.ContainingType?.ToDisplayString();
        if (!string.IsNullOrWhiteSpace(minimalQualifiedContainingType))
        {
            var memberDisplayName = $"{minimalQualifiedContainingType}.{symbol.Name}";
            if (string.Equals(memberDisplayName, symbolName, StringComparison.Ordinal))
            {
                return "memberDisplayName";
            }
        }

        return null;
    }

    private static string CreateSymbolKey(ISymbol symbol)
    {
        return symbol.GetDocumentationCommentId()
            ?? symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    private static int GetMatchPriority(string matchKind)
    {
        return matchKind switch
        {
            "documentationCommentId" => 0,
            "fullName" => 1,
            "displayName" => 2,
            "memberName" => 3,
            "memberDisplayName" => 4,
            _ => 10,
        };
    }
}
}
