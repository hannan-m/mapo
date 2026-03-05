using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

public class DeepFlatteningTests : MapoVerifier
{
    [Fact]
    public void TwoLevelFlattening_GeneratesCorrectCode()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class Inner { public string Name { get; set; } = """"; }
public class Middle { public Inner Inner { get; set; } = new(); }
public class S { public Middle Middle { get; set; } = new(); }
public class T { public string MiddleInnerName { get; set; } = """"; }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("Middle");
        generated.Should().Contain("Inner");
        generated.Should().Contain("Name");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void ThreeLevelFlattening_GeneratesCorrectCode()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class L3 { public string Value { get; set; } = """"; }
public class L2 { public L3 L3 { get; set; } = new(); }
public class L1 { public L2 L2 { get; set; } = new(); }
public class S { public L1 L1 { get; set; } = new(); }
public class T { public string L1L2L3Value { get; set; } = """"; }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("L1");
        generated.Should().Contain("L2");
        generated.Should().Contain("L3");
        generated.Should().Contain("Value");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void FourLevelFlattening_StopsAtDepthCap()
    {
        // 5 levels deep: should NOT match (depth cap is 4)
        string source = @"
using Mapo.Attributes;
namespace Test;
public class L4 { public string X { get; set; } = """"; }
public class L3 { public L4 L4 { get; set; } = new(); }
public class L2 { public L3 L3 { get; set; } = new(); }
public class L1 { public L2 L2 { get; set; } = new(); }
public class S { public L1 L1 { get; set; } = new(); }
public class T { public string L1L2L3L4X { get; set; } = """"; }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        // Should produce MAPO001 for unmapped property
        result.Diagnostics.Any(d => d.Id == "MAPO001").Should().BeTrue();
    }

    [Fact]
    public void DeepFlattening_WithNullable_GeneratesNullGuards()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class Country { public string Name { get; set; } = """"; }
public class Address { public Country Country { get; set; } = new(); }
public class S { public Address Address { get; set; } = new(); }
public class T { public string AddressCountryName { get; set; } = """"; }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("?.");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void TwoLevelFlattening_CompilesAndRuns()
    {
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public class Country { public string Name { get; set; } = """"; }
public class Address { public string City { get; set; } = """"; public Country Country { get; set; } = new(); }
public class S { public Address Headquarters { get; set; } = new(); }
public class T { public string HeadquartersCity { get; set; } = """"; public string HeadquartersCountryName { get; set; } = """"; }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var s = new S
        {
            Headquarters = new Address
            {
                City = ""Tokyo"",
                Country = new Country { Name = ""Japan"" }
            }
        };
        var t = M.Map(s);
        if (t.HeadquartersCity != ""Tokyo"") throw new Exception($""Expected 'Tokyo', got '{t.HeadquartersCity}'"");
        if (t.HeadquartersCountryName != ""Japan"") throw new Exception($""Expected 'Japan', got '{t.HeadquartersCountryName}'"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }
}
