using Mediso.AiImpactAnalysis.Core.Models;

namespace Mediso.AiImpactAnalysis.Core.Abstractions;

public interface IAnalysisService
{
    Task<ImpactAnalysisResult> AnalyzeAsync(
        TicketInput ticket,
        IReadOnlyList<RetrievedChunk> context,
        CancellationToken cancellationToken = default);
}
