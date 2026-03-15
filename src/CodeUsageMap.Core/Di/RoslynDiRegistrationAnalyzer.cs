using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Diagnostics;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Core.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace CodeUsageMap.Core.Di;

public sealed class RoslynDiRegistrationAnalyzer
{
    private static readonly HashSet<string> SupportedRegistrationMethods = new(StringComparer.Ordinal)
    {
        "AddSingleton",
        "AddScoped",
        "AddTransient",
    };

    public async Task<IReadOnlyList<DiRegistrationInfo>> CollectAsync(
        ISymbol symbol,
        Solution solution,
        AnalyzeOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(options);

        var targetType = ResolveTargetType(symbol);
        if (targetType is null)
        {
            return [];
        }

        var results = new Dictionary<string, DiRegistrationInfo>(StringComparer.Ordinal);

        foreach (var document in solution.Projects.SelectMany(static project => project.Documents))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = document.FilePath ?? document.Name;
            if (!AnalysisDocumentFilter.ShouldInclude(document.Project.Name, filePath, options))
            {
                continue;
            }

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            foreach (var invocation in syntaxRoot.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (semanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation invocationOperation)
                {
                    continue;
                }

                if (!TryCreateRegistrationInfo(document, invocation, invocationOperation, symbol, targetType, cancellationToken, out var info))
                {
                    continue;
                }

                results[info.RegistrationId] = info;
            }
        }

