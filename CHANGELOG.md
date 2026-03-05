# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-03-05

### Core Mapping
- Compile-time object mapping via Roslyn incremental source generator
- Automatic property matching by name (case-insensitive)
- Record and constructor parameter mapping (selects best public constructor)
- Init-only (`init`) and `required` property support via object initializer
- Update (void) mapping for mutating existing objects in place

### Type Conversions
- Enum-to-enum mapping via optimized `switch` expressions (case-insensitive member matching)
- Enum-to-string auto-conversion via `.ToString()`
- String-to-enum auto-conversion via `Enum.Parse<T>()`
- Nullable value type auto-coercion (`int?` → `int` via `?? default`)
- Global type converters via `AddConverter<TSource, TTarget>()`

### Flattening & Navigation
- Single-level property flattening (`Address.City` → `AddressCity`)
- Multi-level recursive flattening up to 4 levels deep
- Null-safe navigation chains (`?.` operators) for all flattened properties

### Collections
- `List<T>`, `T[]`, `IEnumerable<T>` collection mapping
- Devirtualized `for` loops with direct indexing (no `IEnumerable` virtual dispatch)
- Pre-allocated capacity (`new List<T>(source.Count)`) to avoid resize allocations
- LINQ elimination: `.Select().ToList()` replaced with optimized `for` loops

### Configuration
- Fluent `IMapConfig<S,T>` API with `.Map()`, `.Ignore()`, `.AddConverter()`, `.ReverseMap()`
- Lambda-based configuration (fully refactoring-safe, parsed at compile time)
- Multiple `Configure` methods per mapper class (one per type pair)
- Dependency injection support — constructor parameters available in Configure lambdas

### Advanced Features
- Polymorphic mapping via `[MapDerived]` attribute
- Circular reference safety via `UseReferenceTracking` option
- Reverse mapping generation via `.ReverseMap()`
- Extension methods auto-generated for static mappers
- `IAsyncEnumerable<T>` streaming extensions auto-generated for static mappers
- Strict mode (`StrictMode = true`) turns unmapped properties into compile errors

### Diagnostics & IDE
- MAPO001–MAPO011 compile-time diagnostics with clear messages
- IDE code fix for MAPO001: "Map property 'X'" inserts `.Map()` call
- IDE code fix for MAPO003: "Add 'partial' modifier" fixes non-partial class

### Quality
- NativeAOT compatible (zero reflection at runtime)
- SourceLink integration for debuggable NuGet packages
- Deterministic builds
- 129 unit tests + 52 integration tests
