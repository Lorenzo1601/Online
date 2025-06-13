using Microsoft.EntityFrameworkCore;
using Online;
using Online.Data;
using Online.Models;

var builder = WebApplication.CreateBuilder(args);

// Aggiunge esplicitamente i file di configurazione e abilita il ricaricamento automatico
// quando vengono modificati. Questo è cruciale per il corretto funzionamento.
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Registra le impostazioni per renderle disponibili nell'applicazione
builder.Services.Configure<CleanupSettings>(
    builder.Configuration.GetSection("CleanupSettings"));

// Aggiunge i servizi al container.
builder.Services.AddControllersWithViews();

// Connessione al DB mantenuta come da tua richiesta.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySQL(builder.Configuration.GetConnectionString("DefaultConnection")));


// --- CORREZIONE ---
// Registra OpcUaService come Singleton. Viene creato una sola volta e condiviso.
builder.Services.AddSingleton<OpcUaService>();

// La riga che causava l'errore è stata rimossa, perché OpcUaService non è un IHostedService.
// Il DataCleanUpService, invece, lo è e viene registrato correttamente qui sotto.

// Registra il servizio di pulizia dati in background.
builder.Services.AddHostedService<DataCleanUpService>();


var app = builder.Build();

// Configura la pipeline delle richieste HTTP.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
