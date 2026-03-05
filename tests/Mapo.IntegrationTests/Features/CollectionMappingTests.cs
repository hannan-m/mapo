using System.Collections.Generic;
using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

public class BasicSource
{
    public int Value { get; set; }
}

public class BasicDest
{
    public int Value { get; set; }
}

public class CollectionSource
{
    public List<BasicSource> ArrayItems { get; set; } = [];
    public List<BasicSource> ListItems { get; set; } = [];
}

public class CollectionDest
{
    public List<BasicDest> ArrayItems { get; set; } = [];
    public List<BasicDest> ListItems { get; set; } = [];
}

[Mapper]
public partial class CollectionMapper
{
    public partial CollectionDest Map(CollectionSource source);

    public partial BasicDest MapBasic(BasicSource source);
}

public class CollectionMappingTests
{
    [Fact]
    public void Collections_ShouldMapCorrectly()
    {
        var mapper = new CollectionMapper();

        var source = new CollectionSource
        {
            ArrayItems = [new BasicSource { Value = 1 }, new BasicSource { Value = 2 }],
            ListItems = [new BasicSource { Value = 3 }, new BasicSource { Value = 4 }],
        };

        var dest = mapper.Map(source);

        dest.ArrayItems.Should().NotBeNull();
        dest.ArrayItems.Should().HaveCount(2);
        dest.ArrayItems[0].Value.Should().Be(1);
        dest.ArrayItems[1].Value.Should().Be(2);

        dest.ListItems.Should().NotBeNull();
        dest.ListItems.Should().HaveCount(2);
        dest.ListItems[0].Value.Should().Be(3);
        dest.ListItems[1].Value.Should().Be(4);
    }
}