        return results.Values.ToArray();
    }

    private static bool TryCreateRegistrationInfo(
        Document document,
        InvocationExpressionSyntax invocation,
        IInvocationOperation invocationOperation,
        ISymbol targetSymbol,
        INamedTypeSymbol targetType,
        CancellationToken cancellationToken,
        out DiRegistrationInfo info)
    {
        info = null!;

        var method = invocationOperation.TargetMethod;
        if (!SupportedRegistrationMethods.Contains(method.Name))
        {
            return false;
        }

        if (!TryResolveServiceAndImplementation(invocationOperation, out var serviceType, out var implementationType, out var registrationKind))
        {
            return false;
        }

        if (serviceType is null || implementationType is null)
        {
            return false;
        }

        var normalizedTargetType = NormalizeType(targetType);
        var matchesService = SymbolEqualityComparer.Default.Equals(normalizedTargetType, NormalizeType(serviceType));
        var matchesImplementation = SymbolEqualityComparer.Default.Equals(normalizedTargetType, NormalizeType(implementationType));
        if (!matchesService && !matchesImplementation)
        {
            return false;
        }

        var serviceSymbol = ResolveServiceSymbol(targetSymbol, serviceType);
        var implementationSymbol = ResolveImplementationSymbol(targetSymbol, implementationType, serviceType);
        if (serviceSymbol is null || implementationSymbol is null)
        {
            return false;
        }

        var lineSpan = invocation.GetLocation().GetLineSpan();
        var lineNumber = lineSpan.StartLinePosition.Line + 1;
        var filePath = document.FilePath ?? document.Name;
        var lifetime = method.Name["Add".Length..];
        var registrationId = $"{document.Project.Name}:{filePath}:{lineNumber}:{serviceSymbol.GetDocumentationCommentId() ?? serviceSymbol.ToDisplayString()}->{implementationSymbol.GetDocumentationCommentId() ?? implementationSymbol.ToDisplayString()}";

        info = new DiRegistrationInfo
        {
            RegistrationId = registrationId,
            RegistrationDisplayName = $"{lifetime}: {serviceType.ToDisplayString()} -> {implementationType.ToDisplayString()}",
            ServiceSymbolId = serviceSymbol.GetDocumentationCommentId() ?? serviceSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            ServiceDisplayName = serviceSymbol.ToDisplayString(),
            ServiceKind = ClassifyNodeKind(serviceSymbol),
            ImplementationSymbolId = implementationSymbol.GetDocumentationCommentId() ?? implementationSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            ImplementationDisplayName = implementationSymbol.ToDisplayString(),
            ImplementationKind = ClassifyNodeKind(implementationSymbol),
            Lifetime = lifetime,
            RegistrationKind = registrationKind,
            ProjectName = document.Project.Name,
            NamespaceName = method.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            FilePath = filePath,
            LineNumber = lineNumber,
            RegistrationText = invocation.ToString(),
            Confidence = registrationKind == "typeof" ? AnalysisConfidence.High : AnalysisConfidence.Confirmed,
        };
        return true;
    }

    private static bool TryResolveServiceAndImplementation(
        IInvocationOperation invocationOperation,
        out INamedTypeSymbol? serviceType,
        out INamedTypeSymbol? implementationType,
        out string registrationKind)
    {
        serviceType = null;
        implementationType = null;
        registrationKind = string.Empty;

        if (invocationOperation.TargetMethod.TypeArguments.Length == 2)
        {
            serviceType = invocationOperation.TargetMethod.TypeArguments[0] as INamedTypeSymbol;
            implementationType = invocationOperation.TargetMethod.TypeArguments[1] as INamedTypeSymbol;
            registrationKind = "generic";
            return serviceType is not null && implementationType is not null;
        }

        if (invocationOperation.TargetMethod.TypeArguments.Length == 1)
        {
            serviceType = invocationOperation.TargetMethod.TypeArguments[0] as INamedTypeSymbol;
            implementationType = serviceType;
            registrationKind = "self";
            return serviceType is not null;
        }

        var typeofArguments = invocationOperation.Arguments
            .Select(static argument => argument.Value)
            .OfType<ITypeOfOperation>()
            .Select(static argument => argument.TypeOperand as INamedTypeSymbol)
            .Where(static type => type is not null)
            .Cast<INamedTypeSymbol>()
            .ToArray();

        if (typeofArguments.Length >= 2)
        {
            serviceType = typeofArguments[0];
            implementationType = typeofArguments[1];
            registrationKind = "typeof";
            return true;
        }

        if (typeofArguments.Length == 1)
        {
            serviceType = typeofArguments[0];
            implementationType = typeofArguments[0];
            registrationKind = "typeof-self";
            return true;
        }

        return false;
    }

    private static ISymbol? ResolveServiceSymbol(ISymbol targetSymbol, INamedTypeSymbol serviceType)
    {
        return targetSymbol switch
        {
            INamedTypeSymbol namedType when SymbolEqualityComparer.Default.Equals(NormalizeType(namedType), NormalizeType(serviceType)) => targetSymbol,
            IMethodSymbol method when SymbolEqualityComparer.Default.Equals(NormalizeType(method.ContainingType), NormalizeType(serviceType)) => method,
            IPropertySymbol property when SymbolEqualityComparer.Default.Equals(NormalizeType(property.ContainingType), NormalizeType(serviceType)) => property,
            IEventSymbol @event when SymbolEqualityComparer.Default.Equals(NormalizeType(@event.ContainingType), NormalizeType(serviceType)) => @event,
            _ => serviceType,
        };
    }

    private static ISymbol? ResolveImplementationSymbol(ISymbol targetSymbol, INamedTypeSymbol implementationType, INamedTypeSymbol serviceType)
    {
        if (targetSymbol is INamedTypeSymbol namedType &&
            SymbolEqualityComparer.Default.Equals(NormalizeType(namedType), NormalizeType(implementationType)))
        {
            return namedType;
        }

        if (targetSymbol is IMethodSymbol method)
        {
            if (SymbolEqualityComparer.Default.Equals(NormalizeType(method.ContainingType), NormalizeType(implementationType)))
            {
                return method;
            }

            if (SymbolEqualityComparer.Default.Equals(NormalizeType(method.ContainingType), NormalizeType(serviceType)))
            {
                return FindMatchingMethod(method, implementationType) is ISymbol implementationMethod
                    ? implementationMethod
                    : implementationType;
            }
        }

        if (targetSymbol is IPropertySymbol property)
        {
            if (SymbolEqualityComparer.Default.Equals(NormalizeType(property.ContainingType), NormalizeType(implementationType)))
            {
                return property;
            }

            if (SymbolEqualityComparer.Default.Equals(NormalizeType(property.ContainingType), NormalizeType(serviceType)))
            {
                return implementationType.GetMembers(property.Name).OfType<IPropertySymbol>().FirstOrDefault() is ISymbol implementationProperty
                    ? implementationProperty
                    : implementationType;
            }
        }

        if (targetSymbol is IEventSymbol @event)
        {
            if (SymbolEqualityComparer.Default.Equals(NormalizeType(@event.ContainingType), NormalizeType(implementationType)))
            {
                return @event;
            }

            if (SymbolEqualityComparer.Default.Equals(NormalizeType(@event.ContainingType), NormalizeType(serviceType)))
            {
                return implementationType.GetMembers(@event.Name).OfType<IEventSymbol>().FirstOrDefault() is ISymbol implementationEvent
                    ? implementationEvent
                    : implementationType;
            }
        }

        return implementationType;
    }

    private static IMethodSymbol? FindMatchingMethod(IMethodSymbol serviceMethod, INamedTypeSymbol implementationType)
    {
        return implementationType
            .GetMembers(serviceMethod.Name)
            .OfType<IMethodSymbol>()
            .FirstOrDefault(candidate =>
                candidate.Arity == serviceMethod.Arity &&
                candidate.Parameters.Length == serviceMethod.Parameters.Length &&
                candidate.Parameters.Zip(serviceMethod.Parameters).All(static pair =>
                    string.Equals(
                        pair.First.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        pair.Second.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        StringComparison.Ordinal)));
    }

    private static INamedTypeSymbol? ResolveTargetType(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol namedType => namedType,
            IMethodSymbol method => method.ContainingType,
            IPropertySymbol property => property.ContainingType,
            IEventSymbol @event => @event.ContainingType,
            _ => null,
        };
    }

    private static INamedTypeSymbol NormalizeType(INamedTypeSymbol symbol)
    {
        return symbol.OriginalDefinition;
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
