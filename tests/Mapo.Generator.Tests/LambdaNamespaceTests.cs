using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

/// <summary>
/// Tests for lambda inlining with project type references (Bug 3).
/// </summary>
public class LambdaNamespaceTests : MapoVerifier
{
    [Fact]
    public void LambdaWithProjectEnum_IncludesNamespace()
    {
        // Bug 3: Lambda referencing Facility enum from a different namespace
        string source =
            @"
using Mapo.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using Test.Enums;

namespace Test.Enums
{
    public enum Facility { Parking, Wifi, Restaurant }
}

namespace Test
{
    public class LocationDto { public List<string>? Facilities { get; set; } }
    public class Location { public List<string> FacilityNames { get; set; } = new(); }

    [Mapper]
    public partial class LocationMapper
    {
        public partial Location Map(LocationDto source);

        static void Configure(IMapConfig<LocationDto, Location> config)
        {
            config.Map(d => d.FacilityNames, s => s.Facilities != null
                ? s.Facilities.Select(f => Enum.Parse<Facility>(f).ToString()).ToList()
                : new List<string>());
        }
    }
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("using Test.Enums;");
        AssertGeneratedCodeCompiles(source);
    }

    [Fact]
    public void LambdaWithMultipleNamespaces_AllIncluded()
    {
        string source =
            @"
using Mapo.Attributes;
using Test.Helpers;
using Test.Models;

namespace Test.Helpers
{
    public static class StringHelper
    {
        public static string Capitalize(string s) => s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1) : s;
    }
}

namespace Test.Models
{
    public class NameInfo { public string First { get; set; } = """"; public string Last { get; set; } = """"; }
}

namespace Test
{
    public class S { public string Name { get; set; } = """"; }
    public class T { public string FormattedName { get; set; } = """"; }

    [Mapper]
    public partial class M
    {
        public partial T Map(S s);
        static void Configure(IMapConfig<S, T> config)
        {
            config.Map(d => d.FormattedName, s => StringHelper.Capitalize(s.Name));
        }
    }
}";
        var result = RunGenerator(source);
        var generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        generated.Should().Contain("using Test.Helpers;");
        generated.Should().Contain("using Test.Models;");
        AssertGeneratedCodeCompiles(source);
    }
}
