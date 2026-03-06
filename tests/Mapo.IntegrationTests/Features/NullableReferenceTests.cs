using System;
using System.Collections.Generic;
using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// --- Types for nullable reference type tests ---

public class NullableRefSource
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public List<string>? Tags { get; set; }
}

public class NonNullableRefTarget
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public List<string> Tags { get; set; } = new();
}

public class NullableNestedSource
{
    public string? Name { get; set; }
    public NullableRefSource? Contact { get; set; }
}

public class NonNullableNestedTarget
{
    public string Name { get; set; } = "";
    public NonNullableRefTarget? Contact { get; set; }
}

public record NullablePersonDto(string? Name, string? Title, int Age);

public record NullablePerson(string Name, string Title, int Age);

[Mapper]
public static partial class NullableRefMapper
{
    public static partial NonNullableRefTarget MapFlat(NullableRefSource src);

    public static partial NonNullableNestedTarget MapNested(NullableNestedSource src);

    public static partial NullablePerson MapRecord(NullablePersonDto src);
}

public class NullableReferenceTests
{
    [Fact]
    public void NullableString_ToNonNullable_UsesNullForgiving()
    {
        var result = NullableRefMapper.MapFlat(
            new NullableRefSource
            {
                FirstName = "Alice",
                LastName = null,
                Email = "alice@test.com",
            }
        );

        result.FirstName.Should().Be("Alice");
        result.LastName.Should().BeNull();
        result.Email.Should().Be("alice@test.com");
    }

    [Fact]
    public void NullableNestedObject_WhenPopulated_MapsCorrectly()
    {
        var result = NullableRefMapper.MapNested(
            new NullableNestedSource
            {
                Name = "Acme",
                Contact = new NullableRefSource
                {
                    FirstName = "Bob",
                    LastName = "Smith",
                    Email = "bob@acme.com",
                },
            }
        );

        result.Name.Should().Be("Acme");
        result.Contact.Should().NotBeNull();
        result.Contact!.FirstName.Should().Be("Bob");
    }

    [Fact]
    public void NullableNestedObject_WhenNull_ReturnsNull()
    {
        var result = NullableRefMapper.MapNested(new NullableNestedSource { Name = "Empty", Contact = null });

        result.Name.Should().Be("Empty");
        result.Contact.Should().BeNull();
    }

    [Fact]
    public void NullableRecordConstructorParams_MapCorrectly()
    {
        var result = NullableRefMapper.MapRecord(new NullablePersonDto("Alice", "CTO", 30));

        result.Name.Should().Be("Alice");
        result.Title.Should().Be("CTO");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void NullableCollection_WhenNull_ReturnsEmpty()
    {
        var result = NullableRefMapper.MapFlat(new NullableRefSource { FirstName = "X", Tags = null });

        // Nullable source coalesces to empty list for non-nullable target
        result.Tags.Should().NotBeNull();
        result.Tags.Should().BeEmpty();
    }

    [Fact]
    public void NullableCollection_WhenPopulated_MapsElements()
    {
        var result = NullableRefMapper.MapFlat(
            new NullableRefSource
            {
                FirstName = "X",
                Tags = new List<string> { "admin", "active" },
            }
        );

        result.Tags.Should().BeEquivalentTo(new[] { "admin", "active" });
    }
}
