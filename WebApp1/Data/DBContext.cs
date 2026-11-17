using Microsoft.EntityFrameworkCore;
using WebApp1.Models;

namespace WebApp1.Data
{
    public class DBContext : DbContext
    {
        public DBContext(DbContextOptions<DBContext> options) : base(options) { }

        public DbSet<Account> Accounts { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Account>(entity =>
            {
                entity.ToTable("Accounts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
                entity.Property(e => e.PasswordHash).HasMaxLength(512).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.SessionVersion).IsRequired();
                entity.HasIndex(e => e.Username).IsUnique();
            });
        }
    }
}
