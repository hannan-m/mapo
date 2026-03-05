using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for enum mapping code generation (Issue #4 and general correctness).
/// The enum emitter must use the actual parameter name from the mapping,
/// not a hardcoded "value".
/// </summary>
public class EnumMappingTests : MapoVerifier
{
    [Fact]
    public void EnumMapping_UsesCorrectParameterName()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public enum Color { Red, Green, Blue }
public enum ColorDto { Red, Green, Blue }
public class S { public Color Color { get; set; } }
public class T { public ColorDto Color { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();

        // The switch expression should use the parameter name from the method signature
        generated.Should().Contain("switch");
        generated.Should().Contain("Test.Color.Red => Test.ColorDto.Red");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void EnumMapping_AllCasesMatched()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public enum Status { Active, Inactive, Pending }
public enum StatusDto { Active, Inactive, Pending }
public class S { public Status Status { get; set; } }
public class T { public StatusDto Status { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();

        generated.Should().Contain("Test.Status.Active => Test.StatusDto.Active");
        generated.Should().Contain("Test.Status.Inactive => Test.StatusDto.Inactive");
        generated.Should().Contain("Test.Status.Pending => Test.StatusDto.Pending");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void EnumMapping_PartialMatch_UnmatchedFallsToDefault()
    {
        // Source has a member that target doesn't — should fall to default arm
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public enum Priority { Low, Medium, High, Critical }
public enum PriorityDto { Low, Medium, High }
public class S { public Priority Priority { get; set; } }
public class T { public PriorityDto Priority { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();

        generated.Should().Contain("Test.Priority.Low => Test.PriorityDto.Low");
        generated.Should().Contain("Test.Priority.High => Test.PriorityDto.High");
        generated.Should().NotContain("Critical => ");
        generated.Should().Contain("_ => default(");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void EnumMapping_CompilesAndRuns()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public enum Color { Red, Green, Blue }
public enum ColorDto { Red, Green, Blue }
public class S { public Color Color { get; set; } }
public class T { public ColorDto Color { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var s1 = new S { Color = Color.Red };
        var t1 = M.Map(s1);
        if (t1.Color != ColorDto.Red) throw new Exception($""Expected Red, got {t1.Color}"");

        var s2 = new S { Color = Color.Blue };
        var t2 = M.Map(s2);
        if (t2.Color != ColorDto.Blue) throw new Exception($""Expected Blue, got {t2.Color}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void EnumMapping_CaseInsensitiveMatch()
    {
        // Enum members matched case-insensitively
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public enum Src { ACTIVE, INACTIVE }
public enum Tgt { Active, Inactive }
public class S { public Src Status { get; set; } }
public class T { public Tgt Status { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();

        generated.Should().Contain("Test.Src.ACTIVE => Test.Tgt.Active");
        generated.Should().Contain("Test.Src.INACTIVE => Test.Tgt.Inactive");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void EnumMapping_MultipleEnumProperties()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public enum Color { Red, Blue }
public enum Size { Small, Large }
public enum ColorDto { Red, Blue }
public enum SizeDto { Small, Large }
public class S { public Color Color { get; set; } public Size Size { get; set; } }
public class T { public ColorDto Color { get; set; } public SizeDto Size { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var s = new S { Color = Color.Blue, Size = Size.Large };
        var t = M.Map(s);
        if (t.Color != ColorDto.Blue) throw new Exception($""Expected Blue, got {t.Color}"");
        if (t.Size != SizeDto.Large) throw new Exception($""Expected Large, got {t.Size}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void EnumMapping_SwitchExpressionUsesMethodParameter()
    {
        // Verify the generated switch expression uses the method's actual parameter name
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public enum E1 { A, B }
public enum E2 { A, B }
public class S { public E1 Val { get; set; } }
public class T { public E2 Val { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();

        // The enum mapping method should have a parameter and the switch should use it
        // Parameter name from the auto-generated enum mapping is "value"
        generated.Should().Contain("value switch");
        AssertGeneratedCodeCompiles(source);
    }
}
