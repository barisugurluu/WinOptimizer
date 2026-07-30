using System.Reflection;
using Serilog;
using Serilog.Events;

namespace WinOptimizer.Safety.Diagnostics;

/// <summary>
/// LoggingBootstrap — Serilog yapılandırılmış dosya günlüğünü kurar (master plan Bölüm 8.3/§19).
/// Rolling dosya: <c>%ProgramData%\WinOptimizer\logs\&lt;önek&gt;YYYYMMDD.log</c> (günlük rotasyon, 7 gün).
/// Zenginleştirme: uygulama sürümü + kaynak bağlamı her olaya eklenir (yapılandırılmış günlük).
/// Microsoft.Extensions.Logging'e Serilog köprüsü <c>AddSerilog</c> ile yapılır.
/// </summary>
/// <remarks>
/// <b>Neden Safety katmanında?</b> Üç süreç de (App, Cli, Service) aynı günlük klasörüne
/// yazmak zorunda. Safety, üçünün de referansladığı en alt katman olduğu için buraya taşındı —
/// yeni bir yukarı bağımlılık oluşmaz. Önceden yalnızca App'te olduğu için servis EventLog'a
/// yazıyordu ve teşhis paketi "servis çalışmıyor" şikâyetinde servise dair hiçbir şey
/// içermiyordu.
/// </remarks>
public static class LoggingBootstrap
{
    /// <summary>App süreci için dosya öneki.</summary>
    public const string AppPrefix = "app-";

    /// <summary>RealtimeGuard servisi için dosya öneki.</summary>
    public const string ServicePrefix = "service-";

    /// <summary>Komut satırı için dosya öneki.</summary>
    public const string CliPrefix = "cli-";

    /// <summary>
    /// Günlük dosyası kaydedicisi oluşturur.
    /// </summary>
    /// <param name="baseDir">Veri dizini (genelde <c>%ProgramData%\WinOptimizer</c>).</param>
    /// <param name="filePrefix">
    /// Dosya adı öneki — süreçler aynı klasörde ayrışsın diye zorunlu:
    /// <see cref="AppPrefix"/>, <see cref="ServicePrefix"/>, <see cref="CliPrefix"/>.
    /// </param>
    public static Serilog.ILogger CreateLogger(string baseDir, string filePrefix)
    {
        string logDir = Path.Combine(baseDir, "logs");
        Directory.CreateDirectory(logDir);
        string logPath = Path.Combine(logDir, filePrefix + ".log");

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
