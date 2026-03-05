using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for positional record constructor mapping — the primary gap identified in MAPO_ANALYSIS.md.
/// </summary>
public class RecordConstructorTests : MapoVerifier
{
    [Fact]
    public void SimpleRecord_DirectMapping_Works()
    {
        // Baseline: simple record with matching types
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class SDto { public int Id { get; set; } public string Name { get; set; } = """"; }
public record T(int Id, string Name);
[Mapper]
public partial class M { public partial T Map(SDto s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new SDto { Id = 1, Name = ""Hello"" });
        if (result.Id != 1) throw new Exception($""Id: {result.Id}"");
        if (result.Name != ""Hello"") throw new Exception($""Name: {result.Name}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void Record_NullableStringToString_InConstructor()
    {
        // OCPI scenario: RegularHoursDto has string? params, RegularHours has string params
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class SDto { public int Weekday { get; set; } public string? PeriodBegin { get; set; } public string? PeriodEnd { get; set; } }
public record T(int Weekday, string PeriodBegin, string PeriodEnd);
[Mapper]
public partial class M { public partial T Map(SDto s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new SDto { Weekday = 1, PeriodBegin = ""08:00"", PeriodEnd = ""17:00"" });
        if (result.Weekday != 1) throw new Exception($""Weekday: {result.Weekday}"");
        if (result.PeriodBegin != ""08:00"") throw new Exception($""Begin: {result.PeriodBegin}"");
        if (result.PeriodEnd != ""17:00"") throw new Exception($""End: {result.PeriodEnd}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void Record_NullableValueTypeToValueType_InConstructor()
    {
        // OCPI scenario: ExceptionalPeriodDto has DateTime? → ExceptionalPeriod has DateTime
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class SDto { public DateTime? Start { get; set; } public DateTime? End { get; set; } }
public record T(DateTime Start, DateTime? End);
[Mapper]
public partial class M { public partial T Map(SDto s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var now = DateTime.UtcNow;
        var result = mapper.Map(new SDto { Start = now, End = null });
        if (result.Start != now) throw new Exception($""Start mismatch"");
        if (result.End != null) throw new Exception($""End should be null"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void Record_AsAutoDiscoveredNestedType()
    {
        // Key scenario: record is auto-discovered as a nested type, not user-declared
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;
public class ItemDto { public int Id { get; set; } public string Name { get; set; } = """"; }
public record Item(int Id, string Name);
public class ContainerDto { public List<ItemDto> Items { get; set; } = new(); }
public class Container { public List<Item> Items { get; set; } = new(); }
[Mapper]
public partial class M { public partial Container Map(ContainerDto s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var dto = new ContainerDto { Items = new List<ItemDto> { new ItemDto { Id = 1, Name = ""A"" }, new ItemDto { Id = 2, Name = ""B"" } } };
        var result = mapper.Map(dto);
        if (result.Items.Count != 2) throw new Exception($""Count: {result.Items.Count}"");
        if (result.Items[0].Id != 1) throw new Exception($""Id: {result.Items[0].Id}"");
        if (result.Items[0].Name != ""A"") throw new Exception($""Name: {result.Items[0].Name}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void Record_NullableNestedAutoDiscovered()
    {
        // Nullable nested record that needs auto-discovery
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class InnerDto { public int X { get; set; } public int Y { get; set; } }
public record Inner(int X, int Y);
public class OuterDto { public InnerDto? Child { get; set; } }
public class Outer { public Inner? Child { get; set; } }
[Mapper]
public partial class M { public partial Outer Map(OuterDto s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        // Non-null case
        var r1 = mapper.Map(new OuterDto { Child = new InnerDto { X = 3, Y = 4 } });
        if (r1.Child == null) throw new Exception(""Expected non-null Child"");
        if (r1.Child.X != 3) throw new Exception($""X: {r1.Child.X}"");
        if (r1.Child.Y != 4) throw new Exception($""Y: {r1.Child.Y}"");
        // Null case
        var r2 = mapper.Map(new OuterDto { Child = null });
        if (r2.Child != null) throw new Exception(""Expected null Child"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void Record_WithMixedConstructorAndInitProperties()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class SDto { public int Id { get; set; } public string Name { get; set; } = """"; public string? Notes { get; set; } }
public record T(int Id, string Name)
{
    public string? Notes { get; set; }
}
[Mapper]
public partial class M { public partial T Map(SDto s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new SDto { Id = 1, Name = ""Hi"", Notes = ""Some notes"" });
        if (result.Id != 1) throw new Exception($""Id: {result.Id}"");
        if (result.Name != ""Hi"") throw new Exception($""Name: {result.Name}"");
        if (result.Notes != ""Some notes"") throw new Exception($""Notes: {result.Notes}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void Record_FullOcpiScenario_RegularHours()
    {
        // Mimics the exact OCPI RegularHours scenario from MAPO_ANALYSIS.md
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;
public record RegularHoursDto(int Weekday, string? PeriodBegin, string? PeriodEnd);
public record RegularHours(int Weekday, string PeriodBegin, string PeriodEnd);
public class OpeningTimesDto
{
    public bool TwentyFourSeven { get; set; }
    public List<RegularHoursDto>? RegularHours { get; set; }
}
public class OpeningTimes
{
    public bool TwentyFourSeven { get; set; }
    public List<RegularHours> RegularHours { get; set; } = new();
}
public class LocationDto
{
    public string Name { get; set; } = """";
    public OpeningTimesDto? OpeningTimes { get; set; }
}
public class Location
{
    public string Name { get; set; } = """";
    public OpeningTimes? OpeningTimes { get; set; }
}
[Mapper]
public partial class M { public partial Location Map(LocationDto s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var dto = new LocationDto
        {
            Name = ""Station"",
            OpeningTimes = new OpeningTimesDto
            {
                TwentyFourSeven = false,
                RegularHours = new List<RegularHoursDto>
                {
                    new RegularHoursDto(1, ""08:00"", ""17:00""),
                    new RegularHoursDto(2, ""09:00"", ""18:00"")
                }
            }
        };
        var result = mapper.Map(dto);
        if (result.Name != ""Station"") throw new Exception($""Name: {result.Name}"");
        if (result.OpeningTimes == null) throw new Exception(""Expected non-null OpeningTimes"");
        if (result.OpeningTimes.TwentyFourSeven) throw new Exception(""Expected false"");
        if (result.OpeningTimes.RegularHours.Count != 2) throw new Exception($""Count: {result.OpeningTimes.RegularHours.Count}"");
        if (result.OpeningTimes.RegularHours[0].Weekday != 1) throw new Exception($""Day: {result.OpeningTimes.RegularHours[0].Weekday}"");
        if (result.OpeningTimes.RegularHours[0].PeriodBegin != ""08:00"") throw new Exception($""Begin: {result.OpeningTimes.RegularHours[0].PeriodBegin}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }
}
