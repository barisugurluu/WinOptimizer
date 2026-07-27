using WinOptimizer.Core;

namespace WinOptimizer.App.Resources;

/// <summary>
/// Modül görünen adlarını UI yereline göre çözer (master plan Bölüm 12.5 — modül i18n'ı).
///
/// <para>Modüller, katmanlama gereği <c>WinOptimizer.App</c>'e bağımlı olamaz (döngüsel
/// bağımlılık), bu yüzden <see cref="IOptimizationModule.DisplayName"/> her modülde
/// hard-coded TR varsayılan tutar. Bu sınıf, modül kimliğine karşılık gelen resx
/// anahtarını (<c>Module_{Id}</c>) arar; bulursa yerelleştirilmiş değeri, bulamazsa
/// modülün TR varsayılanına geri döner. Böylece EN kullanıcısı İngilizce modül adları
/// görür ve hiçbir modül sözleşmesi/dosyası değişmez.</para>
/// </summary>
public static class ModuleDisplayNameResolver
{
    /// <summary>Bir modülün yerelleştirilmiş görünen adını döndürür.</summary>
    public static string Resolve(IOptimizationModule module) =>
        Strings.GetModuleDisplayName(module.Id) ?? module.DisplayName;
}
