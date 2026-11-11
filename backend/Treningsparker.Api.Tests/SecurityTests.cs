using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Extensions.DependencyInjection;
using Treningsparker.Api.Data;
using Treningsparker.Api.Endpoints;

namespace Treningsparker.Api.Tests;

public class SecurityTests
{
    private static async Task<(HttpClient Client, AuthResponse Auth)> RegisterAsync(ApiFactory factory)
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"user{Guid.NewGuid():N}@example.com", "Passord123", "Testbruker"), TestContext.Current.CancellationToken);
        res.EnsureSuccessStatusCode();
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth);
    }

    private static CreateParkRequest Park(string name = "Testpark") =>
        new(name, "Test", "Sandnes", "Lura", 58.86, 5.74, "Tuftepark", ["Pull-up-stang"], true);

    [Theory]
    [InlineData("CHANGE_ME_development_only_key_at_least_32_chars", "Strong-password-123")]
    [InlineData("a-proper-secret-key-that-is-long-enough-123", "Admin123!")]
    [InlineData("too-short", "Strong-password-123")]
    [InlineData(null, "Strong-password-123")]
    public async Task Refuses_to_start_with_public_or_missing_secrets(string? jwtKey, string adminPassword)
    {
        await using var factory = new ApiFactory();
        factory.Settings["Jwt:Key"] = jwtKey;
        factory.Settings["Admin:Password"] = adminPassword;
        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Refusing to start", ex.ToString());
    }

    [Theory]
    [InlineData("Jwt:Issuer")]
    [InlineData("Jwt:Audience")]
    public async Task Refuses_to_start_without_token_issuer_or_audience(string key)
    {
        await using var factory = new ApiFactory();
        factory.Settings[key] = "";
        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Refusing to start", ex.ToString());
    }

    [Fact]
    public async Task Locks_out_an_account_after_repeated_wrong_passwords()
    {
        await using var factory = new ApiFactory();
        factory.Settings["RateLimiting:LoginFailuresPerAccount"] = "3";
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var wrong = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(ApiFactory.AdminEmail, "wrong"), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        // Even the right password is refused while the account is locked.
        var right = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(ApiFactory.AdminEmail, ApiFactory.AdminPassword), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, right.StatusCode);
    }

    [Fact]
    public async Task Limits_requests_to_auth_endpoints()
    {
        await using var factory = new ApiFactory();
        factory.Settings["RateLimiting:AuthRequestsPerMinute"] = "2";
        var client = factory.CreateClient();
        var login = new LoginRequest("nobody@example.com", "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", login, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", login, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", login, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Limits_park_suggestions_per_user()
    {
        await using var factory = new ApiFactory();
        factory.Settings["RateLimiting:SubmissionsPerHour"] = "2";
        var (client, _) = await RegisterAsync(factory);

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/parks", Park("1"), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/parks", Park("2"), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/parks", Park("3"), TestContext.Current.CancellationToken)).StatusCode);

        // Another user has their own allowance.
        var (other, _) = await RegisterAsync(factory);
        Assert.Equal(HttpStatusCode.Created, (await other.PostAsJsonAsync("/api/parks", Park("4"), TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Tokens_expire_after_the_configured_lifetime()
    {
        await using var factory = new ApiFactory();
        var (_, auth) = await RegisterAsync(factory);
        var expires = new JsonWebTokenHandler().ReadJsonWebToken(auth.Token).ValidTo;
        Assert.InRange(expires, DateTime.UtcNow.AddHours(11), DateTime.UtcNow.AddHours(12.1));
    }

    [Fact]
    public async Task Revoked_admin_rights_take_effect_immediately()
    {
        await using var factory = new ApiFactory();
        var (client, auth) = await RegisterAsync(factory);

        async Task SetAdmin(bool isAdmin)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == auth.User.Id, TestContext.Current.CancellationToken);
            user.IsAdmin = isAdmin;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await SetAdmin(true);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(auth.User.Email, "Passord123"), TestContext.Current.CancellationToken);
        var adminToken = (await login.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/parks", TestContext.Current.CancellationToken)).StatusCode);

        await SetAdmin(false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/parks", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Rejects_tokens_for_deleted_users()
    {
        await using var factory = new ApiFactory();
        var (client, auth) = await RegisterAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.Where(u => u.Id == auth.User.Id).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/parks/mine", TestContext.Current.CancellationToken)).StatusCode);
    }

    public static TheoryData<CreateParkRequest> InvalidParks => new()
    {
        Park() with { Area = new string('a', 101) },
        Park() with { Description = new string('a', 2001) },
        Park() with { Name = new string('a', 151) },
        Park() with { Equipment = Enumerable.Range(0, 21).Select(i => $"Apparat {i}").ToArray() },
        Park() with { Equipment = [new string('a', 51)] },
    };

    [Theory]
    [MemberData(nameof(InvalidParks))]
    public async Task Rejects_overlong_park_input_with_400(CreateParkRequest park)
    {
        await using var factory = new ApiFactory();
        var (client, _) = await RegisterAsync(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/parks", park, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Validation_errors_are_returned_in_norwegian()
    {
        await using var factory = new ApiFactory();
        var (client, _) = await RegisterAsync(factory);
        var res = await client.PostAsJsonAsync("/api/parks", Park() with { Latitude = 59.91, Longitude = 10.75 }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("Plasseringen må være i Stavanger/Sandnes-området.", await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var register = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest("ikke-en-epost", "Passord123", "Navn"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
        Assert.Contains("Ugyldig e-postadresse.", await register.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rejects_overlong_registration_input_with_400()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var tooLongName = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("a@b.no", "Passord123", new string('a', 101)), TestContext.Current.CancellationToken);
        var tooLongPassword = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("a@b.no", new string('a', 129), "Navn"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, tooLongName.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLongPassword.StatusCode);
    }
}
