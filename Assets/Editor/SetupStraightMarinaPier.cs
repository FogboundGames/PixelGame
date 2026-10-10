using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class SetupStraightMarinaPier
    {
        private const string ScenePath = "Assets/Scenes/Gemi.unity";

        [MenuItem("PixelGame/⚓ Setup Straight Marina Pier (Düz İskele Slotları)")]
        public static void ApplyStraightPierSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            // 1. MarinaSlotLayout'u ayarla
            MarinaSlotLayout layout = Object.FindFirstObjectByType<MarinaSlotLayout>();
            if (layout != null)
            {
                Undo.RecordObject(layout, "Setup Straight Pier");
                layout.EnableCurvedPier = false;
                layout.UsePerCountSettings = true;
                layout.ResetToCalibratedDefaults(0);
                layout.ApplyLayout();
                EditorUtility.SetDirty(layout);
                EditorUtility.SetDirty(layout.gameObject);
                Debug.Log("<color=#00FFAA><b>[StraightPier]</b></color> MarinaSlotLayout düz iskeleye ayarlandı!");
            }

            // 2. [Marina_Curved_Pier] nesnesini gizle
            GameObject curvedPier = GameObject.Find("[Marina_Curved_Pier]");
            if (curvedPier != null && curvedPier.activeSelf)
            {
                Undo.RecordObject(curvedPier, "Hide Curved Pier");
                curvedPier.SetActive(false);
                EditorUtility.SetDirty(curvedPier);
                Debug.Log("<color=#00FFAA><b>[StraightPier]</b></color> [Marina_Curved_Pier] gizlendi!");
            }

            // 3. Su kontrolcüsü alanını güncelle
            HypercasualWaterController water = Object.FindFirstObjectByType<HypercasualWaterController>();
            if (water != null)
            {
                Undo.RecordObject(water, "Update Water Limits");
                Material mat = water.EnsureMaterial();
                if (mat != null)
                {
                    mat.SetFloat("_WaterMinV", 0.00f);
                    mat.SetFloat("_WaterMaxV", 0.31f);
                    EditorUtility.SetDirty(mat);
                }
                EditorUtility.SetDirty(water);
            }

            // 4. Sahneyi kaydet
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            // 5. Ekran görüntüsü al
            CaptureGameViewScreenshot.CaptureGemiScene();
        }
    }
}
