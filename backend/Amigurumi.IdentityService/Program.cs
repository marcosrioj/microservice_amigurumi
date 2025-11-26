using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Amigurumi.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var settings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? JwtSettings.Default();
        var key = Encoding.UTF8.GetBytes(settings.SecretKey);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Roles.Admin, policy => policy.RequireRole(Roles.Admin));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<UserStore>();
builder.Services.AddSingleton<TokenService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Registration creates a user and seeds a refresh token.
app.MapPost("/auth/register", (RegisterRequest request, UserStore store, TokenService tokenService) =>
{
    if (store.Exists(request.Email))
    {
        return Results.Conflict(new { message = "Email already registered." });
    }

    var user = store.CreateUser(request);
    var auth = tokenService.IssueTokens(user);
    return Results.Ok(auth);
})
.WithName("Register");

// Login validates password and returns tokens.
app.MapPost("/auth/login", (LoginRequest request, UserStore store, TokenService tokenService) =>
{
    var user = store.GetByEmail(request.Email);
    if (user is null || !user.ValidatePassword(request.Password))
    {
        return Results.Unauthorized();
    }

    var auth = tokenService.IssueTokens(user);
    return Results.Ok(auth);
})
.WithName("Login");

// Refresh exchanges a refresh token for a new access token.
app.MapPost("/auth/refresh", (RefreshRequest request, UserStore store, TokenService tokenService) =>
{
    var user = store.GetByRefreshToken(request.RefreshToken);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var auth = tokenService.IssueTokens(user, request.RefreshToken);
    return Results.Ok(auth);
})
.WithName("RefreshToken");

// Returns profile info for the currently authenticated user.
app.MapGet("/auth/me", (ClaimsPrincipal principal, UserStore store) =>
{
    var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (idClaim is null)
    {
        return Results.Unauthorized();
    }

    var user = store.GetById(Guid.Parse(idClaim));
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var response = new MeResponse(user.Id, user.Email, user.DisplayName, user.Role);
    return Results.Ok(response);
})
.RequireAuthorization()
.WithName("Me");

app.Run();

internal sealed class UserStore
{
    private readonly ConcurrentDictionary<string, User> _usersByEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _refreshIndex = new();

    public bool Exists(string email) => _usersByEmail.ContainsKey(email);

    public User CreateUser(RegisterRequest request)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim().ToLowerInvariant(),
            DisplayName = request.DisplayName,
            Role = request.IsAdmin ? Roles.Admin : Roles.Customer
        };

        user.SetPassword(request.Password);
        _usersByEmail[user.Email] = user;
        return user;
    }

    public User? GetByEmail(string email) =>
        _usersByEmail.TryGetValue(email.Trim().ToLowerInvariant(), out var user) ? user : null;

    public User? GetById(Guid id) =>
        _usersByEmail.Values.SingleOrDefault(u => u.Id == id);

    public User? GetByRefreshToken(string refreshToken) =>
        _refreshIndex.TryGetValue(refreshToken, out var email) ? GetByEmail(email) : null;

    public void TrackRefreshToken(User user, string refreshToken)
    {
        _refreshIndex[refreshToken] = user.Email;
    }
}

internal sealed class TokenService
{
    private readonly JwtSecurityTokenHandler _handler = new();
    private readonly JwtSettings _settings;
    private readonly UserStore _store;

    public TokenService(IConfiguration config, UserStore store)
    {
        _settings = config.GetSection("Jwt").Get<JwtSettings>() ?? JwtSettings.Default();
        _store = store;
    }

    public AuthResponse IssueTokens(User user, string? existingRefresh = null)
    {
        var access = CreateAccessToken(user);
        var refresh = existingRefresh ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        _store.TrackRefreshToken(user, refresh);

        return new AuthResponse(access.Token, refresh, access.ExpiresAtUtc);
    }

    private AccessToken CreateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTimeOffset.UtcNow.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role)
        };

        var jwt = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return new AccessToken(_handler.WriteToken(jwt), expires);
    }
}

internal sealed record AccessToken(string Token, DateTimeOffset ExpiresAtUtc);

internal sealed class User
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = Roles.Customer;
    public string PasswordHash { get; private set; } = string.Empty;

    public void SetPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        PasswordHash = Convert.ToBase64String(bytes);
    }

    public bool ValidatePassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return PasswordHash.Equals(Convert.ToBase64String(bytes), StringComparison.Ordinal);
    }
}

internal sealed record JwtSettings
{
    public string Issuer { get; init; } = "amigurumi.identity";
    public string Audience { get; init; } = "amigurumi.clients";
    public string SecretKey { get; init; } = "please-change-me-in-appsettings-super-secret";
    public int AccessTokenMinutes { get; init; } = 60;

    public static JwtSettings Default() => new();
}
