using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for null source handling in generated mappers (Issue #16).
/// </summary>
public class NullHandlingTests : MapoVerifier
{
    [Fact]
    public void NullSource_ThrowsArgumentNullException()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper]
public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        bool threw = false;
        try { M.Map(null); }
        catch (ArgumentNullException) { threw = true; }
        if (!threw) throw new Exception(""Expected ArgumentNullException for null source"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NullSource_UpdateMapping_ReturnsEarly()
    {
        // Update mapping with null source should just return (no exception, no crash)
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public string Name { get; set; } = """"; }
public class T { public string Name { get; set; } = """"; }
[Mapper]
public static partial class M { public static partial void Update(S s, T t); }

public static class TestRunner
{
    public static void Run()
    {
        var t = new T { Name = ""Original"" };
        M.Update(null, t);
        if (t.Name != ""Original"") throw new Exception(""Update with null source should not modify target"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NullSource_Collection_ThrowsArgumentNullException()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;
public class SI { public int Id { get; set; } }
public class TI { public int Id { get; set; } }
[Mapper]
public static partial class M { public static partial List<TI> MapList(List<SI> src); }

public static class TestRunner
{
    public static void Run()
    {
        bool threw = false;
        try { M.MapList(null); }
        catch (ArgumentNullException) { threw = true; }
        if (!threw) throw new Exception(""Expected ArgumentNullException for null source"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NonNullSource_WorksNormally()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
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
        AssertGeneratedCodeRuns(source);
    }
}
