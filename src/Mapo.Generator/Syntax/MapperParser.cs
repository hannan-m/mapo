using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Mapo.Generator.Diagnostics;
using Mapo.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mapo.Generator.Syntax;

public static class MapperParser
{
    private const string MapperAttributeName = "MapperAttribute";
    private const string MapperAttributeNamespace = "Mapo.Attributes";
    private const string ConfigureMethodName = "Configure";

    public static ParseResult? Parse(SyntaxNode node, SemanticModel model, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var classDeclaration = (ClassDeclarationSyntax)node;
        var diagnostics = new List<Diagnostic>();

        if (model.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
            return null;
        if (
            !classSymbol
                .GetAttributes()
                .Any(a =>
                    a.AttributeClass?.Name == MapperAttributeName
                    && a.AttributeClass.ContainingNamespace?.ToDisplayString() == MapperAttributeNamespace
                )
        )
            return null;

        // MAPO003: [Mapper] on non-partial class
        if (!classDeclaration.Modifiers.Any(m => m.ValueText == "partial"))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MapperOnNonPartialClass,
                    classDeclaration.Identifier.GetLocation(),
                    classSymbol.Name
                )
            );
            return new ParseResult(null, diagnostics);
        }

        bool isStatic = classSymbol.IsStatic;

        var allConfigureMethodNodes = classDeclaration
            .Members.OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text == ConfigureMethodName)
            .ToList();

        // MAPO004: Validate Configure method signatures
        var configureMethodNodes = new List<MethodDeclarationSyntax>();
        foreach (var configMethod in allConfigureMethodNodes)
        {
            var methodSymbol = model.GetDeclaredSymbol(configMethod) as IMethodSymbol;
            if (methodSymbol == null)
                continue;

            if (!methodSymbol.IsStatic)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidConfigureSignature,
                        configMethod.Identifier.GetLocation(),
                        classSymbol.Name,
                        "Method must be static."
                    )
                );
                continue;
            }

            if (methodSymbol.Parameters.Length == 0)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidConfigureSignature,
                        configMethod.Identifier.GetLocation(),
                        classSymbol.Name,
                        "Method must have at least one parameter of type IMapConfig<TSource, TTarget>."
                    )
                );
                continue;
            }

            var firstParamType = methodSymbol.Parameters[0].Type as INamedTypeSymbol;
            if (
                firstParamType == null
                || firstParamType.Name != "IMapConfig"
                || firstParamType.ContainingNamespace?.ToDisplayString() != MapperAttributeNamespace
                || firstParamType.TypeArguments.Length != 2
            )
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidConfigureSignature,
                        configMethod.ParameterList.Parameters[0].GetLocation(),
                        classSymbol.Name,
                        "First parameter must be of type IMapConfig<TSource, TTarget>."
                    )
                );
                continue;
            }

            configureMethodNodes.Add(configMethod);
        }

        var injectedMembers = new List<InjectedMember>();
        var constructor = classSymbol.Constructors.FirstOrDefault(c => !c.IsImplicitlyDeclared);
        if (constructor != null)
        {
            foreach (var param in constructor.Parameters)
            {
                injectedMembers.Add(new InjectedMember(param.Type.ToDisplayString(), param.Name));
            }
        }

        var globalConverters = new List<GlobalConverter>();
        foreach (var configNode in configureMethodNodes)
        {
            var (_, _, converters, _) = ConfigParser.ParseConfiguration(configNode, model, diagnostics);
            foreach (var c in converters)
            {
                globalConverters.Add(c);
            }
        }

        var mapperAttr = classSymbol
            .GetAttributes()
            .FirstOrDefault(a =>
                a.AttributeClass?.Name == MapperAttributeName
                && a.AttributeClass.ContainingNamespace?.ToDisplayString() == MapperAttributeNamespace
            );
        bool strictMode = false;
        bool useReferenceTracking = false;
        if (mapperAttr != null)
        {
            var strictArg = mapperAttr.NamedArguments.FirstOrDefault(x => x.Key == "StrictMode");
            if (strictArg.Value.Value is bool b)
                strictMode = b;

            var trackingArg = mapperAttr.NamedArguments.FirstOrDefault(x => x.Key == "UseReferenceTracking");
            if (trackingArg.Value.Value is bool t)
                useReferenceTracking = t;
        }

        var resultMappings = new List<MethodMapping>();
        var enumMappingsToGenerate = new Dictionary<string, (ITypeSymbol Source, ITypeSymbol Target)>();
        var injectedRenames = injectedMembers.ToDictionary(m => m.Name, m => $"_{m.Name}");

        var partialMethodNodes = classDeclaration
            .Members.OfType<MethodDeclarationSyntax>()
            .Where(m => m.Modifiers.Any(mod => mod.ValueText == "partial"))
            .ToList();

        var partialMethodSymbols = new List<IMethodSymbol>();
        foreach (var mNode in partialMethodNodes)
        {
            if (model.GetDeclaredSymbol(mNode) is IMethodSymbol mSymbol && mSymbol.Parameters.Length >= 1)
            {
                partialMethodSymbols.Add(mSymbol);
            }
        }

        var nameMap = new Dictionary<(ITypeSymbol, ITypeSymbol), string>(new TypePairComparer());
        var discoveryQueue = new Queue<(ITypeSymbol Source, ITypeSymbol Target, string Name, bool IsUserDeclared)>();
        var queuedPairs = new HashSet<(ITypeSymbol, ITypeSymbol)>(new TypePairComparer());
        // Track parent chain for cycle detection (maps child → parent that discovered it)
        var parentMap = new Dictionary<(ITypeSymbol, ITypeSymbol), (ITypeSymbol, ITypeSymbol)?>(new TypePairComparer());

        // 1. Initial Pass: Register all user-declared partial methods
        foreach (var method in partialMethodNodes)
        {
            if (model.GetDeclaredSymbol(method) is not IMethodSymbol methodSymbol)
                continue;
            if (methodSymbol.Parameters.Length < 1)
                continue;

            var sourceType = methodSymbol.Parameters[0].Type;
            bool isUpdateMapping = methodSymbol.Parameters.Length == 2 && methodSymbol.ReturnsVoid;
            ITypeSymbol targetType = isUpdateMapping ? methodSymbol.Parameters[1].Type : methodSymbol.ReturnType;

            if (targetType is not INamedTypeSymbol)
                continue;

            var key = (sourceType, targetType);
            nameMap[key] = methodSymbol.Name;
            parentMap[key] = null; // root node, no parent
            discoveryQueue.Enqueue((sourceType, targetType, methodSymbol.Name, true));
            queuedPairs.Add(key);
        }

        // 2. Discovery Loop
        var processedPairs = new HashSet<(ITypeSymbol, ITypeSymbol)>(new TypePairComparer());
        while (discoveryQueue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var (sourceType, targetType, methodName, isUserDeclared) = discoveryQueue.Dequeue();
            if (!processedPairs.Add((sourceType, targetType)))
                continue;

            var partialSymbol = partialMethodSymbols.FirstOrDefault(m =>
                m.Name == methodName && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, sourceType)
            );

            // If it's a creation mapping (not update), check return type too
            if (
                partialSymbol != null
                && partialSymbol.Parameters.Length == 1
                && !SymbolEqualityComparer.Default.Equals(partialSymbol.ReturnType, targetType)
            )
            {
                partialSymbol = null;
            }

            bool isUpdateMapping =
                partialSymbol != null && partialSymbol.ReturnsVoid && partialSymbol.Parameters.Length == 2;
            var sourceName = partialSymbol?.Parameters[0].Name ?? "src";

            var (customMappings, ignoredProps, _, shouldReverse) = ConfigParser.ParseSpecificConfiguration(
                configureMethodNodes,
                sourceType,
                targetType,
                model,
                diagnostics
            );

            var discoveryList = new List<(ITypeSymbol, ITypeSymbol)>();
            var mapping = MethodMappingFactory.CreateMethodMapping(
                partialSymbol,
                sourceType,
                targetType,
                sourceName,
                customMappings,
                ignoredProps,
                partialMethodSymbols,
                enumMappingsToGenerate,
                injectedRenames,
                globalConverters,
                isUpdateMapping,
                partialSymbol?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().GetLocation(),
                discoveryList,
                nameMap,
                model,
                isUserDeclared,
                diagnostics
            );

            // MAPO005: No accessible properties
            if (
                isUserDeclared
                && !mapping.IsEnumMapping
                && !mapping.IsCollectionMapping
                && mapping.PropertyMappings.Count == 0
                && mapping.ConstructorArgs.Count == 0
                && mapping.UnmappedProperties.Count == 0
                && customMappings.Count == 0
            )
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.NoAccessibleProperties,
                        partialSymbol?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().GetLocation()
                            ?? Location.None,
                        sourceType.ToDisplayString(),
                        targetType.ToDisplayString()
                    )
                );
            }

            if (shouldReverse && !isUpdateMapping)
            {
                var reversePair = (targetType, sourceType);
                if (queuedPairs.Add(reversePair))
                {
                    var reverseName = nameMap.TryGetValue(reversePair, out var rn)
                        ? rn
                        : (
                            "Map"
                            + TypeHelpers.CleanGenericName(targetType)
                            + "To"
                            + TypeHelpers.CleanGenericName(sourceType)
                        );
                    nameMap[reversePair] = reverseName;
                    discoveryQueue.Enqueue((targetType, sourceType, reverseName, false));
                }
            }

            var currentPair = (sourceType, targetType);
            foreach (var discovered in discoveryList)
            {
                if (queuedPairs.Add(discovered))
                {
                    var autoName = nameMap.TryGetValue(discovered, out var n)
                        ? n
                        : (
                            "Map"
                            + TypeHelpers.CleanGenericName(discovered.Item1)
                            + "To"
                            + TypeHelpers.CleanGenericName(discovered.Item2)
                        );
                    nameMap[discovered] = autoName;
                    parentMap[discovered] = currentPair;
                    discoveryQueue.Enqueue((discovered.Item1, discovered.Item2, autoName, false));
                }
                else if (
                    !useReferenceTracking
                    && processedPairs.Contains(discovered)
                    && !mapping.IsCollectionMapping
                    && !TypeHelpers.IsCollection(discovered.Item1)
                    && !TypeHelpers.IsCollection(discovered.Item2)
                )
                {
                    // Check for actual cycle by walking the parent chain
                    var comparer = new TypePairComparer();
                    bool isCycle = false;
                    var ancestor = currentPair;
                    var visited = new HashSet<(ITypeSymbol, ITypeSymbol)>(comparer);
                    while (true)
                    {
                        if (!visited.Add(ancestor))
                            break; // prevent infinite loop in parent chain
                        if (comparer.Equals(ancestor, discovered))
                        {
                            isCycle = true;
                            break;
                        }
                        if (!parentMap.TryGetValue(ancestor, out var parent) || parent == null)
                            break;
                        ancestor = parent.Value;
                    }

                    if (isCycle)
                    {
                        diagnostics.Add(
                            Diagnostic.Create(
                                DiagnosticDescriptors.CircularReferenceWithoutTracking,
                                partialSymbol?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().GetLocation()
                                    ?? Location.None,
                                sourceType.ToDisplayString(),
                                targetType.ToDisplayString()
                            )
                        );
                    }
                }
            }

            resultMappings.Add(mapping);
        }

        foreach (var entry in enumMappingsToGenerate)
        {
            var sourceFields = entry
                .Value.Source.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(f => f.HasConstantValue)
                .ToList();
            var targetFields = entry
                .Value.Target.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(f => f.HasConstantValue)
                .ToList();

            var cases = new Dictionary<string, string>();
            foreach (var sField in sourceFields)
            {
                var tMatch = targetFields.FirstOrDefault(t =>
                    t.Name.Equals(sField.Name, System.StringComparison.OrdinalIgnoreCase)
                );
                if (tMatch != null)
                {
                    cases[$"{entry.Value.Source.ToDisplayString()}.{sField.Name}"] =
                        $"{entry.Value.Target.ToDisplayString()}.{tMatch.Name}";
                }
                else if (strictMode)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.UnmatchedEnumMember,
                            Location.None,
                            entry.Value.Source.ToDisplayString(),
                            sField.Name,
                            entry.Value.Target.ToDisplayString()
                        )
                    );
                }
            }

            resultMappings.Add(
                new MethodMapping(
                    methodName: entry.Key,
                    sourceTypeDisplayString: entry.Value.Source.ToDisplayString(),
                    targetTypeDisplayString: entry.Value.Target.ToDisplayString(),
                    targetTypeName: entry.Value.Target.Name,
                    targetIsAbstract: false,
                    sourceName: "value",
                    parameters: new List<string> { $"{entry.Value.Source.ToDisplayString()} value" },
                    constructorArgs: new List<ConstructorArg>(),
                    propertyMappings: new List<PropertyMapping>(),
                    unmappedProperties: new List<string>(),
                    isUserDeclared: false,
                    isEnumMapping: true,
                    enumCases: cases
                )
            );
        }

        // Collect user using directives from the source file for generated code
        var userUsings = classDeclaration
            .SyntaxTree.GetRoot(ct)
            .DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Where(u => u.Name != null)
            .Select(u => u.Name!.ToString())
            .Where(ns =>
                !ns.StartsWith("System")
                && ns != "Mapo.Attributes"
                && ns != classSymbol.ContainingNamespace.ToDisplayString()
            )
            .Distinct()
            .ToList();

        var mapper = new MapperInfo(
            classSymbol.ContainingNamespace.ToDisplayString(),
            classSymbol.Name,
            isStatic,
            strictMode,
            useReferenceTracking,
            resultMappings,
            injectedMembers,
            globalConverters,
            userUsings
        );
        return new ParseResult(mapper, diagnostics);
    }

    private class TypePairComparer : IEqualityComparer<(ITypeSymbol Source, ITypeSymbol Target)>
    {
        public bool Equals((ITypeSymbol Source, ITypeSymbol Target) x, (ITypeSymbol Source, ITypeSymbol Target) y)
        {
            return SymbolEqualityComparer.Default.Equals(x.Source, y.Source)
                && SymbolEqualityComparer.Default.Equals(x.Target, y.Target);
        }

        public int GetHashCode((ITypeSymbol Source, ITypeSymbol Target) obj)
        {
            int hash = 17;
            hash = hash * 31 + SymbolEqualityComparer.Default.GetHashCode(obj.Source);
            hash = hash * 31 + SymbolEqualityComparer.Default.GetHashCode(obj.Target);
            return hash;
        }
    }
}
