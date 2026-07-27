using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Safety;

/// <summary>
/// Dış komutları (sfc, Dism, powercfg, pnputil, defrag vb.) admin bağlamında
/// çalıştırır ve stdout'u akış olarak raporlar (master plan Bölüm 11.3).
/// Güvenlik (§17.5): birincil imza <see cref="RunAsync(string, IReadOnlyCollection{string}, ...)"/>
/// argümanları <c>ArgumentList</c> ile dizi olarak alır — kullanıcı/kullanıcı-verisi
/// kaynaklı değerler asla string birleştirmeyle komuta dönüştürülmez (komut enjeksiyonu kapalı).
/// Eski string tabanlı aşırı yükleme yalnızca sabit/sabit-metin argümanları içindir.
/// </summary>
public sealed class ProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner> logger) => _logger = logger;

    /// <summary>
    /// Komutu güvenli biçimde çalıştırır: argümanlar ayrı liste öğeleri olarak
    /// <see cref="ProcessStartInfo.ArgumentList"/> üzerinden verilir (§17.5).
    /// </summary>
    public async Task<int> RunAsync(
        string file,
        IReadOnlyCollection<string> args,
        IProgress<string>? output = null,
        CancellationToken ct = default)
    {
        var psi = BuildStartInfo(file);
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        return await ExecuteAsync(psi, file, args, output, ct);
    }

    /// <summary>
    /// Komutu çalıştırır ve tüm stdout/stderr'i tek metin olarak döndürür (güvenli imza).
    /// </summary>
    public async Task<(int ExitCode, string Output)> RunCaptureAsync(
        string file, IReadOnlyCollection<string> args, CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder();
        var progress = new Progress<string>(line => sb.AppendLine(line));
        int code = await RunAsync(file, args, progress, ct);
        return (code, sb.ToString());
    }

    /// <summary>
    /// [Uyumluluk] Tek sabit-metin argüman dizisiyle çalıştırır. Yalnızca statik
    /// argümanlar için; dinamik/kullanıcı verisi için liste aşırı yüklemesini kullanın.
    /// </summary>
    public Task<int> RunAsync(
        string file, string args, IProgress<string>? output = null, CancellationToken ct = default) =>
        RunAsync(file, SplitToArgs(file, args), output, ct);

    /// <summary>[Uyumluluk] Sabit-metin argümanıyla yakala.</summary>
    public Task<(int ExitCode, string Output)> RunCaptureAsync(
        string file, string args, CancellationToken ct = default) =>
        RunCaptureAsync(file, SplitToArgs(file, args), ct);

    private static ProcessStartInfo BuildStartInfo(string file) => new(file)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = System.Text.Encoding.UTF8
    };

    private async Task<int> ExecuteAsync(
        ProcessStartInfo psi, string file, IReadOnlyCollection<string> args,
        IProgress<string>? output, CancellationToken ct)
    {
        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) output?.Report(e.Data);
        };
        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) output?.Report("[ERR] " + e.Data);
        };

        _logger.LogInformation("Komut çalıştırılıyor: {File} {Args}", file, string.Join(' ', args));
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        await p.WaitForExitAsync(ct);
        _logger.LogDebug("Komut bitti (exit {Code}): {File}", p.ExitCode, file);
        return p.ExitCode;
    }

    /// <summary>
    /// Eski string argüman imzası için güvenli belirteçleştirme. <paramref name="file"/>
    /// <c>cmd.exe</c>/<c>powershell.exe</c> ise metin tek bir argüman olarak korunur
    /// (kabuk tek argüman bekler); aksi halde boşluklardan bölünür.
    /// </summary>
    internal static string[] SplitToArgs(string file, string args)
    {
        string name = Path.GetFileName(file);
        if (name.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { args };
        }

        return args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
