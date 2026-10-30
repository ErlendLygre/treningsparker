using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Treningsparker.Api.Data;

namespace Treningsparker.Api.Endpoints;

public static class AuthEndpoints
{
    private static readonly PasswordHasher<User> Hasher = new();

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async (RegisterRequest req, AppDbContext db, IConfiguration config) =>
        {
            var email = req.Email?.Trim().ToLowerInvariant() ?? "";
            var errors = new Dictionary<string, string[]>();
            if (!email.Contains('@')) errors["email"] = ["Ugyldig e-postadresse."];
            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
                errors["password"] = ["Passordet må være minst 8 tegn."];
            if (string.IsNullOrWhiteSpace(req.DisplayName)) errors["displayName"] = ["Navn er påkrevd."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            if (await db.Users.AnyAsync(u => u.Email == email))
                return Results.Conflict(new { message = "Det finnes allerede en bruker med denne e-postadressen." });

            var user = new User { Email = email, DisplayName = req.DisplayName.Trim() };
            user.PasswordHash = Hasher.HashPassword(user, req.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Ok(CreateResponse(user, config));
        });

        group.MapPost("/login", async (LoginRequest req, AppDbContext db, IConfiguration config) =>
        {
            var email = req.Email?.Trim().ToLowerInvariant() ?? "";
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null || Hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password ?? "") == PasswordVerificationResult.Failed)
                return Results.Json(new { message = "Feil e-post eller passord." }, statusCode: StatusCodes.Status401Unauthorized);
            return Results.Ok(CreateResponse(user, config));
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(principal.GetUserId());
            return user is null ? Results.Unauthorized() : Results.Ok(ToDto(user));
        }).RequireAuthorization();
    }

    public static int GetUserId(this ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, u.IsAdmin);

    private static AuthResponse CreateResponse(User user, IConfiguration config)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.DisplayName),
        };
        if (user.IsAdmin) claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), ToDto(user));
    }
}
