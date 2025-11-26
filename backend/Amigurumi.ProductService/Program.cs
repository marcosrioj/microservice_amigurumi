using System.Collections.Concurrent;
using System.Text;
using Amigurumi.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();
builder.Services.AddSingleton<ProductStore>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var settings = builder.Configuration.GetSection("Jwt");
        var secret = settings.GetValue<string>("SecretKey") ?? "dev-secret-change-this-please";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = settings.GetValue<string>("Issuer"),
            ValidAudience = settings.GetValue<string>("Audience"),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
        };
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Product catalog is kept in-memory for the demo. Replace with EF Core or Dapper when wiring a real database.
app.MapGet("/products", (ProductStore store) =>
{
    return Results.Ok(store.GetAll());
})
.WithName("ListProducts");

app.MapGet("/products/{id:guid}", (Guid id, ProductStore store) =>
{
    var product = store.Get(id);
    return product is null ? Results.NotFound() : Results.Ok(product);
})
.WithName("GetProduct");

app.MapPost("/products", [Authorize(Roles = Roles.Admin)] (CreateProductRequest request, ProductStore store) =>
{
    var created = store.Create(request);
    return Results.Created($"/products/{created.Id}", created);
})
.WithName("CreateProduct");

app.MapPut("/products/{id:guid}", [Authorize(Roles = Roles.Admin)] (Guid id, UpdateProductRequest request, ProductStore store) =>
{
    var updated = store.Update(id, request);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
})
.WithName("UpdateProduct");

app.MapDelete("/products/{id:guid}", [Authorize(Roles = Roles.Admin)] (Guid id, ProductStore store) =>
{
    return store.Delete(id) ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteProduct");

app.Run();

internal sealed class ProductStore
{
    private readonly ConcurrentDictionary<Guid, ProductDto> _products = new();

    public ProductStore()
    {
        // Seed with a few examples so the UI has data on first run.
        Create(new CreateProductRequest("Octopus Amigurumi", "Handmade purple octopus", 28.50m, 10, ["octopus", "soft"]));
        Create(new CreateProductRequest("Bunny Amigurumi", "Pastel bunny with scarf", 24.00m, 15, ["bunny", "cute"]));
        Create(new CreateProductRequest("Dinosaur Amigurumi", "Green dino with spikes", 32.00m, 7, ["dino", "green"]));
    }

    public IReadOnlyCollection<ProductDto> GetAll() => _products.Values.OrderBy(p => p.Name).ToArray();

    public ProductDto? Get(Guid id) => _products.TryGetValue(id, out var product) ? product : null;

    public ProductDto Create(CreateProductRequest request)
    {
        var product = new ProductDto(Guid.NewGuid(), request.Name, request.Description, request.Price, request.Stock, request.Tags);
        _products[product.Id] = product;
        return product;
    }

    public ProductDto? Update(Guid id, UpdateProductRequest request)
    {
        if (!_products.ContainsKey(id))
        {
            return null;
        }

        var product = new ProductDto(id, request.Name, request.Description, request.Price, request.Stock, request.Tags);
        _products[id] = product;
        return product;
    }

    public bool Delete(Guid id) => _products.TryRemove(id, out _);
}
