using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.DirectMapping;

public enum OrderStatus
{
    Draft,
    Confirmed,
    Shipped,
    Delivered,
    Cancelled,
}

public record Tenant(Guid Id, string Name, string CurrencyCode);

public class Category
{
    public string Name { get; set; } = "";
    public Category? Parent { get; set; }
}

public abstract record PaymentMethod;

public record CreditCard(string LastFour, string Brand) : PaymentMethod;

public record Crypto(string WalletAddress, string Network) : PaymentMethod;

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string ZipCode { get; set; } = "";
}

public class Product
{
    public Guid Id { get; set; }
    public string SKU { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public Category? Category { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class Order
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentMethod? Payment { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public Customer? Customer { get; set; } // Circular reference
    public Address? ShippingAddress { get; set; }
}

public record OrderItem(Product Product, int Quantity);

public class Customer
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public Address? Address { get; set; }
    public List<Order> Orders { get; set; } = new();
}

public record ProductDto(Guid Id, string SKU, string Name, string CategoryName, string PriceDisplay);

public record ProductUpdateDto(string Name, decimal Price);

public record OrderDto(
    Guid Id,
    string FormattedDate,
    string StatusLabel,
    string PaymentInfo,
    decimal TotalPrice,
    List<ProductDto> Products,
    string ShippingAddressCity,
    string InternalNote = ""
);

public record CustomerDto(Guid Id, string FullName, string Email, List<OrderDto> RecentOrders);

public interface ICurrencyFormatter
{
    string Format(decimal amount, string currencyCode);
}

[Mapper(UseReferenceTracking = true, StrictMode = true)]
public partial class EnterpriseIntegrationMapper
{
    private readonly ICurrencyFormatter _formatter;
    private readonly Tenant _tenant;

    public EnterpriseIntegrationMapper(ICurrencyFormatter formatter, Tenant tenant)
    {
        _formatter = formatter;
        _tenant = tenant;
    }

    public partial CustomerDto MapToDto(Customer customer);

    public partial OrderDto MapOrder(Order order);

    public partial ProductDto MapProduct(Product product);

    public partial List<OrderDto> MapOrders(List<Order> orders);

    public partial Product MapProductDtoToProduct(ProductDto src);

    public partial void ApplyUpdate(ProductUpdateDto source, Product target);

    [MapDerived(typeof(CreditCard), typeof(string))]
    [MapDerived(typeof(Crypto), typeof(string))]
    public static string MapPayment(PaymentMethod? method) =>
        method switch
        {
            CreditCard cc => $"Card: {cc.Brand} (***{cc.LastFour})",
            Crypto crypto => $"Crypto: {crypto.Network} ({crypto.WalletAddress[..8]}...)",
            _ => "Unknown",
        };

    static void Configure(IMapConfig<Customer, CustomerDto> config)
    {
        config.Map(d => d.FullName, s => s.FirstName + " " + s.LastName).Map(d => d.RecentOrders, s => s.Orders);
    }

    static void Configure(IMapConfig<Product, ProductDto> config, ICurrencyFormatter formatter, Tenant tenant)
    {
        config
            .Map(d => d.CategoryName, s => s.Category.Name)
            .Map(d => d.PriceDisplay, s => formatter.Format(s.Price, tenant.CurrencyCode))
            .ReverseMap();
    }

    static void Configure(IMapConfig<ProductDto, Product> config)
    {
        config.Ignore(d => d.Price).Ignore(d => d.Category).Ignore(d => d.LastUpdated);
    }

    static void Configure(IMapConfig<Order, OrderDto> config)
    {
        config.AddConverter<DateTime, string>(dt => dt.ToString("yyyy-MM-dd HH:mm:ss"));

        config
            .Map(d => d.FormattedDate, s => s.Date)
            .Map(d => d.StatusLabel, s => s.Status.ToString().ToUpper())
            .Map(d => d.PaymentInfo, s => MapPayment(s.Payment))
            .Map(d => d.TotalPrice, s => s.Items.Sum(i => i.Product.Price * i.Quantity))
            .Map(d => d.Products, s => s.Items.Select(i => i.Product).ToList())
            .Ignore(d => d.InternalNote);
    }

    static void Configure(IMapConfig<ProductUpdateDto, Product> config)
    {
        config.Ignore(d => d.Id).Ignore(d => d.SKU).Ignore(d => d.Category).Ignore(d => d.LastUpdated);
    }
}

public class TestFormatter : ICurrencyFormatter
{
    public string Format(decimal amount, string currencyCode) =>
        $"${amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";
}

public class DirectMappingTests
{
    [Fact]
    public void ForwardMapping_ShouldMapPropertiesCorrectly()
    {
        var tenant = new Tenant(Guid.NewGuid(), "Acme", "USD");
        var mapper = new EnterpriseIntegrationMapper(new TestFormatter(), tenant);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            SKU = "PRD-1",
            Name = "Laptop",
            Price = 999.99m,
            Category = new Category { Name = "Electronics" },
            LastUpdated = DateTime.UtcNow,
        };

        var dto = mapper.MapProduct(product);

        dto.Name.Should().Be("Laptop");
        dto.CategoryName.Should().Be("Electronics");
        dto.PriceDisplay.Should().Be("$999.99");
        dto.Id.Should().Be(product.Id);
        dto.SKU.Should().Be("PRD-1");
    }

    [Fact]
    public void ReverseMapping_ShouldMapPropertiesAndRespectIgnores()
    {
        var tenant = new Tenant(Guid.NewGuid(), "Acme", "USD");
        var mapper = new EnterpriseIntegrationMapper(new TestFormatter(), tenant);

        var id = Guid.NewGuid();
        var dto = new ProductDto(id, "PRD-2", "Mouse", "Accessories", "$29.99");

        var entity = mapper.MapProductDtoToProduct(dto);

        entity.Name.Should().Be("Mouse");
        entity.SKU.Should().Be("PRD-2");
        entity.Id.Should().Be(id);

        // Ignored properties should be default
        entity.Price.Should().Be(0m);
        entity.Category.Should().BeNull();
        entity.LastUpdated.Should().Be(default);
    }

    [Fact]
    public void ApplyUpdate_ShouldModifyExistingEntityAndRespectIgnores()
    {
        var tenant = new Tenant(Guid.NewGuid(), "Acme", "USD");
        var mapper = new EnterpriseIntegrationMapper(new TestFormatter(), tenant);

        var originalId = Guid.NewGuid();
        var originalCategory = new Category { Name = "Electronics" };
        var originalLastUpdated = DateTime.UtcNow.AddDays(-1);

        var product = new Product
        {
            Id = originalId,
            SKU = "PRD-1",
            Name = "Laptop",
            Price = 999.99m,
            Category = originalCategory,
            LastUpdated = originalLastUpdated,
        };

        var update = new ProductUpdateDto("Updated Laptop", 1099.99m);
        mapper.ApplyUpdate(update, product);

        // Mapped properties
        product.Name.Should().Be("Updated Laptop");
        product.Price.Should().Be(1099.99m);

        // Ignored properties should remain unchanged
        product.Id.Should().Be(originalId);
        product.SKU.Should().Be("PRD-1");
        product.Category.Should().BeSameAs(originalCategory);
        product.LastUpdated.Should().Be(originalLastUpdated);
    }
}
