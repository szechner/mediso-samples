namespace Mediso.AiImpactAnalysis.Core.Models;

public sealed record ChunkEmbedding(string ChunkId, IReadOnlyList<float> Vector);
