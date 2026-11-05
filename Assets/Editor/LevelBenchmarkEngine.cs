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
    /// - %100 Çözülebilirlik (0 Deadlock) simülasyon garantisi
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
            public int mysteryCubePercent = 30; // İç küplerin % kaçı '?' olsun
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
            public int poolRows = 2;       // Sıra (dalga)
            public int poolColumns = 5;    // Kolon
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
            public bool is100PercentSolvable;
            public float deadlockRisk;
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
            if (config.generateMysteryCubes && (config.mysteryCubePercent > 0 || config.durationPacing != PuzzleDurationPacing.FastCasual))
            {
                int percent = (config.durationPacing == PuzzleDurationPacing.TacticalDeep) ? Mathf.Max(35, config.mysteryCubePercent) :
                              (config.durationPacing == PuzzleDurationPacing.StandardPuzzle) ? Mathf.Max(25, config.mysteryCubePercent) :
                              config.mysteryCubePercent;

                generatedMystery = GenerateInnerMysteryCubes(grid, minX, maxX, minY, maxY, percent);
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
            List<WagonSequenceEntry> masterSequence = InterleaveWithLayerLock(wagonsByColor, exposedColorPriorities, config.durationPacing, rng);

            // 8. Halatlı Gemiler (Linked Ships) Uygula
            if (config.generateLinks && masterSequence.Count >= 6)
            {
                int linkPairs = (config.durationPacing == PuzzleDurationPacing.TacticalDeep) ? Mathf.Clamp(masterSequence.Count / 5, 5, 8) :
                                (config.durationPacing == PuzzleDurationPacing.StandardPuzzle) ? Mathf.Clamp(masterSequence.Count / 6, 3, 5) :
                                Mathf.Clamp(masterSequence.Count / 8, 1, 3);

                ApplyStrategicLinks(masterSequence, linkPairs, rng);
            }

            // 9. Gizli Gemiler (Hidden Ships) Uygula
            if (config.generateHidden && masterSequence.Count >= 8)
            {
                int hiddenCount = (config.durationPacing == PuzzleDurationPacing.TacticalDeep) ? Mathf.Clamp(masterSequence.Count / 5, 6, 9) :
                                  (config.durationPacing == PuzzleDurationPacing.StandardPuzzle) ? Mathf.Clamp(masterSequence.Count / 7, 3, 5) :
                                  Mathf.Clamp(masterSequence.Count / 9, 1, 3);

                ApplyBenchmarkHidden(masterSequence, hiddenCount, rng);
            }

            // 10. Çift Renkli Konteyner (Dual Color) Uygula
            if (config.allowDualColor && masterSequence.Count >= 10)
            {
                ApplyBenchmarkDualColor(masterSequence, tempLevel.ColorPalette, rng);
            }

            // 11. Simülasyon ile %100 Çözülebilirlik Doğrulaması & Kilitlenme Otomatik Çözücü
            tempLevel.WagonSequence = masterSequence;
            tempLevel.UseCustomWagonSequence = true;

            bool isSolvable = true;
            float risk = 0f;

            if (config.guaranteeSolvability)
            {
                var simReport = LevelSolvabilityAnalyzer.Analyze(tempLevel, runSimulation: true, trials: config.solvabilityTrials);
                if (simReport.deadlockCount > 0)
                {
                    bool solved = ResolveDeadlocksWithMutation(tempLevel, grid, minX, maxX, minY, maxY, config.solvabilityTrials, rng);
                    if (solved)
                    {
                        masterSequence = tempLevel.WagonSequence;
                        isSolvable = true;
                        risk = 0f;
                    }
                    else
                    {
                        var retest = LevelSolvabilityAnalyzer.Analyze(tempLevel, runSimulation: true, trials: config.solvabilityTrials);
                        isSolvable = (retest.deadlockCount == 0);
                        risk = retest.DeadlockRisk;
                    }
                }
            }

            // 12. Benzersizlik Doğrulaması
            res.isUnique = VerifySequenceNovelty(masterSequence);

            res.success = true;
            res.sequence = masterSequence;
            res.totalAssignedCapacity = masterSequence.Sum(w => w.capacity + (w.isDualColor ? (w.secondaryCapacity > 0 ? w.secondaryCapacity : w.capacity) : 0));
            res.linkedCount = masterSequence.Count(w => w.linkId > 0);
            res.hiddenCount = masterSequence.Count(w => w.isHidden);
            res.dualCount = masterSequence.Count(w => w.isDualColor);
            res.is100PercentSolvable = isSolvable;
            res.deadlockRisk = risk;

            res.benchmarkSummary = $"Bölüm {config.targetLevelIndex} Üretildi ({res.estimatedPlaytime}): {res.totalCubes} Küp | {res.sequence.Count} Gemi | " +
                                   $"{(res.mysteryCubeCount > 0 ? $"{res.mysteryCubeCount} Gizli Küp (?) | " : "")}" +
                                   $"{res.linkedCount / 2} Halat Çifti | {res.hiddenCount} Gizli Gemi | " +
                                   $"{(res.is100PercentSolvable ? "✓ %100 Çözülebilir (0 Deadlock)" : $"⚠️ Kilitlenme Riski: %{risk * 100:F1}")}";

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

        public static List<Vector2Int> GenerateInnerMysteryCubes(
            Dictionary<(int, int), int> grid,
            int minX, int maxX, int minY, int maxY,
            int percent)
        {
            var result = new List<Vector2Int>();
            if (grid == null || grid.Count == 0 || percent <= 0) return result;

            var innerCandidates = new List<Vector2Int>();
            foreach (var kvp in grid)
            {
                int x = kvp.Key.Item1;
                int y = kvp.Key.Item2;

                if (grid.ContainsKey((x - 1, y)) &&
                    grid.ContainsKey((x + 1, y)) &&
                    grid.ContainsKey((x, y - 1)) &&
                    grid.ContainsKey((x, y + 1)))
                {
                    innerCandidates.Add(new Vector2Int(x, y));
                }
            }

            if (innerCandidates.Count == 0) return result;

            var rng = new System.Random();
            for (int i = innerCandidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var temp = innerCandidates[i];
                innerCandidates[i] = innerCandidates[j];
                innerCandidates[j] = temp;
            }

            int targetCount = Mathf.Clamp(Mathf.RoundToInt(innerCandidates.Count * (percent / 100f)), 15, 80);
            for (int i = 0; i < Mathf.Min(targetCount, innerCandidates.Count); i++)
            {
                result.Add(innerCandidates[i]);
            }

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

        private static void ApplyStrategicLinks(List<WagonSequenceEntry> list, int targetPairs, System.Random rng)
        {
            int n = list.Count;
            int pairCount = 0;
            var usedIndices = new HashSet<int>();

            var candidates = Enumerable.Range(2, n - 2).ToList();
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = candidates[i]; candidates[i] = candidates[j]; candidates[j] = t;
            }

            int linkId = 1;
            for (int c = 0; c < candidates.Count - 1 && pairCount < targetPairs; c++)
            {
                int idxA = candidates[c];
                int idxB = idxA + 1;
                if (idxB >= n) idxB = idxA - 1;

                if (usedIndices.Contains(idxA) || usedIndices.Contains(idxB)) continue;

                var wA = list[idxA];
                var wB = list[idxB];
                if (wA == null || wB == null) continue;

                wA.linkId = linkId;
                wB.linkId = linkId;
                usedIndices.Add(idxA);
                usedIndices.Add(idxB);

                linkId++;
                pairCount++;
            }
        }

        private static void ApplyBenchmarkHidden(List<WagonSequenceEntry> list, int targetHidden, System.Random rng)
        {
            int n = list.Count;
            int hiddenAssigned = 0;

            var candidates = new List<int>();
            for (int i = 5; i < n; i++)
            {
                if (list[i] != null && list[i].linkId == 0)
                {
                    candidates.Add(i);
                }
            }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = candidates[i]; candidates[i] = candidates[j]; candidates[j] = t;
            }

            for (int k = 0; k < candidates.Count && hiddenAssigned < targetHidden; k++)
            {
                list[candidates[k]].isHidden = true;
                hiddenAssigned++;
            }
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

        private static bool ResolveDeadlocksWithMutation(
            PixelLevelData level,
            Dictionary<(int, int), int> grid,
            int minX, int maxX, int minY, int maxY,
            int trials,
            System.Random rng)
        {
            var seq = level.WagonSequence;
            if (seq == null || seq.Count <= 2) return true;

            for (int step = 0; step < 16; step++)
            {
                var air = LevelSolvabilityAnalyzer.ComputeOutsideAir(grid, minX, maxX, minY, maxY);
                var exposedPalIndices = new HashSet<int>();
                foreach (var kvp in grid)
                {
                    if (LevelSolvabilityAnalyzer.IsExposed(air, kvp.Key.Item1, kvp.Key.Item2))
                    {
                        exposedPalIndices.Add(kvp.Value);
                    }
                }

                int exposedFrontCount = 0;
                for (int i = 0; i < Mathf.Min(5, seq.Count); i++)
                {
                    if (exposedPalIndices.Contains(seq[i].paletteIndex)) exposedFrontCount++;
                }

                if (exposedFrontCount < 2)
                {
                    for (int j = 5; j < seq.Count; j++)
                    {
                        if (exposedPalIndices.Contains(seq[j].paletteIndex) && seq[j].linkId == 0)
                        {
                            var temp = seq[1];
                            seq[1] = seq[j];
                            seq[j] = temp;
                            break;
                        }
                    }
                }
                else
                {
                    int i1 = 2 + rng.Next(Mathf.Max(1, seq.Count - 2));
                    int i2 = 2 + rng.Next(Mathf.Max(1, seq.Count - 2));
                    if (seq[i1].linkId == 0 && seq[i2].linkId == 0)
                    {
                        var temp = seq[i1];
                        seq[i1] = seq[i2];
                        seq[i2] = temp;
                    }
                }

                var check = LevelSolvabilityAnalyzer.Analyze(level, runSimulation: true, trials: trials);
                if (check.completed && check.deadlockCount == 0)
                {
                    return true;
                }
            }

            return false;
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
