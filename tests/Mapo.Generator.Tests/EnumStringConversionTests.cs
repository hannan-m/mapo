using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

public class EnumStringConversionTests : MapoVerifier
{
    [Fact]
    public void EnumToString_GeneratesSuccessfully()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public enum Status { Active, Inactive }
public class S { public Status Status { get; set; } }
public class T { public string Status { get; set; } = """"; }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain(".ToString()");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void StringToEnum_GeneratesSuccessfully()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public enum Status { Active, Inactive }
public class S { public string Status { get; set; } = """"; }
public class T { public Status Status { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("Enum.Parse");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void EnumToString_WithCustomConverter_ConverterTakesPrecedence()
    {
        string source = @"
using Mapo.Attributes;
namespace Test;
public enum Status { Active, Inactive }
public class S { public Status Status { get; set; } }
public class T { public string Status { get; set; } = """"; }
[Mapper] public static partial class M
{
    public static partial T Map(S s);
    static void Configure(IMapConfig<S, T> config)
    {
        config.AddConverter<Status, string>(e => e == Status.Active ? ""ON"" : ""OFF"");
    }
}";

        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        // Converter should be used, not .ToString()
        generated.Should().Contain("ON");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void EnumToString_CompilesAndRuns()
    {
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public enum Color { Red, Green, Blue }
public class S { public Color Color { get; set; } }
public class T { public string Color { get; set; } = """"; }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var s = new S { Color = Color.Blue };
        var t = M.Map(s);
        if (t.Color != ""Blue"") throw new Exception($""Expected 'Blue', got '{t.Color}'"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void StringToEnum_CompilesAndRuns()
    {
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public enum Color { Red, Green, Blue }
public class S { public string Color { get; set; } = """"; }
public class T { public Color Color { get; set; } }
[Mapper] public static partial class M { public static partial T Map(S s); }

public static class TestRunner
{
    public static void Run()
    {
        var s = new S { Color = ""Green"" };
        var t = M.Map(s);
        if (t.Color != Color.Green) throw new Exception($""Expected Green, got {t.Color}"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }

    [Fact]
    public void EnumToString_WithCustomMapping_Works()
    {
        string source = @"
using Mapo.Attributes;
using System;
namespace Test;
public enum Status { Active, Inactive }
public class S { public Status MyStatus { get; set; } }
public class T { public string StatusLabel { get; set; } = """"; }
[Mapper] public static partial class M
{
    public static partial T Map(S s);
    static void Configure(IMapConfig<S, T> config)
    {
        config.Map(d => d.StatusLabel, s => s.MyStatus);
    }
}

public static class TestRunner
{
    public static void Run()
    {
        var s = new S { MyStatus = Status.Active };
        var t = M.Map(s);
        if (t.StatusLabel != ""Active"") throw new Exception($""Expected 'Active', got '{t.StatusLabel}'"");
    }
}";
        AssertGeneratedCodeRuns(source);
    }
}
