using Mediso.AiImpactAnalysis.Cli.Runtime;
using Mediso.AiImpactAnalysis.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.AddAiImpactInfrastructure(builder.Configuration);
builder.Services.AddSingleton<CliRunner>();

using var host = builder.Build();
var runner = host.Services.GetRequiredService<CliRunner>();
var exitCode = await runner.RunAsync(args, CancellationToken.None);

Environment.ExitCode = exitCode;
