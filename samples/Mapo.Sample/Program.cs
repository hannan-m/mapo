using System.Runtime.CompilerServices;
using Mapo.Attributes;

namespace Mapo.Sample;

// =============================================================================
// DOMAIN MODELS
// =============================================================================

public enum OrderStatus { Draft, Confirmed, Processing, Shipped, Delivered, Cancelled }
public enum OrderStatusLabel { Draft, Confirmed, Processing, Shipped, Delivered, Cancelled }
public enum MembershipTier { Free, Bronze, Silver, Gold, Platinum }

// Deep nesting for multi-level flattening
public class Country
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
}

public class Region
{
    public string Name { get; set; } = "";
    public Country Country { get; set; } = new();
}

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string ZipCode { get; set; } = "";
    public Region Region { get; set; } = new();
}

public class Customer
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public MembershipTier Tier { get; set; }
    public int? LoyaltyPoints { get; set; }
    public Address ShippingAddress { get; set; } = new();
}

public class Product
{
    public Guid Id { get; set; }
    public string SKU { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal UnitPrice { get; set; }
    public string InternalNotes { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string[] Tags { get; set; } = [];
}

public class OrderLine
{
    public Product Product { get; set; } = new();
    public int Quantity { get; set; }
    public decimal Discount { get; set; }
}

public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public DateTime PlacedAt { get; set; }
    public OrderStatus Status { get; set; }
    public Customer Customer { get; set; } = null!;
    public List<OrderLine> Lines { get; set; } = [];
    public Address ShippingAddress { get; set; } = new();
}

// Update DTO for void mapping
public class ProductUpdateDto
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal UnitPrice { get; set; }
    public string[] Tags { get; set; } = [];
}

// String-to-enum source
public class OrderFilterDto
{
    public string Status { get; set; } = "";
}

// String-to-enum target
public class OrderFilter
{
    public OrderStatus Status { get; set; }
}

// =============================================================================
// DTOs
// =============================================================================

// Init-only and required properties
public class CustomerDto
{
    public required string FullName { get; init; }
    public required string Email { get; init; }
    public string Tier { get; init; } = "";                              // Enum -> string auto
    public int LoyaltyPoints { get; init; }                              // Nullable auto-coercion (int? -> int)
    public string ShippingAddressCity { get; init; } = "";               // 2-level flattening
    public string ShippingAddressRegionName { get; init; } = "";         // 3-level flattening
    public string ShippingAddressRegionCountryName { get; init; } = "";  // 4-level flattening
}

// Record with constructor (constructor mapping)
public record OrderLineDto(
    string ProductName,
    string ProductSKU,
    int Quantity,
    decimal LineTotal);

public record OrderDto
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; } = "";
    public OrderStatusLabel Status { get; init; }                 // Enum -> enum auto
    public string PlacedAt { get; init; } = "";                   // DateTime -> string via AddConverter
    public string CustomerName { get; init; } = "";               // Computed
    public decimal TotalAmount { get; init; }                     // Computed
    public string TotalDisplay { get; init; } = "";               // DI: formatter.Format(...)
    public List<OrderLineDto> Lines { get; init; } = [];          // Collection mapping
    public string ShippingCity { get; init; } = "";               // Custom mapping
}

// Simple DTO for static mapper
public class ProductSummaryDto
{
    public string Name { get; set; } = "";
    public string PriceDisplay { get; set; } = "";
}

// Reverse mapping types
public class ProductSummary
{
    public string Name { get; set; } = "";
    public decimal UnitPrice { get; set; }
}

// =============================================================================
// SERVICES (for dependency injection)
// =============================================================================

public interface ICurrencyFormatter
{
    string Format(decimal amount);
}

public class CurrencyFormatter : ICurrencyFormatter
{
    public string Format(decimal amount) => amount.ToString("C");
}

// =============================================================================
// MAPPERS
// =============================================================================

