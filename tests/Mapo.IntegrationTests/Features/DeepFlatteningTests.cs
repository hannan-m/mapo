using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// =============================================================================
// Flattening: nested property access (e.g., Headquarters.City → HeadquartersCity)
// Tests single-level flattening from multiple nested objects
// =============================================================================

public class Company
{
    public string Name { get; set; } = "";
    public CompanyAddress Headquarters { get; set; } = new();
    public CompanyContact PrimaryContact { get; set; } = new();
}

public class CompanyAddress
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
}

public class CompanyContact
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

public class CompanyFlatDto
{
    public string Name { get; set; } = "";
    public string HeadquartersCity { get; set; } = "";
    public string HeadquartersStreet { get; set; } = "";
    public string HeadquartersCountry { get; set; } = "";
    public string PrimaryContactName { get; set; } = "";
    public string PrimaryContactEmail { get; set; } = "";
}

[Mapper]
public partial class DeepFlatteningMapper
{
    public partial CompanyFlatDto Map(Company company);
}

public class DeepFlatteningTests
{
    [Fact]
    public void ShouldFlattenMultipleNestedObjects()
    {
        var mapper = new DeepFlatteningMapper();

        var company = new Company
        {
            Name = "Acme Corp",
            Headquarters = new CompanyAddress
            {
                Street = "123 Main St",
                City = "San Francisco",
                Country = "USA",
            },
            PrimaryContact = new CompanyContact { Name = "John CEO", Email = "ceo@acme.com" },
        };

        var dto = mapper.Map(company);

        dto.Name.Should().Be("Acme Corp");
        dto.HeadquartersCity.Should().Be("San Francisco");
        dto.HeadquartersStreet.Should().Be("123 Main St");
        dto.HeadquartersCountry.Should().Be("USA");
        dto.PrimaryContactName.Should().Be("John CEO");
        dto.PrimaryContactEmail.Should().Be("ceo@acme.com");
    }

    [Fact]
    public void ShouldFlattenFromDifferentNestedSources()
    {
        var mapper = new DeepFlatteningMapper();

        var company = new Company
        {
            Name = "Widgets Inc",
            Headquarters = new CompanyAddress
            {
                Street = "456 Oak Ave",
                City = "London",
                Country = "UK",
            },
            PrimaryContact = new CompanyContact { Name = "Jane CTO", Email = "cto@widgets.co.uk" },
        };

        var dto = mapper.Map(company);

        // All flattened fields should come from their respective nested objects
        dto.HeadquartersCity.Should().NotBe(dto.PrimaryContactName);
        dto.HeadquartersCountry.Should().Be("UK");
        dto.PrimaryContactEmail.Should().Contain("widgets");
    }
}
