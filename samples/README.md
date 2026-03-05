# Samples

Runnable examples demonstrating Mapo features. Each sample is a standalone console application targeting .NET 10.

---

## [Mapo.Sample](./Mapo.Sample) — Comprehensive Feature Showcase

Demonstrates all Mapo features in a single e-commerce scenario:

| # | Feature | How It's Shown |
|---|---------|----------------|
| 1 | Basic auto-matching | Properties matched by name (case-insensitive) |
| 2 | `init` + `required` properties | `CustomerDto` with `required string FullName { get; init; }` |
| 3 | Record/constructor mapping | `OrderLineDto` record with positional parameters |
| 4 | Enum-to-string (auto) | `MembershipTier.Gold` -> `"Gold"` |
| 5 | Enum-to-enum (auto) | `OrderStatus.Shipped` -> `OrderStatusLabel.Shipped` |
| 6 | String-to-enum (auto) | `"Confirmed"` -> `OrderStatus.Confirmed` |
| 7 | Nullable auto-coercion | `int? 1250` -> `int 1250`, `int? null` -> `int 0` |
| 8 | Deep flattening (2-4 levels) | `ShippingAddress.Region.Country.Name` -> `ShippingAddressRegionCountryName` |
| 9 | Custom computed mappings | `s.Lines.Sum(...)` -> `TotalAmount` |
| 10 | Dependency injection | `ICurrencyFormatter` injected via constructor, used in `Configure` |
| 11 | Global type converters | `AddConverter<DateTime, string>(dt => dt.ToString("yyyy-MM-dd"))` |
| 12 | Collection mapping | `List<OrderLine>` -> `List<OrderLineDto>` with auto-discovery |
| 13 | Batch collection mapping | `MapOrders(List<Order>)` with pre-allocated capacity |
| 14 | Static mapper | `ProductStaticMapper` with zero allocation overhead |
| 15 | Extension methods | `product.MapToDto()` auto-generated for static mappers |
| 16 | Async streaming | `IAsyncEnumerable<Product>.MapToDtoStreamAsync()` |
| 17 | Reverse mapping | `.ReverseMap()` generates `Product -> ProductSummary -> Product` |
| 18 | Update (void) mapping | `ApplyUpdate(update, product)` modifies existing object |
| 19 | `.Ignore()` | Preserves `Id`, `SKU`, `CreatedAt` during update mapping |
| 20 | Strict mode | `StrictMode = true` — unmapped properties are compile errors |

```bash
dotnet run --project samples/Mapo.Sample -c Release
```

---

## [Mapo.Circular](./Mapo.Circular)

Demonstrates circular reference safety with `UseReferenceTracking = true`:

- `User` <-> `User` (followers/following graph)
- `User` <-> `Community` (members/admin bidirectional)
- `User` <-> `Message` (sender/receiver)
- Handles cycles without stack overflow

```bash
dotnet run --project samples/Mapo.Circular -c Release
```

---

## [Mapo.Polymorphic](./Mapo.Polymorphic)

Demonstrates polymorphic dispatch with `[MapDerived]`:

- Base type mapping (`Notification` -> `NotificationDto`)
- Three derived types: `Email`, `SMS`, `Push`
- Collection of mixed types dispatched correctly at runtime
- Computed fields per derived type (body truncation, phone masking)

```bash
dotnet run --project samples/Mapo.Polymorphic -c Release
```

---

## [Mapo.Aot](./Mapo.Aot)

NativeAOT-compatible minimal API demonstrating zero-reflection mapping:

- `PublishAot=true` with `InvariantGlobalization`
- Static mapper with computed fields
- JSON serialization via `JsonSerializerContext`
- HTTP endpoints: `GET /products`, `POST /products/batch`

```bash
dotnet run --project samples/Mapo.Aot -c Release
```

To verify AOT compilation:

```bash
dotnet publish samples/Mapo.Aot -c Release -r osx-arm64 --self-contained /p:PublishAot=true
```

---

## [Mapo.Benchmarks](./Mapo.Benchmarks)

Performance comparison of 5 mapping approaches on a complex e-commerce domain:

| Library | Type |
|:--------|:-----|
| Manual | Hand-written C# (baseline) |
| **Mapo** | Compile-time source generator |
| Mapperly | Compile-time source generator |
| AutoMapper | Runtime reflection |
| Mapster | Runtime IL emission |

Benchmarks map `List<Order>` -> `List<OrderDto>` with 100, 1,000, and 10,000 orders. Each order has nested customers, addresses, products, categories, and computed fields.

```bash
cd samples/Mapo.Benchmarks
dotnet run -c Release
```

See [Benchmark Details](../docs/BENCHMARKS.md) for results and methodology.