// --- Static Mapper: extension methods, async streaming, reverse mapping ---
[Mapper]
public static partial class ProductStaticMapper
{
    public static partial ProductSummaryDto MapToDto(Product product);
    public static partial ProductSummary MapToSummary(Product product);
    public static partial Product MapFromSummary(ProductSummary summary);

    static void Configure(IMapConfig<Product, ProductSummaryDto> config)
    {
        config.Map(d => d.PriceDisplay, s => s.UnitPrice.ToString("C"));
    }

    static void Configure(IMapConfig<Product, ProductSummary> config)
    {
        config.ReverseMap();
    }
}

// --- Instance Mapper: DI, flattening, nullable, enum conversions, converters, update, ignore, strict ---
[Mapper(StrictMode = true)]
public partial class ECommerceMapper
{
    private readonly ICurrencyFormatter _formatter;

    public ECommerceMapper(ICurrencyFormatter formatter)
    {
        _formatter = formatter;
    }

    public partial CustomerDto MapCustomer(Customer customer);
    public partial OrderDto MapOrder(Order order);
    public partial List<OrderDto> MapOrders(List<Order> orders);
    public partial OrderLineDto MapOrderLine(OrderLine line);
    public partial void ApplyUpdate(ProductUpdateDto source, Product target);
    public partial OrderFilter MapFilter(OrderFilterDto filter);

    static void Configure(IMapConfig<Customer, CustomerDto> config)
    {
        config.Map(d => d.FullName, s => s.FirstName + " " + s.LastName);
        // Auto features (no explicit .Map() needed):
        //   Email          — auto-matched by name
        //   Tier           — MembershipTier -> string (enum-to-string auto)
        //   LoyaltyPoints  — int? -> int (nullable auto-coercion with ?? default)
        //   ShippingAddressCity              — 2-level flattening
        //   ShippingAddressRegionName        — 3-level deep flattening
        //   ShippingAddressRegionCountryName — 4-level deep flattening
    }

    static void Configure(IMapConfig<OrderLine, OrderLineDto> config)
    {
        config.Map(d => d.ProductName, s => s.Product.Name)
              .Map(d => d.ProductSKU, s => s.Product.SKU)
              .Map(d => d.LineTotal, s => (s.Product.UnitPrice - s.Discount) * s.Quantity);
    }

    static void Configure(IMapConfig<Order, OrderDto> config, ICurrencyFormatter formatter)
    {
        config
            .Map(d => d.CustomerName, s => s.Customer.FirstName + " " + s.Customer.LastName)
            .Map(d => d.TotalAmount, s => s.Lines.Sum(l => (l.Product.UnitPrice - l.Discount) * l.Quantity))
            .Map(d => d.TotalDisplay, s => formatter.Format(s.Lines.Sum(l => (l.Product.UnitPrice - l.Discount) * l.Quantity)))
            .Map(d => d.ShippingCity, s => s.ShippingAddress.City)
            // Global type converter: all DateTime -> string properties in this mapping
            .AddConverter<DateTime, string>(dt => dt.ToString("yyyy-MM-dd"));
        // Auto features:
        //   Id          — auto-matched
        //   OrderNumber — auto-matched
        //   Status      — OrderStatus -> OrderStatusLabel (enum-to-enum auto)
        //   PlacedAt    — DateTime -> string (via AddConverter above)
        //   Lines       — List<OrderLine> -> List<OrderLineDto> (collection auto-discovery)
    }

    // Update mapping: preserve Id, SKU, InternalNotes, CreatedAt
    static void Configure(IMapConfig<ProductUpdateDto, Product> config)
    {
        config.Ignore(d => d.Id)
              .Ignore(d => d.SKU)
              .Ignore(d => d.InternalNotes)
              .Ignore(d => d.CreatedAt);
    }
}

// =============================================================================
// PROGRAM
// =============================================================================

