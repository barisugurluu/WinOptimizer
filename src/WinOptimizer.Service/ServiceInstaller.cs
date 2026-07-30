using System.Globalization;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;
using WinOptimizer.Safety;

namespace WinOptimizer.Service;

/// <summary>
/// Servis kurulum/kaldırma verb'leri. Servis tanımının (ad, görünen ad, açıklama,
/// başlangıç türü, kurtarma eylemleri) <b>tek kaynağıdır</b>: kurulum sihirbazı
/// (<c>installer/WinOptimizer.iss</c> [Run]/[UninstallRun]) ve uygulama içindeki
/// "Kur/Onar" düğmesi (Faz 2 <c>GuardServiceController</c>) ikisi de bu verb'leri çağırır.
/// Daha önce tanım üç yerde kopyalıydı ve senkronsuz kaldığı için kurulum donuyordu.
/// </summary>
/// <remarks>
/// Verb'ler bilinçli olarak <b>tireli</b>: <c>Host.CreateApplicationBuilder(args)</c>
/// tanımadığı argümanları yapılandırma olarak yorumlar; çıplak <c>install</c> pozisyonel
/// argüman olarak okunur ve servis normal worker gibi başlar (eski donma hatası).
/// </remarks>
internal static class ServiceInstaller
{
    /// <summary>Servisin sistemdeki adı (sc.exe / ServiceController için).</summary>
    public const string ServiceName = "WinOptimizerGuard";

    /// <summary>Hizmetler konsolunda görünen ad.</summary>
    public const string DisplayName = "WinOptimizer RealtimeGuard";

    /// <summary>Hizmetler konsolundaki açıklama.</summary>
    public const string ServiceDescription = "WinOptimizer gerçek zamanlı koruma servisi";

    /// <summary>Başarılı.</summary>
    public const int ExitOk = 0;

    /// <summary>Yönetici ayrıcalığı yok — kurulum/kaldırma yapılamaz.</summary>
    public const int ExitNotElevated = 2;

    /// <summary>sc.exe bir adımda başarısız oldu (çıktı günlüğe yazılır).</summary>
    public const int ExitScFailure = 3;

    /// <summary>Servisin durup silinmesi için beklenecek en uzun süre.</summary>
    private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Argümanları inceler; bir verb tanınırsa işi yapıp çıkış kodunu döndürür.
    /// Verb yoksa <c>null</c> döner ve çağıran normal servis/worker akışına devam eder.
    /// </summary>
    public static async Task<int?> TryHandleAsync(string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            return null;
        }

