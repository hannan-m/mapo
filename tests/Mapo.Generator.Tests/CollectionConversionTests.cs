using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for collection element type conversion (Bug 2: List&lt;string&gt; to List&lt;Enum&gt;).
/// </summary>
public class CollectionConversionTests : MapoVerifier
{
    [Fact]
    public void ListString_ToListEnum_GeneratesValidCode()
    {
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;
namespace Test;
public enum Facility { Parking, Wifi, Restaurant }
public class S { public List<string> Facilities { get; set; } = new(); }
public class T { public List<Facility> Facilities { get; set; } = new(); }
[Mapper]
public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("Enum.Parse");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void ListString_ToListEnum_RuntimeConversion()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;
public enum Facility { Parking, Wifi, Restaurant }
public class S { public List<string> Facilities { get; set; } = new(); }
public class T { public List<Facility> Facilities { get; set; } = new(); }
[Mapper]
public partial class M { public partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new S { Facilities = new List<string> { ""Parking"", ""Wifi"" } });
        if (result.Facilities.Count != 2) throw new Exception($""Expected 2, got {result.Facilities.Count}"");
        if (result.Facilities[0] != Facility.Parking) throw new Exception($""Expected Parking, got {result.Facilities[0]}"");
        if (result.Facilities[1] != Facility.Wifi) throw new Exception($""Expected Wifi, got {result.Facilities[1]}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void ListEnum_ToListString_GeneratesValidCode()
    {
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;
namespace Test;
public enum Status { Active, Inactive }
public class S { public List<Status> Statuses { get; set; } = new(); }
public class T { public List<string> Statuses { get; set; } = new(); }
[Mapper]
public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain(".ToString()");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void NullableListString_ToListEnum_HandlesNull()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;
public enum Facility { Parking, Wifi }
public class S { public List<string>? Facilities { get; set; } }
public class T { public List<Facility> Facilities { get; set; } = new(); }
[Mapper]
public partial class M { public partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new S { Facilities = null });
        if (result.Facilities == null) throw new Exception(""Expected non-null list"");
        if (result.Facilities.Count != 0) throw new Exception($""Expected 0, got {result.Facilities.Count}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void ListString_ToIReadOnlyListEnum_GeneratesValidCode()
    {
        // Mimics the OCPI pattern: List<string> → IReadOnlyList<Facility>
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;
namespace Test;
public enum Capability { ChargingProfileCapable, RemoteStartStopCapable }
public class S { public List<string>? Capabilities { get; set; } }
public class T { public IReadOnlyList<Capability> Capabilities { get; set; } = new List<Capability>(); }
[Mapper]
public partial class M { public partial T Map(S s); }";
        AssertGeneratedCodeCompiles(source);
    }
}
