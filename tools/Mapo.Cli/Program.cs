using Mapo.Generator.Emit;
using Mapo.Generator.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mapo.Cli;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
            return ShowHelp();

        if (args[0] is "--version" or "-v")
        {
            Console.WriteLine(typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");
            return 0;
        }

        if (args[0] != "gen")
        {
            Console.Error.WriteLine($"Unknown command: {args[0]}");
            Console.Error.WriteLine("Run 'mapo --help' for usage.");
            return 1;
        }

        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: mapo gen <input-dir> <output-dir>");
            return 1;
        }

        return Generate(args[1], args[2]);
    }

    static int Generate(string inputArg, string outputArg)
    {
        string inputDir = Path.GetFullPath(inputArg);
        string outputDir = Path.GetFullPath(outputArg);

        if (!Directory.Exists(inputDir))
        {
            Console.Error.WriteLine($"Error: Input directory '{inputDir}' does not exist.");
            return 1;
        }

        var csharpFiles = Directory.GetFiles(inputDir, "*.cs", SearchOption.AllDirectories);
        if (csharpFiles.Length == 0)
        {
            Console.Error.WriteLine($"Error: No .cs files found in '{inputDir}'.");
            return 1;
        }

        Console.WriteLine($"Scanning {csharpFiles.Length} file(s) in: {inputDir}");

        var syntaxTrees = csharpFiles.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f)).ToList();

        var compilation = CSharpCompilation
            .Create("MapoTemp")
            .AddReferences(
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Mapo.Attributes.MapperAttribute).Assembly.Location)
            )
            .AddSyntaxTrees(syntaxTrees);

        Directory.CreateDirectory(outputDir);

        int count = 0;
        foreach (var tree in syntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetCompilationUnitRoot();
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classes)
            {
                var result = MapperParser.Parse(classDecl, model);

                if (result?.Mapper != null)
                {
                    string source = MapperEmitter.Emit(result.Mapper);
                    string filePath = Path.Combine(outputDir, $"{result.Mapper.ClassName}.g.cs");
                    File.WriteAllText(filePath, source);
                    Console.WriteLine($"  Generated: {result.Mapper.ClassName}.g.cs");
                    count++;
                }
            }
        }

        if (count == 0)
        {
            Console.WriteLine("No [Mapper] classes found.");
            return 0;
        }

        Console.WriteLine($"Done. Generated {count} mapper(s) in: {outputDir}");
        return 0;
    }

    static int ShowHelp()
    {
        Console.WriteLine(
            """
            mapo - Compile-time object mapping code generator for .NET

            Usage:
              mapo gen <input-dir> <output-dir>
              mapo --help
              mapo --version

            Commands:
              gen    Scan C# source files for [Mapper] classes and generate mapping code.

            Arguments:
              <input-dir>    Directory containing C# source files to scan (recursive).
              <output-dir>   Directory where generated .g.cs files are written.

            Examples:
              mapo gen src/Models generated/
              mapo gen . ./Generated
            """
        );
        return 0;
    }
}
