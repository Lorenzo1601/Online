# Online

.NET9.0 Required --> [https://dotnet.microsoft.com/it-it/download/dotnet/9.0]

1. Scopo del Progetto
Lo scopo principale del progetto è creare un'applicazione web per il monitoraggio in tempo reale di macchinari industriali tramite il protocollo OPC UA. L'applicazione consente di:

•	Configurare la connessione a un server OPC UA.

•	Selezionare i nodi (variabili) del server OPC UA da monitorare.

•	Registrare i dati provenienti da questi nodi in un database.

•	Visualizzare i dati storici in forma di grafici e tabelle.

•	Gestire "ricette" o set di parametri da inviare ai macchinari.

•	Configurare la pulizia automatica dei dati storici per gestire lo spazio su disco.

L'applicazione è costruita utilizzando ASP.NET Core MVC, con un backend in C# e un frontend basato su Razor Pages, HTML, CSS e JavaScript (con jQuery e Bootstrap).

3. Architettura del Progetto
Il progetto segue un'architettura Model-View-Controller (MVC), tipica delle applicazioni ASP.NET Core.
•	Model: Le classi in C# che rappresentano i dati dell'applicazione (es. Macchina, Ricetta, MacchinaOpcUaLog) e i modelli per le viste (es. IndexViewModel). Si trovano principalmente nella directory Models/.
•	View: I file .cshtml che definiscono l'interfaccia utente. Si trovano nella directory Views/.
•	Controller: Le classi in C# che gestiscono le richieste HTTP, interagiscono con i modelli e restituiscono le viste. Si trovano nella directory Controllers/.
Oltre al pattern MVC, il progetto fa uso di Servizi per incapsulare la logica di business principale, come la comunicazione OPC UA e la pulizia dei dati.
4. Componenti Principali
3.1. Backend
•	Program.cs: Punto di ingresso dell'applicazione. Qui vengono configurati e registrati i servizi essenziali:
o	DbContext: Configura Entity Framework Core per la connessione al database MySQL (ApplicationDbContext).
o	Servizi in background: 
	OpcUaService: Servizio centrale che gestisce la connessione al server OPC UA, la sottoscrizione ai nodi e la scrittura dei dati nel database.
	DataCleanUpService: Servizio periodico che elimina i dati vecchi dalla tabella MacchinaOpcUaLogs in base alle impostazioni di ritenzione.
	MonitoringConfigService: Servizio che aggiorna dinamicamente la configurazione di monitoraggio del OpcUaService quando le impostazioni nel database vengono modificate.
•	Controllers/:
o	HomeController.cs: Gestisce la navigazione principale, inclusa la visualizzazione della home page, della pagina di configurazione OPC UA (OpcUa.cshtml), dello storico (Storico.cshtml) e delle ricette (Ricette.cshtml).
o	OpcUaController.cs: Fornisce endpoint API (/api/opcua/...) per interagire con il server OPC UA, ad esempio per navigare tra i nodi del server.
o	SettingsController.cs: Gestisce il salvataggio delle impostazioni dell'applicazione, come l'intervallo di pulizia dei dati e il periodo di ritenzione.
•	Services/:
o	OpcUaService.cs: Utilizza la libreria Opc.UaFx.Client per connettersi a un server OPC UA. Legge la configurazione delle macchine e dei nodi dal database e si sottoscrive alle modifiche dei loro valori. Quando un valore cambia, lo registra nella tabella MacchinaOpcUaLogs.
o	DataCleanUpService.cs: Un BackgroundService che, a intervalli regolari, esegue una query sul database per cancellare i log più vecchi della data di ritenzione impostata.
o	MonitoringConfigService.cs: Monitora le modifiche nelle configurazioni delle macchine e notifica al OpcUaService di ricaricare la sua configurazione senza dover riavviare l'intera applicazione.
•	Data/ApplicationDbContext.cs: Definisce il contesto del database per Entity Framework Core, mappando le classi del modello alle tabelle del database.
•	Models/:
o	Macchina.cs: Rappresenta un macchinario da monitorare, con le sue proprietà e l'elenco dei nodi OPC UA associati.
o	Connessione.cs: Rappresenta un nodo OPC UA da monitorare.
o	MacchinaOpcUaLog.cs: Rappresenta un singolo record di log, con il valore del nodo, il timestamp e l'ID della macchina.
o	Ricetta.cs e ParametroRicetta.cs: Definiscono la struttura per le ricette e i loro parametri.
o	CleanUpSettings.cs: Modello per le impostazioni di pulizia dei dati.
3.2. Database
•	DB/onlinedb.sql: Script SQL per la creazione dello schema del database MySQL. Le tabelle principali sono: 
o	Macchine: Elenco dei macchinari configurati.
o	Connessioni: Elenco dei nodi OPC UA da monitorare per ogni macchina.
o	MacchinaOpcUaLogs: Tabella in cui vengono storicizzati i dati letti dal server OPC UA.
o	Ricette e ParametriRicette: Tabelle per la gestione delle ricette.
o	Settings: Tabella per le impostazioni generali dell'applicazione.
3.3. Frontend
•	Views/: Contiene le pagine Razor per l'interfaccia utente.
o	Home/Index.cshtml: La dashboard principale che visualizza lo stato attuale dei nodi monitorati.
o	Home/OpcUa.cshtml: Pagina per la configurazione delle macchine e l'aggiunta/rimozione dei nodi OPC UA da monitorare.
o	Home/Storico.cshtml: Pagina per visualizzare i grafici dei dati storici. Permette di selezionare una macchina, un intervallo di date e visualizzare i dati. Include una funzione per esportare i dati in PDF.
o	Home/Ricette.cshtml: Interfaccia per la gestione delle ricette.
o	Settings/Index.cshtml: Pagina per le impostazioni di pulizia dei dati.
•	wwwroot/: Contiene tutte le risorse statiche.
o	js/site.js: Script JavaScript generici per il sito.
o	js/Ricette.js: Logica JavaScript specifica per la gestione dinamica delle ricette nella pagina Ricette.cshtml.
o	css/site.css: Fogli di stile personalizzati.
o	lib/: Contiene le librerie client-side come Bootstrap e jQuery.
5. Funzionalità Chiave
1.	Configurazione Dinamica: L'applicazione permette di aggiungere nuove macchine e specificare i nodi OPC UA da monitorare direttamente dall'interfaccia web. Queste configurazioni sono salvate nel database e caricate dinamicamente dal OpcUaService.
2.	Monitoraggio in Tempo Reale: Il OpcUaService si sottoscrive ai cambiamenti di valore dei nodi OPC UA e li registra immediatamente nel database, fornendo un monitoraggio continuo.
3.	Visualizzazione Dati Storici: La sezione "Storico" permette agli utenti di analizzare le performance passate dei macchinari tramite grafici interattivi.
4.	Gestione Ricette: È possibile creare, modificare ed eliminare "ricette" (set di parametri). Anche se la logica per inviare queste ricette al PLC non è esplicitamente visibile nel codice fornito, l'infrastruttura per la loro gestione è presente.
5.	Manutenzione Automatica del Database: Il DataCleanUpService assicura che il database non cresca indefinitamente, eliminando periodicamente i dati più vecchi e mantenendo le performance del sistema.
5. Come Avviare il Progetto
Per eseguire questo progetto in un ambiente di sviluppo, sono necessari i seguenti passaggi:
1.	Prerequisiti:
o	.NET SDK (versione compatibile con il progetto, probabilmente .NET 6 o successiva).
o	Un server di database MySQL.
o	Un server OPC UA per il testing (può essere simulato).
2.	Configurazione del Database:
o	Eseguire lo script DB/onlinedb.sql sul server MySQL per creare il database onlinedb e le relative tabelle.
o	Aggiornare la stringa di connessione nel file appsettings.json, specificando l'indirizzo del server, il nome utente e la password per accedere a MySQL.
3.	Avvio dell'Applicazione:
o	Aprire il progetto con un IDE come Visual Studio o Visual Studio Code.
o	Eseguire il comando dotnet run dalla root del progetto.
o	L'applicazione sarà accessibile all'URL specificato nel profilo di avvio (es. https://localhost:7037).
