# 🤖 BMAD Agent Framework

> Framework AI Multi-Agente per la Consulenza IT — Metodologia BMAD & Orchestrazione Artigianale in-IDE

---

## Cos'è questo progetto?

Questo repository è il **sistema nervoso** di un team virtuale di agenti AI specializzati. Ogni file `.md` è un'istruzione, un ruolo, una memoria o un artifact. Il principio fondamentale: **artefatti prima del codice**.

## La Metodologia BMAD

| Livello | Componente    | Descrizione                              | Esempi                        |
|---------|---------------|------------------------------------------|-------------------------------|
| 1       | Modelli (LLM) | Il motore logico (forza lavoro).         | GPT-4, Claude 3.5, Gemini     |
| 2       | Framework     | L'infrastruttura di automazione.         | LangGraph, CrewAI, AutoGen    |
| 3       | Metodologia   | Le regole di ingaggio e i ruoli.         | BMAD, OpenSpec                |
| 4       | Interfaccia   | L'ambiente di lavoro umano-IA.           | Cursor, VS Code, Windsurf     |

## Il Team Virtuale

| Agente          | File                          | Responsabilità                       |
|-----------------|-------------------------------|--------------------------------------|
| 🔍 Analyst      | `personas/analyst.md`         | Requisiti, PRD, user stories         |
| 🏗️ Architect    | `personas/architect.md`       | Tech spec, schema DB, API design     |
| 💻 Developer    | `personas/developer.md`       | Implementazione codice               |
| 🧪 QA Tester    | `personas/qa_tester.md`       | Test, validazione, bug report        |
| 🎯 Orchestrator | `personas/orchestrator.md`    | Coordinamento, routing task          |

## Workflow (5 Fasi)

```
Input Cliente → [Analyst] PRD → [Architect] Tech Spec → [Developer] Codice → [QA] Validazione → Consegna
```

## Quick Start

1. Clona il repository
2. Copia `projects/_template/` in `projects/<nome-cliente>/`
3. Incolla il brief del cliente in `projects/<nome-cliente>/brief.md`
4. Apri VS Code / Cursor e segui il workflow in `docs/BMAD_GUIDE.md`

## Struttura del Progetto

```
.agent/          ← Istruzioni globali per l'AI (SYSTEM_PROMPT, WORKFLOW, RULES)
personas/        ← Definizione dei ruoli agente
prompts/         ← Template prompt per ogni fase
artifacts/       ← Template e output documentazione
projects/        ← Un subfolder per progetto cliente
docs/            ← Guide e documentazione operativa
```

## Roadmap

| Fase | Approccio       | Strumenti                                        |
|------|-----------------|--------------------------------------------------|
| 1    | Artigianale     | VS Code + Copilot/Cursor + file MD               |
| 2    | Semi-automatico | Script Python + API LLM                          |
| 3    | Industriale     | LangGraph / CrewAI con state management          |

---

> 📖 Leggi `docs/BMAD_GUIDE.md` per il workflow completo.
> ⚙️ Leggi `docs/SETUP.md` per configurare l'ambiente.