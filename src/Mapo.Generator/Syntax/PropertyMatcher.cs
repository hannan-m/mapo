using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Mapo.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mapo.Generator.Syntax;

internal static class PropertyMatcher
{
    public static bool TryMatchSource(
        string targetName,
        ITypeSymbol targetType,
        ITypeSymbol sourceType,
        string sourceName,
        Dictionary<string, (string ParamName, ExpressionSyntax Body)> customMappings,
        List<IMethodSymbol> partialMethods,
        Dictionary<string, (ITypeSymbol, ITypeSymbol)> enumMappings,
        Dictionary<string, string> injectedRenames,
        List<GlobalConverter> globalConverters,
        out string? expression,
        List<(ITypeSymbol, ITypeSymbol)> discoveryList,
        Dictionary<(ITypeSymbol, ITypeSymbol), string> nameMap,
        bool isConstructor,
        SemanticModel model,
        out List<string>? navigationSegments,
        out bool requiresNullGuard,
        out string mappingOrigin,
        out bool hasNullableMismatch
    )
    {
        expression = null;
        navigationSegments = null;
        requiresNullGuard = false;
        mappingOrigin = "Direct";
        hasNullableMismatch = false;

        if (!isConstructor && injectedRenames.TryGetValue(targetName, out var injectedName))
        {
            expression = injectedName;
            mappingOrigin = "Injected";
            return true;
        }

        if (customMappings.TryGetValue(targetName, out var custom))
        {
            var rewriter = new ParameterRewriter(
                custom.ParamName,
                sourceName,
                ImmutableHashSet<string>.Empty,
                injectedRenames
            );
            var bodyNode = rewriter.Visit(custom.Body);
            var body = bodyNode.ToFullString();

            var cleanProp = body.Replace(sourceName + ".", "");
            var sourcePropsForCheck = TypeHelpers
                .GetAllProperties(sourceType)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            if (sourcePropsForCheck.TryGetValue(cleanProp, out var sPropCheck))
            {
                var sPropDisplay = sPropCheck.Type.ToDisplayString();
                var sPropStripped = TypeHelpers.StripNullableAnnotation(sPropCheck.Type).ToDisplayString();
                var tDisplay = targetType.ToDisplayString();
                var tStripped = TypeHelpers.StripNullableAnnotation(targetType).ToDisplayString();

                var converter = globalConverters.FirstOrDefault(c =>
                    (c.SourceTypeDisplayString == sPropDisplay || c.SourceTypeDisplayString == sPropStripped)
                    && (c.TargetTypeDisplayString == tDisplay || c.TargetTypeDisplayString == tStripped)
                );
                if (converter != null)
                {
                    var converterExpr = System.Text.RegularExpressions.Regex.Replace(
                        converter.Expression,
                        $@"\b{System.Text.RegularExpressions.Regex.Escape(converter.ParamName)}\b",
                        $"({body.Replace("$", "$$")})"
                    );

                    if (sPropDisplay != sPropStripped && converter.SourceTypeDisplayString == sPropStripped)
                    {
                        expression = $"({body} != null ? {converterExpr} : default)";
                    }
                    else
                    {
                        expression = converterExpr;
                    }
                    mappingOrigin = "Converter";
                    return true;
                }

                if (!SymbolEqualityComparer.Default.Equals(sPropCheck.Type, targetType))
                {
                    var pair = (sPropCheck.Type, targetType);
                    string? mName;
                    if (
                        nameMap.TryGetValue(pair, out mName)
                        || TryFindMapper(sPropCheck.Type, targetType, partialMethods, out mName)
                    )
                    {
                        expression = $"{mName}({body})";
                        mappingOrigin = "Custom";
                        return true;
                    }

                    if (TypeHelpers.IsCollection(sPropCheck.Type) && TypeHelpers.IsCollection(targetType))
                    {
                        var sItem = TypeHelpers.GetItemType(sPropCheck.Type);
                        var tItem = TypeHelpers.GetItemType(targetType);
                        if (
                            sItem != null
                            && tItem != null
                            && TypeHelpers.IsMappable(sItem)
                            && TypeHelpers.IsMappable(tItem)
                            && sItem.SpecialType == SpecialType.None
                            && tItem.SpecialType == SpecialType.None
                        )
                        {
                            discoveryList.Add(pair);
                            mName = nameMap.TryGetValue(pair, out var existing)
                                ? existing
                                : (
                                    "Map"
                                    + TypeHelpers.CleanGenericName(sPropCheck.Type)
                                    + "To"
                                    + TypeHelpers.CleanGenericName(targetType)
                                );
                            nameMap[pair] = mName;
                            expression = $"{mName}({body})";
                            mappingOrigin = "Custom";
                            return true;
                        }
                    }

                    if (sPropCheck.Type.TypeKind == TypeKind.Enum && targetType.TypeKind == TypeKind.Enum)
                    {
                        var enumName = $"Map{sPropCheck.Type.Name}To{targetType.Name}";
                        enumMappings[enumName] = (sPropCheck.Type, targetType);
                        expression = $"{enumName}({body})";
                        mappingOrigin = "Custom";
                        return true;
                    }

                    if (
                        sPropCheck.Type.TypeKind == TypeKind.Enum
                        && targetType.SpecialType == SpecialType.System_String
                    )
                    {
                        expression = $"({body}).ToString()";
                        mappingOrigin = "Custom";
                        return true;
                    }

                    if (
                        sPropCheck.Type.SpecialType == SpecialType.System_String
                        && targetType.TypeKind == TypeKind.Enum
                    )
                    {
                        expression = $"System.Enum.Parse<{targetType.ToDisplayString()}>({body})";
                        mappingOrigin = "Custom";
                        return true;
                    }

                    if (
                        TypeHelpers.IsMappable(sPropCheck.Type)
                        && TypeHelpers.IsMappable(targetType)
                        && sPropCheck.Type.SpecialType == SpecialType.None
                        && targetType.SpecialType == SpecialType.None
                    )
                    {
                        discoveryList.Add(pair);
                        mName = nameMap.TryGetValue(pair, out var existing)
                            ? existing
                            : (
                                "Map"
                                + TypeHelpers.CleanGenericName(sPropCheck.Type)
                                + "To"
                                + TypeHelpers.CleanGenericName(targetType)
                            );
                        nameMap[pair] = mName;
                        expression = $"{mName}({body})";
                        mappingOrigin = "Custom";
                        return true;
                    }
                }
            }
            else if (TypeHelpers.IsCollection(targetType) && body.Contains(".Select") && body.Contains(".ToList()"))
            {
                var tItem = TypeHelpers.GetItemType(targetType);
                var sItemInfo = model.GetTypeInfo(custom.Body);
                var sItemCollection = sItemInfo.Type;
                var sItem = sItemCollection != null ? TypeHelpers.GetItemType(sItemCollection) : null;

                if (tItem != null && sItem != null)
                {
                    var pair = (sItem, tItem);
                    string? mItemName;
                    if (
                        nameMap.TryGetValue(pair, out mItemName)
                        || TryFindMapper(sItem, tItem, partialMethods, out mItemName)
                    )
                    {
                        expression = body.Replace(".ToList()", $".Select({mItemName}).ToList()");
                        mappingOrigin = "Custom";
                        return true;
                    }
                    else if (
                        TypeHelpers.IsMappable(sItem)
                        && TypeHelpers.IsMappable(tItem)
                        && sItem.SpecialType == SpecialType.None
                        && tItem.SpecialType == SpecialType.None
                    )
                    {
                        var autoItemName =
                            "Map" + TypeHelpers.CleanGenericName(sItem) + "To" + TypeHelpers.CleanGenericName(tItem);
                        discoveryList.Add(pair);
                        nameMap[pair] = autoItemName;
                        expression = body.Replace(".ToList()", $".Select({autoItemName}).ToList()");
                        mappingOrigin = "Custom";
                        return true;
                    }
                }
            }

            expression = body;
            mappingOrigin = "Custom";
            return true;
        }

        var sourceProps = TypeHelpers
            .GetAllProperties(sourceType)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        if (sourceProps.TryGetValue(targetName, out var sourceProp))
        {
            var sourceDisplay = sourceProp.Type.ToDisplayString();
            var strippedSourceDisplay = TypeHelpers.StripNullableAnnotation(sourceProp.Type).ToDisplayString();
            var targetDisplay = targetType.ToDisplayString();
            var strippedTargetDisplay = TypeHelpers.StripNullableAnnotation(targetType).ToDisplayString();

            var gConverter = globalConverters.FirstOrDefault(c =>
                (c.SourceTypeDisplayString == sourceDisplay || c.SourceTypeDisplayString == strippedSourceDisplay)
                && (c.TargetTypeDisplayString == targetDisplay || c.TargetTypeDisplayString == strippedTargetDisplay)
            );
            if (gConverter != null)
            {
                var sourceAccess = $"{sourceName}.{sourceProp.Name}";
                var converterExpr = System.Text.RegularExpressions.Regex.Replace(
                    gConverter.Expression,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(gConverter.ParamName)}\b",
                    $"({sourceAccess.Replace("$", "$$")})"
                );

                // Wrap with null check if source was nullable but converter expects non-nullable
                if (
                    sourceDisplay != strippedSourceDisplay
                    && gConverter.SourceTypeDisplayString == strippedSourceDisplay
                )
                {
                    expression = $"({sourceName}.{sourceProp.Name} != null ? {converterExpr} : default)";
                }
                else
                {
                    expression = converterExpr;
                }
                mappingOrigin = "Converter";
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(sourceProp.Type, targetType))
            {
                expression = $"{sourceName}.{sourceProp.Name}";
                mappingOrigin = "Direct";

                // Nullable reference type mismatch: string? → string
                if (
                    !sourceProp.Type.IsValueType
                    && sourceProp.NullableAnnotation == NullableAnnotation.Annotated
                    && targetType.NullableAnnotation == NullableAnnotation.NotAnnotated
                )
                {
                    hasNullableMismatch = true;
                    expression = $"{sourceName}.{sourceProp.Name}!";
                }

                return true;
            }

            // Nullable value type auto-coercion: Nullable<T> -> T with ?? default
            if (
                sourceProp.Type is INamedTypeSymbol sourceNamed
                && sourceNamed.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                && sourceNamed.TypeArguments.Length == 1
                && SymbolEqualityComparer.Default.Equals(sourceNamed.TypeArguments[0], targetType)
            )
            {
                expression = $"({sourceName}.{sourceProp.Name} ?? default)";
                mappingOrigin = "Direct";
                hasNullableMismatch = true;
                return true;
            }

            var pair = (sourceProp.Type, targetType);
            string? mName;

            if (TypeHelpers.IsCollection(sourceProp.Type) && TypeHelpers.IsCollection(targetType))
            {
                var sItem = TypeHelpers.GetItemType(sourceProp.Type);
                var tItem = TypeHelpers.GetItemType(targetType);
                if (
                    sItem != null
                    && tItem != null
                    && TypeHelpers.IsMappable(sItem)
                    && TypeHelpers.IsMappable(tItem)
                    && sItem.SpecialType == SpecialType.None
                    && tItem.SpecialType == SpecialType.None
                )
                {
                    discoveryList.Add(pair);
                    if (!nameMap.TryGetValue(pair, out mName))
                    {
                        mName =
                            "Map"
                            + TypeHelpers.CleanGenericName(sourceProp.Type)
                            + "To"
                            + TypeHelpers.CleanGenericName(targetType);
                        nameMap[pair] = mName;
                    }

                    var itemPair = (sItem, tItem);
                    string? itemMapperName;
                    if (
                        !nameMap.TryGetValue(itemPair, out itemMapperName)
                        && !TryFindMapper(sItem, tItem, partialMethods, out itemMapperName)
                    )
                    {
                        itemMapperName =
                            "Map" + TypeHelpers.CleanGenericName(sItem) + "To" + TypeHelpers.CleanGenericName(tItem);
                        nameMap[itemPair] = itemMapperName;
                        discoveryList.Add(itemPair);
                    }

                    expression = $"{mName}({sourceName}.{sourceProp.Name})";
                    mappingOrigin = "Collection";
                    return true;
                }

                // Collection element conversion: List<string> <-> List<Enum>
                if (sItem != null && tItem != null)
                {
                    string? elementExpr = null;

                    if (sItem.SpecialType == SpecialType.System_String && tItem.TypeKind == TypeKind.Enum)
                    {
                        elementExpr = $"System.Enum.Parse<{tItem.ToDisplayString()}>(_item)";
                    }
                    else if (sItem.TypeKind == TypeKind.Enum && tItem.SpecialType == SpecialType.System_String)
                    {
                        elementExpr = "_item.ToString()";
                    }

                    if (elementExpr != null)
                    {
                        var sourceExpr = $"{sourceName}.{sourceProp.Name}";
                        bool sourceIsNullable = sourceProp.NullableAnnotation == NullableAnnotation.Annotated;
                        if (sourceIsNullable)
                        {
                            expression =
                                $"({sourceExpr}?.Select(_item => {elementExpr}).ToList() ?? new System.Collections.Generic.List<{tItem.ToDisplayString()}>())";
                        }
                        else
                        {
                            expression = $"{sourceExpr}.Select(_item => {elementExpr}).ToList()";
                        }
                        mappingOrigin = "Collection";
                        return true;
                    }
                }
            }

            if (sourceProp.Type.TypeKind == TypeKind.Enum && targetType.TypeKind == TypeKind.Enum)
            {
                var enumName = $"Map{sourceProp.Type.Name}To{targetType.Name}";
                enumMappings[enumName] = (sourceProp.Type, targetType);
                expression = $"{enumName}({sourceName}.{sourceProp.Name})";
                mappingOrigin = "EnumConversion";
                return true;
            }

            if (sourceProp.Type.TypeKind == TypeKind.Enum && targetType.SpecialType == SpecialType.System_String)
            {
                expression = $"{sourceName}.{sourceProp.Name}.ToString()";
                mappingOrigin = "EnumConversion";
                return true;
            }

            if (sourceProp.Type.SpecialType == SpecialType.System_String && targetType.TypeKind == TypeKind.Enum)
            {
                if (sourceProp.NullableAnnotation == NullableAnnotation.Annotated)
                {
                    expression = $"System.Enum.Parse<{targetType.ToDisplayString()}>({sourceName}.{sourceProp.Name}!)";
                    hasNullableMismatch = true;
                }
                else
                {
                    expression = $"System.Enum.Parse<{targetType.ToDisplayString()}>({sourceName}.{sourceProp.Name})";
                }
                mappingOrigin = "EnumConversion";
                return true;
            }

            if (
                TypeHelpers.IsMappable(sourceProp.Type)
                && TypeHelpers.IsMappable(targetType)
                && sourceProp.Type.SpecialType == SpecialType.None
                && targetType.SpecialType == SpecialType.None
            )
            {
                var strippedSource = TypeHelpers.StripNullableAnnotation(sourceProp.Type);
                var strippedTarget = TypeHelpers.StripNullableAnnotation(targetType);
                var discoveryPair = (strippedSource, strippedTarget);
                discoveryList.Add(discoveryPair);
                if (!nameMap.TryGetValue(discoveryPair, out mName))
                {
                    mName =
                        "Map"
                        + TypeHelpers.CleanGenericName(strippedSource)
                        + "To"
                        + TypeHelpers.CleanGenericName(strippedTarget);
                    nameMap[discoveryPair] = mName;
                }

                bool sourceIsNullable =
                    sourceProp.NullableAnnotation == NullableAnnotation.Annotated
                    || (
                        sourceProp.Type is INamedTypeSymbol sNullable
                        && sNullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                    );

                if (sourceIsNullable)
                {
                    expression =
                        $"({sourceName}.{sourceProp.Name} != null ? {mName}({sourceName}.{sourceProp.Name}) : default)";
                }
                else
                {
                    expression = $"{mName}({sourceName}.{sourceProp.Name})";
                }
                mappingOrigin = "NestedObject";
                return true;
            }
        }

