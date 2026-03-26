using Mediso.AiImpactAnalysis.Core.Models;

namespace Mediso.AiImpactAnalysis.Core.Abstractions;

public interface IRetrievalService
{
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query,
        IReadOnlyList<CodeChunk> chunks,
        IReadOnlyList<ChunkEmbedding> embeddings,
        int top,
        CancellationToken cancellationToken = default);
}
