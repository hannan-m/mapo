using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

public class NullableValueSource
{
    public int? Score { get; set; }
    public DateTime? Created { get; set; }
    public Guid? TraceId { get; set; }
}

public class NonNullableValueTarget
{
    public int Score { get; set; }
    public DateTime Created { get; set; }
    public Guid TraceId { get; set; }
}

[Mapper]
public static partial class NullableCoercionMapper
{
    public static partial NonNullableValueTarget Map(NullableValueSource src);
}

public class NullableCoercionTests
{
    [Fact]
    public void NullValues_CoerceToDefault()
    {
        var src = new NullableValueSource { Score = null, Created = null, TraceId = null };
        var target = NullableCoercionMapper.Map(src);

        target.Score.Should().Be(0);
        target.Created.Should().Be(default(DateTime));
        target.TraceId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void NonNullValues_PassThrough()
    {
        var now = DateTime.UtcNow;
        var guid = Guid.NewGuid();
        var src = new NullableValueSource { Score = 42, Created = now, TraceId = guid };
        var target = NullableCoercionMapper.Map(src);

        target.Score.Should().Be(42);
        target.Created.Should().Be(now);
        target.TraceId.Should().Be(guid);
    }
}
