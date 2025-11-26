using System.Collections.Concurrent;
using System.Security.Claims;
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
builder.Services.AddSingleton<OrderStore>();
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

// Customer checkout endpoint.
app.MapPost("/orders/checkout", [Authorize] (CheckoutRequest request, ClaimsPrincipal principal, OrderStore store) =>
{
    var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (idClaim is null)
    {
        return Results.Unauthorized();
    }

    var userId = Guid.Parse(idClaim);
    var order = store.Create(userId, request);
    return Results.Created($"/orders/{order.Id}", order);
})
.WithName("Checkout");

app.MapGet("/orders", [Authorize] (ClaimsPrincipal principal, OrderStore store) =>
{
    var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (idClaim is null)
    {
        return Results.Unauthorized();
    }

    var userId = Guid.Parse(idClaim);
    return Results.Ok(store.GetByUser(userId));
})
.WithName("ListOrders");

app.MapGet("/orders/{id:guid}", [Authorize] (Guid id, ClaimsPrincipal principal, OrderStore store) =>
{
    var order = store.Get(id);
    if (order is null)
    {
        return Results.NotFound();
    }

    var userId = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var isAdmin = principal.IsInRole(Roles.Admin);
    if (!isAdmin && order.UserId != userId)
    {
        return Results.Forbid();
    }

    return Results.Ok(order);
})
.WithName("GetOrder");

app.Run();

internal sealed class OrderStore
{
    private readonly ConcurrentDictionary<Guid, OrderDto> _orders = new();

    public OrderDto Create(Guid userId, CheckoutRequest request)
    {
        var total = request.Items.Sum(item => item.UnitPrice * item.Quantity);
        var order = new OrderDto(Guid.NewGuid(), userId, request.Items, total, "created", DateTimeOffset.UtcNow);
        _orders[order.Id] = order;
        return order;
    }

    public IReadOnlyCollection<OrderDto> GetByUser(Guid userId) =>
        _orders.Values.Where(o => o.UserId == userId).OrderByDescending(o => o.CreatedAtUtc).ToArray();

    public OrderDto? Get(Guid id) => _orders.TryGetValue(id, out var order) ? order : null;
}