public class Program
{
    public static async Task Main()
    {
        Console.WriteLine("=== Mapo Feature Showcase ===\n");

        // --- Test Data ---
        var product = new Product
        {
            Id = Guid.NewGuid(),
            SKU = "WM-001",
            Name = "Wireless Mouse",
            Description = "Ergonomic wireless mouse",
            UnitPrice = 29.99m,
            InternalNotes = "Supplier: Acme Corp",
            CreatedAt = new DateTime(2025, 1, 15),
            Tags = ["electronics", "peripherals", "wireless"]
        };

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com",
            Tier = MembershipTier.Gold,
            LoyaltyPoints = 1250,
            ShippingAddress = new Address
            {
                Street = "123 Main St",
                City = "Seattle",
                ZipCode = "98101",
                Region = new Region
                {
                    Name = "Pacific Northwest",
                    Country = new Country { Name = "United States", Code = "US" }
                }
            }
        };

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-2025-001",
            PlacedAt = new DateTime(2025, 3, 1),
            Status = OrderStatus.Shipped,
            Customer = customer,
            ShippingAddress = customer.ShippingAddress,
            Lines =
            [
                new OrderLine { Product = product, Quantity = 2, Discount = 5.00m },
                new OrderLine
                {
                    Product = new Product { SKU = "KB-002", Name = "Mechanical Keyboard", UnitPrice = 89.99m },
                    Quantity = 1,
                    Discount = 0m
                }
            ]
        };

        var formatter = new CurrencyFormatter();
        var mapper = new ECommerceMapper(formatter);

        // ── 1. Basic Auto-Matching + Init/Required Properties ──
        Console.WriteLine("-- 1. Basic Auto-Matching + Init/Required Properties --");
        var customerDto = mapper.MapCustomer(customer);
        Console.WriteLine($"  FullName (required init): {customerDto.FullName}");
        Console.WriteLine($"  Email (required init): {customerDto.Email}");

        // ── 2. Enum-to-String (auto) ──
        Console.WriteLine("\n-- 2. Enum-to-String (auto) --");
        Console.WriteLine($"  MembershipTier.{customer.Tier} -> \"{customerDto.Tier}\"");

        // ── 3. Enum-to-Enum ──
        Console.WriteLine("\n-- 3. Enum-to-Enum --");
        var orderDto = mapper.MapOrder(order);
        Console.WriteLine($"  OrderStatus.{order.Status} -> OrderStatusLabel.{orderDto.Status}");

        // ── 4. String-to-Enum (auto) ──
        Console.WriteLine("\n-- 4. String-to-Enum (auto) --");
        var filterDto = new OrderFilterDto { Status = "Confirmed" };
        var filter = mapper.MapFilter(filterDto);
        Console.WriteLine($"  \"{filterDto.Status}\" -> OrderStatus.{filter.Status}");

        // ── 5. Nullable Auto-Coercion (int? -> int) ──
        Console.WriteLine("\n-- 5. Nullable Auto-Coercion (int? -> int) --");
        Console.WriteLine($"  1250 (int?) -> {customerDto.LoyaltyPoints} (int)");
        var nullCustomer = new Customer
        {
            FirstName = "No", LastName = "Points", Email = "n@a.com",
            LoyaltyPoints = null,
            ShippingAddress = new Address { Region = new Region { Country = new Country() } }
        };
        var nullDto = mapper.MapCustomer(nullCustomer);
        Console.WriteLine($"  null (int?) -> {nullDto.LoyaltyPoints} (default)");

        // ── 6. Deep Flattening (2-4 levels) ──
        Console.WriteLine("\n-- 6. Deep Flattening (2-4 levels) --");
        Console.WriteLine($"  2-level: ShippingAddressCity = {customerDto.ShippingAddressCity}");
        Console.WriteLine($"  3-level: ShippingAddressRegionName = {customerDto.ShippingAddressRegionName}");
        Console.WriteLine($"  4-level: ShippingAddressRegionCountryName = {customerDto.ShippingAddressRegionCountryName}");

        // ── 7. Custom Computed Mappings ──
        Console.WriteLine("\n-- 7. Custom Computed Mappings --");
        Console.WriteLine($"  CustomerName: {orderDto.CustomerName}");
        Console.WriteLine($"  TotalAmount: {orderDto.TotalAmount:C}");

        // ── 8. Dependency Injection ──
        Console.WriteLine("\n-- 8. Dependency Injection --");
        Console.WriteLine($"  TotalDisplay (via ICurrencyFormatter): {orderDto.TotalDisplay}");

        // ── 9. Global Type Converter (DateTime -> string) ──
        Console.WriteLine("\n-- 9. Global Type Converter (DateTime -> string) --");
        Console.WriteLine($"  PlacedAt: {orderDto.PlacedAt}");

        // ── 10. Collection Mapping (auto-discovery) ──
        Console.WriteLine("\n-- 10. Collection Mapping (auto-discovery) --");
        Console.WriteLine($"  {orderDto.Lines.Count} order lines:");
        foreach (var line in orderDto.Lines)
            Console.WriteLine($"    {line.ProductName} x{line.Quantity} = {line.LineTotal:C}");

        // ── 11. Batch Collection Mapping ──
        Console.WriteLine("\n-- 11. Batch Collection Mapping --");
        var orders = mapper.MapOrders([order]);
        Console.WriteLine($"  Mapped {orders.Count} order(s) in batch");

        // ── 12. Static Mapper + Extension Methods ──
        Console.WriteLine("\n-- 12. Static Mapper + Extension Methods --");
        var summaryDto = ProductStaticMapper.MapToDto(product);
        Console.WriteLine($"  Direct call: {summaryDto.Name} ({summaryDto.PriceDisplay})");
        var summaryDto2 = product.MapToDto();
        Console.WriteLine($"  Extension:   {summaryDto2.Name} ({summaryDto2.PriceDisplay})");

        // ── 13. Async Streaming (IAsyncEnumerable) ──
        Console.WriteLine("\n-- 13. Async Streaming (IAsyncEnumerable) --");
        var products = new List<Product>
        {
            product,
            new Product { Name = "USB Hub", UnitPrice = 45.50m }
        };
        int streamCount = 0;
        await foreach (var dto in ToAsyncEnumerable(products).MapToDtoStreamAsync())
        {
            streamCount++;
            Console.WriteLine($"  Streamed: {dto.Name}");
        }
        Console.WriteLine($"  Total: {streamCount} items");

        // ── 14. Reverse Mapping (ReverseMap) ──
        Console.WriteLine("\n-- 14. Reverse Mapping (ReverseMap) --");
        var summary = ProductStaticMapper.MapToSummary(product);
        Console.WriteLine($"  Product -> Summary: {summary.Name}, {summary.UnitPrice:C}");
        var roundTrip = ProductStaticMapper.MapFromSummary(summary);
        Console.WriteLine($"  Summary -> Product: {roundTrip.Name}, {roundTrip.UnitPrice:C}");

        // ── 15. Update (Void) Mapping + Ignore ──
        Console.WriteLine("\n-- 15. Update (Void) Mapping + Ignore --");
        Console.WriteLine($"  Before: {product.Name}, {product.UnitPrice:C}, SKU={product.SKU}");
        var update = new ProductUpdateDto
        {
            Name = "Wireless Mouse Pro",
            Description = "Upgraded ergonomic mouse",
            UnitPrice = 39.99m,
            Tags = ["electronics", "pro"]
        };
        mapper.ApplyUpdate(update, product);
        Console.WriteLine($"  After:  {product.Name}, {product.UnitPrice:C}, SKU={product.SKU} (preserved)");
        Console.WriteLine($"  CreatedAt preserved: {product.CreatedAt:yyyy-MM-dd}");

        Console.WriteLine("\n-- 16. Strict Mode --");
        Console.WriteLine("  ECommerceMapper has StrictMode=true");
        Console.WriteLine("  All unmapped properties cause compile-time errors");

        Console.WriteLine("\nAll features demonstrated successfully.");
    }

    static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
        IEnumerable<T> items,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return item;
        }
    }
}
