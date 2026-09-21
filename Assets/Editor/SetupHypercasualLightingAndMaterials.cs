using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupHypercasualLightingAndMaterials
    {
        static SetupHypercasualLightingAndMaterials()
        {
            EditorApplication.delayCall += ApplyHypercasualOverhaul;
        }

        [MenuItem("Tools/PixelGame/🌟 Hypercasual Parlak Işık ve Karakter Renklerini Uygula")]
        public static void ApplyHypercasualOverhaul()
        {
            Shader cartoonShader = CartoonShader.Get();
            if (cartoonShader == null) return;

            // 1. Mat_Blue.mat (Gövde - Parlak Altın Sarısı / Dinamik Zırh)
            Material matBlue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Materials/Mat_Blue.mat");
            if (matBlue != null)
            {
                matBlue.shader = cartoonShader;
                CartoonShader.ApplyColor(matBlue, new Color32(255, 210, 20, 255));
                EditorUtility.SetDirty(matBlue);
            }

            // 2. Mat_Dark.mat (Gözler, Gözbebekleri, Ağız, Eklemler)
            Material matDark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Materials/Mat_Dark.mat");
            if (matDark != null)
            {
                matDark.shader = cartoonShader;
                matDark.SetColor("_BaseColor", new Color32(22, 24, 32, 255));
                matDark.SetColor("_HColor", Color.white);
                matDark.SetColor("_SColor", new Color(0.14f, 0.14f, 0.18f, 1f));
                matDark.SetFloat("_RampThreshold", 0.45f);
                matDark.SetFloat("_RampSmoothing", 0.2f);
                if (matDark.HasProperty("_Smoothness")) matDark.SetFloat("_Smoothness", 0.90f);
                if (matDark.HasProperty("_SpecularRoughnessPBR")) matDark.SetFloat("_SpecularRoughnessPBR", 0.10f);
                if (matDark.HasProperty("_SpecularColor")) matDark.SetColor("_SpecularColor", Color.white);
                EditorUtility.SetDirty(matDark);
            }

            // 3. Mat_White.mat (Göz Parlamaları, Diş, Beyaz Vurgular)
            Material matWhite = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Materials/Mat_White.mat");
            if (matWhite != null)
            {
                matWhite.shader = cartoonShader;
                matWhite.SetColor("_BaseColor", Color.white);
                matWhite.SetColor("_HColor", Color.white);
                matWhite.SetColor("_SColor", new Color(0.90f, 0.92f, 0.98f, 1f));
                matWhite.SetFloat("_RampThreshold", 0.3f);
                matWhite.SetFloat("_RampSmoothing", 0.1f);
                if (matWhite.HasProperty("_Smoothness")) matWhite.SetFloat("_Smoothness", 0.95f);
                if (matWhite.HasProperty("_SpecularRoughnessPBR")) matWhite.SetFloat("_SpecularRoughnessPBR", 0.05f);
                if (matWhite.HasProperty("_SpecularColor")) matWhite.SetColor("_SpecularColor", Color.white);
                EditorUtility.SetDirty(matWhite);
            }

            // 4. Prefab'ları güncelle
            string[] prefabs = new string[]
            {
                "Assets/Prefabs/BlueBotWithNeckWagon.prefab",
                "Assets/Prefabs/BlueBotWagon.prefab"
            };

            foreach (string pPath in prefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(pPath);
                if (root == null) continue;
                try
                {
                    TruckPaint tp = root.GetComponent<TruckPaint>();
                    if (tp != null)
                    {
                        tp.SetBodyColor(new Color32(255, 210, 20, 255));
                        tp.Apply();
                    }

                    WagonCapacityBadge badge = root.GetComponent<WagonCapacityBadge>();
                    if (badge != null)
                    {
                        badge.ApplyStyle();
                    }

                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r == null || r.name.StartsWith("CapacityBadge") || r.name.StartsWith("Badge")) continue;

                        Mesh mesh = null;
                        if (r is MeshRenderer mr)
                        {
                            MeshFilter mf = mr.GetComponent<MeshFilter>();
                            if (mf != null) mesh = mf.sharedMesh;
                        }
                        else if (r is SkinnedMeshRenderer smr)
                        {
                            mesh = smr.sharedMesh;
                        }

                        int subCount = (mesh != null) ? mesh.subMeshCount : r.sharedMaterials.Length;
                        if (subCount >= 3)
                        {
                            r.sharedMaterials = new Material[] { matBlue, matDark, matWhite };
                        }
                        else if (subCount == 2)
                        {
                            r.sharedMaterials = new Material[] { matBlue, matDark };
                        }
                        else
                        {
                            r.sharedMaterial = matBlue;
                        }
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, pPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            // 5. Sahne Işıklandırmasını Parlak Hypercasual Standartlarına Ayarla
            Light dirLight = Object.FindFirstObjectByType<Light>();
            if (dirLight != null && dirLight.type == LightType.Directional)
            {
                dirLight.color = new Color(1f, 0.98f, 0.94f, 1f); // Sıcak gün ışığı
                dirLight.intensity = 1.35f;                        // Parlak ve net
                dirLight.transform.rotation = Quaternion.Euler(42f, 32f, 0f); // Tatlı sol-üst 3B köşe parıltısı
                EditorUtility.SetDirty(dirLight);
            }

            RenderSettings.ambientLight = new Color(0.38f, 0.40f, 0.48f, 1f);

            // 6. Sahnedeki Vagonları ve Dispatcher'ı Güncelle
            TruckDispatcher dispatcher = Object.FindFirstObjectByType<TruckDispatcher>();
            if (dispatcher != null)
            {
                dispatcher.TrackWagonScale = 0.52f;
                EditorUtility.SetDirty(dispatcher);
            }

            foreach (TruckPaint paint in Object.FindObjectsByType<TruckPaint>(FindObjectsSortMode.None))
            {
                if (paint != null)
                {
                    paint.SetBodyColor(new Color32(255, 210, 20, 255));
                    paint.Apply();
                    EditorUtility.SetDirty(paint);
                }
            }

            foreach (WagonCapacityBadge badge in Object.FindObjectsByType<WagonCapacityBadge>(FindObjectsSortMode.None))
            {
                if (badge != null)
                {
                    badge.ApplyStyle();
                    badge.UpdatePlacement();
                    EditorUtility.SetDirty(badge);
                }
            }

            AssetDatabase.SaveAssets();
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            Debug.Log("<color=#00FFAA><b>[HypercasualSetup]</b></color> Sahnede Toony Colors Pro 2 parlak plastik cila, göz detayları ve hypercasual ışıklandırma %100 başarıyla uygulandı!");
        }
    }
}
