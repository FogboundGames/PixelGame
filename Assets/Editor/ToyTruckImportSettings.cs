using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Assets/Models altındaki oyuncak modeller (ToyTruck, MineCart, Track) için import ayarlarını
    /// ilk importta otomatik yapar: palet dokusu Point filtre / mipmap yok / sıkıştırma yok,
    /// FBX Generic + eksen dönüşümü bake, materyale ortak palet dokusu bağlanır.
    /// Ayrıca "Tools > Toy Truck" altına prefab oluşturma menülerini ekler.
    /// </summary>
    public class ToyTruckImportSettings : AssetPostprocessor
    {
        private const string k_ModelsFolder = "Assets/Models/";

        // Bütün modeller aynı palet dokusunu paylaşır; runtime'da TruckPaint kendi ürettiği dokuyu
        // kullanır, bu dosya editördeki görünüm ve varsayılan renkler içindir.
        private const string k_PalettePath = k_ModelsFolder + "ToyTruck/Truck_Palette.png";

        private const string k_TruckModelPath = k_ModelsFolder + "ToyTruck/ToyTruck.fbx";
        private const string k_CartModelPath = k_ModelsFolder + "MineCart/MineCart.fbx";
        private const string k_TrackModelPath = k_ModelsFolder + "Track/Track.fbx";
        private const string k_PortalModelPath = k_ModelsFolder + "MinePortal/MinePortal.fbx";
        private const string k_MinerModelPath = k_ModelsFolder + "MechaMiner/MechaMiner.fbx";
        private const string k_TruckPrefabPath = "Assets/Prefabs/ToyTruck.prefab";
        private const string k_CartPrefabPath = "Assets/Prefabs/MineCart.prefab";
        private const string k_TrackPrefabPath = "Assets/Prefabs/Track.prefab";
        private const string k_PortalPrefabPath = "Assets/Prefabs/MinePortal.prefab";
        private const string k_MinerPrefabPath = "Assets/Prefabs/MechaMiner.prefab";

        // URP'nin kendi FBX materyal işlemcisinden sonra çalışsın ki palet bağlantısı ezilmesin.
        public override int GetPostprocessOrder() => 100;

        private bool IsToyModel => assetPath.StartsWith(k_ModelsFolder) && assetPath.EndsWith(".fbx");

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
            if (!IsToyModel || !assetImporter.importSettingsMissing)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            // Eksen dönüşümü Blender'da mesh'in içine işlendi (Apply Transform), Unity tekrar bakelemesin
            importer.bakeAxisConversion = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

            if (assetPath == k_MinerModelPath)
            {
                // Madenci karakter skinli bir mesh; blend shape yok, mobilde 4 kemik ağırlığı yeter
                importer.importBlendShapes = false;
                importer.importVisibility = false;
                importer.skinWeights = ModelImporterSkinWeights.Standard;
            }

            if (assetPath == k_CartModelPath || assetPath == k_MinerModelPath)
            {
                // MineCart_Roll ve MechaMiner_Run sürekli tekrarlayan hareketler, döngüye alınsın.
                // MechaMiner_Jump tek seferlik bir hareket, döngüye girmesin.
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    for (int i = 0; i < clips.Length; i++)
                    {
                        string clipName = clips[i].name ?? string.Empty;
                        clips[i].loopTime = clipName.IndexOf("Run", System.StringComparison.OrdinalIgnoreCase) >= 0
                                            || clipName.IndexOf("Roll", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    importer.clipAnimations = clips;
                }
            }
        }

        private void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] materialAnimation)
        {
            if (!IsToyModel)
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
        private static void CreateTruckPrefab()
        {
            CreatePrefab(k_TruckModelPath, k_TruckPrefabPath, "ToyTruck", withTailgate: true, withMover: false);
        }

        [MenuItem("Tools/Toy Truck/Create Mine Cart Prefab")]
        private static void CreateCartPrefab()
        {
            CreatePrefab(k_CartModelPath, k_CartPrefabPath, "MineCart", withTailgate: false, withMover: true);
        }

        [MenuItem("Tools/Toy Truck/Create Track Prefab")]
        private static void CreateTrackPrefab()
        {
            CreatePrefab(k_TrackModelPath, k_TrackPrefabPath, "Track", withTailgate: false, withMover: false);
        }

        [MenuItem("Tools/Toy Truck/Create Mine Portal Prefab")]
        private static void CreatePortalPrefab()
        {
            CreatePrefab(k_PortalModelPath, k_PortalPrefabPath, "MinePortal", withTailgate: false, withMover: false);
        }

        [MenuItem("Tools/Toy Truck/Create Mecha Miner Prefab")]
        private static void CreateMinerPrefab()
        {
            CreatePrefab(k_MinerModelPath, k_MinerPrefabPath, "MechaMiner",
                         withTailgate: false, withMover: false, withAnimator: true);
        }

        /// <summary>
        /// FBX'in içindeki koşu klibini oynatan tek durumlu bir Animator controller döndürür,
        /// yoksa oluşturur.
        /// </summary>
        private static RuntimeAnimatorController GetOrCreateMinerController()
        {
            const string controllerPath = "Assets/Prefabs/MechaMiner.controller";
            var existing = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
            if (existing != null)
            {
                return existing;
            }

            AnimationClip runClip = null;
            AnimationClip jumpClip = null;
            AnimationClip firstClip = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(k_MinerModelPath))
            {
                if (!(asset is AnimationClip candidate) || candidate.name.StartsWith("__preview__"))
                {
                    continue;
                }
                if (firstClip == null) firstClip = candidate;
                if (candidate.name.IndexOf("Jump", System.StringComparison.OrdinalIgnoreCase) >= 0) jumpClip = candidate;
                else if (candidate.name.IndexOf("Run", System.StringComparison.OrdinalIgnoreCase) >= 0) runClip = candidate;
            }

            AnimationClip defaultClip = runClip ?? firstClip;
            if (defaultClip == null)
            {
                Debug.LogWarning("[ToyAssets] " + k_MinerModelPath + " içinde animasyon klibi bulunamadı.");
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(controllerPath));
            UnityEditor.Animations.AnimatorController created =
                UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, defaultClip);

            // Zıplama klibi de controller'a bir durum olarak eklensin; geçişini oyun kodu kursun
            if (jumpClip != null && created.layers.Length > 0)
            {
                UnityEditor.Animations.AnimatorState jumpState =
                    created.layers[0].stateMachine.AddState(jumpClip.name);
                jumpState.motion = jumpClip;
                EditorUtility.SetDirty(created);
                AssetDatabase.SaveAssets();
            }

            return created;
        }

        private static void CreatePrefab(string modelPath, string prefabPath, string objectName,
                                         bool withTailgate, bool withMover, bool withAnimator = false)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                EditorUtility.DisplayDialog("Toy Truck", modelPath + " bulunamadı.", "Tamam");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                instance.name = objectName;
                if (instance.GetComponent<TruckPaint>() == null) instance.AddComponent<TruckPaint>();
                if (withTailgate && instance.GetComponent<TruckTailgate>() == null) instance.AddComponent<TruckTailgate>();
                if (withMover && instance.GetComponent<MineCartMover>() == null) instance.AddComponent<MineCartMover>();

                if (withAnimator)
                {
                    RuntimeAnimatorController controller = GetOrCreateMinerController();
                    var animator = instance.GetComponent<Animator>();
                    if (animator == null) animator = instance.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("[ToyAssets] Prefab oluşturuldu: " + prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
