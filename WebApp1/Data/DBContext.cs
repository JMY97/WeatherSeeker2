using WebApp1.Models;
using Microsoft.EntityFrameworkCore;

namespace WebApp1.Data
{
    public class DBContext : DbContext
    {
        public DBContext(DbContextOptions<DBContext> options) : base(options) { }

        public DbSet<Users> Users { get; set; } = default!;
        public DbSet<Admins> Admins { get; set; } = default!;
        public DbSet<Clients> Clients { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Users>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.name).HasMaxLength(100).IsRequired();
            });

            modelBuilder.Entity<Admins>(entity =>
            {
                entity.ToTable("Admins");
                entity.HasKey(e => e.AdminId);
                entity.Property(e => e.AdminId).ValueGeneratedOnAdd();
                entity.Property(e => e.Id).IsRequired();
                entity.Property(e => e.username).HasMaxLength(100).IsRequired();
                entity.Property(e => e.password).HasMaxLength(512).IsRequired();
                entity.HasIndex(e => e.username).IsUnique();
            });

            modelBuilder.Entity<Clients>(entity =>
            {
                entity.ToTable("Clients");
                entity.HasKey(e => e.ClientId);
                entity.Property(e => e.ClientId).ValueGeneratedOnAdd();
                entity.Property(e => e.Id).IsRequired();
                entity.Property(e => e.username).HasMaxLength(100).IsRequired();
                entity.Property(e => e.password).HasMaxLength(512).IsRequired();
                entity.HasIndex(e => e.username).IsUnique();
            });
        }
    }
}