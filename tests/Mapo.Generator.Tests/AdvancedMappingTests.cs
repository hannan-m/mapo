using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

public class AdvancedMappingTests : MapoVerifier
{
    [Fact]
    public void Flattening_GeneratesNullSafeChain()
    {
        string source =
            "using Mapo.Attributes; namespace Test; public class A { public string City { get; set; } } public class S { public A Home { get; set; } } public class T { public string HomeCity { get; set; } } [Mapper] public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("Home?.City");
        generated.Should().NotContain("?? default", "reference type (string) should not need ?? default");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void RecursiveDiscovery_NestedObject_GeneratesMapper()
    {
        string source =
            @"
using Mapo.Attributes; 
namespace Test; 
public class A { public int Id { get; set; } } 
public class B { public int Id { get; set; } } 
public class S { public A Nested { get; set; } } 
public class T { public B Nested { get; set; } } 
[Mapper] public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("MapAToB(s.Nested)");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void MapDerived_Polymorphism_GeneratesSwitch()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public abstract class Animal { }
public class Dog : Animal { public string Bark { get; set; } = """"; }
public abstract class AnimalDto { }
public class DogDto : AnimalDto { public string Bark { get; set; } = """"; }
[Mapper]
public partial class M 
{ 
    [MapDerived(typeof(Dog), typeof(DogDto))]
    public partial AnimalDto Map(Animal s); 
    public partial DogDto MapDog(Dog d);
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("switch");
        generated.Should().Contain("case Test.Dog d:");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void GlobalConverter_IsApplied()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { public int Count { get; set; } }
public class T { public string Count { get; set; } = """"; }
[Mapper]
public partial class M 
{ 
    public partial T Map(S s); 
    static void Configure(IMapConfig<S, T> config)
    {
        config.AddConverter<int, string>(i => i.ToString() + ""!"");
    }
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("ToString() + \"!\"");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void InjectedMember_IsUsedInCustomMapping()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public interface IFormatter { string Format(int v); }
public class S { public int Value { get; set; } }
public class T { public string Value { get; set; } = """"; }
[Mapper]
public partial class M 
{ 
    private readonly IFormatter _formatter;
    public M(IFormatter formatter) { _formatter = formatter; }
    public partial T Map(S s); 
    static void Configure(IMapConfig<S, T> config, IFormatter formatter)
    {
        config.Map(d => d.Value, s => formatter.Format(s.Value));
    }
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("_formatter.Format(s.Value)");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void ReferenceTracking_GeneratesContext()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class Node { public Node? Next { get; set; } }
public class NodeDto { public NodeDto? Next { get; set; } }
[Mapper(UseReferenceTracking = true)]
public partial class M 
{ 
    public partial NodeDto Map(Node s); 
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("new MappingContext()");
        generated.Should().Contain("_context.TryGet");
        generated.Should().Contain("_context.Add");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void UpdateMapping_PopulatesExistingTarget()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { public string Name { get; set; } = """"; }
public class T { public string Name { get; set; } = """"; }
[Mapper]
public partial class M 
{ 
    public partial void Update(S source, T target); 
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("public partial void Update(Test.S source, Test.T target)");
        generated.Should().NotContain("new Test.T(");
        generated.Should().Contain("target.Name = source.Name;");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void ReverseMap_GeneratesBothDirections()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { public string Name { get; set; } = """"; }
public class T { public string Name { get; set; } = """"; }
[Mapper]
public partial class M 
{ 
    public partial T Map(S s); 
    static void Configure(IMapConfig<S, T> config)
    {
        config.ReverseMap();
    }
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("public partial Test.T Map(Test.S s)");
        generated.Should().Contain("Test.S MapTToS(Test.T src)");
        generated.Should().Contain("target.Name = src.Name;");
        AssertGeneratedCodeCompiles(source);
    }
}
