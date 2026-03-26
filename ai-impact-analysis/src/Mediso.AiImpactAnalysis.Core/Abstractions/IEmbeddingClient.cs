namespace Mediso.AiImpactAnalysis.Core.Abstractions;

public interface IEmbeddingClient
{
    Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default);
}
