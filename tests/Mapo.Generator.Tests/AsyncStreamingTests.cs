using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

public class AsyncStreamingTests : MapoVerifier
{
    [Fact]
    public void StaticMapper_GeneratesAsyncStreamingExtension()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper]
public static partial class M { public static partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("IAsyncEnumerable");
        generated.Should().Contain("MapStreamAsync");
        generated.Should().Contain("EnumeratorCancellation");
    }

    [Fact]
    public void NonStaticMapper_NoAsyncStreamingExtension()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper]
public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().NotContain("IAsyncEnumerable");
        generated.Should().NotContain("MapStreamAsync");
    }

    [Fact]
    public void StaticMapper_MultiplePublicMethods_GeneratesMultipleStreams()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class A { public int Id { get; set; } }
public class B { public int Id { get; set; } }
public class C { public int Id { get; set; } }
public class D { public int Id { get; set; } }
[Mapper]
public static partial class M
{
    public static partial B MapAToB(A a);
    public static partial D MapCToD(C c);
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("MapAToBStreamAsync");
        generated.Should().Contain("MapCToDStreamAsync");
    }
}
