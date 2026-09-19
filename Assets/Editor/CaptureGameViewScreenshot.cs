using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class CaptureGameViewScreenshot
    {
        private const string OutputPath = "scratch/gameplay_view_9_16.png";

        [MenuItem("Tools/PixelGame/📸 9:16 Ekran Görüntüsü Al (Capture Screenshot)")]
        public static void Capture()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("[CaptureGameViewScreenshot] Sahne kamerası bulunamadı!");
                return;
            }

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
                string dir = Path.GetDirectoryName(OutputPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(OutputPath, bytes);
                Object.DestroyImmediate(screenTex);

                Debug.Log($"<color=#00FFAA><b>[Screenshot]</b></color> 9:16 Ekran görüntüsü başarıyla kaydedildi: {OutputPath}");
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
