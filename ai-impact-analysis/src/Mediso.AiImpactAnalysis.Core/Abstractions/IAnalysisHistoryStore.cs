using Mediso.AiImpactAnalysis.Core.Models;

namespace Mediso.AiImpactAnalysis.Core.Abstractions;

public interface IAnalysisHistoryStore
{
    Task<string?> SaveAsync(AnalysisHistoryEntry entry, string dataPath, CancellationToken cancellationToken = default);
}
