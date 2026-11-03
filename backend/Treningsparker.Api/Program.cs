using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Treningsparker.Api.Data;
using Treningsparker.Api.Endpoints;
using Treningsparker.Api.Security;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
StartupChecks.EnsureSecretsConfigured(config, builder.Environment);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = config.GetConnectionString("Default");
    switch (config["DatabaseProvider"]?.ToLowerInvariant())
    {
        case "sqlite":
            options.UseSqlite(connectionString);
            break;
        case "sqlserver":
            options.UseSqlServer(connectionString);
            break;
        default:
            options.UseNpgsql(PostgresConnectionString(connectionString), o => o.EnableRetryOnFailure(3));
            break;
    }
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = config["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
        };
        // Tokens live for hours, so check on each request that the user still exists and that admin
        // rights haven't been revoked since the token was issued.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var user = int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                    ? await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id)
                    : null;
                if (user is null || (context.Principal!.IsInRole("Admin") && !user.IsAdmin))
                    context.Fail("User no longer exists or is no longer an admin.");
            },
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", policy => policy.RequireRole("Admin"));

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(config.GetSection("Cors:Origins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddProblemDetails();
builder.Services.AddAppRateLimiting(config);

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// Measures a database round trip, to tell slow database connections apart from a slow API.
app.MapGet("/api/health/db", async (AppDbContext db) =>
{
    var watch = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        var parks = await db.Parks.CountAsync();
        return Results.Ok(new { status = "ok", parks, elapsedMs = watch.ElapsedMilliseconds });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "error", error = ex.GetType().Name, elapsedMs = watch.ElapsedMilliseconds }, statusCode: 503);
    }
});
app.MapAuthEndpoints();
app.MapParkEndpoints();

await DbSeeder.InitializeAsync(app.Services, config, app.Logger);

app.Run();

public partial class Program
{
    // Serverless containers are paused between requests, which silently kills idle pooled connections,
    // so default to unpooled connections and short timeouts. Values set in the connection string win.
    private static string PostgresConnectionString(string? connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!ContainsKey(connectionString, "Pooling")) builder.Pooling = false;
        if (!ContainsKey(connectionString, "Timeout")) builder.Timeout = 15;
        if (!ContainsKey(connectionString, "Command Timeout")) builder.CommandTimeout = 30;
        return builder.ConnectionString;
    }

    private static bool ContainsKey(string? connectionString, string key) =>
        (connectionString ?? "").Split(';').Any(part => part.Split('=')[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase));
}
