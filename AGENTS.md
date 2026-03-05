# Mapo — AI Agent Reference

This file is a self-contained reference for AI coding assistants (Claude, Gemini, Copilot, Cursor, etc.) to generate correct Mapo mapping code. No external documentation or repository access is needed.

## What is Mapo?

Mapo is a **compile-time object mapper for .NET** powered by a Roslyn source generator. You write partial method declarations; Mapo generates the implementations at build time. Zero reflection, zero allocation overhead, NativeAOT compatible.

**Install:** `dotnet add package Mapo`
**Requires:** .NET 6+ with .NET SDK 8+

---

## Core Concepts

1. Decorate a `partial class` with `[Mapper]`
2. Declare `partial` methods with source → target signatures
3. Optionally add `static void Configure(IMapConfig<TSource, TTarget> config)` for customization
4. Build. Mapo generates the method bodies.

---

## Quick Start

```csharp
using Mapo.Attributes;

public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
}

public class UserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
}

[Mapper]
public partial class UserMapper
{
    public partial UserDto Map(User user);

    static void Configure(IMapConfig<User, UserDto> config)
    {
        config.Map(d => d.FullName, s => s.FirstName + " " + s.LastName);
    }
}

// Usage:
var mapper = new UserMapper();
var dto = mapper.Map(user);
```

---

## API Reference

### Attributes

| Attribute | Target | Purpose |
|:----------|:-------|:--------|
| `[Mapper]` | Class | Marks a partial class as a mapper |
| `[Mapper(StrictMode = true)]` | Class | Unmapped properties become compile errors |
| `[Mapper(UseReferenceTracking = true)]` | Class | Enables circular reference safety |
| `[MapDerived(typeof(Source), typeof(Target))]` | Method | Registers a polymorphic derived type mapping |

### IMapConfig&lt;TSource, TTarget&gt; Methods

| Method | Purpose |
|:-------|:--------|
| `.Map(d => d.Prop, s => expression)` | Custom mapping for a target property |
| `.Ignore(d => d.Prop)` | Exclude a target property from mapping |
| `.AddConverter<TIn, TOut>(x => expression)` | Global type converter for all matching properties |
| `.ReverseMap()` | Auto-generate the inverse mapping |

---

## All Mapping Patterns

### 1. Auto Name Matching

Properties with the same name (case-insensitive) and compatible type are mapped automatically. No configuration needed.

```csharp
public class Source { public int Id { get; set; } public string Name { get; set; } = ""; }
public class Target { public int Id { get; set; } public string Name { get; set; } = ""; }

[Mapper]
public partial class M { public partial Target Map(Source s); }
```

### 2. Custom Computed Mappings

```csharp
static void Configure(IMapConfig<Order, OrderDto> config)
{
    config.Map(d => d.Total, s => s.Items.Sum(i => i.Price * i.Quantity))
          .Map(d => d.CustomerName, s => s.Customer.FirstName + " " + s.Customer.LastName)
          .Map(d => d.StatusLabel, s => s.Status.ToString());
}
```

Any valid C# expression works: method calls, LINQ, ternary, string interpolation, nested navigation.

### 3. Flattening (Automatic, Up to 4 Levels)

Target property names that concatenate a navigation path are auto-resolved:

```csharp
public class Customer
{
    public Address Address { get; set; } = new();
}
public class Address
{
    public string City { get; set; } = "";
    public Country Country { get; set; } = new();
}
public class Country { public string Name { get; set; } = ""; }

public class CustomerDto
{
    public string AddressCity { get; set; } = "";               // 2-level: Address.City
    public string AddressCountryName { get; set; } = "";        // 3-level: Address.Country.Name
}
```

Generated code uses null-safe chains: `source.Address?.City ?? default`.

### 4. Enum Mapping

**Enum → Enum** (different types, matching member names): generates a `switch` expression.

```csharp
public enum Status { Active, Inactive }
public enum StatusDto { Active, Inactive }
// Auto-mapped via switch expression
```

**Enum → String**: auto-generates `.ToString()`.

**String → Enum**: auto-generates `Enum.Parse<T>()`.

### 5. Nullable Value Type Coercion

`Nullable<T>` → `T` auto-generates `?? default`:

```csharp
public class Source { public int? Score { get; set; } }
public class Target { public int Score { get; set; } }
// Generated: target.Score = (source.Score ?? default);
```

### 6. Records and Constructor Mapping

Mapo selects the best public constructor (most matching parameters) and maps by parameter name:

