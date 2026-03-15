using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Diagnostics;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Core.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace CodeUsageMap.Core.Events;

public sealed class RoslynEventUsageCollector
{
    public async Task<IReadOnlyList<EventUsageInfo>> CollectAsync(
        ISymbol symbol,
        Solution solution,
        AnalyzeOptions options,
        CancellationToken cancellationToken)
    {
        return symbol switch
        {
            IEventSymbol eventSymbol => await CollectForEventAsync(eventSymbol, solution, options, cancellationToken),
            IMethodSymbol methodSymbol => await CollectForHandlerAsync(methodSymbol, solution, options, cancellationToken),
            _ => [],
        };
    }

    private async Task<IReadOnlyList<EventUsageInfo>> CollectForEventAsync(
        IEventSymbol eventSymbol,
        Solution solution,
        AnalyzeOptions options,
        CancellationToken cancellationToken)
    {
        var subscriptions = new List<EventUsageInfo>();
        var unsubscriptions = new HashSet<string>(StringComparer.Ordinal);
        var raises = new List<EventUsageInfo>();

        foreach (var document in solution.Projects.SelectMany(static project => project.Documents))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            var filePath = document.FilePath ?? document.Name;
            if (!AnalysisDocumentFilter.ShouldInclude(document.Project.Name, filePath, options))
            {
                continue;
            }

            foreach (var assignment in syntaxRoot.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddAssignmentExpression) &&
                    !assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SubtractAssignmentExpression))
                {
                    continue;
                }

                var eventAssignment = semanticModel.GetOperation(assignment, cancellationToken) as IEventAssignmentOperation;
                var leftSymbol = GetEventSymbol(eventAssignment);
                if (!SymbolEqualityComparer.Default.Equals(leftSymbol, eventSymbol))
                {
                    continue;
                }

                var usage = CreateAssignmentUsage(document, semanticModel, assignment, eventAssignment, cancellationToken);
                if (usage is null)
                {
                    continue;
                }

                if (usage.Kind == EdgeKind.EventUnsubscription)
                {
                    unsubscriptions.Add($"{usage.EventSymbolId}|{usage.HandlerSymbolId}|{usage.HandlerName}");
                }

                subscriptions.Add(usage);
            }

            foreach (var invocation in syntaxRoot.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var eventExpression = TryGetRaisedEventExpression(invocation);
                if (eventExpression is null)
                {
                    continue;
                }

                var raisedEvent = semanticModel.GetSymbolInfo(eventExpression, cancellationToken).Symbol as IEventSymbol;
                if (!SymbolEqualityComparer.Default.Equals(raisedEvent, eventSymbol))
                {
                    continue;
                }

                var containingSymbol = semanticModel.GetEnclosingSymbol(invocation.SpanStart, cancellationToken);
                var lineSpan = invocation.GetLocation().GetLineSpan();
                raises.Add(new EventUsageInfo
                {
                    ContainingSymbolId = CreateLocationSymbolId(containingSymbol?.ToDisplayString() ?? document.Name, document.FilePath ?? document.Name, lineSpan.StartLinePosition.Line + 1),
                    ContainingSymbolDisplayName = containingSymbol?.ToDisplayString() ?? document.Name,
                    ContainingSymbolKind = ClassifyContainingSymbolKind(containingSymbol),
                    EventSymbolId = CreateSymbolId(eventSymbol),
                    EventName = eventSymbol.Name,
                    PublisherTypeName = eventSymbol.ContainingType?.ToDisplayString() ?? string.Empty,
                    ProjectName = document.Project.Name,
                    NamespaceName = containingSymbol?.ContainingNamespace?.ToDisplayString()
                        ?? eventSymbol.ContainingNamespace?.ToDisplayString()
                        ?? string.Empty,
                    Accessibility = AccessibilityDisplay.ToDisplayValue(containingSymbol ?? eventSymbol),
                    FilePath = document.FilePath ?? document.Name,
                    LineNumber = lineSpan.StartLinePosition.Line + 1,
                    Kind = EdgeKind.EventRaise,
                    Confidence = AnalysisConfidence.High,
                });
            }
        }

        var dispatches = new List<EventUsageInfo>();
        var activeSubscriptions = subscriptions
            .Where(static item => item.Kind == EdgeKind.EventSubscription)
            .ToList();

        foreach (var raise in raises)
        {
            foreach (var subscription in activeSubscriptions)
            {
                var unsubscribed = unsubscriptions.Contains($"{subscription.EventSymbolId}|{subscription.HandlerSymbolId}|{subscription.HandlerName}");

                dispatches.Add(new EventUsageInfo
                {
                    ContainingSymbolId = raise.ContainingSymbolId,
                    ContainingSymbolDisplayName = raise.ContainingSymbolDisplayName,
                    ContainingSymbolKind = raise.ContainingSymbolKind,
                    EventSymbolId = subscription.EventSymbolId,
                    EventName = subscription.EventName,
                    PublisherTypeName = subscription.PublisherTypeName,
                    HandlerSymbolId = subscription.HandlerSymbolId,
                    HandlerName = subscription.HandlerName,
                    HandlerKind = subscription.HandlerKind,
                    ProjectName = raise.ProjectName,
                    NamespaceName = raise.NamespaceName,
                    Accessibility = raise.Accessibility,
                    FilePath = raise.FilePath,
                    LineNumber = raise.LineNumber,
                    Kind = EdgeKind.EventDispatchEstimated,
                    Confidence = unsubscribed ? AnalysisConfidence.Unclear : AnalysisConfidence.Estimated,
                    IsUnsubscribed = unsubscribed,
                });
            }
        }

        subscriptions.AddRange(dispatches);
        subscriptions.AddRange(raises);
        return subscriptions;
    }

    private async Task<IReadOnlyList<EventUsageInfo>> CollectForHandlerAsync(
        IMethodSymbol methodSymbol,
        Solution solution,
        AnalyzeOptions options,
        CancellationToken cancellationToken)
    {
        var results = new List<EventUsageInfo>();

        foreach (var document in solution.Projects.SelectMany(static project => project.Documents))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            var filePath = document.FilePath ?? document.Name;
            if (!AnalysisDocumentFilter.ShouldInclude(document.Project.Name, filePath, options))
            {
                continue;
            }

            foreach (var assignment in syntaxRoot.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddAssignmentExpression) &&
                    !assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SubtractAssignmentExpression))
                {
                    continue;
                }

                var eventAssignment = semanticModel.GetOperation(assignment, cancellationToken) as IEventAssignmentOperation;
                var handlerSymbol = TryGetHandlerMethodSymbol(
                    semanticModel,
                    assignment.Right,
                    eventAssignment?.HandlerValue,
                    cancellationToken);
                if (!SymbolEqualityComparer.Default.Equals(handlerSymbol, methodSymbol))
                {
                    continue;
                }

                var eventSymbol = GetEventSymbol(eventAssignment);
                if (eventSymbol is null)
                {
                    continue;
                }

                var usage = CreateAssignmentUsage(document, semanticModel, assignment, eventAssignment, cancellationToken);
                if (usage is not null)
                {
                    results.Add(usage);
                }
            }
        }

        return results;
    }

    private static EventUsageInfo? CreateAssignmentUsage(
        Document document,
        SemanticModel semanticModel,
        AssignmentExpressionSyntax assignment,
        IEventAssignmentOperation? eventAssignment,
        CancellationToken cancellationToken)
    {
        var eventSymbol = GetEventSymbol(eventAssignment);
        if (eventSymbol is null)
        {
            return null;
        }

        var containingSymbol = semanticModel.GetEnclosingSymbol(assignment.SpanStart, cancellationToken);
        var lineSpan = assignment.GetLocation().GetLineSpan();
        var (handlerId, handlerName, handlerKind) = GetHandlerInfo(
            document,
            semanticModel,
            eventAssignment?.HandlerValue,
            assignment.Right,
            cancellationToken);

        return new EventUsageInfo
        {
            ContainingSymbolId = CreateLocationSymbolId(containingSymbol?.ToDisplayString() ?? document.Name, document.FilePath ?? document.Name, lineSpan.StartLinePosition.Line + 1),
            ContainingSymbolDisplayName = containingSymbol?.ToDisplayString() ?? document.Name,
            ContainingSymbolKind = ClassifyContainingSymbolKind(containingSymbol),
            EventSymbolId = CreateSymbolId(eventSymbol),
            EventName = eventSymbol.Name,
            PublisherTypeName = eventSymbol.ContainingType?.ToDisplayString() ?? string.Empty,
            HandlerSymbolId = handlerId,
            HandlerName = handlerName,
            HandlerKind = handlerKind,
            ProjectName = document.Project.Name,
            NamespaceName = containingSymbol?.ContainingNamespace?.ToDisplayString()
                ?? eventSymbol.ContainingNamespace?.ToDisplayString()
                ?? string.Empty,
            Accessibility = AccessibilityDisplay.ToDisplayValue(containingSymbol ?? eventSymbol),
            FilePath = document.FilePath ?? document.Name,
            LineNumber = lineSpan.StartLinePosition.Line + 1,
            Kind = assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SubtractAssignmentExpression)
                ? EdgeKind.EventUnsubscription
                : EdgeKind.EventSubscription,
            Confidence = handlerKind == NodeKind.AnonymousFunction ? AnalysisConfidence.High : AnalysisConfidence.Confirmed,
            IsUnsubscribed = assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SubtractAssignmentExpression),
        };
    }

    private static (string Id, string Name, NodeKind Kind) GetHandlerInfo(
        Document document,
        SemanticModel semanticModel,
        IOperation? handlerOperation,
        ExpressionSyntax fallbackExpression,
        CancellationToken cancellationToken)
    {
        var methodSymbol = TryGetHandlerMethodSymbol(semanticModel, fallbackExpression, handlerOperation, cancellationToken);
        if (methodSymbol is not null)
        {
            return (CreateSymbolId(methodSymbol), methodSymbol.ToDisplayString(), NodeKind.Method);
        }

        var anonymousFunction = TryGetAnonymousFunction(handlerOperation);
        if (anonymousFunction is not null)
        {
            var lineSpan = anonymousFunction.Syntax.GetLocation().GetLineSpan();
            return (
                CreateLocationSymbolId(anonymousFunction.Syntax.ToString(), document.FilePath ?? document.Name, lineSpan.StartLinePosition.Line + 1),
                anonymousFunction.Syntax.ToString(),
                NodeKind.AnonymousFunction);
        }

        return fallbackExpression switch
        {
            ParenthesizedLambdaExpressionSyntax or SimpleLambdaExpressionSyntax or AnonymousMethodExpressionSyntax
                => (CreateLocationSymbolId(fallbackExpression.ToString(), document.FilePath ?? document.Name, fallbackExpression.GetLocation().GetLineSpan().StartLinePosition.Line + 1),
                    fallbackExpression.ToString(),
                    NodeKind.AnonymousFunction),
            _ => (string.Empty, fallbackExpression.ToString(), NodeKind.Unknown),
        };
    }

    private static ExpressionSyntax? TryGetRaisedEventExpression(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess when memberAccess.Name.Identifier.ValueText == "Invoke" => memberAccess.Expression,
            MemberBindingExpressionSyntax memberBinding when memberBinding.Name.Identifier.ValueText == "Invoke"
                => (invocation.Parent as ConditionalAccessExpressionSyntax)?.Expression,
            _ => null,
        };
    }

    private static NodeKind ClassifyContainingSymbolKind(ISymbol? symbol)
    {
        return symbol switch
        {
            IMethodSymbol => NodeKind.Method,
            IPropertySymbol => NodeKind.Property,
            INamedTypeSymbol namedType when namedType.TypeKind == TypeKind.Interface => NodeKind.Interface,
            INamedTypeSymbol => NodeKind.Class,
            _ => NodeKind.Unknown,
        };
    }

    private static string CreateSymbolId(ISymbol symbol)
    {
        return symbol.GetDocumentationCommentId()
            ?? symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    private static string CreateLocationSymbolId(string displayName, string filePath, int lineNumber)
    {
        return $"{displayName}@{filePath}:{lineNumber}";
    }

    private static IEventSymbol? GetEventSymbol(IEventAssignmentOperation? eventAssignment)
    {
        var eventReference = eventAssignment?.EventReference as IEventReferenceOperation;
        return eventReference?.Event
            ?? eventReference?.Member as IEventSymbol;
    }

    private static IMethodSymbol? TryGetHandlerMethodSymbol(IOperation? operation)
    {
        if (operation is null)
        {
            return null;
        }

        if (operation is IMethodReferenceOperation methodReference)
        {
            return methodReference.Method;
        }

        foreach (var child in operation.ChildOperations)
        {
            var methodSymbol = TryGetHandlerMethodSymbol(child);
            if (methodSymbol is not null)
            {
                return methodSymbol;
            }
        }

        return null;
    }

    private static IMethodSymbol? TryGetHandlerMethodSymbol(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        IOperation? operation,
        CancellationToken cancellationToken)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(expression, cancellationToken);
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol
            ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        if (methodSymbol is not null)
        {
            return methodSymbol;
        }

        return TryGetHandlerMethodSymbol(operation);
    }

    private static IAnonymousFunctionOperation? TryGetAnonymousFunction(IOperation? operation)
    {
        if (operation is null)
        {
            return null;
        }

        if (operation is IAnonymousFunctionOperation anonymousFunction)
        {
            return anonymousFunction;
        }

        foreach (var child in operation.ChildOperations)
        {
            var nestedAnonymousFunction = TryGetAnonymousFunction(child);
            if (nestedAnonymousFunction is not null)
            {
                return nestedAnonymousFunction;
            }
        }

        return null;
    }
}
