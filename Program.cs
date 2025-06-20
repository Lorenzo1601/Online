using Microsoft.EntityFrameworkCore;
using Online;
using Online.Data;
using Online.Models;
using Online.Services;
using Rotativa.AspNetCore; // Assicurati che questo namespace sia corretto

var builder = WebApplication.CreateBuilder(args);

// Aggiunge esplicitamente i file di configurazione e abilita il ricaricamento automatico
// quando vengono modificati. Questo � cruciale per il corretto funzionamento.
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Registra le sezioni di configurazione come classi di opzioni, rendendole disponibili
// tramite dependency injection e aggiornabili in tempo reale.
builder.Services.Configure<CleanupSettings>(
    builder.Configuration.GetSection("CleanupSettings"));

builder.Services.Configure<OpcUaConnectionSettings>(
    builder.Configuration.GetSection("OpcUaConnectionSettings"));

// Aggiunge i servizi al container.
builder.Services.AddControllersWithViews();

// Connessione al DB mantenuta come da tua richiesta.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySQL(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient();

// Registra i servizi principali dell'applicazione come Singleton.
// Vengono creati una sola volta e condivisi per tutta la durata dell'applicazione.
builder.Services.AddSingleton<OpcUaMultiServerService>();
builder.Services.AddSingleton<MonitoringConfigService>();

// Registra il servizio di pulizia dati in background.
builder.Services.AddHostedService<DataCleanUpService>();

var app = builder.Build();

// Configura la pipeline delle richieste HTTP.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
