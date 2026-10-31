# PixelGame (LandFlow) — Level Tasarım Rehberi

Bu doküman, PixelGame projesinde **pixel art resimlerden oynanabilir level** üreten bir yapay zeka için yazıldı.
Oyunun kurallarını, level dosyasının yapısını, zorluk ayarını, araçları ve proje sahibinin (Emre) tercihlerini
eksiksiz anlatır. **İşe başlamadan önce baştan sona oku.** Burada yazanlar, aylarca deneme ile ve oyuncu
geri bildirimiyle bulundu; "daha iyi bildiğini" düşünsen bile kurallara uy, emin olmadığında kullanıcıya sor.

Araçlar: `docs/level-tasarim/tools/` (simülatör, kuyruk tasarlayıcı, asset yazıcı, resim zenginleştirici, sıralama).
Tüm komutlar proje kökünden (`PixelGame/`) çalıştırılır.

---

## 0. Kısa özet (en önemli 15 kural)

1. Her level = bir 32×32 PNG + bir `PixelLevelData` asset'i (`Assets/Levels/Level_<N>_<Ad>.asset`) + LevelSequence'te bir satır.
2. Resmin her opak pikseli bir küptür. Her rengin küp sayısı = o rengin gemilerinin kapasite toplamı. **Bir küp bile eksik/fazla olursa level bitmez.**
3. Oyun iki rengi kanal başına **0.04 (≈10/255) toleransla** aynı sayar. Bu kadar yakın iki renk olursa sayılar bozulur. Kanal başına 14'ten yakın tonlar da oyuncu için ayırt edilemez → kullanma.
4. 5 slot sabittir. Kuyrukta sütun (şerit) sayısı `m_PoolColumns`: **çoğunlukla 4**, küçük/az gemili levellerde 3; **Kolay levellerde her zaman 3**. Satır `m_PoolRows: 3`.
5. Gemi kapasitesi en fazla **30** (1024 küplük "büyük" levellerde **40**). Renk başına en fazla **1 küsuratlı** (10'un katı olmayan) gemi.
6. Aynı sütunda aynı renk arka arkaya gelmez.
7. Halat **sadece ikili**, iki gemi **farklı renk**, yan yana komşu sütunda ya da başlangıç havuzunda alt alta.
8. **Halatlı gemi dolunca kalkmaz; partneri de dolana kadar slotu işgal eder.** Zorluğun en büyük kaynağı ve en büyük tuzağı budur.
9. Gizli gemi (`isHidden`) ön sırada başlamaz ve halatlı olmaz.
10. Açılış okunur olmalı: ilk 3 satırda en fazla 2 halat, ön sıranın en fazla yarısı halatlı. **Ön sırada en fazla 1, ilk iki sırada en fazla 3 gemi "dış katman" renginden olur** (başta açıkta ≥6 küpü olan renk: genelde outline). Yoksa oyuncu ekrana bakmadan öndeki gemileri yollar ve level kendini oynar (kullanıcı şikayeti, 2026-10-31).
11. Zorluğu **simülatörle ölç**; Zor level "dikkatli oynayan oyuncu %80–95 geçer" olmalı, "şansa bağlı" olmamalı (zamanlama testi).
12. 3–4 renkli düz ikonlar olduğu gibi **her zaman Kolay**dır; Orta/Zor için resmi zenginleştir (outline, gölge, iç halka, arka plan) ve **önce/sonra görseliyle kullanıcıya onaylat.**
13. Level sırasını **dosyayı elle düzenleyerek değil, Unity içinden** ayarla (§9). Kurallar: iki Zor yan yana olmaz, 3 büyük resim art arda olmaz.
14. `m_DifficultyManual: 1` olmalı, yoksa Unity zorluğu kendi kafasına göre değiştirir.
15. **Kullanıcı açıkça istemeden commit/push yapma.** Projeye ekran görüntüsü/önizleme resmi koyma (önizlemeler geçici klasörde kalır).

---

## 1. Oyun nasıl oynanır (birebir kurallar)

Kod referansları: `Assets/Scripts/CollectionOrder.cs`, `ShipQueuePool.cs`, `ShipDispatcher.cs`, `ShipController.cs`, `PixelLevelData.cs`.

### 1.1 Tahta
- PNG'nin her opak pikseli renkli bir küp. Izgara koordinatı = doku koordinatı: **x soldan sağa, y AŞAĞIDAN yukarı** (Unity `GetPixel(x, y)`; PNG'de en alt satır y=0).
- Küp **alınabilir**, eğer 4 komşusundan (sol/sağ/alt/üst) biri "dış hava"ya bağlıysa. Dış hava = tahtanın 1 hücre genişletilmiş çerçevesinden 4 yönlü flood fill ile ulaşılan boş hücreler. **Kapalı iç boşluklar dış hava değildir**; çapraz komşuluk sayılmaz.
- Bu yüzden bir rengin halkası içindeki başka renk, halka kırılana kadar alınamaz → **"kilit"**. Zorluk buradan doğar.
- Alma sırası (aynı renkten birden çok alınabilir küp varsa): **en alt satır önce**; aynı satırda o satırın uçlarına (sol ve sağ en dıştaki dolu hücreye) **en yakın** olan; eşitlikte **sol**, sonra x küçük. Her küp alınınca liste yeniden hesaplanır.

