using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class FracturedCubeSetup
    {
        private const string PrefabPath = "Assets/Prefabs/MainCube.prefab";

        static FracturedCubeSetup()
        {
            // Otomatik tetikleme kapatıldı: proje her açıldığında sahneyi elle onay
            // almadan değiştirip kaydediyordu. Gerekirse elle çalıştırılır.
            // EditorApplication.delayCall += RunSetupOnLoad;
        }

        private static void RunSetupOnLoad()
        {
            // Sadece asset henüz oluşturulmamışsa otomatik oluştur
            if (Resources.Load<FracturedCubeData>(FracturedCubeData.ResourcePath) == null)
            {
                ExecuteSetup(silent: true);
            }
        }

        [MenuItem("Tools/PixelGame/🧊 12 Parçalı Kırılma Verisini Kur & Güncelle", priority = 35)]
        public static void SetupManual()
        {
            ExecuteSetup(silent: false);
        }

        public static void ExecuteSetup(bool silent)
        {
            // 1. FracturedCubeData ScriptableObject'ini FBX'ten üret/güncelle
            FracturedCubeData data = FracturedCubeData.CreateOrUpdateAsset();

            // 2. MainCube prefabındaki mesh'in intact mesh (Cube.001) olduğunu garantiye al
            bool prefabUpdated = EnsureMainCubeIntactMesh(data.IntactMesh);

            AssetDatabase.SaveAssets();

            if (!silent)
            {
                EditorUtility.DisplayDialog("12 Parçalı Kırılma Sistemi Hazır",
                    $"FBX'ten {data.ShardCount} parça başarıyla yüklendi!\n\n" +
                    $"- Intact Mesh: {(data.IntactMesh != null ? data.IntactMesh.name : "Cube.001")}\n" +
                    $"- MainCube Prefab: {(prefabUpdated ? "Sağlam küp mesh'i ile güncellendi" : "Zaten güncel")}\n" +
                    "- Parçalanma yönleri ve merkez ofsetleri hesaplandı.", "Harika!");
            }
        }

        private static bool EnsureMainCubeIntactMesh(Mesh intactMesh)
        {
            if (intactMesh == null) return false;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                MeshFilter mf = root.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != intactMesh)
                {
                    mf.sharedMesh = intactMesh;
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    Debug.Log($"<color=#00FFAA><b>[MainCube]</b></color> MeshFilter sharedMesh '{intactMesh.name}' olarak güncellendi.");
                    return true;
                }
                return false;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
