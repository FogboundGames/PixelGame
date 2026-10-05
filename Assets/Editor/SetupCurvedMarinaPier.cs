using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupCurvedMarinaPier
    {
        private const string TextureDir = "Assets/Textures/Marina";
        private const string MaterialDir = "Assets/Materials/Marina";
        private const string MeshDir = "Assets/Meshes/Marina";
        private const string ScenePath = "Assets/Scenes/Gemi.unity";

        static SetupCurvedMarinaPier()
        {
            EditorApplication.delayCall += SetupCurvedPier;
        }

        [MenuItem("PixelGame/⚓ Setup Curved Marina Pier (4 & 5 Slots)")]
        public static void SetupCurvedPier()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            EnsureDirectories();

            // 1. Mesh oluştur
            GetOrCreatePierQuadMesh();

            // 2. Materyalleri oluştur / güncelle
            Material mat4 = GetOrCreatePierMaterial("Pier_Curved_4Slots_Mat", $"{TextureDir}/pier_curved_4slots.png");
            Material mat5 = GetOrCreatePierMaterial("Pier_Curved_5Slots_Mat", $"{TextureDir}/pier_curved_5slots.png");
            Material mat3 = GetOrCreatePierMaterial("Pier_Curved_3Slots_Mat", $"{TextureDir}/pier_curved_3slots.png");

            // 3. Sahneyi aç
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            // 4. MarinaSlotLayout'u bul
            MarinaSlotLayout layout = Object.FindFirstObjectByType<MarinaSlotLayout>();
            if (layout == null)
            {
                var anySlot = Object.FindFirstObjectByType<ShipSlot>();
                if (anySlot != null && anySlot.transform.parent != null)
                {
                    layout = anySlot.transform.parent.gameObject.AddComponent<MarinaSlotLayout>();
                }
            }

            if (layout != null)
            {
                Undo.RecordObject(layout, "Setup Curved Pier");

                layout.SetCurvedPierMaterials(mat4, mat5, mat3);

                // Varsayılan kavisli iskele sahil oranları
                layout.EnableCurvedPier = true;
                layout.PierWidth4Slots = 8.1f;
                layout.PierWidth5Slots = 8.55f;
                layout.PierWidth3Slots = 7.2f;
                layout.PierOffsetY = 2.06f;
                layout.PierOffsetZ = 0.04f;
                layout.PierScaleMultiplier = 1.07f;
                layout.PierRotationX = -40f;
                layout.BaySlotOffsetY = 0f;

                // Su ve slot parametreleri
                layout.SlotWidth = 1.05f;
                layout.SlotLength = 1.55f;
                layout.WaterTiltX = -67.892f;
                layout.SlotAngle = 0f;
                layout.ArcCurveY = 0.042f;
                layout.ArcAsymmetry = 0f;
                layout.ArcAngleFan = 0f;

                // Sahne kumsalına tam oturan kök Y konumu
                layout.OffsetY = -2.20f;
                layout.OffsetZ = 0.05f;

                layout.ApplyLayout();
                EditorUtility.SetDirty(layout);
                EditorUtility.SetDirty(layout.gameObject);

                Debug.Log("<color=#00FFAA><b>[CurvedPier]</b></color> Kavisli iskele (4 ve 5 slot uyumlu) başarıyla kuruldu!");
            }

            // 5. Üst sahil görünmez zeminini kur (pikselart küplerinin yürüme animasyonu gölgesi için)
            EnsureUpperBeachGround();

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();

                // Ekran görüntüsü al
                CaptureGameViewScreenshot.CaptureGemiScene();
            }
        }

        public static void EnsureUpperBeachGround()
        {
            UpperBeachGround ground = Object.FindFirstObjectByType<UpperBeachGround>();
            if (ground == null)
            {
                GameObject root = GameObject.Find("[GAMEPLAY_MODELS]");
                GameObject go = new GameObject("[Upper_Beach_Ground]");
                if (root != null)
                {
                    go.transform.SetParent(root.transform, false);
                }
                ground = go.AddComponent<UpperBeachGround>();
                Undo.RegisterCreatedObjectUndo(go, "Create Upper Beach Ground");
            }

            if (ground != null)
            {
                ground.Center = new Vector3(0f, 3.40f, 0.35f);
                ground.Size = new Vector2(11.5f, 5.4f);
                ground.TiltX = 0f;
                ground.EnsureGround();
                EditorUtility.SetDirty(ground);
                if (ground.gameObject != null) EditorUtility.SetDirty(ground.gameObject);
                Debug.Log("<color=#00FFAA><b>[UpperBeachGround]</b></color> Görünmez üst sahil zemini başarıyla kuruldu!");
            }
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MaterialDir)) AssetDatabase.CreateFolder("Assets/Materials", "Marina");

            if (!AssetDatabase.IsValidFolder("Assets/Meshes")) AssetDatabase.CreateFolder("Assets", "Meshes");
            if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/Meshes", "Marina");
        }

        private static Mesh GetOrCreatePierQuadMesh()
        {
            string path = $"{MeshDir}/Pier_Curved_Quad.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = false;
            if (mesh == null)
            {
                mesh = new Mesh { name = "Pier_Curved_Quad" };
                isNew = true;
            }
            else
            {
                mesh.Clear();
            }

            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            mesh.normals = new Vector3[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.RecalculateBounds();

            if (isNew)
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }
            return mesh;
        }

        private static Material GetOrCreatePierMaterial(string matName, string texPath)
        {
            string path = $"{MaterialDir}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = matName;
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTexture("_MainTex", tex);
            }

            if (shader.name.Contains("Universal Render Pipeline"))
            {
                mat.SetFloat("_Surface", 1.0f); // Transparent
                mat.SetFloat("_Blend", 0.0f);   // Alpha
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
                mat.SetColor("_BaseColor", Color.white);
            }
            else
            {
                mat.SetColor("_Color", Color.white);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
