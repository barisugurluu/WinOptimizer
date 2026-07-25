namespace WinOptimizer.Updater;

/// <summary>
/// Güncelleme kanalı — kademeli (staged) sürüm dağıtımı (master plan Bölüm 20.6).
/// Stable kullanıcıları yalnızca kararlı sürümleri alır; Beta kanalı ön sürümleri de denetler.
/// </summary>
public enum UpdateChannel
{
    /// <summary>Sadece kararlı (stable) sürümler — üretim önerisi.</summary>
    Stable = 0,

    /// <summary>Ön sürümler dahil (beta/RC) — test kullanıcıları.</summary>
    Beta = 1,
}
