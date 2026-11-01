# LandFlow Analiz Paneli

Meta (Facebook) reklam verilerini ve GameAnalytics oyuncu verilerini **tek sayfada** toplayan panel.
Kurulum gerektirmez (npm paketi yok), sadece **Node.js 18+** yeterli.

## Panelde neler var

| Bölüm | İçerik | Kaynak |
|---|---|---|
| **Oyun karnesi** | D1/D7 retention, günlük oynama süresi, CPI, CTR, 10. levele ulaşma, tıkanma noktası sayısı. Her biri yeşil, sarı veya kırmızı olarak işaretlenir ve kısa açıklaması yanında yazar. | İkisi |
| **Reklam ve edinme** | Harcama, gösterim, erişilen kişi, sıklık, tıklama/CTR, Meta kurulumu, CPI, CPM, GA yeni oyuncu, tahmini organik oyuncu. Günlük kurulum ve harcama grafikleri. | Meta + GA |
| **Oyuncu davranışı** | DAU, D1/D3/D7/D14/D30 retention eğrisi, oturum/oyuncu, ortalama oturum süresi, oyuncu başına günlük oynama süresi, oyun içi olaylar (`Level:Restart`, `Speed:2X`…) | GA |
| **Level analizi** | Her level için ulaşan oyuncu, başlama, bitirme, kayıp, kazanma %, deneme/bitirme, ortalama bitirme süresi ve bırakan %. Tıkanma ve kopma noktaları otomatik işaretlenir. Kazanma oranı grafiği ve oyuncu hunisi de bu bölümde. | GA |
| **Kampanyalar** | Kampanya bazında harcama, gösterim, erişim, CTR, kurulum, CPI, CPM | Meta |

Tarih aralığı 7, 14, 30 ya da 90 gün seçilebilir. Grafiklerde imleç üstüne gelince değerler görünür, tablolar sütun başlığına tıklanarak sıralanır.

## Unity içinden açma (önerilen)

Unity'de üst menüden **PixelGame → 📈 LandFlow Analiz Paneli** penceresini açın.
1. Meta ve GameAnalytics anahtarlarını yazıp **Anahtarları Kaydet** deyin. Pencere bunları `dashboard/.env` dosyasına yazar.
2. **Başlat ve Aç** dediğinizde sunucu arka planda çalışır ve panel tarayıcıda açılır. Unity kapanınca sunucu da kapanır.

