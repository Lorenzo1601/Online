# Online

.NET9.0 Required --> [https://dotnet.microsoft.com/it-it/download/dotnet/9.0]

Online Monitoring System - Sistema di Monitoraggio OPC UA
Questo progetto è un'applicazione web sviluppata in ASP.NET Core MVC per il monitoraggio e la gestione di macchinari industriali che comunicano tramite il protocollo OPC UA.

📜 Descrizione
L'applicazione permette di connettersi a un server OPC UA, selezionare le variabili (nodi) da monitorare, storicizzarne i valori su un database MySQL e visualizzarli attraverso grafici interattivi. Include inoltre un sistema di gestione di "ricette" (set di parametri) e un meccanismo automatico per la pulizia dei dati storici.

🚀 Tecnologie Utilizzate
Backend: .NET 6, ASP.NET Core MVC, C#

Database: MySQL

ORM: Entity Framework Core

Comunicazione OPC UA: Opc.UaFx.Client

Frontend: HTML, CSS, JavaScript

Librerie Frontend: Bootstrap 5, jQuery, Chart.js (per i grafici)

Esportazione PDF: Libreria per la generazione di PDF da HTML (es. Rotativa, SelectPdf, ecc.)

✨ Funzionalità Principali
📊 Dashboard in Tempo Reale: Visualizza l'ultimo valore ricevuto dai nodi OPC UA monitorati.

🛠️ Configurazione Dinamica: Aggiungi, modifica ed elimina macchinari e i relativi nodi OPC UA da monitorare direttamente dall'interfaccia web, senza riavviare l'applicazione.

📈 Storico e Grafici: Analizza i dati storici attraverso grafici interattivi. Filtra per macchina e per intervallo di date e orari.

📄 Esportazione PDF: Esporta la vista tabellare dei dati storici filtrati in un file PDF.

🧾 Gestione Ricette: Crea e gestisci set di parametri (ricette) da inviare potenzialmente ai macchinari.

⚙️ Impostazioni Configurabili:

Imposta l'indirizzo del server OPC UA.

Configura la pulizia automatica dei dati, specificando l'intervallo di esecuzione del servizio e il periodo di ritenzione dei log.

❤️ Servizi in Background Affidabili:

Un servizio dedicato gestisce la connessione OPC UA e la registrazione dei dati.

Un secondo servizio si occupa della pulizia periodica del database per ottimizzare lo spazio e le performance.

Un terzo servizio monitora le modifiche alla configurazione per aggiornare il client OPC UA in tempo reale.

⚙️ Guida all'Installazione e Avvio
Per eseguire il progetto in un ambiente di sviluppo, segui questi passaggi.

Prerequisiti
.NET 6 SDK o versione successiva.

Un'istanza di MySQL Server in esecuzione.

Un server OPC UA per i test (es. Prosys Simulation Server).

Un IDE come Visual Studio 2022 o Visual Studio Code.

1. Configurazione del Database
Crea un nuovo database sul tuo server MySQL (es. onlinedb).

Esegui lo script SQL che trovi in DB/onlinedb.sql per creare tutte le tabelle necessarie.

2. Configurazione dell'Applicazione
Clona la repository:

git clone https://github.com/lorenzo1601/online.git
cd online

Apri il file appsettings.json.

Modifica la stringa di connessione DefaultConnection con i tuoi dati di accesso a MySQL:

"ConnectionStrings": {
  "DefaultConnection": "server=localhost;port=3306;database=onlinedb;user=root;password=TUA_PASSWORD"
}

Nella sezione OpcUaConnectionSettings, inserisci l'indirizzo del tuo server OPC UA:

"OpcUaConnectionSettings": {
  "ServerUrl": "opc.tcp://localhost:4840"
}

3. Avvio
Apri un terminale nella directory principale del progetto.

Esegui l'applicazione con il seguente comando:

dotnet run

L'applicazione sarà disponibile agli indirizzi specificati nel file Properties/launchSettings.json (es. https://localhost:7037).

📁 Struttura del Progetto
/
├── Controllers/        # Gestiscono le richieste HTTP e la logica delle pagine
├── Data/               # Contiene il DbContext per Entity Framework
├── DB/                 # Contiene lo script SQL per la creazione del database
├── Models/             # Classi che rappresentano i dati e i ViewModel
├── Properties/         # Impostazioni di avvio
├── Views/              # Pagine Razor (CSHTML) per l'interfaccia utente
├── wwwroot/            # File statici (CSS, JS, immagini, librerie)
├── DataCleanUpService.cs   # Servizio per la pulizia periodica dei dati
├── MonitoringConfigService.cs # Servizio per l'aggiornamento dinamico della config
├── OpcUaService.cs     # Servizio principale per la comunicazione OPC UA
└── Program.cs          # Punto di ingresso dell'applicazione e configurazione dei servizi
