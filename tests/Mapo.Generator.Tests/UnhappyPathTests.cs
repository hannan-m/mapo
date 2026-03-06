using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Mapo.Generator.Tests;

public class UnhappyPathTests : MapoVerifier
{
    // --- [MapFrom] unhappy paths ---

    [Fact]
    public void MapFrom_NonExistentSourceProperty_ReportsUnmapped()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T
{
    [MapFrom(""DoesNotExist"")]
    public string Name { get; set; } = """";
}
[Mapper] public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        result.Diagnostics.Any(d => d.Id == "MAPO001").Should().BeTrue();
    }

    [Fact]
    public void MapFrom_ConflictsWithAutoMatch_MapFromWins()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public string Name { get; set; } = """"; public string Alt { get; set; } = """"; }
public class T
{
    [MapFrom(""Alt"")]
    public string Name { get; set; } = """";
}
[Mapper] public partial class M { public partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var result = mapper.Map(new S { Name = ""wrong"", Alt = ""correct"" });
        if (result.Name != ""correct"") throw new Exception($""Expected 'correct', got '{result.Name}'"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    // --- Diagnostic edge cases ---

    [Fact]
    public void MAPO011_UnmatchedEnumMember_EmitsDiagnostic_InStrictMode()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public enum SrcColor { Red, Green, Blue, Magenta }
public enum TgtColor { Red, Green, Blue }
public class S { public SrcColor Color { get; set; } }
public class T { public TgtColor Color { get; set; } }
[Mapper(StrictMode = true)] public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        result.Diagnostics.Any(d => d.Id == "MAPO011").Should().BeTrue();
    }

    [Fact]
    public void StrictMode_UnmappedProperty_IsError_NotWarning()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } public string Extra { get; set; } = """"; }
[Mapper(StrictMode = true)] public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var mapo001 = result.Diagnostics.FirstOrDefault(d => d.Id == "MAPO001");
        mapo001.Should().NotBeNull();
        mapo001!.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void StrictMode_IgnoredProperty_NoError()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } public string Extra { get; set; } = """"; }
[Mapper(StrictMode = true)]
public partial class M
{
    public partial T Map(S s);
    static void Configure(IMapConfig<S, T> config) { config.Ignore(d => d.Extra); }
}";
        var result = RunGenerator(source);
        result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error).Should().BeFalse();
    }

    [Fact]
    public void NonStrictMode_UnmappedProperty_IsWarning()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } public string Extra { get; set; } = """"; }
[Mapper] public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        var mapo001 = result.Diagnostics.FirstOrDefault(d => d.Id == "MAPO001");
        mapo001.Should().NotBeNull();
        mapo001!.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    // --- Enum unhappy paths ---

    [Fact]
    public void EnumPartialMatch_UnmatchedMember_DefaultsAtRuntime()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public enum SrcStatus { Active, Inactive, Deleted }
public enum TgtStatus { Active, Inactive }
public class S { public SrcStatus Status { get; set; } }
public class T { public TgtStatus Status { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var r1 = M.Map(new S { Status = SrcStatus.Active });
        if (r1.Status != TgtStatus.Active) throw new Exception($""Expected Active, got {r1.Status}"");

        var r2 = M.Map(new S { Status = SrcStatus.Deleted });
        if (r2.Status != default(TgtStatus)) throw new Exception($""Expected default, got {r2.Status}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void StringToEnum_InvalidString_ThrowsArgumentException()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public enum Status { Active, Inactive }
public class S { public string Status { get; set; } = """"; }
public class T { public Status Status { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        bool threw = false;
        try { M.Map(new S { Status = ""bogus_value"" }); }
        catch (ArgumentException) { threw = true; }
        if (!threw) throw new Exception(""Expected ArgumentException for invalid enum string"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void StringToEnum_EmptyString_ThrowsArgumentException()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public enum Status { Active, Inactive }
public class S { public string Status { get; set; } = """"; }
public class T { public Status Status { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        bool threw = false;
        try { M.Map(new S { Status = """" }); }
        catch (ArgumentException) { threw = true; }
        if (!threw) throw new Exception(""Expected ArgumentException for empty string enum"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    // --- Constructor unhappy paths ---

    [Fact]
    public void Constructor_NoMatchingParams_FallsBackToParameterless()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public int Id { get; set; } public string Name { get; set; } = """"; }
public class T
{
    public T() { }
    public T(double unrelatedA, double unrelatedB) { }
    public int Id { get; set; }
    public string Name { get; set; } = """";
}
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var t = M.Map(new S { Id = 5, Name = ""Test"" });
        if (t.Id != 5) throw new Exception($""Id: {t.Id}"");
        if (t.Name != ""Test"") throw new Exception($""Name: {t.Name}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    // --- Null input edge cases ---

    [Fact]
    public void NullSource_InstanceMapper_ThrowsArgumentNullException()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper] public partial class M { public partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        bool threw = false;
        try { mapper.Map((S)null!); }
        catch (ArgumentNullException) { threw = true; }
        if (!threw) throw new Exception(""Expected ArgumentNullException"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void NullableNestedObject_NullAtRuntime_ReturnsDefault()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
#nullable enable
public class Inner { public int Value { get; set; } }
public class InnerDto { public int Value { get; set; } }
public class S { public InnerDto? Inner { get; set; } }
public class T { public Inner? Inner { get; set; } }
[Mapper] public partial class M { public partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var r1 = mapper.Map(new S { Inner = null });
        if (r1.Inner != null) throw new Exception(""Expected null Inner"");

        var r2 = mapper.Map(new S { Inner = new InnerDto { Value = 42 } });
        if (r2.Inner == null) throw new Exception(""Expected non-null Inner"");
        if (r2.Inner.Value != 42) throw new Exception($""Expected 42, got {r2.Inner.Value}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    // --- Flattening unhappy paths ---

    [Fact]
    public void Flattening_NullIntermediate_ReturnsDefault()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class Address { public string City { get; set; } = """"; }
public class S { public Address? Address { get; set; } }
public class T { public string AddressCity { get; set; } = """"; }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var r1 = M.Map(new S { Address = null });
        if (r1.AddressCity != default(string)) throw new Exception($""Expected null, got '{r1.AddressCity}'"");

        var r2 = M.Map(new S { Address = new Address { City = ""Berlin"" } });
        if (r2.AddressCity != ""Berlin"") throw new Exception($""Expected Berlin, got '{r2.AddressCity}'"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void Flattening_UnmatchedName_ReportsMAPO001()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class Address { public string City { get; set; } = """"; }
public class S { public Address Address { get; set; } = new(); }
public class T { public string AddressZipCode { get; set; } = """"; }
[Mapper] public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        result.Diagnostics.Any(d => d.Id == "MAPO001").Should().BeTrue();
    }

    // --- Update mapping edge cases ---

    [Fact]
    public void UpdateMapping_IgnoredProperty_PreservesOriginalValue()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public string Name { get; set; } = """"; public int Score { get; set; } }
public class T { public string Name { get; set; } = """"; public int Score { get; set; } public string Secret { get; set; } = """"; }
[Mapper]
public partial class M
{
    public partial void Apply(S s, T t);
    static void Configure(IMapConfig<S, T> config) { config.Ignore(d => d.Secret); }
}

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var target = new T { Name = ""old"", Score = 0, Secret = ""keep-me"" };
        mapper.Apply(new S { Name = ""new"", Score = 99 }, target);
        if (target.Name != ""new"") throw new Exception($""Name: {target.Name}"");
        if (target.Score != 99) throw new Exception($""Score: {target.Score}"");
        if (target.Secret != ""keep-me"") throw new Exception($""Secret was overwritten"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void UpdateMapping_NullSource_DoesNotModifyTarget()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public string Name { get; set; } = """"; }
public class T { public string Name { get; set; } = """"; }
[Mapper] public partial class M { public partial void Apply(S s, T t); }

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var target = new T { Name = ""original"" };
        mapper.Apply(null!, target);
        if (target.Name != ""original"") throw new Exception($""Name was modified to: {target.Name}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    // --- Converter unhappy paths ---

    [Fact]
    public void Converter_NullableSourceWithNullValue_ReturnsDefault()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
#nullable enable
public class S { public string? RawId { get; set; } }
public class T { public Guid Id { get; set; } }
[Mapper]
public partial class M
{
    public partial T Map(S s);
    static void Configure(IMapConfig<S, T> config)
    {
        config.AddConverter<string, Guid>(x => Guid.Parse(x))
              .Map(d => d.Id, s => s.RawId);
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var r1 = mapper.Map(new S { RawId = null });
        if (r1.Id != default(Guid)) throw new Exception($""Expected default Guid, got {r1.Id}"");

        var id = Guid.NewGuid();
        var r2 = mapper.Map(new S { RawId = id.ToString() });
        if (r2.Id != id) throw new Exception($""Expected {id}, got {r2.Id}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    // --- Mixed creation + update in same mapper ---

    [Fact]
    public void SameMapper_CreationAndUpdate_DifferentTypePairs_BothWork()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class CreateS { public int Id { get; set; } public string Name { get; set; } = """"; }
public class UpdateS { public int Id { get; set; } public string Name { get; set; } = """"; }
public class T { public int Id { get; set; } public string Name { get; set; } = """"; }
[Mapper]
public partial class M
{
    public partial T Create(CreateS s);
    public partial void Update(UpdateS s, T t);
}

public static class TestRunner
{
    public static void Run()
    {
        var mapper = new M();
        var created = mapper.Create(new CreateS { Id = 1, Name = ""A"" });
        if (created.Id != 1) throw new Exception($""Create Id: {created.Id}"");

        mapper.Update(new UpdateS { Id = 2, Name = ""B"" }, created);
        if (created.Id != 2) throw new Exception($""Update Id: {created.Id}"");
        if (created.Name != ""B"") throw new Exception($""Update Name: {created.Name}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    // --- Zero/empty value mapping ---

    [Fact]
    public void ZeroAndEmptyString_MappedCorrectly_NotSkipped()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public int Count { get; set; } public string Name { get; set; } = """"; public decimal Amount { get; set; } }
public class T { public int Count { get; set; } = 999; public string Name { get; set; } = ""default""; public decimal Amount { get; set; } = 1m; }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var t = M.Map(new S { Count = 0, Name = """", Amount = 0m });
        if (t.Count != 0) throw new Exception($""Count: {t.Count}"");
        if (t.Name != """") throw new Exception($""Name should be empty, got '{t.Name}'"");
        if (t.Amount != 0m) throw new Exception($""Amount: {t.Amount}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    // --- Nullable value types all null ---

    [Fact]
    public void AllNullableValueTypes_WhenNull_DefaultToZero()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public int? A { get; set; } public long? B { get; set; } public double? C { get; set; } public bool? D { get; set; } }
public class T { public int A { get; set; } public long B { get; set; } public double C { get; set; } public bool D { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var t = M.Map(new S { A = null, B = null, C = null, D = null });
        if (t.A != 0) throw new Exception($""A: {t.A}"");
        if (t.B != 0L) throw new Exception($""B: {t.B}"");
        if (t.C != 0.0) throw new Exception($""C: {t.C}"");
        if (t.D != false) throw new Exception($""D: {t.D}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    // --- Collection with null elements ---

    [Fact]
    public void Collection_WithNullElements_PassedThrough()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;
#nullable enable
public class Inner { public int Id { get; set; } }
public class InnerDto { public int Id { get; set; } }
public class S { public List<InnerDto?> Items { get; set; } = new(); }
public class T { public List<Inner?> Items { get; set; } = new(); }
[Mapper] public partial class M { public partial T Map(S s); }";
        AssertGeneratedCodeCompiles(source);
    }

    // --- Empty collection mapping ---

    [Fact]
    public void EmptyCollection_MappedToEmptyCollection()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
namespace Test;
public class ItemS { public int Id { get; set; } }
public class ItemT { public int Id { get; set; } }
public class S { public List<ItemS> Items { get; set; } = new(); }
public class T { public List<ItemT> Items { get; set; } = new(); }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var t = M.Map(new S { Items = new List<ItemS>() });
        if (t.Items == null) throw new Exception(""Items should not be null"");
        if (t.Items.Count != 0) throw new Exception($""Expected 0 items, got {t.Items.Count}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    // --- Deeply nested null chain ---

    [Fact]
    public void DeepFlatten_NullIntermediate_ReturnsNullForReferenceTypes()
    {
        string source =
            @"
using Mapo.Attributes;
using System;
namespace Test;
public class Address { public string City { get; set; } = """"; public int ZipCode { get; set; } }
public class Person { public Address? Address { get; set; } }
public class FlatPerson
{
    public string AddressCity { get; set; } = """";
    public int AddressZipCode { get; set; }
}
[Mapper] public static partial class M { public static partial FlatPerson Map(Person s); }

public static class TestRunner
{
    public static void Run()
    {
        // Null intermediate → defaults
        var r1 = M.Map(new Person { Address = null });
        if (r1.AddressCity != null) throw new Exception($""City should be null, got '{r1.AddressCity}'"");
        if (r1.AddressZipCode != 0) throw new Exception($""ZipCode should be 0, got {r1.AddressZipCode}"");

        // Populated → maps correctly
        var r2 = M.Map(new Person { Address = new Address { City = ""Berlin"", ZipCode = 10115 } });
        if (r2.AddressCity != ""Berlin"") throw new Exception($""City: '{r2.AddressCity}'"");
        if (r2.AddressZipCode != 10115) throw new Exception($""ZipCode: {r2.AddressZipCode}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }
}
