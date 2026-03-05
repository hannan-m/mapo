using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for ReverseMap() functionality (Issue #12).
/// </summary>
public class ReverseMapTests : MapoVerifier
{
    [Fact]
    public void ReverseMap_SameNameProperties_CompilesAndRuns()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public string Name { get; set; } = """"; public int Age { get; set; } }
public class T { public string Name { get; set; } = """"; public int Age { get; set; } }
[Mapper]
public static partial class M
{
    public static partial T Map(S s);
    static void Configure(IMapConfig<S, T> config) { config.ReverseMap(); }
}

public static class TestRunner
{
    public static void Run()
    {
        var s = new S { Name = ""Alice"", Age = 30 };
        var t = M.Map(s);
        if (t.Name != ""Alice"") throw new Exception($""Forward: Expected Alice, got {t.Name}"");

        var reversed = M.MapTToS(t);
        if (reversed.Name != ""Alice"") throw new Exception($""Reverse: Expected Alice, got {reversed.Name}"");
        if (reversed.Age != 30) throw new Exception($""Reverse: Expected 30, got {reversed.Age}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void ReverseMap_GeneratesBothDirections()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } public string Name { get; set; } = """"; }
public class T { public int Id { get; set; } public string Name { get; set; } = """"; }
[Mapper]
public static partial class M
{
    public static partial T Map(S s);
    static void Configure(IMapConfig<S, T> config) { config.ReverseMap(); }
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("MapTToS");
        AssertGeneratedCodeCompiles(source);
    }
}
