# PixelGame — Level Resmi Çizim Rehberi (yapay zekâ için)

Bu rehber, LandFlow / PixelGame için **32×32 piksel sanat level resmi** çizecek bir yapay zekâ içindir.
Resim kodla çizilir (Python + `tools/pixelkit.py`). Hiçbir adımı atlama; özellikle §6 (kilitler) ve §9 (kontrol listesi).

Genel level kuralları (kuyruk, halat, gizli gemi, zorluk bantları, sıralama) için ayrıca **[REHBER.md](REHBER.md)** var.
Bu dosya yalnızca **resmin kendisini** anlatır; ikisi çelişirse oyun kuralı (REHBER.md) kazanır.

---

## 0. En önemli 12 kural

1. **Tek bakışta ne olduğu anlaşılmalı.** Önce silüet, sonra detay. Ana nesne büyük ve ortada olsun; isimden tahmin edilen resim başarısızdır.
2. Resim **32×32**, alfa yalnızca **0 ya da 255**, **6–11 renk**.
3. Hiçbir iki renk birbirine yakın olmasın: algısal fark **ΔE2000 ≥ 15** (rahat okunması için ≥ 18). Kanal başına 14 fark yetmez — açık mavi ile beyaz, kum ile açık turuncu, iki lacivert bu eşikten geçip oyunda yine karışıyordu (Sahil Kasabası). Oyun 0.04 ≈ 10/255 farkı zaten **aynı renk** sayar → sayılar bozulur. `pixelkit.py` raporu ve `level_asset.py art/check` ΔE'yi ölçer (`renkfarki.py`).
4. Her renk **en az 10 piksel** olsun (rengin tamamı 10'dan azsa o renk tek, küçük bir gemi olur; kaçın).
5. Her malzeme **3 ton**: ana ton + gölge (sağ/alt) + parlama (sol/üst). Işık **sol üstten**.
6. Figürlerin etrafında **koyu kontur**: `(34, 26, 40)`. Kontur, figürün rengine benzemesin.
7. Arka plan **asla tek düz renk** olmasın: 2–3 tonlu bantlar, doku, uzak katman.
8. **Kilit** olmadan level kolaydır: önemli renkler başka renklerin **içinde** kalmalı (§6). Tahta kenarına değen her renk baştan açıktadır.
9. İnce, çubuk gibi nesneler (saksafon, kılıç, ince sap) kilit oluşturmaz → kalınlaştır ya da bir şeyin içine/önüne koy.
10. Küp sayısı zorluğa uysun (§2). Kolay: arka plansız figür 400–500. Orta: sahne 700–899. Zor: tam tahta 1024.
11. Bir partideki resimler **farklı kompozisyonlarda** olsun (§4); aynı kalıbı tekrarlama.
12. Önizleme sayfası üret, **kullanıcıya göster, onay al**; önizlemeleri projeye koyma (geçici klasör).

---

## 1. Teknik zorunluluklar

| Kural | Değer | Neden |
|---|---|---|
| Boyut | 32×32 | Tahta 32×32 hücre; daha küçük resim tahtada küçük görünür |
| Alfa | 0 veya 255 | Yarı saydam piksel küp sayımını bozar |
| Renk sayısı | 6–11 (Kolay figürde 6–8 yeter) | Az renk = bulmaca yok; çok renk = küçük gemi kalabalığı |
| Renk farkı | ΔE2000 ≥ 15 (rahat: ≥ 18) | kanal farkı <10 oyun için aynı renk; ΔE <15 oyuncu oynarken karıştırır |
| Renk başına piksel | ≥ 10 | Daha azı 10'dan küçük tek gemi olur |
| Kontur | `(34, 26, 40)` | Proje standardı; mor-siyah, hiçbir figür rengine yakın değil |
| Tek renk baskınlığı | Hiçbir renk resmin ~%60'ını geçmesin | Bir renk çok baskınsa aynı sütunda aynı renk tekrar etmeye başlar (kuyruk kuralı bozulur) ve level "düz" hissettirir |

**Gerçek hatalar:**
- *Kardan Adam*: şapka `(40,36,48)` ile kontur `(34,26,40)` oyun için aynı renkti → "eksik sayı".
- *Antik Kütüphane* (ilk sürüm): raf gölgesi `(104,64,38)` ile duvar `(96,64,48)` aynı sayıldı.
- *Yengeç*: üç kırmızı tonu oyuncu tarafından ayırt edilemedi.
- *Denizaltı Dünyası* (eski): arka planın %64'ü tek renkti → 17 gemi, sütun tekrarı kaçınılmaz.

---

## 2. Boyut ve doluluk (zorluğa göre)

| Zorluk | Küp | Tipik kompozisyon |
|---|---|---|
| **Kolay** | 400–500 | Arka plansız tek figür, ortada, tuvalin ~%40–50'si. Az ama net kilit (iç kulak, göz, papyon). |
| **Orta** | 700–899 | Sahne ya da nesne + arka plan; üstte birkaç satır boş bırakılabilir (doluluk 0.7–0.88). **900'ü geçme** (900+ "büyük" sayılır, sıralamada büyükler art arda gelmez). |
| **Zor** | 1024 (tam tahta) | Tam dolu sahne, çok tonlu arka plan, iç içe katmanlar. 1–2 tanesi 800+ boşluklu olabilir. |

Kolay figürde 500'ü çok aşma (Sincap 512 sınırda). Orta'da 900'ü aşmamak için gökyüzünü 1–2 satır aşağıdan başlat (`band(5, 31)` gibi).

---

## 3. Okunurluk (en sık yapılan hata)

32×32'de detay yeri yoktur. Okunmayan resim, kuyruk ne kadar iyi olursa olsun kötü level'dır.

**Yap:**
- Ana nesne tuvalin **en az %35–40'ını** kaplasın ve ortada dursun.
- Önce silüeti tek renkle çiz, uzaktan bak: hâlâ anlaşılıyorsa detay ekle.
- Detaylar en az **2 piksel** (1 piksellik detay kontur içinde kaybolur).
- Ana nesne ile arka plan arasında **değer (açık/koyu) farkı** olsun: koyu gece göğünde açık nesne, açık gökyüzünde konturlu orta-koyu nesne.
- Tanıdık ipuçları ekle: denizaltıya periskop ve lomboz, kütüphaneye rafta renkli kitap sırtları, vahaya hurma ağacı ve su.

**Yapma (gerçek örnekler, hepsi yeniden çizildi):**

| Eski resim | Sorun | Yeni çözüm |
|---|---|---|
| Denizaltı Dünyası | Sarı-mavi leke, pembe dikdörtgen, gri bulut — denizaltı yok | Ortada büyük sarı denizaltı (lomboz, periskop, pervane), 3 tonlu derinlik, kum, mercan, balık |
| Antik Kütüphane | Kahverengi bloklar, ortada belirsiz bej şekil | İki yanda kitap rafları (renkli sırtlar), ortada kemerli pencere, masa, mum |
| Çöl Vahası Gece | Yuvarlak yeşil lekeler, turuncu ateş | Dolunay, 3 katman kum tepesi, ayı yansıtan su, iki hurma ağacı |
| Antik Piramit Gece | Düz üçgen + blok | Taş sıraları belirgin, aydınlık/gölge yüzlü 3 piramit, deve silüeti |
| Dişli Çark | Muşta + halkalar gibi okunuyor | Birbirine geçen pirinç ve çelik iki dişli, kare kalın dişler, göbek delikli |
| Çapa Halat | Halat parça parça, görünmüyor | Halat gövdeye iki tur sarılı, arkadan geçen kısımlar gizli, ucu kola sarkar |
| Dondurma Külah | Külahın altı üç ayaklı sehpa gibi | (Örnek) Külah tek sivri koni olmalı, waffle çizgileri koni içinde |

---

## 4. Kompozisyon çeşitliliği

Kullanıcı aynı kalıptaki resimleri reddetti ("birebir aynı tarzda": yandan duran hayvan + kontur + altta çim şeridi, üç kez).
Bir partide kompozisyonları karıştır:

- **Yandan figür** (Papağan dalda, Sincap kütükte)
- **Önden portre** (Panda, Oyuncak Ayı)
- **Simetrik nesne** (Sihirli Lamba, Saat Kulesi, Bonsai)
- **Manzara** (Kayık ve Göl, Yel Değirmeni, Sörf Dalgası, Pirinç Terasları)
- **İç mekân** (Okuma Köşesi, Antik Kütüphane, Kristal Mağara)
- **Gece sahnesi** (Kamp Çadırı, Kağıt Fenerler, Kış Kulübesi, Gece Pazarı)
- **Su altı / yukarıdan bakış** (Batık Gemi, Denizanası, Gelgit Havuzu)

Orta level'larda **her şeyi yuvarlak çerçeve/disk içine koyma** (kullanıcı "hepsi yuvarlak çerçevede, garip" dedi). Serbest silüet + sahne tercih et.
Hayvan çiziyorsan aynı pozu tekrar etme.

---

## 5. Renk ve gölgelendirme

**Malzeme başına 3 ton** (ana / gölge / parlama). Gölge, ana tonun hem daha koyu hem biraz daha doygun/sıcak-soğuk kaydırılmış hâli olsun; sadece siyahla karıştırma.

Projede işe yarayan örnek paletler:

| Malzeme | Ana | Gölge | Parlama |
|---|---|---|---|
| Pirinç / altın | (234,176,48) | (170,110,30) | (255,232,140) |
| Çelik | (140,158,182) | (82,96,122) | (214,226,240) |
| Ahşap | (176,118,66) | (112,70,40) | — |
| Kırmızı kumaş/boya | (220,52,48) | (150,30,42) | — |
| Yaprak | (64,140,70) | (38,92,60) | (108,184,84) |
| Kar | (236,242,250) | (160,176,206) | — |
| Gece göğü (bantlar) | (22,26,64) | (40,44,98) | (90,64,130) ufukta |
| Gündüz göğü | (150,206,240) | — | (200,232,250) üstte |
| Deniz (derinlik) | (70,170,206) | (40,116,176) | (24,62,118) en dipte |

Kurallar:
- **Işık sol üstten.** `Canvas.shape(m, ana, parlama, gölge)` bunu otomatik yapar (sol/üst kenar açık, sağ/alt kenar koyu, sonra kontur).
- Gökyüzü/deniz **yatay bantlar**: 2–3 ton, keskin geçiş (piksel sanatta yumuşak gradyan yok).
- Gece sahnelerinde **küçük sıcak ışıklar** (pencere, fener, yıldız) — göz oraya gider, resim canlanır.
- Aynı tonun 3 varyantını ayrı renk yapma (Yengeç hatası); farklı **renk tonları** (kırmızı, mavi, sarı, yeşil) kullan.
- Kahverengi halat ile altın sarısı, pirinç ile ahşap gibi **benzer tonlu** malzemeleri yan yana koyuyorsan farkı belirgin tut (≥ 40 bir kanalda).

---

## 6. Kilitler: zorluk resimden gelir (ÇOK ÖNEMLİ)

Oyunda bir küp ancak **dışarıdaki havaya komşuysa** alınabilir. Hava, tahtanın dışından içeri yayılır; **tahtanın kenarı da dışarıdır.**
Yani:
- Tahta kenarına değen her renk **baştan açıktadır**.
- Başka renklerin **içinde kalan** bir renk, o renkler kazılana kadar **kilitlidir**.
- Kilit yoksa kuyruk ne olursa olsun level kendini oynar (rastgele oynayan bot bile %90–100 kazanır).

**Kilit yapma yolları:**
- **Kontur:** figürün iç renkleri kontur kalkana kadar kapalı.
- **Halka / çerçeve:** büyük bir renk alanının içine farklı renkte halka (dişlinin iç halkası, plak oluğu).
- **İç detay:** iç kulak, göz, ağız, göbek, papyon (Oyuncak Ayı), pencere ışığı (Kış Kulübesi).
- **Katman:** önde duran nesne arkasındakini kapatır (halat çeliğin üstünden geçer, ağaç gövdesi göğü böler).
- **Zor için tam sahne:** gökyüzü/zemin bantları + ortada konturlu büyük nesne + nesnenin içinde ikinci, üçüncü renk.

**Gerçek ölçümler (rastgele bot kazanma oranı; Kolay hedefi %55–80, Orta %30–55, Zor %12–30):**

| Resim | Durum | Rastgele bot |
|---|---|---|
| Saksafon (ince, her yeri dışarıda) | Kilit yok | **%100** (her kuyrukta) → Oyuncak Ayı ile değiştirildi (%56) |
| Antik Piramit Gece (eski, düz üçgen) | Kilit yok | **%100** |
| Kayık ve Göl (tam genişlik bantlar) | Bantlar kenara değiyor | Normal açılış kuralıyla %100; halat eklemek için kural gevşetildi |
| 3–4 renkli düz ikonlar (Assets/30.10.2026) | Kontur/katman yok | %100 → yalnızca Kolay |
| Kış Kulübesi (tam sahne, iç içe) | Çok katman | %10 (Zor) |

**Manzara uyarısı:** Gökyüzü, dağ, deniz gibi tam genişlik bantların hepsi kenara değer → hepsi açık. Manzarada kilit için:
ortaya **konturlu büyük bir nesne** koy (kayık, değirmen, kulübe), nesnenin **içine** renkler ekle (pencere, kapı, yelken), bantların içine ada/bulut gibi kapalı şekiller yerleştir.

**Açılış kuralı (REHBER.md §0.10):** kuyrukta ön sırada en fazla 1, ilk iki sırada en fazla 3 gemi "dış katman" renginden (başta ≥6 küpü açıkta olan renk) olabilir.
Resimde **dış katman renklerinin sayısı azsa** (2–3) bu kural kolay sağlanır. Her rengi kenara dokunduran resimler bu kuralı imkânsız kılar.

---

## 7. Adım adım iş akışı

```bash
T=docs/level-tasarim/tools
# 1) Konuyu seç (proje içindeki level adlarıyla çakışmasın; §4'e göre kompozisyonu belirle)
# 2) Çiz: tools/ornek_cizimler.py'deki en yakın örneği kopyala, kendi fonksiyonunu yaz
python3 my_art.py            # Canvas(...).save('/tmp/x/Ad.png') + print(c.report())
# 3) Teknik kontrol (HATA olmamalı; UYARI'ları düzelt)
python3 $T/level_asset.py art --png /tmp/x/Ad.png
python3 $T/pixelkit.py /tmp/x/*.png
# 4) Önizleme sayfası: pixelkit.sheet([...]) ya da before_after([...]) → /tmp/x/onizleme.png
#    KULLANICIYA GÖSTER, onay al. Önizlemeyi projeye koyma.
# 5) Onaydan sonra resmi Assets/<tarih>/<kolay|orta|zor>/Ad.png olarak koy, sonra REHBER.md §7 (asset + kuyruk)
# 6) Kilit testi: kuyruk tasarlayıcı ilk 1-2 dakikada "links 0 hidden 0" ve random ~1.00 veriyorsa resimde kilit yok → §6
COLS=4 ITERS=400 python3 $T/design.py /tmp/x/Ad.png O /tmp/x/ad.json 1
```

Mevcut bir resmi değiştiriyorsan PNG'nin **içeriğini** değiştir (`.meta` dosyasına dokunma, level asset'i ona bağlı), sonra level'ın kuyruğunu yeniden tasarla (resim değişince renk sayıları değişir).

