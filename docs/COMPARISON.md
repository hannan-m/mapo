# Mapo vs Other Mappers

A technical comparison for .NET developers choosing between compile-time and runtime mapping libraries.

---

## At a Glance

| | Mapo | Mapperly | AutoMapper | Mapster |
|:--|:----:|:--------:|:----------:|:-------:|
| **Execution** | Compile-time | Compile-time | Runtime | Runtime |
| **Speed** | 0.99x manual | 0.99x manual | 3.96x manual | 2.67x manual |
| **Memory** | 1.00x manual | 1.00x manual | 1.65x manual | 1.26x manual |
| **NativeAOT** | Yes | Yes | No | No |
| **Configuration** | Fluent lambdas | Attributes | Fluent lambdas | Fluent lambdas |

---

## Performance

Measured on .NET 10 mapping 10,000 complex e-commerce orders (nested objects, collections, computed fields):

| Method     | Mean        | vs Manual | Allocated    | vs Manual |
|:-----------|:------------|----------:|:-------------|----------:|
| **Mapo**   | 1,310 us    |     0.99x | 3,898.74 KB  |     1.00x |
| Mapperly   | 1,323 us    |     0.99x | 3,898.74 KB  |     1.00x |
| Manual     | 1,332 us    |     1.00x | 3,898.74 KB  |     1.00x |
| Mapster    | 3,551 us    |     2.67x | 4,914.43 KB  |     1.26x |
| AutoMapper | 5,270 us    |     3.96x | 6,420.91 KB  |     1.65x |

Source generators (Mapo, Mapperly) are **indistinguishable from hand-written code** — identical allocations, identical speed. Runtime mappers (AutoMapper, Mapster) are 2.7-4x slower with 26-65% more allocations.

---

## Feature Comparison

| Feature | Mapo | Mapperly | AutoMapper | Mapster |
|:--------|:----:|:--------:|:----------:|:-------:|
| NativeAOT compatible | Yes | Yes | No | No |
| Refactoring-safe config | Yes | Partial | Yes | Yes |
| Constructor/record mapping | Yes | Yes | Yes | Yes |
| Auto-flattening | Yes (4 levels) | Yes | Yes | Yes |
| Null-safe chains | Yes | Yes | Yes | Yes |
| Enum ↔ enum | Yes | Yes | Yes | Yes |
| Enum ↔ string | Yes | Manual | Yes | Yes |
| Nullable auto-coercion | Yes | No | Yes | Yes |
| Collection fast paths | Yes | Yes | No | No |
| DI integration | Native | Manual | Native | Native |
| Update/patch mapping | Yes | No | No | No |
| Circular reference tracking | Yes | No | Yes | No |
| Polymorphic dispatch | Yes | No | Yes | No |
| Global type converters | Yes | Yes | Yes | Yes |
| Reverse mapping | Yes | Yes | Yes | Yes |
| Async streaming | Yes | No | No | No |
| Auto extension methods | Yes | No | No | Yes |
| IDE quick fixes | Yes | Yes | No | No |
| Strict mode | Yes | Yes | Assert-based | No |

---

## Mapo vs Mapperly

Both are compile-time source generators with identical performance — indistinguishable from hand-written code. The choice comes down to API design and feature set.

### Configuration

```csharp
// Mapperly — attribute-driven, string-based paths
[Mapper]
public partial class MapperlyMapper
{
    [MapProperty(
        new[] { nameof(Product.Category), nameof(Category.Parent), nameof(Category.Name) },
        new[] { nameof(ProductDto.ParentCategoryName) })]
    public partial ProductDto MapProduct(Product product);
}

// Mapo — fluent lambda expressions
[Mapper]
public partial class MapoMapper
{
    public partial ProductDto MapProduct(Product product);

    static void Configure(IMapConfig<Product, ProductDto> config)
    {
        config.Map(d => d.ParentCategoryName, s => s.Category.Parent.Name);
    }
}
```

Mapo's lambda configuration is fully refactoring-safe. Rename `Parent` to `ParentNode` and the IDE updates the lambda automatically. Mapperly's `nameof` arrays give compile-time safety on individual segments but no IDE rename support for the path structure.

