using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Mapo.Generator.Models;

public class ParseResult : IEquatable<ParseResult>
{
    public MapperInfo? Mapper { get; }
    public List<Diagnostic> Diagnostics { get; }

    public ParseResult(MapperInfo? mapper, List<Diagnostic> diagnostics)
    {
        Mapper = mapper;
        Diagnostics = diagnostics ?? new List<Diagnostic>();
    }

    public bool Equals(ParseResult other)
    {
        if (other is null)
            return false;
        if (Mapper is null != (other.Mapper is null))
            return false;
        if (Mapper is not null && !Mapper.Equals(other.Mapper))
            return false;
        return DiagnosticsEqual(Diagnostics, other.Diagnostics);
    }

    public override bool Equals(object obj) => Equals(obj as ParseResult);

    public override int GetHashCode()
    {
        int hash = Mapper?.GetHashCode() ?? 0;
        hash = hash * 31 + Diagnostics.Count;
        foreach (var d in Diagnostics)
        {
            hash = hash * 31 + (d.Id?.GetHashCode() ?? 0);
            hash = hash * 31 + d.GetMessage().GetHashCode();
            hash = hash * 31 + (int)d.Severity;
        }
        return hash;
    }

    private static bool DiagnosticsEqual(List<Diagnostic> a, List<Diagnostic> b)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Id != b[i].Id)
                return false;
            if (a[i].GetMessage() != b[i].GetMessage())
                return false;
            if (a[i].Severity != b[i].Severity)
                return false;
        }
        return true;
    }
}
