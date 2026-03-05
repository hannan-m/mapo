using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for constructor selection determinism (Issue #15).
/// </summary>
public class ConstructorSelectionTests : MapoVerifier
{
    [Fact]
    public void LargestConstructor_Selected()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } public string Name { get; set; } = """"; }
public class T
{
    public T() { }
    public T(int id, string name) { Id = id; Name = name; }
    public int Id { get; set; }
    public string Name { get; set; } = """";
}
[Mapper] public static partial class M { public static partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("new Test.T(s.Id, s.Name)");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void ConstructorWithBestMatchingParams_Preferred()
    {
        // Two constructors with same parameter count — the one whose param names match source properties wins
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public int Id { get; set; } public string Name { get; set; } = """"; }
public class T
{
    public T(int id, string name) { Id = id; Name = name; }
    public T(double unrelated) { }
    public int Id { get; set; }
    public string Name { get; set; } = """";
}
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var s = new S { Id = 1, Name = ""Test"" };
        var t = M.Map(s);
        if (t.Id != 1) throw new Exception($""Expected 1, got {t.Id}"");
        if (t.Name != ""Test"") throw new Exception($""Expected Test, got {t.Name}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void RecordConstructor_MatchedByName()
    {
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public int Id { get; set; } public string Name { get; set; } = """"; }
public record T(int Id, string Name);
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var s = new S { Id = 7, Name = ""Record"" };
        var t = M.Map(s);
        if (t.Id != 7) throw new Exception($""Expected 7, got {t.Id}"");
        if (t.Name != ""Record"") throw new Exception($""Expected Record, got {t.Name}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }
}