### Computed Mappings

```csharp
// Mapperly — must write the entire method manually
public OrderItemDto MapOrderItem(OrderItem item)
{
    return new OrderItemDto(
        item.Product.Name,
        item.Product.SKU,
        item.Quantity,
        (item.Product.UnitPrice - item.Discount) * item.Quantity);
}

// Mapo — express just the computation, auto-generate the rest
static void Configure(IMapConfig<OrderItem, OrderItemDto> config)
{
    config.Map(d => d.LineTotal,
               s => (s.Product.UnitPrice - s.Discount) * s.Quantity);
}
```

### Unique to Mapo
- Circular reference tracking (`UseReferenceTracking`)
- Polymorphic dispatch (`[MapDerived]`)
- Update/patch mapping (void methods)
- Enum ↔ string auto-conversion
- Nullable value type auto-coercion
- Auto-generated async streaming extensions
- Auto-generated fluent extension methods
- DI parameters directly in `Configure` method

### Unique to Mapperly
- `[MapperIgnoreSource]` / `[MapperIgnoreTarget]` at class level
- `[ObjectFactory]` for custom object creation
- `[UseStaticMapper]` to reference external mappers

---

## Mapo vs AutoMapper

AutoMapper uses reflection at runtime. Mapo generates code at compile time.

| Aspect | AutoMapper | Mapo |
|:-------|:-----------|:-----|
| **Speed** | 3.96x slower | 0.99x manual |
| **Memory** | 65% more | Identical to manual |
| **Startup** | Assembly scanning + expression compilation | Zero startup cost |
| **Errors** | Runtime exceptions | Compile-time diagnostics |
| **Debugging** | Opaque runtime mapping | Step through generated `.g.cs` |
| **NativeAOT** | Not supported | Full support |

### API Comparison

```csharp
// AutoMapper
cfg.CreateMap<Order, OrderDto>()
    .ForMember(d => d.Total, opt => opt.MapFrom(s => s.Items.Sum(i => i.Price)));

// Mapo — same intent, less ceremony
config.Map(d => d.Total, s => s.Items.Sum(i => i.Price));
```

| AutoMapper | Mapo Equivalent |
|:-----------|:----------------|
| `CreateMap<S,T>()` | `[Mapper]` + partial method |
| `ForMember(d => d.X, opt => opt.MapFrom(...))` | `config.Map(d => d.X, s => ...)` |
| `ForMember(d => d.X, opt => opt.Ignore())` | `config.Ignore(d => d.X)` |
| `.ReverseMap()` | `config.ReverseMap()` |
| `.ConvertUsing(...)` | `config.AddConverter<S,T>(...)` |
| Profile classes | Multiple Configure methods |
| `AssertConfigurationIsValid()` | `StrictMode = true` (build time) |

---

## Mapo vs Mapster

Mapster uses IL emission at runtime (with `.Compile()` for pre-compilation).

| Aspect | Mapster | Mapo |
|:-------|:--------|:-----|
| **Speed** | 2.67x slower | 0.99x manual |
| **Memory** | 26% more | Identical to manual |
| **NativeAOT** | Not supported (`Reflection.Emit`) | Full support |

```csharp
// Mapster
config.NewConfig<Order, OrderDto>()
    .Map(d => d.Total, s => s.Items.Sum(i => i.Price));

// Mapo — nearly identical API
config.Map(d => d.Total, s => s.Items.Sum(i => i.Price));
```

---

## Mapo vs Manual Code

Manual mapping is the performance baseline — zero framework overhead.

**When to use manual:** A single mapping between two trivial types where a framework adds no value.

**When to use Mapo:** Any project with more than a handful of mappings. Mapo generates the same code you would write by hand, but:

- Automatically discovers and maps matching properties
- Catches unmapped properties at compile time (StrictMode)
- Handles null-safety automatically
- Updates when you add/rename properties (no forgotten assignments)
- Generates collection handling with pre-allocated capacity
- Provides IDE quick fixes for common issues
