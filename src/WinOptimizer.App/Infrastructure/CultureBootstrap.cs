using System.Globalization;
using Wpf.Ui.Appearance;

namespace WinOptimizer.App.Infrastructure;

/// <summary>
/// Ayarlardaki dil tercihini uygulama kültürüne uygular.
/// </summary>
/// <remarks>
/// <para><b>Yeniden başlatma gerekir.</b> XAML'deki <c>x:Static p:Strings.*</c> bağları
/// sayfa/pencere kurulurken bir kez çözülür; kültürü sonradan değiştirmek zaten oluşturulmuş
/// görsel ağacı güncellemez. Bu yüzden "canlı dil değişimi" taklit edilmez — kullanıcıya
/// açıkça yeniden başlatması gerektiği söylenir.</para>
/// <para>Uygulama başlangıcında, <b>herhangi bir pencere oluşturulmadan önce</b> çağrılmalıdır.</para>
/// </remarks>
public static class CultureBootstrap
{
    /// <summary>Desteklenen kültürler (kaynak dosyaları: Strings.resx / Strings.en.resx).</summary>
    private static readonly string[] Supported = ["tr-TR", "en-US"];

    /// <summary>Ayarlardaki dili uygular. Tanınmayan değer yok sayılır (işletim sistemi dili kalır).</summary>
    public static void Apply(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag) ||
            !Supported.Contains(languageTag, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var culture = new CultureInfo(languageTag);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        // CurrentCulture (sayı/tarih biçimi) bilinçli olarak DEĞİŞTİRİLMEZ: kullanıcı Windows
        // bölge ayarlarını kendi belirler ve dosya boyutları zaten FileSizeFormatter ile
        // InvariantCulture'da biçimlenir (CA1305).
    }
}

/// <summary>
/// Ayarlardaki tema tercihini uygular.
/// </summary>
/// <remarks>
/// <c>App.xaml</c> içindeki <c>ThemesDictionary Theme="Dark"</c> yalnızca açılış varsayılanıdır;
/// gerçek tercih burada uygulanır. Tema değişimi canlı çalışır (dil gibi yeniden başlatma
/// gerektirmez), çünkü WPF-UI kaynak sözlüklerini çalışma zamanında değiştirir.
/// </remarks>
public static class ThemeBootstrap
{
    /// <summary>Ayar değerini ("dark"/"light") uygular.</summary>
    public static void Apply(string? theme)
    {
        var applicationTheme = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase)
            ? ApplicationTheme.Light
            : ApplicationTheme.Dark;

        ApplicationThemeManager.Apply(applicationTheme);
    }
}
