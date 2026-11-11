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
        default:
            options.UseNpgsql(PostgresConnectionString(connectionString), o => o.EnableRetryOnFailure(3));
            break;
    }
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim names as they are in the token (sub, role, ...) instead of mapping them to long URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = config["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = TokenService.SigningKey(config),
            NameClaimType = AppClaims.Name,
            RoleClaimType = AppClaims.Role,
        };
        // Tokens live for hours, so check on each request that the user still exists and that admin
        // rights haven't been revoked since the token was issued.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var user = context.Principal?.GetUserId() is int id
                    ? await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, context.HttpContext.RequestAborted)
                    : null;
                if (user is null || (context.Principal!.IsInRole(AppClaims.AdminRole) && !user.IsAdmin))
                    context.Fail("User no longer exists or is no longer an admin.");
            },
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", policy => policy.RequireRole(AppClaims.AdminRole));

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(config.GetSection("Cors:Origins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddAppRateLimiting(config);
builder.Services.AddSingleton<TokenService>();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/api/health", () => TypedResults.Ok(new { status = "ok" }));

// Measures a database round trip, to tell slow database connections apart from a slow API.
app.MapGet("/api/health/db", async (AppDbContext db, CancellationToken ct) =>
{
    var started = System.Diagnostics.Stopwatch.GetTimestamp();
    try
    {
        var parks = await db.Parks.CountAsync(ct);
        return Results.Ok(new { status = "ok", parks, elapsedMs = (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds });
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        return Results.Json(new { status = "error", error = ex.GetType().Name, elapsedMs = (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds },
            statusCode: StatusCodes.Status503ServiceUnavailable);
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
