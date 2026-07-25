# WinOptimizer — Gizlilik Politikası

**Yürürlük tarihi:** 26.07.2026 · **Sürüm:** 1.0

> Bu belge, WinOptimizer'ın verilerinizle ne yaptığını sade dille anlatır.
> Kısa cevap: **hiçbir şeyi bize göndermez.**

---

## 1. Toplanmayan veriler

WinOptimizer **telemetri toplamaz.** Uygulama:

- Kullanım istatistiği, olay izleme veya "analytics" verisi **göndermez**;
- Otomatik çökme raporu **göndermez**;
- Reklam veya profilleme kimliği **oluşturmaz, okumaz, iletmez**;
- Hesap açmanızı istemez, oturum açmanızı gerektirmez;
- Dosyalarınızın içeriğini okumaz, kopyalamaz veya dışarı aktarmaz.

Geliştiricinin işlettiği bir sunucu yoktur. Verilerinizi alacak bir yer yoktur.

---

## 2. Bilgisayarınızda kalan veriler

Uygulama işini yapabilmek için bazı kayıtları **yalnızca yerel diskinizde** tutar:

| Ne | Nerede | Neden |
|----|--------|-------|
| Değişiklik günlüğü (journal) | `%ProgramData%\WinOptimizer\journal\*.jsonl` | Her değişikliği geri alabilmek için |
| Uygulama günlükleri | `%ProgramData%\WinOptimizer\logs\*.log` | Sorun giderme (7 gün sonra otomatik silinir) |
| Ayarlar | `%ProgramData%\WinOptimizer\settings.json` | Tercihleriniz ve eşik değerleri |
| Kayıt defteri yedekleri | `%ProgramData%\WinOptimizer\backups\` | Geri alma güvenlik ağı |
| Teşhis paketi (isteğe bağlı) | `%LocalAppData%\WinOptimizer\diagnostics\` | Yalnızca siz oluşturursanız |

Bu kayıtlar dosya ve kayıt defteri **yollarını** içerir; bu yollarda Windows
kullanıcı adınız geçebilir (ör. `C:\Users\<ad>\AppData\...`). Bunlar bilgisayarınızdan
çıkmaz — siz bir dosyayı elle paylaşmadıkça.

Uygulamayı kaldırdığınızda bu klasörü elle silebilirsiniz.

---

## 3. İnternete çıkılan tek durum: güncelleme denetimi

Dürüst olmak gerekirse **bir ağ isteği vardır.** Güncelleme denetimi
(`update` komutu veya uygulamadaki güncelleme kontrolü) GitHub'ın genel sürüm
API'sine bağlanır:

```
https://api.github.com/repos/.../releases
```

Bu istek sırasında:

- **Gönderilen:** yalnızca standart bir HTTPS isteği (IP adresiniz ve kullanıcı
  aracısı dahil — bunlar her HTTPS isteğinin doğal parçasıdır). Kimliğinizi
  belirten, sisteminizi tanımlayan veya kullanımınızı anlatan **hiçbir ek veri
  eklenmez.**
- **Alınan:** en son sürüm numarası ve indirme bağlantısı.
- **Kimi ilgilendirir:** bu isteği GitHub görür ve kendi günlüklerinde tutabilir;
  bu GitHub'ın politikasına tabidir, WinOptimizer'ın değil.
- **Kapatmak:** güncelleme denetimini çalıştırmazsanız uygulama kendiliğinden
  internete çıkmaz.

Bunun dışında uygulama hiçbir adrese bağlanmaz.

---

## 4. Teşhis paketi (yalnızca siz isterseniz)

Ayarlar → **Teşhis Paketini Dışa Aktar**, günlükleri, değişiklik geçmişini ve
sistem bilgisini bir `.zip` dosyasına toplar.

- Paket **hiçbir yere gönderilmez**; yalnızca diskinize yazılır.
- Kime göndereceğinize (veya hiç göndermemeye) **siz** karar verirsiniz.
- İçine, ne toplandığını anlatan bir `OKUBENI.txt` konur — göndermeden önce
  paketi açıp inceleyebilir, istemediğiniz dosyaları silebilirsiniz.
- Sistem bilgisine kullanıcı adınız ve makine adınız **konmaz**.
- Parola, lisans anahtarı veya kimlik bilgisi **hiçbir koşulda** pakete konmaz.

---

## 5. Yönetici yetkisi neden gerekiyor?

WinOptimizer sistem dosyalarını temizlediği, servisleri ve kayıt defterini
düzenlediği için yönetici olarak çalışır. Bu yetki **yalnızca** uygulamanın
gösterdiği ve onayladığınız işlemler için kullanılır; veri toplamak için değil.

---

## 6. Çocukların gizliliği

Uygulama hiç kimseden kişisel veri toplamadığı için çocuklardan da veri toplamaz.

---

## 7. Değişiklikler

Bu politika değişirse sürüm numarası ve tarih güncellenir; değişiklikler sürüm
notlarında belirtilir. Politikanın geçmişi proje deposunda izlenebilir.

---

## 8. İletişim

Soru veya bildirim için proje deposundaki "Issues" bölümünü kullanabilirsiniz.

> **Not:** Bu belge, ürünün gerçek davranışını açıklamak için yazılmıştır ve hukuki
> danışmanlık değildir. Ticari dağıtım öncesi bir hukukçuya inceletmeniz önerilir.
