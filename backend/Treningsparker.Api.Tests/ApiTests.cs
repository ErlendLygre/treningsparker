using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Treningsparker.Api.Data;
using Treningsparker.Api.Endpoints;

namespace Treningsparker.Api.Tests;

/// <summary>
/// Runs the API against a throwaway SQLite file, or against PostgreSQL when TEST_POSTGRES is set to a
/// connection string (a fresh database is created and dropped per test class).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@treningsparker.no";
    public const string AdminPassword = "Test-admin-password-1";

    private readonly string? _postgres = Environment.GetEnvironmentVariable("TEST_POSTGRES");
    private readonly string _dbName = $"treningsparker_test_{Guid.NewGuid():N}";

    /// <summary>Settings applied on top of the test defaults; change before the first request.</summary>
    public Dictionary<string, string?> Settings { get; } = new()
    {
        ["Jwt:Key"] = "test-signing-key-that-is-long-enough-1234567890",
        ["Admin:Password"] = AdminPassword,
        ["RateLimiting:AuthRequestsPerMinute"] = "10000",
        ["RateLimiting:SubmissionsPerHour"] = "10000",
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        foreach (var (key, value) in Settings) builder.UseSetting(key, value);
        if (_postgres is not null)
        {
            builder.UseSetting("DatabaseProvider", "Postgres");
            builder.UseSetting("ConnectionStrings:Default", $"{_postgres};Database={_dbName}");
        }
        else
        {
            builder.UseSetting("DatabaseProvider", "Sqlite");
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={SqlitePath}");
        }
    }

    private string SqlitePath => Path.Combine(Path.GetTempPath(), $"{_dbName}.db");

    private bool _started;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        _started = true;
        return host;
    }

    public override async ValueTask DisposeAsync()
    {
        if (_started)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }
        await base.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    }
}

public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), TestContext.Current.CancellationToken);
        res.EnsureSuccessStatusCode();
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private async Task<HttpClient> RegisterAsync()
    {
        var client = factory.CreateClient();
        var email = $"user{Guid.NewGuid():N}@example.com";
        var res = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Passord123", "Testbruker"), TestContext.Current.CancellationToken);
        res.EnsureSuccessStatusCode();
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private static CreateParkRequest NewPark(string name) =>
        new(name, "Test", "Sandnes", "Lura", 58.86, 5.74, "Tuftepark", ["Pull-up-stang"], true);

    [Fact]
    public async Task Seeds_thirty_approved_parks_in_both_cities()
    {
        var parks = await factory.CreateClient().GetFromJsonAsync<ParkDto[]>("/api/parks", TestContext.Current.CancellationToken);
        Assert.True(parks!.Length >= 30);
        Assert.Contains(parks, p => p.City == "Stavanger");
        Assert.Contains(parks, p => p.City == "Sandnes");
        Assert.All(parks, p => Assert.Equal("Approved", p.Status));
    }

    [Fact]
    public async Task Anonymous_users_cannot_submit_parks()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/parks", NewPark("Anonym park"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Submitted_park_is_hidden_until_admin_approves_it()
    {
        var user = await RegisterAsync();
        var created = await (await user.PostAsJsonAsync("/api/parks", NewPark("Ny testpark"), TestContext.Current.CancellationToken)).Content.ReadFromJsonAsync<ParkDto>(TestContext.Current.CancellationToken);
        Assert.Equal("Pending", created!.Status);

        var publicParks = await factory.CreateClient().GetFromJsonAsync<ParkDto[]>("/api/parks", TestContext.Current.CancellationToken);
        Assert.DoesNotContain(publicParks!, p => p.Id == created.Id);

        // Regular users are not admins.
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsync($"/api/admin/parks/{created.Id}/approve", null, TestContext.Current.CancellationToken)).StatusCode);

        var admin = await LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);
        var pending = await admin.GetFromJsonAsync<ParkDto[]>("/api/admin/parks?status=pending", TestContext.Current.CancellationToken);
        Assert.Contains(pending!, p => p.Id == created.Id);

        (await admin.PostAsync($"/api/admin/parks/{created.Id}/approve", null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        publicParks = await factory.CreateClient().GetFromJsonAsync<ParkDto[]>("/api/parks", TestContext.Current.CancellationToken);
        Assert.Contains(publicParks!, p => p.Id == created.Id && p.SubmittedBy == "Testbruker");
    }

    [Fact]
    public async Task Rejected_park_is_shown_with_reason_in_my_suggestions()
    {
        var user = await RegisterAsync();
        var created = await (await user.PostAsJsonAsync("/api/parks", NewPark("Avvist park"), TestContext.Current.CancellationToken)).Content.ReadFromJsonAsync<ParkDto>(TestContext.Current.CancellationToken);

        var admin = await LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);
        (await admin.PostAsJsonAsync($"/api/admin/parks/{created!.Id}/reject", new RejectRequest("Duplikat"), TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var mine = await user.GetFromJsonAsync<ParkDto[]>("/api/parks/mine", TestContext.Current.CancellationToken);
        var park = Assert.Single(mine!);
        Assert.Equal("Rejected", park.Status);
        Assert.Equal("Duplikat", park.RejectionReason);
    }

    [Fact]
    public async Task Rejects_parks_outside_stavanger_and_sandnes()
    {
        var user = await RegisterAsync();
        var res = await user.PostAsJsonAsync("/api/parks", NewPark("Oslo") with { Latitude = 59.91, Longitude = 10.75 }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_unauthorized()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(ApiFactory.AdminEmail, "feil"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
