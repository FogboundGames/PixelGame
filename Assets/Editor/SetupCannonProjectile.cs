using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class SetupCannonProjectile
    {
        public const string ProjectilePrefabPath = "Assets/Prefabs/CannonProjectile.prefab";
        public const string CannonPrefabPath = "Assets/Prefabs/VacuumCannon.prefab";

        [MenuItem("Tools/PixelGame/🚀 Mermi Küresini ve Hedef Göstergesini Kur", priority = 26)]
        public static void SetupAll()
        {
            BuildProjectilePrefab();
            UpdateCannonPrefab();
            UpdateSceneWagons();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FFAA><b>[SetupCannonProjectile]</b></color> Mermi küresi (CannonProjectile.prefab) ve namlu hedef göstergesi başarıyla kuruldu!");
        }

        public static GameObject BuildProjectilePrefab()
        {
            GameObject sphereObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereObj.name = "CannonProjectile";

            Collider col = sphereObj.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            MeshRenderer mr = sphereObj.GetComponent<MeshRenderer>();
            Material mat = CartoonShader.CreateMaterial(Color.red, "Mat_Projectile_Default");
            mr.sharedMaterial = mat;

            // TrailRenderer
            TrailRenderer trail = sphereObj.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.minVertexDistance = 0.02f;
            trail.startWidth = 0.18f;
            trail.endWidth = 0.0f;
            trail.autodestruct = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            Shader trailShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (trailShader == null) trailShader = Shader.Find("Sprites/Default");
            Material trailMat = new Material(trailShader) { name = "Mat_ProjectileTrail" };
            trail.sharedMaterial = trailMat;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.85f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            trail.colorGradient = gradient;

            // CannonProjectile component
            CannonProjectile projectile = sphereObj.AddComponent<CannonProjectile>();

            Directory.CreateDirectory(Path.GetDirectoryName(ProjectilePrefabPath));
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(sphereObj, ProjectilePrefabPath);
            Object.DestroyImmediate(sphereObj);

            return savedPrefab;
        }

        public static void UpdateCannonPrefab()
        {
            GameObject cannonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CannonPrefabPath);
            if (cannonPrefab == null) return;

            GameObject instance = PrefabUtility.InstantiatePrefab(cannonPrefab) as GameObject;
            if (instance == null) return;

            WagonTargetIndicator indicator = instance.GetComponent<WagonTargetIndicator>();
            if (indicator == null) indicator = instance.AddComponent<WagonTargetIndicator>();

            PrefabUtility.SaveAsPrefabAsset(instance, CannonPrefabPath);
            Object.DestroyImmediate(instance);
        }

        public static void UpdateSceneWagons()
        {
            TruckDispatcher dispatcher = Object.FindFirstObjectByType<TruckDispatcher>();
            if (dispatcher != null)
            {
                GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
                if (projectilePrefab != null)
                {
                    SerializedObject so = new SerializedObject(dispatcher);
                    SerializedProperty prop = so.FindProperty("m_ProjectilePrefab");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = projectilePrefab;
                    }
                    SerializedProperty shootProp = so.FindProperty("m_UseShootingMechanic");
                    if (shootProp != null) shootProp.boolValue = true;
                    SerializedProperty preserveProp = so.FindProperty("m_PreserveWagonColors");
                    if (preserveProp != null) preserveProp.boolValue = true;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(dispatcher);
                }
            }

            foreach (WagonTargetIndicator ind in Object.FindObjectsByType<WagonTargetIndicator>(FindObjectsSortMode.None))
            {
                ind.SyncWithCargo();
                EditorUtility.SetDirty(ind);
            }
        }
    }
}
