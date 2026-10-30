using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace PixelGame.Editor
{
    public static class MobilePerformanceEditor
    {
        // [MenuItem("PixelGame/📱 Setup Mobile Performance Manager (45-60 FPS)")]
        public static void SetupManager()
        {
            var existing = Object.FindFirstObjectByType<MobilePerformanceManager>();
            if (existing == null)
            {
                var go = new GameObject("[MobilePerformanceManager]");
                existing = go.AddComponent<MobilePerformanceManager>();
                EditorSceneManager.MarkSceneDirty(go.scene);
                EditorSceneManager.SaveScene(go.scene);
                Debug.Log("<color=#00FFAA><b>[MobilePerformance]</b></color> [MobilePerformanceManager] sahneye eklendi ve kaydedildi.");
            }
            else
            {
                Debug.Log("<color=#00FFAA><b>[MobilePerformance]</b></color> [MobilePerformanceManager] sahnede zaten mevcut.");
            }
            Selection.activeGameObject = existing.gameObject;
        }

        // [MenuItem("PixelGame/📊 Toggle Mobile FPS Counter")]
        public static void ToggleFpsCounter()
        {
            var manager = Object.FindFirstObjectByType<MobilePerformanceManager>();
            if (manager == null)
            {
                SetupManager();
                manager = Object.FindFirstObjectByType<MobilePerformanceManager>();
            }

            if (manager != null)
            {
                Undo.RecordObject(manager, "Toggle FPS Counter");
                manager.ShowFpsCounter = !manager.ShowFpsCounter;
                EditorUtility.SetDirty(manager);
                Debug.Log($"<color=#00FFAA><b>[MobilePerformance]</b></color> FPS Sayacı: {(manager.ShowFpsCounter ? "AÇIK" : "KAPALI")}");
            }
        }
    }
}
