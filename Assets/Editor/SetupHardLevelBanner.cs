using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    /// <summary>
    /// HUD_Canvas üzerinde 'HARD LEVEL' afişini kurar, görselini bağlar ve canlı test eder.
    /// </summary>
    [InitializeOnLoad]
    public static class SetupHardLevelBanner
    {
        static SetupHardLevelBanner()
        {
            // Otomatik çalıştırma kapalı: Sahneyi kirletmemesi için. Menüden elle çalıştırılabilir.
            /*
            EditorApplication.delayCall += () =>
            {
                CasualHudController hud = Object.FindFirstObjectByType<CasualHudController>();
                if (hud != null)
                {
                    hud.EnsureHardLevelBanner();
                }
            };
            */
        }

        // [MenuItem("Tools/PixelGame/🔥 Hard Level Banner Kur & Test Et", priority = 45)]
        public static void SetupAndTest()
        {
            CasualHudController hud = Object.FindFirstObjectByType<CasualHudController>();
            if (hud == null)
            {
                Debug.LogError("[SetupHardLevelBanner] Sahnede CasualHudController bulunamadı!");
                return;
            }

            hud.EnsureHardLevelBanner();

            if (hud.HardLevelBannerObj != null)
            {
                EditorUtility.SetDirty(hud.gameObject);
                EditorUtility.SetDirty(hud.HardLevelBannerObj);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());

                Debug.Log("<color=#00FFAA><b>[PixelGame]</b></color> 🔥 Hard Level Banner başarıyla kuruldu ve bağlandı!");

                hud.ShowHardLevelBanner();
            }
        }
    }
}
