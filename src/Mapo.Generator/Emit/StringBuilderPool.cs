using System;
using System.Text;

namespace Mapo.Generator.Emit;

internal static class StringBuilderPool
{
    [ThreadStatic]
    private static StringBuilder? _shared;

    public static StringBuilder Rent()
    {
        var sb = _shared;
        if (sb == null)
        {
            return new StringBuilder(1024);
        }

        _shared = null;
        sb.Clear();
        return sb;
    }

    public static void Return(StringBuilder sb)
    {
        if (sb.Capacity > 4096)
        {
            return;
        }

        _shared = sb;
    }

    public static string ToStringAndReturn(StringBuilder sb)
    {
        var s = sb.ToString();
        Return(sb);
        return s;
    }
}
