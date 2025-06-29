﻿using Online.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace online.Models
{
    [Table("macchinestorico")]
    public class MacchinaOpcUaLog
    {
        // La chiave primaria è composta e viene definita in ApplicationDbContext.cs

        [Column("NomeMacchina")]
        public string NomeMacchina { get; set; }

        [Column("Nome")]
        public string Nome { get; set; }

        [Column("Nodo")]
        public string? Nodo { get; set; }

        [Column("Valore")]
        public string? Valore { get; set; }

        [Column("Qualita")]
        public string? Qualita { get; set; }

        [Column("Timestamp")]
        public DateTime Timestamp { get; set; }

        // Proprietà di navigazione per la relazione con Connessione
        public Connessione Connessione { get; set; }
    }
}
