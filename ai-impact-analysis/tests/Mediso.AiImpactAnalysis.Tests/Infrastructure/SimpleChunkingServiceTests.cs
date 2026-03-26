using Mediso.AiImpactAnalysis.Infrastructure.Services;

namespace Mediso.AiImpactAnalysis.Tests.Infrastructure;

public sealed class SimpleChunkingServiceTests
{
    [Fact]
    public void CreateChunks_ShouldSplitCSharpFileByTypes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ai-impact-analysis-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var src = Path.Combine(tempRoot, "src", "Mediso.AiImpactAnalysis.Core");
            Directory.CreateDirectory(src);
            var file = Path.Combine(src, "TicketInput.cs");
            File.WriteAllText(file, """
public sealed class FirstService
{
    public string A() => "a";
}

public sealed class SecondHandler
{
    public string B() => "b";
}
""");

            var service = new SimpleChunkingService();
            var chunks = service.CreateChunks([file], tempRoot);

            Assert.Equal(2, chunks.Count);
            Assert.All(chunks, chunk => Assert.Equal("Domain", chunk.Layer));
            Assert.Contains(chunks, chunk => chunk.Content.Contains("FirstService", StringComparison.Ordinal));
            Assert.Contains(chunks, chunk => chunk.Content.Contains("SecondHandler", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void CreateChunks_ShouldExtractMetadataForProjectAndModule()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ai-impact-analysis-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var serviceDir = Path.Combine(tempRoot, "src", "Mediso.AiImpactAnalysis.Infrastructure", "Services");
            Directory.CreateDirectory(serviceDir);
            var file = Path.Combine(serviceDir, "AccountValidator.cs");
            File.WriteAllText(file, "public sealed class AccountValidator { }");

            var service = new SimpleChunkingService();
            var chunks = service.CreateChunks([file], tempRoot);

            var chunk = Assert.Single(chunks);
            Assert.Equal("Mediso.AiImpactAnalysis.Infrastructure", chunk.Project);
            Assert.Equal("Services", chunk.Module);
            Assert.Equal("Infrastructure", chunk.Layer);
            Assert.Equal("Validator", chunk.Kind);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
