using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Mapo.Generator.Tests;

public abstract class MapoVerifier
{
    protected Compilation CreateCompilation(string source)
    {
        return CSharpCompilation.Create("compilation",
            new[] { CSharpSyntaxTree.ParseText(source) },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Mapo.Attributes.MapperAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Linq").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Linq.Expressions").Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    protected GeneratorDriverRunResult RunGenerator(string source)
    {
        var compilation = CreateCompilation(source);
        var generator = new MapoGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        return driver.RunGenerators(compilation).GetRunResult();
    }

    protected void AssertGeneratedCodeCompiles(string source)
    {
        var compilation = CreateCompilation(source);
        var generator = new MapoGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var diagnostics = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        diagnostics.Should().BeEmpty("generated code should compile without errors");
    }

    protected void AssertGeneratedCodeRuns(string source, string typeName = "Test.TestRunner", string methodName = "Run")
    {
        var compilation = CreateCompilation(source);
        var generator = new MapoGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var diagnostics = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        diagnostics.Should().BeEmpty("generated code should compile without errors");

        using var ms = new System.IO.MemoryStream();
        var emitResult = outputCompilation.Emit(ms);
        emitResult.Success.Should().BeTrue("Compilation into memory should succeed");

        ms.Seek(0, System.IO.SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());
        var type = assembly.GetType(typeName);
        type.Should().NotBeNull($"Type {typeName} should exist in the compiled assembly");
        var method = type!.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        method.Should().NotBeNull($"Method {methodName} should exist on {typeName}");

        try
        {
            method!.Invoke(null, null);
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException ?? ex;
        }
    }
}
