using System.Text.RegularExpressions;
using Mediso.AiImpactAnalysis.Core.Abstractions;
using Mediso.AiImpactAnalysis.Core.Models;

namespace Mediso.AiImpactAnalysis.Infrastructure.Services;

public sealed class SimpleChunkingService : IChunkingService
{
    private static readonly Regex TypeRegex = new(
        @"\b(class|record|interface|struct)\s+([A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);

    public IReadOnlyList<CodeChunk> CreateChunks(IReadOnlyList<string> files, string repositoryPath)
    {
        var chunks = new List<CodeChunk>(files.Count * 2);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(repositoryPath, file).Replace('\\', '/');
            var project = ResolveProject(relativePath);
            var module = ResolveModule(relativePath, project);
            var layer = ResolveLayer(relativePath, project);
            var kind = ResolveKind(file, relativePath, content);

            if (Path.GetExtension(file).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                var classChunks = CreateTypeChunks(content)
                    .Select(chunkContent => CreateChunk(relativePath, project, module, layer, kind, chunkContent))
                    .ToList();

                if (classChunks.Count > 0)
                {
                    chunks.AddRange(classChunks);
                    continue;
                }
            }

            chunks.Add(CreateChunk(relativePath, project, module, layer, kind, content));
        }

        return chunks;
    }

    private static CodeChunk CreateChunk(
        string filePath,
        string project,
        string module,
        string layer,
        string kind,
        string content)
    {
        return new CodeChunk(
            Id: Guid.NewGuid().ToString("N"),
            FilePath: filePath,
            Project: project,
            Module: module,
            Layer: layer,
            Kind: kind,
            Content: content.Trim());
    }

    private static IReadOnlyList<string> CreateTypeChunks(string content)
    {
        var matches = TypeRegex.Matches(content);
        if (matches.Count == 0)
        {
            return [];
        }

        var chunks = new List<string>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index;
            var nextStart = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;

            var chunk = ExtractTypeBlock(content, start, nextStart);
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }
        }

        return chunks;
    }

    private static string ExtractTypeBlock(string content, int start, int nextStart)
    {
        var firstBrace = content.IndexOf('{', start, nextStart - start);
        if (firstBrace < 0)
        {
            return content[start..nextStart].Trim();
        }

        var depth = 0;
        for (var i = firstBrace; i < content.Length; i++)
        {
            if (content[i] == '{') depth++;
            if (content[i] == '}') depth--;

            if (depth == 0)
            {
                return content[start..Math.Min(i + 1, content.Length)].Trim();
            }
        }

        return content[start..nextStart].Trim();
    }

    private static string ResolveLayer(string relativePath, string project)
    {
        if (relativePath.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
        {
            return "Tests";
        }

        if (relativePath.StartsWith("docs/", StringComparison.OrdinalIgnoreCase))
        {
            return "Docs";
        }

        if (project.Contains("Domain", StringComparison.OrdinalIgnoreCase))
        {
            return "Domain";
        }

        if (project.Contains("Applications", StringComparison.OrdinalIgnoreCase))
        {
            return "Application";
        }

        if (project.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase))
        {
            return "Infrastructure";
        }

        if (project.Contains("Http", StringComparison.OrdinalIgnoreCase))
        {
            return "Api";
        }

        if (project.Contains("Jobs", StringComparison.OrdinalIgnoreCase))
        {
            return "Worker";
        }

        if (project.Contains("DbMigrator", StringComparison.OrdinalIgnoreCase))
        {
            return "Migration";
        }

        if (project.Contains("Common", StringComparison.OrdinalIgnoreCase))
        {
            return "Common";
        }
        return "Unknown";
    }

    private static string ResolveKind(string filePath, string relativePath, string content)
    {
        var extension = Path.GetExtension(filePath);
        if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase)) return "Markdown";
        if (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)) return "ProjectFile";
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase)) return "Config";
        if (relativePath.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase)) return "Test";

        if (Regex.IsMatch(content, @"\bclass\s+\w*Endpoints\b")) return "Endpoints";
        if (Regex.IsMatch(content, @"\bclass\s+\w*Service\b")) return "Service";
        if (Regex.IsMatch(content, @"\bclass\s+\w*Handler\b")) return "Handler";
        if (Regex.IsMatch(content, @"\bclass\s+\w*Validator\b")) return "Validator";
        if (Regex.IsMatch(content, @"\brecord\s+")) return "Record";
        if (Regex.IsMatch(content, @"\bclass\s+\w*Middleware\b")) return "Middleware";
        if (Regex.IsMatch(content, @"\bclass\s+\w*DbContext\b")) return "DbContext";
        if (Regex.IsMatch(content, @"\bclass\s+\w*Command\b")) return "Command";
        if (Regex.IsMatch(content, @"\bclass\s+\w*Query\b")) return "Query";
        if (Regex.IsMatch(content, @"\bclass\s+\w*Result\b")) return "Result";
        if (Regex.IsMatch(content, @"\bclass\s+\w*Dto\b")) return "Dto";

        return "Class";
    }

    private static string ResolveProject(string relativePath)
    {
        var parts = relativePath.Split('/');
        if (parts.Length >= 2 && (parts[0].Equals("src", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("tests", StringComparison.OrdinalIgnoreCase)))
        {
            return parts[1];
        }

        return "Mediso.AiImpactAnalysis";
    }

    private static string ResolveModule(string relativePath, string project)
    {
        var parts = relativePath.Split('/');
        var projectIndex = Array.IndexOf(parts, project);
        if (projectIndex >= 0 && parts.Length > projectIndex + 1)
        {
            var candidate = parts[projectIndex + 1];
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return project;
    }
}
