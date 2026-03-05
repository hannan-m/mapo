# Architecture

Mapo is a Roslyn Incremental Source Generator (`IIncrementalGenerator`). It runs during compilation, analyzes your mapper classes, and injects optimized C# code back into the build.

## Pipeline Overview

```
[Your Code] → Roslyn Compiler → SyntaxProvider → MapperParser → Models → MapperEmitter → [Generated .g.cs]
```

Three phases, fully decoupled:

| Phase | Responsibility | Key Files |
|:------|:---------------|:----------|
| **Entry** | Hook into Roslyn, filter `[Mapper]` classes | `MapoGenerator.cs` |
| **Parse** | Semantic analysis → produce model objects | `Syntax/MapperParser.cs`, `Syntax/ConfigParser.cs`, `Syntax/PropertyMatcher.cs`, `Syntax/MethodMappingFactory.cs` |
| **Emit** | Model objects → generated C# strings | `Emit/MapperEmitter.cs`, `Emit/ObjectEmitter.cs`, `Emit/CollectionEmitter.cs`, `Emit/EnumEmitter.cs`, `Emit/ExpressionEmitter.cs` |

The parser and emitter are fully decoupled. The parser produces plain data objects (no Roslyn types in the models). The emitter consumes strings and lists — zero Roslyn dependencies.

---

## Project Structure

```
src/
├── Mapo.Attributes/                  # Public API (netstandard2.0)
│   ├── MapperAttribute.cs            # [Mapper], [MapDerived], IMapConfig<S,T>
│   └── MappingContext.cs             # Circular reference tracking dictionary
│
├── Mapo.Generator/                   # Source generator (netstandard2.0)
│   ├── MapoGenerator.cs              # IIncrementalGenerator entry point
│   │
│   ├── Syntax/                       # Phase 2: Parsing
│   │   ├── MapperParser.cs           # Top-level orchestrator (~420 lines)
│   │   ├── ConfigParser.cs           # Parses Configure() method chains
│   │   ├── PropertyMatcher.cs        # Property matching, flattening, type coercion
│   │   ├── MethodMappingFactory.cs   # Builds MethodMapping from type pairs
│   │   └── ParameterRewriter.cs      # Rewrites lambda parameter names
│   │
│   ├── Models/                       # Data carriers (no Roslyn dependencies)
│   │   ├── MapperInfo.cs             # Top-level mapper: namespace, class, mappings
│   │   ├── MethodMapping.cs          # Per-method: source/target types, args, props
│   │   ├── PropertyMapping.cs        # Per-property: expression, null guard, origin
│   │   ├── DerivedMappingInfo.cs     # Polymorphic: source/target display strings
│   │   └── ParseResult.cs           # Parser output: MapperInfo + diagnostics
│   │
│   ├── Emit/                         # Phase 3: Code generation
│   │   ├── MapperEmitter.cs          # Orchestrates emission, generates file structure
│   │   ├── ObjectEmitter.cs          # Object mapping: constructor, properties, null guards
│   │   ├── CollectionEmitter.cs      # Collection mapping: type checks, for loops
│   │   ├── EnumEmitter.cs            # Enum mapping: switch expressions
│   │   ├── ExpressionEmitter.cs      # Expression post-processing, null-safe chains
│   │   ├── CodeWriter.cs             # Indented string builder helper
│   │   └── StringBuilderPool.cs      # StringBuilder pooling for allocation efficiency
│   │
│   ├── Diagnostics/
│   │   └── DiagnosticDescriptors.cs  # MAPO001–MAPO011 diagnostic definitions
│   │
│   └── TypeHelpers.cs                # Collection/type classification utilities
│
└── Mapo.Generator.CodeFixes/         # IDE integration
    └── MapoCodeFixProvider.cs         # Code fixes for MAPO001 + MAPO003
```

---

## Phase 1: Entry

`MapoGenerator.cs` implements `IIncrementalGenerator`. It registers a `SyntaxProvider` that:

1. **Filters** syntax nodes for class declarations with a `[Mapper]` attribute (fast syntactic predicate)
2. **Transforms** matching nodes via `MapperParser.Parse()` (semantic analysis)
3. **Collects** diagnostics from the parse result
4. **Emits** generated source via `MapperEmitter.Emit()`
5. **Registers** the output with `context.AddSource()`

The incremental pipeline ensures re-generation only occurs when the mapper's syntax tree or referenced types change.

---

## Phase 2: Parse

### MapperParser (orchestrator)

Receives a `ClassDeclarationSyntax` and `SemanticModel`. Performs:

1. **Validation** — Confirms `[Mapo.Attributes.Mapper]` (full namespace to avoid collisions)
2. **Method discovery** — Finds partial methods (mapping entry points)
3. **Configuration parsing** — Delegates to `ConfigParser` for `.Map()`, `.Ignore()`, `.AddConverter()`, `.ReverseMap()` chains
4. **Mapping generation** — Delegates to `MethodMappingFactory` for each source→target pair
5. **Auto-discovery** — Breadth-first loop discovers nested mapping needs (e.g., `List<OrderItem>` discovers `OrderItem → OrderItemDto`)

### ConfigParser

Parses the `Configure` method body from the syntax tree. Walks the AST to extract:

- `.Map(target, source)` — Lambda bodies become property assignment expressions
- `.Ignore(target)` — Target property names added to ignore set
- `.AddConverter<S,T>(lambda)` — Lambda body stored as global converter expression
- `.ReverseMap()` — Triggers reverse pair discovery

### PropertyMatcher

Resolves how each target property gets its value. Checks (in order):

