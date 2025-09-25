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
        public DbSet<TempSignup> TempSignups { get; set; } = null!;
        public DbSet<Division> Divisions { get; set; } = null!;
        public DbSet<UserDivision> UserDivisions { get; set; } = null!;

        // NEW Storage Tables (replacing the old single StorageContract table)
        public DbSet<StorageUnit> StorageUnits { get; set; } = null!;
        public DbSet<StorageContract> StorageContracts { get; set; } = null!;
        public DbSet<StorageContractUser> StorageContractUsers { get; set; } = null!;

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

            // NEW Storage Configuration
            // Storage Unit configuration
            modelBuilder.Entity<StorageUnit>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UnitId).IsUnique();
                entity.Property(e => e.UnitId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.UnitSize).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Description).HasMaxLength(100);
            });

            // Storage Contract configuration
            modelBuilder.Entity<StorageContract>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ContractNumber).IsUnique();
                entity.Property(e => e.ContractNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.PaymentCycle).IsRequired().HasMaxLength(20);

                // Relationship with StorageUnit
                entity.HasOne(e => e.StorageUnit)
                      .WithMany(u => u.Contracts)
                      .HasForeignKey(e => e.StorageUnitId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Storage Contract User configuration
            modelBuilder.Entity<StorageContractUser>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Composite unique index to prevent duplicate user-contract associations
                entity.HasIndex(e => new { e.StorageContractId, e.UserId }).IsUnique();
                entity.Property(e => e.AccessLevel).HasMaxLength(20).HasDefaultValue("Full");

                // Relationship with StorageContract
                entity.HasOne(e => e.StorageContract)
                      .WithMany(c => c.ContractUsers)
                      .HasForeignKey(e => e.StorageContractId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relationship with User
                entity.HasOne(e => e.User)
                      .WithMany() // User doesn't need back navigation
                      .HasForeignKey(e => e.UserId)
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

            // Updated schema version for new storage structure
            modelBuilder.Entity<SchemaVersion>().HasData(
                new SchemaVersion
                {
                    Version = "1.3-storage-refactor",
                    AppliedAt = DateTime.UtcNow,
                    Description = "Refactored storage to use StorageUnit, StorageContract, and StorageContractUser tables"
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