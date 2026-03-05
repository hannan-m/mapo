using Mapo.Generator.Diagnostics;
using Mapo.Generator.Emit;
using Mapo.Generator.Models;
using Mapo.Generator.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mapo.Generator;

[Generator]
public class MapoGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax c && c.AttributeLists.Count > 0,
                transform: static (ctx, ct) => MapperParser.Parse(ctx.Node, ctx.SemanticModel, ct)
            )
            .Where(static m => m is not null);

        context.RegisterSourceOutput(provider, Execute);
    }

    private static void Execute(SourceProductionContext context, ParseResult? result)
    {
        if (result == null)
            return;

        // Report diagnostics collected during parsing
        foreach (var diag in result.Diagnostics)
        {
            context.ReportDiagnostic(diag);
        }

        var mapper = result.Mapper;
        if (mapper == null)
            return;

        // Report MAPO001 for unmapped properties
        foreach (var mapping in mapper.Mappings)
        {
            foreach (var unmapped in mapping.UnmappedProperties)
            {
                var descriptor = DiagnosticDescriptors.UnmappedPropertyWarning;
                var severity = mapper.StrictMode ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning;

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        descriptor.Id,
                        descriptor.Category,
                        string.Format(descriptor.MessageFormat.ToString(), unmapped, mapping.TargetTypeName),
                        severity,
                        severity,
                        true,
                        severity == DiagnosticSeverity.Error ? 0 : 1,
                        title: descriptor.Title,
                        location: Location.None
                    )
                );
            }
        }

        // Emit the source code
        var source = MapperEmitter.Emit(mapper);
        context.AddSource($"{mapper.ClassName}.g.cs", source);
    }
}
