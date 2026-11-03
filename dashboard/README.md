# LandFlow Analiz Paneli

Retention, churn, DAU/MAU, ülke, oynama süresi, level ve olay verilerini Meta ve GameAnalytics'ten alıp tek sayfada gösterir.
Kurulum yok, npm paketi yok. Sadece **Node.js 18+** gerekir.

## Açma

**`dashboard/panel.html` dosyasına çift tıklayın.** Kurulum ve sunucu gerekmez, Windows ve Linux'ta aynı çalışır, Unity'ye dokunmaz. Yeni sürümü oynayanların verisi Firestore'dan kendiliğinden gelir (internet gerekir), **Yenile** günceller.

GameAnalytics CSV'leri `dashboard/data/csv-paket.js` ile repodan gelir, herkes aynısını görür. Repo herkese açık olduğu için bu sayılar da herkese açıktır. Yeni CSV'leri paylaşmak için `dashboard/data/` içine koyun, `node dashboard/csv-paket.mjs` çalıştırın, commit edip push edin.

Yalnızca kendinize dosya eklemek için sayfaya sürükleyin ya da **CSV ekle** deyin. Tarayıcı onları hatırlar, aynı adlı dosya paketteki dosyanın yerine geçer. **Eklenenleri temizle** yalnızca bunları siler.

Hiç CSV yoksa panel baştan sona **ÖRNEK VERİ** gösterir ve üstte sarı şerit çıkar. İlk gerçek veriyle örnek tamamen kalkar; eksik kalan bölümler boş görünür, gerçek ve uydurma sayılar aynı sayfada asla karışmaz.
Her bölümün yanındaki etiket verinin nereden geldiğini söyler: `Canlı`, `CSV · tarih`, `ÖRNEK`, `Veri yok`.

**Firebase'ten otomatik veri, Meta reklam verisi ya da GameAnalytics PipelineIQ API'si için** anahtarlar gizli kalsın diye küçük bir sunucu gerekir (Node.js 18+):

```bash
node dashboard/server.mjs
```

Windows'ta `dashboard/Panel.bat` de aynısını yapar. Sayfa aynıdır, CSV'ler bu adreste ayrıca eklenir (`http://localhost:5173`, tarayıcı dosyadan açılan sayfayla paylaşmaz).

## Hangi veri nereden gelir

| İstenen | GA Explore CSV (Free ve Pro) | GA Metrics API (yalnızca PipelineIQ) | Meta API |
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

