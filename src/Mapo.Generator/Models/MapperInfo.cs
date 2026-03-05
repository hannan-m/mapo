using System;
using System.Collections.Generic;

namespace Mapo.Generator.Models;

public class GlobalConverter : IEquatable<GlobalConverter>
{
    public string SourceTypeDisplayString { get; }
    public string TargetTypeDisplayString { get; }
    public bool TargetIsString { get; }
    public string ParamName { get; }
    public string Expression { get; }

    public GlobalConverter(
        string sourceTypeDisplayString,
        string targetTypeDisplayString,
        bool targetIsString,
        string paramName,
        string expression
    )
    {
        SourceTypeDisplayString = sourceTypeDisplayString;
        TargetTypeDisplayString = targetTypeDisplayString;
        TargetIsString = targetIsString;
        ParamName = paramName;
        Expression = expression;
    }

    public bool Equals(GlobalConverter other)
    {
        if (other is null)
            return false;
        return SourceTypeDisplayString == other.SourceTypeDisplayString
            && TargetTypeDisplayString == other.TargetTypeDisplayString
            && TargetIsString == other.TargetIsString
            && ParamName == other.ParamName
            && Expression == other.Expression;
    }

    public override bool Equals(object obj) => Equals(obj as GlobalConverter);

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 31 + (SourceTypeDisplayString?.GetHashCode() ?? 0);
        hash = hash * 31 + (TargetTypeDisplayString?.GetHashCode() ?? 0);
        hash = hash * 31 + TargetIsString.GetHashCode();
        hash = hash * 31 + (ParamName?.GetHashCode() ?? 0);
        hash = hash * 31 + (Expression?.GetHashCode() ?? 0);
        return hash;
    }
}

public class InjectedMember : IEquatable<InjectedMember>
{
    public string Type { get; }
    public string Name { get; }

    public InjectedMember(string type, string name)
    {
        Type = type;
        Name = name;
    }

    public bool Equals(InjectedMember other)
    {
        if (other is null)
            return false;
        return Type == other.Type && Name == other.Name;
    }

    public override bool Equals(object obj) => Equals(obj as InjectedMember);

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 31 + (Type?.GetHashCode() ?? 0);
        hash = hash * 31 + (Name?.GetHashCode() ?? 0);
        return hash;
    }
}

public class MapperInfo : IEquatable<MapperInfo>
{
    public string Namespace { get; }
    public string ClassName { get; }
    public bool IsStatic { get; }
    public bool StrictMode { get; }
    public bool UseReferenceTracking { get; }
    public List<MethodMapping> Mappings { get; }
    public List<InjectedMember> InjectedMembers { get; }
    public List<GlobalConverter> GlobalConverters { get; }

    public MapperInfo(
        string @namespace,
        string className,
        bool isStatic,
        bool strictMode,
        bool useReferenceTracking,
        List<MethodMapping> mappings,
        List<InjectedMember> injectedMembers,
        List<GlobalConverter> globalConverters
    )
    {
        Namespace = @namespace;
        ClassName = className;
        IsStatic = isStatic;
        StrictMode = strictMode;
        UseReferenceTracking = useReferenceTracking;
        Mappings = mappings;
        InjectedMembers = injectedMembers;
        GlobalConverters = globalConverters;
    }

    public bool Equals(MapperInfo other)
    {
        if (other is null)
            return false;
        return Namespace == other.Namespace
            && ClassName == other.ClassName
            && IsStatic == other.IsStatic
            && StrictMode == other.StrictMode
            && UseReferenceTracking == other.UseReferenceTracking
            && ListEquals(InjectedMembers, other.InjectedMembers)
            && ListEquals(GlobalConverters, other.GlobalConverters)
            && MappingListEquals(Mappings, other.Mappings);
    }

    public override bool Equals(object obj) => Equals(obj as MapperInfo);

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 31 + (Namespace?.GetHashCode() ?? 0);
        hash = hash * 31 + (ClassName?.GetHashCode() ?? 0);
        hash = hash * 31 + IsStatic.GetHashCode();
        hash = hash * 31 + StrictMode.GetHashCode();
        hash = hash * 31 + UseReferenceTracking.GetHashCode();
        hash = hash * 31 + ListHash(Mappings);
        hash = hash * 31 + ListHash(InjectedMembers);
        hash = hash * 31 + ListHash(GlobalConverters);
        return hash;
    }

    private static int ListHash<T>(List<T> list)
        where T : IEquatable<T>
    {
        if (list is null)
            return 0;
        int hash = 17;
        for (int i = 0; i < list.Count; i++)
            hash = hash * 31 + (list[i]?.GetHashCode() ?? 0);
        return hash;
    }

    private static bool ListEquals<T>(List<T> a, List<T> b)
        where T : IEquatable<T>
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

    private static bool MappingListEquals(List<MethodMapping> a, List<MethodMapping> b)
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
}
