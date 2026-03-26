namespace Mediso.AiImpactAnalysis.Core.Models;

public sealed record AnalysisHistoryEntry(
    DateTimeOffset TimestampUtc,
    string TicketSource,
    string TicketText,
    IReadOnlyList<RetrievedChunk> RetrievedChunks,
    ImpactAnalysisResult Result);
