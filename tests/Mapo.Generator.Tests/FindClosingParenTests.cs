using FluentAssertions;
using Xunit;
using Mapo.Generator.Emit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for FindClosingParen correctness (Issue #5).
/// Parentheses inside string literals, char literals, and verbatim strings
/// must not be counted when finding the matching closing parenthesis.
/// </summary>
public class FindClosingParenTests : MapoVerifier
{
    #region Direct unit tests for FindClosingParen

    [Fact]
    public void SimpleParens_FindsCorrectClose()
    {
        ExpressionEmitter.FindClosingParen("Map(x)", 3).Should().Be(5);
    }

    [Fact]
    public void NestedParens_FindsOuterClose()
    {
        ExpressionEmitter.FindClosingParen("Map(Foo(x))", 3).Should().Be(10);
    }

    [Fact]
    public void StringLiteral_ParensIgnored()
    {
        var expr = "Map(\"hello (world)\")";
        ExpressionEmitter.FindClosingParen(expr, 3).Should().Be(expr.Length - 1);
    }

    [Fact]
    public void CharLiteral_ParenIgnored()
    {
        var expr = "Map('(')";
        ExpressionEmitter.FindClosingParen(expr, 3).Should().Be(expr.Length - 1);
    }

    [Fact]
    public void EscapedQuoteInString_HandledCorrectly()
    {
        // Map("say \"(\"")
        var expr = "Map(\"say \\\"(\\\"\")";
        ExpressionEmitter.FindClosingParen(expr, 3).Should().Be(expr.Length - 1);
    }

    [Fact]
    public void NoClosingParen_ReturnsMinusOne()
    {
        ExpressionEmitter.FindClosingParen("Map(x", 3).Should().Be(-1);
    }

    [Fact]
    public void EmptyArgs_FindsClose()
    {
        ExpressionEmitter.FindClosingParen("Map()", 3).Should().Be(4);
    }

    [Fact]
    public void MultipleArgs_FindsClose()
    {
        ExpressionEmitter.FindClosingParen("Map(a, b, c)", 3).Should().Be(11);
    }

    [Fact]
    public void VerbatimString_ParensIgnored()
    {
        var expr = "Map(@\"test (value)\")";
        ExpressionEmitter.FindClosingParen(expr, 3).Should().Be(expr.Length - 1);
    }

    [Fact]
    public void VerbatimString_EscapedDoubleQuote()
    {
        // Map(@"say ""(""")  — verbatim string with escaped double-quote containing parens
        var expr = "Map(@\"say \"\"(\"\")\")";
        ExpressionEmitter.FindClosingParen(expr, 3).Should().Be(expr.Length - 1);
    }

    [Fact]
    public void MixedStringAndParens()
    {
        // Map("(", Foo(x), ")")
        var expr = "Map(\"(\", Foo(x), \")\")";
        ExpressionEmitter.FindClosingParen(expr, 3).Should().Be(expr.Length - 1);
    }

    #endregion

    #region Integration tests via generator

    [Fact]
    public void CustomMapping_StringWithParens_CompilesAndRuns()
    {
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public int Id { get; set; } }
public class T { public string Label { get; set; } = """"; }
[Mapper]
public static partial class M
{
    public static partial T Map(S s);
    static void Configure(IMapConfig<S, T> config)
    {
        config.Map(d => d.Label, s => ""Item ("" + s.Id + "")"");
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var s = new S { Id = 42 };
        var t = M.Map(s);
        if (t.Label != ""Item (42)"") throw new Exception($""Expected 'Item (42)', got '{t.Label}'"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void CustomMapping_NestedMethodCall_CompilesAndRuns()
    {
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public class S { public string First { get; set; } = """"; public string Last { get; set; } = """"; }
public class T { public string FullName { get; set; } = """"; }
[Mapper]
public static partial class M
{
    public static partial T Map(S s);
    static void Configure(IMapConfig<S, T> config)
    {
        config.Map(d => d.FullName, s => string.Concat(s.First, "" "", s.Last));
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var s = new S { First = ""John"", Last = ""Doe"" };
        var t = M.Map(s);
        if (t.FullName != ""John Doe"") throw new Exception($""Expected 'John Doe', got '{t.FullName}'"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    #endregion
}
