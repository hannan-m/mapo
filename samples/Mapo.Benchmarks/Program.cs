using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using Mapster;

namespace Mapo.Benchmarks;

// ──────────────────────────────────────────────
// Domain Models
// ──────────────────────────────────────────────

#region Domain Models

public enum OrderStatus
{
    Draft,
    Confirmed,
    Shipped,
    Delivered,
    Cancelled,
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Category? Parent { get; set; }
}

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string ZipCode { get; set; } = "";
}

public class Customer
{
    public Guid Id { get; set; }
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
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public Customer Customer { get; set; } = new();
    public List<OrderItem> Items { get; set; } = new();
}

#endregion

// ──────────────────────────────────────────────
// DTOs
// ──────────────────────────────────────────────

#region DTOs

public record ProductDto(string SKU, string Name, string PriceDisplay, string CategoryName, string? ParentCategoryName);

public record OrderItemDto(string ProductName, string ProductSKU, int Quantity, decimal LineTotal);

public record OrderDto(
    Guid Id,
    string OrderNumber,
    string StatusLabel,
    string CustomerName,
    string CustomerEmail,
    string CustomerCity,
    decimal TotalAmount,
    List<OrderItemDto> Items
);

#endregion

// ──────────────────────────────────────────────
// Manual Mapper (baseline)
// ──────────────────────────────────────────────

#region Manual Mapper

public static class ManualMapper
{
    public static ProductDto MapProduct(Product p)
    {
        return new ProductDto(
            p.SKU,
            p.Name,
            p.UnitPrice.ToString("C"),
            p.Category?.Name ?? "",
            p.Category?.Parent?.Name
        );
    }

    public static OrderItemDto MapOrderItem(OrderItem item)
    {
        return new OrderItemDto(
            item.Product.Name,
            item.Product.SKU,
            item.Quantity,
            (item.Product.UnitPrice - item.Discount) * item.Quantity
        );
    }

    public static OrderDto MapOrder(Order order)
    {
        var items = new List<OrderItemDto>(order.Items.Count);
        for (int i = 0; i < order.Items.Count; i++)
            items.Add(MapOrderItem(order.Items[i]));

        return new OrderDto(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.Customer.FirstName + " " + order.Customer.LastName,
            order.Customer.Email,
            order.Customer.Address.City,
            order.Items.Sum(x => (x.Product.UnitPrice - x.Discount) * x.Quantity),
            items
        );
    }

    public static List<OrderDto> MapOrders(List<Order> orders)
    {
        var results = new List<OrderDto>(orders.Count);
        for (int i = 0; i < orders.Count; i++)
            results.Add(MapOrder(orders[i]));
        return results;
    }
}

#endregion

// ──────────────────────────────────────────────
// Mapo Mapper (source generator)
// ──────────────────────────────────────────────

#region Mapo Mapper

[Mapo.Attributes.Mapper(StrictMode = true)]
public partial class MapoBenchmarkMapper
{
    public partial OrderDto MapOrder(Order order);

    public partial List<OrderDto> MapOrders(List<Order> orders);

    public partial OrderItemDto MapOrderItem(OrderItem item);

    public partial ProductDto MapProduct(Product product);

    static void Configure(Mapo.Attributes.IMapConfig<Product, ProductDto> config)
    {
        config
            .Map(d => d.PriceDisplay, s => s.UnitPrice.ToString("C"))
            .Map(d => d.CategoryName, s => s.Category.Name)
            .Map(d => d.ParentCategoryName, s => s.Category.Parent.Name);
    }

    static void Configure(Mapo.Attributes.IMapConfig<OrderItem, OrderItemDto> config)
    {
        config
            .Map(d => d.ProductName, s => s.Product.Name)
            .Map(d => d.ProductSKU, s => s.Product.SKU)
            .Map(d => d.LineTotal, s => (s.Product.UnitPrice - s.Discount) * s.Quantity);
    }

    static void Configure(Mapo.Attributes.IMapConfig<Order, OrderDto> config)
    {
        config
            .Map(d => d.StatusLabel, s => s.Status.ToString())
            .Map(d => d.CustomerName, s => s.Customer.FirstName + " " + s.Customer.LastName)
            .Map(d => d.CustomerEmail, s => s.Customer.Email)
            .Map(d => d.CustomerCity, s => s.Customer.Address.City)
            .Map(d => d.TotalAmount, s => s.Items.Sum(x => (x.Product.UnitPrice - x.Discount) * x.Quantity));
    }
}

#endregion

