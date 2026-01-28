using Mediso.AuditSample.Domain.Services;
using Mediso.AuditSample.Infrastructure.Storage;

namespace Mediso.AuditSample.Api.Batching;

public sealed class AuditBatchingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _cfg;

    public AuditBatchingWorker(IServiceScopeFactory scopeFactory, IConfiguration cfg)
    {
        _scopeFactory = scopeFactory;
        _cfg = cfg;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _cfg.GetValue("Batching:IntervalSeconds", 30);
        var maxItems = _cfg.GetValue("Batching:MaxItems", 200);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IAuditBatchStore>();

                _ = await store.TryCreateNextBatchAsync(maxItems, stoppingToken);
            }
            catch
            {
                // logy + metrics
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }
}