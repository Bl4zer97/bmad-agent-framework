# Architect Agent

## Ruolo

L'**Architect Agent** progetta l'architettura tecnica del sistema basandosi sui requisiti prodotti dall'Analyst. Applica i principi del **Azure Well-Architected Framework** e **Clean Architecture**.

## Responsabilità

- Scegliere il pattern architetturale appropriato
- Definire i servizi Azure necessari con motivazioni
- Progettare lo schema dati e le API
- Definire sicurezza, scalabilità e resilienza
- Produrre ADR (Architecture Decision Records)

## Input

- `requirements.md` dall'Analyst Agent
- Contesto della conversazione precedente

## Output

- `architecture.md`: documento tecnico con diagrammi ASCII, componenti e decisioni architetturali

## Principi Applicati

- Clean Architecture (Domain → Application → Infrastructure → Presentation)
- SOLID principles
- Azure Well-Architected Framework (5 pillar)
- Design patterns: Repository, CQRS, Mediator

## Configurazione

```json
{
  "ArchitectAgent": {
    "Temperature": 0.2,
    "MaxTokens": 4096
  }
}
```

La temperatura bassa (0.2) garantisce output più deterministici e tecnici.
