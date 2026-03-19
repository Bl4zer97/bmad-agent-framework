namespace BmadAgentFramework.Agents.QA;

/// <summary>
/// Generatore di prompt per la generazione dei test.
/// Costruisce prompt specializzati per il QA Agent.
/// </summary>
public static class TestGenerator
{
    /// <summary>
    /// Costruisce il prompt per la generazione dei test.
    /// </summary>
    public static string BuildTestGenerationPrompt(
        string requirementsContent,
        string codeContent,
        string? projectName)
    {
        return $"""
            Analizza il codice C# seguente e genera una suite di test completa.

            ## Requisiti Originali
            {requirementsContent}

            ## Codice da Testare
            {codeContent}

            ## Output Richiesto

            ### 1. Unit Tests (xUnit + FluentAssertions + Moq)
            - Test per ogni metodo della business logic
            - Happy path e error path
            - Boundary values
            - Naming: Should_[ExpectedBehavior]_When_[StateUnderTest]

            ### 2. Integration Tests (WebApplicationFactory)
            - Test per gli endpoint API principali
            - Test del flusso CRUD completo
            - Verifica status codes e response body

            ### 3. Test Data Builders
            - Classi helper per creare dati di test
            - Utilizzo di Bogus per dati realistici

            ### 4. Test Report
            Documento markdown con:
            - Lista completa dei test cases
            - Coverage estimate
            - Rischi identificati
            - Raccomandazioni

            ## Standard
            - xUnit con [Fact] e [Theory]
            - FluentAssertions per assertions leggibili
            - Moq per il mocking
            - In-memory database per i repository test
            - AAA pattern (Arrange, Act, Assert) con commenti

            Produci codice completo, compilabile e con commenti in italiano.
            """;
    }
}
