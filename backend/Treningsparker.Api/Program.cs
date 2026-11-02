using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Treningsparker.Api.Data;
using Treningsparker.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

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
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", policy => policy.RequireRole("Admin"));

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(config.GetSection("Cors:Origins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
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
