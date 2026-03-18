# BMAD Rules — Regole di Ingaggio

## Regole Universali (tutti gli agenti)

### ✅ SEMPRE
- Leggere il proprio file persona prima di iniziare qualsiasi task
- Dichiarare il proprio ruolo all'inizio di ogni risposta: `[AGENTE: Analyst]`
- Produrre output strutturato secondo i template in `artifacts/templates/`
- Chiudere con un **Handoff esplicito**: chi deve fare cosa dopo
- Usare date nel formato `YYYY-MM-DD`
- Versionare gli artifact: `v1.0`, `v1.1`, ecc.

### ❌ MAI
- Uscire dal proprio scope di ruolo
- Inventare requisiti, dati o comportamenti non specificati
- Saltare una fase del workflow
- Generare codice senza TECH_SPEC approvata
- Modificare artifact di fasi precedenti senza notifica

## Regole per Gestione Ambiguità

Se un requisito è ambiguo:
1. NON procedere con assunzioni
2. Formulare 2-3 domande di chiarimento specifiche
3. Attendere risposta prima di continuare
4. Documentare la risposta in "Decisioni" nel PRD

## Regole per la Qualità del Codice

- Ogni funzione deve avere un docstring/commento
- Nessuna credenziale hardcoded (usare `.env`)
- Test unitari per ogni funzione critica
- Nomi variabili in inglese, commenti in italiano (opzionale)

## Formato Handoff

Al termine di ogni fase, usare questo formato:

```
---
✅ **FASE [N] COMPLETATA**
**Agente**: [Nome Agente]
**Output prodotto**: [nome file/artifact]
**Decisioni chiave**: [lista brevissima]
**Open Points**: [se presenti]
**Handoff →**: [Nome Prossimo Agente] per [Fase N+1]
---
```