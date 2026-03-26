using Mediso.AiImpactAnalysis.Core.Models;

namespace Mediso.AiImpactAnalysis.Core.Abstractions;

public interface IChunkingService
{
    IReadOnlyList<CodeChunk> CreateChunks(IReadOnlyList<string> files, string repositoryPath);
}
