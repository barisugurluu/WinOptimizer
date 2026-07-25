namespace WinOptimizer.Updater;

/// <summary>Güncelleme denetleyicisi seçenekleri.</summary>
/// <param name="Owner">GitHub repo sahibi (ör. "WinOptimizer").</param>
/// <param name="Repo">GitHub repo adı (ör. "WinOptimizer").</param>
/// <param name="Channel">Hangi kanal denetlenecek (Stable/Beta).</param>
/// <param name="CurrentVersion">Şu anki uygulama sürümü; verilmezse 0.0.0.0 (her zaman güncelleme var sayılır).</param>
/// <param name="Timeout">Ağ işlemleri zaman aşımı; verilmezse 30 sn.</param>
public sealed record UpdateOptions(
    string Owner = "WinOptimizer",
    string Repo = "WinOptimizer",
    UpdateChannel Channel = UpdateChannel.Stable,
    Version? CurrentVersion = null,
    TimeSpan? Timeout = null)
{
    /// <summary>Geçerli zaman aşımı (varsayılan 30 sn).</summary>
    public TimeSpan TimeoutValue => Timeout ?? TimeSpan.FromSeconds(30);

    /// <summary>Geçerli sürüm (verilmezse 0.0.0.0).</summary>
    public Version CurrentVersionValue => CurrentVersion ?? new Version(0, 0, 0, 0);
}
