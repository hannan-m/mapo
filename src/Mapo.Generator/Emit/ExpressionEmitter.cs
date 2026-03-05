using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mapo.Generator.Models;

namespace Mapo.Generator.Emit;

internal static class ExpressionEmitter
{
    public static string PrepareExpression(PropertyMapping pm, MapperInfo mapper, List<(string MethodName, Regex GroupPattern, Regex CallPattern)> methodPatterns)
    {
        var expr = mapper.IsStatic ? pm.SourceExpression.Replace("this.", "") : pm.SourceExpression;
        expr = RedirectToInternal(expr, mapper, methodPatterns);

        // Structured null-guard from NavigationSegments
        if (pm.RequiresNullGuard && pm.NavigationSegments != null && pm.NavigationSegments.Count >= 2)
        {
            var segments = pm.NavigationSegments;
            // segments[0] is the source parameter (e.g., "src"), never null-guarded
            var chain = segments[0] + "." + segments[1];
            for (int i = 2; i < segments.Count; i++)
            {
                chain += "?." + segments[i];
            }

            if (pm.TargetIsValueType)
            {
                // Nullable chain returns T? for value types — coalesce to T
                expr = $"{chain} ?? default";
            }
            else
            {
                // Nullable chain returns T? for reference types — directly assignable
                expr = chain;
            }
        }

        return expr;
    }

    public static string RedirectToInternal(string expression, MapperInfo mapper, List<(string MethodName, Regex GroupPattern, Regex CallPattern)> methodPatterns)
    {
        var result = expression;
        var prefix = mapper.IsStatic ? "" : "this.";

        foreach (var (methodName, groupPattern, callPattern) in methodPatterns)
        {
            var internalName = mapper.UseReferenceTracking ? methodName + "Internal" : methodName;

            if (groupPattern.IsMatch(result))
            {
                if (mapper.UseReferenceTracking)
                    result = groupPattern.Replace(result, $"_item => {prefix}{internalName}(_item, _context)");
                else
                    result = groupPattern.Replace(result, $"{prefix}{internalName}");
            }

            int lastSearchIndex = 0;
            while (true)
            {
                var match = callPattern.Match(result, lastSearchIndex);
                if (!match.Success) break;

                int start = match.Index;
                int openParen = result.IndexOf('(', start);
                int closeParen = FindClosingParen(result, openParen);

                if (closeParen != -1)
                {
                    string args = result.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                    string replacement;
                    if (mapper.UseReferenceTracking)
                    {
                        if (string.IsNullOrEmpty(args)) replacement = $"{prefix}{internalName}(_context)";
                        else if (args.EndsWith("_context") || args.Contains(", _context")) replacement = $"{prefix}{internalName}({args})";
                        else replacement = $"{prefix}{internalName}({args}, _context)";
                    }
                    else
                    {
                        replacement = $"{prefix}{internalName}({args})";
                    }

                    result = result.Remove(start, closeParen - start + 1).Insert(start, replacement);
                    lastSearchIndex = start + replacement.Length;
                }
                else
                {
                    lastSearchIndex = start + match.Length;
                }

                if (lastSearchIndex >= result.Length) break;
            }
        }
        return result;
    }

    internal static int FindClosingParen(string s, int openParenIndex)
    {
        int count = 0;
        for (int i = openParenIndex; i < s.Length; i++)
        {
            var c = s[i];

            // Skip string literals (regular and verbatim)
            if (c == '"')
            {
                i++;
                // Check for verbatim string @"..."
                bool verbatim = i >= 2 && s[i - 2] == '@';
                while (i < s.Length)
                {
                    if (s[i] == '"')
                    {
                        if (verbatim && i + 1 < s.Length && s[i + 1] == '"')
                        {
                            i++; // skip escaped "" in verbatim
                        }
                        else
                        {
                            break;
                        }
                    }
                    else if (!verbatim && s[i] == '\\')
                    {
                        i++; // skip escaped character
                    }
                    i++;
                }
                continue;
            }

            // Skip char literals
            if (c == '\'')
            {
                i++;
                if (i < s.Length && s[i] == '\\') i++; // skip escape
                i++; // skip the char
                continue;
            }

            if (c == '(') count++;
            else if (c == ')')
            {
                count--;
                if (count == 0) return i;
            }
        }
        return -1;
    }
}
