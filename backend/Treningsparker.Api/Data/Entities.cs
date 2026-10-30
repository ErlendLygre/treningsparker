namespace Treningsparker.Api.Data;

public enum ParkStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

public class User
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public string PasswordHash { get; set; } = "";
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Park
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public required string City { get; set; }
    public string Area { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string ParkType { get; set; } = "Treningspark";
    /// <summary>Semicolon separated list of equipment.</summary>
    public string Equipment { get; set; } = "";
    public bool HasLighting { get; set; }
    public ParkStatus Status { get; set; } = ParkStatus.Pending;
    public string? RejectionReason { get; set; }
    public int? CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}
