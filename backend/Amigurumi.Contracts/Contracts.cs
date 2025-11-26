namespace Amigurumi.Contracts;

// Shared DTOs and records passed between services and the frontends.

public static class Roles
{
    public const string Admin = "admin";
    public const string Customer = "customer";
}

public record RegisterRequest(string Email, string Password, string DisplayName, bool IsAdmin = false);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);
public record MeResponse(Guid UserId, string Email, string DisplayName, string Role);

public record ProductDto(Guid Id, string Name, string Description, decimal Price, int Stock, string[] Tags);
public record CreateProductRequest(string Name, string Description, decimal Price, int Stock, string[] Tags);
public record UpdateProductRequest(string Name, string Description, decimal Price, int Stock, string[] Tags);

public record CartItemDto(Guid ProductId, int Quantity, decimal UnitPrice);
public record CheckoutRequest(IReadOnlyList<CartItemDto> Items, string PaymentMethod, string ShippingAddress);
public record OrderDto(Guid Id, Guid UserId, IReadOnlyList<CartItemDto> Items, decimal Total, string Status, DateTimeOffset CreatedAtUtc);
