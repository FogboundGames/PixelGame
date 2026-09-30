using System.Collections.Generic;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Bir bölümün gerçekten bitirilebilir olup olmadığını, sahneye küp üretmeden
    /// ve play mode'a girmeden analiz eder.
    ///
    /// İki ayrı kontrol yapar:
    ///
    /// 1) KAPASİTE DENGESİ — her renk için "kaç küp var" ile "o renge kaç vagon
    ///    kapasitesi ayrılmış" karşılaştırılır. Eksikse bölüm matematiksel olarak
    ///    bitirilemez, fazlaysa boşa vagon vardır.
    ///
    /// 2) ERİŞİLEBİLİRLİK SİMÜLASYONU — asıl kısıt toplam sayı değildir. Oyunda bir
    ///    küp ancak dış havaya değiyorsa toplanabilir (bkz. ShipDispatcher.CalculateOutsideAir).
    ///    Bu yüzden toplamlar tutsa bile içeride gömülü bir renk oyunu kilitleyebilir.
    ///    Simülasyon çıkarma kurallarını baştan sona oynatıp kilitlenme olup olmadığını
    ///    ve hangi adımda olduğunu bildirir.
    ///
    /// Kurallar ShipDispatcher'daki gerçek mantığın birebir aynası: dış hava 4 yönlü
    /// flood-fill ile hesaplanır, küp 4 komşusundan biri dış havaysa "açık" sayılır,
    /// renk eşleşmesi ShipDispatcher.ColorsMatch ile yapılır.
    /// </summary>
    public static class LevelSolvabilityAnalyzer
    {
        /// <summary>
        /// Kaç geminin rengi "açıktaki" renklerden seçilsin. Oyunun güncel davranışı:
        /// HEPSİ (ShipQueuePool.RefreshQueue). Bu alan "ya eski kural kalsaydı?" gibi
        /// denemeler için açık bırakıldı — 2 yapılırsa eski davranış simüle edilir.
        /// </summary>
        public static int PreferExposedSpawnCount = int.MaxValue;

        /// <summary>
        /// Aynı anda yanaşan gemilere farklı renkler ver. Oyunun güncel davranışı budur
        /// (ShipQueuePool.GetNextNeededColor + IsColorAlreadyQueued).
        /// </summary>
        public static bool AvoidDuplicateSlotColors = true;

        /// <summary>Tek bir rengin küp / vagon kapasitesi dengesi.</summary>
        public class ColorBudget
        {
            public int paletteIndex;
            public Color color;
            public string label;
            public int cubeCount;        // ızgarada gerçekten sayılan küp
            public int storedPixelCount; // palette kayıtlı değer (bayatsa uyuşmaz)
            public int wagonCapacity;    // bu renge ayrılmış toplam vagon kapasitesi
            public int wagonCount;

            public int Diff => wagonCapacity - cubeCount;
            public bool IsShort => Diff < 0;
            public bool IsWasteful => Diff > 0;
            public bool IsStale => storedPixelCount != cubeCount;
        }

        public class Report
        {
            public bool valid;
            public string invalidReason;

            public List<ColorBudget> budgets = new List<ColorBudget>();
            public int totalCubes;
            public int totalWagonCapacity;

            // Simülasyon sonucu
            public bool simulationRan;
            public bool completed;
            public int trials;
            public int deadlockCount;
            public int collectedWorst;
            public string blockReason;
            public float DeadlockRisk => trials > 0 ? deadlockCount / (float)trials : 0f;
            public List<Color> blockedColors = new List<Color>();

            public List<string> warnings = new List<string>();

            public bool AnyShortage
            {
                get
                {
                    for (int i = 0; i < budgets.Count; i++) if (budgets[i].IsShort) return true;
                    return false;
                }
            }
        }

        // ------------------------------------------------------------------
        // Ana giriş
        // ------------------------------------------------------------------

        public static Report Analyze(PixelLevelData level, bool runSimulation = true, int trials = 200)
        {
            var rep = new Report();

            if (level == null) { rep.invalidReason = "Bölüm seçili değil."; return rep; }
            if (level.GetActiveTexture() == null) { rep.invalidReason = "Bölümde görsel (texture/sprite) yok."; return rep; }

            var grid = BuildGrid(level, out int minX, out int maxX, out int minY, out int maxY);
            if (grid.Count == 0) { rep.invalidReason = "Görselden hiç küp çıkmadı (hepsi şeffaf olabilir)."; return rep; }

            rep.valid = true;
            rep.totalCubes = grid.Count;

            var palette = level.ColorPalette;
            int paletteCount = palette != null ? palette.Count : 0;

            // --- 1) Renk başına küp sayımı ---
            var counts = new int[Mathf.Max(1, paletteCount)];
            int unmatched = 0;
            foreach (var kv in grid)
            {
                if (kv.Value < 0 || kv.Value >= paletteCount) { unmatched++; continue; }
                counts[kv.Value]++;
            }
            if (unmatched > 0)
                rep.warnings.Add($"{unmatched} küp hiçbir palet rengiyle eşleşmedi — palet görselle güncel değil (paleti yeniden tara).");

            // --- 2) Renk başına ayrılmış vagon kapasitesi ---
            var capByPalette = new int[Mathf.Max(1, paletteCount)];
            var wagonsByPalette = new int[Mathf.Max(1, paletteCount)];

            var sequence = level.WagonSequence;
            bool usingCustom = level.UseCustomWagonSequence && sequence != null && sequence.Count > 0;

            if (usingCustom)
            {
                for (int i = 0; i < sequence.Count; i++)
                {
                    var w = sequence[i];
                    if (w == null) continue;
                    int pi = w.paletteIndex;
                    // paletteIndex bayat olabilir; güvenmek yerine renkten de doğrula
                    if (pi < 0 || pi >= paletteCount || !PaletteMatches(palette, level, pi, w.wagonColor))
                        pi = MatchPaletteIndex(palette, level, w.wagonColor);

                    if (pi < 0 || pi >= paletteCount)
                    {
                        rep.warnings.Add($"'{w.label}' vagonunun rengi palette karşılık bulamadı — o vagon hiçbir küpü toplayamaz.");
                        continue;
                    }
                    capByPalette[pi] += Mathf.Max(0, w.capacity);
                    wagonsByPalette[pi]++;
                }
            }
            else
            {
                // Özel dizilim yoksa oyun palete göre otomatik vagon üretir:
                // her renk için tam olarak küp sayısını karşılayacak kadar vagon.
                int cap = Mathf.Max(1, level.TruckCapacity);
                for (int i = 0; i < paletteCount; i++)
                {
                    int need = counts[i];
                    if (need <= 0) continue;
                    int wagons = Mathf.CeilToInt(need / (float)cap);
                    wagonsByPalette[i] = wagons;
                    capByPalette[i] = wagons * cap;
                }
                rep.warnings.Add("Özel vagon dizilimi kapalı — kapasite, paletten otomatik üretilen dizilime göre hesaplandı.");
            }

            for (int i = 0; i < paletteCount; i++)
            {
                var e = palette[i];
                if (e == null) continue;
                if (counts[i] == 0 && capByPalette[i] == 0) continue;

                rep.budgets.Add(new ColorBudget
                {
                    paletteIndex = i,
                    color = e.targetColor,
                    label = string.IsNullOrEmpty(e.label) ? $"Renk #{i + 1}" : e.label,
                    cubeCount = counts[i],
                    storedPixelCount = e.pixelCount,
                    wagonCapacity = capByPalette[i],
                    wagonCount = wagonsByPalette[i]
                });
                rep.totalWagonCapacity += capByPalette[i];
            }

            // --- 3) Erişilebilirlik simülasyonu ---
            if (runSimulation) SimulateShipScene(level, grid, minX, maxX, minY, maxY, rep, Mathf.Max(1, trials));

            return rep;
        }

        private static bool PaletteMatches(List<PaletteColorOverride> palette, PixelLevelData level, int index, Color c)
        {
            if (palette == null || index < 0 || index >= palette.Count) return false;
            var e = palette[index];
            if (e == null) return false;
            Color target = PixelCube.AdjustColor(e.targetColor, level.ColorBrightness, level.ColorSaturation, level.ColorContrast);
            Color other = PixelCube.AdjustColor(c, level.ColorBrightness, level.ColorSaturation, level.ColorContrast);
            return ShipDispatcher.ColorsMatch(target, other);
        }

        // ------------------------------------------------------------------
        // Erişilebilirlik simülasyonu
        // ------------------------------------------------------------------

        private class SimShip
        {
            public int paletteIndex;
            public int remaining;
        }

        /// <summary>
        /// GEMİ sahnesinin gerçek kurallarını oynatır.
        ///
        /// Önemli: gemi sahnesi WagonSequence'i KULLANMAZ (o sadece TruckDispatcher /
        /// TruckPool içindir). Gemi renkleri ShipQueuePool.GetNextNeededColor ->
        /// ShipDispatcher.GetRemainingLevelColor ile tahtada KALAN renklerden seçilir;
        /// sadece ilk 2 gemi "açıktaki" renklerden gelir, gerisi kalan renkler arasından
        /// RASTGELE seçilir. Kapasite de kalan küp sayısından türetilir
        /// (kalan <= 20 ise tam kalan, değilse 10..20 arası rastgele).
        ///
        /// Bu yüzden kapasite hiçbir zaman yetersiz kalmaz; gerçek risk SLOT TIKANMASIDIR:
        /// tamamen gömülü bir renge gemi atanırsa o gemi dolamaz, kalkamaz ve slotu işgal
        /// eder. Bütün slotlar böyle olursa oyun kilitlenir. Seçim rastgele olduğu için
        /// sonuç tek bir evet/hayır değil, çok denemeli bir RİSK oranıdır.
        /// </summary>
        private static void SimulateShipScene(PixelLevelData level, Dictionary<(int, int), int> gridSource,
                                              int minX, int maxX, int minY, int maxY, Report rep, int trials)
        {
            rep.simulationRan = true;
            rep.trials = trials;

            int slotCount = Mathf.Max(1, level.SlotCount);
            var rng = new System.Random(12345); // sabit tohum: sonuç tekrarlanabilir olsun
            int deadlocks = 0;
            int worstCollected = int.MaxValue;
            var blockedTally = new Dictionary<int, int>();

            for (int t = 0; t < trials; t++)
            {
                var grid = new Dictionary<(int, int), int>(gridSource);
                var docked = new List<SimShip>();
                int spawnsSoFar = 0;
                int collected = 0;
                int guard = gridSource.Count * 4 + 2000;
                bool deadlocked = false;

                // Renk başına kalan küp sayısı ARTIMLI tutulur; her adımda ızgarayı
                // baştan taramak büyük tahtalarda simülasyonun en pahalı kısmıydı.
                var remainingByColor = new Dictionary<int, int>();
                foreach (var kv in grid)
                {
                    remainingByColor.TryGetValue(kv.Value, out int n0);
                    remainingByColor[kv.Value] = n0 + 1;
                }

                List<int> RemainingColors()
                {
                    var outList = new List<int>();
                    foreach (var kv in remainingByColor) if (kv.Value > 0) outList.Add(kv.Key);
                    return outList;
                }
                List<int> ExposedColors(HashSet<(int, int)> air)
                {
                    var set = new HashSet<int>();
                    foreach (var kv in grid)
                        if (IsExposed(air, kv.Key.Item1, kv.Key.Item2)) set.Add(kv.Value);
                    return new List<int>(set);
                }
                int CountOf(int pi)
                {
                    remainingByColor.TryGetValue(pi, out int n);
                    return n;
                }

                void FillSlots(HashSet<(int, int)> air)
                {
                    while (docked.Count < slotCount)
                    {
                        List<int> pool = null;
                        if (spawnsSoFar < PreferExposedSpawnCount)
                        {
                            pool = ExposedColors(air);
                            if (pool.Count == 0) pool = RemainingColors();
                        }
                        else
                        {
                            pool = RemainingColors();   // gerçek oyunda: rastgele kalan renk
                        }
                        if (pool.Count == 0) return;

                        if (AvoidDuplicateSlotColors && pool.Count > 1)
                        {
                            var free = new List<int>(pool.Count);
                            foreach (int c in pool)
                            {
                                bool taken = false;
                                foreach (var d in docked) if (d.paletteIndex == c) { taken = true; break; }
                                if (!taken) free.Add(c);
                            }
                            if (free.Count > 0) pool = free;
                        }

                        int pi = pool[rng.Next(pool.Count)];
                        int left = CountOf(pi);
                        if (left <= 0) return;

                        int cap = left <= 20 ? left : rng.Next(10, Mathf.Min(21, left + 1));
                        docked.Add(new SimShip { paletteIndex = pi, remaining = cap });
                        spawnsSoFar++;
                    }
                }

                FillSlots(ComputeOutsideAir(grid, minX, maxX, minY, maxY));

                // DALGA mantığı: her küp için dış havayı yeniden hesaplamak O(n²) yapıyordu.
                // Bunun yerine hava bir kez hesaplanır, o an açıkta olup yanaşmış gemilerle
                // eşleşen küplerin TAMAMI (gemi kapasitesi kadar) tek seferde toplanır,
                // sonra hava yeniden hesaplanır. Kilitlenme tespiti açısından eşdeğer:
                // kilit, hiçbir geminin hiçbir açık küple eşleşemediği durumdur.
                while (grid.Count > 0 && guard-- > 0)
                {
                    var air = ComputeOutsideAir(grid, minX, maxX, minY, maxY);

                    // Açıktaki küpleri renk bazında topla
                    var exposedByColor = new Dictionary<int, List<(int, int)>>();
                    foreach (var kv in grid)
                    {
                        if (!IsExposed(air, kv.Key.Item1, kv.Key.Item2)) continue;
                        if (!exposedByColor.TryGetValue(kv.Value, out var lst))
                        {
                            lst = new List<(int, int)>();
                            exposedByColor[kv.Value] = lst;
                        }
                        lst.Add(kv.Key);
                    }

                    int consumedThisWave = 0;

                    for (int i = docked.Count - 1; i >= 0; i--)
                    {
                        var sh = docked[i];
                        if (sh.remaining <= 0) { docked.RemoveAt(i); continue; }
                        if (!exposedByColor.TryGetValue(sh.paletteIndex, out var cells) || cells.Count == 0) continue;

                        int take = Mathf.Min(sh.remaining, cells.Count);
                        for (int k = 0; k < take; k++)
                        {
                            grid.Remove(cells[cells.Count - 1 - k]);
                            remainingByColor[sh.paletteIndex] = remainingByColor[sh.paletteIndex] - 1;
                        }
                        cells.RemoveRange(cells.Count - take, take);

                        sh.remaining -= take;
                        collected += take;
                        consumedThisWave += take;

                        if (sh.remaining <= 0) docked.RemoveAt(i);
                    }

                    if (consumedThisWave > 0)
                    {
                        FillSlots(air);
                        continue;
                    }

                    // Rengi tahtada bitmiş gemiler kalkar, slot boşalır
                    bool freed = false;
                    for (int i = docked.Count - 1; i >= 0; i--)
                    {
                        if (CountOf(docked[i].paletteIndex) <= 0) { docked.RemoveAt(i); freed = true; }
                    }
                    if (freed) { FillSlots(air); continue; }

                    // Hiçbir gemi iş yapamıyor ve hiçbiri kalkamıyor -> KİLİT
                    deadlocked = true;
                    foreach (var sh in docked)
                    {
                        blockedTally.TryGetValue(sh.paletteIndex, out int n);
                        blockedTally[sh.paletteIndex] = n + 1;
                    }
                    break;
                }

                if (deadlocked)
                {
                    deadlocks++;
                    if (collected < worstCollected) worstCollected = collected;
                }
                else if (grid.Count == 0)
                {
                    rep.completed = true;
                }
            }

            rep.deadlockCount = deadlocks;
            rep.collectedWorst = worstCollected == int.MaxValue ? rep.totalCubes : worstCollected;
            rep.completed = deadlocks < trials;

            if (deadlocks > 0)
            {
                rep.blockReason = $"{trials} denemenin {deadlocks} tanesinde kilitlendi " +
                                  $"(%{(deadlocks * 100f / trials):F0}). Sebep: tamamen gömülü bir renge gemi " +
                                  $"atanınca o gemi dolamıyor, kalkamıyor ve slotu işgal ediyor.";

                // En sık tıkayan renkler
                var ordered = new List<KeyValuePair<int, int>>(blockedTally);
                ordered.Sort((a, b) => b.Value.CompareTo(a.Value));
                for (int i = 0; i < ordered.Count && i < 3; i++)
                {
                    int pi = ordered[i].Key;
                    if (level.ColorPalette != null && pi >= 0 && pi < level.ColorPalette.Count)
                        rep.blockedColors.Add(level.ColorPalette[pi].targetColor);
                }
            }
        }

        // ------------------------------------------------------------------
        // Izgara kurulumu
        // ------------------------------------------------------------------

        /// <summary>
        /// Bölümün dokusundan küp ızgarasını kurar. Değer = palet indeksi.
        /// PixelArtGenerator'ın örnekleme kurallarını taklit eder (1:1 çözünürlükte
        /// doğrudan piksel, değilse point örnekleme; şeffaf pikseller atlanır).
        /// </summary>
        public static Dictionary<(int, int), int> BuildGrid(PixelLevelData level, out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = minY = int.MaxValue;
            maxX = maxY = int.MinValue;

            var grid = new Dictionary<(int, int), int>();
            if (level == null) return grid;

            Texture2D tex = level.GetActiveTexture();
            if (tex == null) return grid;

#if UNITY_EDITOR
            EnsureReadable(tex);
#endif
            if (!tex.isReadable) return grid;

            Vector2Int res = level.GetGridResolution();
            int cols = Mathf.Max(1, res.x);
            int rows = Mathf.Max(1, res.y);

            var palette = level.ColorPalette;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    Color raw;
                    if (cols == tex.width && rows == tex.height)
                    {
                        raw = tex.GetPixel(x, y);
                    }
                    else
                    {
                        int px = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) / cols * tex.width), 0, tex.width - 1);
                        int py = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) / rows * tex.height), 0, tex.height - 1);
                        raw = tex.GetPixel(px, py);
                    }

                    if (level.SkipTransparent && raw.a < 0.1f) continue;

                    Color piped = level.ApplyColorPipeline(raw);
                    Color shown = PixelCube.AdjustColor(piped, level.ColorBrightness, level.ColorSaturation, level.ColorContrast);

                    int idx = MatchPaletteIndex(palette, level, shown);
                    grid[(x, y)] = idx;

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            return grid;
        }

        /// <summary>Küp rengini palet girdisiyle eşler; eşleşme yoksa -1.</summary>
        private static int MatchPaletteIndex(List<PaletteColorOverride> palette, PixelLevelData level, Color shown)
        {
            if (palette == null) return -1;
            for (int i = 0; i < palette.Count; i++)
            {
                var e = palette[i];
                if (e == null) continue;
                Color target = PixelCube.AdjustColor(e.targetColor, level.ColorBrightness, level.ColorSaturation, level.ColorContrast);
                if (ShipDispatcher.ColorsMatch(target, shown)) return i;
            }
            return -1;
        }

        /// <summary>
        /// ShipDispatcher.CalculateOutsideAir'ın ızgara (renksiz) karşılığı:
        /// sınırın 1 hücre dışından başlayıp BOŞ hücreler üzerinden 4 yönlü yayılır.
        /// </summary>
        public static HashSet<(int, int)> ComputeOutsideAir(Dictionary<(int, int), int> grid, int minX, int maxX, int minY, int maxY)
        {
            var air = new HashSet<(int, int)>();
            if (grid == null || grid.Count == 0) return air;

            int bMinX = minX - 1, bMaxX = maxX + 1;
            int bMinY = minY - 1, bMaxY = maxY + 1;

            var q = new Queue<(int, int)>();
            void Seed(int x, int y)
            {
                if (air.Add((x, y))) q.Enqueue((x, y));
            }

            for (int x = bMinX; x <= bMaxX; x++) { Seed(x, bMinY); Seed(x, bMaxY); }
            for (int y = bMinY + 1; y < bMaxY; y++) { Seed(bMinX, y); Seed(bMaxX, y); }

            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };

            while (q.Count > 0)
            {
                var (cx, cy) = q.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + dx[i], ny = cy + dy[i];
                    if (nx < bMinX || nx > bMaxX || ny < bMinY || ny > bMaxY) continue;
                    if (air.Contains((nx, ny))) continue;
                    if (grid.ContainsKey((nx, ny))) continue;   // dolu hücreden hava geçmez
                    air.Add((nx, ny));
                    q.Enqueue((nx, ny));
                }
            }

            return air;
        }

        /// <summary>Bir hücrenin 4 komşusundan biri dış havaysa küp toplanabilir.</summary>
        public static bool IsExposed(HashSet<(int, int)> air, int x, int y)
        {
            return air.Contains((x - 1, y)) || air.Contains((x + 1, y))
                || air.Contains((x, y - 1)) || air.Contains((x, y + 1));
        }

#if UNITY_EDITOR
        private static void EnsureReadable(Texture2D tex)
        {
            if (tex == null || tex.isReadable) return;
            string path = UnityEditor.AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return;
            var imp = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
            if (imp != null && !imp.isReadable)
            {
                imp.isReadable = true;
                imp.SaveAndReimport();
            }
        }
#endif
    }
}
