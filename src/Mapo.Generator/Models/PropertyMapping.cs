using System;
using System.Collections.Generic;

namespace Mapo.Generator.Models;

public class CollectionLoopInfo : IEquatable<CollectionLoopInfo>
{
    public string SourceCollectionExpr { get; }
    public string? ProjectionBody { get; }
    public string ItemMapperName { get; }
    public string TargetItemTypeDisplay { get; }
    public string CountMember { get; }

    public CollectionLoopInfo(
        string sourceCollectionExpr,
        string? projectionBody,
        string itemMapperName,
        string targetItemTypeDisplay,
        string countMember
    )
    {
        SourceCollectionExpr = sourceCollectionExpr;
        ProjectionBody = projectionBody;
        ItemMapperName = itemMapperName;
        TargetItemTypeDisplay = targetItemTypeDisplay;
        CountMember = countMember;
    }

    public bool Equals(CollectionLoopInfo other)
    {
        if (other is null)
            return false;
        return SourceCollectionExpr == other.SourceCollectionExpr
            && ProjectionBody == other.ProjectionBody
            && ItemMapperName == other.ItemMapperName
            && TargetItemTypeDisplay == other.TargetItemTypeDisplay
            && CountMember == other.CountMember;
    }

    public override bool Equals(object obj) => Equals(obj as CollectionLoopInfo);

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 31 + (SourceCollectionExpr?.GetHashCode() ?? 0);
        hash = hash * 31 + (ProjectionBody?.GetHashCode() ?? 0);
        hash = hash * 31 + (ItemMapperName?.GetHashCode() ?? 0);
        hash = hash * 31 + (TargetItemTypeDisplay?.GetHashCode() ?? 0);
        hash = hash * 31 + (CountMember?.GetHashCode() ?? 0);
        return hash;
    }
}

public class PropertyMapping : IEquatable<PropertyMapping>
{
    public string TargetName { get; }
    public string SourceExpression { get; }
    public bool TargetIsValueType { get; }
    public bool TargetIsString { get; }
    public bool IsInitOnly { get; }
    public bool IsRequired { get; }
    public bool RequiresNullGuard { get; }
    public List<string>? NavigationSegments { get; }
    public CollectionLoopInfo? CollectionLoop { get; }
    public string MappingOrigin { get; }

    public PropertyMapping(
        string targetName,
        string sourceExpression,
        bool targetIsValueType = false,
        bool targetIsString = false,
        bool isInitOnly = false,
        bool isRequired = false,
        bool requiresNullGuard = false,
        List<string>? navigationSegments = null,
        CollectionLoopInfo? collectionLoop = null,
        string mappingOrigin = "Direct"
    )
    {
        TargetName = targetName;
        SourceExpression = sourceExpression;
        TargetIsValueType = targetIsValueType;
        TargetIsString = targetIsString;
        IsInitOnly = isInitOnly;
        IsRequired = isRequired;
        RequiresNullGuard = requiresNullGuard;
        NavigationSegments = navigationSegments;
        CollectionLoop = collectionLoop;
        MappingOrigin = mappingOrigin ?? "Direct";
    }

    public bool Equals(PropertyMapping other)
    {
        if (other is null)
            return false;
        return TargetName == other.TargetName
            && SourceExpression == other.SourceExpression
            && TargetIsValueType == other.TargetIsValueType
            && TargetIsString == other.TargetIsString
            && IsInitOnly == other.IsInitOnly
            && IsRequired == other.IsRequired
            && RequiresNullGuard == other.RequiresNullGuard
            && MappingOrigin == other.MappingOrigin
            && ListEquals(NavigationSegments, other.NavigationSegments)
            && Equals(CollectionLoop, other.CollectionLoop);
    }

    public override bool Equals(object obj) => Equals(obj as PropertyMapping);

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 31 + (TargetName?.GetHashCode() ?? 0);
        hash = hash * 31 + (SourceExpression?.GetHashCode() ?? 0);
        hash = hash * 31 + TargetIsValueType.GetHashCode();
        hash = hash * 31 + TargetIsString.GetHashCode();
        hash = hash * 31 + IsInitOnly.GetHashCode();
        hash = hash * 31 + IsRequired.GetHashCode();
        hash = hash * 31 + RequiresNullGuard.GetHashCode();
        hash = hash * 31 + (MappingOrigin?.GetHashCode() ?? 0);
        hash = hash * 31 + ListHash(NavigationSegments);
        hash = hash * 31 + (CollectionLoop?.GetHashCode() ?? 0);
        return hash;
    }

    private static bool ListEquals(List<string>? a, List<string>? b)
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

    private static int ListHash(List<string>? list)
    {
        if (list is null)
            return 0;
        int hash = 17;
        for (int i = 0; i < list.Count; i++)
            hash = hash * 31 + (list[i]?.GetHashCode() ?? 0);
        return hash;
    }
}
