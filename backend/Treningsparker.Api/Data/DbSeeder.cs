using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Treningsparker.Api.Data;

public static class DbSeeder
{
    private record SeedPark(
        string Name, string City, string Area, double Latitude, double Longitude,
        string ParkType, string[] Equipment, bool HasLighting, string Description);

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The database can take a while to accept connections after a cold start, so retry.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.EnsureCreatedAsync();
                break;
            }
            catch (Exception ex) when (attempt < 20)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        var adminEmail = config["Admin:Email"] ?? "admin@treningsparker.no";
        var adminPassword = config["Admin:Password"] ?? "Admin123!";
        if (!await db.Users.AnyAsync(u => u.Email == adminEmail))
        {
            var admin = new User { Email = adminEmail, DisplayName = "Administrator", IsAdmin = true };
            admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, adminPassword);
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded admin user {Email}", adminEmail);
        }

        if (!await db.Parks.AnyAsync())
        {
            var path = config["SeedFile"] ?? Path.Combine(AppContext.BaseDirectory, "parks.seed.json");
            if (!File.Exists(path))
            {
                logger.LogWarning("Seed file {Path} not found, skipping park seed", path);
                return;
            }

            var parks = JsonSerializer.Deserialize<SeedPark[]>(
                await File.ReadAllTextAsync(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

            db.Parks.AddRange(parks.Select(p => new Park
            {
                Name = p.Name,
                City = p.City,
                Area = p.Area,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                ParkType = p.ParkType,
                Equipment = string.Join(';', p.Equipment),
                HasLighting = p.HasLighting,
                Description = p.Description,
                Status = ParkStatus.Approved,
                ReviewedAt = DateTime.UtcNow,
            }));
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} parks", parks.Length);
        }
    }
}
