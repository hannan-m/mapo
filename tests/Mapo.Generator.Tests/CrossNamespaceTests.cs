using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for cross-namespace mapping scenarios (Issue #3).
/// When source/target types are in different namespaces than the mapper,
/// the generated code must resolve all type references correctly.
/// </summary>
public class CrossNamespaceTests : MapoVerifier
{
    [Fact]
    public void TypesInDifferentNamespace_ShouldCompile()
    {
        string source =
            @"
using Mapo.Attributes;

namespace Models
{
    public class UserEntity { public int Id { get; set; } public string Name { get; set; } = """"; }
    public class UserDto { public int Id { get; set; } public string Name { get; set; } = """"; }
}

namespace Mappers
{
    [Mapper]
    public static partial class UserMapper
    {
        public static partial Models.UserDto Map(Models.UserEntity src);
    }
}";
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void TypesInThreeNamespaces_ShouldCompile()
    {
        string source =
            @"
using Mapo.Attributes;

namespace Domain.Entities
{
    public class Order { public int Id { get; set; } public decimal Total { get; set; } }
}

namespace Api.Dtos
{
    public class OrderDto { public int Id { get; set; } public decimal Total { get; set; } }
}

namespace Application.Mappers
{
    [Mapper]
    public static partial class OrderMapper
    {
        public static partial Api.Dtos.OrderDto Map(Domain.Entities.Order src);
    }
}";
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void TypesInDifferentNamespace_CompilesAndRuns()
    {
        string source =
            @"
using Mapo.Attributes;
using System;

namespace Models
{
    public class Source { public int Id { get; set; } public string Name { get; set; } = """"; }
    public class Target { public int Id { get; set; } public string Name { get; set; } = """"; }
}

namespace Mappers
{
    [Mapper]
    public static partial class MyMapper
    {
        public static partial Models.Target Map(Models.Source src);
    }
}

namespace Test
{
    public static class TestRunner
    {
        public static void Run()
        {
            var s = new Models.Source { Id = 42, Name = ""Alice"" };
            var t = Mappers.MyMapper.Map(s);
            if (t.Id != 42) throw new Exception($""Expected 42, got {t.Id}"");
            if (t.Name != ""Alice"") throw new Exception($""Expected Alice, got {t.Name}"");
        }
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NestedTypes_DifferentNamespaces_ShouldCompile()
    {
        // Nested object mapping where all types are in different namespaces
        string source =
            @"
using Mapo.Attributes;

namespace Domain
{
    public class Address { public string City { get; set; } = """"; }
    public class Person { public string Name { get; set; } = """"; public Address Home { get; set; } }
}

namespace Dto
{
    public class AddressDto { public string City { get; set; } = """"; }
    public class PersonDto { public string Name { get; set; } = """"; public AddressDto Home { get; set; } }
}

namespace Mapping
{
    [Mapper]
    public static partial class PersonMapper
    {
        public static partial Dto.PersonDto Map(Domain.Person src);
    }
}";
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void NestedTypes_DifferentNamespaces_CompilesAndRuns()
    {
        string source =
            @"
using Mapo.Attributes;
using System;

namespace Domain
{
    public class Address { public string City { get; set; } = """"; }
    public class Person { public string Name { get; set; } = """"; public Address Home { get; set; } }
}

namespace Dto
{
    public class AddressDto { public string City { get; set; } = """"; }
    public class PersonDto { public string Name { get; set; } = """"; public AddressDto Home { get; set; } }
}

namespace Mapping
{
    [Mapper]
    public static partial class PersonMapper
    {
        public static partial Dto.PersonDto Map(Domain.Person src);
    }
}

namespace Test
{
    public static class TestRunner
    {
        public static void Run()
        {
            var p = new Domain.Person { Name = ""Bob"", Home = new Domain.Address { City = ""NYC"" } };
            var dto = Mapping.PersonMapper.Map(p);
            if (dto.Name != ""Bob"") throw new Exception($""Expected Bob, got {dto.Name}"");
            if (dto.Home == null) throw new Exception(""Home should not be null"");
            if (dto.Home.City != ""NYC"") throw new Exception($""Expected NYC, got {dto.Home.City}"");
        }
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void CollectionTypes_DifferentNamespaces_ShouldCompile()
    {
        string source =
            @"
using Mapo.Attributes;
using System.Collections.Generic;

namespace Domain
{
    public class Item { public int Id { get; set; } }
    public class Container { public List<Item> Items { get; set; } = new(); }
}

namespace Dto
{
    public class ItemDto { public int Id { get; set; } }
    public class ContainerDto { public List<ItemDto> Items { get; set; } = new(); }
}

namespace Mapping
{
    [Mapper]
    public static partial class ContainerMapper
    {
        public static partial Dto.ContainerDto Map(Domain.Container src);
    }
}";
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void EnumTypes_DifferentNamespaces_ShouldCompile()
    {
        string source =
            @"
using Mapo.Attributes;

namespace Domain
{
    public enum Status { Active, Inactive }
    public class Entity { public Status Status { get; set; } }
}

namespace Dto
{
    public enum StatusDto { Active, Inactive }
    public class EntityDto { public StatusDto Status { get; set; } }
}

namespace Mapping
{
    [Mapper]
    public static partial class EntityMapper
    {
        public static partial Dto.EntityDto Map(Domain.Entity src);
    }
}";
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void ExtensionMethods_DifferentNamespaces_ShouldCompile()
    {
        // Extension methods reference source/target types which may be in other namespaces
        string source =
            @"
using Mapo.Attributes;

namespace Domain
{
    public class User { public int Id { get; set; } }
}

namespace Dto
{
    public class UserDto { public int Id { get; set; } }
}

namespace Mapping
{
    [Mapper]
    public static partial class UserMapper
    {
        public static partial Dto.UserDto Map(Domain.User src);
    }
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();

        // Extension class should reference fully qualified types
        generated.Should().Contain("Domain.User");
        generated.Should().Contain("Dto.UserDto");
        AssertGeneratedCodeCompiles(source);
    }
}
