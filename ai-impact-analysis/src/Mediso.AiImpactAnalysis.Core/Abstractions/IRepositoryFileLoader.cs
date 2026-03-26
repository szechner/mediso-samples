namespace Mediso.AiImpactAnalysis.Core.Abstractions;

public interface IRepositoryFileLoader
{
    Task<IReadOnlyList<string>> LoadFilesAsync(string repositoryPath, CancellationToken cancellationToken = default);
}
