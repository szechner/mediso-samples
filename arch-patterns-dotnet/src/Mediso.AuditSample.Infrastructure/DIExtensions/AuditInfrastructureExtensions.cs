using Mediso.AuditSample.Infrastructure.Anchoring;
using Mediso.AuditSample.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Mediso.AuditSample.Infrastructure;

public static class AuditInfrastructureExtensions
{
    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddSingleton<NpgsqlDataSource>(_ =>
            NpgsqlDataSource.Create(cfg.GetConnectionString("AuditDb")!));

        services.AddScoped<IAuditRecordStore, PgAuditRecordStore>();
        services.AddScoped<IAuditBatchStore, PgAuditBatchStore>();
        services.AddSingleton<IAnchorProvider, SolanaMemoAnchorProvider>();
        
        return services;
    }
}