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
    public void SameElementType_DifferentContainer_DirectAssignment()
    {
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;
namespace Test;
public class Source { public List<string> Tags { get; set; } = new(); }
public class Target { public IReadOnlyList<string> Tags { get; set; } = new List<string>(); }
[Mapper] public partial class M { public partial Target Map(Source s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().NotContain("MapstringTostring");
        generated.Should().Contain("Tags = s.Tags");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void SameElementType_ListInt_ToIEnumerableInt()
    {
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;
using System;
namespace Test;
public class Source { public List<int> Ids { get; set; } = new(); }
public class Target { public IEnumerable<int> Ids { get; set; } = new List<int>(); }
[Mapper] public partial class M { public partial Target Map(Source s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new Source { Ids = new List<int> { 1, 2, 3 } });
        var list = new List<int>(result.Ids);
        if (list.Count != 3) throw new Exception($""Count: {list.Count}"");
        if (list[0] != 1) throw new Exception($""First: {list[0]}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void SameElementType_ListString_ToIReadOnlyListString_Runs()
    {
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;
using System;
namespace Test;
public class Source { public List<string> Tags { get; set; } = new(); }
public class Target { public IReadOnlyList<string> Tags { get; set; } = new List<string>(); }
[Mapper] public partial class M { public partial Target Map(Source s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new Source { Tags = new List<string> { ""a"", ""b"" } });
        if (result.Tags.Count != 2) throw new Exception($""Count: {result.Tags.Count}"");
        if (result.Tags[0] != ""a"") throw new Exception($""First: {result.Tags[0]}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NullableListString_ToNonNullableIReadOnlyList_EmitsNullCoalescing()
    {
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;
namespace Test;
#nullable enable
public class Source { public List<string>? Tags { get; set; } }
public class Target { public IReadOnlyList<string> Tags { get; set; } = new List<string>(); }
[Mapper] public partial class M { public partial Target Map(Source s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("s.Tags ?? new System.Collections.Generic.List<string>()");
        generated.Should().NotContain("MapstringTostring");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void NullableListString_ToNonNullable_ReturnsEmptyWhenNull()
    {
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;
using System;
namespace Test;
#nullable enable
public class Source { public List<string>? Tags { get; set; } }
public class Target { public IReadOnlyList<string> Tags { get; set; } = new List<string>(); }
[Mapper] public partial class M { public partial Target Map(Source s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        // Null source → empty list
        var result1 = mapper.Map(new Source { Tags = null });
        if (result1.Tags == null) throw new Exception(""Tags should not be null"");
        if (result1.Tags.Count != 0) throw new Exception($""Expected 0, got {result1.Tags.Count}"");

        // Non-null source → passed through
        var result2 = mapper.Map(new Source { Tags = new List<string> { ""a"", ""b"" } });
        if (result2.Tags.Count != 2) throw new Exception($""Expected 2, got {result2.Tags.Count}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NullableListInt_ToNonNullableIEnumerable_EmitsNullCoalescing()
    {
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;
namespace Test;
#nullable enable
public class Source { public List<int>? Scores { get; set; } }
public class Target { public IEnumerable<int> Scores { get; set; } = new List<int>(); }
[Mapper] public partial class M { public partial Target Map(Source s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("s.Scores ?? new System.Collections.Generic.List<int>()");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void NullableToNullable_SameElementType_NoCoalescing()
    {
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;
namespace Test;
#nullable enable
public class Source { public List<string>? Tags { get; set; } }
public class Target { public IReadOnlyList<string>? Tags { get; set; } }
[Mapper] public partial class M { public partial Target Map(Source s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        // Both nullable → direct assignment, no coalescing
        generated.Should().Contain("Tags = s.Tags");
        generated.Should().NotContain("??");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void NullableListString_ToNonNullableList_SameContainer_EmitsNullCoalescing()
    {
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;
namespace Test;
#nullable enable
public class Source { public List<string>? Tags { get; set; } }
public class Target { public List<string> Tags { get; set; } = new(); }
[Mapper] public partial class M { public partial Target Map(Source s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("s.Tags ?? new System.Collections.Generic.List<string>()");
        generated.Should().NotContain("s.Tags!");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void NullableListString_ToNonNullableList_SameContainer_Runs()
    {
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;
using System;
namespace Test;
#nullable enable
public class Source { public List<string>? Tags { get; set; } }
public class Target { public List<string> Tags { get; set; } = new(); }
[Mapper] public partial class M { public partial Target Map(Source s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        // Null source → empty list
        var result1 = mapper.Map(new Source { Tags = null });
        if (result1.Tags == null) throw new Exception(""Tags should not be null"");
        if (result1.Tags.Count != 0) throw new Exception($""Expected 0, got {result1.Tags.Count}"");

        // Non-null source → passed through
        var result2 = mapper.Map(new Source { Tags = new List<string> { ""x"" } });
        if (result2.Tags.Count != 1) throw new Exception($""Expected 1, got {result2.Tags.Count}"");
    }
}";
        AssertGeneratedCodeRuns(source);
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