Tek tıkla açmak için **PixelGame → 📈 LandFlow Analiz Panelini Tarayıcıda Aç** menüsünü kullanabilirsiniz.
Bilgisayarda [Node.js](https://nodejs.org) (LTS) kurulu olmalı. Kurduktan sonra Unity'yi yeniden başlatın.

## Elle çalıştırma

```bash
cd dashboard
cp .env.example .env      # sonra .env içini doldurun
node server.mjs
```

Ardından tarayıcıda **http://localhost:5173** adresini açın.

Anahtarlar girilmeden de çalışır. O durumda üstte **ÖRNEK VERİ** etiketi görünür ve panel uydurma verilerle dolar,
böylece neyin nerede görüneceğini önceden görebilirsiniz. Hangi kaynak canlı, hangisi örnek ya da hatalı, sayfanın üstündeki etiketlerde yazar.

> Anahtarlar sadece bu bilgisayardaki `.env` dosyasında durur, tarayıcıya ve git'e gitmez (`dashboard/.gitignore`).
> Oyundaki GameAnalytics *game key / secret key* bu iş için kullanılmaz. Veri okumak için ayrı bir API anahtarı gerekir (aşağıda).

## Anahtarları alma

### Meta (reklam verileri)
1. **business.facebook.com → İşletme ayarları → Kullanıcılar → Sistem kullanıcıları** sayfasında bir sistem kullanıcısı ekleyin.
2. Bu kullanıcıya reklam hesabını ve *Land Flow* uygulamasını (App ID `1406362561248360`) atayın.
3. **Token oluştur** deyin ve `ads_read` iznini seçin. Çıkan değeri `META_ACCESS_TOKEN` olarak yazın.
4. Ads Manager adres çubuğundaki `act=` değerini `META_AD_ACCOUNT_ID` olarak yazın. Reklam hesabının para birimini de `META_CURRENCY` olarak girin.

Kurulum sayısı, reklamdan gelen `mobile_app_install` olaylarından alınır. Bunun için oyunda Meta SDK'nın açık olması gerekir, `LandFlowMeta.cs` bu işi yapıyor.

### GameAnalytics (oyuncu verileri)
1. GameAnalytics panelinde **Organization settings → Metrics API** sekmesinden bir anahtar oluşturun ve `GA_API_KEY` olarak yazın.
   *Metrics API, GameAnalytics'in ücretli **Pro** planında var.*
2. Oyunu açtığınızda adres çubuğundaki `/game/<sayı>/` değerini `GA_GAME_ID` olarak yazın.

## GameAnalytics metrik adları uyuşmazsa

GA Metrics API'deki metrik ve bölme (split) adları paneldeki varsayılanlardan farklıysa, ilgili bölüm boş kalır ve üstte
**"GA eksik metrikler"** uyarısı çıkar. Düzeltmek için şu adımları izleyin:

1. **http://localhost:5173/api/ga/metrics** adresini açın. Bu sayfa GA'nın desteklediği metrik adlarını listeler.
2. **http://localhost:5173/api/ga/raw?metric=METRIK_ADI&split=ALAN1,ALAN2** adresiyle bir metriğin ham yanıtına bakın.
3. Doğru adları `.env` içindeki `GA_METRIC_*` ve `GA_*_SPLIT` satırlarına yazın, sonra sunucuyu yeniden başlatın.

## Level verisi (CSV ile)

Level verisi API'den gelmezse GA panelinden dışa aktardığınız veriyi `data/levels.csv` dosyasına koyabilirsiniz. Bu dosya varsa her zaman önce o kullanılır.
Biçim için `data/levels.example.csv` dosyasına bakın:

```
id,name,starts,completes,fails,avg_complete_sec,avg_fail_sec,players
Level_001,Kedi,1250,1180,60,52,38,1200
```

`players` (levele başlayan tekil oyuncu) boş bırakılırsa, önceki leveli bitirenlerin sayısı kullanılır.

## Sayılar nasıl hesaplanıyor

- **Level olayları** oyunda `LandFlowAnalytics.cs` tarafından `Level_015` + level adı biçiminde gönderilir.
  `Complete` ve `Fail` olayları, levelde geçen süreyi (saniye) skor olarak taşır.
- **Ulaşan %**: Level 1'e başlayan oyunculara göre o levele gelenlerin oranı.
- **Bırakan %**: Levele ulaşıp seçilen dönemde bitirmeyenlerin oranı.
- **Tıkanma**: Kazanma oranı %40'ın altında. **Kopma**: Ulaşanların %15'inden fazlası bitirmiyor.
- **Günlük oynama süresi** = oturum sayısı × ortalama oturum süresi ÷ DAU.
- **Organik oyuncu** ≈ GA yeni oyuncu − Meta kurulumu. Kabaca bir tahmindir, çünkü iki kaynak aynı oyuncuyu farklı sayabilir.
- GA yalnızca **veri iznini veren** oyuncuları sayar (`LandFlowPrivacy`). Bu yüzden GA'daki yeni oyuncu sayısı mağazadaki indirme sayısından düşük olabilir.
  Gerçek mağaza indirmeleri App Store Connect ve Google Play Console'dadır, bu panel onları çekmez.
- Karnedeki eşikler, hibrit-casual bulmaca oyunları için yaklaşık sektör değerleridir. Kesin kural olarak değil, yön göstermek için kullanın.

## Başka cihazdan açmak

`.env` içinde `HOST=0.0.0.0` yapın. Aynı Wi-Fi'daki telefondan `http://<bilgisayar-IP>:5173` ile açılır.
Paneli internete açmayın. Şifre koruması olmadığı için reklam harcamaları herkese görünür.
