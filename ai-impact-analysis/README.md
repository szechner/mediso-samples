# Mediso.AiImpactAnalysis

Minimalistický CLI sample pro indexaci lokálního repozitáře, retrieval nad chunky a analýzu českých ticketů přes OpenAI API.

## Struktura

- `src/Mediso.AiImpactAnalysis.Cli`
- `src/Mediso.AiImpactAnalysis.Core`
- `src/Mediso.AiImpactAnalysis.Infrastructure`
- `tests/Mediso.AiImpactAnalysis.Tests`
- `docs`
- `data`

## Požadavky

- `.NET 10 SDK`
- OpenAI API key v environment variable
- doporučená práce s `/data`:
  - `chunks.json` a `embeddings.json` jako lokální index
  - `analysis-history/` pro diagnostiku posledních analýz
  - `tickets/` pro testovací markdown tickety

## Konfigurace

Konfigurace se bere z `appsettings.json` + environment variables.

Příklad (PowerShell):

```powershell
$env:OpenAI__ApiKey = "sk-..."
$env:OpenAI__EmbeddingModel = "text-embedding-3-small"
$env:OpenAI__AnalysisModel = "gpt-5-mini"
$env:Indexing__RepositoryPath = "../mediaccountmanager-api"
$env:Indexing__DataPath = "./data"
```

## Sample commands

### index

```bash
dotnet run --project ./src/Mediso.AiImpactAnalysis.Cli -- index --repo ../mediaccountmanager-api --data ./data
```

### inspect

```bash
dotnet run --project ./src/Mediso.AiImpactAnalysis.Cli -- inspect --data ./data --query "validace čísla účtu" --top 10
dotnet run --project ./src/Mediso.AiImpactAnalysis.Cli -- inspect --data ./data --query "iban bban formátování účtu" --top 10
```

### analyze

```bash
dotnet run --project ./src/Mediso.AiImpactAnalysis.Cli -- analyze --data ./data --ticket-file ./data/tickets/ticket-validace-uctu.md
```

## Doporučený testovací scénář

1. spustit `index`
2. ověřit relevance přes `inspect`
3. spustit `analyze`
4. zkontrolovat JSON výstup

## Poznámky k rozpočtu

Sample je navržen úsporně:

- embeddingy se ukládají lokálně,
- retrieval běží lokálně,
- do OpenAI API se posílá jen omezený kontext.
