using System;
using System.Collections.Generic;
using System.Linq;
using Mapo.Generator.Diagnostics;
using Mapo.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mapo.Generator.Syntax;

internal static class ConfigParser
{
    public static (
        Dictionary<string, (string ParamName, ExpressionSyntax Body)> Maps,
        HashSet<string> Ignores,
        List<GlobalConverter> Converters,
        bool ShouldReverse
    ) ParseSpecificConfiguration(
        List<MethodDeclarationSyntax> configMethods,
        ITypeSymbol source,
        ITypeSymbol target,
        SemanticModel model,
        List<Diagnostic> diagnostics = null
    )
    {
        var maps = new Dictionary<string, (string, ExpressionSyntax)>(StringComparer.OrdinalIgnoreCase);
        var ignores = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var converters = new List<GlobalConverter>();
        bool shouldReverse = false;

        // Collect all matching configs: exact match + base type matches
        // Process base types first so derived mappings take precedence
        var matchedConfigs = new List<(MethodDeclarationSyntax Method, bool IsExact)>();

        foreach (var configMethod in configMethods)
        {
            if (configMethod.ParameterList.Parameters.Count < 1)
                continue;
            var firstParamType =
                model.GetTypeInfo(configMethod.ParameterList.Parameters[0].Type!).Type as INamedTypeSymbol;
            if (firstParamType == null || firstParamType.TypeArguments.Length != 2)
                continue;

            var cfgSource = firstParamType.TypeArguments[0];
            var cfgTarget = firstParamType.TypeArguments[1];

            if (
                SymbolEqualityComparer.Default.Equals(cfgSource, source)
                && SymbolEqualityComparer.Default.Equals(cfgTarget, target)
            )
            {
                matchedConfigs.Add((configMethod, true));
            }
            else if (IsBaseOf(cfgSource, source) && IsBaseOf(cfgTarget, target))
            {
                matchedConfigs.Add((configMethod, false));
            }
        }

        if (matchedConfigs.Count == 0)
            return (maps, ignores, converters, shouldReverse);

        // Process base configs first, then exact — so derived overrides base
        foreach (var (configMethod, _) in matchedConfigs.OrderBy(c => c.IsExact))
        {
            var (cMaps, cIgnores, cConverters, cReverse) = ParseConfiguration(configMethod, model, diagnostics);
            foreach (var kvp in cMaps)
            {
                maps[kvp.Key] = kvp.Value; // Later (derived) overwrites earlier (base)
            }
            foreach (var ig in cIgnores)
                ignores.Add(ig);
            converters.AddRange(cConverters);
            if (cReverse)
                shouldReverse = true;
        }

        return (maps, ignores, converters, shouldReverse);
    }

