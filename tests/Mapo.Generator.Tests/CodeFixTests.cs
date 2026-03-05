using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

public class CodeFixTests : MapoVerifier
{
    [Fact]
    public void MAPO003_NonPartialClass_DiagnosticEmitted()
    {
        string source =
            @"
using Mapo.Attributes;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public int Id { get; set; } }
[Mapper]
public class M { public T Map(S s) => null!; }";

        var result = RunGenerator(source);
        result.Diagnostics.Any(d => d.Id == "MAPO003").Should().BeTrue();
    }
}
