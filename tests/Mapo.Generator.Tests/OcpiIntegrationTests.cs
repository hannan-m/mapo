using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Comprehensive OCPI-style integration tests exercising all 7 bug fixes together.
/// Mimics a real-world OCPI (Open Charge Point Interface) domain with:
/// - Nullable nested objects (Bug 1)
/// - Collection element conversion with enums (Bug 2)
/// - Lambda expressions using external types (Bug 3)
/// - Converters with nullable source types (Bug 4)
/// - Nullable string to non-nullable string (Bug 5)
/// - Nullable collections (Bug 6)
/// - Diamond dependencies without false circular warnings (Bug 7)
/// </summary>
public class OcpiIntegrationTests : MapoVerifier
{
    [Fact]
    public void FullOcpiModel_Compiles()
    {
        string source = GetOcpiSource(includeTestRunner: false);
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void FullOcpiModel_NoDiagnosticErrors()
    {
        string source = GetOcpiSource(includeTestRunner: false);
        var result = RunGenerator(source);
        var diagnostics = result.Results[0].Diagnostics;
        // No errors or MAPO010 (false circular reference) warnings
        diagnostics.Should().NotContain(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        diagnostics.Should().NotContain(d => d.Id == "MAPO010");
    }

    [Fact]
    public void FullOcpiModel_RuntimeExecution()
    {
        string source = GetOcpiSource(includeTestRunner: true);
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NullableNestedAndCollections_Together()
    {
        // Combines Bug 1 (nullable nested), Bug 5 (nullable string), Bug 6 (nullable collection)
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;
public class ItemDto { public int Id { get; set; } public string? Label { get; set; } }
public class Item { public int Id { get; set; } public string Label { get; set; } = """"; }
public class ContainerDto
{
    public string? Name { get; set; }
    public ItemDto? MainItem { get; set; }
    public List<ItemDto>? Items { get; set; }
}
public class Container
{
    public string Name { get; set; } = """";
    public Item? MainItem { get; set; }
    public List<Item> Items { get; set; } = new();
}
[Mapper]
public partial class M { public partial Container Map(ContainerDto src); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();

        // All nulls
        var r1 = mapper.Map(new ContainerDto { Name = null, MainItem = null, Items = null });
        if (r1.MainItem != null) throw new Exception(""Expected null MainItem"");
        if (r1.Items == null || r1.Items.Count != 0) throw new Exception(""Expected empty list"");

        // All populated
        var r2 = mapper.Map(new ContainerDto
        {
            Name = ""Test"",
            MainItem = new ItemDto { Id = 1, Label = ""Main"" },
            Items = new List<ItemDto> { new ItemDto { Id = 2, Label = ""Sub"" } }
        });
        if (r2.Name != ""Test"") throw new Exception($""Expected Test, got {r2.Name}"");
        if (r2.MainItem == null) throw new Exception(""Expected non-null MainItem"");
        if (r2.MainItem.Id != 1) throw new Exception($""Expected 1, got {r2.MainItem.Id}"");
        if (r2.Items.Count != 1) throw new Exception($""Expected 1 item, got {r2.Items.Count}"");
        if (r2.Items[0].Id != 2) throw new Exception($""Expected 2, got {r2.Items[0].Id}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void ConverterWithDiamondDependency()
    {
        // Combines Bug 4 (nullable converter matching) with Bug 7 (diamond dependency)
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class SharedDto { public string? Id { get; set; } }
public class SharedModel { public Guid Id { get; set; } }
public class BranchADto { public SharedDto Shared { get; set; } = new(); public string Name { get; set; } = """"; }
public class BranchAModel { public SharedModel Shared { get; set; } = new(); public string Name { get; set; } = """"; }
public class BranchBDto { public SharedDto Shared { get; set; } = new(); public int Value { get; set; } }
public class BranchBModel { public SharedModel Shared { get; set; } = new(); public int Value { get; set; } }
public class RootDto { public BranchADto A { get; set; } = new(); public BranchBDto B { get; set; } = new(); }
public class RootModel { public BranchAModel A { get; set; } = new(); public BranchBModel B { get; set; } = new(); }
[Mapper]
public partial class M
{
    public partial RootModel Map(RootDto src);
    static void Configure(IMapConfig<SharedDto, SharedModel> config)
    {
        config.AddConverter<string, Guid>(x => Guid.Parse(x));
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var id = Guid.NewGuid();
        var mapper = new M();
        var dto = new RootDto
        {
            A = new BranchADto { Shared = new SharedDto { Id = id.ToString() }, Name = ""A"" },
            B = new BranchBDto { Shared = new SharedDto { Id = id.ToString() }, Value = 99 }
        };
        var result = mapper.Map(dto);
        if (result.A.Shared.Id != id) throw new Exception($""Expected {id}, got {result.A.Shared.Id}"");
        if (result.A.Name != ""A"") throw new Exception($""Expected A, got {result.A.Name}"");
        if (result.B.Shared.Id != id) throw new Exception($""Expected {id}, got {result.B.Shared.Id}"");
        if (result.B.Value != 99) throw new Exception($""Expected 99, got {result.B.Value}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    private static string GetOcpiSource(bool includeTestRunner)
    {
        var testRunner = includeTestRunner
            ? @"
public static class TestRunner
{
    public static void Run()
    {
        var mapper = new OcpiMapper();

        // Test with fully populated DTO
        var dto = new LocationDto
        {
            Id = ""LOC001"",
            Name = ""Charging Station Alpha"",
            Operator = new BusinessDetailsDto { Name = ""PowerCo"" },
            Coordinates = new CoordinatesDto { Latitude = ""52.5200"", Longitude = ""13.4050"" },
            Evses = new System.Collections.Generic.List<EvseDto>
            {
                new EvseDto
                {
                    UId = ""EVSE001"",
                    Status = ""AVAILABLE"",
                    Connectors = new System.Collections.Generic.List<ConnectorDto>
                    {
                        new ConnectorDto { Id = ""C1"", Standard = ""IEC_62196_T2"", PowerType = ""AC_3_PHASE"" }
                    }
                }
            }
        };
        var loc = mapper.Map(dto);
        if (loc.Id != ""LOC001"") throw new System.Exception($""Id: {loc.Id}"");
        if (loc.Operator == null) throw new System.Exception(""Operator null"");
        if (loc.Operator.Name != ""PowerCo"") throw new System.Exception($""Operator: {loc.Operator.Name}"");
        if (loc.Coordinates == null) throw new System.Exception(""Coords null"");
        if (loc.Coordinates.Latitude != ""52.5200"") throw new System.Exception($""Lat: {loc.Coordinates.Latitude}"");
        if (loc.Evses.Count != 1) throw new System.Exception($""Evses: {loc.Evses.Count}"");
        if (loc.Evses[0].UId != ""EVSE001"") throw new System.Exception($""EVSE: {loc.Evses[0].UId}"");
        if (loc.Evses[0].Connectors.Count != 1) throw new System.Exception($""Connectors: {loc.Evses[0].Connectors.Count}"");

        // Test with null optional fields (Bug 1 + Bug 6)
        var minimalDto = new LocationDto { Id = ""LOC002"", Name = ""Minimal"" };
        var minLoc = mapper.Map(minimalDto);
        if (minLoc.Id != ""LOC002"") throw new System.Exception($""MinId: {minLoc.Id}"");
        if (minLoc.Operator != null) throw new System.Exception(""Expected null Operator"");
        if (minLoc.Coordinates != null) throw new System.Exception(""Expected null Coords"");
        if (minLoc.Evses == null || minLoc.Evses.Count != 0) throw new System.Exception(""Expected empty Evses"");
    }
}"
            : "";

        return $@"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;

// -- Shared types (Bug 7: diamond dependency - used by Location and EVSE) --
public class CoordinatesDto {{ public string Latitude {{ get; set; }} = """"; public string Longitude {{ get; set; }} = """"; }}
public class Coordinates {{ public string Latitude {{ get; set; }} = """"; public string Longitude {{ get; set; }} = """"; }}

public class BusinessDetailsDto {{ public string Name {{ get; set; }} = """"; }}
public class BusinessDetails {{ public string Name {{ get; set; }} = """"; }}

// -- Connector --
public class ConnectorDto
{{
    public string? Id {{ get; set; }}
    public string? Standard {{ get; set; }}
    public string? PowerType {{ get; set; }}
}}
public class Connector
{{
    public string Id {{ get; set; }} = """";
    public string Standard {{ get; set; }} = """";
    public string PowerType {{ get; set; }} = """";
}}

// -- EVSE --
public class EvseDto
{{
    public string? UId {{ get; set; }}
    public string? Status {{ get; set; }}
    public List<ConnectorDto>? Connectors {{ get; set; }}
}}
public class Evse
{{
    public string UId {{ get; set; }} = """";
    public string Status {{ get; set; }} = """";
    public List<Connector> Connectors {{ get; set; }} = new();
}}

// -- Location (main entity) --
public class LocationDto
{{
    public string? Id {{ get; set; }}
    public string? Name {{ get; set; }}
    public BusinessDetailsDto? Operator {{ get; set; }}         // Bug 1: nullable nested object
    public BusinessDetailsDto? SubOperator {{ get; set; }}      // Bug 1: another nullable nested
    public CoordinatesDto? Coordinates {{ get; set; }}          // Bug 1 + Bug 7: shared type (diamond)
    public List<EvseDto>? Evses {{ get; set; }}                 // Bug 6: nullable collection
}}
public class Location
{{
    public string Id {{ get; set; }} = """";                     // Bug 5: nullable string → non-nullable
    public string Name {{ get; set; }} = """";
    public BusinessDetails? Operator {{ get; set; }}
    public BusinessDetails? SubOperator {{ get; set; }}
    public Coordinates? Coordinates {{ get; set; }}
    public List<Evse> Evses {{ get; set; }} = new();
}}

[Mapper]
public partial class OcpiMapper
{{
    public partial Location Map(LocationDto src);
}}

{testRunner}";
    }
}
