using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for null-guard generation in flattened property mappings (Issues #1 and #2).
///
/// When a target property like "AddressCity" is flattened from "source.Address.City",
/// the generator must emit null-safe navigation. The null-guard logic must correctly
/// handle both value types (need ?? default) and reference types (no ?? default needed).
/// </summary>
public class NullGuardTests : MapoVerifier
{
    [Fact]
    public void Flattened_ValueType_ShouldEmit_NullCoalesce_Default()
    {
        // int AddressZip flattened from source.Address.Zip
        // The nullable chain produces int? so we need ?? default(int) or ?? default
        string source = @"
using Mapo.Attributes;
namespace Test;
public class Address { public int Zip { get; set; } }
public class S { public Address Address { get; set; } }
public class T { public int AddressZip { get; set; } }
[Mapper] public partial class M { public partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();

        generated.Should().Contain("Address?.Zip",
            "should use null-conditional navigation for intermediate object");
        generated.Should().Contain("?? default",
            "value type target needs ?? default to coalesce from nullable chain");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void Flattened_ReferenceType_ShouldNotEmit_NullCoalesce_Default()
    {
        // string AddressCity flattened from source.Address.City
        // The nullable chain produces string? which is assignable to string
        string source = @"
using Mapo.Attributes;
namespace Test;
public class Address { public string City { get; set; } = """"; }
public class S { public Address Address { get; set; } }
public class T { public string AddressCity { get; set; } = """"; }
[Mapper] public partial class M { public partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();

        generated.Should().Contain("Address?.City",
            "should use null-conditional navigation");
        generated.Should().NotContain("?? default",
            "reference type target does NOT need ?? default");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void Flattened_ValueType_CompilesAndRuns()
    {
        // End-to-end: flattened int property with null intermediate should produce default(int) = 0
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public class Address { public int Zip { get; set; } }
public class S { public Address Address { get; set; } }
public class T { public int AddressZip { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        // Case 1: null intermediate
        var s1 = new S { Address = null };
        var t1 = M.Map(s1);
        if (t1.AddressZip != 0) throw new Exception($""Expected 0 but got {t1.AddressZip}"");

        // Case 2: populated intermediate
        var s2 = new S { Address = new Address { Zip = 12345 } };
        var t2 = M.Map(s2);
        if (t2.AddressZip != 12345) throw new Exception($""Expected 12345 but got {t2.AddressZip}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void Flattened_ReferenceType_CompilesAndRuns()
    {
        // End-to-end: flattened string property with null intermediate should produce null
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public class Address { public string City { get; set; } = """"; }
public class S { public Address Address { get; set; } }
public class T { public string AddressCity { get; set; } = """"; }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        // Case 1: null intermediate
        var s1 = new S { Address = null };
        var t1 = M.Map(s1);
        if (t1.AddressCity != null) throw new Exception($""Expected null but got {t1.AddressCity}"");

        // Case 2: populated intermediate
        var s2 = new S { Address = new Address { City = ""NYC"" } };
        var t2 = M.Map(s2);
        if (t2.AddressCity != ""NYC"") throw new Exception($""Expected NYC but got {t2.AddressCity}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void Flattened_BoolValueType_ShouldCompile()
    {
        // bool AddressIsActive flattened from source.Address.IsActive
        string source = @"
using Mapo.Attributes;
namespace Test;
public class Address { public bool IsActive { get; set; } }
public class S { public Address Address { get; set; } }
public class T { public bool AddressIsActive { get; set; } }
[Mapper] public partial class M { public partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();

        generated.Should().Contain("Address?.IsActive ?? default",
            "bool is a value type and needs ?? default");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void Flattened_MixedTypes_AllCompile()
    {
        // Multiple flattened properties with different types in a single mapper
        string source = @"
using Mapo.Attributes;
namespace Test;
public class Info
{
    public string Name { get; set; } = """";
    public int Age { get; set; }
    public bool Active { get; set; }
}
public class S { public Info Info { get; set; } }
public class T
{
    public string InfoName { get; set; } = """";
    public int InfoAge { get; set; }
    public bool InfoActive { get; set; }
}
[Mapper] public partial class M { public partial T Map(S s); }";

        AssertGeneratedCodeCompiles(source);

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();

        // string: no ?? default
        generated.Should().Contain("Info?.Name");
        // int: has ?? default
        generated.Should().Contain("Info?.Age ?? default");
        // bool: has ?? default
        generated.Should().Contain("Info?.Active ?? default");
    }

    [Fact]
    public void Flattened_MixedTypes_CompilesAndRuns()
    {
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public class Info
{
    public string Name { get; set; } = """";
    public int Age { get; set; }
    public bool Active { get; set; }
}
public class S { public Info Info { get; set; } }
public class T
{
    public string InfoName { get; set; } = """";
    public int InfoAge { get; set; }
    public bool InfoActive { get; set; }
}
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        // Null intermediate
        var s1 = new S { Info = null };
        var t1 = M.Map(s1);
        if (t1.InfoName != null) throw new Exception(""Expected null Name"");
        if (t1.InfoAge != 0) throw new Exception(""Expected 0 Age"");
        if (t1.InfoActive != false) throw new Exception(""Expected false Active"");

        // Populated
        var s2 = new S { Info = new Info { Name = ""Bob"", Age = 30, Active = true } };
        var t2 = M.Map(s2);
        if (t2.InfoName != ""Bob"") throw new Exception($""Expected Bob but got {t2.InfoName}"");
        if (t2.InfoAge != 30) throw new Exception($""Expected 30 but got {t2.InfoAge}"");
        if (t2.InfoActive != true) throw new Exception(""Expected true Active"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NonFlattened_DirectProperty_NoNullGuard()
    {
        // Direct property mapping should NOT get null-guard treatment
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public string Name { get; set; } = """"; }
public class T { public string Name { get; set; } = """"; }
[Mapper] public partial class M { public partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();

        generated.Should().NotContain("?.");
        generated.Should().Contain("target.Name = s.Name;");
        AssertGeneratedCodeCompiles(source);
    }
}
