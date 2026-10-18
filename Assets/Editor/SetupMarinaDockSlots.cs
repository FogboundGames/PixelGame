using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupMarinaDockSlots
    {
        private const string MaterialDir = "Assets/Materials/Marina";
        private const string MeshDir = "Assets/Meshes/Marina";
        private const string TextureDir = "Assets/Textures/Marina";
        private const string ScenePath = "Assets/Scenes/Gemi.unity";
        private const string VersionKey = "MarinaSlots_LifebuoyUI_20260930_1";

        static SetupMarinaDockSlots()
        {
            // Otomatik tetikleme kapatıldı: Unity veya PC yeniden başladığında sahneyi habersiz değiştirmemesi için.
            // Gerekirse PixelGame menüsünden elle çalıştırılır.
            // EditorApplication.delayCall += AutoSetupIfNeeded;
        }

        private static void AutoSetupIfNeeded()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(VersionKey, false)) return;
            SessionState.SetBool(VersionKey, true);
            SetupMarinaSlots();
        }

        [MenuItem("PixelGame/🔲 Setup Rounded Square Dock Slots (Image 2)")]
        public static void SetupMarinaSlots()
        {
            EnsureDirectories();

            Material slotMat = GetOrCreateRoundedSquareMaterial();
            Mesh slotMesh = GetOrCreateRoundedSquareQuadMesh(0.56f, 0.56f);

            // Sahnede slotları bul ve güncelle
            if (!EditorSceneManager.GetActiveScene().path.Equals(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            var slots = Object.FindObjectsByType<ShipSlot>(FindObjectsSortMode.None);
            System.Array.Sort(slots, (a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

            if (slots.Length > 0 && slots[0].transform.parent != null)
            {
                Transform parent = slots[0].transform.parent;
                MarinaSlotLayout layout = parent.GetComponent<MarinaSlotLayout>();
                if (layout == null) layout = parent.gameObject.AddComponent<MarinaSlotLayout>();
            }

            Debug.Log($"[MarinaDock] {slots.Length} adet ShipSlot bulundu. Yeni yuvarlak kare (Image 2) slot görselleri uygulanıyor...");

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                int slotNum = slot.SlotIndex + 1;
                slot.transform.localRotation = Quaternion.identity;
                slot.transform.localScale = Vector3.one;

                // 1. Eski nesneleri eksiksiz temizle (tüm çocukları yok et)
                for (int c = slot.transform.childCount - 1; c >= 0; c--)
                {
                    Transform child = slot.transform.GetChild(c);
                    Undo.DestroyObjectImmediate(child.gameObject);
                }

                // 2. Yeni [Slot_RoundedSquare] görsel nesnesi oluştur
                GameObject slotGo = new GameObject("[Slot_RoundedSquare]");
                Undo.RegisterCreatedObjectUndo(slotGo, "Create Slot Rounded Square");
                slotGo.transform.SetParent(slot.transform, false);

                // Zemin yüzeyinin milimetrik üstünde (Y = 0.02f), merkezde
                slotGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                slotGo.transform.localRotation = Quaternion.identity;
                slotGo.transform.localScale = Vector3.one;

                // 3. MeshFilter ve MeshRenderer ekle
                MeshFilter mf = slotGo.AddComponent<MeshFilter>();
                mf.sharedMesh = slotMesh;

                MeshRenderer mr = slotGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = slotMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                // Collider olmamasına dikkat et (tıklamalar engelsiz çalışır)
                Collider col = slotGo.GetComponent<Collider>();
                if (col != null)
                {
                    Undo.DestroyObjectImmediate(col);
                }

                slot.EnsureIndicatorReference();
                Debug.Log($"[MarinaDock] Slot_{slotNum} için 2. görsel tarzı yuvarlak kare slot başarıyla kuruldu.");
            }

            if (slots.Length > 0 && slots[0].transform.parent != null)
            {
                MarinaSlotLayout layout = slots[0].transform.parent.GetComponent<MarinaSlotLayout>();
                if (layout != null)
                {
                    layout.ApplyLayout();
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[MarinaDock] Sahne kaydedildi! Yeni yuvarlak kare slotlar (Image 2) eksiksiz güncellendi.");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder(MaterialDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
                AssetDatabase.CreateFolder("Assets/Materials", "Marina");
            }
            if (!AssetDatabase.IsValidFolder(MeshDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Meshes")) AssetDatabase.CreateFolder("Assets", "Meshes");
                AssetDatabase.CreateFolder("Assets/Meshes", "Marina");
            }
        }

        public static Material GetOrCreateRoundedSquareMaterial()
        {
            string path = $"{MaterialDir}/Slot_RoundedSquare_Mat.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = "Slot_RoundedSquare_Mat";
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/slot_rounded_square.png");
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTexture("_MainTex", tex);
            }

            if (shader.name.Contains("Universal Render Pipeline"))
            {
                mat.SetFloat("_Surface", 1.0f); // 1 = Transparent
                mat.SetFloat("_Blend", 0.0f);   // 0 = Alpha blend
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetColor("_BaseColor", Color.white);
            }
            else
            {
                mat.SetColor("_Color", Color.white);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Mesh GetOrCreateRoundedSquareQuadMesh(float halfW, float halfL)
        {
            string path = $"{MeshDir}/Slot_RoundedSquare_Quad.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "Slot_RoundedSquare_Quad";
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                mesh.Clear();
            }

            // Slot zemin düzlemine oturan yatay Quad (Normalleri +Y yönünde yukarı bakar)
            mesh.vertices = new Vector3[]
            {
                new Vector3(-halfW, 0f, -halfL), // 0: sol-alt
                new Vector3( halfW, 0f, -halfL), // 1: sağ-alt
                new Vector3(-halfW, 0f,  halfL), // 2: sol-üst
                new Vector3( halfW, 0f,  halfL)  // 3: sağ-üst
            };

            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };

            mesh.triangles = new int[]
            {
                0, 2, 1,
                2, 3, 1
            };

            mesh.normals = new Vector3[]
            {
                Vector3.up,
                Vector3.up,
                Vector3.up,
                Vector3.up
            };

            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
