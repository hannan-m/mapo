using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for extension class generation correctness (Issue #17).
/// </summary>
public class ExtensionClassTests : MapoVerifier
{
    [Fact]
    public void NonStaticMapper_NoExtensionClass()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper]
public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().NotContain("Extensions");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void StaticMapper_WithUserDeclaredMethod_HasExtensionClass()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper]
public static partial class M { public static partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("MExtensions");
        generated.Should().Contain("public static Test.T Map(this Test.S source)");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void AutoDiscoveredMethods_NotInExtensionClass()
    {
        // Auto-discovered nested mapper is private — should not appear in extension class
        string source = @"
using Mapo.Attributes;
namespace Test;
public class A { public int X { get; set; } }
public class B { public int X { get; set; } }
public class S { public A Inner { get; set; } }
public class T { public B Inner { get; set; } }
[Mapper]
public static partial class M { public static partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().NotContain("MapAToB(this");
        AssertGeneratedCodeCompiles(source);
    }
}
