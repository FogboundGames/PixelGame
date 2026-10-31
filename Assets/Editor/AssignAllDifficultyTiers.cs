using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Level Designer'da Zor olarak nitelendirilen tüm bölümlerin DifficultyTier kademesini
    /// asset dosyalarına kalıcı olarak senkronize eder.
    /// </summary>
    [InitializeOnLoad]
    public static class AssignAllDifficultyTiers
    {
        static AssignAllDifficultyTiers()
        {
            // Otomatik çalıştırma kapalı: Seviye zorluk kademelerini ezmemesi için.
            // EditorApplication.delayCall += SyncAll;
        }

        // [MenuItem("Tools/PixelGame/Sync All Difficulty Tiers", priority = 40)]
        public static void SyncAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:PixelLevelData", new[] { "Assets/Levels" });
            int zorCount = 0, ortaCount = 0, kolayCount = 0, modified = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var level = AssetDatabase.LoadAssetAtPath<PixelLevelData>(path);
                if (level == null || level.DifficultyIsManual) continue;

                var rep = LevelDifficultyReport.Compute(level);
                if (!rep.valid) continue;

                LevelDifficultyTier tier = rep.tier == LevelDifficultyReport.Tier.Zor ? LevelDifficultyTier.Zor :
                                           rep.tier == LevelDifficultyReport.Tier.Orta ? LevelDifficultyTier.Orta : LevelDifficultyTier.Kolay;

                if (level.DifficultyTier != tier)
                {
                    Undo.RecordObject(level, "Sync Difficulty Tier");
                    level.DifficultyTier = tier;
                    EditorUtility.SetDirty(level);
                    modified++;
                }

                if (tier == LevelDifficultyTier.Zor) zorCount++;
                else if (tier == LevelDifficultyTier.Orta) ortaCount++;
                else kolayCount++;
            }

            if (modified > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=#00FFAA><b>[LevelDifficultySync]</b></color> {modified} bölüm güncellendi — 🔴 Zor: {zorCount} · 🟡 Orta: {ortaCount} · 🟢 Kolay: {kolayCount}");
            }
        }
    }
}
