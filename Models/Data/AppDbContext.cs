using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Models;
using AutumnRidgeUSA.Models.Shared;
using AutumnRidgeUSA.Models.Storage;

namespace AutumnRidgeUSA.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Client> Clients { get; set; } = null!;
        public DbSet<StorageContract> StorageContracts { get; set; } = null!;
        public DbSet<TempSignup> TempSignups { get; set; } = null!;

        public DbSet<Division> Divisions { get; set; }
        public DbSet<UserDivision> UserDivisions { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Email).HasMaxLength(255);
                entity.Property(e => e.PasswordHash).HasMaxLength(255);
                entity.Property(e => e.ConfirmationToken).HasMaxLength(255);
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

            base.OnModelCreating(modelBuilder);
        }
    }
}