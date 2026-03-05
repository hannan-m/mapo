using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for mapper classes in the global namespace (Issue #8).
/// </summary>
public class GlobalNamespaceTests : MapoVerifier
{
    [Fact]
    public void Mapper_InGlobalNamespace_ShouldCompile()
    {
        string source =
            @"
using Mapo.Attributes;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper]
public static partial class M { public static partial T Map(S s); }";
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void Mapper_InGlobalNamespace_CompilesAndRuns()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper]
public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var s = new S { Id = 42 };
        var t = M.Map(s);
        if (t.Id != 42) throw new Exception($""Expected 42, got {t.Id}"");
    }
}";
        AssertGeneratedCodeRuns(source, "TestRunner", "Run");
    }
}
