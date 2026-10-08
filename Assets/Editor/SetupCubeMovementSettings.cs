using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupCubeMovementSettings
    {
        private const string AssetPath = "Assets/Resources/CubeMovementSettings.asset";

        static SetupCubeMovementSettings()
        {
            EditorApplication.delayCall += EnsureSettingsAsset;
        }

        [MenuItem("PixelGame/✨ Ensure Cube Movement Settings")]
        public static void EnsureSettingsAsset()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!Directory.Exists("Assets/Resources"))
            {
                Directory.CreateDirectory("Assets/Resources");
                AssetDatabase.Refresh();
            }

            CubeMovementSettings settings = AssetDatabase.LoadAssetAtPath<CubeMovementSettings>(AssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CubeMovementSettings>();
                settings.MoveSpeed = 4.2f;
                settings.Acceleration = 9.5f;
                settings.Deceleration = 12.0f;
                settings.TurnSpeed = 380f;
                settings.RotationSmoothness = 0.10f;
                settings.PathCurvature = 1.0f;
                settings.PathWidth = 0.12f;
                settings.PathSmoothing = 16;
                settings.ArrivalDistance = 0.45f;
                settings.ObstacleClearance = 0.38f;
                settings.CorridorClearance = 0.15f;
                settings.BobAmount = 0.038f;
                settings.BobSpeed = 14f;
                settings.TiltAmount = 6.5f;
                settings.TiltSmoothness = 0.08f;
                settings.SquashAmount = 0.14f;
                settings.SquashSpeed = 16f;
                settings.ArrivalSlowdown = 0.35f;
                settings.ArrivalEase = 1.6f;
                settings.SettleDuration = 0.11f;
                settings.SettleBounce = 0.045f;
                settings.SpeedVariation = 0.08f;
                settings.BobVariation = 0.35f;
                settings.StartDelayVariation = 0.035f;
                settings.ArrivalVariation = 0.025f;
                settings.AnticipationDuration = 0.08f;
                settings.AnticipationRecoil = 0.035f;
                settings.EnableSandFootprints = true;
                settings.FootstepDistance = 0.38f;
                settings.FootstepSpacing = 0.28f;
                settings.FootprintLifetime = 3.6f;
                settings.FootprintFadeDuration = 1.2f;
                settings.FootprintSize = 0.22f;
                settings.FootprintOpacity = 0.28f;
                settings.FootprintColor = new Color(0.68f, 0.48f, 0.24f, 0.28f);
                settings.FootstepPuff = true;

                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("<color=#00FFAA><b>[CubeMovementSettings]</b></color> CubeMovementSettings.asset başarıyla oluşturuldu!");
            }

            // Sahnedeki ShipDispatcher'a bağla
            ShipDispatcher dispatcher = Object.FindFirstObjectByType<ShipDispatcher>();
            if (dispatcher != null && settings != null)
            {
                var so = new SerializedObject(dispatcher);
                var prop = so.FindProperty("m_CubeMovementSettings");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = settings;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(dispatcher);
                }
            }
        }

        [MenuItem("PixelGame/👣 Ensure Sand Footprint Manager")]
        public static void EnsureSandFootprintManager()
        {
            SandFootprintManager mgr = Object.FindFirstObjectByType<SandFootprintManager>();
            if (mgr == null)
            {
                GameObject root = GameObject.Find("[GAMEPLAY_MODELS]");
                GameObject go = new GameObject("[SandFootprintManager]");
                if (root != null) go.transform.SetParent(root.transform, false);
                mgr = go.AddComponent<SandFootprintManager>();
                Undo.RegisterCreatedObjectUndo(go, "Create Sand Footprint Manager");
                Debug.Log("<color=#00FFAA><b>[SandFootprintManager]</b></color> SandFootprintManager sahneye başarıyla eklendi!");
            }
            else
            {
                Debug.Log("<color=#00FFAA><b>[SandFootprintManager]</b></color> SandFootprintManager zaten sahnede mevcut.");
            }
        }
    }
}
