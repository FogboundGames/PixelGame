using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PixelGame;

namespace PixelGame.Editor
{
    /// <summary>
    /// Gemi sahnesindeki piksellerin (küplerin) mesafesini tamamen homojen,
    /// arkaplan kumunun kesinlikle aradan görünmeyeceği şekilde tam oturan (snug & seamless)
    /// referans görselindeki (Image 3) gibi pahlı ve temiz bir ızgaraya oturtur.
    /// </summary>
    [InitializeOnLoad]
    public static class SetupGemiCubeGrid
    {
        private const string ScenePath = "Assets/Scenes/Gemi.unity";
        private const string LevelPath = "Assets/Levels/Level_02_Heart.asset";
        private const string AutoRunKey = "GemiCubeGrid_Calibrated_v2";

        static SetupGemiCubeGrid()
        {
            EditorApplication.delayCall += AutoRunIfNeeded;
        }

        private static void AutoRunIfNeeded()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(AutoRunKey, false)) return;
            SessionState.SetBool(AutoRunKey, true);
            CalibrateCubeGrid();
        }

        [MenuItem("PixelGame/🎯 Calibrate Gemi Cubes (Snug & Seamless)")]
        public static void CalibrateCubeGrid()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            bool sceneWasOpen = activeScene.path.Equals(ScenePath);
            if (!sceneWasOpen)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            // 1. PixelArtGenerator ayarları
            var gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null)
            {
                Undo.RecordObject(gen, "Calibrate Cube Spacing");
                gen.CubeSpacing = 0f;
                gen.CubeSpacingX = 0f;
                EditorUtility.SetDirty(gen);
            }

            // 2. Seviye verisi ayarları (Level_02_Heart)
            var levelData = AssetDatabase.LoadAssetAtPath<PixelLevelData>(LevelPath);
            if (levelData != null)
            {
                Undo.RecordObject(levelData, "Calibrate Level Cube Spacing");
                levelData.CubeSpacing = 0f;
                levelData.CubeSpacingX = 0f;
                EditorUtility.SetDirty(levelData);
                AssetDatabase.SaveAssets();
            }

            // 3. Sahnede bulunan tüm PixelCube nesnelerini tam homojen ızgaraya yerleştir
            var cubes = Object.FindObjectsByType<PixelCube>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (cubes != null && cubes.Length > 0)
            {
                const float step = 0.25988f; // Tam matematiksel ızgara adımı
                const float cubeScale = 0.2605f; // Arka planın hiçbir açıdan sızmasını engelleyen snug temas
                const float centerX = 0.046555f;
                const float centerY = -0.23938f;

                foreach (var cube in cubes)
                {
                    Undo.RecordObject(cube.transform, "Align PixelCube Snug");
                    int gx = cube.GridX;
                    int gy = cube.GridY;

                    float targetX = centerX + (gx - 11.5f) * step;
                    float targetY = centerY + (gy - 11.5f) * step;

                    cube.transform.localPosition = new Vector3(targetX, targetY, cube.transform.localPosition.z);
                    cube.transform.localScale = new Vector3(cubeScale, cubeScale, cubeScale);
                    EditorUtility.SetDirty(cube.transform);
                }

                Debug.Log($"<color=#00FFAA><b>[SetupGemiCubeGrid]</b></color> {cubes.Length} adet küp homojen ve aralıksız (snug) ızgaraya oturtuldu.");
            }

            EditorSceneManager.SaveOpenScenes();
        }
    }
}
