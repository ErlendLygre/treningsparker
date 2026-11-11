using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treningsparker.Api.Data;
using Treningsparker.Api.Security;

namespace Treningsparker.Api.Endpoints;

public static class AuthEndpoints
{
    private static readonly PasswordHasher<User> Hasher = new();
    // Verified against when the account doesn't exist, so the response time doesn't reveal valid emails.
    private static readonly User DummyUser = new() { Email = "", DisplayName = "" };
    private static readonly string DummyHash = Hasher.HashPassword(DummyUser, "dummy-password");

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").RequireRateLimiting(RateLimiting.Auth);

        group.MapPost("/register", async Task<Results<Ok<AuthResponse>, Conflict<MessageResponse>>> (
            RegisterRequest req, AppDbContext db, TokenService tokens, CancellationToken ct) =>
        {
            var email = req.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.Email == email, ct))
                return TypedResults.Conflict(new MessageResponse("Det finnes allerede en bruker med denne e-postadressen."));

            var user = new User { Email = email, DisplayName = req.DisplayName.Trim() };
            user.PasswordHash = Hasher.HashPassword(user, req.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(CreateResponse(user, tokens));
        });

        group.MapPost("/login", async Task<Results<Ok<AuthResponse>, JsonHttpResult<MessageResponse>>> (
            LoginRequest req, AppDbContext db, TokenService tokens, LoginThrottle throttle, CancellationToken ct) =>
        {
            var email = req.Email?.Trim().ToLowerInvariant() ?? "";
            var password = req.Password ?? "";
            if (throttle.IsLockedOut(email))
                return TypedResults.Json(new MessageResponse("For mange mislykkede forsøk. Prøv igjen om en stund."),
                    statusCode: StatusCodes.Status429TooManyRequests);

            var user = email.Length <= 256 ? await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct) : null;
            var valid = false;
            if (password.Length <= 128)
            {
                if (user is null)
                    Hasher.VerifyHashedPassword(DummyUser, DummyHash, password);
                else
                    valid = Hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
            }
            if (!valid || user is null)
            {
                throttle.RecordFailure(email);
                return TypedResults.Json(new MessageResponse("Feil e-post eller passord."), statusCode: StatusCodes.Status401Unauthorized);
            }

            throttle.Reset(email);
            return TypedResults.Ok(CreateResponse(user, tokens));
        });

        group.MapGet("/me", async Task<Results<Ok<UserDto>, UnauthorizedHttpResult>> (
            ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var user = principal.GetUserId() is int id ? await db.Users.FindAsync([id], ct) : null;
            return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(ToDto(user));
        }).RequireAuthorization();
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, u.IsAdmin);

    private static AuthResponse CreateResponse(User user, TokenService tokens) => new(tokens.CreateToken(user), ToDto(user));
}
