# LandFlow Analiz Paneli

Retention, churn, DAU/MAU, ülke, oynama süresi, level ve olay verilerini Meta ve GameAnalytics'ten alıp tek sayfada gösterir.
Kurulum yok, npm paketi yok. Sadece **Node.js 18+** gerekir.

## Açma

**`dashboard/panel.html` dosyasına çift tıklayın.** Kurulum ve sunucu gerekmez, Windows ve Linux'ta aynı çalışır, Unity'ye dokunmaz.

GameAnalytics CSV'lerini sayfaya sürükleyin ya da **CSV ekle** deyin. Tarayıcı onları hatırlar: sonraki açılışta yeniden eklemeniz gerekmez. Aynı adla eklenen dosya eskisinin yerine geçer, **Temizle** hepsini siler. Veri bilgisayarınızdan çıkmaz.

Hiç CSV yoksa panel baştan sona **ÖRNEK VERİ** gösterir ve üstte sarı şerit çıkar. İlk gerçek veriyle örnek tamamen kalkar; eksik kalan bölümler boş görünür, gerçek ve uydurma sayılar aynı sayfada asla karışmaz.
Her bölümün yanındaki etiket verinin nereden geldiğini söyler: `Canlı`, `CSV · tarih`, `ÖRNEK`, `Veri yok`.

**Meta reklam verisi ya da ücretli GameAnalytics API'si için** anahtarlar gizli kalsın diye küçük bir sunucu gerekir (Node.js 18+):

```bash
node dashboard/server.mjs
```

Windows'ta `dashboard/Panel.bat` de aynısını yapar. Sayfa aynıdır, CSV'ler bu adreste ayrıca eklenir (`http://localhost:5173`, tarayıcı dosyadan açılan sayfayla paylaşmaz).

## Hangi veri nereden gelir

| İstenen | GA Explore CSV (ücretsiz) | GA Metrics API (ücretli) | Meta API |
|---|---|---|---|
| DAU, MAU, yeni oyuncu, oturum, oynama süresi | ✓ | ✓ | |
| Retention (kurulum gününe göre) | ✓ | ✓ | |
| Churn | `100 − retention` olarak hesaplanır | aynı | |
| Kurulumdan N gün sonra toplam oynama | ✓ | ✓ | |
| Ülke | ✓ | ✓ | |
| **Level bazında başlama, bitirme, kayıp, süre, oyuncu** | ✓ | **Vermiyor** | |
| **Olaylar (`Restart`, `2X`, `3X`, `Depleted`)** | ✓ | **Vermiyor** | |
| Platform, olay kategorileri | panelde yok | ✓ | |
| Reklam harcaması, kurulum, CPI, kampanya, ülke, yerleşim | | | ✓ |

