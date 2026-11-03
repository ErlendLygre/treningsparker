using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Treningsparker.Api.Data;
using Treningsparker.Api.Security;

namespace Treningsparker.Api.Endpoints;

public static class ParkEndpoints
{
    public static readonly string[] Cities = ["Stavanger", "Sandnes"];
    public static readonly string[] ParkTypes = ["Tuftepark", "Calisthenicspark", "Treningspark", "Hinderløype"];

    public static void MapParkEndpoints(this IEndpointRouteBuilder app)
    {
        var parks = app.MapGroup("/api/parks");

        // Public: only approved parks are visible on the map.
        parks.MapGet("/", async (AppDbContext db, string? city) =>
        {
            var query = db.Parks.Include(p => p.CreatedBy).Where(p => p.Status == ParkStatus.Approved);
            if (!string.IsNullOrWhiteSpace(city)) query = query.Where(p => p.City == city);
            var list = await query.OrderBy(p => p.City).ThenBy(p => p.Name).ToListAsync();
            return list.Select(ParkDto.From);
        });

        parks.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var park = await db.Parks.Include(p => p.CreatedBy)
                .FirstOrDefaultAsync(p => p.Id == id && p.Status == ParkStatus.Approved);
            return park is null ? Results.NotFound() : Results.Ok(ParkDto.From(park));
        });

        parks.MapGet("/mine", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var list = await db.Parks.Include(p => p.CreatedBy)
                .Where(p => p.CreatedByUserId == userId)
                .OrderByDescending(p => p.CreatedAt).ToListAsync();
            return list.Select(ParkDto.From);
        }).RequireAuthorization();

        // Logged in users can suggest new parks. They stay pending until an admin approves them.
        parks.MapPost("/", async (CreateParkRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            var errors = Validate(req);
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var park = new Park
            {
                Name = req.Name.Trim(),
                Description = req.Description?.Trim() ?? "",
                City = req.City,
                Area = req.Area?.Trim() ?? "",
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                ParkType = string.IsNullOrWhiteSpace(req.ParkType) ? "Treningspark" : req.ParkType,
                Equipment = string.Join(';', CleanEquipment(req.Equipment)),
                HasLighting = req.HasLighting,
                Status = ParkStatus.Pending,
                CreatedByUserId = user.GetUserId(),
            };
            db.Parks.Add(park);
            await db.SaveChangesAsync();
            await db.Entry(park).Reference(p => p.CreatedBy).LoadAsync();
            return Results.Created($"/api/parks/{park.Id}", ParkDto.From(park));
        }).RequireAuthorization().RequireRateLimiting(RateLimiting.Submissions);

        var admin = app.MapGroup("/api/admin/parks").RequireAuthorization("Admin");

        admin.MapGet("/", async (AppDbContext db, string? status) =>
        {
            var query = db.Parks.Include(p => p.CreatedBy).AsQueryable();
            if (Enum.TryParse<ParkStatus>(status, true, out var s)) query = query.Where(p => p.Status == s);
            var list = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
            return list.Select(ParkDto.From);
        });

        admin.MapPost("/{id:int}/approve", async (int id, AppDbContext db) =>
        {
            var park = await db.Parks.Include(p => p.CreatedBy).FirstOrDefaultAsync(p => p.Id == id);
            if (park is null) return Results.NotFound();
            park.Status = ParkStatus.Approved;
            park.RejectionReason = null;
            park.ReviewedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ParkDto.From(park));
        });

        admin.MapPost("/{id:int}/reject", async (int id, RejectRequest req, AppDbContext db) =>
        {
            if (req.Reason?.Trim().Length > 500)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["Begrunnelsen kan være maks 500 tegn."] });
            var park = await db.Parks.Include(p => p.CreatedBy).FirstOrDefaultAsync(p => p.Id == id);
            if (park is null) return Results.NotFound();
            park.Status = ParkStatus.Rejected;
            park.RejectionReason = req.Reason?.Trim();
            park.ReviewedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ParkDto.From(park));
        });

        admin.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var park = await db.Parks.FindAsync(id);
            if (park is null) return Results.NotFound();
            db.Parks.Remove(park);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static Dictionary<string, string[]> Validate(CreateParkRequest req)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Trim().Length > 150) errors["name"] = ["Navn er påkrevd (maks 150 tegn)."];
        if (!Cities.Contains(req.City)) errors["city"] = ["By må være Stavanger eller Sandnes."];
        if (req.ParkType is not null && !ParkTypes.Contains(req.ParkType)) errors["parkType"] = ["Ukjent parktype."];
        // Rough bounding box around Stavanger and Sandnes.
        if (req.Latitude is < 58.70 or > 59.10 || req.Longitude is < 5.45 or > 6.10)
            errors["location"] = ["Plasseringen må være i Stavanger/Sandnes-området."];
        if (req.Description?.Trim().Length > 2000) errors["description"] = ["Beskrivelsen kan være maks 2000 tegn."];
        if (req.Area?.Trim().Length > 100) errors["area"] = ["Område kan være maks 100 tegn."];
        var equipment = CleanEquipment(req.Equipment);
        if (equipment.Count > 20 || equipment.Any(e => e.Length > 50) || string.Join(';', equipment).Length > 1000)
            errors["equipment"] = ["Maks 20 apparater, hver på maks 50 tegn."];
        return errors;
    }

    // Equipment is stored semicolon separated, so semicolons inside an item are dropped.
    private static List<string> CleanEquipment(string[]? equipment) =>
        (equipment ?? []).Select(e => e.Replace(";", "").Trim()).Where(e => e.Length > 0).Distinct().ToList();
}
