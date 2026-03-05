using System;
using System.Collections.Generic;
using System.Linq;
using Mapo.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mapo.Generator.Syntax;

internal static class MethodMappingFactory
{
    public static MethodMapping CreateMethodMapping(
        IMethodSymbol? methodSymbol,
        ITypeSymbol sourceType,
        ITypeSymbol targetType,
        string sourceName,
        Dictionary<string, (string ParamName, ExpressionSyntax Body)> customMappings,
        HashSet<string> ignoredProps,
        List<IMethodSymbol> partialMethods,
        Dictionary<string, (ITypeSymbol, ITypeSymbol)> enumMappings,
        Dictionary<string, string> injectedRenames,
        List<GlobalConverter> globalConverters,
        bool isUpdate,
        Location? location,
        List<(ITypeSymbol, ITypeSymbol)> discoveryList,
        Dictionary<(ITypeSymbol, ITypeSymbol), string> nameMap,
        SemanticModel model,
        bool isUserDeclared,
        List<Diagnostic> diagnostics = null
    )
    {
        var propMappings = new List<PropertyMapping>();
        var constructorArgsList = new List<ConstructorArg>();
        var unmappedProperties = new List<string>();
        var targetNamed = targetType as INamedTypeSymbol;

        var derivedMappings = new List<DerivedMappingInfo>();
        if (methodSymbol != null)
        {
            var derivedAttrs = methodSymbol.GetAttributes().Where(a => a.AttributeClass?.Name == "MapDerivedAttribute");
            foreach (var attr in derivedAttrs)
            {
                if (
                    attr.ConstructorArguments.Length == 2
                    && attr.ConstructorArguments[0].Value is ITypeSymbol dSource
                    && attr.ConstructorArguments[1].Value is ITypeSymbol dTarget
                )
                {
                    derivedMappings.Add(new DerivedMappingInfo(dSource.ToDisplayString(), dTarget.ToDisplayString()));
                    discoveryList.Add((dSource, dTarget));
                }
            }
        }

        if (!isUpdate && targetNamed != null)
        {
            var sourcePropertyNames = new HashSet<string>(
                TypeHelpers.GetAllProperties(sourceType).Select(p => p.Name),
                StringComparer.OrdinalIgnoreCase
            );

            var bestCtor = targetNamed
                .Constructors.Where(c =>
                    c.DeclaredAccessibility == Accessibility.Public || c.DeclaredAccessibility == Accessibility.Internal
                )
                .OrderByDescending(c => c.Parameters.Length)
                .ThenByDescending(c => c.Parameters.Count(p => sourcePropertyNames.Contains(p.Name)))
                .FirstOrDefault();

            var handledByCtor = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (bestCtor != null)
            {
                foreach (var param in bestCtor.Parameters)
                {
                    if (
                        PropertyMatcher.TryMatchSource(
                            param.Name,
                            param.Type,
                            sourceType,
                            sourceName,
                            customMappings,
                            partialMethods,
                            enumMappings,
                            injectedRenames,
                            globalConverters,
                            out var expression,
                            discoveryList,
                            nameMap,
                            true,
                            model,
                            out var navSegments,
                            out var requiresNullGuard,
                            out var ctorOrigin,
                            out var ctorNullMismatch
                        )
                    )
                    {
                        if (ctorNullMismatch)
                        {
                            diagnostics?.Add(
                                Diagnostic.Create(
                                    Diagnostics.DiagnosticDescriptors.NullableToNonNullableMapping,
                                    location ?? Location.None,
                                    param.Name,
                                    sourceType
                                        .GetMembers()
                                        .OfType<IPropertySymbol>()
                                        .FirstOrDefault(p =>
                                            p.Name.Equals(param.Name, StringComparison.OrdinalIgnoreCase)
                                        )
                                        ?.Type.ToDisplayString()
                                        ?? "?",
                                    param.Type.ToDisplayString()
                                )
                            );
                        }
                        constructorArgsList.Add(new ConstructorArg(expression!, null, ctorOrigin));
                        handledByCtor.Add(param.Name);
                    }
                    else
                    {
                        constructorArgsList.Add(new ConstructorArg($"default({param.Type.ToDisplayString()})", null));
                    }
                }
            }

            var targetProps = TypeHelpers.GetAllProperties(targetNamed).Where(p => p.SetMethod != null).ToList();
            foreach (var targetProp in targetProps)
            {
                if (ignoredProps.Contains(targetProp.Name) || handledByCtor.Contains(targetProp.Name))
                    continue;
                if (
                    PropertyMatcher.TryMatchSource(
                        targetProp.Name,
                        targetProp.Type,
                        sourceType,
                        sourceName,
                        customMappings,
                        partialMethods,
                        enumMappings,
                        injectedRenames,
                        globalConverters,
                        out var expression,
                        discoveryList,
                        nameMap,
                        false,
                        model,
                        out var navSegments,
                        out var requiresNullGuard,
                        out var propOrigin,
                        out var propNullMismatch
                    )
                )
                {
                    if (propNullMismatch)
                    {
                        var sourcePropSymbol = sourceType
                            .GetMembers()
                            .OfType<IPropertySymbol>()
                            .FirstOrDefault(p => p.Name.Equals(targetProp.Name, StringComparison.OrdinalIgnoreCase));
                        diagnostics?.Add(
                            Diagnostic.Create(
                                Diagnostics.DiagnosticDescriptors.NullableToNonNullableMapping,
                                location ?? Location.None,
                                targetProp.Name,
                                sourcePropSymbol?.Type.ToDisplayString() ?? "?",
                                targetProp.Type.ToDisplayString()
                            )
                        );
                    }

                    bool isInitOnly = targetProp.SetMethod?.IsInitOnly == true;

                    bool isRequired = IsRequired(targetProp);

                    propMappings.Add(
                        new PropertyMapping(
                            targetProp.Name,
                            expression!,
                            targetProp.Type.IsValueType,
                            targetProp.Type.SpecialType == SpecialType.System_String,
                            isInitOnly,
                            isRequired,
                            requiresNullGuard,
                            navSegments,
                            null,
                            propOrigin
                        )
                    );
                }
                else
                {
                    unmappedProperties.Add(targetProp.Name);
                }
            }
        }
        else if (isUpdate && targetNamed != null)
        {
            var targetProps = TypeHelpers.GetAllProperties(targetNamed).Where(p => p.SetMethod != null).ToList();
            foreach (var targetProp in targetProps)
            {
                if (ignoredProps.Contains(targetProp.Name))
                    continue;
                if (
                    PropertyMatcher.TryMatchSource(
                        targetProp.Name,
                        targetProp.Type,
                        sourceType,
                        sourceName,
                        customMappings,
                        partialMethods,
                        enumMappings,
                        injectedRenames,
                        globalConverters,
                        out var expression,
                        discoveryList,
                        nameMap,
                        false,
                        model,
                        out var navSegments,
                        out var requiresNullGuard,
                        out var propOrigin,
                        out var propNullMismatch
                    )
                )
                {
                    if (propNullMismatch)
                    {
                        var sourcePropSymbol = sourceType
                            .GetMembers()
                            .OfType<IPropertySymbol>()
                            .FirstOrDefault(p => p.Name.Equals(targetProp.Name, StringComparison.OrdinalIgnoreCase));
                        diagnostics?.Add(
                            Diagnostic.Create(
                                Diagnostics.DiagnosticDescriptors.NullableToNonNullableMapping,
                                location ?? Location.None,
                                targetProp.Name,
                                sourcePropSymbol?.Type.ToDisplayString() ?? "?",
                                targetProp.Type.ToDisplayString()
                            )
                        );
                    }

                    bool isInitOnly = targetProp.SetMethod?.IsInitOnly == true;

                    bool isRequired = IsRequired(targetProp);

                    propMappings.Add(
                        new PropertyMapping(
                            targetProp.Name,
                            expression!,
                            targetProp.Type.IsValueType,
                            targetProp.Type.SpecialType == SpecialType.System_String,
                            isInitOnly,
                            isRequired,
                            requiresNullGuard,
                            navSegments,
                            null,
                            propOrigin
                        )
                    );
                }
                else
                {
                    unmappedProperties.Add(targetProp.Name);
                }
            }
        }

        string mName;
        var key = (sourceType, targetType);
        if (methodSymbol != null)
            mName = methodSymbol.Name;
        else if (nameMap.TryGetValue(key, out var n))
            mName = n;
        else
            mName = "Map" + TypeHelpers.CleanGenericName(sourceType) + "To" + TypeHelpers.CleanGenericName(targetType);

        var mParams =
            methodSymbol?.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}").ToList()
            ?? new List<string> { $"{sourceType.ToDisplayString()} {sourceName}" };

