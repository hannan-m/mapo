using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

public class BasicMappingTests : MapoVerifier
{
    [Fact]
    public void BasicMapping_GeneratesSuccessfully()
    {
        string source =
            "using Mapo.Attributes; namespace Test; public class S { public int Id { get; set; } } public class T { public int Id { get; set; } } [Mapper] public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        result.Results[0].GeneratedSources.Should().NotBeEmpty();
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void EnumMapping_GeneratesSwitchExpression()
    {
        string source =
            "using Mapo.Attributes; namespace Test; public enum E1 { A } public enum E2 { A } public class S { public E1 Status { get; set; } } public class T { public E2 Status { get; set; } } [Mapper] public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("switch");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void RecordMapping_UsesConstructor()
    {
        string source =
            "using Mapo.Attributes; namespace Test; public record S(int Id); public record T(int Id); [Mapper] public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("new Test.T(s.Id)");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void RequiredProperties_ShouldWork()
    {
        string source =
            "using Mapo.Attributes; namespace Test; public class S { public string Name { get; set; } } public class T { public required string Name { get; set; } } [Mapper] public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("Name = s.Name");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void InitOnlyProperties_ShouldWork()
    {
        string source =
            "using Mapo.Attributes; namespace Test; public class S { public string Name { get; set; } } public class T { public string Name { get; init; } } [Mapper] public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("Name = s.Name");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void IgnoreProperty_DoesNotMap()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { public string Name { get; set; } = """"; public string Secret { get; set; } = """"; }
public class T { public string Name { get; set; } = """"; public string Secret { get; set; } = """"; }
[Mapper]
public partial class M 
{ 
    public partial T Map(S s); 
    static void Configure(IMapConfig<S, T> config)
    {
        config.Ignore(d => d.Secret);
    }
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("target.Name = s.Name;");
        generated.Should().NotContain("target.Secret = ");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void GeneratedCode_ContainsMappingComments()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class A { public string City { get; set; } = """"; }
public class S { public int Id { get; set; } public A Home { get; set; } }
public class T { public int Id { get; set; } public string HomeCity { get; set; } = """"; }
[Mapper]
public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("// s.Id");
        generated.Should().Contain("// Flattened:");
        AssertGeneratedCodeCompiles(source);
    }
}
