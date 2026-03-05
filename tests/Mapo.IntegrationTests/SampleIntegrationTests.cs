using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Mapo.IntegrationTests;

public class SampleIntegrationTests
{
    private string GetProjectPath(string projectName)
    {
        var currentDir = Directory.GetCurrentDirectory();
        Console.WriteLine($"Current directory: {currentDir}");
        
        // Find root by looking for Mapo.slnx or .git
        var dir = new DirectoryInfo(currentDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Mapo.slnx")))
        {
            dir = dir.Parent;
        }
        
        if (dir == null) throw new Exception("Could not find project root");
        
        return Path.Combine(dir.FullName, "samples", projectName, $"{projectName}.csproj");
    }

    private async Task<(int ExitCode, string Output)> RunProject(string projectName)
    {
        var projectPath = GetProjectPath(projectName);
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" --configuration Release --no-build",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();
        
        var output = await outputTask;
        var error = await errorTask;

        return (process.ExitCode, output + "\nERROR:\n" + error);
    }

    [Fact]
    public async Task Mapo_Sample_Runs_Successfully()
    {
        var (exitCode, output) = await RunProject("Mapo.Sample");
        exitCode.Should().Be(0);
        output.Should().Contain("=== Mapo Feature Showcase ===");
        // Basic mapping + init/required
        output.Should().Contain("FullName (required init): Jane Smith");
        output.Should().Contain("Email (required init): jane.smith@example.com");
        // Enum conversions
        output.Should().Contain("\"Gold\"");
        output.Should().Contain("OrderStatusLabel.Shipped");
        output.Should().Contain("OrderStatus.Confirmed");
        // Nullable coercion
        output.Should().Contain("1250 (int?) -> 1250 (int)");
        output.Should().Contain("0 (default)");
        // Deep flattening
        output.Should().Contain("Seattle");
        output.Should().Contain("Pacific Northwest");
        output.Should().Contain("United States");
        // DI + converter
        output.Should().Contain("PlacedAt: 2025-03-01");
        // Collections
        output.Should().Contain("Wireless Mouse x2");
        output.Should().Contain("Mechanical Keyboard x1");
        // Static + extension
        output.Should().Contain("Extension:");
        // Async streaming
        output.Should().Contain("Streamed: Wireless Mouse");
        // Reverse mapping
        output.Should().Contain("Summary -> Product:");
        // Update + ignore
        output.Should().Contain("SKU=WM-001 (preserved)");
        // Completion
        output.Should().Contain("All features demonstrated successfully");
    }

    [Fact]
    public async Task Mapo_Circular_Runs_Successfully()
    {
        var (exitCode, output) = await RunProject("Mapo.Circular");
        exitCode.Should().Be(0);
        output.Should().Contain("=== Mapo Social Network (Circular) Sample ===");
        output.Should().Contain("User: alice");
        output.Should().Contain("Reference tracking prevented infinite recursion");
    }

    [Fact]
    public async Task Mapo_Polymorphic_Runs_Successfully()
    {
        var (exitCode, output) = await RunProject("Mapo.Polymorphic");
        exitCode.Should().Be(0);
        output.Should().Contain("=== Mapo Polymorphic Notification Sample ===");
        output.Should().Contain("Email to user_1");
        output.Should().Contain("SMS to user_2");
        output.Should().Contain("Push to user_1");
    }

    [Fact]
    public async Task Mapo_Aot_Runs_Successfully()
    {
        var projectPath = GetProjectPath("Mapo.Aot");
        // For Aot, we need to run it in background and make requests
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" --configuration Release --no-build",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = "";
        var error = "";

        try
        {
            // Give it a moment to start
            await Task.Delay(3000);

            if (process.HasExited)
            {
                output = await process.StandardOutput.ReadToEndAsync();
                error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"Process exited early with code {process.ExitCode}. \nOutput: {output}\nError: {error}");
            }

            using var client = new HttpClient();
            // Minimal APIs by default might use random port in some templates, 
            // but WebApplication.CreateSlimBuilder usually defaults to 5000 if not configured.
            // Let's check if it's listening.
            client.BaseAddress = new Uri("http://localhost:5000"); 
            
            HttpResponseMessage response;
            try {
                response = await client.GetAsync("/products");
            } catch (Exception ex) {
                output = await process.StandardOutput.ReadToEndAsync();
                error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"Failed to connect to AOT app: {ex.Message}. \nOutput: {output}\nError: {error}");
            }

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Laptop");
            content.Should().Contain("Mouse");

            // Shut down
            await client.GetAsync("/shutdown");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
    }
}
