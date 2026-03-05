using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Mapo.Generator;

internal static class TypeHelpers
{
    public static IEnumerable<IPropertySymbol> GetAllProperties(ITypeSymbol type)
    {
        var seen = new HashSet<string>();
        var current = type;
        while (current != null)
        {
            foreach (var prop in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (seen.Add(prop.Name))
                    yield return prop;
            }
            current = current.BaseType;
        }
    }

    public static bool IsCollection(ITypeSymbol t) =>
        t is IArrayTypeSymbol
        || (
            t is INamedTypeSymbol named
            && named.IsGenericType
            && (named.Name == "IEnumerable" || named.AllInterfaces.Any(i => i.Name == "IEnumerable" && i.IsGenericType))
        );

    public static ITypeSymbol? GetItemType(ITypeSymbol t) =>
        t is IArrayTypeSymbol a ? a.ElementType : (t as INamedTypeSymbol)?.TypeArguments.FirstOrDefault();

    public static bool IsMappable(ITypeSymbol t) =>
        t.TypeKind == TypeKind.Class || t.TypeKind == TypeKind.Struct || t.TypeKind == TypeKind.Interface;

    public static string CleanGenericName(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
            return CleanGenericName(array.ElementType) + "Array";
        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            var result = named.Name;
            foreach (var arg in named.TypeArguments)
                result += CleanGenericName(arg);
            return result;
        }
        return type.Name;
    }

    public static string CleanGenericName(string name)
    {
        return name.Replace(".", "").Replace("<", "").Replace(">", "").Replace(",", "").Replace(" ", "");
    }
}
