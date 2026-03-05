using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

public enum SourceStatus { Active, Inactive, Pending }
public enum DestStatus { Active, Inactive, Pending }

public record SourceRecord(int Id, string Name, SourceStatus Status);
public record DestRecord(int Id, string Name, DestStatus Status);

[Mapper]
public partial class RecordMapper
{
    public partial DestRecord Map(SourceRecord source);
}

public class EnumAndRecordTests
{
    [Fact]
    public void Map_ShouldHandleRecordsAndEnumsCorrectly()
    {
        var mapper = new RecordMapper();
        
        var source = new SourceRecord(42, "Test Record", SourceStatus.Pending);
        var dest = mapper.Map(source);

        dest.Should().NotBeNull();
        dest.Id.Should().Be(42);
        dest.Name.Should().Be("Test Record");
        dest.Status.Should().Be(DestStatus.Pending);
    }
}
