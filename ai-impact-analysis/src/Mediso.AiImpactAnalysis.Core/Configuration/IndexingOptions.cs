namespace Mediso.AiImpactAnalysis.Core.Configuration;

public sealed class IndexingOptions
{
    public const string SectionName = "Indexing";

    public string RepositoryPath { get; set; } = ".";
    public string DataPath { get; set; } = "./data";
    public int MaxChunkLength { get; set; } = 4000;
}
