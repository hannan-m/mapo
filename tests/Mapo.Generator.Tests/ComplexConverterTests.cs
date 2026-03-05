using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

public class ComplexConverterTests : MapoVerifier
{
    [Fact]
    public void ClassToClass_GeneratesConverterCall()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class CoordinatesDto { public string Lat { get; set; } = """"; public string Lon { get; set; } = """"; }
public class GeoPoint { public double Latitude { get; set; } public double Longitude { get; set; } }
public class Source { public CoordinatesDto Coords { get; set; } = new(); }
public class Target { public GeoPoint Coords { get; set; } = new(); }
[Mapper]
public partial class M
{
    public partial Target Map(Source s);
    static void Configure(IMapConfig<Source, Target> config)
    {
        config.AddConverter<CoordinatesDto, GeoPoint>(c =>
            new GeoPoint { Latitude = double.Parse(c.Lat), Longitude = double.Parse(c.Lon) });
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new Source { Coords = new CoordinatesDto { Lat = ""52.52"", Lon = ""13.405"" } });
        if (Math.Abs(result.Coords.Latitude - 52.52) > 0.001) throw new Exception($""Lat: {result.Coords.Latitude}"");
        if (Math.Abs(result.Coords.Longitude - 13.405) > 0.001) throw new Exception($""Lon: {result.Coords.Longitude}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NullableSource_WrapsWithNullCheck()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class CoordinatesDto { public string Lat { get; set; } = """"; public string Lon { get; set; } = """"; }
public class GeoPoint { public double Latitude { get; set; } public double Longitude { get; set; } }
public class Source { public CoordinatesDto? Coords { get; set; } }
public class Target { public GeoPoint? Coords { get; set; } }
[Mapper]
public partial class M
{
    public partial Target Map(Source s);
    static void Configure(IMapConfig<Source, Target> config)
    {
        config.AddConverter<CoordinatesDto, GeoPoint>(c =>
            new GeoPoint { Latitude = double.Parse(c.Lat), Longitude = double.Parse(c.Lon) });
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var withCoords = mapper.Map(new Source { Coords = new CoordinatesDto { Lat = ""1.0"", Lon = ""2.0"" } });
        if (withCoords.Coords == null) throw new Exception(""Expected non-null Coords"");
        if (Math.Abs(withCoords.Coords.Latitude - 1.0) > 0.001) throw new Exception($""Lat: {withCoords.Coords.Latitude}"");

        var withNull = mapper.Map(new Source { Coords = null });
        if (withNull.Coords != null) throw new Exception(""Expected null Coords"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void AppliesToMultipleProperties_SameTypes()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class MoneyDto { public string Amount { get; set; } = """"; public string Currency { get; set; } = """"; }
public class Money { public decimal Amount { get; set; } public string Currency { get; set; } = """"; }
public class Source { public MoneyDto Price { get; set; } = new(); public MoneyDto Tax { get; set; } = new(); }
public class Target { public Money Price { get; set; } = new(); public Money Tax { get; set; } = new(); }
[Mapper]
public partial class M
{
    public partial Target Map(Source s);
    static void Configure(IMapConfig<Source, Target> config)
    {
        config.AddConverter<MoneyDto, Money>(m =>
            new Money { Amount = decimal.Parse(m.Amount), Currency = m.Currency });
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new Source
        {
            Price = new MoneyDto { Amount = ""99.99"", Currency = ""EUR"" },
            Tax = new MoneyDto { Amount = ""19.00"", Currency = ""EUR"" }
        });
        if (result.Price.Amount != 99.99m) throw new Exception($""Price: {result.Price.Amount}"");
        if (result.Tax.Amount != 19.00m) throw new Exception($""Tax: {result.Tax.Amount}"");
        if (result.Price.Currency != ""EUR"") throw new Exception($""Currency: {result.Price.Currency}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void ConverterTakesPrecedence_OverAutoMapping()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class InnerDto { public int X { get; set; } }
public class Inner { public int X { get; set; } public string Tag { get; set; } = """"; }
public class Source { public InnerDto Child { get; set; } = new(); }
public class Target { public Inner Child { get; set; } = new(); }
[Mapper]
public partial class M
{
    public partial Target Map(Source s);
    static void Configure(IMapConfig<Source, Target> config)
    {
        config.AddConverter<InnerDto, Inner>(d => new Inner { X = d.X * 10, Tag = ""converted"" });
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new Source { Child = new InnerDto { X = 5 } });
        if (result.Child.X != 50) throw new Exception($""X: {result.Child.X} (expected 50 from converter)"");
        if (result.Child.Tag != ""converted"") throw new Exception($""Tag: {result.Child.Tag}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void GeneratedCode_ContainsConverterExpression()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class ADto { public int Value { get; set; } }
public class B { public int Value { get; set; } }
public class S { public ADto Data { get; set; } = new(); }
public class T { public B Data { get; set; } = new(); }
[Mapper]
public partial class M
{
    public partial T Map(S s);
    static void Configure(IMapConfig<S, T> config)
    {
        config.AddConverter<ADto, B>(a => new B { Value = a.Value + 1 });
    }
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("new B { Value = (s.Data).Value + 1 }");
        generated.Should().NotContain("MapADtoToB");
        AssertGeneratedCodeCompiles(source);
    }
}