1. **Injected renames** — DI field references from Configure parameters
2. **Custom mappings** — Explicit `.Map()` from Configure
3. **Global converters** — `AddConverter` type-pair match
4. **Direct name match** — Same name, same type (case-insensitive)
5. **Nullable coercion** — `Nullable<T>` → `T` with `?? default`
6. **Collection mapping** — `List<A>` → `List<B>` with element mapper discovery
7. **Enum conversion** — Enum↔enum (switch), enum→string (`.ToString()`), string→enum (`Enum.Parse`)
8. **Nested object** — Different complex types trigger sub-mapper discovery
9. **Flattening** — Recursive name decomposition (`AddressCity` → `Address.City`), up to 4 levels

### MethodMappingFactory

Builds a `MethodMapping` from a source→target type pair:

- Selects the best public constructor (most parameters matching source properties)
- Iterates settable target properties, delegating to `PropertyMatcher` for each
- Tracks unmapped properties for diagnostics
- Detects `[MapDerived]` attributes for polymorphic dispatch

### Discovery Queue

```
User-declared partial methods → enqueue
    ↓
For each dequeued (source, target) pair:
    Build MethodMapping → discover nested types → enqueue new pairs
    ↓
Until queue is empty
```

This handles arbitrary nesting: `Order` → `OrderItem` → `Product` → `Category`.

---

## Phase 3: Emit

`MapperEmitter.Emit()` takes a `MapperInfo` and builds a complete C# source string.

### Object Methods (ObjectEmitter)

1. Public partial method → delegates to `{Name}Internal()`
2. Internal method with `[MethodImpl(AggressiveInlining | AggressiveOptimization)]`
3. Null guard: `ArgumentNullException` for creation, early return for update
4. Polymorphic dispatch: `switch` on source type for `[MapDerived]`
5. Reference tracking: `_context.TryGet()` / `_context.Add()`
6. Constructor call with matched arguments
7. Object initializer for init-only / required properties
8. Property assignments for regular settable properties

### Collection Methods (CollectionEmitter)

1. Type-check cascade: `T[]` → `List<T>` → fallback `IEnumerable`
2. Pre-allocated `new List<T>(capacity)` for known-count collections
3. `for` loop with direct indexing (no virtual dispatch)

### Enum Methods (EnumEmitter)

1. `switch` expression matching source → target members (case-insensitive)
2. `default` fallback for unmatched members

### Expression Post-Processing (ExpressionEmitter)

- **Null-safe chains**: Builds `source.Address?.City ?? default` from `NavigationSegments`
- **Internal redirect**: Replaces public method calls with `Internal` variants
- **Reference tracking context**: Appends `_context` parameter to internal calls

### Extension Methods & Streaming

For static mappers, `MapperEmitter` generates:

- `{ClassName}Extensions` class with fluent `.Map()` extension methods
- `IAsyncEnumerable<T>` streaming extension methods with `[EnumeratorCancellation]`

---

## Model Objects

All models implement `IEquatable<T>` for Roslyn's incremental caching:

| Model | Purpose |
|:------|:--------|
| `MapperInfo` | Namespace, class name, static/instance, strict mode, reference tracking, list of mappings, injected members, global converters |
| `MethodMapping` | Method name, source/target types, constructor args, property mappings, derived mappings, enum cases, flags (enum/collection/update/user-declared) |
| `PropertyMapping` | Target name, source expression, null guard info, navigation segments, collection loop info, mapping origin |
| `ConstructorArg` | Expression string + optional `CollectionLoopInfo` |
| `CollectionLoopInfo` | Source collection expression, projection body, item mapper name, target item type, count member |
| `DerivedMappingInfo` | Source/target type display strings for polymorphic dispatch |
| `ParseResult` | `MapperInfo` + list of diagnostics (wrapper for incremental pipeline) |

---

## Diagnostics

| ID | Severity | Description |
|----|----------|-------------|
| MAPO001 | Warning/Error | Unmapped target property |
| MAPO003 | Error | Mapper class not partial |
| MAPO004 | Warning | Invalid Configure signature |
| MAPO005 | Warning | No accessible properties |
| MAPO006 | Warning | Duplicate target mapping |
| MAPO007 | Warning | Invalid target property in Map() |
| MAPO008 | Warning | Invalid source expression in Map() |
| MAPO009 | Info | Nullable → non-nullable auto-coercion |
| MAPO010 | Warning | Circular reference without tracking |
| MAPO011 | Warning | Unmatched enum member |

IDE code fixes are provided for MAPO001 (insert `.Map()` call) and MAPO003 (add `partial` modifier).

---

## Performance Design Principles

1. **Incremental generation** — Only re-generates when mapper source changes
2. **Equatable models** — Roslyn caches output when models haven't changed
3. **AggressiveInlining** — Generated methods hint the JIT to inline
4. **AggressiveOptimization** — JIT spends more time optimizing hot methods
5. **Devirtualized collections** — Type checks for `T[]` and `List<T>` avoid `IEnumerable` virtual dispatch
6. **Pre-sized allocations** — `new List<T>(count)` prevents resize GC pressure
7. **LINQ elimination** — `.Select().ToList()` replaced with indexed `for` loops
8. **Zero reflection at runtime** — All mapping logic resolved at compile time

---

## Target Frameworks

| Component | Framework | Why |
|:----------|:----------|:----|
| `Mapo.Attributes` | netstandard2.0 | Broad compatibility (.NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+) |
| `Mapo.Generator` | netstandard2.0 | Required by Roslyn analyzer/generator hosting |
| `Mapo.Generator.CodeFixes` | netstandard2.0 | Required by IDE code fix hosting |
| Roslyn dependency | 4.8.0 | Minimum version for incremental generator APIs |
| Tests / Samples | net10.0 | Modern runtime for testing |
