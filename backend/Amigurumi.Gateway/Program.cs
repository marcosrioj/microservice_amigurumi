using System.Net.Http.Json;
using Amigurumi.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<ServiceEndpoints>(builder.Configuration.GetSection("Services"));
builder.Services.AddHttpClient("identity", (sp, client) =>
{
    var endpoints = sp.GetRequiredService<IConfiguration>().GetSection("Services").Get<ServiceEndpoints>() ?? new();
    client.BaseAddress = new Uri(endpoints.Identity);
});
builder.Services.AddHttpClient("products", (sp, client) =>
{
    var endpoints = sp.GetRequiredService<IConfiguration>().GetSection("Services").Get<ServiceEndpoints>() ?? new();
    client.BaseAddress = new Uri(endpoints.Product);
});
builder.Services.AddHttpClient("orders", (sp, client) =>
{
    var endpoints = sp.GetRequiredService<IConfiguration>().GetSection("Services").Get<ServiceEndpoints>() ?? new();
    client.BaseAddress = new Uri(endpoints.Order);
});
builder.Services.AddSingleton<ProxyService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Identity passthrough endpoints.
app.MapPost("/api/auth/register", (RegisterRequest request, ProxyService proxy) =>
    proxy.ForwardPost<RegisterRequest, AuthResponse>("identity", "/auth/register", request));

app.MapPost("/api/auth/login", (LoginRequest request, ProxyService proxy) =>
    proxy.ForwardPost<LoginRequest, AuthResponse>("identity", "/auth/login", request));

app.MapPost("/api/auth/refresh", (RefreshRequest request, ProxyService proxy) =>
    proxy.ForwardPost<RefreshRequest, AuthResponse>("identity", "/auth/refresh", request));

// Catalog endpoints.
app.MapGet("/api/catalog", (ProxyService proxy, HttpContext ctx) =>
    proxy.ForwardGet<IEnumerable<ProductDto>>("products", "/products", ctx.Request.Headers.Authorization));

app.MapGet("/api/catalog/{id:guid}", (Guid id, ProxyService proxy, HttpContext ctx) =>
    proxy.ForwardGet<ProductDto?>("products", $"/products/{id}", ctx.Request.Headers.Authorization));

app.MapPost("/api/catalog", (CreateProductRequest request, ProxyService proxy, HttpContext ctx) =>
    proxy.ForwardPost<CreateProductRequest, ProductDto>("products", "/products", request, ctx.Request.Headers.Authorization));

app.MapPut("/api/catalog/{id:guid}", (Guid id, UpdateProductRequest request, ProxyService proxy, HttpContext ctx) =>
    proxy.ForwardPut<UpdateProductRequest, ProductDto>("products", $"/products/{id}", request, ctx.Request.Headers.Authorization));

app.MapDelete("/api/catalog/{id:guid}", (Guid id, ProxyService proxy, HttpContext ctx) =>
    proxy.ForwardDelete("products", $"/products/{id}", ctx.Request.Headers.Authorization));

// Orders endpoints.
app.MapPost("/api/orders/checkout", (CheckoutRequest request, ProxyService proxy, HttpContext ctx) =>
    proxy.ForwardPost<CheckoutRequest, OrderDto>("orders", "/orders/checkout", request, ctx.Request.Headers.Authorization));

app.MapGet("/api/orders", (ProxyService proxy, HttpContext ctx) =>
    proxy.ForwardGet<IEnumerable<OrderDto>>("orders", "/orders", ctx.Request.Headers.Authorization));

app.MapGet("/api/orders/{id:guid}", (Guid id, ProxyService proxy, HttpContext ctx) =>
    proxy.ForwardGet<OrderDto>("orders", $"/orders/{id}", ctx.Request.Headers.Authorization));

app.Run();

internal sealed class ProxyService
{
    private readonly IHttpClientFactory _factory;

    public ProxyService(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<IResult> ForwardGet<T>(string clientName, string path, string? authHeader = null)
    {
        var client = CreateClient(clientName, authHeader);
        var response = await client.GetAsync(path);
        return await ToResult<T>(response);
    }

    public async Task<IResult> ForwardPost<TRequest, TResponse>(string clientName, string path, TRequest request, string? authHeader = null)
    {
        var client = CreateClient(clientName, authHeader);
        var response = await client.PostAsJsonAsync(path, request);
        return await ToResult<TResponse>(response);
    }

    public async Task<IResult> ForwardPut<TRequest, TResponse>(string clientName, string path, TRequest request, string? authHeader = null)
    {
        var client = CreateClient(clientName, authHeader);
        var response = await client.PutAsJsonAsync(path, request);
        return await ToResult<TResponse>(response);
    }

    public async Task<IResult> ForwardDelete(string clientName, string path, string? authHeader = null)
    {
        var client = CreateClient(clientName, authHeader);
        var response = await client.DeleteAsync(path);
        return response.IsSuccessStatusCode ? Results.StatusCode((int)response.StatusCode) : Results.BadRequest(await response.Content.ReadAsStringAsync());
    }

    private HttpClient CreateClient(string name, string? authHeader)
    {
        var client = _factory.CreateClient(name);
        client.DefaultRequestHeaders.Clear();
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            client.DefaultRequestHeaders.Add("Authorization", authHeader);
        }

        return client;
    }

    private static async Task<IResult> ToResult<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<T>();
            return Results.Json(payload);
        }

        var text = await response.Content.ReadAsStringAsync();
        return Results.StatusCode((int)response.StatusCode, text);
    }
}

internal sealed record ServiceEndpoints
{
    public string Identity { get; init; } = "http://localhost:5001";
    public string Product { get; init; } = "http://localhost:5002";
    public string Order { get; init; } = "http://localhost:5003";
}