```csharp
public record OrderDto(Guid Id, string CustomerName, decimal Total);

[Mapper]
public partial class OrderMapper
{
    public partial OrderDto Map(Order order);

    static void Configure(IMapConfig<Order, OrderDto> config)
    {
        config.Map(d => d.CustomerName, s => s.Customer.Name)
              .Map(d => d.Total, s => s.Lines.Sum(l => l.Amount));
    }
}
```

Init-only and required properties are set via object initializer.

### 7. Collection Mapping

`List<T>`, `T[]`, and `IEnumerable<T>` are mapped automatically. Mapo auto-discovers the element mapping.

```csharp
[Mapper]
public partial class OrderMapper
{
    public partial OrderDto Map(Order order);           // Lines auto-discovered
    public partial List<OrderDto> MapAll(List<Order> orders);  // Batch
}
```

Generated code uses pre-allocated `new List<T>(source.Count)` and devirtualized `for` loops.

### 8. Update (Void) Mapping

Mutate an existing object instead of creating a new one:

```csharp
[Mapper]
public partial class ProductMapper
{
    public partial void ApplyUpdate(ProductUpdate source, Product target);

    static void Configure(IMapConfig<ProductUpdate, Product> config)
    {
        config.Ignore(d => d.Id)
              .Ignore(d => d.CreatedAt);
    }
}
```

### 9. Ignore Properties

```csharp
static void Configure(IMapConfig<Source, Target> config)
{
    config.Ignore(d => d.InternalField)
          .Ignore(d => d.CachedValue);
}
```

### 10. Global Type Converters

Apply a conversion rule to all properties matching a type pair:

```csharp
static void Configure(IMapConfig<Order, OrderDto> config)
{
    config.AddConverter<DateTime, string>(dt => dt.ToString("yyyy-MM-dd"));
}
// All DateTime→string properties in this mapping use the converter
```

Precedence: explicit `.Map()` > `AddConverter` > auto-detection.

### 11. Dependency Injection

Instance mappers accept constructor parameters usable in Configure:

```csharp
[Mapper]
public partial class OrderMapper
{
    private readonly ICurrencyFormatter _formatter;
    public OrderMapper(ICurrencyFormatter formatter) { _formatter = formatter; }

    public partial OrderDto Map(Order order);

    static void Configure(IMapConfig<Order, OrderDto> config, ICurrencyFormatter formatter)
    {
        config.Map(d => d.PriceDisplay, s => formatter.Format(s.Total));
    }
}
```

Configure parameters match constructor parameters. Lambdas are parsed at compile time.

### 12. Static Mappers (Extension Methods + Async Streaming)

```csharp
[Mapper]
public static partial class ProductMapper
{
    public static partial ProductDto Map(Product product);
}

// Auto-generated:
// product.Map()                                          — extension method
// products.AsAsyncEnumerable().MapStreamAsync()          — IAsyncEnumerable streaming
```

Static mappers cannot use DI or reference tracking.

### 13. Reverse Mapping

```csharp
static void Configure(IMapConfig<Product, ProductSummary> config)
{
    config.ReverseMap();  // Generates ProductSummary → Product mapper
}
```

### 14. Polymorphic Dispatch

```csharp
[Mapper]
public partial class ShapeMapper
{
    [MapDerived(typeof(Circle), typeof(CircleDto))]
    [MapDerived(typeof(Rectangle), typeof(RectangleDto))]
    public partial ShapeDto Map(Shape shape);

    public partial CircleDto MapCircle(Circle circle);
    public partial RectangleDto MapRect(Rectangle rect);
}
```

Generated code uses `switch` on runtime type.

### 15. Circular References

```csharp
[Mapper(UseReferenceTracking = true)]
public partial class UserMapper
{
    public partial UserDto Map(User user);
}
```

Uses a per-call `MappingContext` dictionary to detect cycles.

### 16. Strict Mode

```csharp
[Mapper(StrictMode = true)]
public partial class SafeMapper
{
    public partial TargetDto Map(Source source);
    // Compile ERROR if any TargetDto property is unmapped
}
```

---

## Diagnostics

| ID | Severity | Meaning | Fix |
|----|----------|---------|-----|
| MAPO001 | Warning/Error | Unmapped target property | Add `.Map()`, `.Ignore()`, or match the name |
| MAPO003 | Error | Class not partial | Add `partial` keyword |
| MAPO004 | Warning | Configure method wrong signature | Make it `static`, first param `IMapConfig<S,T>` |
| MAPO005 | Warning | No accessible properties | Add public properties |
| MAPO006 | Warning | Duplicate mapping for property | Remove duplicate `.Map()` |
| MAPO009 | Info | Nullable → non-nullable coercion | Auto-handled with `?? default` |
| MAPO010 | Warning | Circular reference without tracking | Add `UseReferenceTracking = true` |
| MAPO011 | Warning | Unmatched enum member | Add member to target or accept default |

