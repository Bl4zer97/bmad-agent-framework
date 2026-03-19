namespace BmadAgentFramework.Agents.Analyst;

/// <summary>
/// Parser e builder per i prompt di analisi dei requisiti.
/// Costruisce prompt strutturati per l'Analyst Agent da inviare ad Azure OpenAI.
/// </summary>
public static class RequirementsParser
{
    /// <summary>
    /// Costruisce il prompt di analisi partendo dai requisiti dell'utente.
    /// Il prompt include istruzioni specifiche per produrre un PRD strutturato.
    /// </summary>
    /// <param name="userRequirements">Testo libero con i requisiti dell'utente</param>
    /// <param name="metadata">Metadati aggiuntivi (es. stack tecnologico preferito)</param>
    public static string BuildAnalysisPrompt(
        string userRequirements,
        Dictionary<string, string>? metadata = null)
    {
        var techStack = metadata?.TryGetValue("techStack", out var stack) == true
            ? stack
            : "C# .NET 8, Azure";

        return $"""
            Analizza i seguenti requisiti di progetto e produci un documento di analisi completo.

            ## Requisiti del Cliente
            {userRequirements}

            ## Stack Tecnologico Target
            {techStack}

            ## Output Richiesto
            Produci un documento di analisi (PRD) con questa struttura:

            # Product Requirements Document (PRD)

            ## 1. Panoramica del Progetto
            - Titolo del progetto
            - Obiettivo principale
            - Valore business

            ## 2. Utenti e Stakeholder
            - Chi sono gli utenti finali
            - Ruoli e permessi

            ## 3. Requisiti Funzionali
            Lista dettagliata delle funzionalità (formato: RF-01, RF-02, ...)

            ## 4. Requisiti Non Funzionali
            - Performance
            - Sicurezza
            - Scalabilità
            - Disponibilità

            ## 5. User Stories
            Formato: Come [utente], voglio [azione], per [beneficio]

            ## 6. Criteri di Accettazione
            Per ogni user story principale

            ## 7. Stack Tecnologico Raccomandato
            - Framework .NET suggerito
            - Servizi Azure necessari
            - Pattern architetturali consigliati

            ## 8. Stima Complessità
            - Effort stimato (T-shirt sizing: S/M/L/XL)
            - Rischi principali
            - Dipendenze

            Sii preciso, professionale e tecnico. Il documento deve essere direttamente utilizzabile dall'Architect Agent.
            """;
    }

    /// <summary>
    /// Estrae il nome del progetto dall'output dell'analisi.
    /// Cerca il titolo nel formato "# Nome Progetto" o "Titolo: Nome Progetto".
    /// </summary>
    public static string ExtractProjectName(string analysisOutput)
    {
        var lines = analysisOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("# ") && !line.StartsWith("## "))
            {
                return line[2..].Trim();
            }
            if (line.Contains("Titolo:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(':', 2);
                if (parts.Length > 1)
                    return parts[1].Trim();
            }
        }
        return "Progetto BMAD";
    }
}
