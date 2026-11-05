using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Oyundaki 162 seviyenin detaylı sıra, kapasite ve mekanik analizinden
    /// (Benchmark) türetilmiş akıllı seviye üretim motoru.
    ///
    /// Kurallar ve İstatistikler:
    /// - Ortalama Küp: ~659 | Renk: 6-9 | Gemi: 22-26 (Zor seviyelerde 30-50+ gemi)
    /// - Kapasiteler: %71.5 Yuvarlak (30, 40, 20, 10, 50) + Kalan Küp Dengeleyiciler (12, 14, 16, 18)
    /// - Halatlı Gemiler (Linked): Seviyelerin %92'sinde var (ortalama 4-7 çift)
    /// - Gizli Gemiler (Hidden): Seviyelerin %94'ünde var (ortalama 4-7 gemi, asla ilk 5 sırada değil)
    /// - Gizli Küpler (Mystery ?): Zor seviyelerin %90'ında var (ortalama 40-70 küp)
    /// - Çift Renkli Konteyner (Dual): Seviyelerin %32'sinde 1 adet
    /// - Çözülebilirlik: her deneme LevelQueueSimulator ile (gerçek kuyruk + halat kuralları) ölçülür;
    ///   kurallara uymayan ya da çözülemeyen sonuç işaretlenir
    /// - Birebir kopya seviye üretmeme (Benzersizlik doğrulaması)
    /// </summary>
    public static class LevelBenchmarkEngine
    {
        public enum DifficultyPreset
        {
            AutoBySequence = 0, // Seviye numarasına göre otomatik uyarla
            Kolay = 1,          // 4-5 Renk, ~20-25 Gemi, Düşük İskele Baskısı
            Orta = 2,           // 6-8 Renk, ~30-40 Gemi, Dengeli Bulmaca
            Zor = 3             // 7-10 Renk, ~40-60 Gemi, Taktiksel Katman Kilitleri, Gizli Küpler
        }

        public enum PuzzleDurationPacing
        {
            FastCasual = 0,     // ⚡ Hızlı / Giriş (~1.5 Dk) -> Geniş Kapasiteler, ~20-25 Gemi
            StandardPuzzle = 1, // 🧩 Standart Bulmaca (~3 - 4 Dk) -> Dengeli Kapasiteler, ~30-40 Gemi, %25 Gizli Küp
            TacticalDeep = 2,   // 🧠 Zorlu & Uzun Soluklu (~5 - 8+ Dk) -> Sıkı Kapasiteler, ~40-60 Gemi, %40 Gizli Küp
            Custom = 3          // 🎛️ Özel (Kullanıcı Hedef Gemi Sayısı veya Kapasite Belirler)
        }

        public class GenerationConfig
        {
            public DifficultyPreset preset = DifficultyPreset.Orta;
            public PuzzleDurationPacing durationPacing = PuzzleDurationPacing.StandardPuzzle;
            public int targetShipCount = 0; // 0 = profile göre otomatik hesapla, >0 = hedeflenen gemi sayısı
            public int targetLevelIndex = 163;
            public string levelName = "Yeni Seviye";

            // Mekanikler
            public bool generateLinks = true;
            public bool generateHidden = true;
            public bool generateMysteryCubes = true;
            public int mysteryCubePercent = 5;  // Tüm küplerin % kaçı '?' olsun (en fazla MaxMysteryCubePercent)
            public MysteryRevealCondition mysteryRevealCondition = MysteryRevealCondition.WhenExposed;
            public bool preferRoundCapacities = true;
            public float roundRatio = 0.75f;
            public bool allowDualColor = false;
            public bool guaranteeSolvability = true;
            public int solvabilityTrials = 80;

            // Kapasite Sınırları
            public int maxCapacity = 30;
            public int minCapacity = 10;
            public int peakCapacity = 20;

            // Havuz & İskele Slot Ayarları
            public int poolRows = 3;       // Sıra (dalga) — ekranda her şeridin ilk 3 gemisi görünür
            public int poolColumns = 4;    // Kolon — rehber: çoğunlukla 4, Kolay ve küçük levellerde 3
            public int slotCount = 5;      // ⚓ Yanaşma Slotu
        }

        public class GenerationResult
        {
            public bool success;
            public string errorMessage;
            public List<WagonSequenceEntry> sequence = new List<WagonSequenceEntry>();
            public List<PaletteColorOverride> palette = new List<PaletteColorOverride>();
            public List<Vector2Int> mysteryCubes = new List<Vector2Int>();
            public MysteryRevealCondition mysteryRevealCondition = MysteryRevealCondition.WhenExposed;
            public int totalCubes;
            public int totalAssignedCapacity;
            public int linkedCount;
            public int hiddenCount;
            public int dualCount;
            public int mysteryCubeCount;
            public LevelDifficultyTier resolvedTier;
            public bool is100PercentSolvable;   // kurallara uyuyor VE kuyruk simülatöründe çözülebilir
            public float deadlockRisk;          // akıllı oyuncunun kaybetme oranı
            public bool simulated;
            public float simRandomWinRate;
            public float simSmartWinRate;
            public List<string> ruleErrors = new List<string>();
            public bool isUnique;
            public string estimatedPlaytime;
            public string benchmarkSummary;
        }

        /// <summary>
        /// Verilen görsel dokudan bulmaca süresi, katman kilitleri ve 162 seviye benchmark kurallarına uygun
        /// derinlikli ve dengeli seviye verisi üretir.
        /// </summary>
        public static GenerationResult GenerateFromTexture(Texture2D texture, GenerationConfig config)
        {
            var res = new GenerationResult();
            if (texture == null)
            {
                res.errorMessage = "Görsel (Texture2D) seçilmedi!";
                return res;
            }

            PixelPaletteOptimizer.EnsureReadable(texture);

            // 1. Kademeyi Çözümle
            LevelDifficultyTier tier = LevelDifficultyTier.Orta;
            if (config.preset == DifficultyPreset.Kolay) tier = LevelDifficultyTier.Kolay;
            else if (config.preset == DifficultyPreset.Zor) tier = LevelDifficultyTier.Zor;
            else if (config.preset == DifficultyPreset.AutoBySequence)
            {
                if (config.targetLevelIndex <= 20) tier = LevelDifficultyTier.Kolay;
                else if (config.targetLevelIndex <= 80) tier = LevelDifficultyTier.Orta;
                else tier = (config.targetLevelIndex % 3 == 0) ? LevelDifficultyTier.Zor : LevelDifficultyTier.Orta;
            }
            res.resolvedTier = tier;

            // 2. Geçici PixelLevelData Oluştur
            var tempLevel = ScriptableObject.CreateInstance<PixelLevelData>();
            tempLevel.LevelName = config.levelName;
            tempLevel.LevelIndex = config.targetLevelIndex;
            tempLevel.DifficultyTier = tier;
            tempLevel.LevelTexture = texture;
            tempLevel.OriginalSourceTexture = texture;
            tempLevel.UseNativeResolution = true;
            tempLevel.CubeSpacing = 0.06f;
            tempLevel.CubeSpacingX = 0f;
            tempLevel.CubeDepth = 1.0f;
            tempLevel.BoardTiltAngle = 18.0f;
            tempLevel.CubeFrontTiltAngle = -6.0f;
            tempLevel.SkipTransparent = false;
            tempLevel.PoolColumns = Mathf.Clamp(config.poolColumns, 1, 8);
            tempLevel.PoolRows = Mathf.Clamp(config.poolRows, 1, 6);
            tempLevel.SlotCount = Mathf.Clamp(config.slotCount, 1, 8);

            tempLevel.ExtractPaletteFromTexture();
            if (tempLevel.ColorPalette == null || tempLevel.ColorPalette.Count == 0)
            {
                res.errorMessage = "Görselde opak piksel bulunamadı!";
                UnityEngine.Object.DestroyImmediate(tempLevel);
                return res;
            }

            res.palette = new List<PaletteColorOverride>(tempLevel.ColorPalette);
            res.totalCubes = tempLevel.GetTotalCubeCountInPalette();

            // 3. Süre & Bulmaca Derinliğine Göre Kapasite & Gemi Sayısı Ölçekleme
            int effectiveMinCap = config.minCapacity;
            int effectiveMaxCap = config.maxCapacity;
            int targetShips = config.targetShipCount;

            switch (config.durationPacing)
            {
                case PuzzleDurationPacing.FastCasual:
                    effectiveMinCap = Mathf.Max(10, config.minCapacity);
                    effectiveMaxCap = Mathf.Max(30, config.maxCapacity);
                    if (targetShips <= 0) targetShips = Mathf.Clamp(res.totalCubes / 30, 16, 28);
                    res.estimatedPlaytime = "~1.5 - 2.5 Dakika (Hızlı)";
                    break;

                case PuzzleDurationPacing.StandardPuzzle:
                    effectiveMinCap = Mathf.Clamp(config.minCapacity, 10, 20);
                    effectiveMaxCap = Mathf.Clamp(config.maxCapacity, 20, 35);
                    if (targetShips <= 0) targetShips = Mathf.Clamp(res.totalCubes / 24, 25, 42);
                    res.estimatedPlaytime = "~3.5 - 5.0 Dakika (Standart)";
                    break;

                case PuzzleDurationPacing.TacticalDeep:
                    effectiveMinCap = Mathf.Clamp(config.minCapacity, 10, 20);
                    effectiveMaxCap = Mathf.Clamp(config.maxCapacity, 20, 35);
                    if (targetShips <= 0) targetShips = Mathf.Clamp(res.totalCubes / 20, 32, 55);
                    res.estimatedPlaytime = "~5.5 - 8.5+ Dakika (Zorlu / Taktiksel)";
                    break;

                case PuzzleDurationPacing.Custom:
                    if (targetShips <= 0) targetShips = Mathf.Clamp(res.totalCubes / 24, 20, 60);
                    res.estimatedPlaytime = $"Özel (~{targetShips} Gemi)";
                    break;
            }

            tempLevel.TruckCapacity = effectiveMaxCap;
            tempLevel.MinTruckCapacity = effectiveMinCap;
            tempLevel.PeakTruckCapacity = (effectiveMinCap + effectiveMaxCap) / 2;

            // 4. Izgara & Dış Hava (Peeling Order) Analizi
            var grid = LevelSolvabilityAnalyzer.BuildGrid(tempLevel, out int minX, out int maxX, out int minY, out int maxY);
            var exposedColorPriorities = CalculateExposedColorPriorities(grid, minX, maxX, minY, maxY, tempLevel.ColorPalette.Count);

            // 5. Gizli Küpler (Mystery Cubes - '?') Üretimi
            List<Vector2Int> generatedMystery = new List<Vector2Int>();
            // Rehber §1.5: küplerin ~%4–5'i, "abartmadan" (eskiden %25/%35'e zorlanıyordu, oyuncu resmin üçte birini göremiyordu)
            if (config.generateMysteryCubes && config.mysteryCubePercent > 0)
            {
                generatedMystery = GenerateInnerMysteryCubes(grid, minX, maxX, minY, maxY, config.mysteryCubePercent);
            }
            res.mysteryCubes = generatedMystery;
            res.mysteryCubeCount = generatedMystery.Count;
            res.mysteryRevealCondition = config.mysteryRevealCondition;
            tempLevel.MysteryCubeCoordinates = new List<Vector2Int>(generatedMystery);
            tempLevel.MysteryRevealCondition = config.mysteryRevealCondition;

            // 6. Renk Başına Vagon Kapasitelerini Dağıt
            var rng = new System.Random(Environment.TickCount ^ config.targetLevelIndex);
            var wagonsByColor = new Dictionary<int, List<WagonSequenceEntry>>();

            for (int pIdx = 0; pIdx < tempLevel.ColorPalette.Count; pIdx++)
            {
                var entry = tempLevel.ColorPalette[pIdx];
                if (entry == null || entry.pixelCount <= 0) continue;

                int cubesOfColor = entry.pixelCount;
                Color col = entry.targetColor;
                string label = string.IsNullOrEmpty(entry.label) ? $"Renk #{pIdx + 1}" : entry.label;

                // Hedef gemi sayısına göre renk başına düşen gemi miktarını orantıla
                float colorShare = cubesOfColor / (float)Mathf.Max(1, res.totalCubes);
                int desiredShipsForColor = Mathf.Max(1, Mathf.RoundToInt(targetShips * colorShare));

                List<int> loads = SplitCapacitiesPaced(cubesOfColor, effectiveMinCap, effectiveMaxCap, desiredShipsForColor, config.preferRoundCapacities, config.roundRatio, rng);

                var list = new List<WagonSequenceEntry>();
                for (int w = 0; w < loads.Count; w++)
                {
                    list.Add(new WagonSequenceEntry(col, loads[w], pIdx, $"{label} ({w + 1})"));
                }
                wagonsByColor[pIdx] = list;
            }

            // 7. Taktiksel Katman Kilitli Interleaving (Bulmaca Açılışı & Pacing)
            List<WagonSequenceEntry> baseOrder = InterleaveWithLayerLock(wagonsByColor, exposedColorPriorities, config.durationPacing, rng);

            // 8. Çift Renkli Konteyner: iki gemiyi birleştirip birini sildiği için halat/gizli konumları belirlenmeden ÖNCE yapılır
            //    (eskiden sonra yapılıyordu; silinen geminin arkasındaki herkes bir sıra kayıyor, halatlar geçersiz konuma düşüyordu)
            if (config.allowDualColor && baseOrder.Count >= 10)
            {
                ApplyBenchmarkDualColor(baseOrder, tempLevel.ColorPalette, rng);
            }

            // 9–11. Sıra + halat + gizli: birkaç deneme üret, her birini kurallarla ve gerçek kuyruk simülatörüyle ölç,
            //       kurallara uyan, çözülebilir ve zorluk bandına en yakın olanı seç
            int cols = tempLevel.PoolColumns;
            var (minPairs, maxPairs) = MechanicRange(tier, baseOrder.Count);
            int attempts = config.guaranteeSolvability ? 16 : 1;
            List<WagonSequenceEntry> masterSequence = null;
            List<string> bestErrors = null;
            LevelQueueSimulator.Result bestSim = default;
            double bestScore = double.MaxValue;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var seq = baseOrder.Select(CloneWithoutMechanics).ToList();
                if (attempt > 0) PerturbOrder(seq, cols, rng, 1 + attempt);
                FixColumnRepeats(seq, cols, rng);

                if (config.generateLinks) ApplyStrategicLinks(seq, rng.Next(minPairs, maxPairs + 1), cols, rng);
                if (config.generateHidden) ApplyBenchmarkHidden(seq, rng.Next(minPairs, maxPairs + 1), cols, rng);

                var errors = ValidateQueueRules(seq, cols);
                LevelQueueSimulator.Result sim = default;
                double score = errors.Count * 10.0;
                if (config.guaranteeSolvability)
                {
                    sim = LevelQueueSimulator.Simulate(tempLevel, seq, Mathf.Clamp(config.solvabilityTrials / 2, 20, 60), attempt + 1);
                    score += (sim.Solvable ? 0.0 : 100.0) + BandDistance(tier, sim);
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    masterSequence = seq;
                    bestErrors = errors;
                    bestSim = sim;
                }
                if (score < 0.01) break;
            }

            tempLevel.WagonSequence = masterSequence;
            tempLevel.UseCustomWagonSequence = true;

            bool isSolvable = !config.guaranteeSolvability || bestSim.Solvable;
            float risk = config.guaranteeSolvability ? 1f - bestSim.smartWinRate : 0f;
            res.ruleErrors = bestErrors;
            res.simRandomWinRate = bestSim.randomWinRate;
            res.simSmartWinRate = bestSim.smartWinRate;
            res.simulated = config.guaranteeSolvability;
            if (bestSim.error != null) res.ruleErrors.Add("Simülasyon: " + bestSim.error);

            // 12. Benzersizlik Doğrulaması
            res.isUnique = VerifySequenceNovelty(masterSequence);

            res.success = true;
            res.sequence = masterSequence;
            res.totalAssignedCapacity = masterSequence.Sum(w => w.capacity + (w.isDualColor ? (w.secondaryCapacity > 0 ? w.secondaryCapacity : w.capacity) : 0));
            res.linkedCount = masterSequence.Count(w => w.linkId > 0);
            res.hiddenCount = masterSequence.Count(w => w.isHidden);
            res.dualCount = masterSequence.Count(w => w.isDualColor);
            res.is100PercentSolvable = isSolvable && res.ruleErrors.Count == 0;
            res.deadlockRisk = risk;

            string verdict = !res.simulated ? "⚠️ Simüle edilmedi"
                : !isSolvable ? "⛔ Çözülemiyor"
                : res.ruleErrors.Count > 0 ? $"⚠️ {res.ruleErrors.Count} kural hatası"
                : $"✓ Çözülebilir (rastgele %{res.simRandomWinRate * 100:F0}, akıllı %{res.simSmartWinRate * 100:F0})";
            res.benchmarkSummary = $"Bölüm {config.targetLevelIndex} Üretildi ({res.estimatedPlaytime}): {res.totalCubes} Küp | {res.sequence.Count} Gemi | " +
                                   $"{(res.mysteryCubeCount > 0 ? $"{res.mysteryCubeCount} Gizli Küp (?) | " : "")}" +
                                   $"{res.linkedCount / 2} Halat Çifti | {res.hiddenCount} Gizli Gemi | {verdict}" +
                                   (res.ruleErrors.Count > 0 ? "\n• " + string.Join("\n• ", res.ruleErrors.Take(6)) : "");

            UnityEngine.Object.DestroyImmediate(tempLevel);
            return res;
        }

        public static List<int> SplitCapacitiesPaced(int totalCubes, int minCap, int maxCap, int desiredCount, bool preferRound, float roundRatio, System.Random rng)
        {
            var loads = new List<int>();
            if (totalCubes <= 0) return loads;

            minCap = Mathf.Max(4, minCap);
            maxCap = Mathf.Max(minCap, maxCap);

            int fewest = Mathf.Max(1, Mathf.CeilToInt(totalCubes / (float)maxCap));
            int most = Mathf.Max(fewest, totalCubes / Mathf.Max(1, minCap));
            int count = Mathf.Clamp(desiredCount, fewest, most);

            if (count <= 1)
            {
                loads.Add(totalCubes);
                return loads;
            }

            if (preferRound)
            {
                // 10'un katları (10, 20, 30, 40, 50): Sayıların ezici çoğunluğu 10'un katı olur,
                // tam bölünmeyen küpler ise sadece TEK bir vagonda toplanır.
                var allowedRounds = new List<int>();
                for (int m = 10; m <= 60; m += 10)
                {
                    if (m >= minCap - 2 && m <= maxCap + 5)
                        allowedRounds.Add(m);
                }
                if (allowedRounds.Count == 0)
                {
                    allowedRounds.Add(20);
                    allowedRounds.Add(30);
                }

                float avg = totalCubes / (float)count;
                int bestRound = allowedRounds[0];
                float bestDist = float.MaxValue;
                foreach (var r in allowedRounds)
                {
                    float dist = Mathf.Abs(r - avg);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestRound = r;
                    }
                }

                for (int i = 0; i < count; i++) loads.Add(bestRound);
                int curSum = bestRound * count;
                int diff = totalCubes - curSum;

                // Farkı 10'ar 10'ar dağıt (10'un katı bozulmaz)
                int safety = 0;
                while (diff >= 10 && safety++ < 200)
                {
                    var cands = new List<int>();
                    for (int i = 0; i < count; i++)
                    {
                        if (loads[i] + 10 <= maxCap + 5) cands.Add(i);
                    }
                    if (cands.Count == 0) break;
                    int idx = cands[rng.Next(cands.Count)];
                    loads[idx] += 10;
                    diff -= 10;
                }

                safety = 0;
                while (diff <= -10 && safety++ < 200)
                {
                    var cands = new List<int>();
                    for (int i = 0; i < count; i++)
                    {
                        if (loads[i] - 10 >= Mathf.Max(4, minCap - 4)) cands.Add(i);
                    }
                    if (cands.Count == 0) break;
                    int idx = cands[rng.Next(cands.Count)];
                    loads[idx] -= 10;
                    diff += 10;
                }

                // 10'a tam bölünmeyen kalan küpleri sadece TEK bir vagona ver (diğer tüm vagonlar 10'un katı kalır)
                if (diff != 0)
                {
                    int chosenIdx = count - 1;
                    for (int i = 0; i < count; i++)
                    {
                        int candidateCap = loads[i] + diff;
                        if (candidateCap >= minCap - 2 && candidateCap <= maxCap + 6)
                        {
                            chosenIdx = i;
                            break;
                        }
                    }
                    loads[chosenIdx] += diff;
                }

                // Rastgele karıştır
                for (int i = loads.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    int t = loads[i]; loads[i] = loads[j]; loads[j] = t;
                }

                return loads;
            }
            else
            {
                int avg = totalCubes / count;
                int sum = 0;
                for (int i = 0; i < count; i++)
                {
                    int cap = Mathf.Clamp(minCap + rng.Next((maxCap - minCap + 1)), minCap, maxCap);
                    loads.Add(cap);
                    sum += cap;
                }

                int safety = 0;
                while (sum != totalCubes && safety++ < 10000)
                {
                    int idx = rng.Next(count);
                    if (sum > totalCubes && loads[idx] > minCap)
                    {
                        loads[idx]--;
                        sum--;
                    }
                    else if (sum < totalCubes && loads[idx] < maxCap)
                    {
                        loads[idx]++;
                        sum++;
                    }
                }

                for (int i = loads.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    int t = loads[i]; loads[i] = loads[j]; loads[j] = t;
                }

                return loads;
            }
        }

        public const int MaxMysteryCubePercent = 10;

        /// <summary>
        /// Gizli küpler: tüm küplerin percent'i kadar (en fazla %10), iç bölgede renk sınırlarına denk gelen
        /// 2–3 küçük küme olarak (docs/level-tasarim/tools/level_asset.py mystery ile aynı yaklaşım).
        /// </summary>
        public static List<Vector2Int> GenerateInnerMysteryCubes(
            Dictionary<(int, int), int> grid,
            int minX, int maxX, int minY, int maxY,
            int percent)
        {
            var result = new List<Vector2Int>();
            if (grid == null || grid.Count == 0 || percent <= 0) return result;

            bool Inner((int, int) p) => grid.ContainsKey((p.Item1 - 1, p.Item2)) && grid.ContainsKey((p.Item1 + 1, p.Item2))
                                        && grid.ContainsKey((p.Item1, p.Item2 - 1)) && grid.ContainsKey((p.Item1, p.Item2 + 1));
            var neighbours = new (int, int)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            bool OnBoundary((int, int) p) => neighbours.Any(d => grid.TryGetValue((p.Item1 + d.Item1, p.Item2 + d.Item2), out int c) && c != grid[p]);

            var seeds = grid.Keys.Where(p => Inner(p) && OnBoundary(p)).ToList();
            if (seeds.Count == 0) seeds = grid.Keys.Where(Inner).ToList();
            if (seeds.Count == 0) return result;

            var rng = new System.Random();
            int target = Mathf.RoundToInt(grid.Count * Mathf.Min(percent, MaxMysteryCubePercent) / 100f);
            int clusters = Mathf.Clamp(target / 8, 2, 3);
            int perCluster = Mathf.Max(1, Mathf.CeilToInt(target / (float)clusters));
            var chosen = new HashSet<(int, int)>();

            for (int c = 0; c < clusters && chosen.Count < target; c++)
            {
                var seed = seeds[rng.Next(seeds.Count)];
                if (chosen.Contains(seed)) continue;
                var queue = new Queue<(int, int)>();
                queue.Enqueue(seed);
                int added = 0;
                while (queue.Count > 0 && added < perCluster && chosen.Count < target)
                {
                    var p = queue.Dequeue();
                    if (chosen.Contains(p) || !Inner(p)) continue;
                    chosen.Add(p);
                    added++;
                    foreach (var d in neighbours.OrderBy(_ => rng.Next())) queue.Enqueue((p.Item1 + d.Item1, p.Item2 + d.Item2));
                }
            }

            foreach (var p in chosen) result.Add(new Vector2Int(p.Item1, p.Item2));
            return result;
        }

        private static List<int> CalculateExposedColorPriorities(Dictionary<(int, int), int> grid, int minX, int maxX, int minY, int maxY, int colorCount)
        {
            var priorities = new List<int>();
            if (grid == null || grid.Count == 0) return priorities;

            var remaining = new Dictionary<(int, int), int>(grid);
            var seenColors = new HashSet<int>();

            int safety = 0;
            while (remaining.Count > 0 && safety++ < 100)
            {
                var air = LevelSolvabilityAnalyzer.ComputeOutsideAir(remaining, minX, maxX, minY, maxY);
                var peel = new List<(int, int)>();

                foreach (var kvp in remaining)
                {
                    if (LevelSolvabilityAnalyzer.IsExposed(air, kvp.Key.Item1, kvp.Key.Item2))
                    {
                        peel.Add(kvp.Key);
                        int colIdx = kvp.Value;
                        if (!seenColors.Contains(colIdx) && colIdx >= 0)
                        {
                            seenColors.Add(colIdx);
                            priorities.Add(colIdx);
                        }
                    }
                }

                if (peel.Count == 0) break;
                foreach (var pt in peel) remaining.Remove(pt);
            }

            for (int c = 0; c < colorCount; c++)
            {
                if (!priorities.Contains(c)) priorities.Add(c);
            }

            return priorities;
        }

        private static List<WagonSequenceEntry> InterleaveWithLayerLock(
            Dictionary<int, List<WagonSequenceEntry>> wagonsByColor,
            List<int> colorPriority,
            PuzzleDurationPacing pacing,
            System.Random rng)
        {
            var result = new List<WagonSequenceEntry>();
            var queues = new Dictionary<int, Queue<WagonSequenceEntry>>();

            foreach (var kvp in wagonsByColor)
            {
                queues[kvp.Key] = new Queue<WagonSequenceEntry>(kvp.Value);
            }

            if (pacing == PuzzleDurationPacing.TacticalDeep && colorPriority.Count >= 4)
            {
                int colOuter1 = colorPriority[0];
                int colOuter2 = colorPriority.Count > 1 ? colorPriority[1] : colOuter1;
                int colMid1 = colorPriority.Count > 2 ? colorPriority[2] : colOuter1;
                int colMid2 = colorPriority.Count > 3 ? colorPriority[3] : colOuter2;

                if (queues[colOuter1].Count > 0) result.Add(queues[colOuter1].Dequeue());
                if (queues[colOuter2].Count > 0) result.Add(queues[colOuter2].Dequeue());
                if (queues[colMid1].Count > 0) result.Add(queues[colMid1].Dequeue());
                if (queues[colOuter1].Count > 0) result.Add(queues[colOuter1].Dequeue());
                if (queues[colMid2].Count > 0) result.Add(queues[colMid2].Dequeue());
            }
            else
            {
                foreach (int colIdx in colorPriority)
                {
                    if (queues.ContainsKey(colIdx) && queues[colIdx].Count > 0)
                    {
                        result.Add(queues[colIdx].Dequeue());
                    }
                }
            }

            bool hasMore = true;
            while (hasMore)
            {
                hasMore = false;
                foreach (int colIdx in colorPriority)
                {
                    if (queues.ContainsKey(colIdx) && queues[colIdx].Count > 0)
                    {
                        result.Add(queues[colIdx].Dequeue());
                        hasMore = true;
                    }
                }
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Kuyruk kuralları (docs/level-tasarim/REHBER.md §1.3, §1.4, §4.2)
        // ------------------------------------------------------------------

        /// <summary>Zorluğa göre halat çifti / gizli gemi sayısı aralığı; küçük kuyruklarda gemi sayısıyla sınırlanır.</summary>
        public static (int min, int max) MechanicRange(LevelDifficultyTier tier, int shipCount)
        {
            var (lo, hi) = tier == LevelDifficultyTier.Kolay ? (1, 2) : tier == LevelDifficultyTier.Orta ? (4, 6) : (5, 6);
            int cap = Mathf.Max(1, shipCount / 4);
            return (Mathf.Min(lo, cap), Mathf.Min(hi, cap));
        }

        /// <summary>Halatlı iki gemi yan yana (aynı satır, komşu sütun) ya da başlangıç havuzunda alt alta olmalı.</summary>
        public static bool IsValidLinkPosition(int a, int b, int cols)
        {
            if (a > b) { int t = a; a = b; b = t; }
            if (b == a + 1 && a % cols != cols - 1) return true;
            return b == a + cols && b < cols * 3;
        }

        private static bool IsLinkable(WagonSequenceEntry w) => w != null && w.linkId == 0 && !w.isHidden && !w.isDualColor;

        /// <summary>
        /// Halat çiftleri: yalnızca geçerli konumlar, iki farklı renk, Güçlü/gizli gemiye halat yok.
        /// Açılış: ilk 3 satırda en fazla 2 halat, ön sıranın en fazla yarısı halatlı.
        /// </summary>
        public static void ApplyStrategicLinks(List<WagonSequenceEntry> list, int targetPairs, int cols, System.Random rng)
        {
            int n = list.Count;
            var candidates = new List<(int a, int b)>();
            for (int i = 0; i < n; i++)
            {
                if (i + 1 < n && IsValidLinkPosition(i, i + 1, cols)) candidates.Add((i, i + 1));
                if (i + cols < n && IsValidLinkPosition(i, i + cols, cols)) candidates.Add((i, i + cols));
            }
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = candidates[i]; candidates[i] = candidates[j]; candidates[j] = t;
            }

            int linkId = list.Where(w => w != null).Select(w => w.linkId).DefaultIfEmpty(0).Max() + 1;
            int pairs = 0, openingPairs = 0, frontLinked = 0;
            foreach (var (a, b) in candidates)
            {
                if (pairs >= targetPairs) break;
                if (!IsLinkable(list[a]) || !IsLinkable(list[b]) || list[a].paletteIndex == list[b].paletteIndex) continue;
                bool opening = a < cols * 3;
                int front = (a < cols ? 1 : 0) + (b < cols ? 1 : 0);
                if (opening && openingPairs >= 2) continue;
                if (frontLinked + front > cols / 2) continue;

                list[a].linkId = linkId;
                list[b].linkId = linkId;
                linkId++;
                pairs++;
                if (opening) openingPairs++;
                frontLinked += front;
            }
        }

        /// <summary>Gizli gemi ön sırada başlamaz (yanaşana kadar açılmaz), halatlı ve Güçlü olmaz.</summary>
        public static void ApplyBenchmarkHidden(List<WagonSequenceEntry> list, int targetHidden, int cols, System.Random rng)
        {
            var candidates = new List<int>();
            for (int i = cols; i < list.Count; i++)
            {
                if (list[i] != null && list[i].linkId == 0 && !list[i].isDualColor && !list[i].isHidden) candidates.Add(i);
            }
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = candidates[i]; candidates[i] = candidates[j]; candidates[j] = t;
            }
            for (int k = 0; k < candidates.Count && k < targetHidden; k++) list[candidates[k]].isHidden = true;
        }

        private static bool SameColor(WagonSequenceEntry a, WagonSequenceEntry b) => a != null && b != null && a.paletteIndex == b.paletteIndex;

        private static int ColumnConflicts(List<WagonSequenceEntry> list, int i, int cols)
        {
            int c = 0;
            if (i - cols >= 0 && SameColor(list[i], list[i - cols])) c++;
            if (i + cols < list.Count && SameColor(list[i], list[i + cols])) c++;
            return c;
        }

        /// <summary>Aynı sütunda aynı renk arka arkaya gelmesin: çakışan gemiyi çakışmayı azaltan bir gemiyle yer değiştirir.</summary>
        public static void FixColumnRepeats(List<WagonSequenceEntry> list, int cols, System.Random rng)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                bool changed = false;
                for (int i = cols; i < list.Count; i++)
                {
                    if (!SameColor(list[i], list[i - cols]) || list[i].linkId != 0) continue;
                    int start = rng.Next(list.Count);
                    for (int k = 0; k < list.Count; k++)
                    {
                        int j = (start + k) % list.Count;
                        if (j == i || list[j].linkId != 0) continue;
                        int before = ColumnConflicts(list, i, cols) + ColumnConflicts(list, j, cols);
                        (list[i], list[j]) = (list[j], list[i]);
                        int after = ColumnConflicts(list, i, cols) + ColumnConflicts(list, j, cols);
                        if (after < before) { changed = true; break; }
                        (list[i], list[j]) = (list[j], list[i]);
                    }
                }
                if (!changed) break;
            }
        }

        /// <summary>Halatsız gemiler arasında rastgele yer değişimi (yeni deneme için kuyruğu biraz karıştırır).</summary>
        private static void PerturbOrder(List<WagonSequenceEntry> list, int cols, System.Random rng, int swaps)
        {
            for (int k = 0; k < swaps; k++)
            {
                int i = rng.Next(list.Count), j = rng.Next(list.Count);
                if (i != j && list[i].linkId == 0 && list[j].linkId == 0) (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static WagonSequenceEntry CloneWithoutMechanics(WagonSequenceEntry w)
        {
            return new WagonSequenceEntry(w.wagonColor, w.capacity, w.paletteIndex, w.label)
            {
                isDualColor = w.isDualColor,
                secondaryColor = w.secondaryColor,
                secondaryCapacity = w.secondaryCapacity,
            };
        }

        /// <summary>Kuyruğun kesin kural ihlalleri (level_asset.py check'teki HATA satırlarının karşılığı).</summary>
        public static List<string> ValidateQueueRules(List<WagonSequenceEntry> list, int cols)
        {
            var errors = new List<string>();
            if (list == null) return errors;
            cols = Mathf.Max(1, cols);

            for (int i = cols; i < list.Count; i++)
            {
                if (SameColor(list[i], list[i - cols])) errors.Add($"Aynı sütunda aynı renk arka arkaya: sıra {i - cols + 1} ve {i + 1}");
            }

            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < list.Count; i++)
            {
                var w = list[i];
                if (w == null) continue;
                if (w.isHidden && i < cols) errors.Add($"Gizli gemi ön sırada (sıra {i + 1}) — yanaşana kadar açılmaz");
                if (w.isHidden && w.linkId > 0) errors.Add($"Gizli gemi halatlı (sıra {i + 1})");
                if (w.linkId <= 0) continue;
                if (!groups.TryGetValue(w.linkId, out var g)) groups[w.linkId] = g = new List<int>();
                g.Add(i);
            }

            int openingPairs = 0;
            foreach (var kv in groups)
            {
                var g = kv.Value;
                if (g.Count != 2)
                {
                    errors.Add($"Halat {kv.Key}: {g.Count} gemi (yalnızca ikili halat)");
                    continue;
                }
                if (!IsValidLinkPosition(g[0], g[1], cols)) errors.Add($"Halat {kv.Key}: sıra {g[0] + 1}-{g[1] + 1} geçersiz (yan yana komşu sütun ya da başlangıç havuzunda alt alta olmalı)");
                if (SameColor(list[g[0]], list[g[1]])) errors.Add($"Halat {kv.Key}: iki gemi aynı renk");
                if (list[g[0]].isDualColor || list[g[1]].isDualColor) errors.Add($"Halat {kv.Key}: Güçlü konteyner halatlı olmaz");
                if (g[0] < cols * 3) openingPairs++;
            }
            if (openingPairs > 2) errors.Add($"Açılışta (ilk 3 sıra) {openingPairs} halat — en fazla 2");
            return errors;
        }

        /// <summary>Simülasyon sonucunun zorluk bandına uzaklığı (docs/level-tasarim/tools/design.py BANDS; Zor = adil Zor "S").</summary>
        private static double BandDistance(LevelDifficultyTier tier, LevelQueueSimulator.Result sim)
        {
            var (rLo, rHi, sLo, sHi) = tier == LevelDifficultyTier.Kolay ? (0.55, 0.80, 0.98, 1.00)
                : tier == LevelDifficultyTier.Orta ? (0.30, 0.55, 0.90, 1.00)
                : (0.12, 0.30, 0.80, 0.95);
            double Out(double v, double lo, double hi) => v < lo ? lo - v : v > hi ? v - hi : 0.0;
            return Out(sim.randomWinRate, rLo, rHi) + Out(sim.smartWinRate, sLo, sHi);
        }

        private static void ApplyBenchmarkDualColor(List<WagonSequenceEntry> list, List<PaletteColorOverride> palette, System.Random rng)
        {
            if (list == null || list.Count < 8 || palette == null || palette.Count < 2) return;

            // 1. Paletten geçerli ve küp sayısı > 0 olan renkleri belirle
            var validColors = new List<(int pIndex, PaletteColorOverride p)>();
            for (int p = 0; p < palette.Count; p++)
            {
                if (palette[p] != null && palette[p].pixelCount > 0)
                {
                    validColors.Add((p, palette[p]));
                }
            }
            if (validColors.Count < 2) return;

            // 2. Birincil aday (wA): Orta sıralarda (index 3..Count-3), halatsız, gizli olmayan, tek renkli bir vagon seç
            int startIdx = Mathf.Clamp(3, 0, list.Count - 1);
            int endIdx = Mathf.Max(startIdx, list.Count - 3);

            var candidatesA = new List<int>();
            for (int i = startIdx; i <= endIdx; i++)
            {
                var w = list[i];
                if (w != null && w.linkId == 0 && !w.isHidden && !w.isDualColor)
                {
                    candidatesA.Add(i);
                }
            }
            if (candidatesA.Count == 0) return;

            int aIdx = candidatesA[rng.Next(candidatesA.Count)];
            var wA = list[aIdx];
            int palA = wA.paletteIndex;

            // 3. İkincil aday (wB): palA'dan farklı renkte, halatsız, gizli olmayan, tek renkli bir vagon seç
            var candidatesB = new List<int>();
            for (int i = 0; i < list.Count; i++)
            {
                if (i == aIdx) continue;
                var w = list[i];
                if (w != null && w.linkId == 0 && !w.isHidden && !w.isDualColor && w.paletteIndex != palA)
                {
                    candidatesB.Add(i);
                }
            }
            if (candidatesB.Count == 0) return;

            // wA'ya mesafe olarak makul yakınlıktaki bir partneri tercih et
            candidatesB.Sort((x, y) => Mathf.Abs(x - aIdx).CompareTo(Mathf.Abs(y - aIdx)));
            int bIdx = candidatesB[0];
            var wB = list[bIdx];

            // 4. İki vagonu Çift Renkli Konteyner olarak birleştir:
            // wA birincil rengi (wA.wagonColor) ve birincil kapasitesini (wA.capacity) korur.
            // wB'nin rengi ve kapasitesi wA'nın secondaryColor ve secondaryCapacity'si olur.
            // wB listeden silinir. Böylece her iki rengin toplam taşınan küp sayısı %100 KUSURSUZ KORUNUR!
            wA.isDualColor = true;
            wA.secondaryColor = wB.wagonColor;
            wA.secondaryCapacity = wB.capacity;
            wA.linkId = 0;
            wA.isHidden = false;

            string nameA = (palA >= 0 && palA < palette.Count && !string.IsNullOrEmpty(palette[palA].label)) ? palette[palA].label : $"Renk #{palA + 1}";
            int palB = wB.paletteIndex;
            string nameB = (palB >= 0 && palB < palette.Count && !string.IsNullOrEmpty(palette[palB].label)) ? palette[palB].label : $"Renk #{palB + 1}";
            wA.label = $"Güçlü ({nameA}+{nameB})";

            // wB'yi listeden kaldır
            list.RemoveAt(bIdx);
        }

        /// <summary>Elle düzenlenmiş bir kuyruğu kaydetmeden önce kurallarla ve kuyruk simülatörüyle ölçer.</summary>
        public static (List<string> errors, LevelQueueSimulator.Result sim) CheckWorkingSequence(
            Texture2D texture, List<PaletteColorOverride> palette, List<WagonSequenceEntry> sequence, int poolRows, int poolColumns, int slotCount)
        {
            var errors = ValidateQueueRules(sequence, poolColumns);
            if (texture == null || palette == null || sequence == null || sequence.Count == 0) return (errors, default);

            PixelPaletteOptimizer.EnsureReadable(texture);
            var temp = ScriptableObject.CreateInstance<PixelLevelData>();
            try
            {
                temp.LevelTexture = texture;
                temp.OriginalSourceTexture = texture;
                temp.UseNativeResolution = true;
                temp.SkipTransparent = false;
                temp.ColorPalette.Clear();
                temp.ColorPalette.AddRange(palette);
                temp.PoolColumns = Mathf.Clamp(poolColumns, 1, 8);
                temp.PoolRows = Mathf.Clamp(poolRows, 1, 6);
                temp.SlotCount = Mathf.Clamp(slotCount, 1, 8);
                var sim = LevelQueueSimulator.Simulate(temp, sequence);
                if (sim.error != null) errors.Add("Simülasyon: " + sim.error);
                return (errors, sim);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        public static bool VerifySequenceNovelty(List<WagonSequenceEntry> newSeq)
        {
            if (newSeq == null || newSeq.Count == 0) return true;

            var seqAsset = AssetDatabase.LoadAssetAtPath<LevelSequence>("Assets/Levels/LevelSequence.asset");
            if (seqAsset == null || seqAsset.Levels == null) return true;

            string newSignature = GetSequenceSignature(newSeq);

            foreach (var lvl in seqAsset.Levels)
            {
                if (lvl == null || !lvl.UseCustomWagonSequence || lvl.WagonSequence == null) continue;
                if (lvl.WagonSequence.Count != newSeq.Count) continue;

                string existingSig = GetSequenceSignature(lvl.WagonSequence);
                if (newSignature == existingSig)
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetSequenceSignature(List<WagonSequenceEntry> list)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                var w = list[i];
                if (w == null) continue;
                sb.Append($"{w.paletteIndex}:{w.capacity}:{w.linkId}:{w.isHidden}|");
            }
            return sb.ToString();
        }

        public static PixelLevelData SaveAndRegisterNewLevel(
            Texture2D texture,
            string levelName,
            int targetIndex,
            LevelDifficultyTier tier,
            List<PaletteColorOverride> palette,
            List<WagonSequenceEntry> sequence,
            List<Vector2Int> mysteryCubes,
            MysteryRevealCondition mysteryRevealCondition,
            int poolRows,
            int poolColumns,
            int slotCount,
            int minCap,
            int peakCap,
            int maxCap,
            out string savedPath)
        {
            savedPath = "";
            if (texture == null || sequence == null || sequence.Count == 0) return null;

            if (!AssetDatabase.IsValidFolder("Assets/Levels"))
            {
                AssetDatabase.CreateFolder("Assets", "Levels");
            }

            string cleanName = string.IsNullOrEmpty(levelName) ? "Yeni_Bolum" : levelName.Replace(" ", "_");
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/Levels/Level_{targetIndex:D2}_{cleanName}.asset");

            var newLevel = ScriptableObject.CreateInstance<PixelLevelData>();
            newLevel.LevelName = levelName;
            newLevel.LevelIndex = targetIndex;
            newLevel.DifficultyTier = tier;
            newLevel.DifficultyIsManual = true;
            newLevel.LevelTexture = texture;
            newLevel.OriginalSourceTexture = texture;
            newLevel.UseNativeResolution = true;

            newLevel.CubeSpacing = 0.06f;
            newLevel.CubeSpacingX = 0f;
            newLevel.CubeDepth = 1.0f;
            newLevel.BoardTiltAngle = 18.0f;
            newLevel.CubeFrontTiltAngle = -6.0f;
            newLevel.TargetZ = 0f;
            newLevel.InnerPadding = 0.08f;
            newLevel.SkipTransparent = false;

            // Havuz & İskele Slot Ayarları
            newLevel.PoolColumns = Mathf.Clamp(poolColumns, 1, 8);
            newLevel.PoolRows = Mathf.Clamp(poolRows, 1, 6);
            newLevel.SlotCount = Mathf.Clamp(slotCount, 1, 8);
            newLevel.MinTruckCapacity = Mathf.Max(1, minCap);
            newLevel.PeakTruckCapacity = Mathf.Max(minCap, peakCap);
            newLevel.TruckCapacity = Mathf.Max(peakCap, maxCap);

            if (mysteryCubes != null && mysteryCubes.Count > 0)
            {
                newLevel.MysteryCubeCoordinates = new List<Vector2Int>(mysteryCubes);
                newLevel.MysteryRevealCondition = mysteryRevealCondition;
            }

            if (palette != null && palette.Count > 0)
            {
                newLevel.ColorPalette.Clear();
                foreach (var p in palette)
                {
                    newLevel.ColorPalette.Add(new PaletteColorOverride(p.originalColor, p.targetColor, p.pixelCount, p.label));
                }
            }
            else
            {
                newLevel.ExtractPaletteFromTexture();
            }

            newLevel.WagonSequence.Clear();
            foreach (var w in sequence)
            {
                var entry = new WagonSequenceEntry(w.wagonColor, w.capacity, w.paletteIndex, w.label, w.linkId)
                {
                    isHidden = w.isHidden,
                    isDualColor = w.isDualColor,
                    secondaryColor = w.secondaryColor,
                    secondaryCapacity = w.secondaryCapacity
                };
                newLevel.WagonSequence.Add(entry);
            }
            newLevel.UseCustomWagonSequence = true;

            AssetDatabase.CreateAsset(newLevel, assetPath);
            EditorUtility.SetDirty(newLevel);

            var seqAsset = AssetDatabase.LoadAssetAtPath<LevelSequence>("Assets/Levels/LevelSequence.asset");
            if (seqAsset != null)
            {
                Undo.RecordObject(seqAsset, "Add New Level to Sequence");
                if (!seqAsset.Levels.Contains(newLevel))
                {
                    seqAsset.Levels.Add(newLevel);
                }
                EditorUtility.SetDirty(seqAsset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            savedPath = assetPath;
            return newLevel;
        }
    }
}
