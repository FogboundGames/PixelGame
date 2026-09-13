using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class ExecuteBoardShadowUpdate
    {
        private const string SessionKey = "BoardShadowUpdate_Applied_v3";

        static ExecuteBoardShadowUpdate()
        {
            // Kullanıcı gölgeleri kapattığı için otomatik gölge oluşturucu devre dışı
            // EditorApplication.delayCall += RunUpdate;
        }

        [MenuItem("Tools/PixelGame/🖼️ Pano ve Küp Gölgelerini Tamamen Temizle")]
        public static void ForceUpdate()
        {
            SessionState.SetBool(SessionKey, false);
            RunUpdate();
            EditorUtility.DisplayDialog("Gölgeler Temizlendi!", 
                "Pano çerçeve gölgesi ve küp altı gölgeleri tamamen temizlendi ve sahne kaydedildi.", "Tamam");
        }

        private static void RunUpdate()
        {
            if (Application.isPlaying) return;

            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null)
            {
                gen.EnableBoardShadow = false;
                gen.EnableCubeShadows = false;
                gen.EnableFigureContourShadow = false;
                gen.EnsureBoardShadowDisabled();
                gen.EnsureFigureContourShadowDisabled();
                gen.ApplyShadowsToAllExistingCubes();

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Debug.Log("<color=#00FFAA><b>[BoardShadowUpdate]</b></color> Gölgeler tamamen temizlendi ve sahne kaydedildi!");
            }
        }
    }
}
