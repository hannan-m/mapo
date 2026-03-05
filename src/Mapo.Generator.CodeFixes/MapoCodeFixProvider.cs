using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mapo.Generator.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mapo.Generator.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MapoCodeFixProvider)), Shared]
public class MapoCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(
            DiagnosticDescriptors.UnmappedPropertyWarning.Id,
            DiagnosticDescriptors.MapperOnNonPartialClass.Id
        );

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
            return;

        var diagnostic = context.Diagnostics.First();

        if (diagnostic.Id == DiagnosticDescriptors.MapperOnNonPartialClass.Id)
        {
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            var classDecl = token.Parent?.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDecl == null)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Add 'partial' modifier",
                    createChangedDocument: c => AddPartialModifierAsync(context.Document, classDecl, c),
                    equivalenceKey: "AddPartial"
                ),
                diagnostic
            );
            return;
        }

        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the method declaration (the partial mapping method)
        var methodDeclaration = root.FindToken(diagnosticSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();
        if (methodDeclaration == null)
            return;

        // Find the class containing this method
        var classDeclaration = methodDeclaration.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDeclaration == null)
            return;

        // Find the Configure method in the same class
        var configureMethod = classDeclaration
            .Members.OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "Configure");

        if (configureMethod == null)
            return;

        // Extract property name from diagnostic message
        // Message format: "Property '{0}' in target type '{1}' is not mapped..."
        var parts = diagnostic.GetMessage().Split('\'');
        if (parts.Length < 2)
            return;
        var propertyName = parts[1];

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Map property '{propertyName}'",
                createChangedDocument: c => AddMappingAsync(context.Document, configureMethod, propertyName, c),
                equivalenceKey: $"Map_{propertyName}"
            ),
            diagnostic
        );
    }

    private static async Task<Document> AddPartialModifierAsync(
        Document document,
        ClassDeclarationSyntax classDecl,
        CancellationToken cancellationToken
    )
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        var newClassDecl = classDecl.AddModifiers(
            SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space)
        );
        var newRoot = root.ReplaceNode(classDecl, newClassDecl);
        return document.WithSyntaxRoot(newRoot);
    }

    private async Task<Document> AddMappingAsync(
        Document document,
        MethodDeclarationSyntax configureMethod,
        string propertyName,
        CancellationToken cancellationToken
    )
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        // Find the expression statement in Configure (e.g. config.Map(...))
        var expressionStatement = configureMethod.Body?.Statements.OfType<ExpressionStatementSyntax>().FirstOrDefault();
        if (expressionStatement == null)
            return document;

        // Simple implementation: Append .Map(d => d.Prop, s => s.Prop) to the existing chain
        var newMapping = SyntaxFactory.ParseExpression($".Map(d => d.{propertyName}, s => s.{propertyName})");
        var newExpression = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expressionStatement.Expression,
                SyntaxFactory.IdentifierName("Map")
            ),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(
                    new[]
                    {
                        SyntaxFactory.Argument(SyntaxFactory.ParseExpression($"d => d.{propertyName}")),
                        SyntaxFactory.Argument(SyntaxFactory.ParseExpression($"s => s.{propertyName}")),
                    }
                )
            )
        );

        var newStatement = expressionStatement.WithExpression(newExpression);
        var newRoot = root.ReplaceNode(expressionStatement, newStatement);

        return document.WithSyntaxRoot(newRoot);
    }
}
