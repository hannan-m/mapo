using System;
using System.Collections.Generic;

namespace Mapo.Generator.Models;

public class ConstructorArg : IEquatable<ConstructorArg>
{
    public string Expression { get; }
    public CollectionLoopInfo? CollectionLoop { get; }
    public string MappingOrigin { get; }

    public ConstructorArg(string expression, CollectionLoopInfo? collectionLoop = null, string mappingOrigin = "Direct")
    {
        Expression = expression;
        CollectionLoop = collectionLoop;
        MappingOrigin = mappingOrigin ?? "Direct";
    }

    public bool Equals(ConstructorArg other)
    {
        if (other is null)
            return false;
        return Expression == other.Expression
            && MappingOrigin == other.MappingOrigin
            && Equals(CollectionLoop, other.CollectionLoop);
    }

    public override bool Equals(object obj) => Equals(obj as ConstructorArg);

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 31 + (Expression?.GetHashCode() ?? 0);
        hash = hash * 31 + (CollectionLoop?.GetHashCode() ?? 0);
        hash = hash * 31 + (MappingOrigin?.GetHashCode() ?? 0);
        return hash;
    }
}

public class MethodMapping : IEquatable<MethodMapping>
{
    public string MethodName { get; }
    public string SourceTypeDisplayString { get; }
    public string TargetTypeDisplayString { get; }
    public string TargetTypeName { get; }
    public bool TargetIsAbstract { get; }
    public string SourceName { get; }
    public List<string> Parameters { get; }
    public List<ConstructorArg> ConstructorArgs { get; }
    public List<PropertyMapping> PropertyMappings { get; }
    public List<string> UnmappedProperties { get; }
    public bool IsUserDeclared { get; }
    public bool IsEnumMapping { get; }
    public Dictionary<string, string>? EnumCases { get; }
    public bool IsUpdateMapping { get; }
    public List<DerivedMappingInfo> DerivedMappings { get; }
    public bool GenerateProjection { get; }
    public bool IsCollectionMapping { get; }
    public string? SourceItemTypeDisplayString { get; }
    public string? TargetItemTypeDisplayString { get; }

    public MethodMapping(
        string methodName,
        string sourceTypeDisplayString,
        string targetTypeDisplayString,
        string targetTypeName,
        bool targetIsAbstract,
        string sourceName,
        List<string> parameters,
        List<ConstructorArg> constructorArgs,
        List<PropertyMapping> propertyMappings,
        List<string> unmappedProperties,
        bool isUserDeclared,
        bool isEnumMapping = false,
        Dictionary<string, string>? enumCases = null,
        bool generateProjection = true,
        bool isUpdateMapping = false,
        List<DerivedMappingInfo>? derivedMappings = null,
        bool isCollectionMapping = false,
        string? sourceItemTypeDisplayString = null,
        string? targetItemTypeDisplayString = null
    )
    {
        MethodName = methodName;
        SourceTypeDisplayString = sourceTypeDisplayString;
        TargetTypeDisplayString = targetTypeDisplayString;
        TargetTypeName = targetTypeName;
        TargetIsAbstract = targetIsAbstract;
        SourceName = sourceName;
        Parameters = parameters;
        ConstructorArgs = constructorArgs;
        PropertyMappings = propertyMappings;
        UnmappedProperties = unmappedProperties;
        IsUserDeclared = isUserDeclared;
        IsEnumMapping = isEnumMapping;
        EnumCases = enumCases;
        GenerateProjection = generateProjection;
        IsUpdateMapping = isUpdateMapping;
        DerivedMappings = derivedMappings ?? new List<DerivedMappingInfo>();
        IsCollectionMapping = isCollectionMapping;
        SourceItemTypeDisplayString = sourceItemTypeDisplayString;
        TargetItemTypeDisplayString = targetItemTypeDisplayString;
    }

    public bool Equals(MethodMapping other)
    {
        if (other is null)
            return false;
        return MethodName == other.MethodName
            && SourceTypeDisplayString == other.SourceTypeDisplayString
            && TargetTypeDisplayString == other.TargetTypeDisplayString
            && TargetTypeName == other.TargetTypeName
            && TargetIsAbstract == other.TargetIsAbstract
            && SourceName == other.SourceName
            && IsUserDeclared == other.IsUserDeclared
            && IsEnumMapping == other.IsEnumMapping
            && IsUpdateMapping == other.IsUpdateMapping
            && GenerateProjection == other.GenerateProjection
            && IsCollectionMapping == other.IsCollectionMapping
            && SourceItemTypeDisplayString == other.SourceItemTypeDisplayString
            && TargetItemTypeDisplayString == other.TargetItemTypeDisplayString
            && ListEquals(Parameters, other.Parameters)
            && CtorArgListEquals(ConstructorArgs, other.ConstructorArgs)
            && ListEquals(UnmappedProperties, other.UnmappedProperties)
            && PropListEquals(PropertyMappings, other.PropertyMappings)
            && DerivedListEquals(DerivedMappings, other.DerivedMappings)
            && DictEquals(EnumCases, other.EnumCases);
    }

    public override bool Equals(object obj) => Equals(obj as MethodMapping);

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 31 + (MethodName?.GetHashCode() ?? 0);
        hash = hash * 31 + (SourceTypeDisplayString?.GetHashCode() ?? 0);
        hash = hash * 31 + (TargetTypeDisplayString?.GetHashCode() ?? 0);
        hash = hash * 31 + (TargetTypeName?.GetHashCode() ?? 0);
        hash = hash * 31 + TargetIsAbstract.GetHashCode();
        hash = hash * 31 + (SourceName?.GetHashCode() ?? 0);
        hash = hash * 31 + IsUserDeclared.GetHashCode();
        hash = hash * 31 + IsEnumMapping.GetHashCode();
        hash = hash * 31 + IsUpdateMapping.GetHashCode();
        hash = hash * 31 + GenerateProjection.GetHashCode();
        hash = hash * 31 + IsCollectionMapping.GetHashCode();
        hash = hash * 31 + (SourceItemTypeDisplayString?.GetHashCode() ?? 0);
        hash = hash * 31 + (TargetItemTypeDisplayString?.GetHashCode() ?? 0);
        return hash;
    }

    private static bool CtorArgListEquals(List<ConstructorArg> a, List<ConstructorArg> b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
            if (!a[i].Equals(b[i]))
                return false;
        return true;
    }

    private static bool ListEquals(List<string> a, List<string> b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i])
                return false;
        return true;
    }

    private static bool PropListEquals(List<PropertyMapping> a, List<PropertyMapping> b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
            if (!a[i].Equals(b[i]))
                return false;
        return true;
    }

    private static bool DerivedListEquals(List<DerivedMappingInfo> a, List<DerivedMappingInfo> b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
            if (!a[i].Equals(b[i]))
                return false;
        return true;
    }

    private static bool DictEquals(Dictionary<string, string> a, Dictionary<string, string> b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        if (a.Count != b.Count)
            return false;
        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var val) || val != kvp.Value)
                return false;
        }
        return true;
    }
}
