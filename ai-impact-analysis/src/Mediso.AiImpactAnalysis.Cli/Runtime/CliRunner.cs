using System.Diagnostics;
using System.Text.Json;
using Mediso.AiImpactAnalysis.Core.Abstractions;
using Mediso.AiImpactAnalysis.Core.Configuration;
using Mediso.AiImpactAnalysis.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mediso.AiImpactAnalysis.Cli.Runtime;

public sealed class CliRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IRepositoryFileLoader _fileLoader;
    private readonly IChunkingService _chunkingService;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IIndexStore _indexStore;
    private readonly IRetrievalService _retrievalService;
    private readonly IAnalysisService _analysisService;
    private readonly IAnalysisHistoryStore _analysisHistoryStore;
    private readonly IndexingOptions _indexingOptions;
    private readonly OpenAiOptions _openAiOptions;
    private readonly ILogger<CliRunner> _logger;

    public CliRunner(
        IRepositoryFileLoader fileLoader,
        IChunkingService chunkingService,
        IEmbeddingClient embeddingClient,
        IIndexStore indexStore,
        IRetrievalService retrievalService,
        IAnalysisService analysisService,
        IAnalysisHistoryStore analysisHistoryStore,
        IOptions<IndexingOptions> indexingOptions,
        IOptions<OpenAiOptions> openAiOptions,
        ILogger<CliRunner> logger)
    {
        _fileLoader = fileLoader;
        _chunkingService = chunkingService;
        _embeddingClient = embeddingClient;
        _indexStore = indexStore;
        _retrievalService = retrievalService;
        _analysisService = analysisService;
        _analysisHistoryStore = analysisHistoryStore;
        _indexingOptions = indexingOptions.Value;
        _openAiOptions = openAiOptions.Value;
        _logger = logger;
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 1;
        }

        var command = args[0].ToLowerInvariant();
        var options = ParseOptions(args.Skip(1).ToArray());

        return command switch
        {
            "index" => await RunIndexAsync(options, cancellationToken),
            "inspect" => await RunInspectAsync(options, cancellationToken),
            "analyze" => await RunAnalyzeAsync(options, cancellationToken),
            _ => UnknownCommand(command)
        };
    }

    private async Task<int> RunIndexAsync(IReadOnlyDictionary<string, string> options, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var repositoryPath = GetOption(options, "repo", _indexingOptions.RepositoryPath);
        var dataPath = GetOption(options, "data", _indexingOptions.DataPath);
        var chunksPath = Path.Combine(dataPath, "chunks.json");
        var embeddingsPath = Path.Combine(dataPath, "embeddings.json");

        _logger.LogInformation("Start indexace. Repo: {RepoPath}, Data: {DataPath}", repositoryPath, dataPath);

        var files = await _fileLoader.LoadFilesAsync(repositoryPath, cancellationToken);
        var extensionStats = files
            .GroupBy(path => Path.GetExtension(path).ToLowerInvariant())
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}:{group.Count()}")
            .ToArray();
        _logger.LogInformation("Nalezeno {Count} souborů. Typy: {Types}", files.Count, string.Join(", ", extensionStats));

        var chunks = _chunkingService.CreateChunks(files, repositoryPath);
        _logger.LogInformation("Vytvořeno {Count} chunků.", chunks.Count);

        var embeddings = new List<ChunkEmbedding>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var vector = await _embeddingClient.GenerateEmbeddingAsync(chunk.Content, cancellationToken);
            embeddings.Add(new ChunkEmbedding(chunk.Id, vector));
        }

        _logger.LogInformation("Vytvořeno {CreatedCount} embeddingů, načteno {LoadedCount}.", embeddings.Count, 0);

        await _indexStore.SaveChunksAsync(dataPath, chunks, cancellationToken);
        await _indexStore.SaveEmbeddingsAsync(dataPath, embeddings, cancellationToken);

        stopwatch.Stop();
        _logger.LogInformation("Indexace dokončena za {ElapsedMs} ms. chunks.json: {ChunksPath}, embeddings.json: {EmbeddingsPath}",
            stopwatch.ElapsedMilliseconds, chunksPath, embeddingsPath);

        Console.WriteLine($"Indexace dokončena. Soubory: {files.Count}, chunky: {chunks.Count}, embeddings: {embeddings.Count}.");
        return 0;
    }

    private async Task<int> RunInspectAsync(IReadOnlyDictionary<string, string> options, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var dataPath = GetOption(options, "data", _indexingOptions.DataPath);
        var top = int.TryParse(GetOption(options, "top", "10"), out var parsedTop) ? parsedTop : 10;
        var query = GetOption(options, "query", string.Empty);

        _logger.LogInformation("Start inspect. Dotaz: \"{Query}\", top: {Top}, data: {DataPath}", query, top, dataPath);

        var chunks = await _indexStore.LoadChunksAsync(dataPath, cancellationToken);
        var embeddings = await _indexStore.LoadEmbeddingsAsync(dataPath, cancellationToken);
        _logger.LogInformation("Inspect kandidáti: chunky {ChunkCount}, embeddingy {EmbeddingCount}", chunks.Count, embeddings.Count);

        if (chunks.Count == 0)
        {
            _logger.LogWarning("Inspect nelze provést: index je prázdný.");
            Console.WriteLine("Index je prázdný. Spusť nejdřív příkaz index.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            var sample = chunks.Take(top).Select(chunk => new
            {
                score = 0.0,
                filePath = chunk.FilePath,
                layer = chunk.Layer,
                kind = chunk.Kind,
                snippet = CreateSnippet(chunk.Content)
            });

            Console.WriteLine(JsonSerializer.Serialize(sample, JsonOptions));
            stopwatch.Stop();
            _logger.LogInformation("Inspect dokončen bez dotazu za {ElapsedMs} ms.", stopwatch.ElapsedMilliseconds);
            return 0;
        }

        var results = await _retrievalService.RetrieveAsync(query, chunks, embeddings, top, cancellationToken);
        foreach (var item in results.Take(Math.Min(top, 10)))
        {
            _logger.LogInformation("Inspect výběr: {Score:F4} | {FilePath} | {Layer} | {Kind}", item.Score, item.Chunk.FilePath, item.Chunk.Layer, item.Chunk.Kind);
        }

        var output = results.Select(item => new
        {
            score = Math.Round(item.Score, 4),
            filePath = item.Chunk.FilePath,
            layer = item.Chunk.Layer,
            kind = item.Chunk.Kind,
            snippet = CreateSnippet(item.Chunk.Content)
        });

        Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
        stopwatch.Stop();
        _logger.LogInformation("Inspect dokončen za {ElapsedMs} ms.", stopwatch.ElapsedMilliseconds);
        return 0;
    }

    private async Task<int> RunAnalyzeAsync(IReadOnlyDictionary<string, string> options, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var dataPath = GetOption(options, "data", _indexingOptions.DataPath);
        var ticketFile = GetOption(options, "ticket-file", string.Empty);
        var ticketText = GetOption(options, "ticket", string.Empty);
        var top = int.TryParse(GetOption(options, "top", "6"), out var parsedTop) ? parsedTop : 6;
        var isTicketFromFile = false;

        if (string.IsNullOrWhiteSpace(ticketFile) && !string.IsNullOrWhiteSpace(ticketText) && File.Exists(ticketText))
        {
            ticketFile = ticketText;
        }

        if (!string.IsNullOrWhiteSpace(ticketFile) && File.Exists(ticketFile))
        {
            ticketText = await File.ReadAllTextAsync(ticketFile, cancellationToken);
            isTicketFromFile = true;
        }

        if (string.IsNullOrWhiteSpace(ticketText))
        {
            _logger.LogError("Analyze nelze provést: chybí ticket text i ticket-file.");
            Console.WriteLine("Chybí ticket. Použij --ticket nebo --ticket-file.");
            return 1;
        }

        _logger.LogInformation("Start analyze. Zdroj ticketu: {Source}, délka: {Length}, top: {Top}, data: {DataPath}",
            isTicketFromFile ? $"soubor {ticketFile}" : "textový argument",
            ticketText.Length,
            top,
            dataPath);

        var ticket = new TicketInput("Analýza ticketu", ticketText.Trim());
        var chunks = await _indexStore.LoadChunksAsync(dataPath, cancellationToken);
        var embeddings = await _indexStore.LoadEmbeddingsAsync(dataPath, cancellationToken);
        _logger.LogInformation("Analyze kandidáti: chunky {ChunkCount}, embeddingy {EmbeddingCount}", chunks.Count, embeddings.Count);

        var retrievalTop = Math.Max(top * 3, 15);
        var retrieved = await _retrievalService.RetrieveAsync(ticket.Description, chunks, embeddings, retrievalTop, cancellationToken);

        var filtered = retrieved
            .Where(c => c.Chunk.Layer != "Docs")
            .ToList();

        var api = filtered
            .Where(c => c.Chunk.Layer == "Api")
            .OrderByDescending(c => c.Score)
            .Take(2);

        var domain = filtered
            .Where(c => c.Chunk.Layer == "Domain")
            .OrderByDescending(c => c.Score)
            .Take(2);

        var application = filtered
            .Where(c => c.Chunk.Layer == "Application")
            .OrderByDescending(c => c.Score)
            .Take(2);

        var selectedContext = api
            .Concat(domain)
            .Concat(application)
            .ToList();

        if (selectedContext.Count < top)
        {
            var remaining = filtered
                .Where(c => !selectedContext.Any(x => x.Chunk.Id == c.Chunk.Id))
                .OrderByDescending(c => c.Score)
                .Take(top - selectedContext.Count);

            selectedContext = selectedContext.Concat(remaining).ToList();
        }

        foreach (var item in selectedContext)
        {
            _logger.LogInformation(
                "Analyze výběr: {Score:F4} | {FilePath} | {Layer} | {Kind}",
                item.Score,
                item.Chunk.FilePath,
                item.Chunk.Layer,
                item.Chunk.Kind);
        }

        _logger.LogInformation(
            "Počet chunků odeslaných do modelu: {Count}. Analysis model: {Model}",
            selectedContext.Count,
            _openAiOptions.AnalysisModel);

        var analysis = await _analysisService.AnalyzeAsync(ticket, selectedContext, cancellationToken);
        
        if (analysis.TicketSummary.Contains("fallback", StringComparison.OrdinalIgnoreCase)
            || analysis.TicketSummary.Contains("OpenAI API klíč není nastaven", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Analyze dokončen ve fallback režimu.");
        }

        var json = JsonSerializer.Serialize(analysis, JsonOptions);
        Console.WriteLine(json);
        PrintHumanReadable(analysis);

        var historyEntry = new AnalysisHistoryEntry(
            TimestampUtc: DateTimeOffset.UtcNow,
            TicketSource: isTicketFromFile ? (ticketFile ?? string.Empty) : "inline",
            TicketText: ticketText,
            RetrievedChunks: retrieved,
            Result: analysis);

        var historyPath = await _analysisHistoryStore.SaveAsync(historyEntry, dataPath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(historyPath))
        {
            _logger.LogInformation("Analysis-history uloženo do {Path}", historyPath);
        }

        stopwatch.Stop();
        _logger.LogInformation("Analyze dokončen za {ElapsedMs} ms.", stopwatch.ElapsedMilliseconds);
        return 0;
    }
    
    private static string CleanItem(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        return s.TrimStart().StartsWith("- ")
            ? s.TrimStart().Substring(2)
            : s;
    }
    
    private static string HighlightPath(string s)
    {
        var idx = s.IndexOf("src/", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) idx = s.IndexOf("docs/", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return s;

        var before = s.Substring(0, idx);
        var path = s.Substring(idx);
        return $"{before}\x1b[36m{path}\x1b[0m"; // cyan (funguje v většině terminálů)
    }

    private static void PrintList(string title, IReadOnlyList<string> items)
    {
        Console.WriteLine($"\n{title}:");

        if (items == null || items.Count == 0)
        {
            Console.WriteLine("  - (žádné)");
            return;
        }

        foreach (var item in items)
        {
            Console.WriteLine($"  - {HighlightPath(CleanItem(item))}");
        }
    }
    
    private static void PrintHumanReadable(ImpactAnalysisResult result)
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("🧠 AI DOPADOVÁ ANALÝZA");
        Console.WriteLine("========================================");

        Console.WriteLine("\n📌 Shrnutí:");
        Console.WriteLine(result.TicketSummary);

        PrintList("📦 Dotčené moduly", result.AffectedModules);
        PrintList("🔎 Existující artefakty", result.ExistingArtifacts);
        PrintList("❌ Chybějící části", result.MissingCapabilities);
        PrintList("🛠 Navrhované změny", result.ProposedChanges);
        PrintList("⚠️ Rizika", result.Risks);

        Console.WriteLine("\n⏱ Odhad:");
        Console.WriteLine(result.Estimate);

        Console.WriteLine("\n========================================\n");
    }

    private static string GetOption(IReadOnlyDictionary<string, string> options, string key, string fallback)
        => options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var current = args[i];
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = current[2..];
            var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";

            options[key] = value;
        }

        return options;
    }

    private static int UnknownCommand(string command)
    {
        Console.WriteLine($"Neznámý příkaz '{command}'. Použij index | inspect | analyze.");
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Použití:");
        Console.WriteLine("  dotnet run --project ./src/Mediso.AiImpactAnalysis.Cli -- index --repo ../MediAccountManager --data ./data");
        Console.WriteLine("  dotnet run --project ./src/Mediso.AiImpactAnalysis.Cli -- inspect --data ./data --top 10 --query \"validace účtu\"");
        Console.WriteLine("  dotnet run --project ./src/Mediso.AiImpactAnalysis.Cli -- analyze --data ./data --ticket-file ./data/tickets/ticket-validace-uctu.md --top 6");
    }

    private static string CreateSnippet(string content)
    {
        var singleLine = content.Replace("\r", " ").Replace("\n", " ").Trim();
        return singleLine.Length <= 180 ? singleLine : singleLine[..180] + "...";
    }
}
