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
                    poolRows = 2,
                    poolColumns = 5,
                    slotCount = 5
                };

                var res = LevelBenchmarkEngine.GenerateFromTexture(tex, config);
                if (!res.success)
                {
                    Debug.LogError($"[Batch Kolay] Seviye {lvlIdx} üretilemedi: {res.errorMessage}");
                    continue;
                }

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

                Debug.Log($"[Batch Kolay ✓] Seviye {lvlIdx} ({item.name}) kaydedildi: {savedPath} | {res.totalCubes} Küp | {res.sequence.Count} Gemi | %100 Çözülebilir");
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
                    mysteryCubePercent = 25,
                    mysteryRevealCondition = MysteryRevealCondition.WhenExposed,
                    preferRoundCapacities = true,
                    roundRatio = 1.0f,
                    allowDualColor = true,
                    guaranteeSolvability = true,
                    solvabilityTrials = 80,
                    minCapacity = 10,
                    peakCapacity = 20,
                    maxCapacity = 30,
                    poolRows = 2,
                    poolColumns = 5,
                    slotCount = 5
                };

                var res = LevelBenchmarkEngine.GenerateFromTexture(tex, config);
                if (!res.success)
                {
                    Debug.LogError($"[Batch Orta] Seviye {lvlIdx} üretilemedi: {res.errorMessage}");
                    continue;
                }

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

                Debug.Log($"[Batch Orta ✓] Seviye {lvlIdx} ({item.name}) kaydedildi: {savedPath} | {res.totalCubes} Küp | {res.sequence.Count} Gemi | {res.dualCount} Güçlü | %100 Çözülebilir");
                successCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=#00FF88>🎉 [Batch Tamamlandı] Toplam {successCount}/20 yeni seviye başarıyla üretildi ve LevelSequence'e eklendi!</color>");
        }
    }
}
