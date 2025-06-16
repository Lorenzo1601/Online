using Microsoft.EntityFrameworkCore;
using online.Models;
using Online.Models;

namespace Online.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSet per la tua funzionalità di gestione macchine (usata in HomeController)
        public DbSet<Macchina> Macchine { get; set; }

        // DbSet per la nuova funzionalità OPC UA
        public DbSet<Connessione> Connessioni { get; set; }
        public DbSet<MacchinaOpcUaLog> MacchineOpcUaLog { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- CORREZIONE APPLICATA QUI ---
            // Definisce la chiave primaria composta per l'entità 'Macchina'.
            // Questo risolve l'eccezione InvalidOperationException.
            modelBuilder.Entity<Macchina>()
                .HasKey(m => new { m.NomeMacchina, m.IP_Address });
            // --- FINE CORREZIONE ---

            // Configurazione per la nuova tabella Connessioni
            modelBuilder.Entity<Connessione>()
                .HasKey(c => c.NomeMacchina);

            // Configurazione per la tabella dei log OPC UA
            modelBuilder.Entity<MacchinaOpcUaLog>()
                .HasKey(e => new { e.NomeMacchina, e.Nome, e.Timestamp });

            modelBuilder.Entity<MacchinaOpcUaLog>()
                .HasOne(log => log.Connessione)
                .WithMany()
                .HasForeignKey(log => log.NomeMacchina);
        }
    }
}
