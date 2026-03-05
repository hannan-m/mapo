using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// --- Types for same-element-type collection tests ---

public class PrimitiveCollectionSource
{
    public List<string> Tags { get; set; } = new();
    public List<int> Scores { get; set; } = new();
    public List<string>? OptionalLabels { get; set; }
}

public class PrimitiveCollectionTarget
{
    public IReadOnlyList<string> Tags { get; set; } = new List<string>();
    public IEnumerable<int> Scores { get; set; } = new List<int>();
    public IReadOnlyList<string> OptionalLabels { get; set; } = new List<string>();
}

public enum Facility
{
    Parking,
    Restaurant,
    WiFi,
    Toilet,
}

public class FacilitySource
{
    public List<string>? Facilities { get; set; }
}

public class FacilityTarget
{
    public List<Facility> Facilities { get; set; } = new();
}

[Mapper]
public static partial class SameElementCollectionMapper
{
    public static partial PrimitiveCollectionTarget Map(PrimitiveCollectionSource src);

    public static partial FacilityTarget MapFacilities(FacilitySource src);
}

public class SameElementCollectionTests
{
    [Fact]
    public void ListString_ToIReadOnlyListString_DirectAssignment()
    {
        var result = SameElementCollectionMapper.Map(
            new PrimitiveCollectionSource
            {
                Tags = new List<string> { "a", "b", "c" },
                Scores = new List<int> { 10, 20 },
            }
        );

        result.Tags.Should().BeEquivalentTo(new[] { "a", "b", "c" });
        result.Scores.Should().BeEquivalentTo(new[] { 10, 20 });
    }

    [Fact]
    public void NullableListString_ToNonNullable_ReturnsEmptyOrDirect()
    {
        var withLabels = SameElementCollectionMapper.Map(
            new PrimitiveCollectionSource
            {
                Tags = new List<string>(),
                Scores = new List<int>(),
                OptionalLabels = new List<string> { "x", "y" },
            }
        );
        withLabels.OptionalLabels.Should().BeEquivalentTo(new[] { "x", "y" });

        var withoutLabels = SameElementCollectionMapper.Map(
            new PrimitiveCollectionSource
            {
                Tags = new List<string>(),
                Scores = new List<int>(),
                OptionalLabels = null,
            }
        );
        // Direct assignment: nullable source stays null (no empty-list wrapping for same-element-type)
        withoutLabels.OptionalLabels.Should().BeNull();
    }

    [Fact]
    public void ListString_ToListEnum_ParsesElements()
    {
        var result = SameElementCollectionMapper.MapFacilities(
            new FacilitySource
            {
                Facilities = new List<string> { "Parking", "WiFi", "Toilet" },
            }
        );

        result.Facilities.Should().HaveCount(3);
        result.Facilities.Should().Contain(Facility.Parking);
        result.Facilities.Should().Contain(Facility.WiFi);
    }

    [Fact]
    public void NullListString_ToListEnum_ReturnsEmpty()
    {
        var result = SameElementCollectionMapper.MapFacilities(new FacilitySource { Facilities = null });

        result.Facilities.Should().NotBeNull();
        result.Facilities.Should().BeEmpty();
    }
}
