using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ARTR.Pien.CodeAnalysis;

/// <summary>
/// Power-of-Ten Roslyn analyzers for ARTR Pien production code.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PowerOfTenAnalyzer : DiagnosticAnalyzer
{
    public const int MaxLogicalLines = 60;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => PienDescriptors.All;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration, SyntaxKind.LocalFunctionStatement);
        context.RegisterSyntaxNodeAction(AnalyzeGoto, SyntaxKind.GotoStatement, SyntaxKind.GotoCaseStatement, SyntaxKind.GotoDefaultStatement);
        context.RegisterSyntaxNodeAction(AnalyzeUnsafe, SyntaxKind.UnsafeStatement, SyntaxKind.PointerType);
        context.RegisterSyntaxNodeAction(AnalyzeWhile, SyntaxKind.WhileStatement);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeCatch, SyntaxKind.CatchClause);
        context.RegisterSyntaxNodeAction(AnalyzeExpressionStatement, SyntaxKind.ExpressionStatement);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        BlockSyntax? body;
        string name;
        bool isAsync;
        TypeSyntax? returnType;
        ParameterListSyntax parameters;

        IMethodSymbol? methodSymbol;
        if (context.Node is MethodDeclarationSyntax method)
        {
            body = method.Body;
            name = method.Identifier.ValueText;
            isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
            returnType = method.ReturnType;
            parameters = method.ParameterList;
            methodSymbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
            AnalyzeRecursion(context, methodSymbol, body, method.ExpressionBody);
        }
        else if (context.Node is LocalFunctionStatementSyntax local)
        {
            body = local.Body;
            name = local.Identifier.ValueText;
            isAsync = local.Modifiers.Any(SyntaxKind.AsyncKeyword);
            returnType = local.ReturnType;
            parameters = local.ParameterList;
            methodSymbol = context.SemanticModel.GetDeclaredSymbol(local, context.CancellationToken);
            AnalyzeRecursion(context, methodSymbol, body, local.ExpressionBody);
        }
        else
        {
            return;
        }

        if (isAsync && returnType is PredefinedTypeSyntax predefined &&
            predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            context.ReportDiagnostic(Diagnostic.Create(PienDescriptors.AsyncVoidProhibited, context.Node.GetLocation(), name));
        }

        if (body is null)
        {
            return;
        }

        var logicalLines = CountLogicalLines(body);
        if (logicalLines > MaxLogicalLines)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PienDescriptors.MethodTooLong,
                context.Node.GetLocation(),
                name,
                logicalLines,
                MaxLogicalLines));
        }

        _ = parameters;
    }

    private static void AnalyzeRecursion(
        SyntaxNodeAnalysisContext context,
        IMethodSymbol? methodSymbol,
        BlockSyntax? body,
        ArrowExpressionClauseSyntax? expressionBody)
    {
        if (methodSymbol is null)
        {
            return;
        }

        var nodes = body is not null
            ? body.DescendantNodes()
            : expressionBody?.DescendantNodes() ?? Enumerable.Empty<SyntaxNode>();

        foreach (var invocation in nodes.OfType<InvocationExpressionSyntax>())
        {
            var invoked = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
            if (invoked is null)
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(invoked.OriginalDefinition, methodSymbol.OriginalDefinition))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    PienDescriptors.DirectRecursion,
                    invocation.GetLocation(),
                    methodSymbol.Name));
            }
        }
    }

    private static void AnalyzeGoto(SyntaxNodeAnalysisContext context)
        => context.ReportDiagnostic(Diagnostic.Create(PienDescriptors.GotoProhibited, context.Node.GetLocation()));

    private static void AnalyzeUnsafe(SyntaxNodeAnalysisContext context)
        => context.ReportDiagnostic(Diagnostic.Create(PienDescriptors.UnsafeProhibited, context.Node.GetLocation()));

    private static void AnalyzeWhile(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not WhileStatementSyntax whileStatement)
        {
            return;
        }

        if (whileStatement.Condition is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.TrueLiteralExpression))
        {
            // Allow while(true) only when the body references CancellationToken / ThrowIfCancellationRequested / break.
            var text = whileStatement.Statement.ToFullString();
            if (!text.Contains("CancellationToken") &&
                !text.Contains("ThrowIfCancellationRequested") &&
                !text.Contains("IsCancellationRequested") &&
                !text.Contains("break"))
            {
                context.ReportDiagnostic(Diagnostic.Create(PienDescriptors.UnboundedLoop, whileStatement.GetLocation()));
            }
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax member)
        {
            var name = member.Name.Identifier.ValueText;
            if (name is "Wait" or "GetResult")
            {
                context.ReportDiagnostic(Diagnostic.Create(PienDescriptors.SyncOverAsync, member.Name.GetLocation(), name));
            }
        }

        if (invocation.Expression is MemberAccessExpressionSyntax resultAccess &&
            resultAccess.Name.Identifier.ValueText == "Result" &&
            resultAccess.Expression is InvocationExpressionSyntax)
        {
            context.ReportDiagnostic(Diagnostic.Create(PienDescriptors.SyncOverAsync, resultAccess.Name.GetLocation(), "Result"));
        }
    }

    private static void AnalyzeExpressionStatement(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ExpressionStatementSyntax statement)
        {
            return;
        }

        if (statement.Expression is InvocationExpressionSyntax invocation)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(invocation, context.CancellationToken);
            if (typeInfo.Type is INamedTypeSymbol named && IsTaskLike(named))
            {
                context.ReportDiagnostic(Diagnostic.Create(PienDescriptors.IgnoredTask, invocation.GetLocation()));
            }
        }
    }

    private static void AnalyzeCatch(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not CatchClauseSyntax catchClause)
        {
            return;
        }

        if (catchClause.Declaration?.Type is IdentifierNameSyntax id && id.Identifier.ValueText == "Exception")
        {
            var bodyText = catchClause.Block.ToFullString();
            if (!bodyText.Contains("throw") && catchClause.Filter is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(PienDescriptors.BroadCatch, catchClause.GetLocation()));
            }
        }
    }

    private static bool IsTaskLike(INamedTypeSymbol type)
    {
        var name = type.Name;
        return name is "Task" or "ValueTask" ||
               (type.IsGenericType && (type.Name is "Task" or "ValueTask"));
    }

    private static int CountLogicalLines(BlockSyntax body)
    {
        var count = 0;
        foreach (var line in body.ToFullString().Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed is "{" or "}" or "};")
            {
                continue;
            }

            if (trimmed.StartsWith("//", System.StringComparison.Ordinal) ||
                trimmed.StartsWith("///", System.StringComparison.Ordinal) ||
                trimmed.StartsWith("*", System.StringComparison.Ordinal))
            {
                continue;
            }

            count++;
        }

        return count;
    }
}