---

## 8. pixelkit kısa başvuru

```python
from pixelkit import *            # tools/ içinden; başka yerdeyse sys.path'e tools/'u ekle

c = Canvas()                      # 32×32 boş (saydam) tuval
# Şekiller piksel kümesidir; | birleşim, - fark, & kesişim
circ(cx, cy, r)                   # daire
ring(cx, cy, r_dis, r_ic)         # halka (kilit!)
ell(cx, cy, rx, ry, rot=0)        # elips (rot radyan)
rect(x0, y0, x1, y1)              # dikdörtgen, uçlar dahil
band(y0, y1)                      # tam genişlik yatay bant (kenara değer → açık)
poly([(x, y), ...])               # çokgen
seg([(x, y), ...], r)             # kalın çizgi / kıvrım (halat, sap, dal, gövde)
ALL                               # bütün tahta

c.fill(m, renk)                   # boya (sonraki öncekinin üstüne)
c.shape(m, ana, parlama, gölge)   # dolgu + ışık/gölge kenarı + kontur (OUTLINE)
c.shape(m, ana, None, None, None) # konturu istemiyorsan out=None
c.outline(m, renk)                # yalnızca kontur
c.dots(m, renk, every=6, seed=1)  # seyrek doku (kum, yıldız, yosun)
c.where(renk)                     # şu an o renkte olan pikseller (doku eklemek için)
c.save('Ad.png'); print(c.report())

sheet([('Başlık', 'a.png'), ...], 'onizleme.png')                 # toplu önizleme
before_after([('Başlık', 'eski.png', 'yeni.png'), ...], 'ba.png')  # önce / sonra
```

