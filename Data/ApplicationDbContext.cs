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

        public DbSet<MacchinaOpcUaLog> MacchineOpcUaLog { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Configura la chiave primaria composita per Macchina
            modelBuilder.Entity<Macchina>()
                .HasKey(m => new { m.NomeMacchina, m.IP_Address });

            // --- MODIFICA PER LA NUOVA CHIAVE COMPOSITA ---
            modelBuilder.Entity<MacchinaOpcUaLog>()
                .HasKey(l => new { l.Id, l.Nome }); // Definizione della chiave composita

            modelBuilder.Entity<MacchinaOpcUaLog>()
                .Property(l => l.Id)
                .ValueGeneratedOnAdd(); // Imposta l'Id come auto-incrementale
            // --- FINE MODIFICA ---
        }
    }
}
