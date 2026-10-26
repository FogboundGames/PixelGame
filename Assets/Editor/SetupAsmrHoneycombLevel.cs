using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupAsmrHoneycombLevel
    {
        private const string TexturePath = "Assets/32x32/BalPetegi.png";
        private const string LevelAssetPath = "Assets/Levels/Level_41_BalPetegi.asset";
        private const string SequenceAssetPath = "Assets/Levels/LevelSequence.asset";

        static SetupAsmrHoneycombLevel()
        {
            EditorApplication.delayCall += EnsureLevelCreated;
        }

        [MenuItem("Tools/PixelGame/🍯 ASMR Bal Peteği Seviyesini Kur ve Güncelle", priority = 10)]
        public static void EnsureLevelCreated()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // 1. Doku Ayarlarını Doğrula
            TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (!importer.isReadable) { importer.isReadable = true; dirty = true; }
                if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; dirty = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
                if (dirty)
                {
                    importer.SaveAndReimport();
                }
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (tex == null)
            {
                Debug.LogWarning($"[SetupAsmrHoneycombLevel] Görsel bulunamadı: {TexturePath}");
                return;
            }

            // 2. Seviye Assetini Bul ya da Oluştur
            PixelLevelData level = AssetDatabase.LoadAssetAtPath<PixelLevelData>(LevelAssetPath);
            bool isNew = false;
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<PixelLevelData>();
                isNew = true;
            }

            level.LevelName = "Bal Peteği";
            level.LevelIndex = 41;
            level.DifficultyTier = LevelDifficultyTier.Orta;
            level.DifficultyIsManual = true;
            level.LevelTexture = tex;
            level.OriginalSourceTexture = tex;
            level.UseNativeResolution = true;
            level.SlotCount = 4; // 4 İskele Slotu (Akıcı, ASMR rahatlığında oyun deneyimi)

            // 3. Palet ve Vagon Dizilimi
            level.ExtractPaletteFromTexture();

            // Sevimli ve anlaşılır etiketler ata
            if (level.ColorPalette != null)
            {
                for (int i = 0; i < level.ColorPalette.Count; i++)
                {
                    var p = level.ColorPalette[i];
                    Color c = p.originalColor;
                    if (c.r > 0.9f && c.g > 0.9f && c.b > 0.9f)
                        p.label = "Bal Parıltısı & Kanat";
                    else if (c.r > 0.85f && c.g > 0.65f)
                        p.label = "Altın Bal";
                    else if (c.r > 0.7f && c.g > 0.4f)
                        p.label = "Süzülen Bal";
                    else if (c.r > 0.5f && c.g > 0.25f)
                        p.label = "Petek Çerçevesi";
                    else
                        p.label = "Koyu Çikolata / Arı";
                }
            }

            if (level.WagonSequence == null || level.WagonSequence.Count == 0)
            {
                level.GenerateInterleavedWagonSequenceFromPalette();
            }
            level.UseCustomWagonSequence = true;

            // 4. Görsel Kalibrasyon & 3D Açı Ayarları (Referans Seviyeden Kopyala)
            PixelLevelData refLevel = AssetDatabase.LoadAssetAtPath<PixelLevelData>("Assets/Levels/Level_39_Cupcake.asset");
            if (refLevel != null)
            {
                level.CubeSpacing = refLevel.CubeSpacing;
                level.CubeSpacingX = refLevel.CubeSpacingX;
                level.CubeDepth = refLevel.CubeDepth;
                level.BoardTiltAngle = refLevel.BoardTiltAngle;
                level.CubeFrontTiltAngle = refLevel.CubeFrontTiltAngle;
                level.CubeRowStepOffset = refLevel.CubeRowStepOffset;
                level.InnerPadding = refLevel.InnerPadding;
                level.TruckCapacity = refLevel.TruckCapacity;
                level.MinTruckCapacity = refLevel.MinTruckCapacity;
                level.PeakTruckCapacity = refLevel.PeakTruckCapacity;
                level.UseRoundCapacities = refLevel.UseRoundCapacities;
                level.RoundCapacityRatio = refLevel.RoundCapacityRatio;
            }
            else
            {
                level.BoardTiltAngle = 18f;
                level.CubeFrontTiltAngle = 25f;
                level.InnerPadding = 0.08f;
                level.TruckCapacity = 30;
                level.MinTruckCapacity = 4;
                level.PeakTruckCapacity = 10;
                level.UseRoundCapacities = true;
                level.RoundCapacityRatio = 0.8f;
            }

            // 5. Çözülebilirlik Simülasyonu
            var rep = LevelSolvabilityAnalyzer.Analyze(level, runSimulation: true, trials: 40);
            if (!rep.completed || rep.deadlockCount > 0)
            {
                // Gerekiyorsa slot sayısını artır veya vagonları hafif karıştır
                level.SlotCount = 5;
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(level, LevelAssetPath);
                Debug.Log($"<color=#FAB919>🍯 [ASMR Level]</color> 'Assets/Levels/Level_41_BalPetegi.asset' başarıyla oluşturuldu!");
            }
            else
            {
                EditorUtility.SetDirty(level);
                Debug.Log($"<color=#FAB919>🍯 [ASMR Level]</color> 'Assets/Levels/Level_41_BalPetegi.asset' güncellendi!");
            }

            // 6. LevelSequence Listesine Ekle
            LevelSequence sequence = AssetDatabase.LoadAssetAtPath<LevelSequence>(SequenceAssetPath);
            if (sequence != null)
            {
                if (!sequence.Levels.Contains(level))
                {
                    sequence.Levels.Add(level);
                    EditorUtility.SetDirty(sequence);
                    Debug.Log($"<color=#FAB919>🍯 [ASMR Level]</color> 'Bal Peteği' bölümü LevelSequence oynanış listesine eklendi! (Toplam {sequence.Levels.Count} Seviye)");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