Çizim sırası **arkadan öne**: gökyüzü → uzak katman → zemin → ana nesne → ana nesnenin detayları → ön plan (ot, taş, balık).
Konturu her parçadan sonra ayrı çek ki parçalar birbirinden ayrılsın (`shape` bunu yapar).

**Örnekler:** `tools/ornek_cizimler.py` — Papağan (Kolay figür), Oyuncak Ayı (Kolay, iç kilitler), Kayık ve Göl (Orta manzara),
Bonsai (Orta nesne + arka plan), Kış Kulübesi (Zor tam sahne), Denizaltı Dünyası (tam sahne, ortada büyük nesne).
`python3 tools/ornek_cizimler.py /tmp/ornekler` hepsini çizer; çıktılar oyundaki resimlerle piksel piksel aynıdır.

Örnek iskelet (Orta, gece sahnesi):

```python
from pixelkit import *

def kamp():
    c = Canvas()
    NV=(26,32,72); NV2=(44,52,104); ST=(250,230,140); PN=(34,96,72); PL=(64,140,86)
    TN=(232,124,52); TD=(164,70,40); GLW=(255,214,92); FR=(232,64,40); GRD=(92,70,58); LOG=(128,84,52)
    c.fill(rect(0,4,31,31), NV); c.fill(rect(0,15,31,23), NV2)            # 1) gök, 2 bant (üst 4 satır boş → <900 küp)
    c.fill(circ(25,8,2.6) - circ(26.3,7.2,2.2), ST)                        # hilal
    for p in [(3,6),(9,5),(14,8),(19,5),(30,13)]: c.fill({p}, ST)          # yıldızlar
    for (x,h) in [(2,14),(7,10),(29,12),(24,9)]:                           # 2) çam ağaçları (uzak katman)
        tree = poly([(x-3.5,25),(x,25-h),(x+3.5,25)]); c.fill(tree, PN)
        c.fill({p for p in tree if p[0] < x and p[1] % 3 == 0}, PL)        #    sol yüzde ışık çizgileri
    c.fill(poly([(0,25),(31.9,24),(31.9,31.9),(0,31.9)]), GRD)             # 3) zemin
    tent = poly([(6,27),(14,13),(22,27)]); c.fill(tent, TN)                # 4) ana nesne: çadır
    c.fill({p for p in tent if p[0] >= 14}, TD)                            #    gölge yüzü (kilit: iç renk)
    c.fill(poly([(11.5,27),(14,20),(16.5,27)]), GLW)                       #    içeriden ışık (kilit)
    fire = poly([(21.5,28),(23,23),(25,18.5),(27,23),(28.5,28)]); c.fill(fire, FR)   # 5) kamp ateşi
    c.fill(poly([(23.2,28),(25,22.5),(26.8,28)]), GLW)
    c.fill(seg([(21.5,29),(28.5,27)],0.8) | seg([(21.5,27),(28.5,29)],0.8), LOG)
    return c

c = kamp(); c.save('/tmp/x/KampCadiri.png'); print(c.report())
```

