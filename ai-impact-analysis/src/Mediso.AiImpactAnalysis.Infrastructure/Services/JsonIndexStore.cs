using System.Text.Json;
using Mediso.AiImpactAnalysis.Core.Abstractions;
using Mediso.AiImpactAnalysis.Core.Models;
using Microsoft.Extensions.Logging;

namespace Mediso.AiImpactAnalysis.Infrastructure.Services;

public sealed class JsonIndexStore : IIndexStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<JsonIndexStore> _logger;

    public JsonIndexStore(ILogger<JsonIndexStore> logger)
    {
        _logger = logger;
    }

    public async Task SaveChunksAsync(string dataPath, IReadOnlyList<CodeChunk> chunks, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(dataPath);
        var path = Path.Combine(dataPath, "chunks.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, chunks, JsonOptions, cancellationToken);
        _logger.LogInformation("Uloženo {Count} chunků do {Path}", chunks.Count, path);
    }

    public async Task SaveEmbeddingsAsync(string dataPath, IReadOnlyList<ChunkEmbedding> embeddings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(dataPath);
        var path = Path.Combine(dataPath, "embeddings.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, embeddings, JsonOptions, cancellationToken);
        _logger.LogInformation("Uloženo {Count} embeddingů do {Path}", embeddings.Count, path);
    }

    public async Task<IReadOnlyList<CodeChunk>> LoadChunksAsync(string dataPath, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(dataPath, "chunks.json");
        if (!File.Exists(path)) return [];

        await using var stream = File.OpenRead(path);
        var chunks = await JsonSerializer.DeserializeAsync<List<CodeChunk>>(stream, JsonOptions, cancellationToken);
        var result = chunks ?? [];
        _logger.LogInformation("Načteno {Count} chunků z {Path}", result.Count, path);
        return result;
    }

    public async Task<IReadOnlyList<ChunkEmbedding>> LoadEmbeddingsAsync(string dataPath, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(dataPath, "embeddings.json");
        if (!File.Exists(path)) return [];

        await using var stream = File.OpenRead(path);
        var embeddings = await JsonSerializer.DeserializeAsync<List<ChunkEmbedding>>(stream, JsonOptions, cancellationToken);
        var result = embeddings ?? [];
        _logger.LogInformation("Načteno {Count} embeddingů z {Path}", result.Count, path);
        return result;
    }
}
