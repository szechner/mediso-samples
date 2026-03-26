using Mediso.AiImpactAnalysis.Core.Abstractions;

namespace Mediso.AiImpactAnalysis.Infrastructure.Services;

public sealed class RepositoryFileLoader : IRepositoryFileLoader
{
    private static readonly string[] AllowedExtensions = [".cs", ".csproj", ".md", ".json"];
    private static readonly string[] ExcludedFolders = [".git", "bin", "obj", "node_modules", ".vs", ".idea", ".vscode"];

    public Task<IReadOnlyList<string>> LoadFilesAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(repositoryPath))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var files = Directory.EnumerateFiles(repositoryPath, "*.*", SearchOption.AllDirectories)
            .Where(path => AllowedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Where(IsRelevantJsonOrNonJson)
            .Where(path => !IsInsideExcludedDirectory(path))
            .Take(2_000)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    private static bool IsInsideExcludedDirectory(string path)
    {
        var normalized = path.Replace('\\', '/');
        return ExcludedFolders.Any(folder => normalized.Contains($"/{folder}/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRelevantJsonOrNonJson(string path)
    {
        if (!Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName = Path.GetFileName(path);
        if (fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/docs/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/tickets/", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("config", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("settings", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("swagger", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("openapi", StringComparison.OrdinalIgnoreCase);
    }
}
