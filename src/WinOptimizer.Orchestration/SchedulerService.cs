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
            // ArgumentList: gün/saat/yol kullanıcıdan gelir; string birleştirmeyle komuta
            // gömülmez (CLAUDE.md §3 — komut enjeksiyonu kapalı).
            //
            // /ru SYSTEM + /np: görev, kullanıcı OTURUM AÇMAMIŞ olsa da çalışır. Bunlar
            // olmadan görev "yalnızca kullanıcı oturum açtığında" kipinde oluşuyordu ve
            // 03:00'teki haftalık bakım pratikte hiç çalışmıyordu.
            // (Bu, integrity.key'in LocalMachine kapsamına geçmesinden SONRA güvenlidir:
            //  aksi halde SYSTEM olarak çalışan görev anahtarı çözemez ve journal imzalarını
            //  geçersiz kılardı.)
            string[] args =
            [
                "/create", "/tn", TaskName,
                "/tr", $"\"{cliPath}\" optimize --yes",
                "/sc", "weekly", "/d", day, "/st", time,
                "/ru", "SYSTEM", "/np",
                "/rl", "highest", "/f",
            ];
            return RunSchtasks(args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Görev oluşturulamadı.");
            return false;
        }
    }

    /// <summary>Zamanlanmış görevi siler.</summary>
    public bool DeleteWeeklyTask() => RunSchtasks(["/delete", "/tn", TaskName, "/f"]);

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

    private bool RunSchtasks(string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var p = Process.Start(psi);
            if (p is null)
            {
                _logger.LogError("schtasks.exe başlatılamadı.");
                return false;
            }

            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            bool ok = p.ExitCode == 0;
            if (ok)
            {
                _logger.LogInformation("schtasks {Args}: başarılı", string.Join(' ', args));
            }
            else
            {
                // Sebep artık yutulmuyor: kullanıcı "oluşturuldu" yazısını görüp görevin
                // aslında oluşmadığını fark edemiyordu.
                _logger.LogError("schtasks {Args}: başarısız (exit {Code}){NewLine}{Out}{Err}",
                    string.Join(' ', args), p.ExitCode, Environment.NewLine, stdout.Trim(), stderr.Trim());
            }

            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "schtasks çalıştırılamadı.");
            return false;
        }
    }
}
