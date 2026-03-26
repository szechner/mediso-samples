namespace Mediso.AiImpactAnalysis.Core.Models;

public sealed record ImpactAnalysisResult(
    string TicketSummary,
    IReadOnlyList<string> AffectedModules,
    IReadOnlyList<string> ExistingArtifacts,
    IReadOnlyList<string> MissingCapabilities,
    IReadOnlyList<string> ProposedChanges,
    IReadOnlyList<string> Risks,
    string Estimate);
