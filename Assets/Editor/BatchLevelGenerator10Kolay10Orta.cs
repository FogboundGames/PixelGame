using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class BatchLevelGenerator10Kolay10Orta
    {
        [MenuItem("Tools/PixelGame/🚀 10 Kolay 10 Orta Level Üret")]
        public static void Generate20Levels()
        {
            var kolayList = new (string path, string name)[]
            {
                ("Assets/04.11.2026/kolay/AdaPalmiye.png", "Ada Palmiye"),
                ("Assets/04.11.2026/kolay/Avokado.png", "Avokado"),
                ("Assets/04.11.2026/kolay/Balon.png", "Ucan Balon"),
                ("Assets/04.11.2026/kolay/Bambu.png", "Bambu"),
                ("Assets/04.11.2026/kolay/BoyamaPaleti.png", "Boyama Paleti"),
                ("Assets/04.11.2026/kolay/CanYelegi.png", "Can Yelegi"),
                ("Assets/04.11.2026/kolay/CicekSaksisi.png", "Cicek Saksisi"),
                ("Assets/04.11.2026/kolay/DenizFeneriAda.png", "Deniz Feneri Ada"),
                ("Assets/04.11.2026/kolay/DondurmaKulah.png", "Dondurma Kulah"),
                ("Assets/04.11.2026/kolay/GunbatimiYelken.png", "Gunbatimi Yelken")
            };

            var ortaList = new (string path, string name)[]
            {
                ("Assets/04.11.2026/orta/AntikKilic.png", "Antik Kilic"),
                ("Assets/04.11.2026/orta/AntikKutuphane.png", "Antik Kutuphane"),
                ("Assets/04.11.2026/orta/AntikPiramitGece.png", "Antik Piramit Gece"),
                ("Assets/04.11.2026/orta/AntikTerazi.png", "Antik Terazi"),
                ("Assets/04.11.2026/orta/ArcadeKol.png", "Arcade Kol"),
                ("Assets/04.11.2026/orta/CapaHalat.png", "Capa Halat"),
                ("Assets/04.11.2026/orta/ColVahasiGece.png", "Col Vahasi Gece"),
                ("Assets/04.11.2026/orta/DenizaltiDunyasi.png", "Denizalti Dunyasi"),
                ("Assets/04.11.2026/orta/DikisMakinesi.png", "Dikis Makinesi"),
                ("Assets/04.11.2026/orta/GelenekselCayBahcesi.png", "Geleneksel Cay Bahcesi")
            };

            int startIndex = 166;
            int successCount = 0;

            // 1. 10 Kolay Seviye Üret
            for (int i = 0; i < kolayList.Length; i++)
            {
                int lvlIdx = startIndex + i;
                var item = kolayList[i];
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(item.path);
                if (tex == null)
                {
                    Debug.LogError($"[Batch] Doku bulunamadı: {item.path}");
                    continue;
                }

                var config = new LevelBenchmarkEngine.GenerationConfig
                {
                    preset = LevelBenchmarkEngine.DifficultyPreset.Kolay,
                    durationPacing = LevelBenchmarkEngine.PuzzleDurationPacing.FastCasual,
                    targetLevelIndex = lvlIdx,
                    levelName = item.name,
                    generateLinks = true,
                    generateHidden = true,
                    generateMysteryCubes = false,
                    mysteryCubePercent = 0,
                    preferRoundCapacities = true,
                    roundRatio = 1.0f,
                    allowDualColor = false,
                    guaranteeSolvability = true,
                    solvabilityTrials = 80,
                    minCapacity = 10,
                    peakCapacity = 20,
                    maxCapacity = 30,
                    poolRows = 3,
                    poolColumns = 3, // Kolay her zaman 3 sütun
                    slotCount = 5
                };

                var res = LevelBenchmarkEngine.GenerateFromTexture(tex, config);
                if (!IsSaveable(res, $"[Batch Kolay] Seviye {lvlIdx} ({item.name})")) continue;

                LevelBenchmarkEngine.SaveAndRegisterNewLevel(
                    tex,
                    item.name,
                    lvlIdx,
                    LevelDifficultyTier.Kolay,
                    res.palette,
                    res.sequence,
                    res.mysteryCubes,
                    res.mysteryRevealCondition,
                    config.poolRows,
                    config.poolColumns,
                    config.slotCount,
                    config.minCapacity,
                    config.peakCapacity,
                    config.maxCapacity,
                    out string savedPath);

                Debug.Log($"[Batch Kolay ✓] Seviye {lvlIdx} ({item.name}) kaydedildi: {savedPath} | {res.totalCubes} Küp | {res.sequence.Count} Gemi | rastgele %{res.simRandomWinRate * 100:F0}, akıllı %{res.simSmartWinRate * 100:F0}");
                successCount++;
            }

            // 2. 10 Orta Seviye Üret
            for (int i = 0; i < ortaList.Length; i++)
            {
                int lvlIdx = startIndex + kolayList.Length + i;
                var item = ortaList[i];
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(item.path);
                if (tex == null)
                {
                    Debug.LogError($"[Batch] Doku bulunamadı: {item.path}");
                    continue;
                }

                var config = new LevelBenchmarkEngine.GenerationConfig
                {
                    preset = LevelBenchmarkEngine.DifficultyPreset.Orta,
                    durationPacing = LevelBenchmarkEngine.PuzzleDurationPacing.StandardPuzzle,
                    targetLevelIndex = lvlIdx,
                    levelName = item.name,
                    generateLinks = true,
                    generateHidden = true,
                    generateMysteryCubes = true,
                    mysteryCubePercent = 4,
                    mysteryRevealCondition = MysteryRevealCondition.WhenExposed,
                    preferRoundCapacities = true,
                    roundRatio = 1.0f,
                    allowDualColor = true,
                    guaranteeSolvability = true,
                    solvabilityTrials = 80,
                    minCapacity = 10,
                    peakCapacity = 20,
                    maxCapacity = 30,
                    poolRows = 3,
                    poolColumns = PoolColumnsFor(tex),
                    slotCount = 5
                };

                var res = LevelBenchmarkEngine.GenerateFromTexture(tex, config);
                if (!IsSaveable(res, $"[Batch Orta] Seviye {lvlIdx} ({item.name})")) continue;

                LevelBenchmarkEngine.SaveAndRegisterNewLevel(
                    tex,
                    item.name,
                    lvlIdx,
                    LevelDifficultyTier.Orta,
                    res.palette,
                    res.sequence,
                    res.mysteryCubes,
                    res.mysteryRevealCondition,
                    config.poolRows,
                    config.poolColumns,
                    config.slotCount,
                    config.minCapacity,
                    config.peakCapacity,
                    config.maxCapacity,
                    out string savedPath);

                Debug.Log($"[Batch Orta ✓] Seviye {lvlIdx} ({item.name}) kaydedildi: {savedPath} | {res.totalCubes} Küp | {res.sequence.Count} Gemi | {res.dualCount} Güçlü | rastgele %{res.simRandomWinRate * 100:F0}, akıllı %{res.simSmartWinRate * 100:F0}");
                successCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=#00FF88>🎉 [Batch Tamamlandı] Toplam {successCount}/20 yeni seviye başarıyla üretildi ve LevelSequence'e eklendi!</color>");
        }

        // Rehber: kuyruk çoğunlukla 4 sütun; ≤~23 gemilik küçük levellerde 3
        private static int PoolColumnsFor(Texture2D tex)
        {
            if (tex == null) return 4;
            PixelPaletteOptimizer.EnsureReadable(tex);
            int cubes = 0;
            foreach (var p in tex.GetPixels32()) if (p.a > 0) cubes++;
            return cubes < 600 ? 3 : 4;
        }

        /// <summary>Kural hatası olan ya da kuyruk simülatöründe çözülemeyen level kaydedilmez.</summary>
        private static bool IsSaveable(LevelBenchmarkEngine.GenerationResult res, string tag)
        {
            if (!res.success)
            {
                Debug.LogError($"{tag} üretilemedi: {res.errorMessage}");
                return false;
            }
            if (!res.is100PercentSolvable)
            {
                Debug.LogError($"{tag} KAYDEDİLMEDİ: {res.benchmarkSummary}");
                return false;
            }
            foreach (var w in res.difficultyWarnings) Debug.LogWarning($"{tag}: {w}");
            return true;
        }
    }
}