        if (
            TryFlattenNullSafe(
                targetName,
                targetType,
                sourceType,
                sourceName,
                out expression,
                out navigationSegments,
                out requiresNullGuard
            )
        )
        {
            mappingOrigin = "Flattened";
            return true;
        }

        return false;
    }

    private static bool TryFlattenNullSafe(
        string targetName,
        ITypeSymbol targetType,
        ITypeSymbol sourceType,
        string sourceName,
        out string? expression,
        out List<string>? navigationSegments,
        out bool requiresNullGuard,
        int depth = 0
    )
    {
        expression = null;
        navigationSegments = null;
        requiresNullGuard = false;
        if (depth > 2)
            return false; // Cap at 4 levels of navigation (depth + 2 segments)

        var sourceProps = sourceType.GetMembers().OfType<IPropertySymbol>().ToList();

        foreach (var sProp in sourceProps)
        {
            if (!targetName.StartsWith(sProp.Name, StringComparison.OrdinalIgnoreCase))
                continue;
            var remaining = targetName.Substring(sProp.Name.Length);
            if (remaining.Length == 0)
                continue;

            // Direct match at this level
            var nestedProp = sProp
                .Type.GetMembers()
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p => p.Name.Equals(remaining, StringComparison.OrdinalIgnoreCase));

            if (nestedProp != null && SymbolEqualityComparer.Default.Equals(nestedProp.Type, targetType))
            {
                expression = $"{sourceName}.{sProp.Name}.{nestedProp.Name}";
                navigationSegments = new List<string> { sourceName, sProp.Name, nestedProp.Name };
                requiresNullGuard = true;
                return true;
            }

            // Recurse deeper
            if (
                TryFlattenNullSafe(
                    remaining,
                    targetType,
                    sProp.Type,
                    $"{sourceName}.{sProp.Name}",
                    out expression,
                    out navigationSegments,
                    out requiresNullGuard,
                    depth + 1
                )
            )
            {
                requiresNullGuard = true;
                return true;
            }
        }
        return false;
    }

    private static bool TryFindMapper(ITypeSymbol s, ITypeSymbol t, List<IMethodSymbol> methods, out string? name)
    {
        name = methods
            .FirstOrDefault(m =>
                SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, s)
                && SymbolEqualityComparer.Default.Equals(m.ReturnType, t)
            )
            ?.Name;
        return name != null;
    }
}
