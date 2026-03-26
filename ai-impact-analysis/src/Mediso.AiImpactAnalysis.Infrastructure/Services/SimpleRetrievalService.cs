using Mediso.AiImpactAnalysis.Core.Abstractions;
using Mediso.AiImpactAnalysis.Core.Models;

namespace Mediso.AiImpactAnalysis.Infrastructure.Services;

public sealed class SimpleRetrievalService : IRetrievalService
{
    private readonly IEmbeddingClient _embeddingClient;

    public SimpleRetrievalService(IEmbeddingClient embeddingClient)
    {
        _embeddingClient = embeddingClient;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query,
        IReadOnlyList<CodeChunk> chunks,
        IReadOnlyList<ChunkEmbedding> embeddings,
        int top,
        CancellationToken cancellationToken = default
    )
    {
        if (chunks.Count == 0 || embeddings.Count == 0)
        {
            return [];
        }

        var queryEmbedding = await _embeddingClient.GenerateEmbeddingAsync(query, cancellationToken);
        if (queryEmbedding.Count == 0)
        {
            return [];
        }

        var byId = chunks.ToDictionary(chunk => chunk.Id, chunk => chunk);
        var scored = new List<RetrievedChunk>();

        foreach (var embedding in embeddings)
        {
            if (!byId.TryGetValue(embedding.ChunkId, out var chunk))
            {
                continue;
            }

            var score = CosineSimilarity(queryEmbedding, embedding.Vector) + MetadataBoost(query, chunk);
            scored.Add(new RetrievedChunk(chunk, score));
        }

        return scored
            .OrderByDescending(item => item.Score)
            .Take(Math.Max(1, top))
            .ToList();
    }

    private static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var length = Math.Min(left.Count, right.Count);
        if (length == 0)
        {
            return 0;
        }

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (var i = 0; i < length; i++)
        {
            var l = left[i];
            var r = right[i];
            dot += l * r;
            leftNorm += l * l;
            rightNorm += r * r;
        }

        if (leftNorm <= 0 || rightNorm <= 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }

    private static double MetadataBoost(string query, CodeChunk chunk)
    {
        var normalizedQuery = query.ToLowerInvariant();
        var boost = 0d;

        if (normalizedQuery.Contains("valid") || normalizedQuery.Contains("iban") || normalizedQuery.Contains("účt"))
        {
            if (chunk.Kind is "Controller" or "Handler" or "Validator")
            {
                boost += 0.06;
            }
        }

        if (normalizedQuery.Contains("dokument") || normalizedQuery.Contains("specifik") || normalizedQuery.Contains("api"))
        {
            if (chunk.Layer == "Docs")
            {
                boost += 0.04;
            }
        }

        if (!query.Contains("apikey") && (query.Contains("api") || query.Contains("endpoint") || query.Contains("http")))
        {
            if (chunk.Layer == "Api")
            {
                if (chunk.FilePath.Contains("Jobs", StringComparison.OrdinalIgnoreCase) ||
                    chunk.FilePath.Contains("Ops", StringComparison.OrdinalIgnoreCase) ||
                    chunk.FilePath.Contains("Auth", StringComparison.OrdinalIgnoreCase))
                {
                    boost -= 0.05;
                }
                else
                {
                    boost += 0.08;
                }
            }
        }

        if (chunk.FilePath.Contains("Endpoints") || chunk.FilePath.Contains("Controller"))
        {
            boost += 0.05;
        }

        return boost;
    }
}