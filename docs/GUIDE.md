# Mapo Usage Guide

Complete reference for using Mapo in your .NET projects.

---

## Table of Contents

- [Installation](#installation)
- [Getting Started](#getting-started)
- [Basic Mapping](#basic-mapping)
- [Flattening](#flattening)
- [Custom Mappings](#custom-mappings)
- [Collections](#collections)
- [Enum Mapping](#enum-mapping)
- [Records and Constructors](#records-and-constructors)
- [Nullable Types](#nullable-types)
- [Update Mapping](#update-mapping)
- [Dependency Injection](#dependency-injection)
- [Polymorphic Mapping](#polymorphic-mapping)
- [Circular References](#circular-references)
- [Global Type Converters](#global-type-converters)
- [Reverse Mapping](#reverse-mapping)
- [Async Streaming](#async-streaming)
- [Extension Methods](#extension-methods)
- [Static vs Instance Mappers](#static-vs-instance-mappers)
- [Strict Mode](#strict-mode)
- [Ignoring Properties](#ignoring-properties)
- [Generated Code](#generated-code)
- [Diagnostics Reference](#diagnostics-reference)
- [Best Practices](#best-practices)
- [Performance Tips](#performance-tips)
- [Troubleshooting](#troubleshooting)
- [Migrating from AutoMapper](#migrating-from-automapper)
- [Complete Example](#complete-example)

---

## Installation

```bash
dotnet add package Mapo
```

The NuGet package includes two assemblies:

| Assembly | Role | Ships with your app? |
|:---------|:-----|:---------------------|
| `Mapo.Attributes` | Public API (`[Mapper]`, `IMapConfig<S,T>`) | Yes |
| `Mapo.Generator` | Roslyn source generator | No (compile-time only) |

**Requirements:** .NET 6+ (or any runtime supporting netstandard2.0) with .NET SDK 8+.

---

## Getting Started

Three steps to your first mapper:

```csharp
// 1. Define source and target types
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

// 2. Create a partial class with [Mapper] and declare partial methods
using Mapo.Attributes;

[Mapper]
public partial class UserMapper
{
    public partial UserDto Map(User user);
}

// 3. Use the mapper
var mapper = new UserMapper();
UserDto dto = mapper.Map(user);
```

Build your project. Mapo generates the method body at compile time. You can inspect the generated code by pressing F12 (Go to Definition) on the `Map` call.

> **Key insight:** Mapo reads your partial method signatures and generates implementations. You declare *what* to map; Mapo writes *how*.

---

## Basic Mapping

Mapo matches properties by name (case-insensitive). Properties with the same name and compatible type are mapped automatically.

```csharp
[Mapper]
public partial class UserMapper
{
    public partial UserDto Map(User user);
}
```

Generated (simplified):

```csharp
public partial UserDto Map(User user)
{
    if (user == null) throw new ArgumentNullException(nameof(user));
    var target = new UserDto();
    target.Id = user.Id;
    target.Name = user.Name;
    target.Email = user.Email;
    return target;
}
```

Properties that exist on the target but not on the source trigger a [MAPO001](#mapo001-property-is-not-mapped) diagnostic.

---

## Flattening

Mapo automatically flattens nested properties when the target property name matches a path through source navigation properties.

```csharp
public class Customer
{
    public string Name { get; set; } = "";
    public Address Address { get; set; } = new();
}

public class CustomerDto
{
    public string Name { get; set; } = "";
    public string AddressCity { get; set; } = "";    // Flattened from Address.City
    public string AddressZipCode { get; set; } = ""; // Flattened from Address.ZipCode
}
```

Mapo sees `AddressCity`, finds `Address` on `Customer`, then looks for `City` inside `Address`. Generated code uses null-safe chains:

```csharp
target.AddressCity = customer.Address?.City ?? default;
target.AddressZipCode = customer.Address?.ZipCode ?? default;
```

### Deep Flattening

Flattening works recursively up to 4 levels deep:

```csharp
public class Country { public string Name { get; set; } = ""; }
public class Address { public string City { get; set; } = ""; public Country Country { get; set; } = new(); }
public class Company { public Address Headquarters { get; set; } = new(); }

public class CompanyDto
{
    public string HeadquartersCity { get; set; } = "";           // 2-level
    public string HeadquartersCountryName { get; set; } = "";    // 3-level
}
```

Generated:

```csharp
target.HeadquartersCity = company.Headquarters?.City ?? default;
target.HeadquartersCountryName = company.Headquarters?.Country?.Name ?? default;
```

Beyond 4 levels, properties are reported as unmapped ([MAPO001](#mapo001-property-is-not-mapped)).

---

## Custom Mappings

When property names don't match or you need computed values, add a static `Configure` method:

```csharp
[Mapper]
public partial class OrderMapper
{
    public partial OrderDto Map(Order order);

    static void Configure(IMapConfig<Order, OrderDto> config)
    {
        config.Map(d => d.OrderDate, s => s.CreatedAt.ToString("yyyy-MM-dd"))
              .Map(d => d.CustomerName, s => s.Customer.FirstName + " " + s.Customer.LastName)
              .Map(d => d.Total, s => s.Items.Sum(i => i.Price * i.Quantity));
    }
}
```

### How Configure Works

The `Configure` method is **parsed at compile time** from the syntax tree. It is never called at runtime. The lambda bodies are compiled directly into the generated mapping code.

**Rules:**
- Must be named `Configure`
- Must be `static` ([MAPO004](#mapo004-configure-method-has-wrong-signature) if not)
- First parameter must be `IMapConfig<TSource, TTarget>`
- Multiple `Configure` methods (for different type pairs) are allowed in one class
- Supports fluent chaining: `.Map()`, `.Ignore()`, `.AddConverter()`, `.ReverseMap()`

### The Map Method

```csharp
config.Map(
    target => target.PropertyName,  // Which target property to set
    source => source.SomeExpression // What value to assign
);
```

The source expression can be any valid C# expression:

```csharp
config.Map(d => d.Name, s => s.FullName);                            // Property access
config.Map(d => d.Display, s => $"{s.Name} ({s.Code})");             // String interpolation
config.Map(d => d.PriceText, s => s.Price.ToString("C"));            // Method calls
config.Map(d => d.Total, s => s.Items.Sum(i => i.Amount));           // LINQ
config.Map(d => d.City, s => s.Address.City);                        // Nested navigation
config.Map(d => d.Status, s => s.IsActive ? "Active" : "Inactive"); // Ternary
```

---

## Collections

Mapo handles `List<T>`, `T[]`, and `IEnumerable<T>` automatically. When a source property is a collection of type `A` and the target is a collection of type `B`, Mapo maps each element.

```csharp
[Mapper]
public partial class OrderMapper
{
    public partial OrderDto Map(Order order);
    // Mapo auto-discovers that List<OrderItem> -> List<OrderItemDto>
    // needs an OrderItem -> OrderItemDto mapper and generates it
}
```

You can also declare collection methods explicitly:

```csharp
[Mapper]
public partial class OrderMapper
{
    public partial List<OrderDto> MapOrders(List<Order> orders);
    public partial OrderDto MapOrder(Order order);
}
```

### Collection Performance

Generated code uses devirtualized fast paths — no `IEnumerable` virtual dispatch:

```csharp
// Generated (simplified):
if (orders is List<Order> sourceList)
{
    var list = new List<OrderDto>(sourceList.Count);  // Pre-allocated
    for (int i = 0; i < sourceList.Count; i++)
        list.Add(MapOrderInternal(sourceList[i]));
    return list;
}
```

### LINQ Elimination

`.Select().ToList()` in custom mappings is replaced with optimized `for` loops:

```csharp
// You write:
config.Map(d => d.Products, s => s.Items.Select(i => i.Product).ToList());

// Mapo generates:
var _col_0 = new List<ProductDto>(order.Items.Count);
for (var _i = 0; _i < order.Items.Count; _i++)
    _col_0.Add(MapProductInternal(order.Items[_i].Product));
```

No LINQ allocation. No `IEnumerable` overhead. Pre-allocated capacity.

---

## Enum Mapping

### Enum to Enum

When source and target have different enum types with matching member names, Mapo generates an optimized `switch` expression:

```csharp
public enum OrderStatus { Draft, Confirmed, Shipped }
public enum OrderStatusDto { Draft, Confirmed, Shipped }
```

Generated:

```csharp
return value switch
{
    OrderStatus.Draft => OrderStatusDto.Draft,
    OrderStatus.Confirmed => OrderStatusDto.Confirmed,
    OrderStatus.Shipped => OrderStatusDto.Shipped,
    _ => default(OrderStatusDto)
};
```

Members are matched **case-insensitively**. Unmatched members fall through to `default` and trigger [MAPO011](#mapo011-unmatched-enum-member).

### Enum to String

When a source property is an enum and the target is `string`, Mapo auto-generates `.ToString()`:

```csharp
public class Order { public OrderStatus Status { get; set; } }
public class OrderDto { public string Status { get; set; } = ""; }

// Generated: target.Status = order.Status.ToString();
```

### String to Enum

When a source property is `string` and the target is an enum, Mapo auto-generates `Enum.Parse<T>()`:

```csharp
public class OrderDto { public string Status { get; set; } = ""; }
public class Order { public OrderStatus Status { get; set; } }

// Generated: target.Status = System.Enum.Parse<OrderStatus>(orderDto.Status);
```

Invalid strings throw `ArgumentException` at runtime (consistent with how null sources throw `ArgumentNullException`). Use `AddConverter` for custom error handling.

> **Precedence:** An explicit `AddConverter` always overrides auto-detection.

---

## Records and Constructors

Mapo fully supports `record` types and parameterized constructors. It selects the **best public constructor** (most parameters matching source properties) and maps arguments by name:

```csharp
public record Source(int Id, string Name, string Email);
public record Target(int Id, string Name);

[Mapper]
public partial class RecordMapper
{
    public partial Target Map(Source source);
}

// Generated: return new Target(source.Id, source.Name);
```

### Mixing Constructors and Properties

Types with both constructor parameters and settable properties are handled:

```csharp
public record OrderDto(Guid Id, string Status)
{
    public string? Notes { get; set; }  // Set via property assignment
}
```

### Init-Only and Required Properties

Both `init` and `required` properties are set via object initializer syntax:

```csharp
public class Target
{
    public string Name { get; init; } = "";
    public required int Age { get; set; }
}

// Generated:
var target = new Target
{
    Name = source.Name,
    Age = source.Age,
};
```

---

## Nullable Types

### Nullable Reference Types

Mapo generates null-safe `?.` chains for flattened nullable navigation:

```csharp
public class Source { public Address? HomeAddress { get; set; } }
public class Target { public string HomeAddressCity { get; set; } = ""; }

// Generated: target.HomeAddressCity = source.HomeAddress?.City ?? default;
```

### Nullable Value Type Auto-Coercion

When a source property is `Nullable<T>` and the target is `T`, Mapo auto-generates `?? default`:

```csharp
public class Source { public int? Score { get; set; } }
public class Target { public int Score { get; set; } }

// Generated: target.Score = (source.Score ?? default);
```

[MAPO009](#mapo009-nullable-to-non-nullable-mapping) is emitted as an informational diagnostic. Override with `AddConverter` for custom null handling:

```csharp
// Throw instead of defaulting to 0
config.AddConverter<int?, int>(x => x ?? throw new InvalidOperationException("Score is required"));
```

---

## Update Mapping

Declare a `void` method with two parameters to mutate an existing object instead of creating a new one:

```csharp
[Mapper]
public partial class ProductMapper
{
    public partial void ApplyUpdate(ProductUpdate source, Product target);

    static void Configure(IMapConfig<ProductUpdate, Product> config)
    {
        config.Ignore(d => d.Id)         // Don't overwrite primary key
              .Ignore(d => d.CreatedAt);  // Don't overwrite audit field
    }
}
```

Usage with EF Core:

```csharp
var product = db.Products.Find(id);
mapper.ApplyUpdate(updateDto, product);
db.SaveChanges();
```

---

## Dependency Injection

Instance mappers accept constructor parameters that can be used in custom mappings:

```csharp
[Mapper(StrictMode = true)]
public partial class OrderMapper
{
    private readonly ICurrencyFormatter _formatter;
    private readonly Tenant _tenant;

    public OrderMapper(ICurrencyFormatter formatter, Tenant tenant)
    {
        _formatter = formatter;
        _tenant = tenant;
    }

    public partial OrderDto Map(Order order);

    static void Configure(IMapConfig<Order, OrderDto> config,
                          ICurrencyFormatter formatter, Tenant tenant)
    {
        config.Map(d => d.PriceDisplay,
                   s => formatter.Format(s.Total, tenant.CurrencyCode));
    }
}
```

The `Configure` method accepts the same parameters as the constructor. Mapo reads the lambda bodies at compile time and generates code referencing the instance fields (`_formatter`, `_tenant`).

Register with ASP.NET Core DI:

```csharp
builder.Services.AddSingleton<ICurrencyFormatter, CurrencyFormatter>();
builder.Services.AddScoped<Tenant>(sp => /* resolve from request context */);
builder.Services.AddTransient<OrderMapper>();
```

---

## Polymorphic Mapping

Map base class references to the correct derived DTO based on runtime type:

```csharp
[Mapper]
public partial class ShapeMapper
{
    [MapDerived(typeof(Circle), typeof(CircleDto))]
    [MapDerived(typeof(Rectangle), typeof(RectangleDto))]
    public partial ShapeDto Map(Shape shape);

    public partial CircleDto MapCircle(Circle circle);
    public partial RectangleDto MapRectangle(Rectangle rect);
}
```

Generated:

```csharp
public partial ShapeDto Map(Shape shape)
{
    if (shape == null) throw new ArgumentNullException(nameof(shape));
    switch (shape)
    {
        case Circle d: return MapCircleInternal(d);
        case Rectangle d: return MapRectangleInternal(d);
    }
    return null!;
}
```

`[MapDerived]` is `AllowMultiple = true` — add as many derived types as needed.

---

## Circular References

For types that reference each other (e.g., `User` has `List<User> Friends`), enable reference tracking:

```csharp
[Mapper(UseReferenceTracking = true)]
public partial class UserMapper
{
    public partial UserDto Map(User user);
}
```

Generated code tracks mapped objects in a `MappingContext` dictionary (using reference equality). If the same source object is encountered again, the previously mapped target is returned:

```csharp
private UserDto MapInternal(User user, MappingContext _context)
{
    if (user == null) throw new ArgumentNullException(nameof(user));
    if (_context.TryGet<UserDto>(user, out var _existing)) return _existing!;

    var target = new UserDto { Name = user.Name };
    _context.Add(user, target);  // Register before mapping children
    target.Friends = MapFriendsInternal(user.Friends, _context);
    return target;
}
```

> **Note:** `MappingContext` is created per public method call. It is not thread-safe and should not be shared.

---

## Global Type Converters

Apply a conversion rule to **all** properties of a given type pair within a mapping:

```csharp
static void Configure(IMapConfig<Order, OrderDto> config)
{
    config.AddConverter<DateTime, string>(dt => dt.ToString("yyyy-MM-dd HH:mm"));
    config.AddConverter<string, string>(s => s.Trim());
}
```

If `Order` has `CreatedAt` (`DateTime`) and `OrderDto` has `CreatedAt` (`string`), the converter is applied automatically — no explicit `.Map()` needed.

> **Precedence:** Explicit `.Map()` > `AddConverter` > auto-detection (enum↔string, nullable coercion, etc.)

---

## Reverse Mapping

Generate the inverse mapping with a single call:

```csharp
static void Configure(IMapConfig<User, UserDto> config)
{
    config.Map(d => d.FullName, s => s.FirstName + " " + s.LastName)
          .ReverseMap();
}
```

For properties that can't be automatically reversed, add a separate Configure:

```csharp
static void Configure(IMapConfig<UserDto, User> config)
{
    config.Ignore(d => d.FirstName)
          .Ignore(d => d.LastName);
}
```

---

## Async Streaming

For `static` mappers, Mapo auto-generates `IAsyncEnumerable<T>` extension methods:

```csharp
[Mapper]
public static partial class ProductMapper
{
    public static partial ProductDto Map(Product product);
}

// Auto-generated extension:
// public static async IAsyncEnumerable<ProductDto> MapStreamAsync(
//     this IAsyncEnumerable<Product> source, CancellationToken ct = default)
```

Usage with EF Core:

```csharp
await foreach (var dto in dbContext.Products.AsAsyncEnumerable().MapStreamAsync())
{
    yield return dto;  // One object in memory at a time
}
```

---

## Extension Methods

For `static` mappers with single-parameter methods, Mapo generates fluent extension methods:

```csharp
[Mapper]
public static partial class UserMapper
{
    public static partial UserDto Map(User user);
}

// Auto-generated in UserMapperExtensions:
// public static UserDto Map(this User source) => UserMapper.Map(source);

var dto = user.Map();
```

---

## Static vs Instance Mappers

| | Static Mapper | Instance Mapper |
|:--|:-------------|:----------------|
| Syntax | `public static partial class` | `public partial class` |
| Allocation | Zero (no mapper instance) | One object |
| Extension methods | Auto-generated | Not available |
| Async streaming | Auto-generated | Not available |
| Dependency injection | Not supported | Supported |
| Reference tracking | Not supported | Supported |

**Use static** for simple mappings without external dependencies.
**Use instance** when you need DI, reference tracking, or per-request state.

---

## Strict Mode

Enable `StrictMode` to turn unmapped property warnings into compile errors:

```csharp
[Mapper(StrictMode = true)]
public partial class UserMapper
{
    public partial UserDto Map(User user);
    // Compile ERROR if UserDto has any unmapped property
}
```

### Resolving Unmapped Properties

Three options:

1. **Add a custom mapping:** `.Map(d => d.Prop, s => s.Value)` in Configure
2. **Ignore it:** `.Ignore(d => d.Prop)` in Configure
3. **Match the name:** Add a property with the same name to the source type

---

## Ignoring Properties

Use `.Ignore()` to exclude properties from mapping. This silences MAPO001:

```csharp
static void Configure(IMapConfig<Order, OrderDto> config)
{
    config.Ignore(d => d.InternalNotes)
          .Ignore(d => d.AuditTrail);
}
```

---

## Generated Code

Mapo generates files named `{ClassName}.g.cs`. Inspect them by enabling:

```xml
<PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>
```

Files appear at: `obj/{Config}/{TFM}/generated/Mapo.Generator/Mapo.Generator.MapoGenerator/`

You can also press **F12** (Go to Definition) on any partial method to navigate to the generated implementation.

### What Gets Generated

For each mapping method:

1. **Public partial method** — delegates to internal implementation
2. **Private internal method** — actual mapping logic with `[MethodImpl(AggressiveInlining | AggressiveOptimization)]`
3. **Null guard** — `ArgumentNullException` for null source (or early return for update mappings)
4. **Constructor call or object initializer** — based on target type
5. **Property assignments** — with null-safe chains where needed

For static mappers, additionally:
- **Extension methods** in `{ClassName}Extensions`
- **Async streaming** `IAsyncEnumerable<T>` extensions

---

## Diagnostics Reference

| ID | Severity | Title | IDE Quick Fix |
|----|----------|-------|:-------------:|
| MAPO001 | Warning/Error | Property is not mapped | Yes |
| MAPO003 | Error | Mapper class must be partial | Yes |
| MAPO004 | Warning | Configure method has wrong signature | |
| MAPO005 | Warning | Type has no accessible properties | |
| MAPO006 | Warning | Duplicate mapping for target property | |
| MAPO007 | Warning | Invalid target property in mapping configuration | |
| MAPO008 | Warning | Invalid source expression in mapping configuration | |
| MAPO009 | Info | Nullable source mapped to non-nullable target | |
| MAPO010 | Warning | Circular reference detected without UseReferenceTracking | |
| MAPO011 | Warning | Enum member has no match in target | |

### MAPO001: Property is not mapped

A target property has no matching source property and no custom mapping.

- **Severity:** Warning (default), Error (with `StrictMode = true`)
- **IDE Quick Fix:** Ctrl+. → "Map property 'X'" inserts a `.Map()` call in Configure
- **Fix:** Add `.Map(d => d.Prop, s => s.Value)`, `.Ignore(d => d.Prop)`, or match the name

### MAPO003: Mapper class must be partial

The class has `[Mapper]` but is missing the `partial` keyword.

- **Severity:** Error
- **IDE Quick Fix:** Ctrl+. → "Add 'partial' modifier"
- **Fix:** `public class MyMapper` → `public partial class MyMapper`

### MAPO004: Configure method has wrong signature

The `Configure` method must be `static` with `IMapConfig<S,T>` as the first parameter.

- **Fix:** Add `static` modifier; ensure first parameter type is correct

### MAPO005: Type has no accessible properties

Neither source has gettable properties nor target has settable properties.

- **Fix:** Add public properties with getters (source) or setters (target)

### MAPO006: Duplicate mapping for target property

Multiple `.Map()` calls target the same property. Only the first is used.

- **Fix:** Remove the duplicate `.Map()` call

### MAPO007: Invalid target property

The target lambda in `.Map()` references a property that doesn't exist or isn't settable.

- **Fix:** Check the property name and ensure it has a setter

### MAPO008: Invalid source expression

The source lambda in `.Map()` contains syntax errors.

- **Fix:** Verify the lambda compiles independently

### MAPO009: Nullable to non-nullable mapping

A `Nullable<T>` source is mapped to a `T` target. Mapo auto-coerces with `?? default`.

- **Informational only.** Use `AddConverter` to customize null handling if needed.

### MAPO010: Circular reference without tracking

Types form a circular reference graph that will cause infinite recursion.

- **Fix:** Add `UseReferenceTracking = true` to the `[Mapper]` attribute

### MAPO011: Unmatched enum member

A source enum member has no matching member in the target enum. Falls through to `default`.

- **Fix:** Add the missing member to the target enum, or accept the `default` fallback

---

## Best Practices

### One mapper per bounded context

Group related mappings in a single mapper class. Don't create one mapper per type pair — it prevents Mapo from auto-discovering nested mappings.

```csharp
// Good: one mapper for the Order aggregate
[Mapper(StrictMode = true)]
public partial class OrderMapper
{
    public partial OrderDto Map(Order order);
    public partial OrderItemDto MapItem(OrderItem item);
    public partial ProductDto MapProduct(Product product);
}

// Avoid: scattered single-pair mappers
[Mapper] public partial class OrderToOrderDtoMapper { ... }
[Mapper] public partial class OrderItemToOrderItemDtoMapper { ... }
```

### Always enable StrictMode

StrictMode catches forgotten properties at compile time. It's your safety net when source or target types change.

```csharp
[Mapper(StrictMode = true)]  // Recommended for all production mappers
```

### Prefer static mappers when possible

Static mappers generate extension methods and async streaming extensions. They have zero allocation overhead and work well with functional-style code.

```csharp
[Mapper(StrictMode = true)]
public static partial class UserMapper
{
    public static partial UserDto Map(User user);
}

// Usage: var dto = user.Map();
```

### Use Ignore for intentionally unmapped properties

Always explicitly `Ignore` properties you don't want mapped. This documents intent and survives refactoring.

```csharp
config.Ignore(d => d.InternalField)   // Clear intent
      .Ignore(d => d.CachedValue);
```

### Keep Configure methods focused

Each Configure method handles one type pair. Keep them short. If you have complex computed logic, extract it into a private static method:

```csharp
static void Configure(IMapConfig<Order, OrderDto> config)
{
    config.Map(d => d.Total, s => CalculateTotal(s));
}

private static decimal CalculateTotal(Order order) =>
    order.Items.Sum(i => i.Price * i.Quantity);
```

### Let Mapo auto-discover nested mappers

Don't declare mapper methods you don't need to call directly. Mapo auto-generates internal methods for nested types:

```csharp
[Mapper]
public partial class OrderMapper
{
    public partial OrderDto Map(Order order);
    // Mapo auto-discovers OrderItem -> OrderItemDto and generates it internally
}
```

---

## Performance Tips

### Pre-allocated collections

Mapo generates `new List<T>(source.Count)` to avoid resize allocations. This happens automatically — no configuration needed.

### Avoid IEnumerable in source types

Use `List<T>` or `T[]` in your source types. Mapo generates devirtualized `for` loops for these. `IEnumerable<T>` falls back to slower enumeration with unknown count.

### Use static mappers for hot paths

Static mapper methods are candidates for JIT inlining. The generated `[MethodImpl(AggressiveInlining)]` hint works best on small, static methods.

### Minimize circular reference tracking

`UseReferenceTracking = true` adds a dictionary lookup per mapped object. Only enable it when your object graph actually has cycles.

### Batch with collection methods

For bulk mapping, declare an explicit collection method:

```csharp
public partial List<OrderDto> MapOrders(List<Order> orders);
```

This generates a single optimized loop instead of repeated single-object calls.

---

## Troubleshooting

### "Class must be partial" (MAPO003)

Add the `partial` keyword to your mapper class. Use the IDE quick fix (Ctrl+.) for a one-click fix.

### Generated code doesn't update after changes

Clean and rebuild. Source generators cache aggressively:

```bash
dotnet clean && dotnet build
```

In Visual Studio / Rider, restart the IDE if the generator appears stuck.

### "Property is not mapped" for a property that exists on the source

Property matching is **case-insensitive** but requires exact name correspondence. `UserName` on the source does not match `Username` on the target — the casing segments differ. Use `.Map()` for non-matching names.

### Null reference in generated code

If you see runtime `NullReferenceException` in generated code, check for nullable navigation properties without null guards. Mapo generates `?.` chains for flattened properties, but custom `.Map()` expressions use your lambda verbatim:

```csharp
// This can throw if Customer is null:
config.Map(d => d.City, s => s.Customer.Address.City);

// Fix: handle null in your lambda
config.Map(d => d.City, s => s.Customer?.Address?.City ?? "");
```

### Can't see generated files in IDE

Enable generated file output in your project:

```xml
<PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>
```

Then look in `obj/{Config}/{TFM}/generated/Mapo.Generator/`.

### Build error: "Predefined type 'System.Runtime.CompilerServices.IsExternalInit' is not defined"

This occurs on older target frameworks that don't define `init` support. Ensure your project targets `net6.0` or later. If you must target `netstandard2.0`, avoid `init` properties in your DTOs.

---

## Migrating from AutoMapper

### Configuration

```csharp
// AutoMapper
cfg.CreateMap<Order, OrderDto>()
    .ForMember(d => d.Total, opt => opt.MapFrom(s => s.Items.Sum(i => i.Price)));

// Mapo
static void Configure(IMapConfig<Order, OrderDto> config)
{
    config.Map(d => d.Total, s => s.Items.Sum(i => i.Price));
}
```

### Key Differences

| AutoMapper | Mapo |
|:-----------|:-----|
| `CreateMap<S,T>()` | `[Mapper]` attribute on partial class |
| `ForMember(d => d.X, opt => opt.MapFrom(...))` | `config.Map(d => d.X, s => ...)` |
| `ForMember(d => d.X, opt => opt.Ignore())` | `config.Ignore(d => d.X)` |
| `cfg.CreateMap<S,T>().ReverseMap()` | `config.ReverseMap()` |
| `CreateMap<S,T>().ConvertUsing(...)` | `config.AddConverter<S,T>(...)` |
| Profile classes | Multiple Configure methods in one mapper |
| `mapper.Map<TDest>(source)` | `mapper.Map(source)` (type-safe, no generic needed) |
| Runtime exceptions for missing maps | Compile-time warnings/errors |
| `AssertConfigurationIsValid()` | `StrictMode = true` (checked at build time) |

### Migration Checklist

1. Add `Mapo` NuGet package
2. Create a partial mapper class with `[Mapper(StrictMode = true)]`
3. Declare partial methods matching your `CreateMap<S,T>()` pairs
4. Convert `ForMember` calls to `.Map()` in a static `Configure` method
5. Convert `Ignore()` calls
6. Convert `ConvertUsing()` to `AddConverter()`
7. Remove AutoMapper NuGet package and DI registration
8. Build — StrictMode will catch any missed mappings

---

## Complete Example

A real-world e-commerce mapper demonstrating multiple features:

```csharp
using Mapo.Attributes;

// Domain types
public enum OrderStatus { Draft, Confirmed, Shipped, Delivered, Cancelled }

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
}

public class Customer
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public Address Address { get; set; } = new();
}

public class Product
{
    public string SKU { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public Category? Category { get; set; }
}

public class OrderItem
{
    public Product Product { get; set; } = new();
    public int Quantity { get; set; }
    public decimal Discount { get; set; }
}

public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public OrderStatus Status { get; set; }
    public Customer Customer { get; set; } = new();
    public List<OrderItem> Items { get; set; } = new();
}

// DTO types (records with constructor parameters)
public record OrderItemDto(string ProductName, string ProductSKU,
    int Quantity, decimal LineTotal);

public record OrderDto(Guid Id, string OrderNumber, string StatusLabel,
    string CustomerName, string CustomerEmail, string CustomerCity,
    decimal TotalAmount, List<OrderItemDto> Items);

// Mapper
[Mapper(StrictMode = true)]
public partial class ECommerceMapper
{
    public partial OrderDto MapOrder(Order order);
    public partial List<OrderDto> MapOrders(List<Order> orders);
    public partial OrderItemDto MapOrderItem(OrderItem item);

    static void Configure(IMapConfig<OrderItem, OrderItemDto> config)
    {
        config.Map(d => d.ProductName, s => s.Product.Name)
              .Map(d => d.ProductSKU, s => s.Product.SKU)
              .Map(d => d.LineTotal,
                   s => (s.Product.UnitPrice - s.Discount) * s.Quantity);
    }

    static void Configure(IMapConfig<Order, OrderDto> config)
    {
        config.Map(d => d.StatusLabel, s => s.Status.ToString())
              .Map(d => d.CustomerName,
                   s => s.Customer.FirstName + " " + s.Customer.LastName)
              .Map(d => d.CustomerEmail, s => s.Customer.Email)
              .Map(d => d.CustomerCity, s => s.Customer.Address.City)
              .Map(d => d.TotalAmount,
                   s => s.Items.Sum(x =>
                       (x.Product.UnitPrice - x.Discount) * x.Quantity));
    }
}
```

This single mapper handles:
- **Flattening** — `Customer.Address.City` to `CustomerCity`
- **Enum to string** — `Status.ToString()` for `StatusLabel`
- **Computed values** — `LineTotal` and `TotalAmount` calculations
- **String concatenation** — `FirstName + " " + LastName`
- **Collection mapping** — `List<OrderItem>` to `List<OrderItemDto>` with auto-discovery
- **Constructor mapping** — record DTOs with positional parameters

Usage:

```csharp
var mapper = new ECommerceMapper();

var orderDto = mapper.MapOrder(order);         // Single
var orderDtos = mapper.MapOrders(orders);       // Batch (pre-allocated)
```
