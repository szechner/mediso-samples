namespace Mediso.AiImpactAnalysis.Core.Models;

public sealed record CodeChunk(
    string Id,
    string FilePath,
    string Project,
    string Module,
    string Layer,
    string Kind,
    string Content);
