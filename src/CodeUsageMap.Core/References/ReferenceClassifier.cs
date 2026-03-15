using CodeUsageMap.Contracts.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeUsageMap.Core.References;

internal static class ReferenceClassifier
{
    public static EdgeKind Classify(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(semanticModel);

        var classifiedNode = node
            .AncestorsAndSelf()
            .FirstOrDefault(
                static candidate => candidate is InvocationExpressionSyntax
                    or ObjectCreationExpressionSyntax
                    or ImplicitObjectCreationExpressionSyntax
                    or AttributeSyntax
                    or AssignmentExpressionSyntax);

        return classifiedNode switch
        {
            InvocationExpressionSyntax invocation => ClassifyInvocation(invocation, semanticModel, cancellationToken),
            ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax => EdgeKind.InstantiatedBy,
            AttributeSyntax => EdgeKind.Reference,
            AssignmentExpressionSyntax assignment when assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddAssignmentExpression) => EdgeKind.EventSubscription,
            AssignmentExpressionSyntax assignment when assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SubtractAssignmentExpression) => EdgeKind.EventUnsubscription,
            _ => EdgeKind.Reference,
        };
    }

    private static EdgeKind ClassifyInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var symbol = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol
            ?? semanticModel.GetSymbolInfo(invocation.Expression, cancellationToken).Symbol as IMethodSymbol;
        if (symbol?.ContainingType?.TypeKind == TypeKind.Interface)
        {
            return EdgeKind.InterfaceDispatch;
        }

        return EdgeKind.DirectCall;
    }
}
