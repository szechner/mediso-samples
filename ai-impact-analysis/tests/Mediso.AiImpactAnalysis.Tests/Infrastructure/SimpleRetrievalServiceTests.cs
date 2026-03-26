using Mediso.AiImpactAnalysis.Core.Abstractions;
using Mediso.AiImpactAnalysis.Core.Models;
using Mediso.AiImpactAnalysis.Infrastructure.Services;

namespace Mediso.AiImpactAnalysis.Tests.Infrastructure;

public sealed class SimpleRetrievalServiceTests
{
    [Fact]
    public async Task RetrieveAsync_ShouldRankBySimilarityAndMetadataBoost()
    {
        var fakeEmbedding = new FakeEmbeddingClient([1f, 0f, 0f]);
        var service = new SimpleRetrievalService(fakeEmbedding);

        var chunks = new List<CodeChunk>
        {
            new("a", "src/A.cs", "P", "M", "Application", "Controller", "class AccountController {}"),
            new("b", "src/B.cs", "P", "M", "Application", "Class", "class GenericClass {}")
        };

        var embeddings = new List<ChunkEmbedding>
        {
            new("a", [0.95f, 0.02f, 0.03f]),
            new("b", [0.90f, 0.08f, 0.02f])
        };

        var result = await service.RetrieveAsync("validace účtu", chunks, embeddings, top: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("a", result[0].Chunk.Id);
    }

    private sealed class FakeEmbeddingClient : IEmbeddingClient
    {
        private readonly IReadOnlyList<float> _vector;

        public FakeEmbeddingClient(IReadOnlyList<float> vector)
        {
            _vector = vector;
        }

        public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
            => Task.FromResult(_vector);
    }
}