### 1.2 Gemiler (oyunda kum üstünde konteyner olarak görünür)
- Level'ın gemi listesi `m_WagonSequence`. **i. gemi `i % sütunSayısı` numaralı şeride gider**; her şerit bir kuyruktur. Ekranda her şeridin ilk ~3 gemisi görünür (ekran boyuna göre en az 2, uzun ekranda 3; tasarımı 3'e göre yap).
- Oyuncu yalnızca bir şeridin **en öndeki** gemisine dokunup onu boş bir slota gönderebilir. **5 slot** vardır (`PixelLevelData.FixedSlotCount = 5`, level'dan değiştirilemez).
- Slottaki gemi, kendi rengindeki alınabilir küpleri denizcilerle toplar (denizci başına en fazla 5 küplük taşıma, gemiler paralel çalışır). Kapasitesi dolunca kalkar, slot boşalır.
- Gemi rengine ait alınabilir küp yoksa **slotta bekler** (slotu işgal eder).
- **Kaybetme:** gönderilebilecek gemi yok (5 slot dolu ya da öndeki tek seçenek 2 slot isteyen halatlı çift) VE hiçbir gemi toplayamıyor ve kalkamıyor → ~1 sn sonra level başarısız.
- **Otomatik yerleştirme:** kuyrukta yeni gemi kalmadığında ve bekleyen gemiler boş slotlara sığıyorsa oyun kalanları kendisi gönderir (`m_DisableAutoPlaceTurbo: 1` ile kapatılır; genelde açık kalır).

### 1.3 Halat (linkId)
- Aynı `linkId`'ye sahip **iki** gemi birbirine halatla bağlıdır (`MaxLinkGroupSize = 2`; kod daha büyük grupları da destekler ama kullanıcı **sadece ikili** istiyor).
- Geçerli konumlar: **yan yana** (i ve i+1, i son sütunda değil) veya **alt alta** (i ve i+sütun, ama ikisi de başlangıç havuzunda: i+sütun < sütun×3). Level Designer başka eşleşmeye izin vermez.
- Gönderme: ikisi de görünür olmalı ve önlerinde kendi grupları dışında gemi olmamalı (alt alta çiftte üstteki öndeyse alttaki de "önü açık" sayılır). **2 boş slot** gerekir; oyun bitişik slot açmak için tekli gemileri kaydırır.
- **Kritik:** halatlı gemi dolunca **tek başına kalkmaz**, partneri dolana kadar slotta bekler; sonra ikisi birlikte kalkar. Bir halatlı çiftin bir üyesinin rengi henüz kilitliyse, iki slot uzun süre kilitlenir. Bu kural yüzünden fazla halatlı leveller "imkansız" olabilir.

### 1.4 Gizli gemi (isHidden, "?" konteyner)
- Rengi soru işaretiyle örtülü gelir. Arka sıralarda görülmüş bir gizli gemi **ön sıraya gelince açılır**. Ön sırada **başlayan** gizli gemi slota yanaşana kadar açılmaz → oyuncuya haksız. Kural: **gizli gemi ilk satırda olmaz, halatlı olmaz.**
- Gizli gemiler oyuncunun ileriyi planlamasını zorlaştırır; simülatör bunun etkisini ölçmez (botlar zaten öne bakar).

### 1.5 Gizli küp (mystery cube)
- `m_MysteryCubeCoordinates` listesindeki hücreler ("?" ile) gri/mor görünür. `m_MysteryRevealCondition: 0` (WhenExposed): küp **alınabilir hale geldiği anda** rengi açılır. Yani toplamayı değiştirmez, sadece oyuncunun iç kısmı önceden görüp plan yapmasını engeller.
- Koordinatlar doku koordinatıdır (y aşağıdan). `m_MysteryCubeColor` paletteki hiçbir renge yakın olmamalı.
- Kullanım: Zor levellerde ve bazı Ortalarda **küplerin ~%4–5'i**, iç bölgede renk sınırlarına denk gelen 2–3 küçük küme ("abartmadan"). Simülatör etkisini ölçemez.

---

## 2. Level dosyası (PixelLevelData) yapısı

Konum: `Assets/Levels/Level_<N>_<Ad>.asset` (+ `.meta`, benzersiz guid). YAML; ilgili alanlar:

| Alan | Değer / anlam |
|---|---|
| `m_Name` | Dosya adıyla aynı: `Level_<N>_<Ad>` (N dosya adında kalır; sıra değişince dosya adı değişmez, sorun değil) |
| `m_LevelName` | Görünen ad; Türkçe karakterler YAML kaçışıyla: `"\xC7ilek"`, `"Uğur B\xF6ceği"` |
| `m_LevelIndex` | **LevelSequence'teki konumu (1'den başlar).** Level Designer levelleri bu sayıya göre sıralar. Tekrar olmamalı. |
| `m_DifficultyTier` | 0 Kolay, 1 Orta, 2 Zor |
| `m_DifficultyManual` | **1** (0 ise Unity analiz edip tier'ı üzerine yazar) |
| `m_LevelTexture`, `m_OriginalSourceTexture` | PNG'nin guid'i (ikisi de aynı) |
| `m_UseNativeResolution` | 1 |
| `m_ColorPalette` | Her renk: `originalColor`, `targetColor` (aynı), `pixelCount`. **Piksel sayısına göre azalan sırada.** |
| `m_PoolColumns` | Şerit sayısı: 4 (çoğunluk) veya 3 |
| `m_PoolRows` | 3 |
| `m_UseCustomWagonSequence` | **1** |
| `m_WagonSequence` | Gemi listesi: `wagonColor` (paletteki renk), `capacity`, `paletteIndex`, `label: Vagon`, `linkId` (0 = halatsız, aynı sayı = çift), `isHidden` (0/1) |
| `m_TruckCapacity` | 30 (büyük levelde 40) — özel sırada sadece bilgi amaçlı |
| `m_MysteryCubeCoordinates` | `- {x: 4, y: 12}` listesi ya da `[]` |
| `m_MysteryRevealCondition` | 0 |
| `m_MysteryCubeColor` | `{r: 0.415686, g: 0.352941, b: 0.478431, a: 1}` gibi, paletten uzak |
| `m_SlotCount` | Yok sayılır (her zaman 5) |

**PNG ayarları (`.png.meta`):** `isReadable: 1` (Read/Write açık, yoksa oyun pikselleri okuyamaz), `filterMode: 0` (Point), `nPOTScale: 0`, varsayılan platformda `textureCompression: 0` (diğer platform satırları Saturn'deki gibi kalabilir). `level_asset.py new` PNG'nin meta'sı yoksa `Assets/32x32/Saturn.png.meta`'dan kopyalayıp yeni guid verir. `Assets/30.10.2026/` klasöründeki resimlerin meta'ları zaten doğru (readable + point).

**`paletteIndex` ile değil renk koduyla eşleştir:** Aynı piksel sayısına sahip renklerde Unity'nin palet sırası değişebilir. Araçlar paleti `(-pikselSayısı, renk)` sırasıyla yazar ve gemileri buna göre indeksler; paleti elle düzenlersen gemi `paletteIndex`'lerini de güncelle.

**Kaçınılacak:** Unity'nin `ExtractPaletteFromTexture` / "Kademeleri Analizden Ata" / Level Designer otomatik dağıtıcıları; senin tasarladığın sırayı ve kapasiteleri ezerler.

---

## 3. Resim kuralları

### 3.1 Teknik
- 32×32 (daha küçük kare de olur ama tahtada küçük görünür; erken levellerde bile en az ~15 piksel genişlik).
- Alfa sadece 0 ya da 255.
- **6–11 renk.** 10 pikselin altında renk bırakma (en yakın renge kat ya da büyüt).
- İki renk kanal başına <0.04 farklıysa oyun onları **aynı renk** sayar → sayılar tutmaz (gerçek hata: Kardan Adam'da şapka (40,36,48) ile outline (34,26,40) birleşti). Kanal başına <14 farklı tonlar da oyuncu için aynıdır → kullanma. Aynı rengin birbirine yakın 3 tonu gibi "renk ailesi" yapma (Yengeç: 3 kırmızı tonu ayırt edilemedi).
- Outline rengi, figürün rengine yakın olmasın (yeşil kaktüse koyu yeşil outline "üçüncü yeşil" gibi okundu). Proje genelinde outline: koyu mor-siyah `(34, 26, 40)`.
- Bir rengin resmin ~%90'ı olduğu level yapma ("amaçsız, düz").

### 3.2 Büyüklük
- **Büyük** = ≥900 küp (pratikte 32×32 dolu, 1024 küp). **Küçük** = arka plansız figür (300–750 küp).
- Kullanıcı "1024'ler üst üste gelmesin" istiyor (sıralama §8). Orta için çoğunlukla küçük, Zor için çoğunlukla büyük (dolu, çok tonlu arka planlı) iyi çalıştı. Kullanıcı Zor için "tam dolu, boşluklara gerek yok" dedi; 1–2 tanesi boşluklu olabilir.

### 3.3 Stil
- Projenin stili: kalın koyu outline, gölge tonu, küçük parlama, detaylar (yüz, benek, desen); arka planlar **çok tonlu** (gökyüzü bantları, deniz bantları, kareli desen) — **tek düz renk arka plan yanlış görünür.**
- **Bir partide kompozisyonu çeşitlendir.** Kullanıcı aynı kalıptan üç hayvanı (yandan duran, outline'lı, altında çim şeridi) "birebir aynı tarzda, sevemedim" diye reddetti. Karıştır: yandan figür, önden portre, yuvarlak çerçeveli sahne (lombozdan bakış), simetrik obje (kupa, saat), tam sahne (gökyüzü + zemin).

### 3.4 Elindeki düz ikonlar (Assets/30.10.2026/ — 30 resim, 3–4 renk, 234–584 küp, outline'sız)
Ölçüldü: **olduğu gibi bırakılırsa kuyruk ne olursa olsun simülatörde %100 geçilir** (bütün renkler baştan açıkta). Yani:
- **Kolay** level için olduğu gibi kullan (halat 1–2, gizli 1–2, **3 sütun**, ön sıra kuralı §0/10).
- **Orta** için zenginleştir: `--outline --shade --highlight --ring` (renk 3 → ~9; ölçüm: Ampul random %38, akıllı %100).
- **Zor** için ayrıca arka plan ekle: `--bg sky|night|sea|sunset|checker` (1024 küp; ölçüm: Ampul+gece random %16, Can Simidi+deniz random %16 / akıllı %88).
- Zenginleştirilmiş resmi **ayrı dosya adıyla** kaydet (örn. `Assets/30.10.2026/AmpulZ.png` ya da `Assets/32x32/Ampul.png`) — orijinali ezme. Her zaman `--preview` ile önce/sonra görseli üret ve **kullanıcıya göster, onay al.** Otomatik sonuç kötü görünürse (gölge garip yerde, halka anlamsız) elle düzelt ya da seçenekleri azalt.
- Elle kilit ekleme fikirleri: büyük renk alanının içine farklı renkte halka (Yengeç'te kırmızı kabuğun içine bordo halka), beneklerin etrafına koyu çerçeve, figürün içine ikinci bir motif, figürü saran çerçeve/kase/lomboz.

---

## 4. Kuyruk (gemi sırası) kuralları

### 4.1 Kapasite bölme
- Her renk ayrı gemilere bölünür. `n ≤ max` → tek gemi. Değilse: `r = n % 10`; `r > 0` ise bir küsuratlı gemi `10 + r`, kalanı `max`, …, son parça 10'un katı. Örn. max 30: 187 → 30,30,30,30,30,20,17; 66 → 30,20,16. Max = 30 (büyük levelde 40).
- Kapasiteler çoğunlukla 10/20/30(/40) okunmalı; renk başına en fazla 1 küsuratlı gemi; küsuratlı gemiler kuyruğa homojen dağılsın (hepsi başa yığılmasın). 10'dan küçük gemi olmasın (rengin tamamı <10 değilse).
- Gemi sayısını gereksiz artırma (çok küçük gemi levelleri uzatır, oyun döngüsünü bozar).

### 4.2 Sıra
- **Aynı sütunda aynı renk arka arkaya gelmez** (i ile i+sütun aynı renk olamaz). Aynı renk farklı sütunlarda aynı satırda olabilir ("dalga" açılışı serbest).
- Halat: §1.3. **İki gemisi farklı renk** olsun (aynı renk halat anlamsız). Ön sıra/açılış kuralı: **ilk 3 satırda en fazla 2 halat, ön sıranın en fazla yarısı halatlı** (kullanıcı tamamen halatlı açılışı elle düzeltti).
- Gizli: ilk satırda değil, halatlı değil.
- Sütun: büyük ve çok gemili levellerde **4**, ≤~23 gemilik küçük levellerde 3 olabilir (4 sütun her şeridi çok kısa bırakır). Kullanıcı genel olarak **"çoğunlukla 4"** istiyor.

### 4.3 Halat / gizli sayıları (kullanıcının istediği yoğunluk)
| Zorluk | Halat (çift) | Gizli gemi | Gizli küp |
|---|---|---|---|
| Kolay | 1–2 | 1–2 | yok |
| Orta | 4–6 | 4–6 | bazılarında ~%4 |
| Zor | 5–6 | 5–6 | ~%5 |
Küçük (15–20 gemilik) levellerde üst sınıra zorlama; sayı kadar **açılış kuralı** ve **çözülebilirlik** önemli.

---

## 5. Zorluk

### 5.1 Zorluğun kaynakları (etki sırasıyla)
1. **Resimdeki kilitler** (iç içe renkler, outline, halka, arka plan). Kilit yoksa kuyruk ne olursa olsun kolaydır.
2. **Kuyruk sırası**: içeride kilitli renklerin gemilerini öne koymak slotları tıkar.
3. **Halatlar**: iki slotu birden ve birlikte bağlar (dolan bekler).
4. **Gizli gemi / gizli küp**: bilgi saklar (simülatörde görünmez).
5. Sütun sayısı: 3 sütun = daha az seçenek (daha zor), 4 = daha çok seçenek.
Slot sayısı **sabit 5**; zorluk slotla ayarlanmaz.

### 5.2 Simülatör ne ölçer
`tools/sim` her oyunu oynar:
- **random**: her hamlede gönderilebilir gemilerden rastgele biri. "Düşünmeden oynayan oyuncu".
- **smart**: 1 hamle ileri bakan açgözlü bot (en çok küp toplayan, slotu tıkamayan hamle). "Dikkatli oyuncu" yaklaşık.
- **dfs**: tüm hamleleri deneyen arama → 1 = çözülebilir, 0 = **çözümsüz (ASLA teslim etme)**, -1 = bütçe yetmedi (tekrar dene / emin değilsin).
- **tap testi** (`m` modu): oyuncu denizciler toplamayı bitirmeden dokunursa ve gemiler paralel/rastgele sırayla toplarsa. Normal sonuçla arasında büyük fark varsa level **şansa bağlı** hissettirir (gerçek vaka: Şemsiye akıllı %49 → tap %29; kullanıcı "bazen ilk sıralarda takılıyorum, bazen sona geliyorum" dedi).
- **p** modu: akıllı bot kaybettiğinde, hatanın kaç hamle önce yapıldığı (kısa olmalı ≤5; uzun = "kör ezber").

### 5.3 Hedef bantlar (kullanıcı oynayarak kalibre etti)
| Zorluk | random | smart | tap testi | not |
|---|---|---|---|---|
| Kolay | 0.55–0.80 | ≥ 0.98 | — | **3 sütun.** Eski hedef ≥0.90 "ekrana bakmadan geçiliyor" dedirtti |
| Orta | 0.30–0.55 | ≥ 0.90 | smart ≥ 0.85 | |
| Zor | 0.12–0.30 | 0.80–0.95 (1.00 da olur) | smart, normalden en fazla ~0.1 düşük | "dikkatli oynayan geçer" |
Kalibrasyon dersleri:
- Zor'u smart ≈ 0.50'ye ayarlamak **fazla sert**: kullanıcı "Kelebek asla geçilmiyor" dedi (random 0.07 / smart 0.49). Smart 0.80+ olan Zor'lar şikayet almadı.
- Kullanıcı Orta'yı biraz zorlaştırdı (Kardan Adam'ı elle 5 halat, 6 gizli yaptı) ama **çözümsüz bıraktı** — her elle değişiklikten sonra simülatörden geçir.
- 3–4 renkli düz resim → %100 (kolay); bu Orta/Zor değildir.
- Gerçek oyun simülatöre göre biraz farklı (denizci yürüyüş süresi, animasyonlar); bantlar bunu hesaba katarak seçildi.

### 5.4 Simülatörün bilmediği şeyler
Gizli gemi/küp bilgisi, görsel okunurluk, oyuncunun sabrı. Bu yüzden: Zor'da gizlileri abartma, açılışı okunur tut, hata erken fark edilsin (p modu).

---

## 6. Araçlar

Kurulum: `g++` ve `python3` + `Pillow` gerekir.
```bash
cd docs/level-tasarim/tools && g++ -O2 -o sim sim.cpp && cd -
```
| Araç | İş |
|---|---|
| `tools/sim.cpp` | Simülatör (C++). `./sim level.txt 300 1 1` → `random smart dfs`. `... 300 1 0 m` → tap testi. `... 30 1 p` → hata→kayıp mesafesi. |
| `tools/design.py` | Kuyruk tasarlayıcı. `COLS=4 ITERS=400 python3 design.py resim.png O out.json 7`. Kapasiteleri böler, sırayı/halatları/gizlileri hedef banda göre arar (3 yeniden başlatma), sonunda dfs ve tap testi yapar. Zor için **`S`** kullan (`Z` eski sert bant). Çıktıda `pen 0.000` = bantta. |
| `tools/level_asset.py` | `new`, `apply`, `mystery`, `check`, `export`, `art` (§7). |
| `tools/enrich.py` | Düz ikonu zenginleştirir (§3.4). |
| `tools/sequence.py` | `show` (sıra + ihlaller), `suggest --from N` (kurallara uyan sıra önerisi). |

`design.py` birden çok tohumla paralel çalıştırılabilir; en iyi adayı seç (önce `pen`, sonra tap testinde smart'ın normalden ne kadar düştüğü, sonra hedef bandın ortası). 25 level × 2 tohum 16 çekirdekte ~15–25 dk sürer: uzun işleri arka planda çalıştır ve kullanıcıya haber ver (uzun süre sessiz kalmak "takıldı mı?" dedirtti).

---

## 7. Adım adım: bir resimden level

```bash
T=docs/level-tasarim/tools
# 1) Resmi incele: gerçekte ne olduğunu söyle (emin değilsen sor), sonra denetle
python3 $T/level_asset.py art --png Assets/30.10.2026/Ampul.png
# 2) Hedef zorluğa göre gerekiyorsa zenginleştir, önizlemeyi KULLANICIYA GÖSTER, onay al
python3 $T/enrich.py Assets/30.10.2026/Ampul.png Assets/30.10.2026/AmpulZ.png --outline --shade --highlight --ring --bg night --preview /tmp/onizleme_ampul.png
python3 $T/level_asset.py art --png Assets/30.10.2026/AmpulZ.png      # HATA olmamalı
# 3) Asset oluştur (index = koymayı düşündüğün sıra; sonra §9 ile kesinleşir)
python3 $T/level_asset.py new --png Assets/30.10.2026/AmpulZ.png --name Ampul --title "Ampul" --index 69
# 4) Kuyruğu tasarla (2–3 tohum dene, en iyisini seç)
COLS=4 ITERS=400 python3 $T/design.py Assets/30.10.2026/AmpulZ.png S /tmp/ampul_1.json 1
# 5) Asset'e yaz (+ Zor/Orta ise gizli küp)
python3 $T/level_asset.py apply --asset Assets/Levels/Level_69_Ampul.asset --design /tmp/ampul_1.json
python3 $T/level_asset.py mystery --asset Assets/Levels/Level_69_Ampul.asset --frac 0.05 --seed 1
# 6) Denetle ve bağımsız olarak asset'ten tekrar simüle et
python3 $T/level_asset.py check --asset Assets/Levels/Level_69_Ampul.asset          # çıkış 0 olmalı
python3 $T/level_asset.py export --asset Assets/Levels/Level_69_Ampul.asset --out /tmp/ampul.txt
$T/sim /tmp/ampul.txt 300 9 1 ; $T/sim /tmp/ampul.txt 300 9 0 m
# 7) Sıraya ekle (Unity içinden, §9) ve Unity'de doğrula (§10)
```
Toplu üretimde her level için aynı adımlar; resim değişikliklerini **toplu bir önizleme sayfasında** (her resim önce/sonra, adı, hedef zorluk, küp sayısı) göster.

---

## 8. Sıralama kuralları (LevelSequence)

- **İki Zor yan yana gelmez.** Zor'dan sonra Kolay iyi bir nefes molasıdır.
- **3 büyük (≥900 küp) resim art arda gelmez**; mümkünse büyük–küçük sırayla değişsin. İki küçük de yan yana gelmesin (yumuşak kural).
- İlk ~6 level öğretici: Kolay, küçük, az renkli.
- Aynı yoğunluk sınıfında küp sayısı ilerledikçe artsın; Kolay/Orta/Zor dengeli dağılsın.
- Yeni levelleri eklerken büyük–büyük çiftlerinin **arasına** küçük Orta'ları serpiştirmek iyi çalıştı.
- **Kullanıcının elle yaptığı sıra değişikliklerine (commit'lerine) saygı göster**, kendi sıranı geri dayatma.
- `python3 tools/sequence.py show --from 39` ihlalleri listeler; `suggest --from 39` öneri verir (dosyaya yazmaz).

## 9. Sırayı Unity'de uygulama (tek doğru yol)

`LevelSequence.asset`'i dışarıdan düzenlemek **kalıcı değil**: Level Designer penceresi açıkken kendi bellek listesini kaydeder ve senin sıranı ezer (gerçekleşti: aynı `m_LevelIndex`'e sahip üç çift oluştu). Unity MCP `execute_code` ile:

```csharp
// tail: belirli bir konumdan (burada 39) itibaren istenen sıra, dosya adlarıyla (uzantısız)
string[] tail = { "Level_61_Volkan", "Level_43_Piksel_Kalp", /* ... */ };
int keep = 38; // ilk 38 level aynen kalır
var seq = UnityEditor.AssetDatabase.LoadAssetAtPath<PixelGame.LevelSequence>("Assets/Levels/LevelSequence.asset");
var list = new System.Collections.Generic.List<PixelGame.PixelLevelData>(seq.Levels.Take(keep));
foreach (var n in tail) {
  var l = UnityEditor.AssetDatabase.LoadAssetAtPath<PixelGame.PixelLevelData>("Assets/Levels/" + n + ".asset");
  if (l == null || list.Contains(l)) return "eksik/tekrar: " + n;
  list.Add(l);
}
for (int i = 0; i < list.Count; i++) {
  var so = new UnityEditor.SerializedObject(list[i]);
  so.FindProperty("m_LevelIndex").intValue = i + 1;
  so.ApplyModifiedPropertiesWithoutUndo();
  UnityEditor.EditorUtility.SetDirty(list[i]);
}
seq.SetLevels(list);
UnityEditor.EditorUtility.SetDirty(seq);
UnityEditor.AssetDatabase.SaveAssets();
var lm = UnityEngine.Object.FindFirstObjectByType<PixelGame.LevelManager>();
if (lm != null) lm.RefreshFromSequence();
return "ok " + list.Count;
```
Yeni bir level'ı sona eklemek için de aynı kodu kullan (tail'e ekle). Sonra diskteki dosyayı `sequence.py show` ile tekrar kontrol et (`m_LevelIndex ≠ konum` / `tekrar` uyarısı olmamalı). Sahneyi (Gemi.unity) kaydetme; LevelManager oyun başında listeyi LevelSequence'ten yeniden okur.

## 10. Unity'de doğrulama

Dosyaları dışarıdan yazdıktan sonra `refresh_unity` (force, assets), sonra:
```csharp
var seq = UnityEditor.AssetDatabase.LoadAssetAtPath<PixelGame.LevelSequence>("Assets/Levels/LevelSequence.asset");
var sb = new System.Text.StringBuilder();
for (int i = 0; i < seq.Levels.Count; i++) {
  var l = seq.Levels[i]; var px = l.LevelTexture.GetPixels(); int bad = 0;
  foreach (var c in l.WagonSequence.Select(w => w.wagonColor).Distinct()) {
    int n = px.Count(p => p.a > 0.1f && PixelGame.PaletteColorOverride.ColorsMatch(p, c, 0.04f));
    if (n != l.WagonSequence.Where(w => w.wagonColor == c).Sum(w => w.capacity)) bad++;
  }
  if (bad > 0 || l.LevelIndex != i + 1) sb.Append((i+1) + " " + l.LevelName + " idx=" + l.LevelIndex + " renkHata=" + bad + "\n");
}
return sb.Length == 0 ? "hepsi OK (" + seq.Levels.Count + ")" : sb.ToString();
```
Editörde ayrıca `LevelSolvabilityAnalyzer.Analyze(level, runSimulation, trials)` ve `LevelDesignWarnings.Compute(level)` var; çapraz kontrol için kullanılabilir (ama zorluk kararını bu rehberin simülatörüne göre ver).

Oyunda test (isteğe bağlı): Play'e girince LandFlow yükleme ekranı ~15 sn sürer, bekle. Script değiştirdiysen Play'den önce derlemenin bitmesini bekle. Uzun Play oturumları, çok ekran görüntüsü ve tüm levelleri tek seferde yüklemek makineyi kilitledi/OOM yaptı: kısa tut, sonra Play'den çık.

---

## 11. Tuzaklar (hepsi gerçekten yaşandı)

- **Halatlı gemi partnerini bekler** kuralını unutan simülatör, oyunda imkansız olan levelleri "çözülebilir" gösterdi. `tools/sim.cpp` bu kuralı içerir; kendi simülatörünü yazarsan mutlaka ekle.
- **Renk toleransı 0.04**: yakın iki renk tek renk sayılır → "eksik sayı". `level_asset.py art/check` bunu yakalar.
- **Level Designer sıralamayı ezer** → §9.
- **`m_DifficultyManual: 0`** → Unity tier'ı değiştirir (Piksel Kalp dosyada Kolay, editörde Orta göründü).
- **Ön sırada başlayan gizli gemi** slota kadar açılmaz.
- **Geçici klasörler silinebilir** (`/tmp` yeniden başlatmada gider) — kalıcı araçlar `docs/level-tasarim/tools/` altında.
- Unity `[InitializeOnLoad]` editör kancaları sahneyi yeniden kurup kaydedebilir; sahneye dokunmuyorsan sorun değil.
- Kullanıcının kendi commit'lerini (elle sıra değişikliği, bir halatı kaldırma vb.) **geri alma**; neden yaptığını düşün, gerekirse sor.

## 12. Kullanıcıyla çalışma şekli

- Türkçe konuş. Kısa ve net ol; uzun işlerde ne yaptığını tek cümleyle haber ver.
- Her level için: resmin ne olduğunu söyle → gerekiyorsa resim değişikliğini önce/sonra göster → kuyruğu kur → simülatör sonucunu tabloyla raporla (random / smart / tap / halat / gizli / sütun).
- Toplu iş bitince **sıralamayı görsel olarak** (kart başına küçük resim, numara, zorluk etiketi, küp sayısı, halat, gizli) 3–4 sayfada göster; kullanıcı bunu seviyor.
- Ekran görüntüleri/önizlemeleri projeye koyma; geçici klasörde tut.
- **Commit/push yapma**, kullanıcı ayrıca istemedikçe. Geri almak isterse `git restore -- Assets/Levels/` gibi sadece level dosyalarını geri al; başka (sahne, ayar) değişikliklere dokunma.
- Kullanıcı ve ekip arkadaşı (Ezgi) oynayıp geri bildirim verir: "şansa bağlı", "asla geçilmiyor", "çok kolay", "aynı tarzda", "büyükler üst üste" gibi. Bunları ölçülebilir bir kurala çevir (bu rehberdeki kuralların çoğu böyle doğdu) ve aynı hatayı diğer levellerde de kontrol et.

## 13. Teslim öncesi kontrol listesi (her level)

- [ ] `level_asset.py art` → HATA yok; renk 6–11; 10 pikselden az renk yok.
- [ ] Resim değiştiyse kullanıcı önce/sonra görseli onayladı.
- [ ] `level_asset.py check` → çıkış 0 (sayılar, halat, gizli, aynı sütun, açılış).
- [ ] Asset'ten bağımsız simülasyon: dfs = 1; random/smart hedef bantta; tap testi smart'ta büyük düşüş yok.
- [ ] `m_DifficultyManual: 1`, doğru tier, `m_PoolColumns` 4 (küçükse 3), `m_PoolRows` 3.
- [ ] Sıra Unity içinden uygulandı; `sequence.py show` ihlal göstermiyor (iki Zor yan yana yok, 3 büyük art arda yok, index = konum).
- [ ] Unity doğrulama kodu "hepsi OK".
- [ ] Commit yapılmadı (kullanıcı istemedi).
