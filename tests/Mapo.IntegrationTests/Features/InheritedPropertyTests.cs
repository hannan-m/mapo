using System;
using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// --- Types for inherited property tests ---

public abstract class EntityBase
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
}

public class CustomerEntity : EntityBase
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public bool Active { get; set; }
}

public class CustomerEntityDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public bool Active { get; set; }
}

public abstract class AuditBase
{
    public DateTime ModifiedAt { get; set; }
    public string ModifiedBy { get; set; } = "";
}

public abstract class FullAuditBase : AuditBase
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderEntity : FullAuditBase
{
    public string Reference { get; set; } = "";
    public decimal Total { get; set; }
}

public class OrderEntityDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string ModifiedBy { get; set; } = "";
    public string Reference { get; set; } = "";
    public decimal Total { get; set; }
}

[Mapper]
public static partial class InheritedPropertyMapper
{
    public static partial CustomerEntityDto MapCustomer(CustomerEntity src);

    public static partial OrderEntityDto MapOrder(OrderEntity src);
}

public class InheritedPropertyTests
{
    [Fact]
    public void SingleLevelInheritance_MapsBaseProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var result = InheritedPropertyMapper.MapCustomer(
            new CustomerEntity
            {
                Id = id,
                CreatedAt = now,
                CreatedBy = "system",
                Name = "Alice",
                Email = "alice@test.com",
                Active = true,
            }
        );

        result.Id.Should().Be(id);
        result.CreatedAt.Should().Be(now);
        result.CreatedBy.Should().Be("system");
        result.Name.Should().Be("Alice");
        result.Email.Should().Be("alice@test.com");
        result.Active.Should().BeTrue();
    }

    [Fact]
    public void MultiLevelInheritance_MapsAllAncestorProperties()
    {
        var id = Guid.NewGuid();
        var created = DateTime.UtcNow.AddDays(-1);
        var modified = DateTime.UtcNow;
        var result = InheritedPropertyMapper.MapOrder(
            new OrderEntity
            {
                Id = id,
                CreatedAt = created,
                ModifiedAt = modified,
                ModifiedBy = "admin",
                Reference = "ORD-001",
                Total = 150.00m,
            }
        );

        result.Id.Should().Be(id);
        result.CreatedAt.Should().Be(created);
        result.ModifiedAt.Should().Be(modified);
        result.ModifiedBy.Should().Be("admin");
        result.Reference.Should().Be("ORD-001");
        result.Total.Should().Be(150.00m);
    }
}
