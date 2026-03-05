# Mapo

[![CI](https://github.com/mapo-mapper/mapo/actions/workflows/ci.yml/badge.svg)](https://github.com/mapo-mapper/mapo/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Mapo.svg)](https://www.nuget.org/packages/Mapo)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**High-performance, compile-time object mapping for .NET.**

Mapo is a Roslyn source generator that writes your mapping code at build time. You get the fluent API of AutoMapper with the runtime speed of hand-written C# — zero reflection, zero allocation overhead, NativeAOT-ready out of the box.

```csharp
using Mapo.Attributes;

[Mapper]
public partial class UserMapper
{
    public partial UserDto Map(User user);

    static void Configure(IMapConfig<User, UserDto> config)
    {
        config.Map(d => d.FullName, s => s.FirstName + " " + s.LastName);
        // AddressCity is auto-resolved from Address.City via intelligent flattening
    }
}
```

That's it. Mapo generates a complete, null-safe, inlineable implementation at compile time.

---

## Why Mapo?

- **Zero overhead.** Generated code allocates identically to hand-written code. No reflection. No expression tree compilation. No hidden dictionaries.
- **Compile-time safety.** Unmapped properties are caught as warnings (or errors in strict mode) before your code ever runs.
- **Refactoring-safe.** Lambda-based configuration means IDE renames propagate automatically — no magic strings.
- **NativeAOT-ready.** No `System.Reflection.Emit`, no dynamic code generation. Works with `dotnet publish -r linux-x64 --self-contained /p:PublishAot=true`.
- **Familiar API.** If you've used AutoMapper, you already know `Map()`, `Ignore()`, and `AddConverter()`.

---

## Benchmarks

Measured with [BenchmarkDotNet](https://benchmarkdotnet.org/) on .NET 10 mapping 10,000 complex orders with nested objects, collections, computed fields, and enum conversions:

| Method     | Mean        | Ratio | Allocated   | Alloc Ratio |
|:-----------|:------------|------:|:------------|------------:|
| **Mapo**   | 1,310 us    |  0.99 | 3,898.74 KB |        1.00 |
| Mapperly   | 1,323 us    |  0.99 | 3,898.74 KB |        1.00 |
| Manual     | 1,332 us    |  1.00 | 3,898.74 KB |        1.00 |
| Mapster    | 3,551 us    |  2.67 | 4,914.43 KB |        1.26 |
| AutoMapper | 5,270 us    |  3.96 | 6,420.91 KB |        1.65 |

Mapo generates code that is **indistinguishable from hand-written C#** — identical allocations and identical speed. See [Benchmark Details](docs/BENCHMARKS.md).

---

## Installation

```bash
dotnet add package Mapo
```

**Requirements:** .NET 6+ (or any runtime supporting netstandard2.0) with Roslyn 4.8+ (ships with .NET SDK 8+).

The NuGet package includes both the public API (`Mapo.Attributes`) and the source generator (`Mapo.Generator`). No extra configuration needed.

---

## Quick Start

### Step 1: Define your types

```csharp
public class Product
{
    public string SKU { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public Category? Category { get; set; }
}

public record ProductDto(string SKU, string Name, string PriceDisplay, string CategoryName);
```

### Step 2: Create a mapper

```csharp
using Mapo.Attributes;

[Mapper(StrictMode = true)]
public partial class ProductMapper
{
    public partial ProductDto Map(Product product);

    static void Configure(IMapConfig<Product, ProductDto> config)
    {
        config.Map(d => d.PriceDisplay, s => s.Price.ToString("C"))
              .Map(d => d.CategoryName, s => s.Category.Name);
    }
}
```

### Step 3: Use it

```csharp
var mapper = new ProductMapper();
var dto = mapper.Map(product);
```

The generated code handles null-safety (`Category?.Name ?? default`), selects the best constructor, and uses `[MethodImpl(AggressiveInlining)]` for optimal JIT behavior.

---

## Features

| Feature | Description |
|:--------|:------------|
| [Basic Mapping](docs/GUIDE.md#basic-mapping) | Auto-match properties by name (case-insensitive), classes and records |
| [Flattening](docs/GUIDE.md#flattening) | `Address.City` auto-maps to `AddressCity` — recursive up to 4 levels |
| [Custom Mappings](docs/GUIDE.md#custom-mappings) | Strongly-typed lambda expressions via `IMapConfig<S,T>` |
| [Collections](docs/GUIDE.md#collections) | `List<T>`, `T[]`, `IEnumerable<T>` with devirtualized `for` loops |
| [Enum Mapping](docs/GUIDE.md#enum-mapping) | Enum-to-enum switch, enum-to-string `.ToString()`, string-to-enum `Enum.Parse` |
| [Nullable Coercion](docs/GUIDE.md#nullable-value-type-auto-coercion) | `int?` to `int` auto-coerced with `?? default` |
| [Records & Constructors](docs/GUIDE.md#records-and-constructors) | Full support for immutable types, primary constructors, init-only, required |
| [Update Mapping](docs/GUIDE.md#update-mapping) | Mutate existing objects with `void` mapping methods |
| [Dependency Injection](docs/GUIDE.md#dependency-injection) | Constructor-inject services, use them in Configure lambdas |
| [Polymorphic Dispatch](docs/GUIDE.md#polymorphic-mapping) | `[MapDerived]` for inheritance hierarchies |
| [Circular References](docs/GUIDE.md#circular-references) | `UseReferenceTracking` prevents stack overflow in object graphs |
| [Global Converters](docs/GUIDE.md#global-type-converters) | `AddConverter<TIn, TOut>()` applies to all matching properties |
| [Reverse Mapping](docs/GUIDE.md#reverse-mapping) | `.ReverseMap()` generates bidirectional mappers |
| [Async Streaming](docs/GUIDE.md#async-streaming) | Auto-generated `IAsyncEnumerable<T>` extensions for EF Core |
| [Strict Mode](docs/GUIDE.md#strict-mode) | Compile-time errors for unmapped properties |
| [IDE Quick Fixes](docs/GUIDE.md#diagnostics-reference) | Ctrl+. to auto-fix unmapped properties and non-partial classes |

---

## Documentation

| Document | Description |
|:---------|:------------|
| [Usage Guide](docs/GUIDE.md) | Complete reference with examples for every feature |
| [Architecture](docs/ARCHITECTURE.md) | Internal design: parse, analyze, emit pipeline |
| [Benchmarks](docs/BENCHMARKS.md) | Detailed performance methodology and results |
| [Comparison](docs/COMPARISON.md) | Mapo vs AutoMapper, Mapperly, and Mapster |
| [Changelog](CHANGELOG.md) | Release history |

---

## Repository Structure

```
src/
  Mapo.Attributes/          Public API: [Mapper], IMapConfig<S,T>, [MapDerived]
  Mapo.Generator/           Roslyn incremental source generator
  Mapo.Generator.CodeFixes/ IDE code fix provider for MAPO001 + MAPO003
tests/
  Mapo.Generator.Tests/     129 unit tests (Roslyn compilation testing)
  Mapo.IntegrationTests/    52 integration tests (runtime verification)
samples/
  Mapo.Sample/              Full e-commerce scenario
  Mapo.Benchmarks/          Performance comparison vs AutoMapper, Mapperly, Mapster
  Mapo.Circular/            Circular reference tracking demo
  Mapo.Polymorphic/         Polymorphic dispatch demo
  Mapo.Aot/                 NativeAOT compilation verification
```

---

## Contributing

1. Fork the repository
2. Create a feature branch from `dev`
3. Make your changes
4. Run the full test suite:
   ```bash
   dotnet test tests/Mapo.Generator.Tests/ --configuration Release
   dotnet test tests/Mapo.IntegrationTests/ --configuration Release
   ```
5. Open a pull request against `main`

Please ensure all existing tests pass and add tests for new functionality.

---

## License

MIT
