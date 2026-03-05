using System;
using System.Globalization;
using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// =============================================================================
// Multiple converters: several type converters applied in a single mapper
// =============================================================================

public class SensorReading
{
    public Guid SensorId { get; set; }
    public double Temperature { get; set; }
    public DateTime ReadingTime { get; set; }
    public bool IsAlarm { get; set; }
    public decimal Voltage { get; set; }
}

public class SensorReadingDto
{
    public string SensorId { get; set; } = "";
    public string Temperature { get; set; } = "";
    public string ReadingTime { get; set; } = "";
    public string IsAlarm { get; set; } = "";
    public string Voltage { get; set; } = "";
}

[Mapper]
public partial class SensorMapper
{
    public partial SensorReadingDto Map(SensorReading reading);

    static void Configure(IMapConfig<SensorReading, SensorReadingDto> config)
    {
        config.AddConverter<Guid, string>(g => g.ToString("D"))
              .AddConverter<double, string>(d => d.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
              .AddConverter<DateTime, string>(dt => dt.ToString("yyyy-MM-dd HH:mm"))
              .AddConverter<bool, string>(b => b ? "YES" : "NO")
              .AddConverter<decimal, string>(d => d.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
    }
}

public class MultipleConverterTests
{
    [Fact]
    public void MultipleConverters_ShouldApplyToMatchingTypes()
    {
        var mapper = new SensorMapper();
        var id = Guid.Parse("12345678-1234-1234-1234-123456789abc");

        var reading = new SensorReading
        {
            SensorId = id,
            Temperature = 72.456,
            ReadingTime = new DateTime(2025, 7, 4, 12, 30, 0),
            IsAlarm = true,
            Voltage = 3.314m
        };

        var dto = mapper.Map(reading);

        dto.SensorId.Should().Be("12345678-1234-1234-1234-123456789abc");
        dto.Temperature.Should().Be("72.5");
        dto.ReadingTime.Should().Be("2025-07-04 12:30");
        dto.IsAlarm.Should().Be("YES");
        dto.Voltage.Should().Be("3.314");
    }

    [Fact]
    public void BoolConverter_FalseValue()
    {
        var mapper = new SensorMapper();
        var reading = new SensorReading
        {
            SensorId = Guid.Empty,
            Temperature = 0.0,
            ReadingTime = DateTime.MinValue,
            IsAlarm = false,
            Voltage = 0m
        };

        var dto = mapper.Map(reading);
        dto.IsAlarm.Should().Be("NO");
    }
}
