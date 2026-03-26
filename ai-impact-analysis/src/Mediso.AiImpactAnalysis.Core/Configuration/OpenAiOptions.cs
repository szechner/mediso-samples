namespace Mediso.AiImpactAnalysis.Core.Configuration;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public string AnalysisModel { get; set; } = "gpt-5-mini";
}
