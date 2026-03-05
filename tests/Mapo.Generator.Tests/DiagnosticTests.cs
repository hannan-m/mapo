using Microsoft.CodeAnalysis;
using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

public class DiagnosticTests : MapoVerifier
{
    [Fact]
    public void StrictMode_MissingProperty_ReportsError()
    {
        string source = "using Mapo.Attributes; namespace Test; public class S { public int Id { get; set; } } public class T { public int Id { get; set; } public string Name { get; set; } } [Mapper(StrictMode = true)] public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error).Should().BeTrue();
    }

    [Fact]
    public void NonPartialClass_ReportsMAPO003()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper]
public class M { public T Map(S s) => null!; }";
        var result = RunGenerator(source);
        result.Diagnostics.Any(d => d.Id == "MAPO003").Should().BeTrue();
    }

    [Fact]
    public void NonStaticConfigure_ReportsMAPO004()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper]
public partial class M
{
    public partial T Map(S s);
    void Configure(IMapConfig<S, T> config) { }
}";
        var result = RunGenerator(source);
        result.Diagnostics.Any(d => d.Id == "MAPO004").Should().BeTrue();
    }

    [Fact]
    public void EmptyTypes_ReportsMAPO005()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { }
public class T { }
[Mapper]
public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        result.Diagnostics.Any(d => d.Id == "MAPO005").Should().BeTrue();
    }

    [Fact]
    public void DuplicateMapping_ReportsMAPO006()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } public string Alt { get; set; } = """"; }
public class T { public string Name { get; set; } = """"; }
[Mapper]
public partial class M
{
    public partial T Map(S s);
    static void Configure(IMapConfig<S, T> config)
    {
        config.Map(d => d.Name, s => s.Id.ToString())
              .Map(d => d.Name, s => s.Alt);
    }
}";
        var result = RunGenerator(source);
        result.Diagnostics.Any(d => d.Id == "MAPO006").Should().BeTrue();
    }

    [Fact]
    public void InvalidTargetProperty_ReportsMAPO007()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper]
public partial class M
{
    public partial T Map(S s);
    static void Configure(IMapConfig<S, T> config)
    {
        config.Map(d => d.NonExistent, s => s.Id);
    }
}";
        var result = RunGenerator(source);
        result.Diagnostics.Any(d => d.Id == "MAPO007").Should().BeTrue();
    }

    [Fact]
    public void NullableToNonNullable_ReportsMAPO009()
    {
        string source = @"
#nullable enable
using Mapo.Attributes;
namespace Test;
public class S { public string? Name { get; set; } }
public class T { public string Name { get; set; } = """"; }
[Mapper]
public partial class M { public partial T Map(S s); }";
        var result = RunGenerator(source);
        result.Diagnostics.Any(d => d.Id == "MAPO009").Should().BeTrue();
    }
}
