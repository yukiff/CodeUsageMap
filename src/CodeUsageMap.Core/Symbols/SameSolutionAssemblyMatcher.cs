using Microsoft.CodeAnalysis;

namespace CodeUsageMap.Core.Symbols;

internal sealed class SameSolutionAssemblyMatcher
{
    public async Task<IReadOnlyList<Project>> FindCandidatesAsync(
        Solution solution,
        IAssemblySymbol assemblySymbol,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(assemblySymbol);

        var assemblyName = assemblySymbol.Identity.Name;
        var matches = new List<Project>();

        foreach (var project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                continue;
            }

            if (string.Equals(compilation.AssemblyName, assemblyName, StringComparison.Ordinal))
            {
                matches.Add(project);
            }
        }

        return matches;
    }
}
