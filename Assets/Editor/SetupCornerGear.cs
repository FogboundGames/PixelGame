using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupCornerGear
    {
        private const string GearFramesDir = "Assets/UI/GearFrames";

        static SetupCornerGear()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                EnsureCornerGearInScene(silent: true);
            };
        }

        [MenuItem("Tools/PixelGame/⚙️ Sol Alt Köşeye Animasyonlu Dişliyi Yerleştir (Corner Gear Setup)", priority = 38)]
        public static void SetupManual()
        {
            EnsureCornerGearInScene(silent: false);
        }

        public static void EnsureCornerGearInScene(bool silent)
        {
            // 1. Kareleri yükle
            List<Sprite> frames = new List<Sprite>();
            for (int i = 0; i < 36; i++)
            {
                string path = $"{GearFramesDir}/gear_{i:02d}.png";
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) frames.Add(s);
            }

            if (frames.Count == 0)
            {
                Debug.LogWarning("[SetupCornerGear] Dişli kareleri henüz yüklenemedi (Assets/UI/GearFrames)!");
                return;
            }

            // 2. Ray kökünü ve Corner_BL'yi bul
            GameObject railsGroup = GameObject.Find("PerimeterRails");
            Transform parent = railsGroup != null ? railsGroup.transform : null;

            Vector3 cornerPos = new Vector3(-2.7125964f, -2.732596f, -0.15f);

            GameObject cornerBL = GameObject.Find("Corner_BL");
            if (cornerBL != null)
            {
                cornerPos = cornerBL.transform.position;
                cornerPos.z = -0.15f; // Ray yüzeyinin hafif önünde
                if (parent == null) parent = cornerBL.transform.parent;

                // 3D köşe rayı aktif kalsın, dişli hafif önünde dönsün
                if (!cornerBL.activeSelf)
                {
                    cornerBL.SetActive(true);
                    EditorUtility.SetDirty(cornerBL);
                }
            }

            // 3. Corner_Gear nesnesini bul veya oluştur
            GameObject gearObj = GameObject.Find("Corner_Gear");
            if (gearObj == null)
            {
                gearObj = new GameObject("Corner_Gear");
                Undo.RegisterCreatedObjectUndo(gearObj, "Create Corner_Gear");
            }

            if (parent != null)
            {
                gearObj.transform.SetParent(parent, true);
            }

            gearObj.transform.position = cornerPos;
            gearObj.transform.rotation = Quaternion.identity;
            gearObj.transform.localScale = new Vector3(0.20f, 0.20f, 1.0f);

            // 4. SpriteRenderer bileşeni
            SpriteRenderer sr = gearObj.GetComponent<SpriteRenderer>();
            if (sr == null) sr = gearObj.AddComponent<SpriteRenderer>();

            Shader spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default")
                               ?? Shader.Find("Sprites/Default")
                               ?? Shader.Find("Universal Render Pipeline/Unlit");

            if (spriteShader != null && (sr.sharedMaterial == null || sr.sharedMaterial.shader != spriteShader))
            {
                sr.sharedMaterial = new Material(spriteShader);
            }

            sr.sprite = frames[0];
            sr.sortingOrder = 10;

            // 5. CornerGearAnimator bileşeni
            CornerGearAnimator animator = gearObj.GetComponent<CornerGearAnimator>();
            if (animator == null) animator = gearObj.AddComponent<CornerGearAnimator>();

            animator.frames = frames.ToArray();
            animator.baseFps = 20f;
            animator.syncWithTrackFlow = true;
            animator.reverse = false;
            animator.trackFlow = Object.FindFirstObjectByType<TrackFlow>();

            EditorUtility.SetDirty(gearObj);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            if (!silent)
            {
                EditorUtility.DisplayDialog(
                    "Köşe Dişlisi Kuruldu",
                    $"Animasyonlu dişli ({frames.Count} kare) başarıyla sol alt köşeye (Corner_BL) yerleştirildi ve TrackFlow ile senkronize edildi!\n\nKonum: {cornerPos}",
                    "Tamam"
                );
            }

            Debug.Log($"<color=#00FFAA><b>[SetupCornerGear]</b></color> Dişli {frames.Count} kareyle sol alt köşeye ({cornerPos}) yerleştirildi!");
        }
    }
}

// touch
