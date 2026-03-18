# DevOps Agent

## Ruolo

Il **DevOps Agent** genera l'infrastruttura Azure con Bicep e la pipeline CI/CD con GitHub Actions, completando il ciclo di vita del software dal codice al deployment.

## Responsabilità

- Generare pipeline GitHub Actions (build → test → scan → deploy)
- Creare infrastruttura Azure con Bicep (IaC)
- Scrivere Dockerfile multi-stage
- Documentare le procedure operative

## Input

- `architecture.md` dall'Architect (servizi Azure, requisiti)
- `requirements.md` per informazioni sul deployment

## Output

- `ci.yml`: GitHub Actions pipeline completa
- `main.bicep`: infrastruttura Azure come codice
- Dockerfile ottimizzato per .NET 8
- `deploy.md`: documentazione operativa

## Best Practice Applicate

- **GitOps**: infrastruttura come codice nel repository
- **Zero-trust**: nessun segreto in chiaro, tutto in Key Vault
- **Environment separation**: dev/staging/prod con approvazioni
- **Automated rollback**: in caso di fallimento del deployment

## Configurazione

```json
{
  "DevOpsAgent": {
    "Temperature": 0.1,
    "MaxTokens": 8192
  }
}
```

Temperatura molto bassa (0.1): la generazione di YAML e Bicep deve essere estremamente deterministica.
