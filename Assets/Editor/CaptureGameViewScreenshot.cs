using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class CaptureGameViewScreenshot
    {
        private const string OutputPath = "scratch/gameplay_view_9_16.png";

        static CaptureGameViewScreenshot()
        {
            // Otomatik tetikleme kapatıldı: proje her açıldığında ekran görüntüsü
            // almaya çalışıp beklenmedik yan etkilere sebep oluyordu. Gerekirse elle çalıştırılır.
            // EditorApplication.delayCall += Capture;
        }

        private static bool s_IsCapturing = false;

        public static void CaptureBatch()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SetupVisualOverhaul.ApplyOverhaul();
            Capture();
        }

        [MenuItem("Tools/PixelGame/📸 9:16 Ekran Görüntüsü Al (Capture Screenshot)")]
        public static void Capture()
        {
            if (s_IsCapturing) return;
            s_IsCapturing = true;

            try
            {
                Camera cam = Camera.main;
                if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
                if (cam == null)
                {
                    Debug.LogError("[CaptureGameViewScreenshot] Sahne kamerası bulunamadı!");
                    return;
                }

                int width = 1080;
                int height = 1920;

                RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                RenderTexture prevRT = cam.targetTexture;
                RenderTexture prevActive = RenderTexture.active;

                try
                {
                    EnsureRoundedToyCubesApplied();

                    TrackFakeShadow shadow = TrackFakeShadow.EnsureShadow(null);
                    if (shadow != null)
                    {
                        shadow.RebuildMesh();
                    }

                    cam.targetTexture = rt;
                    cam.Render();

                    RenderTexture.active = rt;
                    Texture2D screenTex = new Texture2D(width, height, TextureFormat.RGB24, false);
                    screenTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    screenTex.Apply();

                    byte[] bytes = screenTex.EncodeToPNG();
                    string dir = Path.GetDirectoryName(OutputPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllBytes(OutputPath, bytes);
                    Object.DestroyImmediate(screenTex);

                    Debug.Log($"<color=#00FFAA><b>[Screenshot]</b></color> 9:16 Ekran görüntüsü başarıyla kaydedildi: {OutputPath}");
                }
                finally
                {
                    cam.targetTexture = prevRT;
                    RenderTexture.active = prevActive;
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
            finally
            {
                s_IsCapturing = false;
            }
        }

        private static void EnsureRoundedToyCubesApplied()
        {
            try
            {
                const string modelDir = "Assets/Models";
                const string assetPath = "Assets/Models/RoundedCube.asset";
                const string prefabPath = "Assets/Prefabs/MainCube.prefab";
                const string matPath = "Assets/Materials/PixelCube_Cartoon.mat";

                // 1. Mesh'i oluştur (deterministic — her seferinde yeniden üret,
                //    winding düzeltmesi mevcut bozuk asset'i de onarır)
                if (!Directory.Exists(modelDir)) Directory.CreateDirectory(modelDir);
                Mesh roundedMesh = CreateRoundedCubeMesh(1.0f, 0.125f, 3);
                roundedMesh.name = "RoundedCube";
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.CreateAsset(roundedMesh, assetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("<color=#00FFAA><b>[SetupRoundedCubes]</b></color> RoundedCube.asset yeniden oluşturuldu (winding düzeltildi)!");

                // 2. MainCube.prefab'a yeni mesh'i bağla
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    MeshFilter mf = prefab.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != roundedMesh)
                    {
                        mf.sharedMesh = roundedMesh;
                        EditorUtility.SetDirty(prefab);
                        PrefabUtility.SavePrefabAsset(prefab);
                    }
                }

                // 3. Materyali parlak oyuncak plastik speküler ile yapılandır
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat != null)
                {
                    bool matDirty = false;
                    if (mat.HasProperty("_StylizedPlasticOn") && mat.GetFloat("_StylizedPlasticOn") < 0.5f) { mat.SetFloat("_StylizedPlasticOn", 1.0f); matDirty = true; }
                    if (mat.HasProperty("_PlasticAngleX")) { mat.SetFloat("_PlasticAngleX", -0.45f); matDirty = true; }
                    if (mat.HasProperty("_PlasticHighlightIntensity")) { mat.SetFloat("_PlasticHighlightIntensity", 2.85f); matDirty = true; }
                    if (mat.HasProperty("_PlasticHighlightSize")) { mat.SetFloat("_PlasticHighlightSize", 0.26f); matDirty = true; }
                    if (mat.HasProperty("_PlasticTopLight")) { mat.SetFloat("_PlasticTopLight", 0.25f); matDirty = true; }
                    if (mat.HasProperty("_PlasticBevelAO")) { mat.SetFloat("_PlasticBevelAO", 0.45f); matDirty = true; }
                    if (mat.HasProperty("_SpecularColor")) { mat.SetColor("_SpecularColor", Color.white); matDirty = true; }
                    if (mat.HasProperty("_SpecularRoughnessPBR")) { mat.SetFloat("_SpecularRoughnessPBR", 0.18f); matDirty = true; }
                    if (mat.HasProperty("_Smoothness")) { mat.SetFloat("_Smoothness", 0.92f); matDirty = true; }

                    if (matDirty)
                    {
                        EditorUtility.SetDirty(mat);
                        AssetDatabase.SaveAssets();
                    }
                }

                // 4. PixelArtGenerator jeneratörünü güncelle (boşluksuz ızgara & 3D derinlik)
                PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
                if (gen != null)
                {
                    SerializedObject sGen = new SerializedObject(gen);
                    SerializedProperty spSpacing = sGen.FindProperty("m_CubeSpacing");
                    SerializedProperty spDepth = sGen.FindProperty("m_CubeDepth");

                    bool genDirty = false;
                    if (spSpacing != null && spSpacing.floatValue > 0.005f) { spSpacing.floatValue = 0.002f; genDirty = true; }
                    if (spDepth != null && spDepth.floatValue < 0.65f) { spDepth.floatValue = 0.78f; genDirty = true; }

                    if (genDirty)
                    {
                        sGen.ApplyModifiedProperties();
                        EditorUtility.SetDirty(gen);
                        gen.UpdateExistingCubesTransforms();
                    }
                }

                // 5. Sahnedeki mevcut küpleri yeni mesh ve doğru 3D derinlikle güncelle
                GameObject container = GameObject.Find("PixelArtContainer");
                if (container != null)
                {
                    PixelCube[] cubes = container.GetComponentsInChildren<PixelCube>(true);
                    bool sceneDirty = false;
                    foreach (PixelCube cube in cubes)
                    {
                        if (cube == null) continue;
                        MeshFilter mf = cube.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != roundedMesh)
                        {
                            mf.sharedMesh = roundedMesh;
                            sceneDirty = true;
                        }

                        Vector3 scale = cube.transform.localScale;
                        float targetZ = scale.x * 0.78f;
                        if (Mathf.Abs(scale.z - targetZ) > 0.005f)
                        {
                            scale.z = targetZ;
                            cube.transform.localScale = scale;
                            sceneDirty = true;
                        }
                    }

                    if (sceneDirty)
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EnsureRoundedToyCubesApplied] Hata: {ex.Message}");
            }
        }

        public static Mesh CreateRoundedCubeMesh(float size, float radius, int seg)
        {
            float half = size * 0.5f;
            float r = radius;
            float core = half - r;

            float[] angles = new float[seg + 1];
            for (int i = 0; i <= seg; i++) angles[i] = i * (Mathf.PI * 0.5f) / seg;

            System.Collections.Generic.List<float> coords1D = new System.Collections.Generic.List<float>();
            for (int i = 0; i <= seg; i++) coords1D.Add(-core - r * Mathf.Cos(angles[i]));
            for (int i = 0; i <= seg; i++) coords1D.Add(core + r * Mathf.Sin(angles[i]));

            int N = coords1D.Count;

            (int axU, int axV, int axW, float wVal, int wSign)[] facesConfig = new[]
            {
                (0, 1, 2,  half,  1), // +Z
                (0, 1, 2, -half, -1), // -Z
                (2, 1, 0,  half,  1), // +X
                (2, 1, 0, -half, -1), // -X
                (0, 2, 1,  half,  1), // +Y
                (0, 2, 1, -half, -1), // -Y
            };

            System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Vector3> normals = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Vector2> uvs = new System.Collections.Generic.List<Vector2>();
            System.Collections.Generic.Dictionary<Vector3Int, int> vertMap = new System.Collections.Generic.Dictionary<Vector3Int, int>();

            int GetVertexIndex(float x, float y, float z)
            {
                float cx = Mathf.Clamp(x, -core, core);
                float cy = Mathf.Clamp(y, -core, core);
                float cz = Mathf.Clamp(z, -core, core);

                float dx = x - cx;
                float dy = y - cy;
                float dz = z - cz;
                float dist = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);

                Vector3 pos;
                Vector3 norm;
                if (dist > 1e-5f)
                {
                    pos = new Vector3(cx + dx * (r / dist), cy + dy * (r / dist), cz + dz * (r / dist));
                    norm = new Vector3(dx / dist, dy / dist, dz / dist);
                }
                else
                {
                    pos = new Vector3(cx, cy, cz);
                    norm = new Vector3(0f, 0f, 1f);
                }

                Vector3Int key = new Vector3Int(
                    Mathf.RoundToInt(pos.x * 10000f),
                    Mathf.RoundToInt(pos.y * 10000f),
                    Mathf.RoundToInt(pos.z * 10000f)
                );

                if (!vertMap.TryGetValue(key, out int idx))
                {
                    idx = vertices.Count;
                    vertMap[key] = idx;
                    vertices.Add(pos);
                    normals.Add(norm);
                    uvs.Add(new Vector2((pos.x + half) / size, (pos.y + half) / size));
                }
                return idx;
            }

            System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();
            foreach (var f in facesConfig)
            {
                int[,] grid = new int[N, N];
                for (int j = 0; j < N; j++)
                {
                    for (int i = 0; i < N; i++)
                    {
                        float[] p = new float[3];
                        p[f.axU] = coords1D[i];
                        p[f.axV] = coords1D[j];
                        p[f.axW] = f.wVal;
                        grid[j, i] = GetVertexIndex(p[0], p[1], p[2]);
                    }
                }

                for (int j = 0; j < N - 1; j++)
                {
                    for (int i = 0; i < N - 1; i++)
                    {
                        int v00 = grid[j, i];
                        int v10 = grid[j, i + 1];
                        int v11 = grid[j + 1, i + 1];
                        int v01 = grid[j + 1, i];

                        // Winding: normalin dışa bakması için hem wSign hem de
                        // (axU, axV, axW) eksen permütasyonunun handedness'i hesaba katılır.
                        // Even permütasyon (cyclic: 0→1, 1→2, 2→0) normali sabit tutar;
                        // tek permütasyonda (cross(axU, axV) = -axW) sarma çevrilmelidir.
                        bool evenPerm = ((f.axU + 1) % 3 == f.axV) && ((f.axV + 1) % 3 == f.axW);
                        if (evenPerm == (f.wSign > 0))
                        {
                            triangles.Add(v00); triangles.Add(v10); triangles.Add(v11);
                            triangles.Add(v00); triangles.Add(v11); triangles.Add(v01);
                        }
                        else
                        {
                            triangles.Add(v00); triangles.Add(v11); triangles.Add(v10);
                            triangles.Add(v00); triangles.Add(v01); triangles.Add(v11);
                        }
                    }
                }
            }

            Mesh m = new Mesh();
            m.name = "RoundedCubeMesh";
            m.vertices = vertices.ToArray();
            m.normals = normals.ToArray();
            m.uv = uvs.ToArray();
            m.triangles = triangles.ToArray();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }
    }
}
// trigger 09/20/2026 00:46:30
