using System;
using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

public class CustomConverterSource
{
    public DateTime CreatedAt { get; set; }
    public decimal Price { get; set; }
    public Guid Identifier { get; set; }
}

public class CustomConverterDest
{
    public string CreatedAt { get; set; } = "";
    public string Price { get; set; } = "";
    public string Identifier { get; set; } = "";
}

[Mapper]
public partial class CustomConverterMapper
{
    public partial CustomConverterDest Map(CustomConverterSource source);

    static void Configure(IMapConfig<CustomConverterSource, CustomConverterDest> config)
    {
        config.AddConverter<DateTime, string>(dt => dt.ToString("yyyy-MM-dd"));
        config.AddConverter<decimal, string>(d =>
            $"${d.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}"
        );
        config.AddConverter<Guid, string>(g => g.ToString("N"));
    }
}

public class CustomConverterTests
{
    [Fact]
    public void AddConverter_ShouldApplyToAllPropertiesOfMatchingTypes()
    {
        var mapper = new CustomConverterMapper();
        var id = Guid.NewGuid();
        var date = new DateTime(2024, 5, 20);

        var source = new CustomConverterSource
        {
            CreatedAt = date,
            Price = 49.99m,
            Identifier = id,
        };

        var dest = mapper.Map(source);

        dest.CreatedAt.Should().Be("2024-05-20");
        dest.Price.Should().Be("$49.99");
        dest.Identifier.Should().Be(id.ToString("N"));
    }
}
