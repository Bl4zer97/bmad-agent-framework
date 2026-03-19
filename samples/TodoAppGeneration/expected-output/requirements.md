# Product Requirements Document (PRD)
## Progetto: Todo App REST API

**Generato da**: BMAD Analyst Agent  
**Data**: 2024-01-15  
**Versione**: 1.0

---

## 1. Panoramica del Progetto

**Titolo**: Todo App REST API  
**Obiettivo**: Fornire un'API REST completa per la gestione di liste di task (todo) con supporto multi-utente, autenticazione Azure AD e deployment su Azure.  
**Valore Business**: Aumentare la produttività dei team riducendo il tempo di gestione delle attività del 30%.

---

## 2. Utenti e Stakeholder

| Ruolo | Descrizione | Permessi |
|-------|-------------|----------|
| User | Utente finale che gestisce i propri task | CRUD sui propri task |
| Admin | Amministratore del sistema | CRUD su tutti i task, gestione utenti |
| Manager | Manager di team | Visualizzazione task del team |

---

## 3. Requisiti Funzionali

| ID | Requisito | Priorità |
|----|-----------|----------|
| RF-01 | Creare un nuovo task con titolo, descrizione, scadenza e priorità | Alta |
| RF-02 | Elencare tutti i task dell'utente corrente con paginazione | Alta |
| RF-03 | Filtrare task per stato (completato/non completato) | Alta |
| RF-04 | Filtrare task per priorità (Alta/Media/Bassa) | Media |
| RF-05 | Aggiornare i dettagli di un task | Alta |
| RF-06 | Segnare un task come completato | Alta |
| RF-07 | Eliminare un task | Alta |
| RF-08 | Assegnare un task a un altro utente | Media |
| RF-09 | Autenticazione tramite Azure AD (Entra ID) | Alta |
| RF-10 | Notifiche email per task scaduti | Bassa |

---

## 4. Requisiti Non Funzionali

- **Performance**: Response time < 200ms per il 95° percentile
- **Scalabilità**: Supporto fino a 10.000 utenti concorrenti
- **Disponibilità**: 99.9% uptime (SLA Azure App Service)
- **Sicurezza**: OWASP Top 10 compliance, dati cifrati at rest e in transit
- **Manutenibilità**: Code coverage > 80%, documentazione Swagger completa

---

## 5. User Stories

1. **Come utente**, voglio creare un task con titolo e descrizione, per organizzare il mio lavoro
2. **Come utente**, voglio vedere tutti i miei task pendenti, per sapere cosa devo fare
3. **Come utente**, voglio filtrare i task per priorità, per gestire prima le cose più urgenti
4. **Come manager**, voglio assegnare task ai miei collaboratori, per delegare il lavoro

---

## 6. Stack Tecnologico Raccomandato

- **Framework**: ASP.NET Core 8 Web API
- **Database**: Azure SQL Database + Entity Framework Core 8
- **Autenticazione**: Azure AD (Microsoft.Identity.Web)
- **Hosting**: Azure App Service (B2 tier per staging, P1v3 per prod)
- **Monitoring**: Application Insights
- **CI/CD**: GitHub Actions
