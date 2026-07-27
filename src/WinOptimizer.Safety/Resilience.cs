using System.Management;
using System.Runtime.InteropServices;
using Polly;
using Polly.Retry;

namespace WinOptimizer.Safety;

/// <summary>
/// Dayanıklılık (resilience) ilkeleri — WMI/process/disk çağrılarındaki geçici
/// hataları saydam biçimde yeniden dener (master plan §18.1 &amp; §19 — Polly).
///
/// <para>Bakım işlemleri (geri yükleme noktası, WMI sorguları, dış komutlar) düşük
/// frekansta ama güvenilir olmalıdır: RPC sunucusu meşgul (COMException 0x8001010A),
/// WMA geçici hatası veya anlık I/O kilidi yeniden denenebilir. Bu boru hattı
/// üstel geri çekilme (exponential backoff) ile 3 kez dener.</para>
/// </summary>
public static class Resilience
{
    private static readonly ResiliencePipeline _transient = BuildTransient();

    /// <summary>
    /// Geçici hatalarda (COM/Management/IO/timeout) üstel geri çekilme ile
    /// en fazla 3 yeniden deneme yapan boru hattı.
    /// </summary>
    public static ResiliencePipeline Transient => _transient;

    /// <summary>Bir işlemi geçici-hata boru hattı içinde çalıştırır (Task→ValueTask köprüsü).</summary>
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
    {
        return await _transient.ExecuteAsync(async token => await action(token).ConfigureAwait(false), ct)
            .ConfigureAwait(false);
    }

    private static ResiliencePipeline BuildTransient()
    {
        var retry = new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromMilliseconds(200),
            MaxDelay = TimeSpan.FromSeconds(5),
            ShouldHandle = new PredicateBuilder()
                .Handle<COMException>()
                .Handle<ManagementException>()
                .Handle<IOException>()
                .Handle<TimeoutException>()
        };

        return new ResiliencePipelineBuilder()
            .AddRetry(retry)
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }
}