- **Ücretsiz planda veri elle güncellenir:** Explore'dan CSV indirip panele sürüklersiniz.
- **Metrics API** yalnızca PipelineIQ planında var (fiyat listesinde "$499/ay'dan başlayan"). Olursa `.env` ile canlı gelir, ama level ve olay yine CSV ister. [API şeması](https://docs.gameanalytics.com/redocusaurus/metrics-api.yaml)
- **Meta:** eski Facebook Analytics 2021'de kapandı. Oyunun Meta'ya gönderdiği olaylar Events Manager'da görünür; panel Meta'dan yalnızca reklam verisini okur.
- **İki oyun:** GameAnalytics'te Android (`PixelGame`) ve iOS (`LandFlowIOS`) ayrı oyunlardır. Panel tek oyun gösterir; `data/` içine ikisini karıştırmayın.

## Oyun gerçekte ne gönderiyor

`Assets/Scripts/LandFlowAnalytics.cs` (GameAnalytics):

| Olay | Biçim | Not |
|---|---|---|
| Level başladı | `Start:Level_015:Hayalet` | |
| Level bitti | `Complete:Level_015:Hayalet`, score = geçen saniye | Level süresi buradan |
| Level kaybedildi | `Fail:Level_015:Hayalet`, score = geçen saniye | |
| Tasarım olayları | `Level:Restart`, `Speed:2X`, `Speed:3X`, `Speed:Depleted` | |
| Oturum | GA SDK'sı otomatik gönderir | DAU, süre ve retention buradan |

`Assets/Scripts/LandFlowMeta.cs` (Meta): yalnızca `FB.ActivateApp()` ve SDK'nın otomatik olayları. Özel olay yok.

Her ikisi de veri izni veren oyuncularla sınırlıdır (`LandFlowPrivacy`), bu yüzden sayılar mağaza indirmelerinden düşük olabilir.

**Ölçülemeyenler ve oyuna eklenmesi gerekenler** panelin en altında listelidir: Restart ve yarıda bırakılan denemelerin süresi, kampanya kaynağı (MMP yok), gelir olayları, sabit `Build 0.1`.

## Anahtarlar

```bash
cp dashboard/.env.example dashboard/.env
```

`.env` git'e girmez ve tarayıcıya gönderilmez. Oyundaki GA game key / secret key veri okumaz, burada kullanılmaz.

**Meta:**
1. business.facebook.com → İşletme ayarları → Sistem kullanıcıları → kullanıcı ekleyin, reklam hesabını atayın.
2. Token oluşturun, `ads_read` iznini seçin, `META_ACCESS_TOKEN` olarak yazın.
3. Ads Manager adres çubuğundaki `act=` değerini `META_AD_ACCOUNT_ID` olarak yazın.

**GameAnalytics (yalnızca PipelineIQ):**
1. Organization settings sayfasından API anahtarı oluşturup `GA_API_KEY` olarak yazın.
2. Oyunu açınca adres çubuğundaki `/game/<sayı>/` değerini `GA_GAME_ID` olarak yazın.
3. `http://localhost:5173/api/ga/check` adresi anahtarın gördüğü oyunları listeler.

## GameAnalytics CSV

Yol: GA → oyun → **Explore**. Date range seçin, sorguyu kurun, sayfanın altındaki tabloda **Export → Download** deyin, dosyayı panele sürükleyin.
Dosya adı önemsiz, ne olduğu başlıktan anlaşılır; aynı metrik iki dosyadaysa yenisi kullanılır. CSV'ler git'e girmez ve tarayıcıda durur, bu yüzden Ezgi'nin kendi tarayıcısına ayrıca eklemesi gerekir.

**Üç tuzak:**
- **Sorgudan kalan filtreye bakın.** Filters satırında beklemediğiniz bir filtre varsa X ile kaldırın: New Users'tan sonra DAU seçince filtre kaldığı için DAU yanlış geldi.
- **Sonuç yüklenmeden Export demeyin.** Yüklenirken bütün değerler 0 görünür.
- **Level sorgularında Limit'i 500 yapın.** Varsayılan 50'de levellerin bir kısmı kesilir. Split eklemeyin, limit 50'ye düşer.

| Metrik (Predefined) | Group by | Aggregation | Not |
|---|---|---|---|
| `DAU` | Time | | |
| `New Users` | Time | | |
| `Session Count (started)` | Time | | |
| `Playtime per session` | Time | | |
| `Playtime per user` | Time | | |
| `MAU (rolling 30 days)` | Time | | |
| `Retention` | Time | | Days ahead varsayılanı 1–7 ve 14; D30 için listede varsa 30'u ekleyin |
| `Cumulative Playtime per user` | Time | | |
| `DAU` | Global dimension → Country code | | Ülke tablosu |
| `Level attempts started` | Event dimension → Progression 01 | Count | |
| `Level attempts started` | Event dimension → Progression 01 | Unique users | Oyuncu sayısı |
| `Level attempts completed` | Event dimension → Progression 01 | Count | |
| `Level attempts completed` | Event dimension → Progression 01 | Mean score per event | **Dosya adında `bitirme` geçmeli** |
| `Level attempts failed` | Event dimension → Progression 01 | Count | |
| `Level attempts failed` | Event dimension → Progression 01 | Mean score per event | **Dosya adında `kayip` geçmeli** |
| `Custom event count per user` | Event dimension → Event id 02 | Count | Olaylar |
| `Custom event count per user` | Event dimension → Event id 02 | Unique users | Olay başına oyuncu |

Ortalama skorun başlığı iki durumda da aynı olduğu için tek istisna o iki dosyanın adıdır. Aynı ada sahip dosyalar birbirinin yerine geçtiği için indirirken farklı adla kaydedin. Tanınmayan dosya panelin üstünde uyarı olarak görünür.
Sık kullanacağınız sorguları Explore'da **Save** ile saklayabilirsiniz. Level, olay ve ülke dosyaları indirirken seçilen dönemi gösterir; paneldeki tarih seçici onları etkilemez, etiketteki tarih dosyanın kaydedildiği gündür.

## Sayılar nasıl hesaplanıyor

- **Retention:** kurulum gününe göre kohortların yeni oyuncu sayısıyla ağırlıklı ortalaması, N günü dolmamış kohortlar hariç. API ile dönemde yeterli kohort yoksa önceki kohortlar da kullanılır.
- **Churn:** `100 − N. gün retention`. Kalıcı kaybı değil, o gün dönmeyenleri gösterir.
- **Ülke D1:** yalnızca API ile, son en çok 14 kohortun düz ortalaması.
- **MAU:** dönemin son gününden geriye 30 gün içindeki tekil oyuncu.
- **Günlük oynama:** `session_length_per_user`, gün içinde oyuncu başına toplam süre.
- **Ulaşan:** levele başlayan tekil oyuncu; yoksa önceki leveli bitirenler (`~` ile işaretli).
- **Toplam süre:** `bitirme × ort. süre + kayıp × ort. süre`. Restart ve yarıda bırakma dahil değildir.
- **Tıkanma ve Kopma:** kazanma oranı %40 altı ya da ulaşanların %15'ten fazlasının bitirmemesi. Eşikler panelde işaret olsun diye seçildi, sektör ölçütü değildir.
- **Reklam dışı oyuncu:** `GA yeni oyuncu − Meta kurulum`. İki kaynak farklı sayar, kaba tahmindir.

## Doğrulama durumu

- **GA CSV:** başlıklar, alt satırlar ve toplamlar gerçek hesaptan (`PixelGame`, Ekim 2026) görüldü. Sayıların saniye olduğu GA arayüzüyle karşılaştırıldı.
- **Dosyadan açma:** Linux'ta Chrome ile `file://` üzerinden sürükle-bırak, yenileme ve temizleme denendi. Windows'ta ve Firefox'ta denenmedi.
- **GA Metrics API ve Meta:** gerçek anahtarla **denenmedi**. İstekler API şemasına karşı denetlendi, panel şema biçiminde yanıt veren sahte sunucuyla sınandı.
- İlk gerçek Meta bağlantısında `actions` içindeki kurulum türü (`mobile_app_install` ve benzerleri) kontrol edilmeli.
