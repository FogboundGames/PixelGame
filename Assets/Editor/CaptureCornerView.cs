using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class CaptureCornerView
    {
        // [MenuItem("Tools/PixelGame/📸 Test Köşe Görüntüsü Al (Capture Corner Test)")]
        public static void Capture()
        {
            SetupCornerLauncherStation.ApplyCornerLauncher();

            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null) return;

            int width = 1080;
            int height = 1920;

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevRT = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;

            try
            {
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                Texture2D screenTex = new Texture2D(width, height, TextureFormat.RGB24, false);
                screenTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenTex.Apply();

                byte[] bytes = screenTex.EncodeToPNG();
                string outPath = "scratch/corner_test.png";
                File.WriteAllBytes(outPath, bytes);
                Object.DestroyImmediate(screenTex);

                Debug.Log($"<color=#00FFAA><b>[CornerTest]</b></color> Test ekran görüntüsü kaydedildi: {outPath}");
            }
            finally
            {
                cam.targetTexture = prevRT;
                RenderTexture.active = prevActive;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
