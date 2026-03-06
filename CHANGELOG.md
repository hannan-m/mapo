# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.1] - 2026-03-06

### Fixed
- **Nullable same-element-type collections** — `List<string>?` → `List<string>`, `List<string>?` → `IReadOnlyList<string>`, and custom-mapped same-element collections now emit `?? new List<T>()` instead of null-forgiving `!`, eliminating CS8601 warnings and runtime null propagation

### Added
- 23 new edge case and unhappy path tests covering: `[MapFrom]` with non-existent/conflicting properties, MAPO011 strict mode diagnostics, enum partial matches and invalid string parsing, constructor fallback, null source inputs, flattening with null intermediates, update mapping with ignored properties, converter with nullable null values, mixed creation+update mappers, zero/empty value mapping, nullable value types, null collection elements, and empty collections

### Changed
- Test count: 227 unit tests (was 198), 70 integration tests

## [1.1.0] - 2026-03-06

### Added
- **`[MapFrom]` attribute** — property-level attribute for renaming source properties without a Configure method. Supports enum conversion, global converters, nested auto-discovered types, and update mappings.
- Complex type converter integration tests confirming `AddConverter<ClassA, ClassB>()` works for class-to-class conversions with null handling
- 35 new unit tests (`MapFromTests`, `ComplexConverterTests`, `CollectionMappingTests`) and 18 new integration tests covering `[MapFrom]`, complex converters, same-element collections, nullable references, and inherited properties

### Fixed
- **Same-element-type collection mapping** — `List<string>` → `IReadOnlyList<string>` and similar covariant container changes no longer generate a bogus `MapstringTostring` method. Direct assignment is used when element types match.

### Changed
- Test count: 198 unit tests (was 163), 70 integration tests (was 52)

## [1.0.1] - 2026-03-05

### Fixed
- **Nullable nested objects:** `new Type?()` is invalid C# for reference types — now strips `?` from target types in `new` expressions and emits null-conditional mapping (`source.Prop != null ? Map(source.Prop) : default`) for nullable source properties
- **Collection element conversion:** `List<string>` → `List<Enum>` and vice versa now auto-generates inline LINQ with `Enum.Parse<T>()` / `.ToString()`
- **Lambda namespace resolution:** converter lambdas referencing external types (e.g., `Guid.Parse`) now compile correctly — user `using` directives are forwarded to generated code
- **Nullable converter matching:** `AddConverter<string, Guid>()` now correctly matches `string?` source properties by stripping nullable annotations before lookup
- **Nullable string warnings:** `string?` → `string` assignments now use the null-forgiving operator (`!`) to suppress CS8601; `string?` → enum uses `Enum.Parse<T>(source.Prop!)` to suppress CS8604
- **Nullable collections:** `List<T>?` source collections now return an empty list instead of throwing `ArgumentNullException`
- **Spurious MAPO010:** diamond dependencies (two parents sharing a child type) no longer trigger false circular reference warnings — replaced simple re-discovery heuristic with ancestry-based cycle detection

### Added
- `TypeHelpers.StripNullableAnnotation()` and `ToConstructableDisplayString()` shared utilities
- `MapperInfo.UserUsings` property for forwarding user `using` directives
- 36 new regression tests: `NullableReferenceTypeTests`, `CollectionConversionTests`, `LambdaNamespaceTests`, `NullableCollectionTests`, `CycleDetectionTests`, `OcpiIntegrationTests`

### Changed
- Test infrastructure (`MapoVerifier`) now enables `NullableContextOptions.Enable` and uses explicit `CSharpParseOptions` with latest language version
- CI release job builds before packing to ensure generator DLL exists (fixes NU5019)

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
- 129 unit tests + 52 integration tests (at initial release)
