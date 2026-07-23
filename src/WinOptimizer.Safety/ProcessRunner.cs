using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Safety;

/// <summary>
/// Dış komutları (sfc, Dism, powercfg, pnputil, defrag vb.) admin bağlamında
/// çalıştırır ve stdout'u akış olarak raporlar (master plan Bölüm 11.3).
/// </summary>
public sealed class ProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner> logger) => _logger = logger;

    /// <summary>
    /// Komutu çalıştırır, stdout/stderr'i <paramref name="output"/> üzerinden raporlar.
    /// </summary>
    /// <returns>İşlem çıkış kodu.</returns>
    public async Task<int> RunAsync(
        string file,
        string args,
        IProgress<string>? output = null,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        };

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) output?.Report(e.Data);
        };
        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) output?.Report("[ERR] " + e.Data);
        };

        _logger.LogInformation("Komut çalıştırılıyor: {File} {Args}", file, args);
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        await p.WaitForExitAsync(ct);
        _logger.LogDebug("Komut bitti (exit {Code}): {File}", p.ExitCode, file);
        return p.ExitCode;
    }

    /// <summary>Komutu çalıştırır ve tüm çıktıyı tek bir metin olarak döndürür.</summary>
    public async Task<(int ExitCode, string Output)> RunCaptureAsync(
        string file, string args, CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder();
        var progress = new Progress<string>(line => sb.AppendLine(line));
        int code = await RunAsync(file, args, progress, ct);
        return (code, sb.ToString());
    }
}

// Kullanım örnekleri (master plan Bölüm 11.3):
//   await RunAsync("sfc", "/scannow", progress);
//   await RunAsync("Dism.exe", "/Online /Cleanup-Image /RestoreHealth", progress);
//   await RunAsync("powercfg", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61", progress);
