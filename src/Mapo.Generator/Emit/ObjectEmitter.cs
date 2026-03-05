using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mapo.Generator.Models;

namespace Mapo.Generator.Emit;

internal static class ObjectEmitter
{
    public static void Emit(
        CodeWriter cw,
        MethodMapping mapping,
        MapperInfo mapper,
        List<(string MethodName, Regex GroupPattern, Regex CallPattern)> methodPatterns
    )
    {
        var paramsList = string.Join(", ", mapping.Parameters);
        var internalParams = mapper.UseReferenceTracking ? paramsList + ", MappingContext _context" : paramsList;

        string partialKeyword = mapping.IsUserDeclared ? "partial " : "";
        string accessibility = mapping.IsUserDeclared ? "public " : "internal ";

        cw.AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]");
        cw.AppendLine(
            $"{accessibility}{(mapper.IsStatic ? "static " : "")}{partialKeyword}{(mapping.IsUpdateMapping ? "void" : mapping.TargetTypeDisplayString)} {mapping.MethodName}({paramsList})"
        );

        using (cw.Block())
        {
            if (mapper.UseReferenceTracking)
            {
                cw.AppendLine(
                    $"{(mapping.IsUpdateMapping ? "" : "return ")}{mapping.MethodName}Internal({string.Join(", ", mapping.Parameters.Select(p => p.Split(' ').Last()))}, new MappingContext());"
                );
            }
            else
            {
                cw.AppendLine(
                    $"{(mapping.IsUpdateMapping ? "" : "return ")}{mapping.MethodName}Internal({string.Join(", ", mapping.Parameters.Select(p => p.Split(' ').Last()))});"
                );
            }
        }

