using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for nullable collection handling (Bug 6: null collections should return empty, not throw).
/// </summary>
public class NullableCollectionTests : MapoVerifier
{
    [Fact]
    public void NullableCollection_ReturnsEmpty_NotThrow()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;
public class SItem { public int Id { get; set; } }
public class TItem { public int Id { get; set; } }
public class S { public List<SItem>? Items { get; set; } }
public class T { public List<TItem> Items { get; set; } = new(); }
[Mapper]
public partial class M { public partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new S { Items = null });
        if (result.Items == null) throw new Exception(""Expected non-null list"");
        if (result.Items.Count != 0) throw new Exception($""Expected 0, got {result.Items.Count}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NullableCollection_NonNull_MapsNormally()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;
public class SItem { public int Id { get; set; } }
public class TItem { public int Id { get; set; } }
public class S { public List<SItem>? Items { get; set; } }
public class T { public List<TItem> Items { get; set; } = new(); }
[Mapper]
public partial class M { public partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new S { Items = new List<SItem> { new SItem { Id = 1 }, new SItem { Id = 2 } } });
        if (result.Items.Count != 2) throw new Exception($""Expected 2, got {result.Items.Count}"");
        if (result.Items[0].Id != 1) throw new Exception($""Expected 1, got {result.Items[0].Id}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NonNullableCollection_StillThrowsOnNull()
    {
        // Non-nullable collection should still throw ArgumentNullException
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;
public class SItem { public int Id { get; set; } }
public class TItem { public int Id { get; set; } }
public class S { public List<SItem> Items { get; set; } = new(); }
public class T { public List<TItem> Items { get; set; } = new(); }
[Mapper]
public partial class M
{
    public partial T Map(S s);
    public partial List<TItem> MapList(List<SItem> src);
}

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        bool threw = false;
        try { mapper.MapList(null!); }
        catch (ArgumentNullException) { threw = true; }
        if (!threw) throw new Exception(""Expected ArgumentNullException for null non-nullable collection"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }
}
