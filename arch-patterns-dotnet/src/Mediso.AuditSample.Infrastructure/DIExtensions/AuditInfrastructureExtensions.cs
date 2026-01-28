using Mediso.AuditSample.Domain.Services;
using Mediso.AuditSample.Infrastructure.Anchoring;
using Mediso.AuditSample.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Mediso.AuditSample.Infrastructure.DIExtensions;

public static class AuditInfrastructureExtensions
{
    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddSingleton<NpgsqlDataSource>(_ =>
            NpgsqlDataSource.Create(cfg.GetConnectionString("AuditDb")!));

        services.AddSingleton<IAuditRecordStore, PgAuditRecordStore>();
        services.AddSingleton<IAuditBatchStore, PgAuditBatchStore>();
        services.AddSingleton<IAnchorProvider, SolanaMemoAnchorProvider>();
        services.AddSingleton<IAuditAnchorStore, PgAuditAnchorStore>();

        services.AddSingleton<Solnet.Rpc.IRpcClient>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var url = cfg["Solana:RpcUrl"] ?? "https://api.mainnet-beta.solana.com";
            return Solnet.Rpc.ClientFactory.GetClient(url);
        });

        services.AddSingleton<Mediso.AuditSample.Infrastructure.Solana.SolanaTxVerifier>();
        
        return services;
    }
}