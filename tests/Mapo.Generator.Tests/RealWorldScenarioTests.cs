using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

public class RealWorldScenarioTests : MapoVerifier
{
    [Fact]
    public void LargeFlatDto_MapsAllPropertyTypes()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class Source
{
    public int P1 { get; set; } public int P2 { get; set; } public int P3 { get; set; }
    public int P4 { get; set; } public int P5 { get; set; } public int P6 { get; set; }
    public int P7 { get; set; } public int P8 { get; set; } public int P9 { get; set; }
    public int P10 { get; set; } public string P11 { get; set; } = """";
    public string P12 { get; set; } = """"; public string P13 { get; set; } = """";
    public string P14 { get; set; } = """"; public string P15 { get; set; } = """";
    public string P16 { get; set; } = """"; public string P17 { get; set; } = """";
    public string P18 { get; set; } = """"; public string P19 { get; set; } = """";
    public string P20 { get; set; } = """"; public bool P21 { get; set; }
    public bool P22 { get; set; } public bool P23 { get; set; }
    public DateTime P24 { get; set; } public DateTime P25 { get; set; }
    public decimal P26 { get; set; } public decimal P27 { get; set; }
    public double P28 { get; set; } public Guid P29 { get; set; }
    public long P30 { get; set; }
}
public class Target
{
    public int P1 { get; set; } public int P2 { get; set; } public int P3 { get; set; }
    public int P4 { get; set; } public int P5 { get; set; } public int P6 { get; set; }
    public int P7 { get; set; } public int P8 { get; set; } public int P9 { get; set; }
    public int P10 { get; set; } public string P11 { get; set; } = """";
    public string P12 { get; set; } = """"; public string P13 { get; set; } = """";
    public string P14 { get; set; } = """"; public string P15 { get; set; } = """";
    public string P16 { get; set; } = """"; public string P17 { get; set; } = """";
    public string P18 { get; set; } = """"; public string P19 { get; set; } = """";
    public string P20 { get; set; } = """"; public bool P21 { get; set; }
    public bool P22 { get; set; } public bool P23 { get; set; }
    public DateTime P24 { get; set; } public DateTime P25 { get; set; }
    public decimal P26 { get; set; } public decimal P27 { get; set; }
    public double P28 { get; set; } public Guid P29 { get; set; }
    public long P30 { get; set; }
}
[Mapper]
public partial class M { public partial Target Map(Source s); }

public static class TestRunner
{
    public static void Run()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var s = new Source
        {
            P1 = 1, P2 = 2, P3 = 3, P4 = 4, P5 = 5, P6 = 6, P7 = 7, P8 = 8, P9 = 9, P10 = 10,
            P11 = ""a"", P12 = ""b"", P13 = ""c"", P14 = ""d"", P15 = ""e"",
            P16 = ""f"", P17 = ""g"", P18 = ""h"", P19 = ""i"", P20 = ""j"",
            P21 = true, P22 = false, P23 = true,
            P24 = now, P25 = now.AddDays(1),
            P26 = 99.99m, P27 = 100.01m,
            P28 = 3.14, P29 = id, P30 = 999999L
        };
        var mapper = new M();
        var t = mapper.Map(s);
        if (t.P1 != 1) throw new Exception($""P1: {t.P1}"");
        if (t.P10 != 10) throw new Exception($""P10: {t.P10}"");
        if (t.P15 != ""e"") throw new Exception($""P15: {t.P15}"");
        if (t.P21 != true) throw new Exception($""P21: {t.P21}"");
        if (t.P24 != now) throw new Exception($""P24 mismatch"");
        if (t.P26 != 99.99m) throw new Exception($""P26: {t.P26}"");
        if (t.P29 != id) throw new Exception($""P29: {t.P29}"");
        if (t.P30 != 999999L) throw new Exception($""P30: {t.P30}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void SameShapeCloning_ProducesIndependentCopy()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class User { public int Id { get; set; } public string Name { get; set; } = """"; public string Email { get; set; } = """"; }
public class UserSnapshot { public int Id { get; set; } public string Name { get; set; } = """"; public string Email { get; set; } = """"; }
[Mapper]
public partial class M { public partial UserSnapshot Map(User u); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var u = new User { Id = 42, Name = ""Alice"", Email = ""alice@test.com"" };
        var snap = mapper.Map(u);
        if (snap.Id != 42) throw new Exception($""Id: {snap.Id}"");
        if (snap.Name != ""Alice"") throw new Exception($""Name: {snap.Name}"");
        if (snap.Email != ""alice@test.com"") throw new Exception($""Email: {snap.Email}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void ThreeLevelNullableNesting_HandlesAllNullCombinations()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class CountryDto { public string? Name { get; set; } public string? Code { get; set; } }
public class AddressDto { public string? City { get; set; } public CountryDto? Country { get; set; } }
public class CompanyDto { public AddressDto? Headquarters { get; set; } public string? Name { get; set; } }
public class Country { public string Name { get; set; } = """"; public string Code { get; set; } = """"; }
public class Address { public string City { get; set; } = """"; public Country? Country { get; set; } }
public class Company { public Address? Headquarters { get; set; } public string Name { get; set; } = """"; }
[Mapper]
public partial class M { public partial Company Map(CompanyDto s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();

        var full = mapper.Map(new CompanyDto
        {
            Name = ""Acme"",
            Headquarters = new AddressDto
            {
                City = ""Berlin"",
                Country = new CountryDto { Name = ""Germany"", Code = ""DE"" }
            }
        });
        if (full.Name != ""Acme"") throw new Exception($""Name: {full.Name}"");
        if (full.Headquarters == null) throw new Exception(""HQ null"");
        if (full.Headquarters.City != ""Berlin"") throw new Exception($""City: {full.Headquarters.City}"");
        if (full.Headquarters.Country == null) throw new Exception(""Country null"");
        if (full.Headquarters.Country.Name != ""Germany"") throw new Exception($""Country: {full.Headquarters.Country.Name}"");

        var empty = mapper.Map(new CompanyDto { Name = ""Empty"", Headquarters = null });
        if (empty.Headquarters != null) throw new Exception(""Expected null HQ"");

        var partial = mapper.Map(new CompanyDto { Name = ""Partial"", Headquarters = new AddressDto { City = ""London"", Country = null } });
        if (partial.Headquarters == null) throw new Exception(""HQ null"");
        if (partial.Headquarters.Country != null) throw new Exception(""Expected null Country"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void MultipleMappers_SameSharedTypes_NoConflict()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class Address { public string City { get; set; } = """"; public string Zip { get; set; } = """"; }
public class AddressDto { public string City { get; set; } = """"; public string Zip { get; set; } = """"; }
public class User { public string Name { get; set; } = """"; public Address Address { get; set; } = new(); }
public class UserDto { public string Name { get; set; } = """"; public AddressDto Address { get; set; } = new(); }
public class Order { public string Ref { get; set; } = """"; public Address ShipTo { get; set; } = new(); }
public class OrderDto { public string Ref { get; set; } = """"; public AddressDto ShipTo { get; set; } = new(); }

[Mapper]
public partial class UserMapper { public partial UserDto Map(User u); }

[Mapper]
public partial class OrderMapper { public partial OrderDto Map(Order o); }

public static class TestRunner
{
    public static void Run()
    {
        var u = new UserMapper().Map(new User { Name = ""Alice"", Address = new Address { City = ""NYC"", Zip = ""10001"" } });
        if (u.Name != ""Alice"") throw new Exception($""User name: {u.Name}"");
        if (u.Address.City != ""NYC"") throw new Exception($""User city: {u.Address.City}"");

        var o = new OrderMapper().Map(new Order { Ref = ""ORD-1"", ShipTo = new Address { City = ""LA"", Zip = ""90001"" } });
        if (o.Ref != ""ORD-1"") throw new Exception($""Order ref: {o.Ref}"");
        if (o.ShipTo.City != ""LA"") throw new Exception($""ShipTo city: {o.ShipTo.City}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NullableToNonNullable_AllCommonValueTypes()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class ApiResponse
{
    public int? StatusCode { get; set; }
    public string? Message { get; set; }
    public bool? Success { get; set; }
    public DateTime? Timestamp { get; set; }
    public decimal? Amount { get; set; }
    public Guid? TraceId { get; set; }
    public long? RequestId { get; set; }
}
public class InternalResult
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = """";
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal Amount { get; set; }
    public Guid TraceId { get; set; }
    public long RequestId { get; set; }
}
[Mapper]
public partial class M { public partial InternalResult Map(ApiResponse r); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var withValues = mapper.Map(new ApiResponse { StatusCode = 200, Message = ""OK"", Success = true, Timestamp = now, Amount = 42.5m, TraceId = id, RequestId = 123L });
        if (withValues.StatusCode != 200) throw new Exception($""Status: {withValues.StatusCode}"");
        if (withValues.Message != ""OK"") throw new Exception($""Msg: {withValues.Message}"");
        if (withValues.TraceId != id) throw new Exception($""Trace: {withValues.TraceId}"");

        var allNull = mapper.Map(new ApiResponse());
        if (allNull.StatusCode != 0) throw new Exception($""Default status: {allNull.StatusCode}"");
        if (allNull.Amount != 0m) throw new Exception($""Default amount: {allNull.Amount}"");
        if (allNull.TraceId != Guid.Empty) throw new Exception($""Default trace: {allNull.TraceId}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void RecordTarget_WithComputedFields()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public enum OrderStatus { Draft, Confirmed, Shipped }
public class Order
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = """";
    public OrderStatus Status { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
}
public record OrderSummary(Guid Id, string CustomerName, string Status, decimal Total, string CreatedDate);
[Mapper]
public partial class M
{
    public partial OrderSummary Map(Order o);
    static void Configure(IMapConfig<Order, OrderSummary> config)
    {
        config.Map(d => d.CreatedDate, s => s.CreatedAt.ToString(""yyyy-MM-dd""));
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var order = new Order { Id = Guid.NewGuid(), CustomerName = ""Bob"", Status = OrderStatus.Confirmed, Total = 99.99m, CreatedAt = new DateTime(2026, 1, 15) };
        var summary = mapper.Map(order);
        if (summary.CustomerName != ""Bob"") throw new Exception($""Name: {summary.CustomerName}"");
        if (summary.Status != ""Confirmed"") throw new Exception($""Status: {summary.Status}"");
        if (summary.Total != 99.99m) throw new Exception($""Total: {summary.Total}"");
        if (summary.CreatedDate != ""2026-01-15"") throw new Exception($""Date: {summary.CreatedDate}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void RecordSourceToFlatDto_WithCollections()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;
public record LineItem(string ProductName, int Quantity, decimal UnitPrice);
public class LineItemDto { public string ProductName { get; set; } = """"; public int Quantity { get; set; } public decimal UnitPrice { get; set; } }
public class OrderPlaced
{
    public Guid OrderId { get; set; }
    public string CustomerEmail { get; set; } = """";
    public List<LineItem> Items { get; set; } = new();
    public DateTime PlacedAt { get; set; }
}
public class OrderPlacedDto
{
    public Guid OrderId { get; set; }
    public string CustomerEmail { get; set; } = """";
    public List<LineItemDto> Items { get; set; } = new();
    public DateTime PlacedAt { get; set; }
}
[Mapper]
public partial class M { public partial OrderPlacedDto Map(OrderPlaced e); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var evt = new OrderPlaced
        {
            OrderId = Guid.NewGuid(),
            CustomerEmail = ""bob@test.com"",
            PlacedAt = DateTime.UtcNow,
            Items = new List<LineItem>
            {
                new LineItem(""Widget"", 2, 9.99m),
                new LineItem(""Gadget"", 1, 24.99m)
            }
        };
        var dto = mapper.Map(evt);
        if (dto.CustomerEmail != ""bob@test.com"") throw new Exception($""Email: {dto.CustomerEmail}"");
        if (dto.Items.Count != 2) throw new Exception($""Items: {dto.Items.Count}"");
        if (dto.Items[0].ProductName != ""Widget"") throw new Exception($""Product: {dto.Items[0].ProductName}"");
        if (dto.Items[0].Quantity != 2) throw new Exception($""Qty: {dto.Items[0].Quantity}"");
        if (dto.Items[1].UnitPrice != 24.99m) throw new Exception($""Price: {dto.Items[1].UnitPrice}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void ComputedProperties_WithLambdaExpressions()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public bool InStock { get; set; }
    public string? Sku { get; set; }
}
public class ProductCard
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
    public string PriceDisplay { get; set; } = """";
    public string Description { get; set; } = """";
    public string Availability { get; set; } = """";
    public string Sku { get; set; } = """";
}
[Mapper]
public partial class M
{
    public partial ProductCard Map(Product p);
    static void Configure(IMapConfig<Product, ProductCard> config)
    {
        config.Map(d => d.PriceDisplay, s => s.Price.ToString(""C""))
              .Map(d => d.Availability, s => s.InStock ? ""In Stock"" : ""Out of Stock"");
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var card = mapper.Map(new Product { Id = 1, Name = ""Laptop"", Price = 999.99m, Description = ""Fast"", InStock = true, Sku = ""LAP-001"" });
        if (card.Id != 1) throw new Exception($""Id: {card.Id}"");
        if (card.Name != ""Laptop"") throw new Exception($""Name: {card.Name}"");
        if (card.Availability != ""In Stock"") throw new Exception($""Avail: {card.Availability}"");
        if (card.Sku != ""LAP-001"") throw new Exception($""Sku: {card.Sku}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void UpdateMapping_IgnoresAuditFields()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class UpdateProductDto
{
    public string Name { get; set; } = """";
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public bool Active { get; set; }
}
public class ProductEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = """";
    public DateTime? ModifiedAt { get; set; }
}
[Mapper]
public partial class M
{
    public partial void Apply(UpdateProductDto src, ProductEntity target);
    static void Configure(IMapConfig<UpdateProductDto, ProductEntity> config)
    {
        config.Ignore(d => d.Id)
              .Ignore(d => d.CreatedAt)
              .Ignore(d => d.CreatedBy)
              .Ignore(d => d.ModifiedAt);
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var entity = new ProductEntity { Id = 99, Name = ""Old"", Price = 10m, CreatedAt = DateTime.MinValue, CreatedBy = ""system"" };
        mapper.Apply(new UpdateProductDto { Name = ""New"", Price = 25m, Description = ""Updated"", Active = true }, entity);
        if (entity.Name != ""New"") throw new Exception($""Name: {entity.Name}"");
        if (entity.Price != 25m) throw new Exception($""Price: {entity.Price}"");
        if (entity.Active != true) throw new Exception($""Active: {entity.Active}"");
        if (entity.Id != 99) throw new Exception($""Id changed: {entity.Id}"");
        if (entity.CreatedBy != ""system"") throw new Exception($""CreatedBy changed: {entity.CreatedBy}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NullableCollection_OfRecords_MapsToEmptyList()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;
public record TagDto(string? Key, string? Value);
public class Tag { public string Key { get; set; } = """"; public string Value { get; set; } = """"; }
public class ResourceDto
{
    public string Id { get; set; } = """";
    public List<TagDto>? Tags { get; set; }
}
public class Resource
{
    public string Id { get; set; } = """";
    public List<Tag> Tags { get; set; } = new();
}
[Mapper]
public partial class M { public partial Resource Map(ResourceDto s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var withTags = mapper.Map(new ResourceDto
        {
            Id = ""res-1"",
            Tags = new List<TagDto> { new TagDto(""env"", ""prod""), new TagDto(""team"", ""backend"") }
        });
        if (withTags.Tags.Count != 2) throw new Exception($""Tags: {withTags.Tags.Count}"");
        if (withTags.Tags[0].Key != ""env"") throw new Exception($""Key: {withTags.Tags[0].Key}"");
        if (withTags.Tags[1].Value != ""backend"") throw new Exception($""Val: {withTags.Tags[1].Value}"");

        var nullTags = mapper.Map(new ResourceDto { Id = ""res-2"", Tags = null });
        if (nullTags.Tags == null || nullTags.Tags.Count != 0) throw new Exception(""Expected empty tags"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void MultipleEnumFields_StringToEnum()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public enum Priority { Low, Medium, High, Critical }
public enum Status { Open, InProgress, Resolved, Closed }
public enum Category { Bug, Feature, Task, Epic }
public class TicketDto
{
    public string? Priority { get; set; }
    public string? Status { get; set; }
    public string? Category { get; set; }
    public string Title { get; set; } = """";
}
public class Ticket
{
    public Priority Priority { get; set; }
    public Status Status { get; set; }
    public Category Category { get; set; }
    public string Title { get; set; } = """";
}
[Mapper]
public partial class M { public partial Ticket Map(TicketDto d); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var t = mapper.Map(new TicketDto { Priority = ""High"", Status = ""InProgress"", Category = ""Bug"", Title = ""Fix it"" });
        if (t.Priority != Priority.High) throw new Exception($""Pri: {t.Priority}"");
        if (t.Status != Status.InProgress) throw new Exception($""St: {t.Status}"");
        if (t.Category != Category.Bug) throw new Exception($""Cat: {t.Category}"");
        if (t.Title != ""Fix it"") throw new Exception($""Title: {t.Title}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void InheritedBaseClassProperties_AreMapped()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public abstract class EntityBase { public int Id { get; set; } public DateTime CreatedAt { get; set; } }
public class Customer : EntityBase { public string Name { get; set; } = """"; public string Email { get; set; } = """"; }
public class CustomerDto { public int Id { get; set; } public DateTime CreatedAt { get; set; } public string Name { get; set; } = """"; public string Email { get; set; } = """"; }
[Mapper]
public partial class M { public partial CustomerDto Map(Customer c); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var now = DateTime.UtcNow;
        var dto = mapper.Map(new Customer { Id = 5, CreatedAt = now, Name = ""Alice"", Email = ""alice@x.com"" });
        if (dto.Id != 5) throw new Exception($""Id: {dto.Id}"");
        if (dto.CreatedAt != now) throw new Exception(""CreatedAt mismatch"");
        if (dto.Name != ""Alice"") throw new Exception($""Name: {dto.Name}"");
        if (dto.Email != ""alice@x.com"") throw new Exception($""Email: {dto.Email}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }
}
