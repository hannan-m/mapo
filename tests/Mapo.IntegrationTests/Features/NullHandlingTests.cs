using System;
using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// =============================================================================
// Null handling: ArgumentNullException for null source, null-safe navigation
// =============================================================================

public class NullableSource
{
    public string Name { get; set; } = "";
    public NullableInner? Inner { get; set; }
    public string? NullableValue { get; set; }
}

public class NullableInner
{
    public string Value { get; set; } = "";
}

public class NullableDest
{
    public string Name { get; set; } = "";
    public string? InnerValue { get; set; }
    public string? NullableValue { get; set; }
}

[Mapper]
public partial class NullHandlingMapper
{
    public partial NullableDest Map(NullableSource source);

    static void Configure(IMapConfig<NullableSource, NullableDest> config)
    {
        config.Map(d => d.InnerValue, s => s.Inner != null ? s.Inner.Value : null);
    }
}

public class NullHandlingTests
{
    [Fact]
    public void NullSource_ShouldThrowArgumentNullException()
    {
        var mapper = new NullHandlingMapper();
        var act = () => mapper.Map(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NullableProperty_WhenNull_ShouldMapToNull()
    {
        var mapper = new NullHandlingMapper();
        var source = new NullableSource
        {
            Name = "Test",
            Inner = null,
            NullableValue = null,
        };

        var dest = mapper.Map(source);

        dest.Name.Should().Be("Test");
        dest.InnerValue.Should().BeNull();
        dest.NullableValue.Should().BeNull();
    }

    [Fact]
    public void NullableProperty_WhenPopulated_ShouldMapCorrectly()
    {
        var mapper = new NullHandlingMapper();
        var source = new NullableSource
        {
            Name = "Test",
            Inner = new NullableInner { Value = "InnerVal" },
            NullableValue = "NotNull",
        };

        var dest = mapper.Map(source);

        dest.Name.Should().Be("Test");
        dest.InnerValue.Should().Be("InnerVal");
        dest.NullableValue.Should().Be("NotNull");
    }
}
