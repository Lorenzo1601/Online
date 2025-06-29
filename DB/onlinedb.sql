CREATE DATABASE  IF NOT EXISTS `onlinedb` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci */ /*!80016 DEFAULT ENCRYPTION='N' */;
USE `onlinedb`;
-- MySQL dump 10.13  Distrib 8.0.41, for Win64 (x86_64)
--
-- Host: 127.0.0.1    Database: onlinedb
-- ------------------------------------------------------
-- Server version	8.4.5

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `connessioni`
--

DROP TABLE IF EXISTS `connessioni`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `connessioni` (
  `NomeMacchina` varchar(512) NOT NULL,
  `IP_Address` varchar(512) NOT NULL,
  `Porta` int NOT NULL,
  `Tipo` varchar(45) NOT NULL,
  PRIMARY KEY (`NomeMacchina`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `connessioni`
--

LOCK TABLES `connessioni` WRITE;
/*!40000 ALTER TABLE `connessioni` DISABLE KEYS */;
/*!40000 ALTER TABLE `connessioni` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `macchine`
--

DROP TABLE IF EXISTS `macchine`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `macchine` (
  `NomeMacchina` varchar(512) NOT NULL,
  `IP_Address` varchar(100) NOT NULL,
  PRIMARY KEY (`NomeMacchina`,`IP_Address`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `macchine`
--

LOCK TABLES `macchine` WRITE;
/*!40000 ALTER TABLE `macchine` DISABLE KEYS */;
/*!40000 ALTER TABLE `macchine` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `macchineopcua`
--

DROP TABLE IF EXISTS `macchineopcua`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `macchineopcua` (
  `NomeMacchina` varchar(210) NOT NULL,
  `Nome` varchar(512) NOT NULL,
  `Nodo` varchar(1024) DEFAULT NULL,
  `Valore` varchar(512) DEFAULT NULL,
  `Qualita` varchar(45) DEFAULT NULL,
  `TimeStamp` datetime DEFAULT NULL,
  KEY `NomeMacchina_idx` (`NomeMacchina`),
  CONSTRAINT `NomeMacchina` FOREIGN KEY (`NomeMacchina`) REFERENCES `connessioni` (`NomeMacchina`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `macchineopcua`
--

LOCK TABLES `macchineopcua` WRITE;
/*!40000 ALTER TABLE `macchineopcua` DISABLE KEYS */;
/*!40000 ALTER TABLE `macchineopcua` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `parametricetta`
--

DROP TABLE IF EXISTS `parametricetta`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `parametricetta` (
  `NomeRicetta` varchar(512) NOT NULL,
  `NomeTag` varchar(212) NOT NULL,
  `NomeMacchina` varchar(512) NOT NULL,
  `Connessione` varchar(2048) NOT NULL,
  `Valore` varchar(1024) NOT NULL,
  KEY `NomeRicetta_idx` (`NomeRicetta`),
  CONSTRAINT `NomeRicetta` FOREIGN KEY (`NomeRicetta`) REFERENCES `ricette` (`NomeRicetta`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `parametricetta`
--

LOCK TABLES `parametricetta` WRITE;
/*!40000 ALTER TABLE `parametricetta` DISABLE KEYS */;
/*!40000 ALTER TABLE `parametricetta` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `ricette`
--

DROP TABLE IF EXISTS `ricette`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ricette` (
  `NomeRicetta` varchar(512) NOT NULL,
  PRIMARY KEY (`NomeRicetta`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `ricette`
--

LOCK TABLES `ricette` WRITE;
/*!40000 ALTER TABLE `ricette` DISABLE KEYS */;
/*!40000 ALTER TABLE `ricette` ENABLE KEYS */;
UNLOCK TABLES;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2025-06-18 10:44:55
