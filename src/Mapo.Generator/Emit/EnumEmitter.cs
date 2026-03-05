using Mapo.Generator.Models;

namespace Mapo.Generator.Emit;

internal static class EnumEmitter
{
    public static void Emit(CodeWriter cw, MethodMapping mapping, bool isStatic, string paramsList)
    {
        cw.AppendLine(
            $"internal {(isStatic ? "static " : "")}{mapping.TargetTypeDisplayString} {mapping.MethodName}({paramsList})"
        );
        using (cw.Block())
        {
            cw.AppendLine($"return {mapping.SourceName} switch");
            cw.AppendLine("{");
            cw.Indent();
            if (mapping.EnumCases != null)
            {
                foreach (var @case in mapping.EnumCases)
                {
                    cw.AppendLine($"{@case.Key} => {@case.Value},");
                }
            }
            cw.AppendLine($"_ => default({mapping.TargetTypeDisplayString})");
            cw.Dedent();
            cw.AppendLine("};");
        }
    }
}
