using ExpressRecipe.Data.Common;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.AIService.Data;

public sealed class AIProviderConfigRepository : SqlHelper, IAIProviderConfigRepository
{
    public AIProviderConfigRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<AIProviderConfigDto?> GetConfigAsync(string useCase,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT UseCase, Provider
            FROM AIProviderConfig
            WHERE UseCase = @UseCase AND IsDeleted = 0
            """;

        var results = await ExecuteReaderAsync(
            sql,
            reader => new AIProviderConfigDto
            {
                UseCase  = GetString(reader, "UseCase") ?? string.Empty,
                Provider = GetString(reader, "Provider") ?? "Ollama"
            },
            ct,
            CreateParameter("@UseCase", useCase));

        return results.FirstOrDefault();
    }
}
