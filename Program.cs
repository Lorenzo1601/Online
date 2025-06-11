using Microsoft.EntityFrameworkCore;
using Online.Data; // Assicurati che questo using sia presente
// using Pomelo.EntityFrameworkCore.MySql; // Rimosso se non usi Pomelo, commentato se preferisci l'altro provider
using MySql.EntityFrameworkCore; // Aggiunto se usi il provider Oracle/MySQL ufficiale

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Recupera la stringa di connessione da appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Registra ApplicationDbContext per la dependency injection
// Scegli UNA delle seguenti opzioni a seconda del provider MySQL che stai usando:

// Opzione 1: Se usi MySql.EntityFrameworkCore (provider ufficiale Oracle)
// Assicurati di aver installato il pacchetto NuGet MySql.EntityFrameworkCore.
// Sostituisci new Version(8, 4, 4) con la versione del tuo server MySQL se diversa.
// Ad esempio, per MySQL 8.0.21, usa: new MySqlServerVersion(new Version(8, 0, 21))
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySQL(connectionString) // Esempio: MySQL Server versione 8.4.4
);

// Opzione 2: Se stavi usando Pomelo.EntityFrameworkCore.MySql (e vuoi continuare ad usarlo)
// Assicurati di aver installato il pacchetto NuGet Pomelo.EntityFrameworkCore.MySql.
// Decommenta la riga seguente e commenta quella dell'Opzione 1.
// builder.Services.AddDbContext<ApplicationDbContext>(options =>
//    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
// );


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
