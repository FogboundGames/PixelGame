using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Oyuncak kamyon (Assets/Models/ToyTruck) için import ayarlarını ilk importta otomatik yapar:
    /// palet dokusu Point filtre / mipmap yok / sıkıştırma yok, FBX Generic + eksen dönüşümü bake,
    /// materyale palet dokusu bağlanır. Ayrıca "Tools > Toy Truck > Create Prefab" menüsünü ekler.
    /// </summary>
    public class ToyTruckImportSettings : AssetPostprocessor
    {
        private const string k_Folder = "Assets/Models/ToyTruck/";
        private const string k_ModelPath = k_Folder + "ToyTruck.fbx";
        private const string k_PalettePath = k_Folder + "Truck_Palette.png";
        private const string k_PrefabPath = "Assets/Prefabs/ToyTruck.prefab";

        // URP'nin kendi FBX materyal işlemcisinden sonra çalışsın ki palet bağlantısı ezilmesin.
        public override int GetPostprocessOrder() => 100;

        private void OnPreprocessTexture()
        {
            if (assetPath != k_PalettePath || !assetImporter.importSettingsMissing)
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        private void OnPreprocessModel()
        {
            if (assetPath != k_ModelPath || !assetImporter.importSettingsMissing)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        private void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] materialAnimation)
        {
            if (assetPath != k_ModelPath)
            {
                return;
            }

            context.DependsOnArtifact(k_PalettePath);
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(k_PalettePath);
            if (palette == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", palette);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", palette);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
        }

        [MenuItem("Tools/Toy Truck/Create Prefab")]
        private static void CreatePrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_ModelPath);
            if (model == null)
            {
                EditorUtility.DisplayDialog("Toy Truck", k_ModelPath + " bulunamadı.", "Tamam");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                instance.name = "ToyTruck";
                if (instance.GetComponent<TruckPaint>() == null) instance.AddComponent<TruckPaint>();
                if (instance.GetComponent<TruckTailgate>() == null) instance.AddComponent<TruckTailgate>();

                Directory.CreateDirectory(Path.GetDirectoryName(k_PrefabPath));
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, k_PrefabPath);
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("[ToyTruck] Prefab oluşturuldu: " + k_PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
