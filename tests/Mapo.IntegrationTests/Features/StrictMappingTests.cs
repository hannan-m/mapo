using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

public class ConfigSource
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SecretData { get; set; } = "";
}

public class ConfigDest
{
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string SecretData { get; set; } = "";
}

[Mapper]
public partial class ConfigMapper
{
    public partial ConfigDest Map(ConfigSource source);

    static void Configure(IMapConfig<ConfigSource, ConfigDest> config)
    {
        config.Map(d => d.DisplayName, s => s.Name).Ignore(d => d.SecretData);
    }
}

public class StrictMappingTests
{
    [Fact]
    public void Configure_ShouldApplyExplicitMappingsAndIgnores()
    {
        var mapper = new ConfigMapper();
        var source = new ConfigSource
        {
            Name = "Public Name",
            Description = "Public Description",
            SecretData = "Hidden Password",
        };

        var dest = mapper.Map(source);

        dest.DisplayName.Should().Be("Public Name");
        dest.Description.Should().Be("Public Description");

        // Ignored property shouldn't be mapped
        dest.SecretData.Should().Be("");
    }
}
