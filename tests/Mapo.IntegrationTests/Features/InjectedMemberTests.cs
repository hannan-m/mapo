using System;
using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// =============================================================================
// DI / Injected members: constructor parameters available in Configure lambdas
// =============================================================================

public interface IDateFormatter
{
    string Format(DateTime date);
}

public class IsoDateFormatter : IDateFormatter
{
    public string Format(DateTime date) => date.ToString("yyyy-MM-dd");
}

public class EventSource
{
    public string Title { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TicketPrice { get; set; }
}

public class EventDto
{
    public string Title { get; set; } = "";
    public string StartDisplay { get; set; } = "";
    public string EndDisplay { get; set; } = "";
    public string PriceDisplay { get; set; } = "";
}

[Mapper]
public partial class EventMapper
{
    private readonly IDateFormatter _dateFormatter;
    private readonly string _currencySymbol;

    public EventMapper(IDateFormatter dateFormatter, string currencySymbol)
    {
        _dateFormatter = dateFormatter;
        _currencySymbol = currencySymbol;
    }

    public partial EventDto Map(EventSource source);

    static void Configure(IMapConfig<EventSource, EventDto> config, IDateFormatter dateFormatter, string currencySymbol)
    {
        config
            .Map(d => d.StartDisplay, s => dateFormatter.Format(s.StartDate))
            .Map(d => d.EndDisplay, s => dateFormatter.Format(s.EndDate))
            .Map(
                d => d.PriceDisplay,
                s => currencySymbol + s.TicketPrice.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
            );
    }
}

public class InjectedMemberTests
{
    [Fact]
    public void InjectedService_ShouldBeUsedInConfigure()
    {
        var mapper = new EventMapper(new IsoDateFormatter(), "$");
        var source = new EventSource
        {
            Title = "Tech Conference",
            StartDate = new DateTime(2025, 9, 15),
            EndDate = new DateTime(2025, 9, 17),
            TicketPrice = 299.99m,
        };

        var dto = mapper.Map(source);

        dto.Title.Should().Be("Tech Conference");
        dto.StartDisplay.Should().Be("2025-09-15");
        dto.EndDisplay.Should().Be("2025-09-17");
        dto.PriceDisplay.Should().Be("$299.99");
    }

    [Fact]
    public void DifferentInjectedValues_ShouldProduceDifferentOutput()
    {
        var euroMapper = new EventMapper(new IsoDateFormatter(), "€");
        var source = new EventSource
        {
            Title = "EU Summit",
            StartDate = new DateTime(2025, 6, 1),
            EndDate = new DateTime(2025, 6, 3),
            TicketPrice = 150.00m,
        };

        var dto = euroMapper.Map(source);

        dto.PriceDisplay.Should().Be("€150.00");
    }
}
