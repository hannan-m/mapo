using System;

namespace Mapo.Generator.Models;

public class DerivedMappingInfo : IEquatable<DerivedMappingInfo>
{
    public string SourceTypeDisplayString { get; }
    public string TargetTypeDisplayString { get; }

    public DerivedMappingInfo(string sourceTypeDisplayString, string targetTypeDisplayString)
    {
        SourceTypeDisplayString = sourceTypeDisplayString;
        TargetTypeDisplayString = targetTypeDisplayString;
    }

    public bool Equals(DerivedMappingInfo other)
    {
        if (other is null) return false;
        return SourceTypeDisplayString == other.SourceTypeDisplayString
            && TargetTypeDisplayString == other.TargetTypeDisplayString;
    }

    public override bool Equals(object obj) => Equals(obj as DerivedMappingInfo);

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 31 + (SourceTypeDisplayString?.GetHashCode() ?? 0);
        hash = hash * 31 + (TargetTypeDisplayString?.GetHashCode() ?? 0);
        return hash;
    }
}
