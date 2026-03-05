using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

public class MapFromTests : MapoVerifier
{
    [Fact]
    public void SimpleRename_MapsFromSourceProperty()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class Source { public string Type { get; set; } = """"; public int Id { get; set; } }
public class Target
{
    public int Id { get; set; }
    [MapFrom(""Type"")]
    public string ItemType { get; set; } = """";
}
[Mapper]
public partial class M { public partial Target Map(Source s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new Source { Type = ""track"", Id = 1 });
        if (result.ItemType != ""track"") throw new Exception($""ItemType: {result.ItemType}"");
        if (result.Id != 1) throw new Exception($""Id: {result.Id}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void WithEnumConversion_AutoAppliesEnumParse()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public enum ItemType { Unknown, Track, Album }
public class Source { public string? Type { get; set; } }
public class Target
{
    [MapFrom(""Type"")]
    public ItemType ItemType { get; set; }
}
[Mapper]
public partial class M { public partial Target Map(Source s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new Source { Type = ""Track"" });
        if (result.ItemType != ItemType.Track) throw new Exception($""ItemType: {result.ItemType}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void WithGlobalConverter_AppliesConverter()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class Source { public string RawId { get; set; } = """"; public string Name { get; set; } = """"; }
public class Target
{
    [MapFrom(""RawId"")]
    public Guid Id { get; set; }
    public string Name { get; set; } = """";
}
[Mapper]
public partial class M
{
    public partial Target Map(Source s);
    static void Configure(IMapConfig<Source, Target> config)
    {
        config.AddConverter<string, Guid>(s => Guid.Parse(s));
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var id = Guid.NewGuid();
        var mapper = new M();
        var result = mapper.Map(new Source { RawId = id.ToString(), Name = ""Test"" });
        if (result.Id != id) throw new Exception($""Id: {result.Id}"");
        if (result.Name != ""Test"") throw new Exception($""Name: {result.Name}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void MultipleProperties_DifferentSources()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class Source { public string Type { get; set; } = """"; public string Class { get; set; } = """"; public int Value { get; set; } }
public class Target
{
    [MapFrom(""Type"")]
    public string ItemType { get; set; } = """";
    [MapFrom(""Class"")]
    public string CssClass { get; set; } = """";
    public int Value { get; set; }
}
[Mapper]
public partial class M { public partial Target Map(Source s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new Source { Type = ""div"", Class = ""active"", Value = 42 });
        if (result.ItemType != ""div"") throw new Exception($""ItemType: {result.ItemType}"");
        if (result.CssClass != ""active"") throw new Exception($""CssClass: {result.CssClass}"");
        if (result.Value != 42) throw new Exception($""Value: {result.Value}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void CaseInsensitive_MatchesRegardlessOfCase()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class Source { public string Type { get; set; } = """"; }
public class Target
{
    [MapFrom(""type"")]
    public string ItemType { get; set; } = """";
}
[Mapper]
public partial class M { public partial Target Map(Source s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new Source { Type = ""hello"" });
        if (result.ItemType != ""hello"") throw new Exception($""ItemType: {result.ItemType}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void OnAutoDiscoveredNestedType_Works()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;
public class ItemDto { public string Type { get; set; } = """"; public int Id { get; set; } }
public class Item
{
    public int Id { get; set; }
    [MapFrom(""Type"")]
    public string Kind { get; set; } = """";
}
public class ContainerDto { public List<ItemDto> Items { get; set; } = new(); }
public class Container { public List<Item> Items { get; set; } = new(); }
[Mapper]
public partial class M { public partial Container Map(ContainerDto s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new ContainerDto
        {
            Items = new List<ItemDto>
            {
                new ItemDto { Type = ""A"", Id = 1 },
                new ItemDto { Type = ""B"", Id = 2 }
            }
        });
        if (result.Items.Count != 2) throw new Exception($""Count: {result.Items.Count}"");
        if (result.Items[0].Kind != ""A"") throw new Exception($""Kind: {result.Items[0].Kind}"");
        if (result.Items[1].Kind != ""B"") throw new Exception($""Kind: {result.Items[1].Kind}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void WithUpdateMapping_Works()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class Source { public string Type { get; set; } = """"; public int Value { get; set; } }
public class Target
{
    [MapFrom(""Type"")]
    public string Kind { get; set; } = """";
    public int Value { get; set; }
}
[Mapper]
public partial class M { public partial void Apply(Source s, Target t); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var target = new Target();
        mapper.Apply(new Source { Type = ""updated"", Value = 99 }, target);
        if (target.Kind != ""updated"") throw new Exception($""Kind: {target.Kind}"");
        if (target.Value != 99) throw new Exception($""Value: {target.Value}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void GeneratedCode_UsesCorrectSourceProperty()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class Source { public string Type { get; set; } = """"; }
public class Target
{
    [MapFrom(""Type"")]
    public string ItemType { get; set; } = """";
}
[Mapper]
public partial class M { public partial Target Map(Source s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("ItemType = s.Type");
        AssertGeneratedCodeCompiles(source);
    }
}
