using Mediso.AiImpactAnalysis.Core.Models;

namespace Mediso.AiImpactAnalysis.Core.Abstractions;

public interface IIndexStore
{
    Task SaveChunksAsync(string dataPath, IReadOnlyList<CodeChunk> chunks, CancellationToken cancellationToken = default);
    Task SaveEmbeddingsAsync(string dataPath, IReadOnlyList<ChunkEmbedding> embeddings, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CodeChunk>> LoadChunksAsync(string dataPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChunkEmbedding>> LoadEmbeddingsAsync(string dataPath, CancellationToken cancellationToken = default);
}
