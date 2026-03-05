using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Mapo.Generator.Tests;

public class NullableCoercionTests : MapoVerifier
{
    [Fact]
    public void NullableInt_ToInt_GeneratesCoercion()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public int? Score { get; set; } }
public class T { public int Score { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("?? default");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void NullableDateTime_ToDateTime_GeneratesCoercion()
    {
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public DateTime? Created { get; set; } }
public class T { public DateTime Created { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("?? default");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void NullableToNonNullable_WithExplicitConverter_ConverterWins()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public int? Score { get; set; } }
public class T { public int Score { get; set; } }
[Mapper] public static partial class M
{
    public static partial T Map(S s);
    static void Configure(IMapConfig<S, T> config)
    {
        config.AddConverter<int?, int>(x => x ?? -1);
    }
}";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("-1");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void NullableInt_Null_CoercesToDefault_CompilesAndRuns()
    {
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public int? Score { get; set; } }
public class T { public int Score { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var s1 = new S { Score = null };
        var t1 = M.Map(s1);
        if (t1.Score != 0) throw new Exception($""Expected 0, got {t1.Score}"");

        var s2 = new S { Score = 42 };
        var t2 = M.Map(s2);
        if (t2.Score != 42) throw new Exception($""Expected 42, got {t2.Score}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void MAPO009_ReportedAsInfo()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public int? Score { get; set; } }
public class T { public int Score { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "MAPO009");
        diag.Should().NotBeNull();
        diag!.Severity.Should().Be(DiagnosticSeverity.Info);
    }

    [Fact]
    public void NullableGuid_ToGuid_CompilesAndRuns()
    {
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public Guid? Id { get; set; } }
public class T { public Guid Id { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var g = Guid.NewGuid();
        var s = new S { Id = g };
        var t = M.Map(s);
        if (t.Id != g) throw new Exception($""Expected {g}, got {t.Id}"");

        var s2 = new S { Id = null };
        var t2 = M.Map(s2);
        if (t2.Id != Guid.Empty) throw new Exception($""Expected empty, got {t2.Id}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }
}
