using System.IO;
using System.Reflection;
using Serilog;
using Serilog.Events;

namespace WinOptimizer.App.Infrastructure;

/// <summary>
/// LoggingBootstrap — Serilog yapılandırılmış dosya günlüğünü kurar (master plan Bölüm 8.3/§19).
/// Rolling dosya: %ProgramData%\WinOptimizer\logs\app-YYYYMMDD.log (günlük rotasyon, 7 gün).
/// Zenginleştirme: uygulama sürümü + kaynak bağlamı her olaya eklenir (yapılandırılmış günlük).
/// Microsoft.Extensions.Logging'e Serilog köprüsü <c>AddSerilog</c> ile yapılır.
/// </summary>
public static class LoggingBootstrap
{
    public static Serilog.ILogger CreateLogger(string baseDir)
    {
        string logDir = Path.Combine(baseDir, "logs");
        Directory.CreateDirectory(logDir);
        string logPath = Path.Combine(logDir, "app-.log");

        string appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("App", "WinOptimizer")
            .Enrich.WithProperty("AppVersion", appVersion)
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                shared: true)
            .CreateLogger();
    }

    public static LogEventLevel ParseLevel(string? value) => value?.ToLowerInvariant() switch
    {
        "trace" => LogEventLevel.Verbose,
        "debug" => LogEventLevel.Debug,
        "info" => LogEventLevel.Information,
        "warn" or "warning" => LogEventLevel.Warning,
        "error" => LogEventLevel.Error,
        _ => LogEventLevel.Debug
    };
}