- **Planımız AnalyticsIQ Pro:** API vermez, veri elle güncellenir: Explore'dan CSV indirip panele sürüklersiniz. `GA_API_KEY` boş kalır.
- **Metrics API** yalnızca PipelineIQ planında var (fiyat listesinde "$499/ay'dan başlayan"). Olursa `.env` ile canlı gelir, ama level ve olay yine CSV ister. [API şeması](https://docs.gameanalytics.com/redocusaurus/metrics-api.yaml)
- **Claude connector (`mcp.gameanalytics.com`):** AnalyticsIQ Pro ile çalışmaz. Bağlanır ama oyun listesi boş döner, sorgu `403: must have PipelineIQ access` verir (2 Kasım 2026, oyun 354324). MCP Access ($149/ay) ya da PipelineIQ Pro ister; level ve olay verisi onda da yok.
- **Meta:** eski Facebook Analytics 2021'de kapandı. Oyunun Meta'ya gönderdiği olaylar Events Manager'da görünür; panel Meta'dan yalnızca reklam verisini okur.
- **İki oyun:** GameAnalytics'te Android (`PixelGame`) ve iOS (`LandFlowIOS`) ayrı oyunlardır. Panel tek oyun gösterir; `data/` içine ikisini karıştırmayın.

## Oyun gerçekte ne gönderiyor

`Assets/Scripts/LandFlowAnalytics.cs` (GameAnalytics):

| Olay | Biçim | Not |
|---|---|---|
| Level başladı | `Start:Level_015:Hayalet` | |
| Level bitti | `Complete:Level_015:Hayalet`, score = geçen saniye | Level süresi buradan |
| Level kaybedildi | `Fail:Level_015:Hayalet`, score = geçen saniye | |
| Tasarım olayları | `Speed:2X`, `Speed:3X`, `Speed:Depleted` | |
| Level yeniden başlatıldı | `Level:Restart:Level_015`, değer = geçen saniye | Eski sürümler `Level:Restart` gönderir, süre ve level yok |
| Level sürerken arka plana geçildi | `Level:Quit:Level_015`, değer = geçen saniye | Deneme başına bir kez; oyuncu dönüp devam etmiş olabilir |
| Oturum | GA SDK'sı otomatik gönderir | DAU, süre ve retention buradan |

GameAnalytics'te **test cihazlarını ayırmak** için her olay custom dimension 01 taşır: editör, development build ve `LandFlowAnalytics.TestDeviceIds` listesindeki cihazlar `tester`, diğerleri `player`. GA panelinde Filter → Custom dimension 01 → `player` seçin. Yeni test cihazı eklerken kimliğin ilk karakterlerini hem bu listeye hem `panel.html` içindeki `TEST_DEVICE_IDS`'e yazın.

**İlk deneyim (FTUE) olayları**, oyuncu başına bir kez: `FTUE:Tutorial:Shown` (Level 1'de el işareti göründü) ve `FTUE:Tutorial:Done` (ilk gemi gönderildi, değer = geçen saniye). Shown ile Done arasındaki fark, tutorial'da takılıp çıkan oyuncu sayısıdır.

`Assets/Scripts/LandFlowMeta.cs` (Meta): `FB.ActivateApp()`, SDK'nın otomatik olayları Level 5, 10, 20 bitince oyuncu başına birer kez `fb_mobile_level_achieved` (`fb_level` = level numarası) ve her denemede `LevelComplete`, `LevelFail`, `LevelRestart`, `LevelQuit` özel olayları (`fb_level` = `Level_015`, `valueToSum` = geçen saniye). Bu olaylar Events Manager'da görünür, panele gelmez; level bazında süre analizi için GameAnalytics kullanılır.

`Assets/Scripts/LandFlowFirebase.cs` (Firebase / Google Analytics): `level_start`, `level_complete`, `level_fail`, `level_restart`, `level_quit`, parametreleri `level_id` ve `seconds`. Veri toplama izin gelene kadar kapalıdır, reklam kimliği toplanmaz (Android). Panele bağlantısı henüz yok.

Hepsi veri izni veren oyuncularla sınırlıdır (`LandFlowPrivacy`), bu yüzden sayılar mağaza indirmelerinden düşük olabilir.

Süre arka planda geçen zamanı saymaz. Eski sürümlerde yeni oyuncunun ilk levelinde `Start` gitmiyor ve süre uygulama açılışından sayılıyordu; bu yüzden eski Level 1 süreleri şişiktir. Eski sürümlerde adında Türkçe harf olan levellerin (111 adın 41'i, ör. `Balık`) `Start`, `Complete` ve `Fail` olayları GameAnalytics tarafından reddediliyordu; bu levellerin verisi eski CSV'lerde eksiktir, ad artık `Balik` biçiminde gider.

**Ölçülemeyenler ve oyuna eklenmesi gerekenler** panelin en altında listelidir: kampanya kaynağı (MMP yok), gelir olayları, sabit `Build 0.1`.

## Firestore

Oyun, Firestore SDK'sı olmadan REST ile oyuncu başına özet yazar ([LandFlowFirestore.cs](../Assets/Scripts/LandFlowFirestore.cs)): `players/{rastgele kimlik}` içinde `firstSeen`, `lastSeen`, `platform`, `version`, `device` (telefon modeli), `country` (cihazın bölge ayarı, konum değil), `events` (`Speed:2X` gibi olay sayıları), gün başına oturum ve süre, level başına ad, `start`, `complete`, `fail`, `restart`, `quit` sayıları ve saniye toplamları. Panel bunu doğrudan okur.

- **Cihazlar:** Firestore'a yazan her cihaz ekip cihazları dahil ayrı satırda görünür, tarih seçiciden etkilenmez. Satıra tıklayınca o cihazın saatli aktivite akışı, günleri ve level başına toplamları açılır. **Tümü** ilk veri gününden bugüne kadar gösterir.
- **Test cihazları:** `panel.html` içindeki `TEST_DEVICE_IDS` listesindeki cihazlar sayılardan çıkarılır, Cihazlar tablosunda `Test` etiketiyle görünür. Üstteki düğme dahil ya da hariç seçer. Eski CSV'lerde cihaz bilgisi olmadığı için oradaki test denemeleri çıkarılamaz.
- **Level sırası:** Level kimliği (`Level_001`) o anki sıradan gelir, sıra değişince başka bir leveli gösterir. Bu yüzden level tablosu varsayılan olarak yalnızca güncel sırayı (Firestore) gösterir; **Eski CSV** ayrı seçenektir ve toplanmaz. Sıra yeniden değişirse Firestore kayıtları da karışır.
- **Birleştirme:** Günlük veride Firestore'un olduğu günler onu, diğer günler CSV'yi gösterir. Ülke ve olay tabloları CSV ile toplanır, sıradan etkilenmez.
- **Retention:** Dönemde en az 10 yeni oyuncu olana kadar CSV'den gelir, sonra Firestore'dan. Firestore verisi varsa panel bugünü de gösterir; retention ve kurulumdan sonraki oynama yalnızca bitmiş günleri sayar.
- **Birleşen:** Ülke ve olay tabloları CSV ile toplanır; ülkede oynama süresi ve D1 yalnızca kendi kaynağından gelir.
- **Güvenlik:** Giriş (Auth) bilinçli olarak kullanılmıyor. Kurallar silmeyi ve başka alanlara yazmayı kapatır, ama **proje kimliğini bilen herkes bu kayıtları okuyabilir ve değiştirebilir**, panel sayıları sahte veriyle bozulabilir. Oyuncu verisinde ad, e-posta gibi kişisel alan tutulmaz.
- **Aktivite akışı:** `players/{kimlik}/activity/{gün}` içinde `SS:dd:ss|olay|level|saniye` satırları (saat UTC). Panel yalnızca satıra tıklayınca okur. Herkes okuyabildiği için kişisel bilgi yazılmaz.
- **Kota:** Ücretsiz planda günde 50.000 okuma ve 20.000 yazma. Panel her açılışta oyuncu başına bir, satıra tıklayınca aktif gün başına bir okuma yapar. Oyun deneme başına bir istek atar, kotadan iki yazma düşer; günde yaklaşık 10.000 deneme yazılabilir.
- **Kurallar:** Firebase Console → Firestore → Rules, repoda değil. `activity` için yalnızca `events` izinlidir. İzinli alanlar: `firstSeen`, `lastSeen`, `platform`, `version`, `device`, `country`, `events`, `days`, `levels`. Oyuna yeni kök alan eklerken önce kuralı güncelleyin, yoksa yazma reddedilir.

## Firebase

Panel sunucuyla açılınca Firebase verisini kendisi çeker, CSV gerekmez. Aynı veri CSV'de de varsa CSV önceliklidir: bu adreste eski CSV kaldıysa **Temizle** deyin.

- **Gelen:** DAU, MAU, yeni oyuncu, oturum, oynama süresi, retention, ülke, platform, olay sayıları, level başına başlama, bitirme, kayıp, Restart, Quit ve süreler.
- **Gelmeyen:** Kurulumdan N gün sonra toplam oynama ve `Speed` olayları; ülke başına D1. Bunlar için CSV kullanılır.
- **Gecikme:** Raporlar gecikmeli dolar, bugünün verisi genelde ertesi gün görünür. Olaylar da toplu gönderilir.
- **Az oyuncu:** Google bazı satırları gizleyebilir; olursa panelin üstünde yazar.
- **Sayılar:** GameAnalytics ile birebir tutmaz, iki servis farklı sayar. Oynama süresi Firebase'te ön planda geçen süredir.
- **Level alanları:** Google Analytics → Custom definitions'ta `level_id` (Event boyutu) ve `seconds` (Event metriği, Seconds) tanımlı olmalı. `http://localhost:5173/api/firebase/check` son 30 dakikanın olaylarını ve tanımları gösterir.

## Türkiye ve diğer ülkeler

Türkiye'deki oyuncuların çoğu ekip ve tanıdık olabileceği için bu bölüm Türkiye, Türkiye dışı ve tümünü yan yana koyar: oyuncu, yeni oyuncu, oynama süresi, oturum, D1/D3/D7 retention, Level 1 bitirme, kazanma oranı, yarıda bırakma ve en ileri level; ayrıca iki grubun günlük aktif oyuncusu ve level hunisi. Yalnızca Firestore'daki oyuncu başına veriden hesaplanır (GameAnalytics CSV'si oyuncuyu ülkeyle eşleyemez). Her oranın yanında kaç kişiden hesaplandığı yazar. Ülkesi bilinmeyen oyuncu yalnızca Tümü'ne girer.

## Meta Ads

Sunucu gerekmez: üstteki **Meta Ads** satırından **Bağla** deyin, erişim anahtarını ve reklam hesabı kimliğini girin. Anahtar yalnızca o tarayıcıda saklanır, repoya girmez, istekler doğrudan Meta'ya gider. Her kişi ve her tarayıcı ayrı girer. Paylaşılan bilgisayarda kullanmayın, **Bağlantıyı kes** anahtarı siler. Sunucu `.env` içinde Meta anahtarı bulursa o kullanılır.

## Anahtarlar

```bash
cp dashboard/.env.example dashboard/.env
```

`.env` git'e girmez ve tarayıcıya gönderilmez. Oyundaki GA game key / secret key veri okumaz, burada kullanılmaz.

**Meta:**
1. business.facebook.com → İşletme ayarları → Sistem kullanıcıları → kullanıcı ekleyin, reklam hesabını atayın.
2. Token oluşturun, `ads_read` iznini seçin, `META_ACCESS_TOKEN` olarak yazın.
   - Son kullanma tarihi olarak **Hiçbir zaman** seçin. Graph API Explorer'dan alınan anahtar 1-2 saatte, uzun ömürlüsü 60 günde düşer ve panel "süresi doldu" hatası verir. Sistem kullanıcısı anahtarı düşmez.
   - Panelde bağladıysanız hata satırındaki **Yeni anahtar** ile yalnızca anahtarı yenileyin, hesap kimliği hazır gelir.
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
| `Custom event count per user` | Event dimension → Event id 03, filtre Event id 02 = `Restart` | Count | **Dosya adında `restart` geçmeli** |
| `Custom event count per user` | Event dimension → Event id 03, filtre Event id 02 = `Restart` | Mean value | **Dosya adında `restart` geçmeli** |
| `Custom event count per user` | Event dimension → Event id 03, filtre Event id 02 = `Quit` | Count | **Dosya adında `cikis` geçmeli** |
| `Custom event count per user` | Event dimension → Event id 03, filtre Event id 02 = `Quit` | Mean value | **Dosya adında `cikis` geçmeli** |

Ortalama skorun başlığı iki durumda da aynı olduğu için tek istisna o iki dosyanın adıdır; Restart ve Quit dosyaları için de aynısı geçerlidir. Aynı ada sahip dosyalar birbirinin yerine geçtiği için indirirken farklı adla kaydedin. Tanınmayan dosya panelin üstünde uyarı olarak görünür.
Sık kullanacağınız sorguları Explore'da **Save** ile saklayabilirsiniz. Level, olay ve ülke dosyaları indirirken seçilen dönemi gösterir; paneldeki tarih seçici onları etkilemez, etiketteki tarih dosyanın kaydedildiği gündür.

## Sayılar nasıl hesaplanıyor

- **Retention:** kurulum gününe göre kohortların yeni oyuncu sayısıyla ağırlıklı ortalaması, N günü dolmamış kohortlar hariç. API ile dönemde yeterli kohort yoksa önceki kohortlar da kullanılır.
- **Churn:** `100 − N. gün retention`. Kalıcı kaybı değil, o gün dönmeyenleri gösterir.
- **Ülke D1:** yalnızca API ile, son en çok 14 kohortun düz ortalaması.
- **MAU:** dönemin son gününden geriye 30 gün içindeki tekil oyuncu.
- **Günlük oynama:** Aktif oyuncunun bir günde oynadığı süre; günler oyuncu sayısıyla ağırlıklıdır.
- **Oyuncu başına toplam:** 30 günün toplam oynaması ÷ aylık aktif oyuncu. GameAnalytics'in özet ekranındaki `Playtime` buna daha yakın görünüyor, tarih aralığına göre fark edebilir.
- **Ulaşan:** levele başlayan tekil oyuncu; yoksa önceki leveli bitirenler (`~` ile işaretli).
- **Toplam süre:** `bitirme × ort. süre + kayıp × ort. süre + restart × ort. süre`. Quit dahil değildir, çünkü dönüp devam eden oyuncunun süresi zaten bitirme ya da kayıpta sayılır.
- **Kazanma %:** `bitirme ÷ (bitirme + kayıp)`. Oyunu açıp kapatmak ve Restart da Start ürettiği için başlama sayısı kullanılmaz; Restart ile bırakılan denemeler orana girmez.
- **Tıkanma ve Kopma:** kazanma oranı %40 altı ya da ulaşanların %15'ten fazlasının bitirmemesi. Bitirme ve kayıp toplamı 5'ten az olan level işaretlenmez ve özet kutularına girmez. Eşikler panelde işaret olsun diye seçildi, sektör ölçütü değildir.
- **Reklam dışı oyuncu:** `GA yeni oyuncu − Meta kurulum`. İki kaynak farklı sayar, kaba tahmindir.

## Doğrulama durumu

- **GA CSV:** başlıklar, alt satırlar ve toplamlar gerçek hesaptan (`PixelGame`, Ekim 2026) görüldü. Sayıların saniye olduğu GA arayüzüyle karşılaştırıldı.
- **Dosyadan açma:** Linux'ta Chrome ile `file://` üzerinden sürükle-bırak, yenileme ve temizleme denendi. Windows'ta ve Firefox'ta denenmedi.
- **Restart ve Quit CSV'leri:** Yeni olaylar henüz yayında olmadığı için gerçek dosya görülmedi. `Event id 03` sütunu ve ortalama değer başlığı (`Design (mean…`) tahmindir, tanınmayan dosya panelde uyarı olarak çıkar. Örnek dosyayla sınandı.
- **Firestore:** Okuma ve hesaplar örnek veriyle sınandı. Oyunun yazması henüz gerçek cihazda görülmedi.
- **Firebase:** olaylar Waydroid'den Realtime'a ulaştı ve servis hesabıyla giriş çalıştı (Kasım 2026). Panelin Data API'den veri okuması henüz doğrulanmadı.
- **GA Metrics API ve Meta:** gerçek anahtarla **denenmedi**. İstekler API şemasına karşı denetlendi, panel şema biçiminde yanıt veren sahte sunucuyla sınandı.
- İlk gerçek Meta bağlantısında `actions` içindeki kurulum türü (`mobile_app_install` ve benzerleri) kontrol edilmeli.