// ──────────────────────────────────────────────
// AutoMapper Setup (reflection-based)
// ──────────────────────────────────────────────

#region AutoMapper Setup

public static class AutoMapperSetup
{
    public static AutoMapper.IMapper CreateMapper()
    {
        var config = new AutoMapper.MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Product, ProductDto>()
                .ConstructUsing(s => new ProductDto(
                    s.SKU,
                    s.Name,
                    s.UnitPrice.ToString("C"),
                    s.Category != null ? s.Category.Name : "",
                    s.Category != null && s.Category.Parent != null ? s.Category.Parent.Name : null
                ));

            cfg.CreateMap<OrderItem, OrderItemDto>()
                .ConstructUsing(s => new OrderItemDto(
                    s.Product.Name,
                    s.Product.SKU,
                    s.Quantity,
                    (s.Product.UnitPrice - s.Discount) * s.Quantity
                ));

            cfg.CreateMap<Order, OrderDto>()
                .ConstructUsing(
                    (s, ctx) =>
                        new OrderDto(
                            s.Id,
                            s.OrderNumber,
                            s.Status.ToString(),
                            s.Customer.FirstName + " " + s.Customer.LastName,
                            s.Customer.Email,
                            s.Customer.Address.City,
                            s.Items.Sum(x => (x.Product.UnitPrice - x.Discount) * x.Quantity),
                            ctx.Mapper.Map<List<OrderItemDto>>(s.Items)
                        )
                );
        });

        return config.CreateMapper();
    }
}

#endregion

// ──────────────────────────────────────────────
// Mapperly Mapper (source generator)
// ──────────────────────────────────────────────

#region Mapperly Mapper

[Riok.Mapperly.Abstractions.Mapper]
public partial class MapperlyBenchmarkMapper
{
    // Product: source-generated with attribute-driven custom mappings
    [Riok.Mapperly.Abstractions.MapProperty(
        nameof(Product.UnitPrice),
        nameof(ProductDto.PriceDisplay),
        Use = nameof(FormatPrice)
    )]
    [Riok.Mapperly.Abstractions.MapProperty(
        new[] { nameof(Product.Category), nameof(Category.Name) },
        new[] { nameof(ProductDto.CategoryName) }
    )]
    [Riok.Mapperly.Abstractions.MapProperty(
        new[] { nameof(Product.Category), nameof(Category.Parent), nameof(Category.Name) },
        new[] { nameof(ProductDto.ParentCategoryName) }
    )]
    public partial ProductDto MapProduct(Product product);

    // OrderItem: manual (LineTotal is computed)
    public OrderItemDto MapOrderItem(OrderItem item)
    {
        return new OrderItemDto(
            item.Product.Name,
            item.Product.SKU,
            item.Quantity,
            (item.Product.UnitPrice - item.Discount) * item.Quantity
        );
    }

    // Order: manual (multiple computed/aggregated fields)
    public OrderDto MapOrder(Order order)
    {
        var items = new List<OrderItemDto>(order.Items.Count);
        for (int i = 0; i < order.Items.Count; i++)
            items.Add(MapOrderItem(order.Items[i]));

        return new OrderDto(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.Customer.FirstName + " " + order.Customer.LastName,
            order.Customer.Email,
            order.Customer.Address.City,
            order.Items.Sum(x => (x.Product.UnitPrice - x.Discount) * x.Quantity),
            items
        );
    }

    public List<OrderDto> MapOrders(List<Order> orders)
    {
        var result = new List<OrderDto>(orders.Count);
        for (int i = 0; i < orders.Count; i++)
            result.Add(MapOrder(orders[i]));
        return result;
    }

    private string FormatPrice(decimal price) => price.ToString("C");
}

#endregion

// ──────────────────────────────────────────────
// Mapster Setup (reflection + IL emission)
// ──────────────────────────────────────────────

#region Mapster Setup

public static class MapsterSetup
{
    public static TypeAdapterConfig CreateConfig()
    {
        var config = new TypeAdapterConfig();

        config
            .NewConfig<Product, ProductDto>()
            .Map(d => d.PriceDisplay, s => s.UnitPrice.ToString("C"))
            .Map(d => d.CategoryName, s => s.Category != null ? s.Category.Name : "")
            .Map(
                d => d.ParentCategoryName,
                s => s.Category != null && s.Category.Parent != null ? s.Category.Parent.Name : (string?)null
            );

        config
            .NewConfig<OrderItem, OrderItemDto>()
            .Map(d => d.ProductName, s => s.Product.Name)
            .Map(d => d.ProductSKU, s => s.Product.SKU)
            .Map(d => d.LineTotal, s => (s.Product.UnitPrice - s.Discount) * s.Quantity);

        config
            .NewConfig<Order, OrderDto>()
            .Map(d => d.StatusLabel, s => s.Status.ToString())
            .Map(d => d.CustomerName, s => s.Customer.FirstName + " " + s.Customer.LastName)
            .Map(d => d.CustomerEmail, s => s.Customer.Email)
            .Map(d => d.CustomerCity, s => s.Customer.Address.City)
            .Map(d => d.TotalAmount, s => s.Items.Sum(x => (x.Product.UnitPrice - x.Discount) * x.Quantity))
            .Map(d => d.Items, s => s.Items);

        config.Compile();
        return config;
    }
}

