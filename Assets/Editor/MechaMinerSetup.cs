using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Küp kafalı madenci modelini (kask + kazma dahil) oyunda kullanılabilir hale getirir:
    /// FBX import ayarları, animasyon klipleri, Animator controller ve prefab tek seferde kurulur.
    /// Menü: Tools > Toy Truck > Setup Mecha Miner
    /// </summary>
    public static class MechaMinerSetup
    {
        private const string k_Model      = "Assets/Models/MechaMiner/MechaMiner.fbx";
        private const string k_Controller = "Assets/Prefabs/MechaMiner.controller";
        private const string k_Prefab     = "Assets/Prefabs/MechaMiner.prefab";
        private const string k_Palette    = "Assets/Models/ToyTruck/Truck_Palette.png";

        // FBX içindeki take adı -> Unity'de görünecek klip adı, döngü bayrağı
        private static readonly (string take, string name, bool loop)[] s_Clips =
        {
            ("Armature|Running",        "Running",        true),
            ("Armature|Jump",           "Jump",           false),
            ("Armature|ZombiePunching", "ZombiePunching", false),
        };

        private const string k_Speed  = "Speed";
        private const string k_Jump   = "Jump";
        private const string k_Attack = "Attack";

        // [MenuItem("Tools/Toy Truck/Setup Mecha Miner", false, 20)]
        public static void Setup()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_Model) == null)
            {
                Debug.LogError($"[MechaMiner] Model bulunamadı: {k_Model}");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(k_Palette) == null)
            {
                Debug.LogWarning($"[MechaMiner] Palet dokusu yok: {k_Palette}. Renkler beyaz görünebilir.");
            }

            ConfigureImporter();
            AnimatorController controller = BuildController();
            if (controller == null) return;
            BuildPrefab(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(k_Prefab);
            EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log("[MechaMiner] Kurulum tamam: import ayarları, 3 klip, Animator controller ve prefab hazır.");
        }

        private static void ConfigureImporter()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(k_Model);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.skinWeights = ModelImporterSkinWeights.Standard;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

            // Klipleri isimlendir ve döngü bayraklarını ayarla
            var defaults = new Dictionary<string, ModelImporterClipAnimation>();
            foreach (ModelImporterClipAnimation d in importer.defaultClipAnimations)
            {
                defaults[d.takeName] = d;
            }

            var clips = new List<ModelImporterClipAnimation>();
            foreach ((string take, string name, bool loop) in s_Clips)
            {
                if (!defaults.TryGetValue(take, out ModelImporterClipAnimation src))
                {
                    Debug.LogWarning($"[MechaMiner] FBX içinde '{take}' take'i yok, atlanıyor.");
                    continue;
                }
                src.name = name;
                src.loopTime = loop;
                src.keepOriginalPositionY = true;
                clips.Add(src);
            }

            if (clips.Count > 0)
            {
                importer.clipAnimations = clips.ToArray();
            }

            importer.SaveAndReimport();
        }

        private static Dictionary<string, AnimationClip> LoadClips()
        {
            var map = new Dictionary<string, AnimationClip>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(k_Model))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    map[clip.name] = clip;
                }
            }
            return map;
        }

        private static AnimatorController BuildController()
        {
            Dictionary<string, AnimationClip> clips = LoadClips();
            if (!clips.TryGetValue("Running", out AnimationClip run))
            {
                Debug.LogError("[MechaMiner] 'Running' klibi bulunamadı. FBX import ayarlarını kontrol et.");
                return null;
            }
            clips.TryGetValue("Jump", out AnimationClip jump);
            clips.TryGetValue("ZombiePunching", out AnimationClip punch);

            Directory.CreateDirectory(Path.GetDirectoryName(k_Controller));
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(k_Controller) != null)
            {
                AssetDatabase.DeleteAsset(k_Controller);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(k_Controller);
            controller.AddParameter(k_Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(k_Jump, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(k_Attack, AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState runState = sm.AddState("Running", new Vector3(280f, 60f, 0f));
            runState.motion = run;
            runState.speedParameterActive = false;
            sm.defaultState = runState;

            if (jump != null)
            {
                AnimatorState jumpState = sm.AddState("Jump", new Vector3(560f, -40f, 0f));
                jumpState.motion = jump;
                AnimatorStateTransition toJump = sm.AddAnyStateTransition(jumpState);
                toJump.AddCondition(AnimatorConditionMode.If, 0f, k_Jump);
                toJump.hasExitTime = false;
                toJump.duration = 0.08f;
                toJump.canTransitionToSelf = false;

                AnimatorStateTransition back = jumpState.AddTransition(runState);
                back.hasExitTime = true;
                back.exitTime = 0.85f;
                back.duration = 0.15f;
            }

            if (punch != null)
            {
                AnimatorState punchState = sm.AddState("ZombiePunching", new Vector3(560f, 160f, 0f));
                punchState.motion = punch;
                AnimatorStateTransition toPunch = sm.AddAnyStateTransition(punchState);
                toPunch.AddCondition(AnimatorConditionMode.If, 0f, k_Attack);
                toPunch.hasExitTime = false;
                toPunch.duration = 0.08f;
                toPunch.canTransitionToSelf = false;

                AnimatorStateTransition back = punchState.AddTransition(runState);
                back.hasExitTime = false;
                back.duration = 0.20f;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void BuildPrefab(AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_Model);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "MechaMiner";
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Animator animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            if (instance.GetComponent<TruckPaint>() == null)
            {
                instance.AddComponent<TruckPaint>();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(k_Prefab));
            PrefabUtility.SaveAsPrefabAsset(instance, k_Prefab);
            Object.DestroyImmediate(instance);
        }
    }
}
