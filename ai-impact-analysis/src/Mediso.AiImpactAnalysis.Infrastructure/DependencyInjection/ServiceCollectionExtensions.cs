using Mediso.AiImpactAnalysis.Core.Abstractions;
using Mediso.AiImpactAnalysis.Core.Configuration;
using Mediso.AiImpactAnalysis.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mediso.AiImpactAnalysis.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiImpactInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.Configure<IndexingOptions>(configuration.GetSection(IndexingOptions.SectionName));

        services.AddSingleton<IRepositoryFileLoader, RepositoryFileLoader>();
        services.AddSingleton<IChunkingService, SimpleChunkingService>();
        services.AddSingleton<IIndexStore, JsonIndexStore>();
        services.AddSingleton<IEmbeddingClient, OpenAiEmbeddingClient>();
        services.AddSingleton<IRetrievalService, SimpleRetrievalService>();
        services.AddSingleton<IAnalysisService, OpenAiAnalysisService>();
        services.AddSingleton<IAnalysisHistoryStore, JsonAnalysisHistoryStore>();

        return services;
    }
}
