using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// 3-level nested types
public class GeoCountry
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
}

public class GeoAddress
{
    public string City { get; set; } = "";
    public GeoCountry Country { get; set; } = new();
}

public class Organization
{
    public string Name { get; set; } = "";
    public GeoAddress Headquarters { get; set; } = new();
}

public class OrganizationFlatDto
{
    public string Name { get; set; } = "";
    public string HeadquartersCity { get; set; } = ""; // 2-level
    public string HeadquartersCountryName { get; set; } = ""; // 3-level
    public string HeadquartersCountryCode { get; set; } = ""; // 3-level
}

[Mapper]
public static partial class MultiLevelFlatMapper
{
    public static partial OrganizationFlatDto Map(Organization org);
}

public class MultiLevelFlatteningTests
{
    [Fact]
    public void ThreeLevelFlattening_MapsCorrectly()
    {
        var org = new Organization
        {
            Name = "Acme Corp",
            Headquarters = new GeoAddress
            {
                City = "Berlin",
                Country = new GeoCountry { Name = "Germany", Code = "DE" },
            },
        };

        var dto = MultiLevelFlatMapper.Map(org);

        dto.Name.Should().Be("Acme Corp");
        dto.HeadquartersCity.Should().Be("Berlin");
        dto.HeadquartersCountryName.Should().Be("Germany");
        dto.HeadquartersCountryCode.Should().Be("DE");
    }

    [Fact]
    public void ThreeLevelFlattening_MixedLevels()
    {
        var org = new Organization
        {
            Name = "Test Inc",
            Headquarters = new GeoAddress
            {
                City = "Tokyo",
                Country = new GeoCountry { Name = "Japan", Code = "JP" },
            },
        };

        var dto = MultiLevelFlatMapper.Map(org);

        // 1-level direct match
        dto.Name.Should().Be("Test Inc");
        // 2-level flattening
        dto.HeadquartersCity.Should().Be("Tokyo");
        // 3-level flattening
        dto.HeadquartersCountryName.Should().Be("Japan");
        dto.HeadquartersCountryCode.Should().Be("JP");
    }
}
