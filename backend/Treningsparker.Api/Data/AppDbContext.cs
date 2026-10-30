using Microsoft.EntityFrameworkCore;

namespace Treningsparker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Park> Parks => Set<Park>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256);
            e.Property(u => u.DisplayName).HasMaxLength(100);
        });

        b.Entity<Park>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(150);
            e.Property(p => p.Description).HasMaxLength(2000);
            e.Property(p => p.City).HasMaxLength(50);
            e.Property(p => p.Area).HasMaxLength(100);
            e.Property(p => p.ParkType).HasMaxLength(50);
            e.Property(p => p.Equipment).HasMaxLength(1000);
            e.Property(p => p.RejectionReason).HasMaxLength(500);
            e.HasIndex(p => p.Status);
            e.HasOne(p => p.CreatedBy).WithMany().HasForeignKey(p => p.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
