using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using FluentAssertions;
using Xunit;
using Mapo.Generator.Models;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for incremental pipeline caching correctness (Issues #9 and #10).
/// The incremental generator uses Equals/GetHashCode on ParseResult and MapperInfo
/// to decide whether to re-emit. Bugs here cause stale diagnostics or missed re-generation.
/// </summary>
public class IncrementalCachingTests : MapoVerifier
{
    #region ParseResult.Equals tests

    [Fact]
    public void ParseResult_SameDiagnosticCount_DifferentContent_ShouldNotBeEqual()
    {
        // Two ParseResults with 1 diagnostic each but different messages
        // must NOT be considered equal
        var diag1 = Diagnostic.Create(
            Diagnostics.DiagnosticDescriptors.UnmappedPropertyWarning,
            Location.None,
            "PropertyA", "TypeX");

        var diag2 = Diagnostic.Create(
            Diagnostics.DiagnosticDescriptors.UnmappedPropertyWarning,
            Location.None,
            "PropertyB", "TypeX");

        var result1 = new ParseResult(null, new List<Diagnostic> { diag1 });
        var result2 = new ParseResult(null, new List<Diagnostic> { diag2 });

        result1.Equals(result2).Should().BeFalse(
            "ParseResult with different diagnostic messages should not be equal even if count matches");
    }

    [Fact]
    public void ParseResult_IdenticalDiagnostics_ShouldBeEqual()
    {
        var diag1 = Diagnostic.Create(
            Diagnostics.DiagnosticDescriptors.UnmappedPropertyWarning,
            Location.None,
            "PropertyA", "TypeX");

        var diag2 = Diagnostic.Create(
            Diagnostics.DiagnosticDescriptors.UnmappedPropertyWarning,
            Location.None,
            "PropertyA", "TypeX");

        var result1 = new ParseResult(null, new List<Diagnostic> { diag1 });
        var result2 = new ParseResult(null, new List<Diagnostic> { diag2 });

        result1.Equals(result2).Should().BeTrue(
            "ParseResult with identical diagnostic content should be equal");
    }

    [Fact]
    public void ParseResult_DifferentDiagnosticSeverities_ShouldNotBeEqual()
    {
        var diag1 = Diagnostic.Create(
            Diagnostics.DiagnosticDescriptors.UnmappedPropertyWarning,
            Location.None,
            "PropertyA", "TypeX");

        // Create an error-severity version (same descriptor but elevated)
        var diag2 = Diagnostic.Create(
            Diagnostics.DiagnosticDescriptors.MapperOnNonPartialClass,
            Location.None,
            "MyMapper");

        var result1 = new ParseResult(null, new List<Diagnostic> { diag1 });
        var result2 = new ParseResult(null, new List<Diagnostic> { diag2 });

        result1.Equals(result2).Should().BeFalse(
            "ParseResult with different diagnostic IDs should not be equal");
    }

    [Fact]
    public void ParseResult_DifferentDiagnosticCounts_ShouldNotBeEqual()
    {
        var diag = Diagnostic.Create(
            Diagnostics.DiagnosticDescriptors.UnmappedPropertyWarning,
            Location.None,
            "PropertyA", "TypeX");

        var result1 = new ParseResult(null, new List<Diagnostic> { diag });
        var result2 = new ParseResult(null, new List<Diagnostic> { diag, diag });

        result1.Equals(result2).Should().BeFalse();
    }

    [Fact]
    public void ParseResult_BothNullMapper_NoDiagnostics_ShouldBeEqual()
    {
        var result1 = new ParseResult(null, new List<Diagnostic>());
        var result2 = new ParseResult(null, new List<Diagnostic>());

        result1.Equals(result2).Should().BeTrue();
    }

    [Fact]
    public void ParseResult_NullVsNonNullMapper_ShouldNotBeEqual()
    {
        var mapper = new MapperInfo("Test", "M", true, false, false,
            new List<MethodMapping>(), new List<InjectedMember>(), new List<GlobalConverter>());

        var result1 = new ParseResult(null, new List<Diagnostic>());
        var result2 = new ParseResult(mapper, new List<Diagnostic>());

        result1.Equals(result2).Should().BeFalse();
    }

    [Fact]
    public void ParseResult_EqualObjects_ShouldHaveEqualHashCodes()
    {
        var diag1 = Diagnostic.Create(
            Diagnostics.DiagnosticDescriptors.UnmappedPropertyWarning,
            Location.None,
            "PropertyA", "TypeX");

        var diag2 = Diagnostic.Create(
            Diagnostics.DiagnosticDescriptors.UnmappedPropertyWarning,
            Location.None,
            "PropertyA", "TypeX");

        var result1 = new ParseResult(null, new List<Diagnostic> { diag1 });
        var result2 = new ParseResult(null, new List<Diagnostic> { diag2 });

        // If two objects are equal, their hash codes must match
        result1.Equals(result2).Should().BeTrue("precondition: results should be equal");
        result1.GetHashCode().Should().Be(result2.GetHashCode(),
            "equal ParseResult objects must have equal hash codes");
    }

    #endregion

    #region MapperInfo.GetHashCode tests

    [Fact]
    public void MapperInfo_DifferentMappings_ShouldHaveDifferentHashCodes()
    {
        var mapping1 = new MethodMapping("MapAToB", "A", "B", "B", false, "src",
            new List<string> { "A src" }, new List<ConstructorArg>(),
            new List<PropertyMapping>(), new List<string>(), true);

        var mapping2 = new MethodMapping("MapXToY", "X", "Y", "Y", false, "src",
            new List<string> { "X src" }, new List<ConstructorArg>(),
            new List<PropertyMapping>(), new List<string>(), true);

        var info1 = new MapperInfo("Test", "M", true, false, false,
            new List<MethodMapping> { mapping1 }, new List<InjectedMember>(), new List<GlobalConverter>());

        var info2 = new MapperInfo("Test", "M", true, false, false,
            new List<MethodMapping> { mapping2 }, new List<InjectedMember>(), new List<GlobalConverter>());

        // These are NOT equal
        info1.Equals(info2).Should().BeFalse("precondition: different mappings means not equal");

        // While different hash codes aren't strictly required for unequal objects,
        // a good hash function should differentiate these. More importantly,
        // we're testing the fix ensures collection data IS included in the hash.
        // The old bug was that hash was identical for all MapperInfo with same scalar fields.
        info1.GetHashCode().Should().NotBe(info2.GetHashCode(),
            "MapperInfo with different Mappings should ideally have different hash codes");
    }

    [Fact]
    public void MapperInfo_EqualObjects_MustHaveEqualHashCodes()
    {
        var mapping = new MethodMapping("MapAToB", "A", "B", "B", false, "src",
            new List<string> { "A src" }, new List<ConstructorArg>(),
            new List<PropertyMapping>(), new List<string>(), true);

        var info1 = new MapperInfo("Test", "M", true, false, false,
            new List<MethodMapping> { mapping }, new List<InjectedMember>(), new List<GlobalConverter>());

        var info2 = new MapperInfo("Test", "M", true, false, false,
            new List<MethodMapping> { mapping }, new List<InjectedMember>(), new List<GlobalConverter>());

        info1.Equals(info2).Should().BeTrue("precondition: objects should be equal");
        info1.GetHashCode().Should().Be(info2.GetHashCode(),
            "equal MapperInfo objects MUST have equal hash codes (Equals/GetHashCode contract)");
    }

    [Fact]
    public void MapperInfo_DifferentInjectedMembers_ShouldHaveDifferentHashCodes()
    {
        var injected1 = new List<InjectedMember> { new InjectedMember("IFoo", "foo") };
        var injected2 = new List<InjectedMember> { new InjectedMember("IBar", "bar") };

        var info1 = new MapperInfo("Test", "M", false, false, false,
            new List<MethodMapping>(), injected1, new List<GlobalConverter>());

        var info2 = new MapperInfo("Test", "M", false, false, false,
            new List<MethodMapping>(), injected2, new List<GlobalConverter>());

        info1.Equals(info2).Should().BeFalse("precondition: different injected members");
        info1.GetHashCode().Should().NotBe(info2.GetHashCode(),
            "MapperInfo with different InjectedMembers should ideally have different hash codes");
    }

    [Fact]
    public void MapperInfo_DifferentGlobalConverters_ShouldHaveDifferentHashCodes()
    {
        var conv1 = new List<GlobalConverter> { new GlobalConverter("int", "string", true, "x", "x.ToString()") };
        var conv2 = new List<GlobalConverter> { new GlobalConverter("int", "bool", false, "x", "x > 0") };

        var info1 = new MapperInfo("Test", "M", false, false, false,
            new List<MethodMapping>(), new List<InjectedMember>(), conv1);

        var info2 = new MapperInfo("Test", "M", false, false, false,
            new List<MethodMapping>(), new List<InjectedMember>(), conv2);

        info1.Equals(info2).Should().BeFalse("precondition: different converters");
        info1.GetHashCode().Should().NotBe(info2.GetHashCode(),
            "MapperInfo with different GlobalConverters should ideally have different hash codes");
    }

    [Fact]
    public void MapperInfo_EmptyCollections_ShouldBeEqual()
    {
        var info1 = new MapperInfo("Test", "M", true, false, false,
            new List<MethodMapping>(), new List<InjectedMember>(), new List<GlobalConverter>());

        var info2 = new MapperInfo("Test", "M", true, false, false,
            new List<MethodMapping>(), new List<InjectedMember>(), new List<GlobalConverter>());

        info1.Equals(info2).Should().BeTrue();
        info1.GetHashCode().Should().Be(info2.GetHashCode());
    }

    #endregion

    #region Integration: Incremental re-emission

    [Fact]
    public void PropertyRename_ShouldProduceDifferentParseResult()
    {
        // Simulates the scenario where a user renames a property.
        // The generator must detect the change and re-emit.
        string source1 = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } public string Name { get; set; } = """"; }
public class T { public int Id { get; set; } public string Name { get; set; } = """"; }
[Mapper] public partial class M { public partial T Map(S s); }";

        string source2 = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } public string FullName { get; set; } = """"; }
public class T { public int Id { get; set; } public string Name { get; set; } = """"; }
[Mapper] public partial class M { public partial T Map(S s); }";

        var result1 = RunGenerator(source1);
        var result2 = RunGenerator(source2);

        var gen1 = result1.Results[0].GeneratedSources[0].SourceText.ToString();
        var gen2 = result2.Results[0].GeneratedSources[0].SourceText.ToString();

        gen1.Should().NotBe(gen2,
            "renaming a source property should produce different generated code");
    }

    [Fact]
    public void AddingUnmappedProperty_ShouldChangeDiagnostics()
    {
        // When a user adds a new unmapped property, the diagnostics should change
        string source1 = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper(StrictMode = true)] public partial class M { public partial T Map(S s); }";

        string source2 = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } public string Extra { get; set; } = """"; }
[Mapper(StrictMode = true)] public partial class M { public partial T Map(S s); }";

        var result1 = RunGenerator(source1);
        var result2 = RunGenerator(source2);

        var diag1 = result1.Diagnostics.Where(d => d.Id == "MAPO001").ToList();
        var diag2 = result2.Diagnostics.Where(d => d.Id == "MAPO001").ToList();

        diag2.Should().HaveCountGreaterThan(diag1.Count,
            "adding an unmapped property should produce additional diagnostics");
    }

    [Fact]
    public void IdenticalSource_ShouldProduceEqualParseResults()
    {
        // Two identical compilations should produce equal results
        // (so the incremental pipeline can skip re-emission)
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper] public partial class M { public partial T Map(S s); }";

        var result1 = RunGenerator(source);
        var result2 = RunGenerator(source);

        var gen1 = result1.Results[0].GeneratedSources[0].SourceText.ToString();
        var gen2 = result2.Results[0].GeneratedSources[0].SourceText.ToString();

        gen1.Should().Be(gen2,
            "identical source should produce identical generated code");
    }

    #endregion

    #region MethodMapping.Equals/GetHashCode contract

    [Fact]
    public void MethodMapping_EqualObjects_MustHaveEqualHashCodes()
    {
        var pm = new PropertyMapping("Name", "src.Name");
        var mapping1 = new MethodMapping("Map", "S", "T", "T", false, "src",
            new List<string> { "S src" }, new List<ConstructorArg>(),
            new List<PropertyMapping> { pm }, new List<string>(), true);

        var mapping2 = new MethodMapping("Map", "S", "T", "T", false, "src",
            new List<string> { "S src" }, new List<ConstructorArg>(),
            new List<PropertyMapping> { pm }, new List<string>(), true);

        mapping1.Equals(mapping2).Should().BeTrue("precondition");
        mapping1.GetHashCode().Should().Be(mapping2.GetHashCode(),
            "equal MethodMapping objects must have equal hash codes");
    }

    [Fact]
    public void MethodMapping_DifferentPropertyMappings_ShouldNotBeEqual()
    {
        var pm1 = new PropertyMapping("Name", "src.Name");
        var pm2 = new PropertyMapping("Title", "src.Title");

        var mapping1 = new MethodMapping("Map", "S", "T", "T", false, "src",
            new List<string> { "S src" }, new List<ConstructorArg>(),
            new List<PropertyMapping> { pm1 }, new List<string>(), true);

        var mapping2 = new MethodMapping("Map", "S", "T", "T", false, "src",
            new List<string> { "S src" }, new List<ConstructorArg>(),
            new List<PropertyMapping> { pm2 }, new List<string>(), true);

        mapping1.Equals(mapping2).Should().BeFalse();
    }

    #endregion
}
