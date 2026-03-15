using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;

namespace CodeUsageMap.Vsix.Services
{

internal sealed class VisualStudioSymbolContextService
{
    private readonly AsyncPackage _package;

    public VisualStudioSymbolContextService(AsyncPackage package)
    {
        _package = package;
    }

    public async Task<VisualStudioSymbolContext?> TryGetCurrentContextAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var workspaceContext = await TryGetWorkspaceContextAsync(cancellationToken);
        if (workspaceContext is null)
        {
            return null;
        }

        var (solutionPath, workspace, activeDocument, selection) = workspaceContext.Value;
        if (activeDocument?.FullName is not string documentPath || selection is null || string.IsNullOrWhiteSpace(documentPath))
        {
            return null;
        }

        var document = workspace.CurrentSolution.Projects
            .SelectMany(static project => project.Documents)
            .FirstOrDefault(item => string.Equals(item.FilePath, documentPath, StringComparison.OrdinalIgnoreCase));
        if (document is null)
        {
            return null;
        }

        var sourceText = await document.GetTextAsync(cancellationToken);
        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (syntaxRoot is null || semanticModel is null)
        {
            return null;
        }

        var lineIndex = Math.Max(selection.ActivePoint.Line - 1, 0);
        var characterIndex = Math.Max(selection.ActivePoint.LineCharOffset - 1, 0);
        if (lineIndex >= sourceText.Lines.Count)
        {
            return null;
        }

        var line = sourceText.Lines[lineIndex];
        var position = Math.Min(line.Start + characterIndex, line.End);
        var token = syntaxRoot.FindToken(position);
        var symbol = semanticModel.GetSymbolInfo(token.Parent ?? syntaxRoot, cancellationToken).Symbol
            ?? semanticModel.GetDeclaredSymbol(token.Parent ?? syntaxRoot, cancellationToken);
        if (symbol is null)
        {
            return null;
        }

        return new VisualStudioSymbolContext
        {
            SolutionPath = solutionPath,
            SymbolName = ToResolvableSymbolName(symbol),
            DisplayName = symbol.ToDisplayString(),
            SymbolKey = symbol.GetDocumentationCommentId() ?? ToResolvableSymbolName(symbol),
            Kind = ToNodeKind(symbol),
            ProjectName = document.Project.Name,
            FilePath = documentPath,
            LineNumber = lineIndex + 1,
        };
    }

    public async Task<IReadOnlyList<VisualStudioSymbolContext>> SearchSymbolsAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || maxResults <= 0)
        {
            return Array.Empty<VisualStudioSymbolContext>();
        }

        var workspaceContext = await TryGetWorkspaceContextAsync(cancellationToken);
        if (workspaceContext is null)
        {
            return Array.Empty<VisualStudioSymbolContext>();
        }

        var (solutionPath, workspace, _, _) = workspaceContext.Value;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<VisualStudioSymbolContext>();

        foreach (var project in workspace.CurrentSolution.Projects.Where(static project => project.Language == LanguageNames.CSharp))
        {
            var declarations = await SymbolFinder.FindSourceDeclarationsAsync(
                project,
                query,
                ignoreCase: true,
                filter: SymbolFilter.TypeAndMember,
                cancellationToken: cancellationToken);

            foreach (var symbol in declarations.OrderBy(static symbol => symbol.Name, StringComparer.OrdinalIgnoreCase))
            {
                var context = CreateContextFromSymbol(symbol, solutionPath, project.Name);
                if (context is null || !seen.Add(context.SymbolKey))
                {
                    continue;
                }

                results.Add(context);
                if (results.Count >= maxResults)
                {
                    return results;
                }
            }
        }

        return results;
    }

    private async Task<(string SolutionPath, VisualStudioWorkspace Workspace, Document? ActiveDocument, TextSelection? Selection)?> TryGetWorkspaceContextAsync(
        CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await _package.GetServiceAsync(typeof(DTE)) as DTE;
        if (dte?.Solution?.FullName is not string solutionPath || string.IsNullOrWhiteSpace(solutionPath) || !File.Exists(solutionPath))
        {
            return null;
        }

        var componentModel = await _package.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
        var workspace = componentModel?.GetService<VisualStudioWorkspace>();
        if (workspace is null)
        {
            return null;
        }

        return (solutionPath, workspace, dte.ActiveDocument, dte.ActiveDocument?.Selection as TextSelection);
    }

    private static VisualStudioSymbolContext? CreateContextFromSymbol(ISymbol symbol, string solutionPath, string fallbackProjectName)
    {
        var sourceLocation = symbol.Locations.FirstOrDefault(static location => location.IsInSource);
        var syntaxReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        var filePath = sourceLocation?.SourceTree?.FilePath
            ?? syntaxReference?.SyntaxTree.FilePath
            ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var lineNumber = sourceLocation?.GetLineSpan().StartLinePosition.Line + 1;
        if (lineNumber is null && syntaxReference is not null)
        {
            lineNumber = syntaxReference.SyntaxTree.GetLineSpan(syntaxReference.Span).StartLinePosition.Line + 1;
        }

        return new VisualStudioSymbolContext
        {
            SolutionPath = solutionPath,
            SymbolName = ToResolvableSymbolName(symbol),
            DisplayName = symbol.ToDisplayString(),
            SymbolKey = symbol.GetDocumentationCommentId() ?? ToResolvableSymbolName(symbol),
            Kind = ToNodeKind(symbol),
            ProjectName = symbol.ContainingAssembly?.Name ?? fallbackProjectName,
            FilePath = filePath,
            LineNumber = lineNumber,
        };
    }

    private static string ToResolvableSymbolName(ISymbol symbol)
    {
        var fullName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        var containingType = symbol.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return string.IsNullOrWhiteSpace(containingType)
            ? symbol.Name
            : $"{containingType}.{symbol.Name}";
    }

    private static Contracts.Graph.NodeKind ToNodeKind(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol namedType when namedType.TypeKind == TypeKind.Interface => Contracts.Graph.NodeKind.Interface,
            INamedTypeSymbol => Contracts.Graph.NodeKind.Class,
            IMethodSymbol => Contracts.Graph.NodeKind.Method,
            IPropertySymbol => Contracts.Graph.NodeKind.Property,
            IEventSymbol => Contracts.Graph.NodeKind.Event,
            _ => Contracts.Graph.NodeKind.Unknown,
        };
    }
}
}