---

## Common Patterns

### E-Commerce Mapper (Full Example)

```csharp
using Mapo.Attributes;

public class Customer
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public Address Address { get; set; } = new();
}

public class Address { public string City { get; set; } = ""; }

public class OrderLine
{
    public Product Product { get; set; } = new();
    public int Quantity { get; set; }
    public decimal Discount { get; set; }
}

public class Product
{
    public string Name { get; set; } = "";
    public decimal UnitPrice { get; set; }
}

public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public OrderStatus Status { get; set; }
    public Customer Customer { get; set; } = new();
    public List<OrderLine> Lines { get; set; } = [];
}

public enum OrderStatus { Draft, Confirmed, Shipped }

// DTOs
public record OrderLineDto(string ProductName, int Quantity, decimal LineTotal);
public record OrderDto
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; } = "";
    public string Status { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string CustomerCity { get; init; } = "";
    public decimal TotalAmount { get; init; }
    public List<OrderLineDto> Lines { get; init; } = [];
}

[Mapper(StrictMode = true)]
public partial class ECommerceMapper
{
    public partial OrderDto MapOrder(Order order);
    public partial List<OrderDto> MapOrders(List<Order> orders);
    public partial OrderLineDto MapLine(OrderLine line);

    static void Configure(IMapConfig<OrderLine, OrderLineDto> config)
    {
        config.Map(d => d.ProductName, s => s.Product.Name)
              .Map(d => d.LineTotal, s => (s.Product.UnitPrice - s.Discount) * s.Quantity);
    }

    static void Configure(IMapConfig<Order, OrderDto> config)
    {
        config.Map(d => d.Status, s => s.Status.ToString())
              .Map(d => d.CustomerName, s => s.Customer.FirstName + " " + s.Customer.LastName)
              .Map(d => d.CustomerCity, s => s.Customer.Address.City)
              .Map(d => d.TotalAmount, s => s.Lines.Sum(l => (l.Product.UnitPrice - l.Discount) * l.Quantity));
    }
}
```

### DI with ASP.NET Core

```csharp
// Register
builder.Services.AddSingleton<ICurrencyFormatter, CurrencyFormatter>();
builder.Services.AddTransient<OrderMapper>();

// Use in controller
public class OrdersController(OrderMapper mapper) : ControllerBase
{
    [HttpGet("{id}")]
    public OrderDto Get(int id)
    {
        var order = _db.Orders.Find(id);
        return mapper.Map(order);
    }
}
```

### EF Core Update Pattern

```csharp
[Mapper]
public partial class ProductMapper
{
    public partial void ApplyUpdate(ProductUpdateDto source, Product target);

    static void Configure(IMapConfig<ProductUpdateDto, Product> config)
    {
        config.Ignore(d => d.Id)
              .Ignore(d => d.CreatedAt)
              .Ignore(d => d.CreatedBy);
    }
}

// In handler:
var product = await db.Products.FindAsync(id);
mapper.ApplyUpdate(dto, product);
await db.SaveChangesAsync();
```

---

## Rules for AI Agents

When generating Mapo code, follow these rules:

1. **Always use `partial`** on both the class and the method declarations
2. **Always add `using Mapo.Attributes;`**
3. **Prefer `StrictMode = true`** for production mappers — catches missed properties at compile time
4. **Group related mappings in one class** — Mapo auto-discovers nested type mappings within the same mapper
5. **Use `.Ignore()` explicitly** for intentionally unmapped properties — documents intent
6. **Use `static` mappers** when no DI or reference tracking is needed — generates extension methods
7. **Configure is parsed at compile time** — lambda bodies become generated code, they are never called at runtime
8. **Configure parameters after `IMapConfig` must match constructor parameters** for DI
9. **One Configure per type pair** — `static void Configure(IMapConfig<TSource, TTarget> config)`
10. **Do NOT create one mapper per type pair** — group by bounded context so nested discovery works

## CLI Tool

```bash
dotnet tool install --global Mapo.Cli
mapo gen <input-dir> <output-dir>
```

Scans `.cs` files for `[Mapper]` classes and writes `.g.cs` files. Same output as the source generator.
