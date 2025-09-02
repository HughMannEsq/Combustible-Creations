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
        public DbSet<StorageClient> StorageClients { get; set; } = null!;
        public DbSet<TempSignup> TempSignups { get; set; } = null!;

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

            base.OnModelCreating(modelBuilder);
        }
    }
}