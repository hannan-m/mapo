using FluentAssertions;
using Mapo.Generator.Emit;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for Step 16 fixes: GeneratedCode attribute, CodeWriter guard,
/// circular reference diagnostics, unmatched enum member diagnostics.
/// </summary>
public class Step16Tests : MapoVerifier
{
    // ─── #30: [GeneratedCode] attribute on emitted types ───

    [Fact]
    public void GeneratedCode_HasGeneratedCodeAttribute()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("[GeneratedCode(\"Mapo.Generator\", \"1.0.0\")]");
    }

    [Fact]
    public void GeneratedCode_AttributeDoesNotBreakCompilation()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }";
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void GeneratedCode_ExtensionClassHasAttribute()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        // Both the mapper class and extension class should have the attribute
        generated.Split("[GeneratedCode(").Length.Should().BeGreaterThanOrEqualTo(3,
            "both mapper class and extension class should have [GeneratedCode]");
    }

    // ─── #33: CodeWriter double-call guard ───

    [Fact]
    public void CodeWriter_SecondToString_Throws()
    {
        var cw = new CodeWriter();
        cw.AppendLine("// test");
        cw.ToString(); // first call OK
        var act = () => cw.ToString(); // second call should throw
        act.Should().Throw<System.InvalidOperationException>()
            .WithMessage("*already been called*");
    }

    // ─── #25: Circular reference detection ───

    [Fact]
    public void CircularReference_WithoutTracking_EmitsDiagnostic()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class Parent { public int Id { get; set; } public Child Child { get; set; } }
public class Child { public int Id { get; set; } public Parent Parent { get; set; } }
public class ParentDto { public int Id { get; set; } public ChildDto Child { get; set; } }
public class ChildDto { public int Id { get; set; } public ParentDto Parent { get; set; } }
[Mapper] public static partial class M { public static partial ParentDto Map(Parent p); }";
        var result = RunGenerator(source);
        var diagnostics = result.Diagnostics;
        diagnostics.Should().Contain(d => d.Id == "MAPO010",
            "circular reference without UseReferenceTracking should emit MAPO010");
    }

    [Fact]
    public void CircularReference_WithTracking_NoDiagnostic()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class Parent { public int Id { get; set; } public Child Child { get; set; } }
public class Child { public int Id { get; set; } public Parent Parent { get; set; } }
public class ParentDto { public int Id { get; set; } public ChildDto Child { get; set; } }
public class ChildDto { public int Id { get; set; } public ParentDto Parent { get; set; } }
[Mapper(UseReferenceTracking = true)] public static partial class M { public static partial ParentDto Map(Parent p); }";
        var result = RunGenerator(source);
        var diagnostics = result.Diagnostics;
        diagnostics.Should().NotContain(d => d.Id == "MAPO010",
            "circular reference with UseReferenceTracking should not emit MAPO010");
    }

    // ─── #27: Unmatched enum members in StrictMode ───

    [Fact]
    public void UnmatchedEnum_StrictMode_EmitsDiagnostic()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public enum SourceColor { Red, Green, Blue, Purple }
public enum TargetColor { Red, Green, Blue }
public class S { public SourceColor Color { get; set; } }
public class T { public TargetColor Color { get; set; } }
[Mapper(StrictMode = true)] public static partial class M { public static partial T Map(S s); }";
        var result = RunGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "MAPO011",
            "unmatched enum member 'Purple' should emit MAPO011 in strict mode");
    }

    [Fact]
    public void UnmatchedEnum_NonStrictMode_NoDiagnostic()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public enum SourceColor { Red, Green, Blue, Purple }
public enum TargetColor { Red, Green, Blue }
public class S { public SourceColor Color { get; set; } }
public class T { public TargetColor Color { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }";
        var result = RunGenerator(source);
        result.Diagnostics.Should().NotContain(d => d.Id == "MAPO011",
            "unmatched enum members should not emit MAPO011 in non-strict mode");
    }

    [Fact]
    public void MatchedEnum_StrictMode_NoDiagnostic()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public enum SourceColor { Red, Green, Blue }
public enum TargetColor { Red, Green, Blue }
public class S { public SourceColor Color { get; set; } }
public class T { public TargetColor Color { get; set; } }
[Mapper(StrictMode = true)] public static partial class M { public static partial T Map(S s); }";
        var result = RunGenerator(source);
        result.Diagnostics.Should().NotContain(d => d.Id == "MAPO011",
            "fully matched enums should not emit MAPO011 even in strict mode");
    }
}
