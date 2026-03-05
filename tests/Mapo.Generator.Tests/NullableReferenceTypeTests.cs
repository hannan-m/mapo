using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for nullable reference type handling in generated mappers.
/// Covers Bug 1 (new Type?()), Bug 4 (converter match), Bug 5 (nullable warnings), Bug 6 (null collections).
/// </summary>
public class NullableReferenceTypeTests : MapoVerifier
{
    [Fact]
    public void NullableNestedObject_GeneratesValidCode()
    {
        // Bug 1: new GeoCoordinates?() is invalid C# for reference types
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class CoordinatesDto { public string Lat { get; set; } = """"; public string Lon { get; set; } = """"; }
public class GeoCoordinates { public string Lat { get; set; } = """"; public string Lon { get; set; } = """"; }
public class LocationDto { public CoordinatesDto? Coordinates { get; set; } }
public class Location { public GeoCoordinates? Coordinates { get; set; } }
[Mapper]
public partial class LocationMapper { public partial Location Map(LocationDto source); }";
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void NullableNestedObject_NullSource_ReturnsNull()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class InnerDto { public int Value { get; set; } }
public class Inner { public int Value { get; set; } }
public class OuterDto { public InnerDto? Child { get; set; } }
public class Outer { public Inner? Child { get; set; } }
[Mapper]
public partial class M { public partial Outer Map(OuterDto source); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new OuterDto { Child = null });
        if (result.Child != null) throw new Exception(""Expected null Child when source is null"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NullableNestedObject_NonNullSource_MapsCorrectly()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class InnerDto { public int Value { get; set; } }
public class Inner { public int Value { get; set; } }
public class OuterDto { public InnerDto? Child { get; set; } }
public class Outer { public Inner? Child { get; set; } }
[Mapper]
public partial class M { public partial Outer Map(OuterDto source); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new OuterDto { Child = new InnerDto { Value = 42 } });
        if (result.Child == null) throw new Exception(""Expected non-null Child"");
        if (result.Child.Value != 42) throw new Exception($""Expected 42, got {result.Child.Value}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void MultipleNullableNestedObjects_AllCompile()
    {
        // Mimics the OCPI scenario with multiple nullable nested objects
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class BusinessDetailsDto { public string Name { get; set; } = """"; }
public class BusinessDetails { public string Name { get; set; } = """"; }
public class OpeningTimesDto { public string Hours { get; set; } = """"; }
public class OpeningTimes { public string Hours { get; set; } = """"; }
public class LocationDto
{
    public BusinessDetailsDto? Operator { get; set; }
    public BusinessDetailsDto? Suboperator { get; set; }
    public BusinessDetailsDto? Owner { get; set; }
    public OpeningTimesDto? OpeningTimes { get; set; }
}
public class Location
{
    public BusinessDetails? Operator { get; set; }
    public BusinessDetails? Suboperator { get; set; }
    public BusinessDetails? Owner { get; set; }
    public OpeningTimes? OpeningTimes { get; set; }
}
[Mapper]
public partial class LocationMapper { public partial Location Map(LocationDto source); }";
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void NullableString_ToNonNullableString_CompilesCleanly()
    {
        // Bug 5: string? → required string should not produce CS8601
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { public string? Name { get; set; } }
public class T { public string Name { get; set; } = """"; }
[Mapper]
public partial class M { public partial T Map(S s); }";
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void NullableString_ToEnum_GeneratesNullForgiving()
    {
        // Bug 5: string? → enum should use ! to suppress CS8604
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public enum Status { Active, Inactive }
public class S { public string? Status { get; set; } }
public class T { public Status Status { get; set; } }
[Mapper]
public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("!");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void NullableString_ToEnum_RuntimeConversion()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public enum Status { Active, Inactive }
public class S { public string? Status { get; set; } }
public class T { public Status Status { get; set; } }
[Mapper]
public partial class M { public partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new S { Status = ""Active"" });
        if (result.Status != Status.Active) throw new Exception($""Expected Active, got {result.Status}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NullableString_ToNonNullableString_UsesNullForgiving()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { public string? Name { get; set; } }
public class T { public string Name { get; set; } = """"; }
[Mapper]
public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("s.Name!");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void AddConverter_NullableSource_MatchesConverter()
    {
        // Bug 4: AddConverter<string, Guid> should match string? source
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public string? ImageId { get; set; } }
public class T { public Guid ImageId { get; set; } }
[Mapper]
public partial class M
{
    public partial T Map(S s);
    static void Configure(IMapConfig<S, T> config)
    {
        config.AddConverter<string, Guid>(x => Guid.Parse(x));
    }
}";
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void AddConverter_NullableSource_NullValue_ReturnsDefault()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public string? ImageId { get; set; } }
public class T { public Guid ImageId { get; set; } }
[Mapper]
public partial class M
{
    public partial T Map(S s);
    static void Configure(IMapConfig<S, T> config)
    {
        config.AddConverter<string, Guid>(x => Guid.Parse(x));
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new S { ImageId = null });
        if (result.ImageId != Guid.Empty) throw new Exception($""Expected Guid.Empty, got {result.ImageId}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void AddConverter_NullableSource_NonNullValue_ConvertsCorrectly()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public string? ImageId { get; set; } }
public class T { public Guid ImageId { get; set; } }
[Mapper]
public partial class M
{
    public partial T Map(S s);
    static void Configure(IMapConfig<S, T> config)
    {
        config.AddConverter<string, Guid>(x => Guid.Parse(x));
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var id = Guid.NewGuid();
        var mapper = new M();
        var result = mapper.Map(new S { ImageId = id.ToString() });
        if (result.ImageId != id) throw new Exception($""Expected {id}, got {result.ImageId}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }
}