        bool isCollection = TypeHelpers.IsCollection(sourceType) && TypeHelpers.IsCollection(targetType);
        var sItem = isCollection ? TypeHelpers.GetItemType(sourceType) : null;
        var tItem = isCollection ? TypeHelpers.GetItemType(targetType) : null;

        return new MethodMapping(
            methodName: mName,
            sourceTypeDisplayString: sourceType.ToDisplayString(),
            targetTypeDisplayString: targetType.ToDisplayString(),
            targetTypeName: targetType.Name,
            targetIsAbstract: targetType.IsAbstract,
            sourceName: sourceName,
            parameters: mParams,
            constructorArgs: constructorArgsList,
            propertyMappings: propMappings,
            unmappedProperties: unmappedProperties,
            isUserDeclared: isUserDeclared,
            isEnumMapping: false,
            enumCases: null,
            generateProjection: !isUpdate && mParams.Count == 1,
            isUpdateMapping: isUpdate,
            derivedMappings: derivedMappings,
            isCollectionMapping: isCollection,
            sourceItemTypeDisplayString: sItem?.ToDisplayString(),
            targetItemTypeDisplayString: tItem?.ToDisplayString()
        );
    }

    private static bool IsRequired(IPropertySymbol prop)
    {
        try
        {
            var propInfo = typeof(IPropertySymbol).GetProperty("IsRequired");
            if (propInfo?.GetValue(prop) is true)
                return true;
        }
        catch { }
        return prop.GetAttributes().Any(a => a.AttributeClass?.Name == "RequiredMemberAttribute");
    }
}
