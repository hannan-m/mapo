using System;
using System.Collections.Generic;
using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// --- Types for MapFrom tests ---

public class SpotifyTrackDto
{
    public string? Type { get; set; }
    public int DurationMs { get; set; }
    public string Name { get; set; } = "";
}

public enum SpotifyItemType
{
    unknown,
    track,
    album,
}

public class Track
{
    [MapFrom("Type")]
    public SpotifyItemType ItemType { get; set; }

    [MapFrom("DurationMs")]
    public int Duration { get; set; }

    public string Name { get; set; } = "";
}

public class ApiItemDto
{
    public string Type { get; set; } = "";
    public string Class { get; set; } = "";
    public int Id { get; set; }
}

public class DomainItem
{
    public int Id { get; set; }

    [MapFrom("Type")]
    public string ItemType { get; set; } = "";

    [MapFrom("Class")]
    public string CssClass { get; set; } = "";
}

public class NestedItemDto
{
    public string Type { get; set; } = "";
    public int Value { get; set; }
}

public class NestedItem
{
    [MapFrom("Type")]
    public string Kind { get; set; } = "";

    public int Value { get; set; }
}

public class ContainerDto
{
    public List<NestedItemDto> Items { get; set; } = new();
}

public class Container
{
    public List<NestedItem> Items { get; set; } = new();
}

public class UpdateSourceDto
{
    public string Type { get; set; } = "";
    public int Score { get; set; }
}

public class UpdateTarget
{
    [MapFrom("Type")]
    public string Category { get; set; } = "";

    public int Score { get; set; }
}

[Mapper]
public partial class MapFromMapper
{
    public partial Track MapTrack(SpotifyTrackDto src);

    public partial DomainItem MapItem(ApiItemDto src);

    public partial Container MapContainer(ContainerDto src);

    public partial void ApplyUpdate(UpdateSourceDto src, UpdateTarget target);
}

public class MapFromTests
{
    [Fact]
    public void SimpleRename_MapsCorrectProperty()
    {
        var mapper = new MapFromMapper();
        var result = mapper.MapItem(
            new ApiItemDto
            {
                Type = "button",
                Class = "primary",
                Id = 7,
            }
        );

        result.ItemType.Should().Be("button");
        result.CssClass.Should().Be("primary");
        result.Id.Should().Be(7);
    }

    [Fact]
    public void WithEnumConversion_ParsesStringToEnum()
    {
        var mapper = new MapFromMapper();
        var result = mapper.MapTrack(
            new SpotifyTrackDto
            {
                Type = "track",
                DurationMs = 240000,
                Name = "Song",
            }
        );

        result.ItemType.Should().Be(SpotifyItemType.track);
        result.Duration.Should().Be(240000);
        result.Name.Should().Be("Song");
    }

    [Fact]
    public void OnAutoDiscoveredNestedType_Works()
    {
        var mapper = new MapFromMapper();
        var result = mapper.MapContainer(
            new ContainerDto
            {
                Items = new List<NestedItemDto>
                {
                    new() { Type = "A", Value = 1 },
                    new() { Type = "B", Value = 2 },
                },
            }
        );

        result.Items.Should().HaveCount(2);
        result.Items[0].Kind.Should().Be("A");
        result.Items[1].Kind.Should().Be("B");
    }

    [Fact]
    public void WithUpdateMapping_RenamesCorrectly()
    {
        var mapper = new MapFromMapper();
        var target = new UpdateTarget { Category = "old", Score = 0 };
        mapper.ApplyUpdate(new UpdateSourceDto { Type = "new", Score = 99 }, target);

        target.Category.Should().Be("new");
        target.Score.Should().Be(99);
    }
}
