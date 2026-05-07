using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Yarp.ReverseProxy;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;

using StackExchange.Redis;

using Shared.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
    ?? builder.Configuration["Jwt:SecretKey"] 
    ?? throw new InvalidOperationException("JWT_SECRET_KEY not configured");

var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
    ?? builder.Configuration["Jwt:Issuer"] 
    ?? "StockOrchestra";

var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
    ?? builder.Configuration["Jwt:Audience"] 
    ?? "StockOrchestra-Api";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddMemoryCache();

var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") 
    ?? builder.Configuration["Cors:AllowedOrigins"] 
    ?? "http://localhost:3000";

builder.Services.AddCors(options =>
{
    options.AddPolicy("SecureFrontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins.Split(','))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});

builder.Services.AddHealthChecks();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
{
    var connStr = Environment.GetEnvironmentVariable("REDIS_CONNECTION") 
        ?? "localhost:6379,password=redis_secure_pass_2024,abortConnect=false";
    return ConnectionMultiplexer.Connect(connStr);
});
builder.Logging.AddConsole();

var app = builder.Build();

// app.UseIpRateLimiting();
app.UseCors("SecureFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = context.User.FindFirst(ClaimTypes.Name)?.Value;
            var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value);
            
            context.Request.Headers["X-User-Id"] = userId ?? "";
            context.Request.Headers["X-Username"] = username ?? "";
            context.Request.Headers["X-User-Roles"] = string.Join(",", roles);
        }
        
        await next();
    });
});

app.MapHealthChecks("/health");
app.MapGet("/gateway-info", () => "StockOrchestra API Gateway v1.0");

app.MapHub<PriceHub>("/price-hub");

app.MapGet("/api/prices", async (string? symbol, IConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();
    var key = $"price:{symbol?.ToUpperInvariant() ?? "BTC"}:discovered";
    var json = await db.StringGetAsync(key);
    
    if (json.IsNullOrEmpty)
    {
        return Results.NotFound(new { error = "Price not found" });
    }
    
    return Results.Ok(JsonSerializer.Deserialize<object>(json!));
});

app.Run();

public class PriceHub : Hub
{
    public async Task JoinSymbolGroup(string symbol)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, symbol);
    }

    public async Task LeaveSymbolGroup(string symbol)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, symbol);
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}