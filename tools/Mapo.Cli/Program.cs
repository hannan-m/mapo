using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mapo.Generator.Syntax;
using Mapo.Generator.Emit;

namespace Mapo.Cli;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: mapo-gen <input-dir> <output-dir>");
            return 1;
        }

        string inputDir = Path.GetFullPath(args[0]);
        string outputDir = Path.GetFullPath(args[1]);

        if (!Directory.Exists(inputDir))
        {
            Console.WriteLine($"Error: Input directory '{inputDir}' does not exist.");
            return 1;
        }

        Console.WriteLine($"🔍 Scanning for mappers in: {inputDir}");
        
        // 1. Find all C# files
        var csharpFiles = Directory.GetFiles(inputDir, "*.cs", SearchOption.AllDirectories);
        var syntaxTrees = csharpFiles.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f))).ToList();

        // 2. Create Compilation (needed for Semantic Model)
        var compilation = CSharpCompilation.Create("MapoTemp")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(typeof(Mapo.Attributes.MapperAttribute).Assembly.Location))
            .AddSyntaxTrees(syntaxTrees);

        int count = 0;
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

        // 3. Process each tree
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
                    Console.WriteLine($"✨ Generating: {result.Mapper.ClassName}");
                    string source = MapperEmitter.Emit(result.Mapper);
                    string filePath = Path.Combine(outputDir, $"{result.Mapper.ClassName}.g.cs");
                    File.WriteAllText(filePath, source);
                    count++;
                }
            }
        }

        Console.WriteLine($"\n✅ Done! Generated {count} mapper(s) in: {outputDir}");
        return 0;
    }
}

// Minimal polyfill for the context if needed, but since we reference the generator, 
// we should check if GeneratorSyntaxContext can be instantiated easily.
// Actually, GeneratorSyntaxContext is a struct in Microsoft.CodeAnalysis.
