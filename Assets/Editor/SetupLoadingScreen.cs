using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class SetupLoadingScreen
    {
        [MenuItem("Tools/PixelGame/🌊 Land Flow Yükleme Ekranını Sahnede Kur / Güncelle")]
        public static void SetupInScene()
        {
            var existing = Object.FindFirstObjectByType<LandFlowLoadingScreen>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorUtility.DisplayDialog("Yükleme Ekranı Mevcut", "Sahnede zaten LandFlow_LoadingScreen mevcut ve seçildi.", "Tamam");
                return;
            }

            GameObject go = new GameObject("LandFlow_LoadingScreen");
            var screen = go.AddComponent<LandFlowLoadingScreen>();
            Undo.RegisterCreatedObjectUndo(go, "Create LandFlow Loading Screen");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = go;

            EditorUtility.DisplayDialog("Yükleme Ekranı Kuruldu", 
                "Land Flow Yükleme Ekranı başarıyla sahneye eklendi!\n\n" +
                "- Solid mavi zemin\n" +
                "- Land Flow logosu\n" +
                "- Ortalanmış 'LOADING...' yazısı ve dolum çubuğu\n" +
                "- Oyun açılışında, level geçişlerinde ve level fail sonrası otomatik çalışır.", "Harika!");
        }
    }
}