    private static bool IsBaseOf(ITypeSymbol baseType, ITypeSymbol derivedType)
    {
        var current = derivedType.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    public static (
        Dictionary<string, (string ParamName, ExpressionSyntax Body)> Maps,
        HashSet<string> Ignores,
        List<GlobalConverter> Converters,
        bool ShouldReverse
    ) ParseConfiguration(MethodDeclarationSyntax configMethod, SemanticModel model, List<Diagnostic> diagnostics = null)
    {
        var maps = new Dictionary<string, (string, ExpressionSyntax)>(StringComparer.OrdinalIgnoreCase);
        var ignores = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var converters = new List<GlobalConverter>();
        bool shouldReverse = false;

        // Resolve target type for MAPO007 validation
        ITypeSymbol targetTypeSymbol = null;
        HashSet<string> targetPropertyNames = null;
        if (configMethod.ParameterList.Parameters.Count > 0 && configMethod.ParameterList.Parameters[0].Type != null)
        {
            var configParamType =
                model.GetTypeInfo(configMethod.ParameterList.Parameters[0].Type).Type as INamedTypeSymbol;
            if (configParamType != null && configParamType.TypeArguments.Length == 2)
            {
                targetTypeSymbol = configParamType.TypeArguments[1];
                targetPropertyNames = new HashSet<string>(
                    targetTypeSymbol.GetMembers().OfType<IPropertySymbol>().Select(p => p.Name),
                    StringComparer.OrdinalIgnoreCase
                );
            }
        }

        var expressions = new List<ExpressionSyntax>();
        if (configMethod.Body != null)
            expressions.AddRange(
                configMethod.Body.Statements.OfType<ExpressionStatementSyntax>().Select(s => s.Expression)
            );
        else if (configMethod.ExpressionBody != null)
            expressions.Add(configMethod.ExpressionBody.Expression);

        foreach (var expr in expressions)
        {
            var current = expr;
            while (current is InvocationExpressionSyntax inv)
            {
                if (inv.Expression is MemberAccessExpressionSyntax ma)
                {
                    if (ma.Name.Identifier.Text == "Map" && inv.ArgumentList.Arguments.Count == 2)
                    {
                        var targetLambda = inv.ArgumentList.Arguments[0].Expression as LambdaExpressionSyntax;
                        var sourceLambda = inv.ArgumentList.Arguments[1].Expression as LambdaExpressionSyntax;
                        if (targetLambda?.Body is MemberAccessExpressionSyntax targetMa)
                        {
                            var targetProp = targetMa.Name.Identifier.Text;
                            var paramName = GetParamName(sourceLambda);

                            // MAPO007: Validate target property exists
                            if (targetPropertyNames != null && !targetPropertyNames.Contains(targetProp))
                            {
                                diagnostics?.Add(
                                    Diagnostic.Create(
                                        DiagnosticDescriptors.InvalidTargetProperty,
                                        targetMa.Name.GetLocation(),
                                        targetProp,
                                        targetTypeSymbol.ToDisplayString()
                                    )
                                );
                            }

                            // MAPO008: Validate source expression
                            if (sourceLambda?.Body != null)
                            {
                                var typeInfo = model.GetTypeInfo(sourceLambda.Body);
                                if (typeInfo.Type is IErrorTypeSymbol)
                                {
                                    diagnostics?.Add(
                                        Diagnostic.Create(
                                            DiagnosticDescriptors.InvalidSourceExpression,
                                            sourceLambda.Body.GetLocation(),
                                            targetProp
                                        )
                                    );
                                }
                            }

                            // MAPO006: Duplicate target property mapping
                            if (maps.ContainsKey(targetProp))
                            {
                                diagnostics?.Add(
                                    Diagnostic.Create(
                                        DiagnosticDescriptors.DuplicateTargetMapping,
                                        targetMa.Name.GetLocation(),
                                        targetProp
                                    )
                                );
                            }
                            else if (sourceLambda?.Body is ExpressionSyntax bodyExpr)
                            {
                                maps[targetProp] = (paramName, bodyExpr);
                            }
                        }
                    }
                    else if (ma.Name.Identifier.Text == "Ignore" && inv.ArgumentList.Arguments.Count == 1)
                    {
                        var targetLambda = inv.ArgumentList.Arguments[0].Expression as LambdaExpressionSyntax;
                        if (targetLambda?.Body is MemberAccessExpressionSyntax targetMa)
                        {
                            var ignoredProp = targetMa.Name.Identifier.Text;

                            // MAPO007: Validate ignored property exists
                            if (targetPropertyNames != null && !targetPropertyNames.Contains(ignoredProp))
                            {
                                diagnostics?.Add(
                                    Diagnostic.Create(
                                        DiagnosticDescriptors.InvalidTargetProperty,
                                        targetMa.Name.GetLocation(),
                                        ignoredProp,
                                        targetTypeSymbol.ToDisplayString()
                                    )
                                );
                            }

                            ignores.Add(ignoredProp);
                        }
                    }
                    else if (ma.Name.Identifier.Text == "AddConverter" && inv.ArgumentList.Arguments.Count == 1)
                    {
                        var converterLambda = inv.ArgumentList.Arguments[0].Expression as LambdaExpressionSyntax;
                        if (converterLambda != null)
                        {
                            var methodSymbol = model.GetSymbolInfo(inv).Symbol as IMethodSymbol;
                            if (methodSymbol != null && methodSymbol.TypeArguments.Length == 2)
                            {
                                var paramName = GetParamName(converterLambda);
                                converters.Add(
                                    new GlobalConverter(
                                        methodSymbol.TypeArguments[0].ToDisplayString(),
                                        methodSymbol.TypeArguments[1].ToDisplayString(),
                                        methodSymbol.TypeArguments[1].SpecialType == SpecialType.System_String,
                                        paramName,
                                        converterLambda.Body.ToString()
                                    )
                                );
                            }
                        }
                    }
                    else if (ma.Name.Identifier.Text == "ReverseMap")
                    {
                        shouldReverse = true;
                    }
                    current = ma.Expression as InvocationExpressionSyntax;
                }
                else
                    break;
            }
        }
        return (maps, ignores, converters, shouldReverse);
    }

    private static string GetParamName(LambdaExpressionSyntax? lambda)
    {
        if (lambda is SimpleLambdaExpressionSyntax s)
            return s.Parameter.Identifier.Text;
        if (lambda is ParenthesizedLambdaExpressionSyntax p && p.ParameterList.Parameters.Count > 0)
            return p.ParameterList.Parameters[0].Identifier.Text;
        return string.Empty;
    }
}
