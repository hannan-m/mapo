# Benchmarks

All benchmarks use [BenchmarkDotNet](https://benchmarkdotnet.org/) with `[MemoryDiagnoser]` on .NET 10.

## Test Scenario

A real-world e-commerce domain with complex object graphs:

```
Order
  ├── Id (Guid)
  ├── OrderNumber (string)
  ├── Status (enum → string)
  ├── Customer
  │     ├── FirstName + LastName → CustomerName (string concat)
  │     ├── Email → CustomerEmail
  │     └── Address.City → CustomerCity (flattening)
  ├── Items: List<OrderItem>
  │     └── OrderItem
  │           ├── Product.Name → ProductName
  │           ├── Product.SKU → ProductSKU
  │           ├── Quantity
  │           └── (UnitPrice - Discount) * Quantity → LineTotal (computed)
  └── Items.Sum(...) → TotalAmount (aggregate)
```

Mapping patterns exercised per order:
- Flattening (2 levels)
- Nullable navigation
- Enum to string conversion
- String formatting
- Computed expressions
- Aggregation
- Collection mapping with nested objects

## Libraries Compared

| Library | Version | Type |
|:--------|:--------|:-----|
| Manual | — | Hand-written C# (baseline) |
| **Mapo** | source | Compile-time source generator |
| Mapperly | 4.3.1 | Compile-time source generator |
| Mapster | 7.4.0 | Runtime reflection + IL emission |
| AutoMapper | 13.0.1 | Runtime reflection |

## Results

### 100 Orders

| Method     | Mean      | Ratio | Allocated | Alloc Ratio |
|:-----------|----------:|------:|----------:|------------:|
| **Mapo**   | 8.326 us  |  0.98 | 38.34 KB  |        1.00 |
| Manual     | 8.474 us  |  1.00 | 38.34 KB  |        1.00 |
| Mapperly   | 8.488 us  |  1.00 | 38.34 KB  |        1.00 |
| AutoMapper | 20.482 us |  2.42 | 63.08 KB  |        1.65 |
| Mapster    | 27.966 us |  3.31 | 48.50 KB  |        1.26 |

### 1,000 Orders

| Method     | Mean       | Ratio | Allocated  | Alloc Ratio |
|:-----------|----------:|------:|----------:|------------:|
| Mapperly   | 90.188 us  |  0.98 | 382.90 KB |        1.00 |
| **Mapo**   | 91.010 us  |  0.99 | 382.90 KB |        1.00 |
| Manual     | 92.389 us  |  1.00 | 382.90 KB |        1.00 |
| AutoMapper | 213.804 us |  2.32 | 625.63 KB |        1.63 |
| Mapster    | 286.095 us |  3.10 | 484.47 KB |        1.27 |

### 10,000 Orders

| Method     | Mean         | Ratio | Allocated    | Alloc Ratio |
|:-----------|----------:|------:|----------:|------------:|
| **Mapo**   | 1,310 us     |  0.99 | 3,898.74 KB  |        1.00 |
| Mapperly   | 1,323 us     |  0.99 | 3,898.74 KB  |        1.00 |
| Manual     | 1,332 us     |  1.00 | 3,898.74 KB  |        1.00 |
| Mapster    | 3,551 us     |  2.67 | 4,914.43 KB  |        1.26 |
| AutoMapper | 5,270 us     |  3.96 | 6,420.91 KB  |        1.65 |

### Key Takeaways

**Memory:** Mapo, Mapperly, and Manual all allocate the exact same amount of memory (1.00x ratio) across all sizes. Mapster allocates 26% more. AutoMapper allocates 65% more.

**Speed:** Source generators (Mapo, Mapperly) are **indistinguishable from hand-written code**. Both consistently match or slightly beat the manual baseline (within measurement noise). Runtime mappers are 2-4x slower.

**Scaling:** The performance ratios remain stable from 100 to 10,000 items. Source generators scale linearly with zero overhead. AutoMapper degrades more at scale (2.42x at 100 → 3.96x at 10,000) due to GC pressure from 65% higher allocations triggering Gen2 collections.

**Mapo vs Mapperly:** Effectively identical performance. Both generate the same quality of code. Mapo's advantage is its fluent `IMapConfig<S,T>` API — Mapperly requires string-based `[MapProperty]` attributes or manual method implementations for computed mappings.

## Reproducing

```bash
cd samples/Mapo.Benchmarks
dotnet run -c Release
```

The benchmark source is at `samples/Mapo.Benchmarks/Program.cs`. All five mappers are configured identically with the same custom mappings, computed fields, and collection handling.

## How Mapo Stays Fast

1. **No reflection** — Pure compiled C#, same as hand-written code
2. **Pre-allocated collections** — `new List<T>(source.Count)` avoids resize allocations
3. **Devirtualized iteration** — Type checks for `T[]` and `List<T>` bypass `IEnumerable` dispatch
4. **AggressiveInlining** — JIT inlines small mapping methods at call sites
5. **AggressiveOptimization** — JIT spends extra time on hot mapping methods
6. **LINQ elimination** — `.Select().ToList()` chains replaced with `for` loops
