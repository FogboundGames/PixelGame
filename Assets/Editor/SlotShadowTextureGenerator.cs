using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class SlotShadowTextureGenerator
    {
        public const string SlotShadowPath = "Assets/UI/SlotShadow.png";
        public const string RowGroundShadowPath = "Assets/UI/RowGroundShadow.png";
        private const string SourceSlotPath = "Assets/UI/Slot.png";

        [MenuItem("Tools/PixelGame/🎨 Slot Gölge Dokularını Yeniden Üret")]
        public static void GenerateAllShadowTextures()
        {
            GenerateSlotShadowTexture();
            GenerateRowGroundShadowTexture();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FFAA><b>[PixelGame]</b></color> Slot gölge dokuları (SlotShadow.png & RowGroundShadow.png) başarıyla üretildi!");
        }

        public static Sprite GetOrGenerateSlotShadowSprite()
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SlotShadowPath);
            if (sprite != null) return sprite;

            GenerateSlotShadowTexture();
            AssetDatabase.ImportAsset(SlotShadowPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Sprite>(SlotShadowPath);
        }

        public static Sprite GetOrGenerateRowGroundShadowSprite()
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RowGroundShadowPath);
            if (sprite != null) return sprite;

            GenerateRowGroundShadowTexture();
            AssetDatabase.ImportAsset(RowGroundShadowPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Sprite>(RowGroundShadowPath);
        }

        public static void GenerateSlotShadowTexture()
        {
            // Slot.png görselinin oranlarına (916 x 1254, ~0.73) uygun 384 x 528 gölge dokusu
            int targetW = 384;
            int targetH = 528;
            int padding = 36; // Bulanıklığın dışa taşması için güvenli boşluk
            int cornerRadius = 56;

            float[] mask = new float[targetW * targetH];

            // Yuvarlatılmış dikdörtgen (Rounded Rectangle) silüetini hesapla
            int innerLeft = padding;
            int innerRight = targetW - padding;
            int innerBottom = padding;
            int innerTop = targetH - padding;

            for (int y = 0; y < targetH; y++)
            {
                for (int x = 0; x < targetW; x++)
                {
                    float dist = GetDistanceToRoundedRect(x, y, innerLeft, innerRight, innerBottom, innerTop, cornerRadius);
                    mask[y * targetW + x] = dist <= 0f ? 1f : Mathf.Clamp01(1f - dist);
                }
            }

            // Gauss benzeri çift geçişli kutu bulanıklaştırma
            float[] blurred = BoxBlur(mask, targetW, targetH, 16);
            blurred = BoxBlur(blurred, targetW, targetH, 16);

            Texture2D tex = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
            tex.name = "SlotShadow";

            Color[] pixels = new Color[targetW * targetH];
            for (int i = 0; i < pixels.Length; i++)
            {
                float a = Mathf.Clamp01(blurred[i]);
                // Saf beyaz doku; rengi ve opaklığı UI Image Color bileşeninden kontrol edilir
                pixels[i] = new Color(1f, 1f, 1f, a);
            }

            tex.SetPixels(pixels);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            File.WriteAllBytes(SlotShadowPath, bytes);
            ConfigureSpriteImporter(SlotShadowPath);
        }

        public static void GenerateRowGroundShadowTexture()
        {
            // Tüm şeridi alttan saran yumuşak zemin gölgesi (512 x 128)
            int targetW = 512;
            int targetH = 128;
            int paddingX = 44;
            int paddingY = 24;
            int cornerRadius = 40;

            float[] mask = new float[targetW * targetH];

            int innerLeft = paddingX;
            int innerRight = targetW - paddingX;
            int innerBottom = paddingY;
            int innerTop = targetH - paddingY;

            for (int y = 0; y < targetH; y++)
            {
                for (int x = 0; x < targetW; x++)
                {
                    float dist = GetDistanceToRoundedRect(x, y, innerLeft, innerRight, innerBottom, innerTop, cornerRadius);
                    mask[y * targetW + x] = dist <= 0f ? 1f : Mathf.Clamp01(1f - dist);
                }
            }

            float[] blurred = BoxBlur(mask, targetW, targetH, 18);
            blurred = BoxBlur(blurred, targetW, targetH, 18);

            Texture2D tex = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
            tex.name = "RowGroundShadow";

            Color[] pixels = new Color[targetW * targetH];
            for (int i = 0; i < pixels.Length; i++)
            {
                float a = Mathf.Clamp01(blurred[i]);
                pixels[i] = new Color(1f, 1f, 1f, a);
            }

            tex.SetPixels(pixels);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            File.WriteAllBytes(RowGroundShadowPath, bytes);
            ConfigureSpriteImporter(RowGroundShadowPath);
        }

        private static float GetDistanceToRoundedRect(float px, float py, float xMin, float xMax, float yMin, float yMax, float radius)
        {
            float cx = Mathf.Clamp(px, xMin + radius, xMax - radius);
            float cy = Mathf.Clamp(py, yMin + radius, yMax - radius);

            float dx = px - cx;
            float dy = py - cy;

            if (px >= xMin + radius && px <= xMax - radius && py >= yMin && py <= yMax)
                return 0f;
            if (py >= yMin + radius && py <= yMax - radius && px >= xMin && px <= xMax)
                return 0f;

            float distToCenter = Mathf.Sqrt(dx * dx + dy * dy);
            return distToCenter - radius;
        }

        private static float[] BoxBlur(float[] src, int w, int h, int r)
        {
            float[] hPass = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                float sum = 0f;
                int count = 0;
                for (int k = -r; k <= r; k++)
                {
                    int x = Mathf.Clamp(k, 0, w - 1);
                    sum += src[row + x];
                    count++;
                }
                hPass[row] = sum / count;

                for (int x = 1; x < w; x++)
                {
                    int rem = Mathf.Clamp(x - r - 1, 0, w - 1);
                    int add = Mathf.Clamp(x + r, 0, w - 1);
                    sum += src[row + add] - src[row + rem];
                    hPass[row + x] = Mathf.Max(0f, sum / count);
                }
            }

            float[] vPass = new float[w * h];
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                int count = 0;
                for (int k = -r; k <= r; k++)
                {
                    int y = Mathf.Clamp(k, 0, h - 1);
                    sum += hPass[y * w + x];
                    count++;
                }
                vPass[x] = sum / count;

                for (int y = 1; y < h; y++)
                {
                    int rem = Mathf.Clamp(y - r - 1, 0, h - 1);
                    int add = Mathf.Clamp(y + r, 0, h - 1);
                    sum += hPass[add * w + x] - hPass[rem * w + x];
                    vPass[y * w + x] = Mathf.Max(0f, sum / count);
                }
            }

            return vPass;
        }

        private static void ConfigureSpriteImporter(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }
    }
}
