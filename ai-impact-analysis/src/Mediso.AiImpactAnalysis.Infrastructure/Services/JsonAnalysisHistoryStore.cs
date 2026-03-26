using System.Text.Json;
using Mediso.AiImpactAnalysis.Core.Abstractions;
using Mediso.AiImpactAnalysis.Core.Models;
using Microsoft.Extensions.Logging;

namespace Mediso.AiImpactAnalysis.Infrastructure.Services;

public sealed class JsonAnalysisHistoryStore : IAnalysisHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<JsonAnalysisHistoryStore> _logger;

    public JsonAnalysisHistoryStore(ILogger<JsonAnalysisHistoryStore> logger)
    {
        _logger = logger;
    }

    public async Task<string?> SaveAsync(AnalysisHistoryEntry entry, string dataPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var historyDirectory = Path.Combine(dataPath, "analysis-history");
            Directory.CreateDirectory(historyDirectory);

            var fileName = $"{entry.TimestampUtc:yyyyMMdd-HHmmss}.json";
            var fullPath = Path.Combine(historyDirectory, fileName);

            await using var stream = File.Create(fullPath);
            await JsonSerializer.SerializeAsync(stream, entry, JsonOptions, cancellationToken);

            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nepodařilo se uložit analysis-history.");
            return null;
        }
    }
}
