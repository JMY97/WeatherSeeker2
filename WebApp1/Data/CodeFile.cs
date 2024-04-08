using WebApp1.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
namespace WebApp1.Data
{
    public class DBContext : DbContext
    {
        public DbSet<Users>
        Users { get; set; }
    public DBContext(DbContextOptions<DBContext> options) : base(options) { }
        public DbSet<WebApp1.Models.Admins> Admins { get; set; } = default!;
        public DbSet<WebApp1.Models.Clients> Clients { get; set; }= default!;

        public DbSet<WebApp1.Models.LoginViewModel> Login { get; set; } = default!;
    }
}