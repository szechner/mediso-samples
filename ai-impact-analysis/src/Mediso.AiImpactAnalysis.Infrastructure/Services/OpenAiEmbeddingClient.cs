using Mediso.AiImpactAnalysis.Core.Abstractions;
using Mediso.AiImpactAnalysis.Core.Configuration;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;

namespace Mediso.AiImpactAnalysis.Infrastructure.Services;

public sealed class OpenAiEmbeddingClient : IEmbeddingClient
{
    private readonly OpenAiOptions _options;

    public OpenAiEmbeddingClient(IOptions<OpenAiOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return CreateDeterministicEmbedding(input);
        }

        var client = new OpenAIClient(_options.ApiKey);
        EmbeddingClient embeddingClient = client.GetEmbeddingClient(_options.EmbeddingModel);
        var response = await embeddingClient.GenerateEmbeddingAsync(input, cancellationToken: cancellationToken);
        return response.Value.ToFloats().ToArray();
    }

    private static IReadOnlyList<float> CreateDeterministicEmbedding(string input)
    {
        Span<float> vector = stackalloc float[16];
        for (var i = 0; i < input.Length; i++)
        {
            var index = i % vector.Length;
            vector[index] += input[i] / 1024f;
        }

        return vector.ToArray();
    }
}
