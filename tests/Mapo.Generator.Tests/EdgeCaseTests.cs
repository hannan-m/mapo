using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

public class EdgeCaseTests : MapoVerifier
{
    [Fact]
    public void EmptySourceObject_AllDefaults()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { }
public class T { public int Id { get; set; } public string Name { get; set; } = """"; }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        // Should compile (with MAPO001/005 warnings) and generate valid code
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void LargeObject_20Properties_GeneratesSuccessfully()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S
{
    public int P1 { get; set; } public int P2 { get; set; } public int P3 { get; set; }
    public int P4 { get; set; } public int P5 { get; set; } public int P6 { get; set; }
    public int P7 { get; set; } public int P8 { get; set; } public int P9 { get; set; }
    public int P10 { get; set; } public int P11 { get; set; } public int P12 { get; set; }
    public int P13 { get; set; } public int P14 { get; set; } public int P15 { get; set; }
    public int P16 { get; set; } public int P17 { get; set; } public int P18 { get; set; }
    public int P19 { get; set; } public int P20 { get; set; }
}
public class T
{
    public int P1 { get; set; } public int P2 { get; set; } public int P3 { get; set; }
    public int P4 { get; set; } public int P5 { get; set; } public int P6 { get; set; }
    public int P7 { get; set; } public int P8 { get; set; } public int P9 { get; set; }
    public int P10 { get; set; } public int P11 { get; set; } public int P12 { get; set; }
    public int P13 { get; set; } public int P14 { get; set; } public int P15 { get; set; }
    public int P16 { get; set; } public int P17 { get; set; } public int P18 { get; set; }
    public int P19 { get; set; } public int P20 { get; set; }
}
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var s = new S { P1 = 1, P10 = 10, P20 = 20 };
        var t = M.Map(s);
        if (t.P1 != 1 || t.P10 != 10 || t.P20 != 20)
            throw new Exception(""Large object mapping failed"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void SourceWithNoGettableProperties_ReportsMAPO005()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { }
public class T { }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        result.Diagnostics.Any(d => d.Id == "MAPO005").Should().BeTrue();
    }

    [Fact]
    public void MultipleNullableValueTypes_AllCoerced()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S
{
    public int? A { get; set; }
    public long? B { get; set; }
    public double? C { get; set; }
    public bool? D { get; set; }
}
public class T
{
    public int A { get; set; }
    public long B { get; set; }
    public double C { get; set; }
    public bool D { get; set; }
}
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var s = new S { A = 1, B = 2L, C = 3.14, D = true };
        var t = M.Map(s);
        if (t.A != 1 || t.B != 2L || t.C != 3.14 || t.D != true)
            throw new Exception(""Multiple nullable coercion failed"");

        var s2 = new S();
        var t2 = M.Map(s2);
        if (t2.A != 0 || t2.B != 0 || t2.C != 0 || t2.D != false)
            throw new Exception(""Null coercion to defaults failed"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void EnumToString_InConstructor_Works()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public enum Status { Active, Inactive }
public class S { public Status Status { get; set; } }
public record T(string Status);
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var s = new S { Status = Status.Active };
        var t = M.Map(s);
        if (t.Status != ""Active"") throw new Exception($""Expected 'Active', got '{t.Status}'"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void StringToEnum_InConstructor_Works()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public enum Status { Active, Inactive }
public class S { public string Status { get; set; } = """"; }
public record T(Status Status);
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var s = new S { Status = ""Inactive"" };
        var t = M.Map(s);
        if (t.Status != Status.Inactive) throw new Exception($""Expected Inactive, got {t.Status}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }
}
