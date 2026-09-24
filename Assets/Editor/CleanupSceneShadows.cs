using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace PixelGame.Editor
{
    /// <summary>
    /// Sahne ve prefab'lardaki tüm istenmeyen pano ve figür arkası sahte gölge nesnelerini
    /// (BoardGridShadow, FigureContourShadow, BoardFrameShadow_Mat vb.) otomatik olarak
    /// temizleyen ve sahneyi kaydeden araç.
    /// </summary>
    [InitializeOnLoad]
    public static class CleanupSceneShadows
    {
        private const string CleanedSessionKey = "PixelGame_ShadowsPermanentlyPurged_v1";

        static CleanupSceneShadows()
        {
            // Otomatik tetikleme kapatıldı: proje her açıldığında sahneyi elle onay
            // almadan değiştirip kaydediyordu. Gerekirse Tools menüsünden elle çalıştırılır.
            // EditorApplication.delayCall += RunPurge;
        }

        // [MenuItem("Tools/PixelGame/🧹 Pano ve Obje Arkasındaki Gölgeleri Tamamen Temizle", priority = 20)]
        public static void ForcePurgeMenu()
        {
            SessionState.SetBool(CleanedSessionKey, false);
            RunPurge();
            EditorUtility.DisplayDialog("Gölgeler Temizlendi", 
                "Obje arkasındaki tüm pano gölgeleri (BoardGridShadow, FigureContourShadow) sahneden ve prefab'lardan tamamen kaldırıldı!", "Harika");
        }

        public static void RunPurge()
        {
            if (Application.isPlaying) return;

            bool sceneModified = false;

            // 1. Sahnedeki tüm BoardGridShadow, FigureContourShadow veya ilgili gölge nesnelerini bul ve sil
            GameObject[] allSceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allSceneObjects)
            {
                if (go == null) continue;

                string name = go.name;
                if (name.Contains("TrackFakeShadow")) continue;

                bool isBoardShadow = name == "BoardGridShadow" || 
                                     name == "FigureContourShadow" || 
                                     name.Contains("BoardGridShadow") || 
                                     name.Contains("FigureContourShadow");

                if (!isBoardShadow)
                {
                    MeshRenderer mr = go.GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial != null)
                    {
                        string matName = mr.sharedMaterial.name;
                        if (matName.Contains("BoardFrameShadow") || matName.Contains("FigureContourShadow"))
                        {
                            isBoardShadow = true;
                        }
                    }
                }

                if (isBoardShadow)
                {
                    Debug.Log($"<color=orange>[CleanupSceneShadows]</color> Siliniyor: {go.name}");
                    Undo.DestroyObjectImmediate(go);
                    sceneModified = true;
                }
            }

            // 2. Sahnedeki PixelArtGenerator ayarlarını gölgesiz yap
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null)
            {
                gen.EnableBoardShadow = false;
                gen.EnableFigureContourShadow = false;
                gen.EnableCubeShadows = false;
                gen.EnsureBoardShadowDisabled();
                gen.EnsureFigureContourShadowDisabled();
                sceneModified = true;
            }

            // 3. Sahnedeki küplerde kalan CubeShadow nesnelerini temizle
            PixelCube[] sceneCubes = Object.FindObjectsByType<PixelCube>(FindObjectsSortMode.None);
            foreach (var cube in sceneCubes)
            {
                if (cube == null) continue;
                Transform cs = cube.transform.Find("CubeShadow");
                if (cs != null)
                {
                    Undo.DestroyObjectImmediate(cs.gameObject);
                    sceneModified = true;
                }
                Transform csb = cube.transform.Find("CubeShadow_Bottom");
                if (csb != null)
                {
                    Undo.DestroyObjectImmediate(csb.gameObject);
                    sceneModified = true;
                }
            }

            // 4. MainCube.prefab içerisindeki CubeShadow nesnesini de temizle
            string prefabPath = "Assets/Prefabs/MainCube.prefab";
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot != null)
            {
                bool prefabModified = false;
                Transform shadowChild = prefabRoot.transform.Find("CubeShadow");
                if (shadowChild != null)
                {
                    Object.DestroyImmediate(shadowChild.gameObject);
                    prefabModified = true;
                }
                Transform shadowBottomChild = prefabRoot.transform.Find("CubeShadow_Bottom");
                if (shadowBottomChild != null)
                {
                    Object.DestroyImmediate(shadowBottomChild.gameObject);
                    prefabModified = true;
                }

                if (prefabModified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    Debug.Log("<color=#00FFAA>[CleanupSceneShadows]</color> MainCube prefab'ındaki sahte gölgeler temizlendi.");
                }
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            if (sceneModified)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                SceneView.RepaintAll();
                Debug.Log("<color=#00FFAA><b>[CleanupSceneShadows]</b></color> Obje arkasındaki gölgeler temizlendi ve sahne kaydedildi!");
            }
        }
    }
}
