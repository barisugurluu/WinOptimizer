namespace WinOptimizer.Core;

/// <summary>
/// Change journal'a yazılan değişiklik işleminin türü.
/// Her modül, yaptığı işleme karşılık gelen değeri kullanır.
/// (Bkz. master plan Bölüm 16.3 — journal JSONL kayıt örneği.)
/// </summary>
public enum ChangeOperationType
{
    /// <summary>Registry değerinin yazılması.</summary>
    RegistrySetValue,

    /// <summary>Bir Windows servisinin başlangıç türünün değiştirilmesi.</summary>
    ServiceStartType,

    /// <summary>Başlangıç girdisinin etkin/devre dışı bırakılması.</summary>
    StartupToggle,

    /// <summary>Dosya/klasör silinmesi (geri dönüşüme taşınma dahil).</summary>
    FileDelete,

    /// <summary>Bir sürecin working set'inin boşaltılması (EmptyWorkingSet).</summary>
    ProcessOptimize,

    /// <summary>Geri dönüşüm kutusunun boşaltılması.</summary>
    RecycleBinEmpty,

    /// <summary>Komut tabanlı işlem (SFC/DISM/powercfg vb.).</summary>
    CommandRun,

    /// <summary>Sistem geri yükleme noktası oluşturulması.</summary>
    RestorePoint,

    /// <summary>Genel/diğer işlem.</summary>
    Other
}
