using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupYapbozLevel
    {
        private const string TexturePath = "Assets/32x32/Yapboz.png";
        private const string LevelAssetPath = "Assets/Levels/Level_42_Yapboz.asset";
        private const string SequenceAssetPath = "Assets/Levels/LevelSequence.asset";

        static SetupYapbozLevel()
        {
            // Otomatik çalıştırma kapalı: Seviye ayarlarını ve sıra indeksini ezmemesi için.
            // EditorApplication.delayCall += EnsureYapbozLevel;
        }

        // [MenuItem("Tools/PixelGame/🧩 ASMR Piksel Yapboz Seviyesini Kur ve Güncelle", priority = 11)]
        public static void EnsureYapbozLevel()
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
                Debug.LogWarning($"[SetupYapbozLevel] Görsel bulunamadı: {TexturePath}");
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

            level.LevelName = "Piksel Yapboz";
            level.LevelIndex = 42;
            level.DifficultyTier = LevelDifficultyTier.Orta;
            level.DifficultyIsManual = true;
            level.LevelTexture = tex;
            level.OriginalSourceTexture = tex;
            level.UseNativeResolution = true;
            level.SlotCount = 4;

            // Görsel Kalibrasyon
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
            }
            else
            {
                level.BoardTiltAngle = 18f;
                level.CubeFrontTiltAngle = 25f;
                level.InnerPadding = 0.08f;
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(level, LevelAssetPath);
                Debug.Log($"<color=#14D2EB>🧩 [ASMR Level]</color> 'Assets/Levels/Level_42_Yapboz.asset' başarıyla oluşturuldu!");
            }
            else
            {
                EditorUtility.SetDirty(level);
                Debug.Log($"<color=#14D2EB>🧩 [ASMR Level]</color> 'Assets/Levels/Level_42_Yapboz.asset' doğrulandı!");
            }

            // 3. LevelSequence
            LevelSequence sequence = AssetDatabase.LoadAssetAtPath<LevelSequence>(SequenceAssetPath);
            if (sequence != null)
            {
                if (!sequence.Levels.Contains(level))
                {
                    sequence.Levels.Add(level);
                    EditorUtility.SetDirty(sequence);
                    Debug.Log($"<color=#14D2EB>🧩 [ASMR Level]</color> 'Piksel Yapboz' LevelSequence listesine eklendi! (Toplam {sequence.Levels.Count} Seviye)");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