---

## 9. Teslim öncesi kontrol listesi (her resim)

- [ ] Tek bakışta ne olduğu anlaşılıyor (isim olmadan birine sorsan bilir mi?)
- [ ] 32×32, alfa 0/255, 6–11 renk
- [ ] `level_asset.py art` → **HATA yok** (yakın renk yok); her renk ≥ 10 piksel
- [ ] Küp sayısı zorluk aralığında (Kolay 400–500, Orta 700–899, Zor 1024)
- [ ] Her malzemede gölge ve (gerekirse) parlama; ışık sol üstten; konturlar `(34,26,40)`
- [ ] Arka plan çok tonlu (tek düz renk değil)
- [ ] Kilit var: en az 2–3 renk başka renklerin içinde kalıyor; her renk kenara değmiyor
- [ ] Hiçbir renk resmin ~%60'ından fazlası değil
- [ ] Partideki diğer resimlerle aynı kompozisyon kalıbında değil
- [ ] Önizleme kullanıcıya gösterildi ve onaylandı; önizleme dosyası projeye konmadı
- [ ] (Level'a dönüşünce) kuyruk tasarlayıcı halat/gizli koyabildi ve hedef bantta (REHBER.md §5.3)

---

## 10. Sık hatalar → çözüm

| Belirti | Sebep | Çözüm |
|---|---|---|
| "Ne olduğu anlaşılmıyor" | Ana nesne küçük/parçalı, lekeler | Ana nesneyi büyüt, ortala, silüeti tek parça yap, tanıdık ipucu ekle |
| Oyun iki rengi aynı sayıyor | Kanal farkı < 10 | Bir rengi ≥ 14 (tercihen ≥ 25) uzaklaştır |
| Rastgele bot %90–100 kazanıyor | Kilit yok / her renk kenarda / nesne ince | §6: kontur, halka, iç detay, katman; ince nesneyi kalınlaştır |
| Kuyruk tasarlayıcı "links 0 hidden 0" | Dış katman rengi çok → açılış kuralı imkânsız | Kenara değen renk sayısını azalt (bantları azalt, nesneyi kenardan uzaklaştır) |
| Sütunda aynı renk tekrar ediyor (kural hatası) | Bir renk resmin %60+'ı | Büyük alanı 2–3 tona/desene böl ya da başka nesnelerle kır |
| Çok küçük gemiler (≤ 9) | Rengin < 10 pikseli var | Detayı büyüt ya da rengi komşu renge kat |
| Resim "düz", "amaçsız" | Tek renk arka plan, gölgesiz dolgu | Bantlar, doku, malzeme başına 3 ton |
| Partideki resimler birbirinin kopyası | Aynı kalıp | §4'teki kompozisyon türlerinden farklılarını seç |
