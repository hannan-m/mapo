# Mapo v1.0.0 Source Generator -- Issue Analysis

This document catalogs concrete bugs and limitations encountered while integrating Mapo v1.0.0 into a real-world OCPI (Open Charge Point Interface) mapping project. All examples use actual types and data from the project.

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Input JSON Payload](#input-json-payload)
3. [DTO Definitions (Source Types)](#dto-definitions-source-types)
4. [Domain Definitions (Target Types)](#domain-definitions-target-types)
5. [The Mapper](#the-mapper)
6. [Generated Code](#generated-code)
7. [Bug 1: `new Type?()` -- Invalid C# for Nullable Reference Types](#bug-1-new-type----invalid-c-for-nullable-reference-types)
8. [Bug 2: Cannot Map `List<string>` to Collection of Enums](#bug-2-cannot-map-liststring-to-collection-of-enums)
9. [Bug 3: Inlined Lambda Expressions Lack Namespace Imports](#bug-3-inlined-lambda-expressions-lack-namespace-imports)
10. [Bug 4: `AddConverter` Does Not Match Nullable Source Types](#bug-4-addconverter-does-not-match-nullable-source-types)
11. [Bug 5: Nullable Warnings -- `string?` Passed to Non-Nullable Parameters](#bug-5-nullable-warnings----string-passed-to-non-nullable-parameters)
12. [Bug 6: Null Collections Throw Instead of Returning Empty](#bug-6-null-collections-throw-instead-of-returning-empty)
13. [Bug 7: Spurious Circular Reference Warning (MAPO010)](#bug-7-spurious-circular-reference-warning-mapo010)
14. [Cumulative Impact](#cumulative-impact)
15. [Conclusion](#conclusion)

---

## Project Overview

- **Framework:** .NET 10, C# with nullable reference types enabled
- **Serialization:** `System.Text.Json`
- **Source types (DTOs):** Immutable `record` types with `[JsonPropertyName]` attributes
- **Target types (Domain):** `sealed class` entities with `init` properties, `sealed record` value objects, enums with SCREAMING_SNAKE_CASE members
- **Mapping surface:** `LocationDto` -> `Location` with ~25 properties, including nested objects (`BusinessDetails`, `GeoCoordinates`, `OpeningTimes`), collections (`List<EvseDto>` -> `IReadOnlyList<Evse>`), enum conversions (`string` -> `enum`), and type conversions (`string` -> `Guid`).

### Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    </PropertyGroup>
    <ItemGroup>
      <PackageReference Include="Mapo" Version="1.0.0" />
    </ItemGroup>
</Project>
```

---

## Input JSON Payload

This is the real OCPI location payload used throughout the project for testing:

```json
{
  "id": "018b9f48-b768-4b6b-a97c-d473f10548d3",
  "type": "ON_STREET",
  "parking_type": "ON_STREET",
  "parking_places": [],
  "publish": true,
  "access_type": "PUBLIC",
  "name": "Lewes Station",
  "address": "Pinwell Road",
  "city": "Lewes",
  "postal_code": "BN7 2UP",
  "country": "GBR",
  "coordinates": {
    "latitude": "50.871085",
    "longitude": "0.011912"
  },
  "evses": [
    {
      "uid": "GB-APC-E91751I32W4028538-1",
      "evse_id": "GB*APC*E91751I32W4028538*1",
      "status": "UNKNOWN",
      "status_schedule": [],
      "capabilities": ["REMOTE_START_STOP_CAPABLE"],
      "connectors": [
        {
          "id": "1",
          "standard": "IEC_62196_T2",
          "format": "SOCKET",
          "power_type": "AC_1_PHASE",
          "voltage": 230,
          "amperage": 32,
          "max_electric_power": 7400,
          "tariff_id": "15549c89b14c4523b81da02fae1f62b5",
          "last_updated": "2026-03-04T22:45:22Z",
          "reimbursement_tariff_id": ""
        }
      ],
      "coordinates": { "latitude": "50.871085", "longitude": "0.011912" },
      "directions": [],
      "parking_restrictions": [],
      "parking": [],
      "images": [],
      "last_updated": "2026-03-04T22:45:22Z",
      "evse_sequence_number": 1
    },
    {
      "uid": "GB-APC-E91751I32W4028538-2",
      "evse_id": "GB*APC*E91751I32W4028538*2",
      "status": "UNKNOWN",
      "status_schedule": [],
      "capabilities": ["REMOTE_START_STOP_CAPABLE"],
      "connectors": [
        {
          "id": "2",
          "standard": "IEC_62196_T2",
          "format": "SOCKET",
          "power_type": "AC_1_PHASE",
          "voltage": 230,
          "amperage": 32,
          "max_electric_power": 7400,
          "tariff_id": "15549c89b14c4523b81da02fae1f62b5",
          "last_updated": "2026-03-04T22:41:04Z",
          "reimbursement_tariff_id": ""
        }
      ],
      "coordinates": { "latitude": "50.871085", "longitude": "0.011912" },
      "directions": [],
      "parking_restrictions": [],
      "parking": [],
      "images": [],
      "last_updated": "2026-03-04T22:41:04Z",
      "evse_sequence_number": 2
    }
  ],
  "directions": [],
  "operator": {
    "name": "APCOA United Kingdom",
    "website": "https://www.apcoa.co.uk/ev-charging/",
    "logo": {
      "image_id": "00000000-0000-0000-0000-000000000000",
      "url": "https://www.apcoa.nl/typo3conf/ext/fm_customer/Resources/Public/images/logos/apc_logo.png",
      "thumbnail": "https://www.apcoa.nl/typo3conf/ext/fm_customer/Resources/Public/images/logos/apc_logo.png",
      "category": "OPERATOR",
      "type": "png"
    },
    "phone_number": "+44800 068 8388"
  },
  "suboperator": {
    "name": "APCOA United Kingdom",
    "website": "https://www.apcoa.co.uk/ev-charging/",
    "logo": {
      "image_id": "00000000-0000-0000-0000-000000000000",
      "url": "https://www.apcoa.nl/typo3conf/ext/fm_customer/Resources/Public/images/logos/apc_logo.png",
      "thumbnail": "https://www.apcoa.nl/typo3conf/ext/fm_customer/Resources/Public/images/logos/apc_logo.png",
      "category": "OPERATOR",
      "type": "png"
    },
    "phone_number": "+44800 068 8388"
  },
  "auth_rules": [],
  "owner": {
    "name": "APCOA United Kingdom",
    "website": "https://www.apcoa.co.uk/ev-charging/",
    "logo": {
      "image_id": "00000000-0000-0000-0000-000000000000",
      "url": "https://www.apcoa.nl/typo3conf/ext/fm_customer/Resources/Public/images/logos/apc_logo.png",
      "thumbnail": "https://www.apcoa.nl/typo3conf/ext/fm_customer/Resources/Public/images/logos/apc_logo.png",
      "category": "OPERATOR",
      "type": "png"
    },
    "phone_number": "+44800 068 8388"
  },
  "facilities": ["TRAIN_STATION"],
  "time_zone": "GMT Standard Time",
  "opening_times": {
    "regular_hours": [],
    "twentyfourseven": true,
    "exceptional_openings": [],
    "exceptional_closings": []
  },
  "images": [],
  "custom_groups": [{ "name": "apcoa-gtr" }],
  "cpo_id": "APCOA-GB",
  "etag": "03172265-739d-43ec-ac96-e05f1a6c6ec5",
  "created_by": "thomas.mason@apcoa.com",
  "modified_by": "dmitri.yesayan@greenflux.com",
  "customised_fields": [],
  "created": "2024-07-12T13:56:39Z",
  "modified": "2024-09-24T13:13:59Z",
  "last_updated": "2026-03-04T22:45:22Z"
}
```

---

## DTO Definitions (Source Types)

### LocationDto

```csharp
using System.Text.Json.Serialization;

namespace ConsoleApp1.Dtos;

public sealed record LocationDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("parking_type")] string? ParkingType,
    [property: JsonPropertyName("parking_places")] List<object>? ParkingPlaces,
    [property: JsonPropertyName("publish")] bool Publish,
    [property: JsonPropertyName("access_type")] string? AccessType,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("city")] string? City,
    [property: JsonPropertyName("postal_code")] string? PostalCode,
    [property: JsonPropertyName("country")] string? Country,
    [property: JsonPropertyName("coordinates")] CoordinatesDto? Coordinates,
    [property: JsonPropertyName("evses")] List<EvseDto>? Evses,
    [property: JsonPropertyName("directions")] List<object>? Directions,
    [property: JsonPropertyName("operator")] BusinessDetailsDto? Operator,
    [property: JsonPropertyName("suboperator")] BusinessDetailsDto? Suboperator,
    [property: JsonPropertyName("auth_rules")] List<object>? AuthRules,
    [property: JsonPropertyName("owner")] BusinessDetailsDto? Owner,
    [property: JsonPropertyName("facilities")] List<string>? Facilities,
    [property: JsonPropertyName("time_zone")] string? TimeZone,
    [property: JsonPropertyName("opening_times")] OpeningTimesDto? OpeningTimes,
    [property: JsonPropertyName("images")] List<ImageDto>? Images,
    [property: JsonPropertyName("custom_groups")] List<CustomGroupDto>? CustomGroups,
    [property: JsonPropertyName("cpo_id")] string? CpoId,
    [property: JsonPropertyName("etag")] string? Etag,
    [property: JsonPropertyName("created_by")] string? CreatedBy,
    [property: JsonPropertyName("modified_by")] string? ModifiedBy,
    [property: JsonPropertyName("customised_fields")] List<object>? CustomisedFields,
    [property: JsonPropertyName("created")] DateTime? Created,
    [property: JsonPropertyName("modified")] DateTime? Modified,
    [property: JsonPropertyName("last_updated")] DateTime? LastUpdated
);
```

### EvseDto

```csharp
using System.Text.Json.Serialization;

namespace ConsoleApp1.Dtos;

public sealed record EvseDto(
    [property: JsonPropertyName("uid")] string Uid,
    [property: JsonPropertyName("evse_id")] string? EvseId,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("status_schedule")] List<object>? StatusSchedule,
    [property: JsonPropertyName("capabilities")] List<string>? Capabilities,
    [property: JsonPropertyName("connectors")] List<ConnectorDto>? Connectors,
    [property: JsonPropertyName("coordinates")] CoordinatesDto? Coordinates,
    [property: JsonPropertyName("directions")] List<object>? Directions,
    [property: JsonPropertyName("parking_restrictions")] List<string>? ParkingRestrictions,
    [property: JsonPropertyName("parking")] List<object>? Parking,
    [property: JsonPropertyName("images")] List<ImageDto>? Images,
    [property: JsonPropertyName("last_updated")] DateTime? LastUpdated,
    [property: JsonPropertyName("evse_sequence_number")] int? EvseSequenceNumber
);
```

### ConnectorDto

```csharp
using System.Text.Json.Serialization;

namespace ConsoleApp1.Dtos;

public sealed record ConnectorDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("standard")] string? Standard,
    [property: JsonPropertyName("format")] string? Format,
    [property: JsonPropertyName("power_type")] string? PowerType,
    [property: JsonPropertyName("voltage")] int Voltage,
    [property: JsonPropertyName("amperage")] int Amperage,
    [property: JsonPropertyName("max_electric_power")] int? MaxElectricPower,
    [property: JsonPropertyName("tariff_id")] string? TariffId,
    [property: JsonPropertyName("last_updated")] DateTime? LastUpdated,
    [property: JsonPropertyName("reimbursement_tariff_id")] string? ReimbursementTariffId
);
```

### CoordinatesDto

```csharp
using System.Text.Json.Serialization;

namespace ConsoleApp1.Dtos;

public sealed record CoordinatesDto(
    [property: JsonPropertyName("latitude")] string Latitude,
    [property: JsonPropertyName("longitude")] string Longitude
);
```

### BusinessDetailsDto

```csharp
using System.Text.Json.Serialization;

namespace ConsoleApp1.Dtos;

public sealed record BusinessDetailsDto(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("website")] string? Website,
    [property: JsonPropertyName("logo")] ImageDto? Logo,
    [property: JsonPropertyName("phone_number")] string? PhoneNumber
);
```

### ImageDto

```csharp
using System.Text.Json.Serialization;

namespace ConsoleApp1.Dtos;

public sealed record ImageDto(
    [property: JsonPropertyName("image_id")] string? ImageId,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("thumbnail")] string? Thumbnail,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("type")] string? Type
);
```

### OpeningTimesDto, RegularHoursDto, ExceptionalPeriodDto

```csharp
using System.Text.Json.Serialization;

namespace ConsoleApp1.Dtos;

public sealed record RegularHoursDto(
    [property: JsonPropertyName("weekday")] int Weekday,
    [property: JsonPropertyName("period_begin")] string? PeriodBegin,
    [property: JsonPropertyName("period_end")] string? PeriodEnd
);

public sealed record ExceptionalPeriodDto(
    [property: JsonPropertyName("period_begin")] DateTime? PeriodBegin,
    [property: JsonPropertyName("period_end")] DateTime? PeriodEnd
);

public sealed record OpeningTimesDto(
    [property: JsonPropertyName("regular_hours")] List<RegularHoursDto>? RegularHours,
    [property: JsonPropertyName("twentyfourseven")] bool TwentyFourSeven,
    [property: JsonPropertyName("exceptional_openings")] List<ExceptionalPeriodDto>? ExceptionalOpenings,
    [property: JsonPropertyName("exceptional_closings")] List<ExceptionalPeriodDto>? ExceptionalClosings
);
```

### CustomGroupDto

```csharp
using System.Text.Json.Serialization;

namespace ConsoleApp1.Dtos;

public sealed record CustomGroupDto(
    [property: JsonPropertyName("name")] string Name
);
```

---

## Domain Definitions (Target Types)

### Enums

```csharp
namespace ConsoleApp1.Domain.Enums;

public enum LocationType
{
    ON_STREET, PARKING_GARAGE, UNDERGROUND_GARAGE, PARKING_LOT, OTHER, UNKNOWN
}

public enum AccessType
{
    PUBLIC, PRIVATE, SEMI_PUBLIC, UNKNOWN
}

public enum EvseStatus
{
    AVAILABLE, BLOCKED, CHARGING, INOPERATIVE, OUTOFORDER, PLANNED, REMOVED, RESERVED, UNKNOWN
}

public enum Capability
{
    CHARGING_PROFILE_CAPABLE, CREDIT_CARD_PAYABLE, DEBIT_CARD_PAYABLE,
    REMOTE_START_STOP_CAPABLE, RESERVABLE, RFID_READER, UNLOCK_CAPABLE, UNKNOWN
}

public enum ConnectorStandard
{
    CHADEMO, DOMESTIC_A, DOMESTIC_B, DOMESTIC_C, DOMESTIC_D, DOMESTIC_E, DOMESTIC_F,
    DOMESTIC_G, DOMESTIC_H, DOMESTIC_I, DOMESTIC_J, DOMESTIC_K, DOMESTIC_L,
    IEC_60309_2_single_16, IEC_60309_2_three_16, IEC_60309_2_three_32, IEC_60309_2_three_64,
    IEC_62196_T1, IEC_62196_T1_COMBO, IEC_62196_T2, IEC_62196_T2_COMBO,
    IEC_62196_T3A, IEC_62196_T3C, TESLA_R, TESLA_S, UNKNOWN
}

public enum ConnectorFormat
{
    SOCKET, CABLE, UNKNOWN
}

public enum PowerType
{
    AC_1_PHASE, AC_3_PHASE, DC, UNKNOWN
}

public enum Facility
{
    HOTEL, RESTAURANT, CAFE, MALL, SUPERMARKET, SPORT, RECREATION_AREA, NATURE,
    MUSEUM, BUS_STOP, TAXI_STAND, TRAIN_STATION, AIRPORT, CARPOOL_PARKING,
    FUEL_STATION, WIFI, UNKNOWN
}

public enum ImageCategory
{
    CHARGER, ENTRANCE, LOCATION, NETWORK, OPERATOR, OTHER, OWNER, UNKNOWN
}
```

> **Note:** Enum members use SCREAMING_SNAKE_CASE to match OCPI API strings, enabling Mapo's built-in `Enum.Parse` conversion.

### Value Objects

#### GeoCoordinates

```csharp
namespace ConsoleApp1.Domain.ValueObjects;

public sealed record GeoCoordinates
{
    public required decimal Latitude { get; init; }
    public required decimal Longitude { get; init; }
}
```

### Entities

#### Location

```csharp
using ConsoleApp1.Domain.Enums;
using ConsoleApp1.Domain.ValueObjects;

namespace ConsoleApp1.Domain.Entities;

public sealed class Location
{
    public required Guid Id { get; init; }
    public LocationType Type { get; init; }
    public LocationType ParkingType { get; init; }
    public bool Publish { get; init; }
    public AccessType AccessType { get; init; }
    public required string Name { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public GeoCoordinates? Coordinates { get; init; }
    public IReadOnlyList<Evse> Evses { get; init; } = [];
    public BusinessDetails? Operator { get; init; }
    public BusinessDetails? Suboperator { get; init; }
    public BusinessDetails? Owner { get; init; }
    public IReadOnlyList<Facility> Facilities { get; init; } = [];
    public string? TimeZone { get; init; }
    public OpeningTimes? OpeningTimes { get; init; }
    public IReadOnlyList<Image> Images { get; init; } = [];
    public IReadOnlyList<string> CustomGroups { get; init; } = [];
    public string? CpoId { get; init; }
    public string? Etag { get; init; }
    public string? CreatedBy { get; init; }
    public string? ModifiedBy { get; init; }
    public DateTime? Created { get; init; }
    public DateTime? Modified { get; init; }
    public DateTime? LastUpdated { get; init; }
}
```

#### Evse

```csharp
using ConsoleApp1.Domain.Enums;
using ConsoleApp1.Domain.ValueObjects;

namespace ConsoleApp1.Domain.Entities;

public sealed class Evse
{
    public required string Uid { get; init; }
    public string? EvseId { get; init; }
    public EvseStatus Status { get; init; }
    public IReadOnlyList<Capability> Capabilities { get; init; } = [];
    public IReadOnlyList<Connector> Connectors { get; init; } = [];
    public GeoCoordinates? Coordinates { get; init; }
    public IReadOnlyList<Image> Images { get; init; } = [];
    public DateTime? LastUpdated { get; init; }
    public int? EvseSequenceNumber { get; init; }
}
```

#### Connector

```csharp
using ConsoleApp1.Domain.Enums;

namespace ConsoleApp1.Domain.Entities;

public sealed class Connector
{
    public required string Id { get; init; }
    public ConnectorStandard Standard { get; init; }
    public ConnectorFormat Format { get; init; }
    public PowerType PowerType { get; init; }
    public int Voltage { get; init; }
    public int Amperage { get; init; }
    public int? MaxElectricPower { get; init; }
    public string? TariffId { get; init; }
    public string? ReimbursementTariffId { get; init; }
    public DateTime? LastUpdated { get; init; }
}
```

#### Image

```csharp
using ConsoleApp1.Domain.Enums;

namespace ConsoleApp1.Domain.Entities;

public sealed class Image
{
    public Guid ImageId { get; init; }
    public string? Url { get; init; }
    public string? Thumbnail { get; init; }
    public ImageCategory Category { get; init; }
    public string Type { get; init; } = string.Empty;
}
```

> **Note:** `Url` and `Thumbnail` were originally `Uri?` but had to be changed to `string?` because Mapo generated `new Uri?()` -- the same nullable reference type constructor bug as Bug 1. `Website` on `BusinessDetails` was similarly changed from `Uri?` to `string?`.

#### BusinessDetails

```csharp
namespace ConsoleApp1.Domain.Entities;

public sealed class BusinessDetails
{
    public required string Name { get; init; }
    public string? Website { get; init; }
    public Image? Logo { get; init; }
    public string? PhoneNumber { get; init; }
}
```

#### OpeningTimes, RegularHours, ExceptionalPeriod

```csharp
namespace ConsoleApp1.Domain.Entities;

public sealed record RegularHours(int Weekday, string PeriodBegin, string PeriodEnd);

public sealed record ExceptionalPeriod(DateTime PeriodBegin, DateTime? PeriodEnd);

public sealed class OpeningTimes
{
    public IReadOnlyList<RegularHours> RegularHours { get; init; } = [];
    public bool TwentyFourSeven { get; init; }
    public IReadOnlyList<ExceptionalPeriod> ExceptionalOpenings { get; init; } = [];
    public IReadOnlyList<ExceptionalPeriod> ExceptionalClosings { get; init; } = [];
}
```

---

## The Mapper

This is the final mapper after all workarounds were applied. It compiles and produces correct output:

```csharp
using System.Globalization;
using ConsoleApp1.Domain.Entities;
using ConsoleApp1.Domain.Enums;
using ConsoleApp1.Domain.ValueObjects;
using ConsoleApp1.Dtos;
using Mapo.Attributes;

namespace ConsoleApp1.Mapping;

[Mapper]
public partial class LocationMapper
{
    public partial Location Map(LocationDto source);

    static void Configure(IMapConfig<LocationDto, Location> config)
    {
        config
            .AddConverter<string, Guid>(s => Guid.Parse(s))
            .Map(d => d.Coordinates, s => MapCoordinates(s.Coordinates))
            .Map(d => d.Operator, s => MapBusinessDetails(s.Operator))
            .Map(d => d.Suboperator, s => MapBusinessDetails(s.Suboperator))
            .Map(d => d.Owner, s => MapBusinessDetails(s.Owner))
            .Map(d => d.Facilities, s => MapFacilities(s.Facilities))
            .Map(d => d.OpeningTimes, s => MapOpeningTimes(s.OpeningTimes))
            .Map(d => d.CustomGroups, s => MapCustomGroups(s.CustomGroups));
    }

    static void Configure(IMapConfig<EvseDto, Evse> config)
    {
        config
            .Map(d => d.Capabilities, s => MapCapabilities(s.Capabilities))
            .Map(d => d.Coordinates, s => MapCoordinates(s.Coordinates));
    }

    static void Configure(IMapConfig<ImageDto, Image> config)
    {
        config.Map(d => d.ImageId, s => ParseGuidSafe(s.ImageId));
    }

    private static GeoCoordinates? MapCoordinates(CoordinatesDto? dto) =>
        dto != null ? new GeoCoordinates { Latitude = decimal.Parse(dto.Latitude, CultureInfo.InvariantCulture), Longitude = decimal.Parse(dto.Longitude, CultureInfo.InvariantCulture) } : null;

    private static BusinessDetails? MapBusinessDetails(BusinessDetailsDto? dto) =>
        dto != null ? new BusinessDetails { Name = dto.Name ?? "", Website = dto.Website, PhoneNumber = dto.PhoneNumber, Logo = dto.Logo != null ? MapImage(dto.Logo) : null } : null;

    private static Image MapImage(ImageDto dto) =>
        new() { ImageId = ParseGuidSafe(dto.ImageId), Url = dto.Url, Thumbnail = dto.Thumbnail, Category = Enum.Parse<ImageCategory>(dto.Category ?? nameof(ImageCategory.UNKNOWN)), Type = dto.Type ?? "" };

    private static OpeningTimes? MapOpeningTimes(OpeningTimesDto? dto) =>
        dto != null ? new OpeningTimes { TwentyFourSeven = dto.TwentyFourSeven, RegularHours = dto.RegularHours?.Select(r => new RegularHours(r.Weekday, r.PeriodBegin ?? "", r.PeriodEnd ?? "")).ToList() ?? [], ExceptionalOpenings = dto.ExceptionalOpenings?.Where(e => e.PeriodBegin.HasValue).Select(e => new ExceptionalPeriod(e.PeriodBegin!.Value, e.PeriodEnd)).ToList() ?? [], ExceptionalClosings = dto.ExceptionalClosings?.Where(e => e.PeriodBegin.HasValue).Select(e => new ExceptionalPeriod(e.PeriodBegin!.Value, e.PeriodEnd)).ToList() ?? [] } : null;

    private static List<Facility> MapFacilities(List<string>? facilities) =>
        facilities?.Select(f => Enum.Parse<Facility>(f)).ToList() ?? [];

    private static List<Capability> MapCapabilities(List<string>? capabilities) =>
        capabilities?.Select(c => Enum.Parse<Capability>(c)).ToList() ?? [];

    private static List<string> MapCustomGroups(List<CustomGroupDto>? groups) =>
        groups?.Select(g => g.Name).ToList() ?? [];

    private static Guid ParseGuidSafe(string? value) =>
        Guid.TryParse(value, out var guid) ? guid : Guid.Empty;
}
```

### What Configure does vs. what the manual helpers do

- **3 Configure methods** (11 `Map()` overrides + 1 `AddConverter`) -- tell Mapo to skip automatic generation for specific properties
- **8 manual helper methods** -- actually perform the mapping that Mapo couldn't handle

---

## Generated Code

This is the full source-generated file emitted by Mapo (after all workarounds are applied). Inspected via `EmitCompilerGeneratedFiles`:

**Path:** `obj/Debug/net10.0/generated/Mapo.Generator/Mapo.Generator.MapoGenerator/LocationMapper.g.cs`

```csharp
// <auto-generated />
#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Mapo.Attributes;
namespace ConsoleApp1.Mapping;

[GeneratedCode("Mapo.Generator", "1.0.0")]
partial class LocationMapper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public partial ConsoleApp1.Domain.Entities.Location Map(ConsoleApp1.Dtos.LocationDto source)
    {
        return MapInternal(source);
    }
    private ConsoleApp1.Domain.Entities.Location MapInternal(ConsoleApp1.Dtos.LocationDto source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var target = new ConsoleApp1.Domain.Entities.Location()
        {
            // Converter applied
            Id = Guid.Parse((source.Id)),
            // Enum conversion
            Type = System.Enum.Parse<ConsoleApp1.Domain.Enums.LocationType>(source.Type),
            // Enum conversion
            ParkingType = System.Enum.Parse<ConsoleApp1.Domain.Enums.LocationType>(source.ParkingType),
            // source.Publish
            Publish = source.Publish,
            // Enum conversion
            AccessType = System.Enum.Parse<ConsoleApp1.Domain.Enums.AccessType>(source.AccessType),
            // source.Name
            Name = source.Name,
            // source.Address
            Address = source.Address,
            // source.City
            City = source.City,
            // source.PostalCode
            PostalCode = source.PostalCode,
            // source.Country
            Country = source.Country,
            // Custom mapping
            Coordinates = MapCoordinates(source.Coordinates),
            // Collection mapping
            Evses = this.MapListEvseDtoToIReadOnlyListEvse(source.Evses),
            // Custom mapping
            Operator = MapBusinessDetails(source.Operator),
            // Custom mapping
            Suboperator = MapBusinessDetails(source.Suboperator),
            // Custom mapping
            Owner = MapBusinessDetails(source.Owner),
            // Custom mapping
            Facilities = MapFacilities(source.Facilities),
            // source.TimeZone
            TimeZone = source.TimeZone,
            // Custom mapping
            OpeningTimes = MapOpeningTimes(source.OpeningTimes),
            // Collection mapping
            Images = this.MapListImageDtoToIReadOnlyListImage(source.Images),
            // Custom mapping
            CustomGroups = MapCustomGroups(source.CustomGroups),
            // source.CpoId
            CpoId = source.CpoId,
            // source.Etag
            Etag = source.Etag,
            // source.CreatedBy
            CreatedBy = source.CreatedBy,
            // source.ModifiedBy
            ModifiedBy = source.ModifiedBy,
            // source.Created
            Created = source.Created,
            // source.Modified
            Modified = source.Modified,
            // source.LastUpdated
            LastUpdated = source.LastUpdated,
        };
        return target;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal System.Collections.Generic.IReadOnlyList<ConsoleApp1.Domain.Entities.Evse> MapListEvseDtoToIReadOnlyListEvse(System.Collections.Generic.List<ConsoleApp1.Dtos.EvseDto>? src)
    {
        return MapListEvseDtoToIReadOnlyListEvseInternal(src);
    }
    private System.Collections.Generic.IReadOnlyList<ConsoleApp1.Domain.Entities.Evse> MapListEvseDtoToIReadOnlyListEvseInternal(System.Collections.Generic.List<ConsoleApp1.Dtos.EvseDto>? src)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        var list = new List<ConsoleApp1.Domain.Entities.Evse>(src.Count);
        for (int i = 0; i < src.Count; i++)
        {
            var item = src[i];
            list.Add(this.MapEvseDtoToEvse(item));
        }
        return list;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal ConsoleApp1.Domain.Entities.Evse MapEvseDtoToEvse(ConsoleApp1.Dtos.EvseDto src)
    {
        return MapEvseDtoToEvseInternal(src);
    }
    private ConsoleApp1.Domain.Entities.Evse MapEvseDtoToEvseInternal(ConsoleApp1.Dtos.EvseDto src)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        var target = new ConsoleApp1.Domain.Entities.Evse()
        {
            // src.Uid
            Uid = src.Uid,
            // src.EvseId
            EvseId = src.EvseId,
            // Enum conversion
            Status = System.Enum.Parse<ConsoleApp1.Domain.Enums.EvseStatus>(src.Status),
            // Custom mapping
            Capabilities = MapCapabilities(src.Capabilities),
            // Collection mapping
            Connectors = this.MapListConnectorDtoToIReadOnlyListConnector(src.Connectors),
            // Custom mapping
            Coordinates = MapCoordinates(src.Coordinates),
            // Collection mapping
            Images = this.MapListImageDtoToIReadOnlyListImage(src.Images),
            // src.LastUpdated
            LastUpdated = src.LastUpdated,
            // src.EvseSequenceNumber
            EvseSequenceNumber = src.EvseSequenceNumber,
        };
        return target;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal System.Collections.Generic.IReadOnlyList<ConsoleApp1.Domain.Entities.Image> MapListImageDtoToIReadOnlyListImage(System.Collections.Generic.List<ConsoleApp1.Dtos.ImageDto>? src)
    {
        return MapListImageDtoToIReadOnlyListImageInternal(src);
    }
    private System.Collections.Generic.IReadOnlyList<ConsoleApp1.Domain.Entities.Image> MapListImageDtoToIReadOnlyListImageInternal(System.Collections.Generic.List<ConsoleApp1.Dtos.ImageDto>? src)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        var list = new List<ConsoleApp1.Domain.Entities.Image>(src.Count);
        for (int i = 0; i < src.Count; i++)
        {
            var item = src[i];
            list.Add(this.MapImageDtoToImage(item));
        }
        return list;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal ConsoleApp1.Domain.Entities.Image MapImageDtoToImage(ConsoleApp1.Dtos.ImageDto src)
    {
        return MapImageDtoToImageInternal(src);
    }
    private ConsoleApp1.Domain.Entities.Image MapImageDtoToImageInternal(ConsoleApp1.Dtos.ImageDto src)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        var target = new ConsoleApp1.Domain.Entities.Image()
        {
            // Custom mapping
            ImageId = ParseGuidSafe(src.ImageId),
            // src.Url
            Url = src.Url,
            // src.Thumbnail
            Thumbnail = src.Thumbnail,
            // Enum conversion
            Category = System.Enum.Parse<ConsoleApp1.Domain.Enums.ImageCategory>(src.Category),
            // src.Type
            Type = src.Type,
        };
        return target;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal System.Collections.Generic.IReadOnlyList<ConsoleApp1.Domain.Entities.Connector> MapListConnectorDtoToIReadOnlyListConnector(System.Collections.Generic.List<ConsoleApp1.Dtos.ConnectorDto>? src)
    {
        return MapListConnectorDtoToIReadOnlyListConnectorInternal(src);
    }
    private System.Collections.Generic.IReadOnlyList<ConsoleApp1.Domain.Entities.Connector> MapListConnectorDtoToIReadOnlyListConnectorInternal(System.Collections.Generic.List<ConsoleApp1.Dtos.ConnectorDto>? src)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        var list = new List<ConsoleApp1.Domain.Entities.Connector>(src.Count);
        for (int i = 0; i < src.Count; i++)
        {
            var item = src[i];
            list.Add(this.MapConnectorDtoToConnector(item));
        }
        return list;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal ConsoleApp1.Domain.Entities.Connector MapConnectorDtoToConnector(ConsoleApp1.Dtos.ConnectorDto src)
    {
        return MapConnectorDtoToConnectorInternal(src);
    }
    private ConsoleApp1.Domain.Entities.Connector MapConnectorDtoToConnectorInternal(ConsoleApp1.Dtos.ConnectorDto src)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        var target = new ConsoleApp1.Domain.Entities.Connector()
        {
            // src.Id
            Id = src.Id,
            // Enum conversion
            Standard = System.Enum.Parse<ConsoleApp1.Domain.Enums.ConnectorStandard>(src.Standard),
            // Enum conversion
            Format = System.Enum.Parse<ConsoleApp1.Domain.Enums.ConnectorFormat>(src.Format),
            // Enum conversion
            PowerType = System.Enum.Parse<ConsoleApp1.Domain.Enums.PowerType>(src.PowerType),
            // src.Voltage
            Voltage = src.Voltage,
            // src.Amperage
            Amperage = src.Amperage,
            // src.MaxElectricPower
            MaxElectricPower = src.MaxElectricPower,
            // src.TariffId
            TariffId = src.TariffId,
            // src.ReimbursementTariffId
            ReimbursementTariffId = src.ReimbursementTariffId,
            // src.LastUpdated
            LastUpdated = src.LastUpdated,
        };
        return target;
    }
}
```

---

## Bug 1: `new Type?()` -- Invalid C# for Nullable Reference Types

**Severity:** Compilation error (CS8628)
**Affected properties:** Every nullable reference-type property mapped automatically

### Problem

When the target property is a nullable reference type (e.g. `GeoCoordinates?`, `BusinessDetails?`, `OpeningTimes?`), Mapo generates object creation expressions using the nullable annotation as a constructor:

```csharp
// GENERATED (invalid C#)
var target = new ConsoleApp1.Domain.ValueObjects.GeoCoordinates?()
{
    Latitude = ...,
    Longitude = ...
};
```

In C#, the `?` annotation on reference types is a compile-time nullability hint -- it is **not** a distinct type that can be instantiated. `new GeoCoordinates?()` is valid only for value types (`Nullable<T>`), not reference types.

### Real types affected

| Source property | Source type | Target property | Target type | What Mapo generates |
|---|---|---|---|---|
| `LocationDto.Coordinates` | `CoordinatesDto?` | `Location.Coordinates` | `GeoCoordinates?` | `new GeoCoordinates?()` |
| `LocationDto.Operator` | `BusinessDetailsDto?` | `Location.Operator` | `BusinessDetails?` | `new BusinessDetails?()` |
| `LocationDto.Suboperator` | `BusinessDetailsDto?` | `Location.Suboperator` | `BusinessDetails?` | `new BusinessDetails?()` |
| `LocationDto.Owner` | `BusinessDetailsDto?` | `Location.Owner` | `BusinessDetails?` | `new BusinessDetails?()` |
| `LocationDto.OpeningTimes` | `OpeningTimesDto?` | `Location.OpeningTimes` | `OpeningTimes?` | `new OpeningTimes?()` |
| `EvseDto.Coordinates` | `CoordinatesDto?` | `Evse.Coordinates` | `GeoCoordinates?` | `new GeoCoordinates?()` |

**Compiler error:** `CS8628: Cannot use a nullable reference type in object creation`

Additionally, `Image.Url`, `Image.Thumbnail` (originally `Uri?`), and `BusinessDetails.Website` (originally `Uri?`) triggered the same pattern -- Mapo generated `new Uri?()`. These had to be **changed from `Uri?` to `string?`** in the domain model to avoid the bug entirely.

### Workaround

Override every nullable nested mapping with an explicit `Map()` call and a manual helper:

```csharp
static void Configure(IMapConfig<LocationDto, Location> config)
{
    config
        .Map(d => d.Coordinates, s => MapCoordinates(s.Coordinates))
        .Map(d => d.Operator, s => MapBusinessDetails(s.Operator))
        .Map(d => d.Suboperator, s => MapBusinessDetails(s.Suboperator))
        .Map(d => d.Owner, s => MapBusinessDetails(s.Owner))
        .Map(d => d.OpeningTimes, s => MapOpeningTimes(s.OpeningTimes));
}

private static GeoCoordinates? MapCoordinates(CoordinatesDto? dto) =>
    dto != null
        ? new GeoCoordinates
          {
              Latitude = decimal.Parse(dto.Latitude, CultureInfo.InvariantCulture),
              Longitude = decimal.Parse(dto.Longitude, CultureInfo.InvariantCulture)
          }
        : null;
```

### Impact

- 6 out of 6 nullable nested object properties required manual mapping
- 3 domain properties (`Url`, `Thumbnail`, `Website`) had their types changed from `Uri?` to `string?` to avoid the same bug
- Completely defeats the purpose of automatic nested type generation for nullable types

---

## Bug 2: Cannot Map `List<string>` to Collection of Enums

**Severity:** Compilation error (CS1061)
**Affected properties:** `Facilities`, `Capabilities`

### Problem

Mapo cannot map `List<string>` -> `IReadOnlyList<Facility>` or `List<string>` -> `IReadOnlyList<Capability>`. It attempts to generate a method like `MapstringToConsoleApp1DomainEnumsFacility` but fails to actually emit it, producing:

```
CS1061: 'LocationMapper' does not contain a definition for
'MapstringToConsoleApp1DomainEnumsFacility'
```

### Real types affected

| Source | Source type | Target | Target type |
|---|---|---|---|
| `LocationDto.Facilities` | `List<string>?` | `Location.Facilities` | `IReadOnlyList<Facility>` |
| `EvseDto.Capabilities` | `List<string>?` | `Evse.Capabilities` | `IReadOnlyList<Capability>` |

This is surprising because Mapo **does** handle scalar `string` -> `enum` via `Enum.Parse` (e.g. `source.Type` -> `LocationType`, `source.Status` -> `EvseStatus`). The bug is specifically in **collection-level** element conversion -- it can convert a single string to an enum but cannot iterate a list and convert each element.

### Concrete data

The JSON contains:
```json
"facilities": ["TRAIN_STATION"],
"capabilities": ["REMOTE_START_STOP_CAPABLE"]
```

These are `List<string>` in the DTOs but `IReadOnlyList<Facility>` and `IReadOnlyList<Capability>` in the domain.

### Workaround

```csharp
config.Map(d => d.Facilities, s => MapFacilities(s.Facilities));

private static List<Facility> MapFacilities(List<string>? facilities) =>
    facilities?.Select(f => Enum.Parse<Facility>(f)).ToList() ?? [];
```

---

## Bug 3: Inlined Lambda Expressions Lack Namespace Imports

**Severity:** Compilation error (CS0246)
**Affected:** Any `Map()` lambda referencing project-specific types

### Problem

When you provide a lambda in `Configure`, Mapo inlines the lambda body directly into the generated file. However, the generated file only contains these `using` directives:

```csharp
// Generated file header (LocationMapper.g.cs, lines 1-10)
// <auto-generated />
#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Mapo.Attributes;
```

No project-specific namespaces are included (`ConsoleApp1.Domain.Enums`, `ConsoleApp1.Dtos`, etc.).

### Concrete example

If you write:

```csharp
static void Configure(IMapConfig<LocationDto, Location> config)
{
    config.Map(d => d.Facilities,
        s => s.Facilities?.Select(f => Enum.Parse<Facility>(f)).ToList() ?? new List<Facility>());
}
```

The generated code inlines this as:

```csharp
Facilities = s.Facilities?.Select(f => Enum.Parse<Facility>(f)).ToList() ?? new List<Facility>(),
```

This produces:
```
CS0246: The type or namespace name 'Facility' could not be found
```

Because the generated file has no `using ConsoleApp1.Domain.Enums;`.

The same issue occurs with DTO types in lambdas:

```csharp
config.Map(d => d.CustomGroups,
    s => s.CustomGroups?.Select(g => g.Name).ToList() ?? new List<CustomGroupDto>());
// CS0246: 'CustomGroupDto' could not be found -- missing 'using ConsoleApp1.Dtos;'
```

### Workaround

Extract all lambda logic into helper methods defined in your own source file (which has proper `using` directives):

```csharp
config.Map(d => d.CustomGroups, s => MapCustomGroups(s.CustomGroups));

private static List<string> MapCustomGroups(List<CustomGroupDto>? groups) =>
    groups?.Select(g => g.Name).ToList() ?? [];
```

The method *call* (`MapCustomGroups(...)`) is namespace-free, so it survives inlining.

---

## Bug 4: `AddConverter` Does Not Match Nullable Source Types

**Severity:** Diagnostic warning (MAPO001), property silently unmapped
**Affected:** Any converter where source property is `T?` but converter is registered for `T`

### Problem

Registering a converter with:

```csharp
config.AddConverter<string, Guid>(s => Guid.Parse(s));
```

This converter matches `string` -> `Guid`. But when the source property is `string?` (e.g. `ImageDto.ImageId`), the converter does **not** match. Mapo emits:

```
MAPO001: Property 'ImageId' in target type 'Image' is not mapped
```

### Real types affected

| Source property | Source type | Target property | Target type | Converter registered |
|---|---|---|---|---|
| `ImageDto.ImageId` | `string?` | `Image.ImageId` | `Guid` | `AddConverter<string, Guid>` |

The converter was registered as `string -> Guid`, but `ImageDto.ImageId` is `string?` (nullable). Mapo treats `string` and `string?` as different types for converter matching purposes.

### Concrete data

The JSON contains:
```json
"image_id": "00000000-0000-0000-0000-000000000000"
```

This is `string?` in `ImageDto` and needs to become `Guid` in `Image`.

### Workaround

Use explicit `Map()` instead of relying on the converter:

```csharp
static void Configure(IMapConfig<ImageDto, Image> config)
{
    config.Map(d => d.ImageId, s => ParseGuidSafe(s.ImageId));
}

private static Guid ParseGuidSafe(string? value) =>
    Guid.TryParse(value, out var guid) ? guid : Guid.Empty;
```

---

## Bug 5: Nullable Warnings -- `string?` Passed to Non-Nullable Parameters

**Severity:** Compiler warnings (CS8604, CS8601), potential runtime `ArgumentNullException`
**Affected:** All `string?` -> `enum` conversions, all `string?` -> non-nullable `string` assignments

### Problem

Mapo generates `Enum.Parse<T>(source.Property)` for string-to-enum conversions but doesn't account for nullable source strings. The generated code passes `string?` directly to `Enum.Parse(string)` which expects non-null.

From the generated `LocationMapper.g.cs`:

```csharp
// Line 29 -- source.Type is string? but Enum.Parse expects string
Type = System.Enum.Parse<ConsoleApp1.Domain.Enums.LocationType>(source.Type),

// Line 31 -- same issue
ParkingType = System.Enum.Parse<ConsoleApp1.Domain.Enums.LocationType>(source.ParkingType),

// Line 35 -- same issue
AccessType = System.Enum.Parse<ConsoleApp1.Domain.Enums.AccessType>(source.AccessType),

// Line 37 -- string? assigned to 'required string'
Name = source.Name,
```

### All occurrences in generated code

| Line | Generated code | Source type | Target type | Warning |
|---|---|---|---|---|
| 29 | `Type = Enum.Parse<LocationType>(source.Type)` | `string?` | `LocationType` | CS8604 |
| 31 | `ParkingType = Enum.Parse<LocationType>(source.ParkingType)` | `string?` | `LocationType` | CS8604 |
| 35 | `AccessType = Enum.Parse<AccessType>(source.AccessType)` | `string?` | `AccessType` | CS8604 |
| 37 | `Name = source.Name` | `string?` | `required string` | CS8601 |
| 114 | `Status = Enum.Parse<EvseStatus>(src.Status)` | `string?` | `EvseStatus` | CS8604 |
| 163 | `Category = Enum.Parse<ImageCategory>(src.Category)` | `string?` | `ImageCategory` | CS8604 |
| 165 | `Type = src.Type` | `string?` | `string` (non-nullable) | CS8601 |
| 198 | `Standard = Enum.Parse<ConnectorStandard>(src.Standard)` | `string?` | `ConnectorStandard` | CS8604 |
| 200 | `Format = Enum.Parse<ConnectorFormat>(src.Format)` | `string?` | `ConnectorFormat` | CS8604 |
| 202 | `PowerType = Enum.Parse<PowerType>(src.PowerType)` | `string?` | `PowerType` | CS8604 |

### Concrete data

From the JSON:
```json
"type": "ON_STREET",
"status": "UNKNOWN",
"standard": "IEC_62196_T2",
"category": "OPERATOR"
```

These are all `string?` in the DTOs. If any were null, `Enum.Parse` would throw `ArgumentNullException` at runtime with no useful diagnostic.

### Expected behavior

Mapo should generate null-safe code, e.g.:

```csharp
Type = source.Type != null
    ? System.Enum.Parse<LocationType>(source.Type)
    : default,
```

Or at minimum use a `!` operator to suppress the warning if it's intentional.

---

## Bug 6: Null Collections Throw Instead of Returning Empty

**Severity:** Runtime `ArgumentNullException` on valid data
**Affected:** All generated collection mapping methods

### Problem

For nullable collection inputs (`List<T>?`), Mapo generates methods that throw `ArgumentNullException` instead of returning an empty collection:

```csharp
// Generated code (LocationMapper.g.cs, lines 88-98)
private IReadOnlyList<Evse> MapListEvseDtoToIReadOnlyListEvseInternal(
    List<EvseDto>? src)   // <-- parameter is nullable
{
    if (src == null) throw new ArgumentNullException(nameof(src));  // <-- throws on null!
    var list = new List<Evse>(src.Count);
    for (int i = 0; i < src.Count; i++)
    {
        var item = src[i];
        list.Add(this.MapEvseDtoToEvse(item));
    }
    return list;
}
```

The parameter type is `List<EvseDto>?` -- the `?` explicitly says null is a valid input. Yet the first line throws on null.

### All occurrences in generated code

| Method | Parameter type | Line | Behavior |
|---|---|---|---|
| `MapListEvseDtoToIReadOnlyListEvseInternal` | `List<EvseDto>?` | 90 | Throws on null |
| `MapListImageDtoToIReadOnlyListImageInternal` | `List<ImageDto>?` | 137 | Throws on null |
| `MapListConnectorDtoToIReadOnlyListConnectorInternal` | `List<ConnectorDto>?` | 176 | Throws on null |

### Concrete scenario

In OCPI data, `images` is often an empty array or absent entirely:

```json
"images": []
```

But if a location comes in without the `images` field at all, `System.Text.Json` deserializes it as `null`. The generated code then throws `ArgumentNullException` instead of returning an empty list. The domain type already has a sensible default:

```csharp
public IReadOnlyList<Image> Images { get; init; } = [];  // default is empty
```

### Expected behavior

```csharp
if (src == null) return new List<Evse>();  // or Array.Empty<Evse>()
```

---

## Bug 7: Spurious Circular Reference Warning (MAPO010)

**Severity:** False positive diagnostic warning
**Affected:** `EvseDto` -> `Evse` mapping

### Problem

Mapo emits a circular reference warning for `EvseDto`, but no circular reference exists in the type graph:

```
MAPO010: Circular reference detected in 'EvseDto'
```

The `EvseDto` type graph is:

```
EvseDto
  +-- List<ConnectorDto>  (ConnectorDto has no nested types)
  +-- CoordinatesDto       (two string properties)
  +-- List<ImageDto>       (ImageDto has no nested types)
```

None of these types reference `EvseDto` back. This is a false positive that adds noise to the build output.

---

## Cumulative Impact

### What Mapo handled automatically (13 of 25 properties on Location)

| Property | Source type | Target type | Generated code |
|---|---|---|---|
| `Publish` | `bool` | `bool` | `Publish = source.Publish` |
| `Address` | `string?` | `string?` | `Address = source.Address` |
| `City` | `string?` | `string?` | `City = source.City` |
| `PostalCode` | `string?` | `string?` | `PostalCode = source.PostalCode` |
| `Country` | `string?` | `string?` | `Country = source.Country` |
| `TimeZone` | `string?` | `string?` | `TimeZone = source.TimeZone` |
| `CpoId` | `string?` | `string?` | `CpoId = source.CpoId` |
| `Etag` | `string?` | `string?` | `Etag = source.Etag` |
| `CreatedBy` | `string?` | `string?` | `CreatedBy = source.CreatedBy` |
| `ModifiedBy` | `string?` | `string?` | `ModifiedBy = source.ModifiedBy` |
| `Created` | `DateTime?` | `DateTime?` | `Created = source.Created` |
| `Modified` | `DateTime?` | `DateTime?` | `Modified = source.Modified` |
| `LastUpdated` | `DateTime?` | `DateTime?` | `LastUpdated = source.LastUpdated` |

All 13 are trivial same-type property assignments that require zero conversion logic.

### What required manual workarounds (12 properties)

| Property | Source -> Target | Issue | Bug # |
|---|---|---|---|
| `Id` | `string` -> `Guid` | Converter + nullable mismatch | 4 |
| `Type` | `string?` -> `LocationType` | Nullable warning, CS8604 | 5 |
| `ParkingType` | `string?` -> `LocationType` | Nullable warning, CS8604 | 5 |
| `AccessType` | `string?` -> `AccessType` | Nullable warning, CS8604 | 5 |
| `Name` | `string?` -> `required string` | Nullable warning, CS8601 | 5 |
| `Coordinates` | `CoordinatesDto?` -> `GeoCoordinates?` | `new Type?()` | 1 |
| `Operator` | `BusinessDetailsDto?` -> `BusinessDetails?` | `new Type?()` | 1 |
| `Suboperator` | `BusinessDetailsDto?` -> `BusinessDetails?` | `new Type?()` | 1 |
| `Owner` | `BusinessDetailsDto?` -> `BusinessDetails?` | `new Type?()` | 1 |
| `OpeningTimes` | `OpeningTimesDto?` -> `OpeningTimes?` | `new Type?()` | 1 |
| `Facilities` | `List<string>?` -> `IReadOnlyList<Facility>` | Missing method, CS1061 | 2 |
| `CustomGroups` | `List<CustomGroupDto>?` -> `IReadOnlyList<string>` | Namespace leak, CS0246 | 3 |

Plus collection methods (`Evses`, `Images`, `Connectors`) that throw on null instead of returning empty (Bug 6).

### The workaround mapper vs. what it should be

**Ideal Mapo mapper (what we hoped for):**

```csharp
[Mapper]
public partial class LocationMapper
{
    public partial Location Map(LocationDto source);
}
```

**Actual Mapo mapper (after all workarounds): 63 lines**

- 3 `Configure` methods with 11 `Map()` overrides and 1 `AddConverter`
- 8 manual helper methods that do the actual conversion work

**Equivalent pure manual mapper: ~60-70 lines**

- Zero magic, zero diagnostics, zero dependency
- Full control over null handling, enum parsing, type conversion
- Every line visible and debuggable

---

## Conclusion

Mapo v1.0.0 works well for trivial same-type property copying but breaks down in real-world scenarios involving:

1. **Nullable reference types** -- Fundamental C# 8+ feature; generates invalid `new Type?()` syntax
2. **Collection element conversions** -- `List<string>` -> `List<Enum>` fails entirely
3. **Lambda inlining** -- No project namespace imports in generated code
4. **Converter nullability** -- `T` converter doesn't match `T?` sources
5. **Null safety** -- Generated code produces CS8604/CS8601 warnings and throws on valid nullable inputs
6. **False diagnostics** -- Spurious circular reference warnings

For an OCPI-style domain with nullable nested types, enum conversions from strings, and collection transformations, **Mapo added complexity rather than removing it**. The workaround-heavy mapper is functionally equivalent to a manual mapper but harder to understand, debug, and maintain due to the split between explicit configuration and invisible generated code.
