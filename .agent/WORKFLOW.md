# BMAD Workflow — Flusso Operativo Dettagliato

## Overview

```
Brief Cliente
     │
     ▼
┌─────────────┐
│   ANALYST   │ → PRD.md (requisiti, user stories, KPI)
└─────────────┘
     │
     ▼
┌─────────────┐
│  ARCHITECT  │ → TECH_SPEC.md + ARCHITECTURE.md
└─────────────┘
     │
     ▼
┌─────────────┐
│  DEVELOPER  │ → Codice in src/
└─────────────┘
     │
     ▼
┌─────────────┐
│  QA TESTER  │ → test_report.md
└─────────────┘
     │
     ▼
┌──────────────────┐
│  ORCHESTRATOR    │ → delivery_summary.md
└──────────────────┘
     │
     ▼
Revisione Umana → ✅ Approvazione / 🔄 Revisione
```

## Fase 1 — Requirements Intake (Analyst)
- **Input**: `projects/<nome>/brief.md`
- **Prompt**: `prompts/01_requirements_intake.md`
- **Output**: `artifacts/output/<nome>/PRD.md`
- **Gate**: Il PRD deve essere approvato prima di passare alla Fase 2

## Fase 2 — Technical Specification (Architect)
- **Input**: `artifacts/output/<nome>/PRD.md`
- **Prompt**: `prompts/02_tech_spec.md` + `prompts/03_architecture.md`
- **Output**: `artifacts/output/<nome>/TECH_SPEC.md` + `artifacts/output/<nome>/ARCHITECTURE.md`
- **Gate**: La Tech Spec deve essere approvata prima di passare alla Fase 3

## Fase 3 — Implementation (Developer)
- **Input**: `artifacts/output/<nome>/TECH_SPEC.md`
- **Prompt**: `prompts/04_implementation.md`
- **Output**: Codice in `projects/<nome>/src/`
- **Gate**: Code review prima di passare alla Fase 4

## Fase 4 — Testing & Validation (QA Tester)
- **Input**: Codice in `projects/<nome>/src/`
- **Prompt**: `prompts/05_review_validation.md`
- **Output**: `artifacts/output/<nome>/test_report.md`
- **Gate**: Tutti i test critici devono passare

## Fase 5 — Delivery (Orchestrator)
- **Input**: Tutti gli artifact + codice
- **Output**: `artifacts/output/<nome>/delivery_summary.md`
- **Action**: Notifica al consulente umano per revisione finale

---