#endregion

// ──────────────────────────────────────────────
// Benchmarks
// ──────────────────────────────────────────────

#region Benchmarks

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class MappingBenchmarks
{
    private List<Order> _orders = null!;

    private static readonly MapoBenchmarkMapper _mapoMapper = new();
    private AutoMapper.IMapper _autoMapper = null!;
    private static readonly MapperlyBenchmarkMapper _mapperlyMapper = new();
    private TypeAdapterConfig _mapsterConfig = null!;

    [Params(100, 1_000, 10_000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);

        var electronics = new Category { Id = 1, Name = "Electronics" };
        var laptops = new Category
        {
            Id = 2,
            Name = "Laptops",
            Parent = electronics,
        };
        var peripherals = new Category
        {
            Id = 3,
            Name = "Peripherals",
            Parent = electronics,
        };

        var products = new[]
        {
            new Product
            {
                SKU = "LAP-001",
                Name = "MacBook Pro 16\"",
                UnitPrice = 2499.00m,
                Category = laptops,
            },
            new Product
            {
                SKU = "LAP-002",
                Name = "ThinkPad X1 Carbon",
                UnitPrice = 1899.00m,
                Category = laptops,
            },
            new Product
            {
                SKU = "PER-001",
                Name = "MX Master 3S",
                UnitPrice = 99.99m,
                Category = peripherals,
            },
            new Product
            {
                SKU = "PER-002",
                Name = "Keychron K2",
                UnitPrice = 79.00m,
                Category = peripherals,
            },
            new Product
            {
                SKU = "GEN-001",
                Name = "USB-C Hub",
                UnitPrice = 45.50m,
                Category = null,
            },
        };

        var addresses = new[]
        {
            new Address
            {
                Street = "123 Main St",
                City = "Seattle",
                State = "WA",
                ZipCode = "98101",
            },
            new Address
            {
                Street = "456 Oak Ave",
                City = "Portland",
                State = "OR",
                ZipCode = "97201",
            },
            new Address
            {
                Street = "789 Pine Rd",
                City = "San Francisco",
                State = "CA",
                ZipCode = "94102",
            },
        };

        _orders = Enumerable
            .Range(1, Count)
            .Select(i =>
            {
                var customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    FirstName = "Customer",
                    LastName = $"#{i}",
                    Email = $"customer{i}@example.com",
                    Address = addresses[i % addresses.Length],
                };

                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    OrderNumber = $"ORD-{i:D6}",
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                    Status = (OrderStatus)(i % 5),
                    Customer = customer,
                };

                int itemCount = 1 + (i % 4);
                for (int j = 0; j < itemCount; j++)
                {
                    order.Items.Add(
                        new OrderItem
                        {
                            Product = products[(i + j) % products.Length],
                            Quantity = 1 + (j % 3),
                            Discount = j == 0 ? 10.0m : 0m,
                        }
                    );
                }

                return order;
            })
            .ToList();

        _autoMapper = AutoMapperSetup.CreateMapper();
        _mapsterConfig = MapsterSetup.CreateConfig();
    }

    [Benchmark(Baseline = true)]
    public List<OrderDto> Manual() => ManualMapper.MapOrders(_orders);

    [Benchmark]
    public List<OrderDto> Mapo() => _mapoMapper.MapOrders(_orders);

    [Benchmark]
    public List<OrderDto> AutoMapper() => _autoMapper.Map<List<OrderDto>>(_orders);

    [Benchmark]
    public List<OrderDto> Mapperly() => _mapperlyMapper.MapOrders(_orders);

    [Benchmark]
    public List<OrderDto> Mapster() => _orders.Adapt<List<OrderDto>>(_mapsterConfig);
}

#endregion

// ──────────────────────────────────────────────
// Entry Point
// ──────────────────────────────────────────────

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<MappingBenchmarks>();
    }
}
