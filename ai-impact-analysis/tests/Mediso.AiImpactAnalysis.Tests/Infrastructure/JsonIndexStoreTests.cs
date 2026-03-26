using Mediso.AiImpactAnalysis.Core.Models;
using Mediso.AiImpactAnalysis.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mediso.AiImpactAnalysis.Tests.Infrastructure;

public sealed class JsonIndexStoreTests
{
    [Fact]
    public async Task SaveAndLoad_ShouldRoundtripChunksAndEmbeddings()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ai-impact-analysis-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var store = new JsonIndexStore(new NullLogger<JsonIndexStore>());

            var chunks = new List<CodeChunk>
            {
                new("id-1", "src/Foo.cs", "Foo", "Foo", "Application", "Source", "class Foo {}")
            };

            var embeddings = new List<ChunkEmbedding>
            {
                new("id-1", [0.1f, 0.2f, 0.3f])
            };

            await store.SaveChunksAsync(tempRoot, chunks);
            await store.SaveEmbeddingsAsync(tempRoot, embeddings);

            var loadedChunks = await store.LoadChunksAsync(tempRoot);
            var loadedEmbeddings = await store.LoadEmbeddingsAsync(tempRoot);

            Assert.Single(loadedChunks);
            Assert.Single(loadedEmbeddings);
            Assert.Equal("id-1", loadedEmbeddings[0].ChunkId);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
