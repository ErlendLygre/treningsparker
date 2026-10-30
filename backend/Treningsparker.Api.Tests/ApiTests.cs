using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Treningsparker.Api.Endpoints;

namespace Treningsparker.Api.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"treningsparker-test-{Guid.NewGuid()}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("DatabaseProvider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}

public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        res.EnsureSuccessStatusCode();
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private async Task<HttpClient> RegisterAsync()
    {
        var client = factory.CreateClient();
        var email = $"user{Guid.NewGuid():N}@example.com";
        var res = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Passord123", "Testbruker"));
        res.EnsureSuccessStatusCode();
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private static CreateParkRequest NewPark(string name) =>
        new(name, "Test", "Sandnes", "Lura", 58.86, 5.74, "Tuftepark", ["Pull-up-stang"], true);

    [Fact]
    public async Task Seeds_thirty_approved_parks_in_both_cities()
    {
        var parks = await factory.CreateClient().GetFromJsonAsync<ParkDto[]>("/api/parks");
        Assert.True(parks!.Length >= 30);
        Assert.Contains(parks, p => p.City == "Stavanger");
        Assert.Contains(parks, p => p.City == "Sandnes");
        Assert.All(parks, p => Assert.Equal("Approved", p.Status));
    }

    [Fact]
    public async Task Anonymous_users_cannot_submit_parks()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/parks", NewPark("Anonym park"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Submitted_park_is_hidden_until_admin_approves_it()
    {
        var user = await RegisterAsync();
        var created = await (await user.PostAsJsonAsync("/api/parks", NewPark("Ny testpark"))).Content.ReadFromJsonAsync<ParkDto>();
        Assert.Equal("Pending", created!.Status);

        var publicParks = await factory.CreateClient().GetFromJsonAsync<ParkDto[]>("/api/parks");
        Assert.DoesNotContain(publicParks!, p => p.Id == created.Id);

        // Regular users are not admins.
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsync($"/api/admin/parks/{created.Id}/approve", null)).StatusCode);

        var admin = await LoginAsync("admin@treningsparker.no", "Admin123!");
        var pending = await admin.GetFromJsonAsync<ParkDto[]>("/api/admin/parks?status=pending");
        Assert.Contains(pending!, p => p.Id == created.Id);

        (await admin.PostAsync($"/api/admin/parks/{created.Id}/approve", null)).EnsureSuccessStatusCode();

        publicParks = await factory.CreateClient().GetFromJsonAsync<ParkDto[]>("/api/parks");
        Assert.Contains(publicParks!, p => p.Id == created.Id && p.SubmittedBy == "Testbruker");
    }

    [Fact]
    public async Task Rejected_park_is_shown_with_reason_in_my_suggestions()
    {
        var user = await RegisterAsync();
        var created = await (await user.PostAsJsonAsync("/api/parks", NewPark("Avvist park"))).Content.ReadFromJsonAsync<ParkDto>();

        var admin = await LoginAsync("admin@treningsparker.no", "Admin123!");
        (await admin.PostAsJsonAsync($"/api/admin/parks/{created!.Id}/reject", new RejectRequest("Duplikat"))).EnsureSuccessStatusCode();

        var mine = await user.GetFromJsonAsync<ParkDto[]>("/api/parks/mine");
        var park = Assert.Single(mine!);
        Assert.Equal("Rejected", park.Status);
        Assert.Equal("Duplikat", park.RejectionReason);
    }

    [Fact]
    public async Task Rejects_parks_outside_stavanger_and_sandnes()
    {
        var user = await RegisterAsync();
        var res = await user.PostAsJsonAsync("/api/parks", NewPark("Oslo") with { Latitude = 59.91, Longitude = 10.75 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_unauthorized()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@treningsparker.no", "feil"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