        cw.AppendLine(
            $"private {(mapper.IsStatic ? "static " : "")}{(mapping.IsUpdateMapping ? "void" : mapping.TargetTypeDisplayString)} {mapping.MethodName}Internal({internalParams})"
        );
        using (cw.Block())
        {
            EmitInternalBody(cw, mapping, mapper, methodPatterns);
        }
    }

    private static void EmitInternalBody(
        CodeWriter cw,
        MethodMapping mapping,
        MapperInfo mapper,
        List<(string MethodName, Regex GroupPattern, Regex CallPattern)> methodPatterns
    )
    {
        if (mapping.IsUpdateMapping)
        {
            cw.AppendLine($"if ({mapping.SourceName} == null) return;");
        }
        else
        {
            cw.AppendLine(
                $"if ({mapping.SourceName} == null) throw new ArgumentNullException(nameof({mapping.SourceName}));"
            );
        }

        if (mapping.DerivedMappings.Count > 0)
        {
            cw.AppendLine($"switch ({mapping.SourceName})");
            using (cw.Block())
            {
                foreach (var derived in mapping.DerivedMappings)
                {
                    var derivedMethod = mapper
                        .Mappings.FirstOrDefault(m =>
                            m.SourceTypeDisplayString == derived.SourceTypeDisplayString
                            && m.TargetTypeDisplayString == derived.TargetTypeDisplayString
                        )
                        ?.MethodName;

                    if (derivedMethod != null)
                    {
                        var prefix = mapper.IsStatic ? "" : "this.";
                        var internalCall = derivedMethod + (mapper.UseReferenceTracking ? "Internal" : "");
                        var callArgs = mapper.UseReferenceTracking ? "d, _context" : "d";
                        cw.AppendLine($"case {derived.SourceTypeDisplayString} d:");
                        using (cw.Block())
                        {
                            cw.AppendLine($"return {prefix}{internalCall}({callArgs});");
                        }
                    }
                }
            }
        }

        if (mapping.TargetIsAbstract && !mapping.IsUpdateMapping)
        {
            cw.AppendLine("return null!;");
            return;
        }

        if (mapper.UseReferenceTracking && !mapping.IsUpdateMapping)
        {
            cw.AppendLine(
                $"if (_context.TryGet<{mapping.TargetTypeDisplayString}>({mapping.SourceName}, out var _existing)) return _existing!;"
            );
        }

        string targetName;
        if (mapping.IsUpdateMapping)
        {
            targetName = mapping.Parameters[1].Split(' ').Last();
        }
        else
        {
            targetName = "target";
            int loopCounter = 0;
            var ctorArgsList = new List<string>();
            if (mapping.ConstructorArgs.Any())
            {
                foreach (var arg in mapping.ConstructorArgs)
                {
                    if (arg.CollectionLoop != null)
                    {
                        var varName = $"_col_{loopCounter++}";
                        CollectionEmitter.EmitLoopBlock(cw, varName, arg.CollectionLoop, mapper, methodPatterns);
                        ctorArgsList.Add(varName);
                    }
                    else
                    {
                        var call = mapper.IsStatic ? arg.Expression.Replace("this.", "") : arg.Expression;
                        call = ExpressionEmitter.RedirectToInternal(call, mapper, methodPatterns);
                        ctorArgsList.Add(call);
                    }
                }
            }
            var ctorArgs = string.Join(", ", ctorArgsList);

            var initOnlyMappings = mapping.PropertyMappings.Where(pm => pm.IsInitOnly || pm.IsRequired).ToList();
            var initLoopVars = new Dictionary<string, string>();
            foreach (var pm in initOnlyMappings)
            {
                if (pm.CollectionLoop != null)
                {
                    var varName = $"_col_{loopCounter++}";
                    CollectionEmitter.EmitLoopBlock(cw, varName, pm.CollectionLoop, mapper, methodPatterns);
                    initLoopVars[pm.TargetName] = varName;
                }
            }

            if (initOnlyMappings.Count > 0)
            {
                cw.AppendLine($"var {targetName} = new {mapping.TargetTypeDisplayString}({ctorArgs})");
                cw.AppendLine("{");
                cw.Indent();
                foreach (var pm in initOnlyMappings)
                {
                    cw.AppendLine(GetMappingComment(pm));
                    if (initLoopVars.TryGetValue(pm.TargetName, out var loopVar))
                    {
                        cw.AppendLine($"{pm.TargetName} = {loopVar},");
                    }
                    else
                    {
                        var expr = ExpressionEmitter.PrepareExpression(pm, mapper, methodPatterns);
                        cw.AppendLine($"{pm.TargetName} = {expr},");
                    }
                }
                cw.Dedent();
                cw.AppendLine("};");
            }
            else
            {
                cw.AppendLine($"var {targetName} = new {mapping.TargetTypeDisplayString}({ctorArgs});");
            }

            if (mapper.UseReferenceTracking)
            {
                cw.AppendLine($"_context.Add({mapping.SourceName}, {targetName});");
            }
        }

        var regularMappings = mapping.PropertyMappings.Where(pm => !pm.IsInitOnly && !pm.IsRequired).ToList();
        if (regularMappings.Any())
        {
            foreach (var pm in regularMappings)
            {
                cw.AppendLine(GetMappingComment(pm));
                if (pm.CollectionLoop != null)
                {
                    var varName = $"_col_p_{pm.TargetName}";
                    CollectionEmitter.EmitLoopBlock(cw, varName, pm.CollectionLoop, mapper, methodPatterns);
                    cw.AppendLine($"{targetName}.{pm.TargetName} = {varName};");
                }
                else
                {
                    var expr = ExpressionEmitter.PrepareExpression(pm, mapper, methodPatterns);
                    cw.AppendLine($"{targetName}.{pm.TargetName} = {expr};");
                }
            }
        }

        if (!mapping.IsUpdateMapping)
        {
            cw.AppendLine($"return {targetName};");
        }
    }

    private static string GetMappingComment(PropertyMapping pm)
    {
        switch (pm.MappingOrigin)
        {
            case "Custom":
                return "// Custom mapping";
            case "Flattened":
                if (pm.NavigationSegments != null && pm.NavigationSegments.Count > 1)
                    return $"// Flattened: {pm.TargetName} <- {string.Join(".", pm.NavigationSegments.Skip(1))}";
                return $"// Flattened: {pm.SourceExpression}";
            case "Injected":
                return "// Injected member";
            case "Converter":
                return "// Converter applied";
            case "EnumConversion":
                return "// Enum conversion";
            case "NestedObject":
                return "// Nested object mapping";
            case "Collection":
                return "// Collection mapping";
            default:
                return $"// {pm.SourceExpression}";
        }
    }
}
