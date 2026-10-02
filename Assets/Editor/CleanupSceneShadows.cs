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
            // İstenildiğinde Tools menüsünden elle çalıştırılabilir
            // EditorApplication.delayCall += RunPurge;
        }

        [MenuItem("Tools/PixelGame/🧹 Pano ve Obje Arkasındaki Gölgeleri Tamamen Temizle", priority = 20)]
        public static void ForcePurgeMenu()
        {
            RunPurge();
            EditorUtility.DisplayDialog("Gölgeler Temizlendi", 
                "Obje arkasındaki tüm pano gölgeleri (BoardGridShadow, FigureContourShadow, CubeShadow) sahneden ve prefab'lardan tamamen kaldırıldı!", "Tamam");
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
                SerializedObject soGen = new SerializedObject(gen);
                var spCubeShadow = soGen.FindProperty("m_EnableCubeShadows");
                var spFigShadow = soGen.FindProperty("m_EnableFigureContourShadow");
                var spBoardShadow = soGen.FindProperty("m_EnableBoardShadow");
                if (spCubeShadow != null) spCubeShadow.boolValue = false;
                if (spFigShadow != null) spFigShadow.boolValue = false;
                if (spBoardShadow != null) spBoardShadow.boolValue = false;
                soGen.ApplyModifiedProperties();

                gen.EnableBoardShadow = false;
                gen.EnableFigureContourShadow = false;
                gen.EnableCubeShadows = false;
                gen.EnsureBoardShadowDisabled();
                gen.EnsureFigureContourShadowDisabled();
                sceneModified = true;
            }

            // 3. Sahnedeki ShipDispatcher ayarlarını (hız ve zıplama) orijinal haline döndür
            ShipDispatcher dispatcher = Object.FindFirstObjectByType<ShipDispatcher>();
            if (dispatcher != null)
            {
                SerializedObject soDisp = new SerializedObject(dispatcher);
                SerializedProperty spSpeed = soDisp.FindProperty("m_RopeSpeed");
                SerializedProperty spDur = soDisp.FindProperty("m_HopDuration");
                SerializedProperty spArc = soDisp.FindProperty("m_HopArcHeight");
                if (spSpeed != null) spSpeed.floatValue = 1.25f;
                if (spDur != null) spDur.floatValue = 0.3f;
                if (spArc != null) spArc.floatValue = 0.6f;
                soDisp.ApplyModifiedProperties();
                sceneModified = true;
            }

            // 4. Sahnedeki küplerde kalan CubeShadow nesnelerini temizle
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
