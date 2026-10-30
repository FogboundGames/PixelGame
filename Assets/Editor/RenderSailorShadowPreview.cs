using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class RenderSailorShadowPreview
    {
        // [MenuItem("PixelGame/📸 Render Sailor Shadow Preview")]
        public static void RenderPreview()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Sailor_Runner.prefab");
            if (prefab == null) return;

            GameObject camGo = new GameObject("TempPreviewCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.backgroundColor = new Color(0.94f, 0.90f, 0.80f, 1f); // Sahil altın kum rengi
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0f, 0f, -3.2f);
            cam.transform.rotation = Quaternion.Euler(22f, 0f, 0f);

            var sailors = new GameObject[3];
            float[] posX = { -1.3f, 0f, 1.3f };
            Vector3[] facings = { Vector3.down, new Vector3(1f, -0.4f, 0f).normalized, Vector3.up };

            for (int i = 0; i < 3; i++)
            {
                sailors[i] = Object.Instantiate(prefab, new Vector3(posX[i], -1.0f, 0f), Quaternion.identity);
                var visual = sailors[i].GetComponent<SailorRunnerVisual>();
                if (visual != null)
                {
                    visual.SetupFakeShadow(0f);
                    visual.SnapFacing(facings[i]);
                }
            }

            RenderTexture rt = new RenderTexture(900, 600, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(900, 600, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 900, 600), 0, 0);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes("scratch/sailor_new_shadow_preview.png", bytes);

            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            for (int i = 0; i < 3; i++) Object.DestroyImmediate(sailors[i]);
            Object.DestroyImmediate(camGo);

            Debug.Log("<color=#00FFAA>[Preview]</color> 3 yönlü gölge önizlemesi 'scratch/sailor_new_shadow_preview.png' dosyasına kaydedildi!");
        }
    }
}
