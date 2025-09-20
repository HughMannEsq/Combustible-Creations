using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Models;
using AutumnRidgeUSA.Models.Storage;

namespace AutumnRidgeUSA.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<StorageContract> StorageContracts { get; set; } = null!;
        public DbSet<TempSignup> TempSignups { get; set; } = null!;
        public DbSet<Division> Divisions { get; set; } = null!;
        public DbSet<UserDivision> UserDivisions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure User entity - COMPLETE security field definitions
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Email).HasMaxLength(255);
                entity.Property(e => e.PasswordHash).HasMaxLength(255);
                entity.Property(e => e.ConfirmationToken).HasMaxLength(255);

                // ALL SECURITY FIELDS - properly defined to match your migration
                entity.Property(e => e.Salt).HasMaxLength(255);
                entity.Property(e => e.CurrentSessionToken).HasMaxLength(255);
                entity.Property(e => e.SessionExpiresAt);
                entity.Property(e => e.LastLoginAt);
                entity.Property(e => e.LastLoginIP).HasMaxLength(45);

                // Add indexes for performance on session lookups
                entity.HasIndex(e => e.CurrentSessionToken);
                entity.HasIndex(e => e.SessionExpiresAt);
            });

            // Configure TempSignup entity
            modelBuilder.Entity<TempSignup>(entity =>
            {
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.VerificationToken).IsUnique();
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.Property(e => e.Email).HasMaxLength(255);
                entity.Property(e => e.VerificationToken).HasMaxLength(255);
                entity.Property(e => e.FirstName).HasMaxLength(50);
                entity.Property(e => e.LastName).HasMaxLength(50);
                entity.Property(e => e.UserId).HasMaxLength(20);
            });

            // Configure StorageContract entity - UPDATED to allow multiple contracts per user
            modelBuilder.Entity<StorageContract>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Change to one-to-many relationship (one user can have many storage contracts)
                entity.HasOne(sc => sc.User)
                    .WithMany() // User can have multiple storage contracts
                    .HasForeignKey(sc => sc.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Division configurations
            modelBuilder.Entity<UserDivision>()
                .HasOne(ud => ud.User)
                .WithMany(u => u.UserDivisions)
                .HasForeignKey(ud => ud.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserDivision>()
                .HasOne(ud => ud.Division)
                .WithMany(d => d.UserDivisions)
                .HasForeignKey(ud => ud.DivisionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Prevent duplicate user-division pairs
            modelBuilder.Entity<UserDivision>()
                .HasIndex(ud => new { ud.UserId, ud.DivisionId })
                .IsUnique();

            // Seed initial divisions
            modelBuilder.Entity<Division>().HasData(
                new Division { Id = 1, Name = "Storage", Description = "Storage facility services", IsActive = true },
                new Division { Id = 2, Name = "Contracting", Description = "Construction and renovation services", IsActive = true },
                new Division { Id = 3, Name = "Real Estate", Description = "Property management and sales", IsActive = true }
            );

            // Add schema version tracking to prevent future issues
            modelBuilder.Entity<SchemaVersion>(entity =>
            {
                entity.HasKey(e => e.Version);
                entity.Property(e => e.Version).HasMaxLength(50);
                entity.Property(e => e.AppliedAt);
                entity.Property(e => e.Description).HasMaxLength(500);
            });

            // Seed current schema version
            modelBuilder.Entity<SchemaVersion>().HasData(
                new SchemaVersion
                {
                    Version = "1.2-security",
                    AppliedAt = DateTime.UtcNow,
                    Description = "Added security columns: Salt, CurrentSessionToken, SessionExpiresAt, LastLoginAt, LastLoginIP"
                }
            );

            base.OnModelCreating(modelBuilder);
        }
    }

    // Add this schema version tracking entity
    public class SchemaVersion
    {
        public string Version { get; set; } = string.Empty;
        public DateTime AppliedAt { get; set; }
        public string? Description { get; set; }
    }
}