# Analyst Agent

## Ruolo

L'**Analyst Agent** è il primo agente nel workflow BMAD. Trasforma i requisiti informali dell'utente in un documento strutturato (PRD - Product Requirements Document).

## Responsabilità

- Analizzare il testo libero dei requisiti
- Identificare le funzionalità core del sistema
- Definire utenti finali e casi d'uso
- Produrre requisiti funzionali e non funzionali
- Suggerire lo stack tecnologico appropriato

## Input

- Stringa di testo con i requisiti dell'utente (linguaggio naturale)
- Metadati opzionali (stack preferito, tipo di app)

## Output

- `requirements.md`: PRD completo con user stories e criteri di accettazione

## System Prompt

Il prompt di sistema dell'Analyst è configurato per:
- Produrre documenti in formato Markdown strutturato
- Essere preciso e tecnico nelle specifiche
- Identificare i rischi e le dipendenze

## Configurazione

```json
{
  "AnalystAgent": {
    "Temperature": 0.5,
    "MaxTokens": 4096
  }
}
```

La temperatura più alta (0.5) rispetto agli altri agenti permette maggiore creatività nell'identificazione dei casi d'uso.
