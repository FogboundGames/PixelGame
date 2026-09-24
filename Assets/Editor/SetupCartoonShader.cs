using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Oyunun görsellerini cartoon (toon) shader'a geçirir.
    ///
    /// Çalışma anında üretilen materyaller (kasa parçaları, uçan parçalar, vagon paleti)
    /// zaten <see cref="CartoonShader"/> üzerinden geliyor. Burada yalnızca asset olarak
    /// duran materyaller — yani küplerin materyali — ayarlanır.
    /// </summary>
    public static class SetupCartoonShader
    {
        private const string k_CubeMaterialPath = "Assets/Materials/PixelCube_Cartoon.mat";
        private const string k_CubePrefabPath = "Assets/Prefabs/MainCube.prefab";

        // [MenuItem("Tools/PixelGame/🎨 Cartoon Shader'a Geçir", priority = 30)]
        public static void Apply()
        {
            Shader shader = Shader.Find(CartoonShader.ShaderName);

            if (shader == null)
            {
                EditorUtility.DisplayDialog("Cartoon Shader Bulunamadı",
                    $"'{CartoonShader.ShaderName}' bulunamadı.\n\n" +
                    "Tools > Toony Colors Pro > Shader Generator 2 ile URP template'inden " +
                    "üretip aynı adı verdiğinden emin ol.", "Tamam");
                return;
            }

            Material cubeMaterial = EnsureCubeMaterial(shader);
            bool assigned = AssignToCubePrefab(cubeMaterial);

            AssetDatabase.SaveAssets();

            string message = $"Küp materyali hazır: {Path.GetFileName(k_CubeMaterialPath)}\n" +
                             (assigned
                                 ? "MainCube prefabına atandı."
                                 : "MainCube prefabı bulunamadı; materyali elle ataman gerekiyor.") +
                             "\n\nVagon, ray, kasa parçaları ve uçan parçalar zaten " +
                             "çalışma anında cartoon shader kullanıyor.\n\n" +
                             "Küplerin sahnede güncellenmesi için bölümü yeniden üret " +
                             "(Level Designer > Bu Leveli Sahnede İnşa Et).";

            EditorUtility.DisplayDialog("Cartoon Shader", message, "Tamam");
            Debug.Log($"<color=#00FFAA><b>[PixelGame]</b></color> Cartoon shader uygulandı: {shader.name}");
        }

        /// <summary>
        /// Küpler için cartoon materyalini döndürür; yoksa oluşturur.
        /// Rengi beyaz kalır: küp renkleri MaterialPropertyBlock ile _BaseColor
        /// üzerinden verildiği için materyalin kendi rengi başlangıç değeridir.
        /// </summary>
        private static Material EnsureCubeMaterial(Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(k_CubeMaterialPath);

            if (material == null)
            {
                material = new Material(shader) { name = "PixelCube_Cartoon" };

                Directory.CreateDirectory(Path.GetDirectoryName(k_CubeMaterialPath));
                AssetDatabase.CreateAsset(material, k_CubeMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            CartoonShader.ApplyColor(material, Color.white);
            EditorUtility.SetDirty(material);

            return material;
        }

        /// <summary>Küp prefabındaki tüm renderer'lara cartoon materyalini atar.</summary>
        private static bool AssignToCubePrefab(Material material)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_CubePrefabPath);
            if (prefab == null) return false;

            GameObject root = PrefabUtility.LoadPrefabContents(k_CubePrefabPath);

            try
            {
                bool changed = false;

                // Gölge quad'larına dokunmuyoruz: onlar kendi saydam materyalini kullanıyor
                MeshRenderer renderer = root.GetComponent<MeshRenderer>();

                if (renderer != null && renderer.sharedMaterial != material)
                {
                    renderer.sharedMaterial = material;
                    changed = true;
                }

                if (changed) PrefabUtility.SaveAsPrefabAsset(root, k_CubePrefabPath);

                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
