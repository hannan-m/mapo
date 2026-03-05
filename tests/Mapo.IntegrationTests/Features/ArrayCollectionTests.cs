using System.Collections.Generic;
using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// =============================================================================
// Collection mapping: arrays, lists, nested collections, empty collections
// =============================================================================

public class TagSource
{
    public string Label { get; set; } = "";
    public int Priority { get; set; }
}

public class TagDest
{
    public string Label { get; set; } = "";
    public int Priority { get; set; }
}

public class ArticleSource
{
    public string Title { get; set; } = "";
    public List<TagSource> Tags { get; set; } = [];
}

public class ArticleDest
{
    public string Title { get; set; } = "";
    public List<TagDest> Tags { get; set; } = [];
}

[Mapper]
public partial class ArrayCollectionMapper
{
    public partial List<TagDest> MapTags(List<TagSource> tags);

    public partial TagDest MapTag(TagSource tag);

    public partial ArticleDest MapArticle(ArticleSource article);
}

public class ArrayCollectionTests
{
    [Fact]
    public void ListMapping_ShouldMapAllItems()
    {
        var mapper = new ArrayCollectionMapper();
        var tags = new List<TagSource>
        {
            new() { Label = "csharp", Priority = 1 },
            new() { Label = "dotnet", Priority = 2 },
            new() { Label = "roslyn", Priority = 3 },
        };

        var result = mapper.MapTags(tags);

        result.Should().HaveCount(3);
        result[0].Label.Should().Be("csharp");
        result[1].Label.Should().Be("dotnet");
        result[2].Label.Should().Be("roslyn");
        result[0].Priority.Should().Be(1);
    }

    [Fact]
    public void EmptyList_ShouldReturnEmptyList()
    {
        var mapper = new ArrayCollectionMapper();
        var result = mapper.MapTags(new List<TagSource>());
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void NullCollection_ShouldThrow()
    {
        var mapper = new ArrayCollectionMapper();
        var act = () => mapper.MapTags(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NestedCollection_InObject_ShouldMapCorrectly()
    {
        var mapper = new ArrayCollectionMapper();
        var article = new ArticleSource
        {
            Title = "Intro to Mapo",
            Tags = [new() { Label = "mapper", Priority = 1 }, new() { Label = "generator", Priority = 2 }],
        };

        var dto = mapper.MapArticle(article);

        dto.Title.Should().Be("Intro to Mapo");
        dto.Tags.Should().HaveCount(2);
        dto.Tags[0].Label.Should().Be("mapper");
        dto.Tags[1].Label.Should().Be("generator");
    }

    [Fact]
    public void SingleItemCollection_ShouldMapCorrectly()
    {
        var mapper = new ArrayCollectionMapper();
        var tags = new List<TagSource>
        {
            new() { Label = "solo", Priority = 42 },
        };

        var result = mapper.MapTags(tags);

        result.Should().ContainSingle();
        result[0].Label.Should().Be("solo");
        result[0].Priority.Should().Be(42);
    }
}
