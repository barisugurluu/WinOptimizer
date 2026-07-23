using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WinOptimizer.Safety;
using WinOptimizer.Service;

// Servis kurulumu (master plan Bölüm 3.17 & Faz 7):
//   sc create WinOptimizerGuard binPath= "<yol>\WinOptimizer.Service.exe" start= auto
//   sc start WinOptimizerGuard
//
// LocalSystem olarak çalışır; otomatik başlangıç.

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<GuardThresholds>();
builder.Services.AddSingleton<MetricsCollector>();
builder.Services.AddSingleton<ThresholdEngine>();
builder.Services.AddSingleton<GuardState>();
builder.Services.AddSingleton<ProcessRunner>();
builder.Services.AddSingleton<RemediationEngine>();

builder.Services.AddSingleton<GuardIpcServer>(sp =>
{
    var state = sp.GetRequiredService<GuardState>();
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GuardIpcServer>>();
    return new GuardIpcServer(state.GetMetric, state.GetAlerts, logger);
});

builder.Services.AddHostedService<RealtimeGuardWorker>();

// Windows servisi olarak çalıştır (konsolda değilse otomatik servis moduna geçer).
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "WinOptimizerGuard";
});

var host = builder.Build();
await host.RunAsync();
