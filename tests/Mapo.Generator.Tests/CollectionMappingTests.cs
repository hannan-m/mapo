using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

public class CollectionMappingTests : MapoVerifier
{
    [Fact]
    public void RecursiveDiscovery_Collection_GeneratesMapper()
    {
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;
namespace Test;
public class SItem { public int Id { get; set; } }
public class TItem { public int Id { get; set; } }
public class S { public List<SItem> Items { get; set; } = new(); }
public class T { public List<TItem> Items { get; set; } = new(); }
[Mapper] public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("MapListSItemToListTItem(s.Items)");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void Collection_ArrayToList_GeneratesOptimizedLoop()
    {
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;
namespace Test;
public class SItem { public string Name { get; set; } = """"; }
public class TItem { public string Name { get; set; } = """"; }
public class S { public SItem[] Items { get; set; } = new SItem[0]; }
public class T { public List<TItem> Items { get; set; } = new(); }
[Mapper]
public partial class M 
{ 
    public partial T Map(S s); 
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("for (int i = 0; i < src.Length; i++)");
        generated.Should().Contain("var item = src[i];");
        AssertGeneratedCodeCompiles(source);
    }
}
