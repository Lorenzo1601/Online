using Microsoft.EntityFrameworkCore;
using Online.Models;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Online.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Macchina> Macchine { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Configura la chiave primaria composita per Macchina
            modelBuilder.Entity<Macchina>()
                .HasKey(m => new { m.NomeMacchina, m.IP_Address });
        }
    }
}
