using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class AnalyzeAllLevelsBenchmark
    {
        [MenuItem("Tools/PixelGame/🔬 Benchmark Tüm Seviyeleri Analiz Et")]
        public static void RunAnalysis()
        {
            var seq = AssetDatabase.LoadAssetAtPath<LevelSequence>("Assets/Levels/LevelSequence.asset");
            if (seq == null || seq.Levels == null || seq.Levels.Count == 0)
            {
                Debug.LogError("[Benchmark] LevelSequence.asset bulunamadı veya boş!");
                return;
            }

            var levels = seq.Levels.Where(l => l != null).ToList();
            int totalLevels = levels.Count;
            Debug.Log($"[Benchmark] {totalLevels} seviye analiz ediliyor...");

            var sb = new StringBuilder();
            sb.AppendLine("# PixelGame 162 Seviye Sıra & Mekanik Analiz Raporu");
            sb.AppendLine($"Tarih: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Toplam Seviye Sayısı: {totalLevels}");
            sb.AppendLine();

            // 1. Genel İstatistikler
            List<int> cubeCounts = new List<int>();
            List<int> colorCounts = new List<int>();
            List<int> wagonCounts = new List<int>();
            Dictionary<int, int> capacityFrequencies = new Dictionary<int, int>();
            Dictionary<int, int> paletteColorCountDist = new Dictionary<int, int>();

            int levelsWithCustomSequence = 0;
            int levelsWithLinkedShips = 0;
            int totalLinkedShips = 0;
            int levelsWithHiddenShips = 0;
            int totalHiddenShips = 0;
            int levelsWithDualColor = 0;
            int totalDualColorShips = 0;
            int levelsWithMysteryCubes = 0;
            int totalMysteryCubes = 0;

            int countKolay = 0, countOrta = 0, countZor = 0;

            // Kapasite aralıkları
            int roundCapacityCount = 0;
            int nonRoundCapacityCount = 0;

            // Gizli gemilerin sıra indeksleri
            List<int> hiddenIndices = new List<int>();

            // Halatlı gemilerin sıra indeksleri
            List<int> linkedIndices = new List<int>();

            // Seviye aralıklarına göre analiz (1-30, 31-70, 71-120, 121-162)
            var phaseStats = new Dictionary<string, (int count, int cubes, int colors, int wagons, int linked, int hidden)>();
            void AddPhase(string phase, int cubes, int colors, int wagons, int linked, int hidden)
            {
                if (!phaseStats.ContainsKey(phase))
                    phaseStats[phase] = (0, 0, 0, 0, 0, 0);
                var cur = phaseStats[phase];
                phaseStats[phase] = (cur.count + 1, cur.cubes + cubes, cur.colors + colors, cur.wagons + wagons, cur.linked + linked, cur.hidden + hidden);
            }

            for (int i = 0; i < totalLevels; i++)
            {
                var lvl = levels[i];
                int lvlNum = i + 1;

                int cubes = lvl.GetTotalCubeCountInPalette();
                cubeCounts.Add(cubes);

                int colors = lvl.ColorPalette != null ? lvl.ColorPalette.Count(p => p != null && p.pixelCount > 0) : 0;
                colorCounts.Add(colors);
                if (!paletteColorCountDist.ContainsKey(colors)) paletteColorCountDist[colors] = 0;
                paletteColorCountDist[colors]++;

                if (lvl.DifficultyTier == LevelDifficultyTier.Kolay) countKolay++;
                else if (lvl.DifficultyTier == LevelDifficultyTier.Orta) countOrta++;
                else if (lvl.DifficultyTier == LevelDifficultyTier.Zor) countZor++;

                int mysteries = lvl.GetMysteryCubeCount();
                if (mysteries > 0)
                {
                    levelsWithMysteryCubes++;
                    totalMysteryCubes += mysteries;
                }

                int wagons = 0;
                int linkedInLvl = 0;
                int hiddenInLvl = 0;
                int dualInLvl = 0;

                if (lvl.UseCustomWagonSequence && lvl.WagonSequence != null && lvl.WagonSequence.Count > 0)
                {
                    levelsWithCustomSequence++;
                    wagons = lvl.WagonSequence.Count;
                    wagonCounts.Add(wagons);

                    for (int w = 0; w < lvl.WagonSequence.Count; w++)
                    {
                        var entry = lvl.WagonSequence[w];
                        if (entry == null) continue;

                        int cap = entry.capacity;
                        if (!capacityFrequencies.ContainsKey(cap)) capacityFrequencies[cap] = 0;
                        capacityFrequencies[cap]++;

                        if (cap % 10 == 0) roundCapacityCount++;
                        else nonRoundCapacityCount++;

                        if (entry.linkId > 0)
                        {
                            linkedInLvl++;
                            linkedIndices.Add(w);
                        }

                        if (entry.isHidden)
                        {
                            hiddenInLvl++;
                            hiddenIndices.Add(w);
                        }

                        if (entry.isDualColor)
                        {
                            dualInLvl++;
                        }
                    }
                }
                else
                {
                    // Otomatik hesaplanan vagon sayısı
                    wagons = lvl.GetRequiredTruckCount();
                    wagonCounts.Add(wagons);
                }

                if (linkedInLvl > 0)
                {
                    levelsWithLinkedShips++;
                    totalLinkedShips += linkedInLvl;
                }

                if (hiddenInLvl > 0)
                {
                    levelsWithHiddenShips++;
                    totalHiddenShips += hiddenInLvl;
                }

                if (dualInLvl > 0)
                {
                    levelsWithDualColor++;
                    totalDualColorShips += dualInLvl;
                }

                string phaseName = lvlNum <= 20 ? "Giriş (1-20)" :
                                   lvlNum <= 60 ? "Erken-Orta (21-60)" :
                                   lvlNum <= 110 ? "Orta-İleri (61-110)" : "Usta (111-162)";
                AddPhase(phaseName, cubes, colors, wagons, linkedInLvl, hiddenInLvl);
            }

            sb.AppendLine("## 1. Genel Dağılım");
            sb.AppendLine($"- **Ortalama Küp Sayısı**: {cubeCounts.Average():F1} (Min: {cubeCounts.Min()}, Max: {cubeCounts.Max()})");
            sb.AppendLine($"- **Ortalama Renk Sayısı**: {colorCounts.Average():F1} (Min: {colorCounts.Min()}, Max: {colorCounts.Max()})");
            sb.AppendLine($"- **Ortalama Vagon/Gemi Sayısı**: {wagonCounts.Average():F1} (Min: {wagonCounts.Min()}, Max: {wagonCounts.Max()})");
            sb.AppendLine($"- **Özel Vagon Sırası Kullanan Seviyeler**: {levelsWithCustomSequence}/{totalLevels} (%{(levelsWithCustomSequence * 100f / totalLevels):F1})");
            sb.AppendLine($"- **Zorluk Kademeleri Dağılımı**: Kolay: {countKolay}, Orta: {countOrta}, Zor: {countZor}");
            sb.AppendLine();

            sb.AppendLine("## 2. Renk Sayısı Dağılımı (Bölüm Başına)");
            foreach (var kvp in paletteColorCountDist.OrderBy(k => k.Key))
            {
                sb.AppendLine($"- **{kvp.Key} Renk**: {kvp.Value} bölüm (%{(kvp.Value * 100f / totalLevels):F1})");
            }
            sb.AppendLine();

            sb.AppendLine("## 3. Gemi Kapasiteleri Dağılımı (Sık Gelen Kapasiteler)");
            int totalCapEntries = roundCapacityCount + nonRoundCapacityCount;
            sb.AppendLine($"- **Yuvarlak Kapasite (10, 20 vb.) Oranı**: {roundCapacityCount}/{totalCapEntries} (%{(totalCapEntries > 0 ? (roundCapacityCount * 100f / totalCapEntries) : 0):F1})");
            sb.AppendLine($"- **Ara Kapasiteler (8, 12, 14, 16 vb.) Oranı**: {nonRoundCapacityCount}/{totalCapEntries} (%{(totalCapEntries > 0 ? (nonRoundCapacityCount * 100f / totalCapEntries) : 0):F1})");
            sb.AppendLine("### En Çok Kullanılan Kapasiteler:");
            foreach (var kvp in capacityFrequencies.OrderByDescending(k => k.Value).Take(10))
            {
                sb.AppendLine($"- Kapasite **{kvp.Key}**: {kvp.Value} gemide kullanıldı (%{(kvp.Value * 100f / Mathf.Max(1, totalCapEntries)):F1})");
            }
            sb.AppendLine();

            sb.AppendLine("## 4. Özel Mekanikler (Halat, Gizli, Çift Renk, Soru İşareti)");
            sb.AppendLine($"- **Halatlı Gemi (Linked Ships)**: {levelsWithLinkedShips}/{totalLevels} seviyede var. Toplam {totalLinkedShips} gemi ({totalLinkedShips / 2} çift).");
            sb.AppendLine($"- **Gizli Gemi (Hidden Ships)**: {levelsWithHiddenShips}/{totalLevels} seviyede var. Toplam {totalHiddenShips} gizli gemi.");
            sb.AppendLine($"- **Çift Renkli Gemi (Dual-Color)**: {levelsWithDualColor}/{totalLevels} seviyede var. Toplam {totalDualColorShips} gemi.");
            sb.AppendLine($"- **Gizli Küpler (Mystery Cubes)**: {levelsWithMysteryCubes}/{totalLevels} seviyede var. Toplam {totalMysteryCubes} küp.");
            sb.AppendLine();

            if (hiddenIndices.Count > 0)
            {
                sb.AppendLine($"### Gizli Gemi Sıra Konumları:");
                sb.AppendLine($"- En erken gizli gemi indeksi: Sıra #{hiddenIndices.Min() + 1}");
                sb.AppendLine($"- Ortalama gizli gemi indeksi: Sıra #{hiddenIndices.Average() + 1:F1}");
                sb.AppendLine($"- İlk 5 sırada gizli gemi var mı?: {(hiddenIndices.Any(x => x < 5) ? "Evet (Önerilmez)" : "Hayır (En erken 6. sıradan başlıyor, doğru kural)")}");
                sb.AppendLine();
            }

            sb.AppendLine("## 5. İlerleme Fazlarına Göre İstatistikler (Progression Pacing)");
            sb.AppendLine("| Faz | Bölüm | Ort. Küp | Ort. Renk | Ort. Gemi | Halatlı/Bölüm | Gizli/Bölüm |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var kvp in phaseStats)
            {
                var s = kvp.Value;
                sb.AppendLine($"| {kvp.Key} | {s.count} | {(s.cubes / (float)s.count):F1} | {(s.colors / (float)s.count):F1} | {(s.wagons / (float)s.count):F1} | {(s.linked / (float)s.count):F1} | {(s.hidden / (float)s.count):F1} |");
            }
            sb.AppendLine();

            // Referans Görsel Ayarları (Visual Tuning)
            var refLvl = levels.FirstOrDefault(l => l.LevelName.Contains("Rakun")) ?? levels[0];
            sb.AppendLine("## 6. Standart 3D Görsel Kalibrasyon Parametreleri");
            sb.AppendLine($"- CubeSpacing: {refLvl.CubeSpacing}");
            sb.AppendLine($"- CubeSpacingX: {refLvl.CubeSpacingX}");
            sb.AppendLine($"- CubeDepth: {refLvl.CubeDepth}");
            sb.AppendLine($"- BoardTiltAngle: {refLvl.BoardTiltAngle}");
            sb.AppendLine($"- CubeFrontTiltAngle: {refLvl.CubeFrontTiltAngle}");
            sb.AppendLine($"- TargetZ: {refLvl.TargetZ}");
            sb.AppendLine($"- InnerPadding: {refLvl.InnerPadding}");
            sb.AppendLine($"- SkipTransparent: {refLvl.SkipTransparent}");
            sb.AppendLine($"- PoolColumns: {refLvl.PoolColumns}, PoolRows: {refLvl.PoolRows}");

            string reportPath = "Assets/Editor/LevelBenchmarkReport.md";
            File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();

            Debug.Log($"[Benchmark] Analiz tamamlandı! Rapor kaydedildi: {reportPath}");
            Debug.Log(sb.ToString());
        }
    }
}

