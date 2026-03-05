using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for polymorphic mapping correctness (Issues #6, #13, #14).
/// </summary>
public class PolymorphicMappingTests : MapoVerifier
{
    [Fact]
    public void Polymorphic_DerivedProperties_NotOverwrittenByBase()
    {
        // The derived mapper sets DogDto.Bark. The base mapping must NOT overwrite it
        // with base-type property expressions (which would reference the wrong variable).
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public abstract class Animal { public string Name { get; set; } = """"; }
public class Dog : Animal { public string Bark { get; set; } = """"; }
public abstract class AnimalDto { public string Name { get; set; } = """"; }
public class DogDto : AnimalDto { public string Bark { get; set; } = """"; }
[Mapper]
public static partial class M
{
    [MapDerived(typeof(Dog), typeof(DogDto))]
    public static partial AnimalDto Map(Animal s);
    public static partial DogDto MapDog(Dog d);
}

public static class TestRunner
{
    public static void Run()
    {
        Animal animal = new Dog { Name = ""Rex"", Bark = ""Woof"" };
        var dto = M.Map(animal);
        if (dto is not DogDto dogDto) throw new Exception(""Expected DogDto"");
        if (dogDto.Name != ""Rex"") throw new Exception($""Expected Rex, got {dogDto.Name}"");
        if (dogDto.Bark != ""Woof"") throw new Exception($""Expected Woof, got {dogDto.Bark}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void Polymorphic_MultipleDerived_CompilesAndRuns()
    {
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public abstract class Shape { public string Color { get; set; } = """"; }
public class Circle : Shape { public double Radius { get; set; } }
public class Square : Shape { public double Side { get; set; } }

public abstract class ShapeDto { public string Color { get; set; } = """"; }
public class CircleDto : ShapeDto { public double Radius { get; set; } }
public class SquareDto : ShapeDto { public double Side { get; set; } }

[Mapper]
public static partial class M
{
    [MapDerived(typeof(Circle), typeof(CircleDto))]
    [MapDerived(typeof(Square), typeof(SquareDto))]
    public static partial ShapeDto Map(Shape s);
    public static partial CircleDto MapCircle(Circle c);
    public static partial SquareDto MapSquare(Square sq);
}

public static class TestRunner
{
    public static void Run()
    {
        Shape circle = new Circle { Color = ""Red"", Radius = 5.0 };
        var circleDto = M.Map(circle);
        if (circleDto is not CircleDto cd) throw new Exception(""Expected CircleDto"");
        if (cd.Color != ""Red"") throw new Exception($""Expected Red, got {cd.Color}"");
        if (cd.Radius != 5.0) throw new Exception($""Expected 5.0, got {cd.Radius}"");

        Shape square = new Square { Color = ""Blue"", Side = 3.0 };
        var squareDto = M.Map(square);
        if (squareDto is not SquareDto sd) throw new Exception(""Expected SquareDto"");
        if (sd.Color != ""Blue"") throw new Exception($""Expected Blue, got {sd.Color}"");
        if (sd.Side != 3.0) throw new Exception($""Expected 3.0, got {sd.Side}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void Polymorphic_GeneratesSwitch_WithCorrectCases()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public abstract class Animal { public string Name { get; set; } = """"; }
public class Dog : Animal { public string Bark { get; set; } = """"; }
public abstract class AnimalDto { public string Name { get; set; } = """"; }
public class DogDto : AnimalDto { public string Bark { get; set; } = """"; }
[Mapper]
public static partial class M
{
    [MapDerived(typeof(Dog), typeof(DogDto))]
    public static partial AnimalDto Map(Animal s);
    public static partial DogDto MapDog(Dog d);
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("case Test.Dog d:");
        generated.Should().Contain("return MapDogInternal(d)");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void Polymorphic_AbstractBaseWithNoMatch_ReturnsNull()
    {
        // When the runtime type doesn't match any derived mapping, abstract base returns null
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public abstract class Animal { public string Name { get; set; } = """"; }
public class Dog : Animal { public string Bark { get; set; } = """"; }
public class Cat : Animal { public string Purr { get; set; } = """"; }
public abstract class AnimalDto { public string Name { get; set; } = """"; }
public class DogDto : AnimalDto { public string Bark { get; set; } = """"; }
[Mapper]
public static partial class M
{
    [MapDerived(typeof(Dog), typeof(DogDto))]
    public static partial AnimalDto Map(Animal s);
    public static partial DogDto MapDog(Dog d);
}

public static class TestRunner
{
    public static void Run()
    {
        // Dog should map correctly
        Animal dog = new Dog { Name = ""Rex"", Bark = ""Woof"" };
        var dogDto = M.Map(dog);
        if (dogDto == null) throw new Exception(""Dog should map to DogDto"");

        // Cat has no derived mapping — abstract base returns null!
        Animal cat = new Cat { Name = ""Whiskers"", Purr = ""Purrr"" };
        var catDto = M.Map(cat);
        if (catDto != null) throw new Exception(""Cat has no mapping, should return null for abstract target"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }
}
