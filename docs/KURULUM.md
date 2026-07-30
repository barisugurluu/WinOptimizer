# WinOptimizer — Kurulum Kılavuzu

Bu belge **son kullanıcı** içindir: WinOptimizer'ı kendi ya da başka bir bilgisayara
kurmak için gereken tek şey burada anlatılıyor. Kaynaktan derleme yapacaksanız
[Geliştirici: kaynaktan derleme](#geliştirici-kaynaktan-derleme) bölümüne bakın.

---

## 1. Gereksinimler

| Gereksinim | Değer |
|---|---|
| İşletim sistemi | Windows 10 sürüm 2004 (derleme 19041) veya üzeri · Windows 11 |
| Mimari | 64-bit (x64). arm64 desteklenmiyor. |
| Yetki | **Yönetici** (kurulum ve uygulama için) |
| Disk | ~150 MB |
| .NET | **GEREKMEZ** — runtime kurulumun içinde gömülüdür |

> **.NET kurmanız gerekmiyor.** Kurulum paketi self-contained'dir: .NET 8 çalışma zamanı
> uygulamayla birlikte gelir. (Eski sürümlerde hedef PC'de .NET 8 Desktop Runtime
> gerekiyordu ve kurulu olmayan makinelerde uygulama "You must install .NET" hatası
> veriyordu — bu durum ortadan kaldırıldı.)

## 2. İndirme ve kurulum

1. [Releases](https://github.com/barisugurluu/WinOptimizer/releases) sayfasından en son
   `WinOptimizer-<sürüm>-setup.exe` dosyasını indirin.
2. (Önerilir) Dosyanın bozulmadığını doğrulayın — bkz. [SHA256 doğrulaması](#4-sha256-doğrulaması).
3. Kuruluma çift tıklayın. SmartScreen uyarısı çıkarsa bkz.
   [SmartScreen uyarısı](#3-smartscreen-uyarısı-normaldir).
4. Yönetici onayı (UAC) verin.
5. Sihirbazda:
   - **Dil**: Türkçe veya English
   - **Lisans sözleşmesi**: okuyup kabul edin
   - **Kurulum klasörü**: varsayılan `C:\Program Files\WinOptimizer`
   - **Ek bileşenler**:
     - ☐ *Masaüstü simgesi* — isteğe bağlı
     - ☐ *RealtimeGuard hizmetini kur ve başlat* — **varsayılan kapalı**. Bu, arka planda
       çalışan ve eşik aşımında otomatik müdahale eden bir Windows hizmetidir. Kurulumdan
       sonra uygulama içinden de kurabilirsiniz; acele etmeyin.
6. Kurulum bitince Başlat menüsünde **WinOptimizer** görünür.

### Sessiz kurulum (kurumsal / betik)

```powershell
.\WinOptimizer-0.1.0-setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

Kurulum klasörünü değiştirmek için: `/DIR="D:\Araclar\WinOptimizer"`
Günlük almak için: `/LOG="C:\Temp\winoptimizer-setup.log"`

winget üzerinden (kararlı sürüm yayınlandığında):

```powershell
winget install WinOptimizer.WinOptimizer
```

## 3. SmartScreen uyarısı normaldir

WinOptimizer **kod imzalı değildir**. Bu bilinçli bir karardır: kod imzalama sertifikası
yıllık ücretlidir ve bu proje ücretsizdir. Sonuç olarak Windows ilk çalıştırmada
**"Windows bilgisayarınızı korudu"** uyarısı gösterebilir.

Nasıl geçilir:

1. **Daha fazla bilgi** bağlantısına tıklayın
2. Görünen **Yine de çalıştır** düğmesine tıklayın

Aynı durum tarayıcıda da olabilir: Chrome/Edge "bu dosya genellikle indirilmiyor" diyerek
indirmeyi durdurabilir → indirmeler listesinden **Sakla / Yine de indir** seçin.

> **Neden kendinden imzalı bir sertifika kullanılmıyor?**
> Kullanılsaydı, sertifikayı kuran makinede geçerli görünür ama **diğer her PC'de
> "geçersiz imza"** olarak okunurdu. Bu, hiç imzalanmamış olmaktan daha kötüdür: bazı
> antivirüs motorları geçersiz imzayı doğrudan şüphe sinyali sayar. Bu yüzden dağıtım
> tamamen imzasızdır ve doğrulama SHA256 ile yapılır.

Ek olarak: WinOptimizer bir sistem bakım aracıdır — kayıt defterine yazar, hizmet
yapılandırır, SFC/DISM çalıştırır. Bu davranış profili antivirüs sezgisel taramalarında
uyarı üretebilir. Kaynak kodu ve derleme hattı herkese açıktır.

## 4. SHA256 doğrulaması

Her sürümde kurulum dosyasının yanında bir `.sha256` dosyası ve sürüm notlarında hash
yayınlanır. İndirdiğiniz dosyayı doğrulamak için:

```powershell
Get-FileHash .\WinOptimizer-0.1.0-setup.exe -Algorithm SHA256
```

Çıkan `Hash` değeri, sürüm notlarındaki değerle **harfi harfine** aynı olmalıdır.
Farklıysa dosyayı **çalıştırmayın**, silin ve yeniden indirin.

## 5. Yönetici yetkisi neden gerekiyor?

WinOptimizer her açılışta UAC onayı ister. Standart bir kullanıcı hesabındaysanız
yönetici parolası sorulur. Bu normaldir; uygulamanın yaptığı işler bunu gerektirir:

- `sfc /scannow`, `DISM /RestoreHealth` — sistem dosyası onarımı
- `HKLM` altında kayıt defteri ince ayarları
- Windows hizmetlerinin başlangıç türünü değiştirme
- `%WINDIR%\Temp`, Prefetch, WER gibi sistem klasörlerini temizleme
- Sistem Geri Yükleme noktası oluşturma

Yönetici olmayan bir modda çalıştırılsaydı bu işlemlerin her biri erişim hatasıyla
başarısız olurdu — UAC istemi, sessiz başarısızlıklardan iyidir.

## 6. Kaldırma

**Ayarlar → Uygulamalar → Yüklü uygulamalar → WinOptimizer → Kaldır**
(veya `C:\Program Files\WinOptimizer\unins000.exe`)

Kaldırma sırasında RealtimeGuard hizmeti durdurulup silinir. Sonunda size şu soru sorulur:

> Ayarlar, günlükler ve geri alma geçmişi de silinsin mi?

**Varsayılan "Hayır"dır ve genellikle doğru cevap budur.** `%ProgramData%\WinOptimizer`
altındaki *change journal*, WinOptimizer'ın yaptığı değişiklikleri geri almanın tek veri
kaynağıdır; silerseniz yapılmış değişiklikler kalıcı hale gelir.

## 7. Güncelleme

Şimdilik güncelleme kontrolü komut satırından yapılır:

```powershell
WinOptimizer.Cli update --check
```

Kurulum, kurulum klasörünü sistem `PATH`'ine eklediği için bu komut yeni açtığınız
herhangi bir terminalde çalışır. Güncelleme varsa:

```powershell
WinOptimizer.Cli update --yes
```

İndirilen paket, sürümün `.sha256` yan dosyasıyla doğrulanır; doğrulama başarısızsa
kurulum yapılmaz. Uygulama açıkken güncelleme yaparsanız kurulum uygulamayı kapatmayı
teklif eder.

> Not: Güncelleme denetlenemezse (ağ yok, depo erişilemez) araç bunu açıkça söyler —
> "Güncelleme denetlenemedi: ...". Sessizce "güncelsiniz" demez.

## 8. Sorun giderme

**İlk açılışta ne olur?** Uygulamayı ilk kez çalıştırdığınızda ana pencereden önce bir
**sistem kontrolü** ekranı gelir: 64-bit, Windows sürümü, yönetici yetkisi, WMI, Sistem Geri
Yükleme, disk alanı, veri dizini yazılabilirliği ve hizmet durumu tek tek denetlenir.
✓ sorun yok · ⚠ kısıtlı (uygulama çalışır) · ✕ engelleyen (bu durumda uygulama açılmaz ve
size ne yapmanız gerektiği söylenir). Bu ekranı sonradan **Yönetim → Sistem & Veri**
sekmesinden istediğiniz zaman tekrar çalıştırabilirsiniz.

| Belirti | Ne yapmalı |
|---|---|
| Kurulum "Finishing installation" adımında donuyor | Bu hata giderildi (v0.1.0-alpha öncesi paketlerde vardı). Elinizde eski bir `WinOptimizer-0.1.0-setup.exe` varsa silin, Releases'ten yeni paketi indirin. |
| Uygulama açılmıyor, hiçbir şey olmuyor | Artık bir **hata penceresi** çıkar; içindeki "Günlük klasörünü aç" ve "Teşhis paketi oluştur" düğmelerini kullanın. Pencere de çıkmazsa `%LOCALAPPDATA%\WinOptimizer\startup-error.txt` dosyasına bakın. |
| Bir sorunu bildireceğim | **Yönetim → Sistem & Veri → Teşhis paketi oluştur.** Üretilen `.zip` günlükleri, hizmet durumunu, gereksinim raporunu ve değişiklik geçmişini içerir; içindeki `OKUBENI.txt` ne gönderdiğinizi açıklar. Hiçbir yere otomatik gönderilmez. |
| RealtimeGuard hizmetini sonradan kurmak istiyorum | **Yönetim → Guard** sekmesi: Kur / Başlat / Durdur / Onar / Kaldır. Kurulum sırasında işaretlemediyseniz sorun değil, hizmet isteğe bağlıdır. |
| "You must install .NET to run this application" | Elinizdeki paket eski (framework-dependent) MSI. Releases'ten güncel `setup.exe`'yi kurun. |
| Hizmet kurulmadı / çalışmıyor | `%ProgramData%\WinOptimizer\logs\service-install.log` dosyasına bakın. Elle: yönetici PowerShell'de `& "C:\Program Files\WinOptimizer\WinOptimizer.Service.exe" install-service` |
| Hizmet durumu | `& "C:\Program Files\WinOptimizer\WinOptimizer.Service.exe" service-status` |
| `WinOptimizer.Cli` komutu bulunamıyor | PATH güncellemesi yalnızca **yeni açılan** terminallerde görünür. Terminali kapatıp açın. |

Günlükler ve teşhis verileri hiçbir yere gönderilmez; bkz. [PRIVACY.md](../PRIVACY.md).

---

## Geliştirici: kaynaktan derleme

### Ön koşullar

- **.NET 8 SDK** (yalnız derleme için; son kullanıcı için gerekli değil) —
  <https://dotnet.microsoft.com/download>. Sürüm `global.json` ile sabitlenmiştir
  (8.0.4xx bandı).
- **Inno Setup 6** (yalnız kurulum paketi üretmek için):
  ```powershell
  winget install JRSoftware.InnoSetup
  ```

### Tek komut

```powershell
.\build\build-installer.ps1
```

Bu komut sırasıyla: derler → testleri koşar → **self-contained** publish yapar →
publish sağlık kontrolü (`hostfxr.dll`/`coreclr.dll` ve üç exe) → `license.rtf` üretir →
ISCC ile `setup.exe` derler → SHA256 yan dosyası yazar.

Çıktı: `installer\build\WinOptimizer-<sürüm>-setup.exe` (+ `.sha256`)

Yararlı anahtarlar:

| Anahtar | Etki |
|---|---|
| `-SkipTests` | `dotnet test` adımını atlar (CI testleri ayrı adımda koşuyor) |
| `-IsccPath <yol>` | ISCC.exe otomatik bulunamazsa |
| `-FrameworkDependent` | Runtime'ı **gömmez** — yalnız tanılama için, dağıtmayın |

Sürüm tek yerden gelir: `Directory.Build.props` → `VersionPrefix` / `VersionSuffix`.
Kurulum ve winget manifestleri bunu türetir; elle senkronize edilecek ikinci bir yer yoktur.

### Neden MSI değil?

Proje bir dönem üç paralel kurulum hattı taşıdı (WiX MSI, self-contained MSI, Inno
setup.exe) ve üçü de farklı biçimde kırıktı. Tek hat olarak Inno Setup seçildi:

- **Kısayol ve dil**: Inno betiği Başlat menüsü/masaüstü kısayollarını ve TR+EN sihirbazı
  zaten sağlıyordu; WiX `Product.wxs` içinde tek bir `<Shortcut>` yoktu — MSI kurulduğunda
  kullanıcının uygulamayı başlatacak hiçbir yolu olmuyordu ve sihirbaz yalnızca Türkçeydi.
- **Payload doğruluğu**: MSI payload üreticisi özyinelemeli değildi; `en\` ve `runtimes\`
  alt klasörleri pakete hiç girmiyordu (self-contained'de bu native DLL'lerin eksilmesi
  demekti).
- **Tekrar üretilebilirlik**: `OutputName` sabit olduğu için "self-contained MSI" elle
  yeniden adlandırılmıştı; aynı komut onu bir daha üretemiyordu.
- **Bakım maliyeti**: Üç hattı senkron tutmanın karşılığı yoktu; MSI'dan hiç kurulum
  yapılmadığı için (yayınlanmış release yok) geçiş maliyeti sıfırdı.

MSI'nin gerçek avantajları (GPO dağıtımı, kurumsal envanter) bu proje için gündemde
değildir. İhtiyaç doğarsa Inno çıktısını saran bir MSI/Burn paketi eklenebilir.