        string verb = args[0].TrimStart('-', '/').ToLowerInvariant();
        return verb switch
        {
            "install-service" => await RunElevatedAsync(InstallAsync, ct),
            "uninstall-service" => await RunElevatedAsync(UninstallAsync, ct),
            "service-status" => StatusAsync(),
            _ => null,
        };
    }

    /// <summary>Yönetici kontrolü + tek noktadan hata yakalama.</summary>
    private static async Task<int> RunElevatedAsync(
        Func<ProcessRunner, CancellationToken, Task<int>> action, CancellationToken ct)
    {
        if (!IsElevated())
        {
            Log("Bu islem yonetici ayricaligi gerektirir. Kurulum sihirbazi zaten yukseltilmis " +
                "calisir; bu exe'yi elle cagiriyorsaniz 'Yonetici olarak calistir' kullanin.");
            return ExitNotElevated;
        }

        using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true));
        var runner = new ProcessRunner(loggerFactory.CreateLogger<ProcessRunner>());

        try
        {
            return await action(runner, ct);
        }
        catch (Exception ex)
        {
            Log($"Beklenmeyen hata: {ex.GetType().Name}: {ex.Message}");
            return ExitScFailure;
        }
    }

    /// <summary>
    /// Servisi kurar veya zaten varsa yeniden yapılandırır (idempotent — Faz 2 "Onar" düğmesi
    /// ve sürüm yükseltmeleri de bunu çağırır), ardından başlatır.
    /// </summary>
    private static async Task<int> InstallAsync(ProcessRunner runner, CancellationToken ct)
    {
        string exePath = ResolveOwnExePath();
        bool exists = ServiceExists();

        // Var olan servisi 'create' ile kurmaya çalışmak 1073 verir; onun yerine 'config'
        // ile aynı degerlere getirilir. Boylece kurulum tekrar calistirildiginda da gecerli.
        string[] primary = exists ? BuildConfigArgs(exePath) : BuildCreateArgs(exePath);
        Log(exists
            ? $"'{ServiceName}' zaten kayitli — yeniden yapilandiriliyor."
            : $"'{ServiceName}' olusturuluyor: {exePath}");

        int code = await RunScAsync(runner, primary, ct);
        if (code != ExitOk)
        {
            return ExitScFailure;
        }

        // Aciklama ve kurtarma eylemleri kritik degil: basarisiz olurlarsa kurulum yine gecerli.
        await RunScAsync(runner, BuildDescriptionArgs(), ct, critical: false);
        await RunScAsync(runner, BuildFailureArgs(), ct, critical: false);

        return StartService();
    }

    /// <summary>
    /// Servisi durdurur ve siler. Servis hiç kurulu değilse başarı döner — kurulum
    /// kaldırma işlemi asla bu yüzden bloklanmamalı.
    /// </summary>
    private static async Task<int> UninstallAsync(ProcessRunner runner, CancellationToken ct)
    {
        if (!ServiceExists())
        {
            Log($"'{ServiceName}' kurulu degil — yapilacak is yok.");
            return ExitOk;
        }

        StopService();

        int code = await RunScAsync(runner, BuildSimpleArgs("delete"), ct);
        if (code != ExitOk)
        {
            return ExitScFailure;
        }

        Log($"'{ServiceName}' kaldirildi.");
        return ExitOk;
    }

    /// <summary>Servis durumunu stdout'a yazar (destek/teşhis için).</summary>
    private static int StatusAsync()
    {
        if (!ServiceExists())
        {
            Console.WriteLine($"{ServiceName}: kurulu degil");
            return ExitOk;
        }

        using var controller = new ServiceController(ServiceName);
        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture, "{0}: {1}", ServiceName, controller.Status));
        return ExitOk;
    }

    // --- sc.exe argüman kurucuları (testlerden doğrulanır) ------------------------------

    /// <summary>
    /// <c>sc create</c> argümanları. sc.exe'nin <c>option= value</c> sözdizimi gereği
    /// <c>binPath=</c> ile değeri <b>ayrı</b> argümanlar olmak zorundadır; bu yüzden
    /// <see cref="ProcessRunner"/>'ın <c>ArgumentList</c> imzası kullanılır (§17.5:
    /// yol/kullanıcı verisi asla string birleştirmeyle komuta gömülmez).
    /// </summary>
    internal static string[] BuildCreateArgs(string exePath) =>
    [
        "create", ServiceName,
        "binPath=", exePath,
        "start=", "auto",
        "DisplayName=", DisplayName,
    ];

    /// <summary>Var olan servisi aynı değerlere getiren <c>sc config</c> argümanları.</summary>
    internal static string[] BuildConfigArgs(string exePath) =>
    [
        "config", ServiceName,
        "binPath=", exePath,
        "start=", "auto",
        "DisplayName=", DisplayName,
    ];

    /// <summary><c>sc description</c> argümanları.</summary>
    internal static string[] BuildDescriptionArgs() =>
        ["description", ServiceName, ServiceDescription];

    /// <summary>
    /// <c>sc failure</c> argümanları: çökme sonrası 60 sn'de iki kez yeniden başlat,
    /// sayaç 24 saatte sıfırlanır. Sondaki boş eylem listeyi kapatır.
    /// </summary>
    internal static string[] BuildFailureArgs() =>
    [
        "failure", ServiceName,
        "reset=", "86400",
        "actions=", "restart/60000/restart/60000//",
    ];

    /// <summary>Tek argümanlı sc fiilleri (<c>delete</c>, <c>query</c> vb.).</summary>
    internal static string[] BuildSimpleArgs(string verb) => [verb, ServiceName];

    // --- yardımcılar --------------------------------------------------------------------

    private static async Task<int> RunScAsync(
        ProcessRunner runner, string[] args, CancellationToken ct, bool critical = true)
    {
        var (code, output) = await runner.RunCaptureAsync("sc.exe", args, ct);
        if (code != ExitOk)
        {
            string level = critical ? "HATA" : "uyari";
            Log($"[{level}] sc.exe {string.Join(' ', args)} -> exit " +
                $"{code.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}{output.Trim()}");
        }

        return code;
    }

    /// <summary>
    /// Servisin kayıtlı olup olmadığını yerelleştirmeden bağımsız biçimde sorgular.
    /// (<c>sc query</c> çıktısını ayrıştırmak Türkçe/İngilizce Windows'ta kırılgandır.)
    /// </summary>
    private static bool ServiceExists()
    {
        ServiceController[] services = ServiceController.GetServices();
        try
        {
            return services.Any(s =>
                s.ServiceName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            foreach (ServiceController service in services)
            {
                service.Dispose();
            }
        }
    }

    private static int StartService()
    {
        using var controller = new ServiceController(ServiceName);
        try
        {
            if (controller.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
            {
                Log($"'{ServiceName}' zaten calisiyor.");
                return ExitOk;
            }

            controller.Start();
            controller.WaitForStatus(ServiceControllerStatus.Running, StatusTimeout);
            Log($"'{ServiceName}' baslatildi.");
            return ExitOk;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
                                      or System.ServiceProcess.TimeoutException)
        {
            // Servis kayitli ama baslatilamadi: kurulum yine de gecerli sayilir; kullanici
            // Guard sekmesinden (Faz 2) tekrar deneyebilir. Sessiz gecilmez, gunluge yazilir.
            Log($"[HATA] '{ServiceName}' baslatilamadi: {ex.Message}");
            return ExitScFailure;
        }
    }

    private static void StopService()
    {
        using var controller = new ServiceController(ServiceName);
        try
        {
            if (controller.Status == ServiceControllerStatus.Stopped)
            {
                return;
            }

            if (controller.CanStop)
            {
                controller.Stop();
                controller.WaitForStatus(ServiceControllerStatus.Stopped, StatusTimeout);
                Log($"'{ServiceName}' durduruldu.");
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
                                      or System.ServiceProcess.TimeoutException)
        {
            // Durdurulamadiysa yine de delete denenir: Windows servisi bir sonraki yeniden
            // baslatmada siler (DELETE_PENDING). Kaldirma islemi bloklanmaz.
            Log($"[uyari] '{ServiceName}' durdurulamadi: {ex.Message}");
        }
    }

    /// <summary>
    /// Kendi exe yolu. Self-contained apphost'ta <see cref="System.Reflection.Assembly.Location"/>
    /// boş/yanlış olabildiği için <see cref="Environment.ProcessPath"/> kullanılır.
    /// </summary>
    private static string ResolveOwnExePath() =>
        Environment.ProcessPath ??
        Path.Combine(AppContext.BaseDirectory, "WinOptimizer.Service.exe");

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Hem stdout'a hem <c>%ProgramData%\WinOptimizer\logs\service-install.log</c>'a yazar.
    /// Kurulum sihirbazı bu exe'yi <c>runhidden</c> ile çağırdığı için konsol çıktısı
    /// kullanıcıya görünmez; dosya kaydı olmadan başarısızlık teşhis edilemez.
    /// </summary>
    private static void Log(string message)
    {
        Console.WriteLine(message);
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "WinOptimizer", "logs");
            Directory.CreateDirectory(dir);
            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd HH:mm:ss} [service-install] {1}{2}",
                DateTime.Now, message, Environment.NewLine);
            File.AppendAllText(Path.Combine(dir, "service-install.log"), line);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"(gunluk yazilamadi: {ex.Message})");
        }
    }
}
