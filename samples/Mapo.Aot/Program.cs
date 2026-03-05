using System.Text.Json.Serialization;
using Mapo.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Mapo.Aot;

// =============================================================================
// DOMAIN MODELS
// =============================================================================

public record Product(Guid Id, string Name, decimal Price, string Category);

// =============================================================================
// DTOs
// =============================================================================

public record ProductDto(Guid Id, string Name, string PriceDisplay, string Category);

public record CreateProductRequest(string Name, decimal Price, string Category);

// =============================================================================
// MAPPER
// =============================================================================

[Mapper]
public static partial class ProductMapper
{
    public static partial ProductDto MapToDto(Product product);

    public static partial Product MapProduct(CreateProductRequest request);

    public static partial List<Product> MapToEntities(List<CreateProductRequest> requests);

    static void Configure(IMapConfig<Product, ProductDto> config)
    {
        config.Map(d => d.PriceDisplay, s => s.Price.ToString("C"));
    }

    static void Configure(IMapConfig<CreateProductRequest, Product> config)
    {
        config.Map(d => d.Id, s => Guid.NewGuid());
    }
}

// =============================================================================
// AOT JSON SERIALIZATION CONTEXT
// =============================================================================

[JsonSerializable(typeof(ProductDto))]
[JsonSerializable(typeof(List<ProductDto>))]
[JsonSerializable(typeof(CreateProductRequest))]
[JsonSerializable(typeof(List<CreateProductRequest>))]
[JsonSerializable(typeof(string))]
internal partial class AppJsonSerializerContext : JsonSerializerContext { }

// =============================================================================
// REPOSITORY
// =============================================================================

public class ProductRepository
{
    private readonly List<Product> _products =
    [
        new Product(Guid.NewGuid(), "Laptop", 999.99m, "Electronics"),
        new Product(Guid.NewGuid(), "Mouse", 19.99m, "Peripherals"),
    ];

    public IEnumerable<Product> GetAll() => _products;

    public void Add(Product product) => _products.Add(product);
}

// =============================================================================
// PROGRAM
// =============================================================================

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        builder.Services.AddSingleton<ProductRepository>();

        var app = builder.Build();
        app.Urls.Add("http://localhost:5000");

        app.MapGet(
            "/products",
            (ProductRepository repo) =>
            {
                var products = repo.GetAll();
                var dtos = products.Select(ProductMapper.MapToDto).ToList();
                return Results.Ok(dtos);
            }
        );

        app.MapPost(
            "/products/batch",
            (List<CreateProductRequest> requests, ProductRepository repo) =>
            {
                var entities = ProductMapper.MapToEntities(requests);
                foreach (var entity in entities)
                    repo.Add(entity);
                return Results.Created("/products", entities.Select(ProductMapper.MapToDto).ToList());
            }
        );

        // Add a route that shuts down the app gracefully
        app.MapGet(
            "/shutdown",
            (IHostApplicationLifetime lifetime) =>
            {
                lifetime.StopApplication();
                return Results.Ok("Shutting down");
            }
        );

        Console.WriteLine("Mapo AOT Sample (Minimal API) is running...");
        app.Run();
    }
}
