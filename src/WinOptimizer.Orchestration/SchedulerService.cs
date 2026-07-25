using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Orchestration;

/// <summary>
/// SchedulerService — Windows Görev Zamanlayıcı entegrasyonu (master plan Faz 8).
/// Haftalık otomatik bakım görevi oluşturur/siler. schtasks komutunu kullanır.
/// </summary>
public sealed class SchedulerService
{
    private const string TaskName = "WinOptimizerWeekly";
    private readonly ILogger<SchedulerService> _logger;

    public SchedulerService(ILogger<SchedulerService> logger) => _logger = logger;

    /// <summary>Haftalık bakım görevini oluşturur (CLI aracılığıyla).</summary>
    /// <param name="day">Gün adı (ör. "Sunday").</param>
    /// <param name="time">Saat (ör. "03:00").</param>
    /// <param name="cliPath">WinOptimizer.Cli.exe tam yolu.</param>
    public bool CreateWeeklyTask(string day, string time, string cliPath)
    {
        try
        {
            // schtasks /create /tn WinOptimizerWeekly /tr "..." /sc weekly /d Sunday /st 03:00 /rl highest /f
            var args = $"/create /tn {TaskName} /tr \"\\\"{cliPath}\\\" optimize --yes\" " +
                       $"/sc weekly /d {day} /st {time} /rl highest /f";
            return RunSchtasks(args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Görev oluşturulamadı.");
            return false;
        }
    }

    /// <summary>Zamanlanmış görevi siler.</summary>
    public bool DeleteWeeklyTask() => RunSchtasks($"/delete /tn {TaskName} /f");

    /// <summary>Görevin var olup olmadığını kontrol eder.</summary>
    public bool IsTaskExists()
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", $"/query /tn {TaskName}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    private bool RunSchtasks(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
            bool ok = p?.ExitCode == 0;
            _logger.LogInformation("schtasks {Args}: {Result}", args, ok ? "başarılı" : "başarısız");
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "schtasks çalıştırılamadı.");
            return false;
        }
    }
}
