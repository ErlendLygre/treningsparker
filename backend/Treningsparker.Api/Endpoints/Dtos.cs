using Treningsparker.Api.Data;

namespace Treningsparker.Api.Endpoints;

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);
public record UserDto(int Id, string Email, string DisplayName, bool IsAdmin);
public record AuthResponse(string Token, UserDto User);

public record CreateParkRequest(
    string Name,
    string? Description,
    string City,
    string? Area,
    double Latitude,
    double Longitude,
    string? ParkType,
    string[]? Equipment,
    bool HasLighting);

public record RejectRequest(string? Reason);

public record ParkDto(
    int Id,
    string Name,
    string Description,
    string City,
    string Area,
    double Latitude,
    double Longitude,
    string ParkType,
    string[] Equipment,
    bool HasLighting,
    string Status,
    string? RejectionReason,
    string? SubmittedBy,
    DateTime CreatedAt,
    DateTime? ReviewedAt)
{
    public static ParkDto From(Park p) => new(
        p.Id, p.Name, p.Description, p.City, p.Area, p.Latitude, p.Longitude, p.ParkType,
        p.Equipment.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        p.HasLighting, p.Status.ToString(), p.RejectionReason, p.CreatedBy?.DisplayName, p.CreatedAt, p.ReviewedAt);
}
