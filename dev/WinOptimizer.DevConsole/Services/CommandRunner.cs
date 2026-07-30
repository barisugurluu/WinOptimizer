using System.Diagnostics;
using WinOptimizer.DevConsole.Models;

namespace WinOptimizer.DevConsole.Services;

/// <summary>
/// Surecleri UI'i durdurmadan calistirir, stdout/stderr'i satir satir akitarak
/// <see cref="OutputReceived"/> olayini tetikler. Iptal destekli (butun agac oldurulur).
/// Bu, DevConsole'un cekirdek motorudur.
/// </summary>
public sealed class CommandRunner : IDisposable
{
    private Process? _process;
    private CancellationTokenSource? _cts;
    private Stopwatch? _watch;

    public bool IsRunning => _process is not null && !_process.HasExited;

    /// <summary>Her cikti satirinda tetiklenir (UI thread'inde degil — cagiran marshal eder).</summary>
    public event Action<OutputLine>? OutputReceived;

    /// <summary>Komut bittiginde tetiklenir (cikis kodu + sure).</summary>
    public event Action<CommandResult>? Completed;

    /// <summary>Bir komutu calistirir. Zaten calisiyorsa yoksayir.</summary>
    public async Task RunAsync(DevCommand cmd, CancellationToken externalCt = default)
    {
        if (IsRunning)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        _watch = Stopwatch.StartNew();

        // Calisma dizini: goreli ise cozum kokune gore, yoksa cozum koku.
        string workingDir = string.IsNullOrEmpty(cmd.WorkingDir)
            ? DevPaths.SolutionRoot
            : Path.Combine(DevPaths.SolutionRoot, cmd.WorkingDir);

        var psi = new ProcessStartInfo(cmd.File)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            WorkingDirectory = workingDir
        };

        // Komut enjeksiyonu onlenmis: argumanlar dizi olarak (ArgumentList).
        foreach (string arg in cmd.Args)
        {
            psi.ArgumentList.Add(arg);
        }

        // Calistirilan komutu da goster (komut satiri seklinde).
        Emit(new OutputLine($"> {cmd.File} {string.Join(' ', cmd.Args)}", OutputLevel.Command));

        try
        {
            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    Emit(Classify(e.Data));
                }
            };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    Emit(Classify(e.Data));
                }
            };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            // Iptali bagla: external/cancel token'i sureci oldurur.
            _cts.Token.Register(() => TryKill());

            await _process.WaitForExitAsync(_cts.Token);
            _watch.Stop();

            int code = _process.ExitCode;
            Emit(new OutputLine(
                $"< cikis kodu {code} — sure {_watch.Elapsed.TotalSeconds:F1}s",
                code == 0 ? OutputLevel.Success : OutputLevel.Error));
            Completed?.Invoke(new CommandResult(cmd.Title, code, _watch.Elapsed));
        }
        catch (OperationCanceledException)
        {
            _watch?.Stop();
            Emit(new OutputLine("< IPTAL EDILDI", OutputLevel.Warning));
            Completed?.Invoke(new CommandResult(cmd.Title, null, _watch?.Elapsed ?? TimeSpan.Zero));
        }
        catch (Exception ex)
        {
            _watch?.Stop();
            Emit(new OutputLine("< HATA: " + ex.Message, OutputLevel.Error));
            Completed?.Invoke(new CommandResult(cmd.Title, -1, _watch?.Elapsed ?? TimeSpan.Zero));
        }
        finally
        {
            _process = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>Calisan sureci derhal durdurur (butun cocuk agaci ile).</summary>
    public void Cancel()
    {
        _cts?.Cancel();
        TryKill();
    }

    private void TryKill()
    {
        try { _process?.Kill(entireProcessTree: true); } catch { /* zaten cikti */ }
    }

    private void Emit(OutputLine line) => OutputReceived?.Invoke(line);

    /// <summary>Satir metnini seviyeye gore siniflandirir (renklendirme icin).</summary>
    private static OutputLine Classify(string text)
    {
        string lower = text.ToLowerInvariant();
        if (lower.Contains("hata") || lower.Contains("error") || lower.Contains("basarisiz"))
        {
            return new OutputLine(text, OutputLevel.Error);
        }

        if (lower.Contains("uyari") || lower.Contains("warning") || lower.Contains("atlandi"))
        {
            return new OutputLine(text, OutputLevel.Warning);
        }

        if (lower.Contains("basari") || lower.Contains("gec") || lower.Contains("0 hata") ||
            lower.Contains("succeeded") || lower.Contains("tamamlandi"))
        {
            return new OutputLine(text, OutputLevel.Success);
        }

        return new OutputLine(text, OutputLevel.Info);
    }

    public void Dispose()
    {
        TryKill();
        _cts?.Dispose();
    }
}
