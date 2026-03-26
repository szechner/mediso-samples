using System.Reflection;
using System.Text.Json;
using Mediso.AiImpactAnalysis.Core.Abstractions;
using Mediso.AiImpactAnalysis.Core.Configuration;
using Mediso.AiImpactAnalysis.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Responses;

namespace Mediso.AiImpactAnalysis.Infrastructure.Services;

public sealed class OpenAiAnalysisService : IAnalysisService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiAnalysisService> _logger;

    public OpenAiAnalysisService(IOptions<OpenAiOptions> options, ILogger<OpenAiAnalysisService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ImpactAnalysisResult> AnalyzeAsync(
        TicketInput ticket,
        IReadOnlyList<RetrievedChunk> context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Analyze běží ve fallback režimu: OpenAI API klíč není nastaven.");
            return CreateFallbackResult(ticket, context, "OpenAI API klíč není nastaven.");
        }

        var prompt = BuildPrompt(ticket, context);

        var client = new OpenAIClient(_options.ApiKey);
        ResponsesClient responsesClient = client.GetResponsesClient();
        _logger.LogInformation("Volání OpenAI Responses API modelem {Model}. Kontext chunků: {Count}", _options.AnalysisModel, context.Count);
        
        var options = new CreateResponseOptions
        {
            Model = _options.AnalysisModel,
            Instructions = "Vrať pouze validní JSON bez markdownu. Vstupy i výstup drž v češtině."
        };

        options.InputItems.Add(ResponseItem.CreateUserMessageItem(prompt));

        var response = await responsesClient.CreateResponseAsync(options, cancellationToken);

        var rawText = ExtractText(response.Value);
        if (TryParseResult(rawText, out var result))
        {
            return result;
        }

        _logger.LogWarning("Analyze běží ve fallback režimu: model nevrátil validní JSON.");
        return CreateFallbackResult(ticket, context, rawText);
    }

    private static string BuildPrompt(TicketInput ticket, IReadOnlyList<RetrievedChunk> context)
    {
        var limitedContext = context.Take(6).ToList();
        var contextText = string.Join("\n\n", limitedContext.Select(chunk =>
            $"Soubor: {chunk.Chunk.FilePath}\nVrstva: {chunk.Chunk.Layer}\nDruh: {chunk.Chunk.Kind}\nSkóre: {chunk.Score:F3}\nVýřez:\n{Trim(chunk.Chunk.Content, 800)}"));

        return $"""
                Jsi architekt .NET backendu. Proveď dopadovou analýzu ticketu.

                Ticket:
                {ticket.Title}

                Popis ticketu:
                {ticket.Description}

                Relevantní kontext:
                {contextText}

                Používej pouze informace z kontextu. Nevymýšlej komponenty, které v kontextu nejsou.

                Vrať POUZE validní JSON bez markdownu, bez komentářů a bez vysvětlení.

                JSON musí obsahovat tato pole:
                - ticketSummary (string)
                - affectedModules (string[])
                - existingArtifacts (string[])
                - missingCapabilities (string[])
                - proposedChanges (string[])
                - risks (string[])
                - estimate (string)

                Vyplň VŠECHNA pole. Pokud něco není jisté, uveď nejlepší odhad.

                Pole "proposedChanges" je POVINNÉ a musí obsahovat konkrétní implementační kroky ve formátu:
                - [soubor] → [změna]
                
                Pole "risks" je POVINNÉ a musí obsahovat alespoň 2 konkrétní rizika nebo otevřené otázky.

                Zahrň:
                - kde přidat endpoint (soubor + metoda)
                - návrh DTO (request/response)
                - jak reuse existující validaci (např. AccountNumber.TryParse / validator)
                - kde implementovat IBAN formátování
                - jaké testy přidat

                Piš konkrétně, ne obecně.
                """;
    }

    private static bool TryParseResult(string rawText, out ImpactAnalysisResult result)
    {
        result = default!;

        if (string.IsNullOrWhiteSpace(rawText))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(rawText);
            var root = document.RootElement;

            result = new ImpactAnalysisResult(
                TicketSummary: root.GetProperty("ticketSummary").GetString() ?? string.Empty,
                AffectedModules: ReadArray(root, "affectedModules"),
                ExistingArtifacts: ReadArray(root, "existingArtifacts"),
                MissingCapabilities: ReadArray(root, "missingCapabilities"),
                ProposedChanges: ReadArray(root, "proposedChanges"),
                Risks: ReadArray(root, "risks"),
                Estimate: root.GetProperty("estimate").GetString() ?? string.Empty);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static ImpactAnalysisResult CreateFallbackResult(
        TicketInput ticket,
        IReadOnlyList<RetrievedChunk> context,
        string summary)
    {
        var modules = context.Select(c => c.Chunk.Module).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
        var artifacts = context.Take(6).Select(c => c.Chunk.FilePath).ToList();

        return new ImpactAnalysisResult(
            TicketSummary: $"{ticket.Title}: {Trim(summary, 280)}",
            AffectedModules: modules,
            ExistingArtifacts: artifacts,
            MissingCapabilities: ["Detailní JSON odpověď z modelu se nepodařilo spolehlivě načíst."],
            ProposedChanges: ["Doplnit veřejný endpoint validace účtu a navázat na stávající interní logiku."],
            Risks: ["Výsledek je fallback bez plně strukturované odpovědi modelu."],
            Estimate: "MVP odhad: 0.5-1.5 MD");
    }

    private static IReadOnlyList<string> ReadArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .OfType<string>()
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static string ExtractText(ResponseResult response)
    {
        foreach (var item in response.OutputItems)
        {
            if (item is not MessageResponseItem message)
            {
                continue;
            }

            foreach (var part in message.Content)
            {
                var textProperty = part.GetType().GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
                if (textProperty?.GetValue(part) is string text && !string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return JsonSerializer.Serialize(response, JsonOptions);
    }

    private static string Trim(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "...";
}
