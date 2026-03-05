using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for Bug 7: Spurious MAPO010 circular reference warnings.
/// Diamond dependencies (two parents sharing a child type) should NOT trigger MAPO010.
/// True circular references (A → B → A) SHOULD trigger MAPO010 when UseReferenceTracking is off.
/// </summary>
public class CycleDetectionTests : MapoVerifier
{
    [Fact]
    public void DiamondDependency_DoesNotTriggerCircularWarning()
    {
        // Bug 7: Parent1 and Parent2 both reference Child.
        // This is a diamond, NOT a cycle. Should produce no MAPO010.
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class ChildDto { public int Id { get; set; } }
public class ChildModel { public int Id { get; set; } }
public class Parent1Dto { public ChildDto Child { get; set; } = new(); public string Name { get; set; } = """"; }
public class Parent1Model { public ChildModel Child { get; set; } = new(); public string Name { get; set; } = """"; }
public class Parent2Dto { public ChildDto Child { get; set; } = new(); public int Value { get; set; } }
public class Parent2Model { public ChildModel Child { get; set; } = new(); public int Value { get; set; } }
public class RootDto { public Parent1Dto P1 { get; set; } = new(); public Parent2Dto P2 { get; set; } = new(); }
public class RootModel { public Parent1Model P1 { get; set; } = new(); public Parent2Model P2 { get; set; } = new(); }
[Mapper]
public partial class M { public partial RootModel Map(RootDto src); }";
        var result = RunGenerator(source);
        var diagnostics = result.Results[0].Diagnostics;
        diagnostics.Should().NotContain(d => d.Id == "MAPO010", "diamond dependencies are not circular references");
    }

    [Fact]
    public void TrueCircularReference_TriggersWarning_WhenNoTracking()
    {
        // A → B → A is a true cycle. Without UseReferenceTracking, MAPO010 is expected.
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class ADto { public BDto? B { get; set; } public int Id { get; set; } }
public class BDto { public ADto? A { get; set; } public int Id { get; set; } }
public class AModel { public BModel? B { get; set; } public int Id { get; set; } }
public class BModel { public AModel? A { get; set; } public int Id { get; set; } }
[Mapper]
public partial class M { public partial AModel Map(ADto src); }";
        var result = RunGenerator(source);
        var diagnostics = result.Results[0].Diagnostics;
        diagnostics
            .Should()
            .Contain(
                d => d.Id == "MAPO010",
                "true circular references should produce MAPO010 when UseReferenceTracking is off"
            );
    }

    [Fact]
    public void TrueCircularReference_NoWarning_WithReferenceTracking()
    {
        // With UseReferenceTracking = true, circular references are handled — no warning.
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class ADto { public BDto? B { get; set; } public int Id { get; set; } }
public class BDto { public ADto? A { get; set; } public int Id { get; set; } }
public class AModel { public BModel? B { get; set; } public int Id { get; set; } }
public class BModel { public AModel? A { get; set; } public int Id { get; set; } }
[Mapper(UseReferenceTracking = true)]
public partial class M { public partial AModel Map(ADto src); }";
        var result = RunGenerator(source);
        var diagnostics = result.Results[0].Diagnostics;
        diagnostics
            .Should()
            .NotContain(
                d => d.Id == "MAPO010",
                "UseReferenceTracking handles circular references so no warning needed"
            );
    }

    [Fact]
    public void DiamondDependency_RuntimeExecution_Works()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class ChildDto { public int Id { get; set; } }
public class ChildModel { public int Id { get; set; } }
public class Parent1Dto { public ChildDto Child { get; set; } = new(); public string Name { get; set; } = """"; }
public class Parent1Model { public ChildModel Child { get; set; } = new(); public string Name { get; set; } = """"; }
public class Parent2Dto { public ChildDto Child { get; set; } = new(); public int Value { get; set; } }
public class Parent2Model { public ChildModel Child { get; set; } = new(); public int Value { get; set; } }
public class RootDto { public Parent1Dto P1 { get; set; } = new(); public Parent2Dto P2 { get; set; } = new(); }
public class RootModel { public Parent1Model P1 { get; set; } = new(); public Parent2Model P2 { get; set; } = new(); }
[Mapper]
public partial class M { public partial RootModel Map(RootDto src); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var dto = new RootDto
        {
            P1 = new Parent1Dto { Child = new ChildDto { Id = 1 }, Name = ""P1"" },
            P2 = new Parent2Dto { Child = new ChildDto { Id = 2 }, Value = 42 }
        };
        var result = mapper.Map(dto);
        if (result.P1.Child.Id != 1) throw new Exception($""Expected 1, got {result.P1.Child.Id}"");
        if (result.P1.Name != ""P1"") throw new Exception($""Expected P1, got {result.P1.Name}"");
        if (result.P2.Child.Id != 2) throw new Exception($""Expected 2, got {result.P2.Child.Id}"");
        if (result.P2.Value != 42) throw new Exception($""Expected 42, got {result.P2.Value}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void ThreeLayerDiamond_DoesNotTriggerCircularWarning()
    {
        // A more complex diamond: Root → Branch1 → Leaf, Root → Branch2 → Leaf
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class LeafDto { public string Value { get; set; } = """"; }
public class LeafModel { public string Value { get; set; } = """"; }
public class Branch1Dto { public LeafDto Leaf { get; set; } = new(); }
public class Branch1Model { public LeafModel Leaf { get; set; } = new(); }
public class Branch2Dto { public LeafDto Leaf { get; set; } = new(); }
public class Branch2Model { public LeafModel Leaf { get; set; } = new(); }
public class RootDto { public Branch1Dto B1 { get; set; } = new(); public Branch2Dto B2 { get; set; } = new(); }
public class RootModel { public Branch1Model B1 { get; set; } = new(); public Branch2Model B2 { get; set; } = new(); }
[Mapper]
public partial class M { public partial RootModel Map(RootDto src); }";
        var result = RunGenerator(source);
        var diagnostics = result.Results[0].Diagnostics;
        diagnostics.Should().NotContain(d => d.Id == "MAPO010", "three-layer diamond is not a circular reference");
    }
}
