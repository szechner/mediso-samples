using Mediso.AuditSample.Domain.Services;
using Mediso.AuditSample.Infrastructure.Anchoring;
using Mediso.AuditSample.Infrastructure.Storage;

namespace Mediso.AuditSample.Api.Anchoring;

public sealed class AuditAnchoringWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _cfg;

    public AuditAnchoringWorker(IServiceScopeFactory scopeFactory, IConfiguration cfg)
    {
        _scopeFactory = scopeFactory;
        _cfg = cfg;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _cfg.GetValue("Anchoring:IntervalSeconds", 20);
        var chain = "solana";
        var network = _cfg["Solana:Network"] ?? "mainnet-beta";

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IAuditBatchStore>();
            var anchor = scope.ServiceProvider.GetRequiredService<IAnchorProvider>();

            var pending = await store.GetNextPendingAnchorAsync(stoppingToken);
            if (pending is not null)
            {
                var memo = AuditMemoFormatter.FormatV1(pending.BatchId, pending.MerkleRootSha256);
                try
                {
                    var sig = await anchor.AnchorMemoAsync(memo, stoppingToken);
                    await store.MarkAnchoredAsync(pending.BatchId, chain, network, sig, stoppingToken);
                }
                catch
                {
                    await store.MarkAnchorFailedAsync(pending.BatchId, "anchor failed", stoppingToken);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    

}