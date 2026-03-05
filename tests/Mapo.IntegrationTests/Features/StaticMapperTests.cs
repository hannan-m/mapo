using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// =============================================================================
// Static mapper: generates extension methods + IAsyncEnumerable streaming
// =============================================================================

public class LogEntry
{
    public int Id { get; set; }
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "Info";
}

public class LogEntryDto
{
    public int Id { get; set; }
    public string Message { get; set; } = "";
    public string FormattedTime { get; set; } = "";
    public string Level { get; set; } = "";
}

[Mapper]
public static partial class LogMapper
{
    public static partial LogEntryDto MapLog(LogEntry entry);

    static void Configure(IMapConfig<LogEntry, LogEntryDto> config)
    {
        config.Map(d => d.FormattedTime, s => s.Timestamp.ToString("HH:mm:ss"));
    }
}

public class StaticMapperTests
{
    [Fact]
    public void StaticMapper_ShouldMapCorrectly()
    {
        var entry = new LogEntry
        {
            Id = 1,
            Message = "Server started",
            Timestamp = new DateTime(2025, 6, 15, 14, 30, 45),
            Level = "Info"
        };

        var dto = LogMapper.MapLog(entry);

        dto.Id.Should().Be(1);
        dto.Message.Should().Be("Server started");
        dto.FormattedTime.Should().Be("14:30:45");
        dto.Level.Should().Be("Info");
    }

    [Fact]
    public void ExtensionMethod_ShouldMapViaThis()
    {
        var entry = new LogEntry
        {
            Id = 2,
            Message = "Request received",
            Timestamp = new DateTime(2025, 6, 15, 9, 0, 0),
            Level = "Debug"
        };

        // Extension method generated: entry.MapLog()
        var dto = entry.MapLog();

        dto.Id.Should().Be(2);
        dto.Message.Should().Be("Request received");
        dto.FormattedTime.Should().Be("09:00:00");
    }

    [Fact]
    public async Task AsyncStreaming_ShouldMapAllItems()
    {
        async IAsyncEnumerable<LogEntry> GenerateEntries()
        {
            yield return new LogEntry { Id = 1, Message = "First", Timestamp = DateTime.Now, Level = "Info" };
            yield return new LogEntry { Id = 2, Message = "Second", Timestamp = DateTime.Now, Level = "Warn" };
            yield return new LogEntry { Id = 3, Message = "Third", Timestamp = DateTime.Now, Level = "Error" };
            await Task.CompletedTask;
        }

        var results = new List<LogEntryDto>();
        await foreach (var dto in GenerateEntries().MapLogStreamAsync())
        {
            results.Add(dto);
        }

        results.Should().HaveCount(3);
        results[0].Message.Should().Be("First");
        results[1].Message.Should().Be("Second");
        results[2].Message.Should().Be("Third");
        results[0].Level.Should().Be("Info");
        results[2].Level.Should().Be("Error");
    }

    [Fact]
    public async Task AsyncStreaming_ShouldSupportCancellation()
    {
        async IAsyncEnumerable<LogEntry> InfiniteEntries(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            int id = 0;
            while (!ct.IsCancellationRequested)
            {
                yield return new LogEntry { Id = id++, Message = $"Entry {id}", Timestamp = DateTime.Now };
                await Task.Yield();
            }
        }

        using var cts = new CancellationTokenSource();
        var results = new List<LogEntryDto>();

        try
        {
            await foreach (var dto in InfiniteEntries(cts.Token).MapLogStreamAsync(cts.Token))
            {
                results.Add(dto);
                if (results.Count >= 5)
                    cts.Cancel();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation triggers
        }

        results.Count.Should().BeGreaterThanOrEqualTo(5);
    }
}
