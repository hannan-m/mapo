using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// =============================================================================
// Constructor mapping: positional records, init-only properties
// =============================================================================

public class PersonSource
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int Age { get; set; }
    public string Email { get; set; } = "";
}

// Target is a positional record — constructor parameters match property names
public record PersonRecord(string FirstName, string LastName, int Age, string Email);

// Target with init-only properties
public class PersonInitOnly
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public int Age { get; init; }
    public string Email { get; init; } = "";
}

// Target mixing ctor + settable
public class PersonMixed
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string Email { get; set; } = "";

    public PersonMixed(string name)
    {
        Name = name;
    }
}

[Mapper]
public partial class ConstructorMapper
{
    public partial PersonRecord MapToRecord(PersonSource source);
    public partial PersonInitOnly MapToInitOnly(PersonSource source);
    public partial PersonMixed MapToMixed(PersonSource source);

    static void Configure(IMapConfig<PersonSource, PersonMixed> config)
    {
        config.Map(d => d.Name, s => $"{s.FirstName} {s.LastName}");
    }
}

public class ConstructorMappingTests
{
    [Fact]
    public void PositionalRecord_ShouldMapViaCtor()
    {
        var mapper = new ConstructorMapper();
        var source = new PersonSource
        {
            FirstName = "Jane",
            LastName = "Doe",
            Age = 30,
            Email = "jane@example.com"
        };

        var record = mapper.MapToRecord(source);

        record.FirstName.Should().Be("Jane");
        record.LastName.Should().Be("Doe");
        record.Age.Should().Be(30);
        record.Email.Should().Be("jane@example.com");
    }

    [Fact]
    public void InitOnlyProperties_ShouldBeSetInObjectInitializer()
    {
        var mapper = new ConstructorMapper();
        var source = new PersonSource
        {
            FirstName = "Bob",
            LastName = "Smith",
            Age = 45,
            Email = "bob@test.com"
        };

        var result = mapper.MapToInitOnly(source);

        result.FirstName.Should().Be("Bob");
        result.LastName.Should().Be("Smith");
        result.Age.Should().Be(45);
        result.Email.Should().Be("bob@test.com");
    }

    [Fact]
    public void MixedCtorAndSetter_ShouldMapCorrectly()
    {
        var mapper = new ConstructorMapper();
        var source = new PersonSource
        {
            FirstName = "Alice",
            LastName = "Wonder",
            Age = 25,
            Email = "alice@example.com"
        };

        var result = mapper.MapToMixed(source);

        result.Name.Should().Be("Alice Wonder");
        result.Age.Should().Be(25);
        result.Email.Should().Be("alice@example.com");
    }
}
