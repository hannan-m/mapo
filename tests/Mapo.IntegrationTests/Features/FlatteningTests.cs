using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

public class NestedAddress
{
    public string City { get; set; } = "";
    public string Zip { get; set; } = "";
}

public class CustomerProfile
{
    public string Name { get; set; } = "";
    public NestedAddress Address { get; set; } = new();
}

public class FlatCustomerDto
{
    public string Name { get; set; } = "";
    public string AddressCity { get; set; } = "";
    public string AddressZip { get; set; } = "";
}

[Mapper]
public partial class FlatteningMapper
{
    public partial FlatCustomerDto Map(CustomerProfile profile);
}

public class FlatteningTests
{
    [Fact]
    public void Map_ShouldFlattenNestedProperties()
    {
        var mapper = new FlatteningMapper();

        var profile = new CustomerProfile
        {
            Name = "John Doe",
            Address = new NestedAddress { City = "New York", Zip = "10001" },
        };

        var dto = mapper.Map(profile);

        dto.Name.Should().Be("John Doe");
        dto.AddressCity.Should().Be("New York");
        dto.AddressZip.Should().Be("10001");
    }
}
