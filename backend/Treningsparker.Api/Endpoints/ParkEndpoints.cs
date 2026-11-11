using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Treningsparker.Api.Data;
using Treningsparker.Api.Security;

namespace Treningsparker.Api.Endpoints;

public static class ParkEndpoints
{
    public static void MapParkEndpoints(this IEndpointRouteBuilder app)
    {
        var parks = app.MapGroup("/api/parks");

        // Public: only approved parks are visible on the map.
        parks.MapGet("/", async (AppDbContext db, string? city, CancellationToken ct) =>
        {
            var query = db.Parks.AsNoTracking().Include(p => p.CreatedBy).Where(p => p.Status == ParkStatus.Approved);
            if (!string.IsNullOrWhiteSpace(city)) query = query.Where(p => p.City == city);
            var list = await query.OrderBy(p => p.City).ThenBy(p => p.Name).ToListAsync(ct);
            return TypedResults.Ok(list.Select(ParkDto.From));
        });

        parks.MapGet("/{id:int}", async Task<Results<Ok<ParkDto>, NotFound>> (int id, AppDbContext db, CancellationToken ct) =>
        {
            var park = await db.Parks.AsNoTracking().Include(p => p.CreatedBy)
                .FirstOrDefaultAsync(p => p.Id == id && p.Status == ParkStatus.Approved, ct);
            return park is null ? TypedResults.NotFound() : TypedResults.Ok(ParkDto.From(park));
        });

        parks.MapGet("/mine", async Task<Results<Ok<IEnumerable<ParkDto>>, UnauthorizedHttpResult>> (
            ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            if (user.GetUserId() is not int userId) return TypedResults.Unauthorized();
            var list = await db.Parks.AsNoTracking().Include(p => p.CreatedBy)
                .Where(p => p.CreatedByUserId == userId)
                .OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
            return TypedResults.Ok(list.Select(ParkDto.From));
        }).RequireAuthorization();

        // Logged in users can suggest new parks. They stay pending until an admin approves them.
        parks.MapPost("/", async Task<Results<Created<ParkDto>, UnauthorizedHttpResult>> (
            CreateParkRequest req, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            if (user.GetUserId() is not int userId) return TypedResults.Unauthorized();
            var park = new Park
            {
                Name = req.Name.Trim(),
                Description = req.Description?.Trim() ?? "",
                City = req.City,
                Area = req.Area?.Trim() ?? "",
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                ParkType = string.IsNullOrWhiteSpace(req.ParkType) ? "Treningspark" : req.ParkType,
                Equipment = string.Join(';', req.CleanEquipment),
                HasLighting = req.HasLighting,
                Status = ParkStatus.Pending,
                CreatedByUserId = userId,
            };
            db.Parks.Add(park);
            await db.SaveChangesAsync(ct);
            await db.Entry(park).Reference(p => p.CreatedBy).LoadAsync(ct);
            return TypedResults.Created($"/api/parks/{park.Id}", ParkDto.From(park));
        }).RequireAuthorization().RequireRateLimiting(RateLimiting.Submissions);

        var admin = app.MapGroup("/api/admin/parks").RequireAuthorization("Admin");

        admin.MapGet("/", async (AppDbContext db, string? status, CancellationToken ct) =>
        {
            var query = db.Parks.AsNoTracking().Include(p => p.CreatedBy).AsQueryable();
            if (Enum.TryParse<ParkStatus>(status, true, out var s)) query = query.Where(p => p.Status == s);
            var list = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
            return TypedResults.Ok(list.Select(ParkDto.From));
        });

        admin.MapPost("/{id:int}/approve", (int id, AppDbContext db, TimeProvider time, CancellationToken ct) =>
            Review(db, id, ParkStatus.Approved, null, time, ct));

        admin.MapPost("/{id:int}/reject", (int id, RejectRequest req, AppDbContext db, TimeProvider time, CancellationToken ct) =>
            Review(db, id, ParkStatus.Rejected, req.Reason?.Trim(), time, ct));

        admin.MapDelete("/{id:int}", async Task<Results<NoContent, NotFound>> (int id, AppDbContext db, CancellationToken ct) =>
        {
            var deleted = await db.Parks.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
            return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
        });
    }

    private static async Task<Results<Ok<ParkDto>, NotFound>> Review(
        AppDbContext db, int id, ParkStatus status, string? reason, TimeProvider time, CancellationToken ct)
    {
        var park = await db.Parks.Include(p => p.CreatedBy).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (park is null) return TypedResults.NotFound();
        park.Status = status;
        park.RejectionReason = reason;
        park.ReviewedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ParkDto.From(park));
    }
}
