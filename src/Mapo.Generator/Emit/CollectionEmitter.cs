using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mapo.Generator.Models;

namespace Mapo.Generator.Emit;

internal static class CollectionEmitter
{
    public static void EmitLoopBlock(
        CodeWriter cw,
        string varName,
        CollectionLoopInfo loop,
        MapperInfo mapper,
        List<(string MethodName, Regex GroupPattern, Regex CallPattern)> methodPatterns
    )
    {
        var memberPrefix = mapper.IsStatic ? "" : "this.";
        var internalName = mapper.UseReferenceTracking ? loop.ItemMapperName + "Internal" : loop.ItemMapperName;
        var itemExpr = loop.ProjectionBody ?? "_item";
        string mapCall;

        if (mapper.UseReferenceTracking)
            mapCall = $"{memberPrefix}{internalName}({itemExpr}, _context)";
        else
            mapCall = $"{memberPrefix}{internalName}({itemExpr})";

        cw.AppendLine(
            $"var {varName} = new List<{loop.TargetItemTypeDisplay}>({loop.SourceCollectionExpr}.{loop.CountMember});"
        );
        cw.AppendLine($"for (var _i = 0; _i < {loop.SourceCollectionExpr}.{loop.CountMember}; _i++)");
        using (cw.Block())
        {
            cw.AppendLine($"var _item = {loop.SourceCollectionExpr}[_i];");
            cw.AppendLine($"{varName}.Add({mapCall});");
        }
    }

    public static void Emit(
        CodeWriter cw,
        MethodMapping mapping,
        MapperInfo mapper,
        List<(string MethodName, Regex GroupPattern, Regex CallPattern)> methodPatterns
    )
    {
        var sItem = mapping.SourceItemTypeDisplayString!;
        var tItem = mapping.TargetItemTypeDisplayString!;
        var paramsList = string.Join(", ", mapping.Parameters);
        var internalParams = mapper.UseReferenceTracking ? paramsList + ", MappingContext _context" : paramsList;

        var itemMapperName = mapper
            .Mappings.FirstOrDefault(m => m.SourceTypeDisplayString == sItem && m.TargetTypeDisplayString == tItem)
            ?.MethodName;

        if (itemMapperName == null)
        {
            itemMapperName = "Map" + TypeHelpers.CleanGenericName(sItem) + "To" + TypeHelpers.CleanGenericName(tItem);
        }

        string partialKeyword = mapping.IsUserDeclared ? "partial " : "";
        string accessibility = mapping.IsUserDeclared ? "public " : "internal ";

        cw.AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]");
        cw.AppendLine(
            $"{accessibility}{(mapper.IsStatic ? "static " : "")}{partialKeyword}{mapping.TargetTypeDisplayString} {mapping.MethodName}({paramsList})"
        );
        using (cw.Block())
        {
            if (mapper.UseReferenceTracking)
            {
                cw.AppendLine(
                    $"return {mapping.MethodName}Internal({string.Join(", ", mapping.Parameters.Select(p => p.Split(' ').Last()))}, new MappingContext());"
                );
            }
            else
            {
                cw.AppendLine(
                    $"return {mapping.MethodName}Internal({string.Join(", ", mapping.Parameters.Select(p => p.Split(' ').Last()))});"
                );
            }
        }

        cw.AppendLine(
            $"private {(mapper.IsStatic ? "static " : "")}{mapping.TargetTypeDisplayString} {mapping.MethodName}Internal({internalParams})"
        );
        using (cw.Block())
        {
            EmitInternalBody(cw, mapping, mapper, sItem, tItem, itemMapperName);
        }
    }

    private static void EmitInternalBody(
        CodeWriter cw,
        MethodMapping mapping,
        MapperInfo mapper,
        string sItem,
        string tItem,
        string itemMapperName
    )
    {
        var srcType = mapping.SourceTypeDisplayString;
        bool isNullableSource = srcType.EndsWith("?");

        if (isNullableSource)
        {
            cw.AppendLine($"if ({mapping.SourceName} == null) return new List<{tItem}>();");
        }
        else
        {
            cw.AppendLine(
                $"if ({mapping.SourceName} == null) throw new ArgumentNullException(nameof({mapping.SourceName}));"
            );
        }

        var memberPrefix = mapper.IsStatic ? "" : "this.";
        var callArgs = mapper.UseReferenceTracking ? "item, _context" : "item";
        var callMethod = mapper.UseReferenceTracking ? itemMapperName + "Internal" : itemMapperName;

        if (
            srcType.StartsWith("System.Collections.Generic.List<")
            || srcType.StartsWith("List<")
            || srcType.StartsWith("global::System.Collections.Generic.List<")
        )
        {
            cw.AppendLine($"var list = new List<{tItem}>({mapping.SourceName}.Count);");
            cw.AppendLine($"for (int i = 0; i < {mapping.SourceName}.Count; i++)");
            using (cw.Block())
            {
                cw.AppendLine($"var item = {mapping.SourceName}[i];");
                cw.AppendLine($"list.Add({memberPrefix}{callMethod}({callArgs}));");
            }
            cw.AppendLine("return list;");
        }
        else if (srcType.EndsWith("[]"))
        {
            cw.AppendLine($"var list = new List<{tItem}>({mapping.SourceName}.Length);");
            cw.AppendLine($"for (int i = 0; i < {mapping.SourceName}.Length; i++)");
            using (cw.Block())
            {
                cw.AppendLine($"var item = {mapping.SourceName}[i];");
                cw.AppendLine($"list.Add({memberPrefix}{callMethod}({callArgs}));");
            }
            cw.AppendLine("return list;");
        }
        else
        {
            cw.AppendLine($"if ({mapping.SourceName} is {sItem}[] array)");
            using (cw.Block())
            {
                cw.AppendLine($"var list = new List<{tItem}>(array.Length);");
                cw.AppendLine("for (int i = 0; i < array.Length; i++)");
                using (cw.Block())
                {
                    cw.AppendLine("var item = array[i];");
                    cw.AppendLine($"list.Add({memberPrefix}{callMethod}({callArgs}));");
                }
                cw.AppendLine("return list;");
            }

            cw.AppendLine($"if ({mapping.SourceName} is List<{sItem}> sourceList)");
            using (cw.Block())
            {
                cw.AppendLine($"var list = new List<{tItem}>(sourceList.Count);");
                cw.AppendLine("for (int i = 0; i < sourceList.Count; i++)");
                using (cw.Block())
                {
                    cw.AppendLine("var item = sourceList[i];");
                    cw.AppendLine($"list.Add({memberPrefix}{callMethod}({callArgs}));");
                }
                cw.AppendLine("return list;");
            }

            cw.AppendLine($"var fallbackList = new List<{tItem}>();");
            cw.AppendLine($"foreach (var item in {mapping.SourceName})");
            using (cw.Block())
            {
                cw.AppendLine($"fallbackList.Add({memberPrefix}{callMethod}({callArgs}));");
            }
            cw.AppendLine("return fallbackList;");
        }
    }
}
