using System.ComponentModel.DataAnnotations;
using Treningsparker.Api.Data;

namespace Treningsparker.Api.Endpoints;

// Request types are validated by ASP.NET Core's built-in minimal API validation (AddValidation in Program.cs);
// invalid requests get a 400 with these messages before the handler runs.

public record RegisterRequest(
    [Required(ErrorMessage = "Ugyldig e-postadresse.")]
    [EmailAddress(ErrorMessage = "Ugyldig e-postadresse.")]
    [StringLength(256, ErrorMessage = "Ugyldig e-postadresse.")]
    string Email,
    [Required(ErrorMessage = "Passordet må være mellom 8 og 128 tegn.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Passordet må være mellom 8 og 128 tegn.")]
    string Password,
    [Required(ErrorMessage = "Navn er påkrevd (maks 100 tegn).")]
    [StringLength(100, ErrorMessage = "Navn er påkrevd (maks 100 tegn).")]
    string DisplayName);

// Not validated on purpose: a failed login must look the same whatever was wrong.
public record LoginRequest(string? Email, string? Password);

public record UserDto(int Id, string Email, string DisplayName, bool IsAdmin);
public record AuthResponse(string Token, UserDto User);
public record MessageResponse(string Message);

public record CreateParkRequest(
    [Required(ErrorMessage = "Navn er påkrevd (maks 150 tegn).")]
    [StringLength(150, ErrorMessage = "Navn er påkrevd (maks 150 tegn).")]
    string Name,
    [StringLength(2000, ErrorMessage = "Beskrivelsen kan være maks 2000 tegn.")]
    string? Description,
    [Required(ErrorMessage = "By må være Stavanger eller Sandnes.")]
    string City,
    [StringLength(100, ErrorMessage = "Område kan være maks 100 tegn.")]
    string? Area,
    double Latitude,
    double Longitude,
    string? ParkType,
    string[]? Equipment,
    bool HasLighting) : IValidatableObject
{
    public static readonly string[] Cities = ["Stavanger", "Sandnes"];
    public static readonly string[] ParkTypes = ["Tuftepark", "Calisthenicspark", "Treningspark", "Hinderløype"];

    // Equipment is stored semicolon separated, so semicolons inside an item are dropped.
    public IReadOnlyList<string> CleanEquipment =>
        [.. (Equipment ?? []).Select(e => e.Replace(";", "").Trim()).Where(e => e.Length > 0).Distinct()];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Cities.Contains(City))
            yield return new("By må være Stavanger eller Sandnes.", [nameof(City)]);
        if (ParkType is not null && !ParkTypes.Contains(ParkType))
            yield return new("Ukjent parktype.", [nameof(ParkType)]);
        // Rough bounding box around Stavanger and Sandnes.
        if (Latitude is < 58.70 or > 59.10 || Longitude is < 5.45 or > 6.10)
            yield return new("Plasseringen må være i Stavanger/Sandnes-området.", [nameof(Latitude), nameof(Longitude)]);
        var equipment = CleanEquipment;
        if (equipment.Count > 20 || equipment.Any(e => e.Length > 50) || string.Join(';', equipment).Length > 1000)
            yield return new("Maks 20 apparater, hver på maks 50 tegn.", [nameof(Equipment)]);
    }
}

public record RejectRequest(
    [StringLength(500, ErrorMessage = "Begrunnelsen kan være maks 500 tegn.")]
    string? Reason);

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
