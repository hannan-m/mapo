using System;
using System.Globalization;
using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// --- Types for complex converter tests ---

public class CoordinatesDto
{
    public string Latitude { get; set; } = "";
    public string Longitude { get; set; } = "";
}

public class GeoPoint
{
    public double Lat { get; set; }
    public double Lon { get; set; }
}

public class LocationSource
{
    public string Name { get; set; } = "";
    public CoordinatesDto? Position { get; set; }
    public CoordinatesDto? Fallback { get; set; }
}

public class LocationTarget
{
    public string Name { get; set; } = "";
    public GeoPoint? Position { get; set; }
    public GeoPoint? Fallback { get; set; }
}

[Mapper]
public partial class ComplexConverterMapper
{
    public partial LocationTarget Map(LocationSource src);

    static void Configure(IMapConfig<LocationSource, LocationTarget> config)
    {
        config.AddConverter<CoordinatesDto, GeoPoint>(c => new GeoPoint
        {
            Lat = double.Parse(c.Latitude, System.Globalization.CultureInfo.InvariantCulture),
            Lon = double.Parse(c.Longitude, System.Globalization.CultureInfo.InvariantCulture),
        });
    }
}

public class ComplexConverterIntegrationTests
{
    [Fact]
    public void ClassToClass_ConvertsCorrectly()
    {
        var mapper = new ComplexConverterMapper();
        var result = mapper.Map(
            new LocationSource
            {
                Name = "Berlin",
                Position = new CoordinatesDto { Latitude = "52.52", Longitude = "13.405" },
                Fallback = new CoordinatesDto { Latitude = "48.8566", Longitude = "2.3522" },
            }
        );

        result.Name.Should().Be("Berlin");
        result.Position.Should().NotBeNull();
        result.Position!.Lat.Should().BeApproximately(52.52, 0.001);
        result.Position!.Lon.Should().BeApproximately(13.405, 0.001);
        result.Fallback.Should().NotBeNull();
        result.Fallback!.Lat.Should().BeApproximately(48.8566, 0.001);
    }

    [Fact]
    public void NullableSource_ReturnsNull()
    {
        var mapper = new ComplexConverterMapper();
        var result = mapper.Map(
            new LocationSource
            {
                Name = "Empty",
                Position = null,
                Fallback = null,
            }
        );

        result.Name.Should().Be("Empty");
        result.Position.Should().BeNull();
        result.Fallback.Should().BeNull();
    }
}
