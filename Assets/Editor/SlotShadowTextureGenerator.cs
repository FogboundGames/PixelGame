using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class SlotShadowTextureGenerator
    {
        public const string SlotShadowPath = "Assets/UI/SlotShadow.png";
        public const string RowGroundShadowPath = "Assets/UI/RowGroundShadow.png";
        public const string PortalShadowPath = "Assets/UI/PortalShadow.png";
        private const string SourceSlotPath = "Assets/UI/Slot.png";

        // [MenuItem("Tools/PixelGame/🎨 Tüm Gölge Dokularını Yeniden Üret")]
        public static void GenerateAllShadowTextures()
        {
            GenerateSlotShadowTexture();
            GenerateRowGroundShadowTexture();
            GeneratePortalShadowTexture();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FFAA><b>[PixelGame]</b></color> Ray ve Portal gölge dokuları (SlotShadow.png, RowGroundShadow.png & PortalShadow.png) başarıyla üretildi!");
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

        public static Sprite GetOrGeneratePortalShadowSprite()
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PortalShadowPath);
            if (sprite != null) return sprite;

            GeneratePortalShadowTexture();
            AssetDatabase.ImportAsset(PortalShadowPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Sprite>(PortalShadowPath);
        }

        public static void GenerateSlotShadowTexture()
        {
            string sourcePath = "Assets/UI/slot1.png";
            if (!File.Exists(sourcePath)) sourcePath = "Assets/UI/Slot.png";

            if (File.Exists(sourcePath))
            {
                byte[] srcBytes = File.ReadAllBytes(sourcePath);
                Texture2D srcTex = new Texture2D(2, 2);
                if (srcTex.LoadImage(srcBytes))
                {
                    int w = srcTex.width;
                    int h = srcTex.height;
                    Color32[] srcPixels = srcTex.GetPixels32();
                    float[] mask = new float[w * h];

                    for (int i = 0; i < srcPixels.Length; i++)
                    {
                        mask[i] = srcPixels[i].a > 25 ? 1f : 0f;
                    }
                    Object.DestroyImmediate(srcTex);

                    // Çift geçişli kutu bulanıklaştırma: Temas gölgesi (küçük yarıçap) + Yayılma aurası (geniş yarıçap)
                    float[] blurTight = BoxBlur(mask, w, h, Mathf.Max(6, Mathf.RoundToInt(w * 0.02f)));
                    blurTight = BoxBlur(blurTight, w, h, Mathf.Max(6, Mathf.RoundToInt(w * 0.02f)));

                    float[] blurSpread = BoxBlur(mask, w, h, Mathf.Max(14, Mathf.RoundToInt(w * 0.045f)));
                    blurSpread = BoxBlur(blurSpread, w, h, Mathf.Max(14, Mathf.RoundToInt(w * 0.045f)));

                    Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    tex.name = "SlotShadow";

                    Color32[] pixels = new Color32[w * h];
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        float alphaNorm = Mathf.Clamp01(blurTight[i] * 0.72f + blurSpread[i] * 0.42f);
                        byte a = (byte)(alphaNorm * 255f);
                        pixels[i] = new Color32(255, 255, 255, a);
                    }

                    tex.SetPixels32(pixels);
                    tex.Apply();

                    byte[] bytes = tex.EncodeToPNG();
                    Object.DestroyImmediate(tex);

                    File.WriteAllBytes(SlotShadowPath, bytes);
                    ConfigureSpriteImporter(SlotShadowPath);
                    return;
                }
            }

            // Fallback: Yuvarlatılmış dikdörtgen (Rounded Rectangle)
            int targetW = 384;
            int targetH = 336; // slot1 oranına daha yakın
            int padding = 32;
            int cornerRadius = 48;

            float[] fallbackMask = new float[targetW * targetH];
            int innerLeft = padding;
            int innerRight = targetW - padding;
            int innerBottom = padding;
            int innerTop = targetH - padding;

            for (int y = 0; y < targetH; y++)
            {
                for (int x = 0; x < targetW; x++)
                {
                    float dist = GetDistanceToRoundedRect(x, y, innerLeft, innerRight, innerBottom, innerTop, cornerRadius);
                    fallbackMask[y * targetW + x] = dist <= 0f ? 1f : Mathf.Clamp01(1f - dist);
                }
            }

            float[] blurred = BoxBlur(fallbackMask, targetW, targetH, 14);
            blurred = BoxBlur(blurred, targetW, targetH, 14);

            Texture2D fbTex = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
            fbTex.name = "SlotShadow";

            Color[] fbPixels = new Color[targetW * targetH];
            for (int i = 0; i < fbPixels.Length; i++)
            {
                float a = Mathf.Clamp01(blurred[i]);
                fbPixels[i] = new Color(1f, 1f, 1f, a);
            }

            fbTex.SetPixels(fbPixels);
            fbTex.Apply();

            byte[] fbBytes = fbTex.EncodeToPNG();
            Object.DestroyImmediate(fbTex);

            File.WriteAllBytes(SlotShadowPath, fbBytes);
            ConfigureSpriteImporter(SlotShadowPath);
        }

        public static void GenerateRowGroundShadowTexture()
        {
            // Ray demirleri ve ahşap traverslerin hat gölgesi (2048 x 512)
            int targetW = 2048;
            int targetH = 512;
            int cx = targetW / 2;
            int cy = targetH / 2;

            float spanX = 2224f;
            float scaleX = (float)targetW / spanX;
            float scaleY = scaleX;

            float[] mask = new float[targetW * targetH];

            float[] slotCenters = new float[] { -832f, -416f, 0f, 416f, 832f };
            float[] tieOffsets = new float[] { -160f, -80f, 0f, 80f, 160f };

            float tieWHalf = 11f * scaleX;
            float tieTopY = 86f * scaleY;
            float tieBottomY = 96f * scaleY;

            // 1. 25 Ahşap Travers (Wooden Ties)
            foreach (float sc in slotCenters)
            {
                foreach (float to in tieOffsets)
                {
                    float tx = cx + (sc + to) * scaleX;
                    int x0 = Mathf.Clamp(Mathf.RoundToInt(tx - tieWHalf), 0, targetW - 1);
                    int x1 = Mathf.Clamp(Mathf.RoundToInt(tx + tieWHalf), 0, targetW - 1);
                    int y0 = Mathf.Clamp(Mathf.RoundToInt(cy - tieBottomY), 0, targetH - 1);
                    int y1 = Mathf.Clamp(Mathf.RoundToInt(cy + tieTopY), 0, targetH - 1);

                    for (int y = y0; y <= y1; y++)
                    {
                        for (int x = x0; x <= x1; x++)
                        {
                            int idx = y * targetW + x;
                            if (mask[idx] < 0.95f) mask[idx] = 0.95f;
                        }
                    }
                }
            }

            // 2. 2 Kesintisiz Demir Ray Hattı (Continuous Iron Rails)
            int railLeft = Mathf.Clamp(Mathf.RoundToInt(cx + (-1030f) * scaleX), 0, targetW - 1);
            int railRight = Mathf.Clamp(Mathf.RoundToInt(cx + (1030f) * scaleX), 0, targetW - 1);

            float railYUpper = 46f * scaleY;
            float railYLower = -46f * scaleY;
            float railThickHalf = 6f * scaleY;

            int uy0 = Mathf.Clamp(Mathf.RoundToInt(cy + railYUpper - railThickHalf), 0, targetH - 1);
            int uy1 = Mathf.Clamp(Mathf.RoundToInt(cy + railYUpper + railThickHalf), 0, targetH - 1);
            for (int y = uy0; y <= uy1; y++)
            {
                for (int x = railLeft; x <= railRight; x++)
                {
                    int idx = y * targetW + x;
                    if (mask[idx] < 0.90f) mask[idx] = 0.90f;
                }
            }

            int ly0 = Mathf.Clamp(Mathf.RoundToInt(cy + railYLower - railThickHalf), 0, targetH - 1);
            int ly1 = Mathf.Clamp(Mathf.RoundToInt(cy + railYLower + railThickHalf), 0, targetH - 1);
            for (int y = ly0; y <= ly1; y++)
            {
                for (int x = railLeft; x <= railRight; x++)
                {
                    int idx = y * targetW + x;
                    if (mask[idx] < 0.90f) mask[idx] = 0.90f;
                }
            }

            // 3. Ray Yatağı Zemin Oklüzyonu (Ambient Ground Shadow)
            float bedYHalf = 72f * scaleY;
            int by0 = Mathf.Clamp(Mathf.RoundToInt(cy - bedYHalf), 0, targetH - 1);
            int by1 = Mathf.Clamp(Mathf.RoundToInt(cy + bedYHalf), 0, targetH - 1);
            for (int y = by0; y <= by1; y++)
            {
                float distY = Mathf.Abs(y - cy) / bedYHalf;
                float val = (1f - distY) * 0.28f;
                for (int x = railLeft; x <= railRight; x++)
                {
                    int idx = y * targetW + x;
                    if (mask[idx] < val) mask[idx] = val;
                }
            }

            // 4. Portalların Alt Temas Gölgeleri
            float[] portalXs = new float[] { -992f, 992f };
            float pr = 76f * scaleX;
            foreach (float portalX in portalXs)
            {
                float px = cx + portalX * scaleX;
                int py0 = Mathf.Clamp(Mathf.RoundToInt(cy - pr), 0, targetH - 1);
                int py1 = Mathf.Clamp(Mathf.RoundToInt(cy + pr), 0, targetH - 1);
                int px0 = Mathf.Clamp(Mathf.RoundToInt(px - pr), 0, targetW - 1);
                int px1 = Mathf.Clamp(Mathf.RoundToInt(px + pr), 0, targetW - 1);

                for (int y = py0; y <= py1; y++)
                {
                    for (int x = px0; x <= px1; x++)
                    {
                        float dx = (x - px) / pr;
                        float dy = (y - cy) / (pr * 0.85f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d < 1f)
                        {
                            float val = (1f - d) * 0.65f;
                            int idx = y * targetW + x;
                            if (mask[idx] < val) mask[idx] = val;
                        }
                    }
                }
            }

            // Yumuşak gölge geçişi (2 geçişli Gauss benzeri kutu bulanıklaştırma)
            float[] blurred = BoxBlur(mask, targetW, targetH, 8);
            blurred = BoxBlur(blurred, targetW, targetH, 8);

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

        public static void GeneratePortalShadowTexture()
        {
            // Maden portalının taş ayakları ve tünel girişine özel temas gölgesi (512 x 512)
            int targetW = 512;
            int targetH = 512;

            float[] mask = new float[targetW * targetH];

            // Sol ayak, sağ ayak ve arka tünel birleşimi için birleşik silüet
            for (int y = 0; y < targetH; y++)
            {
                for (int x = 0; x < targetW; x++)
                {
                    // Sol taş sütun tabanı
                    float dLeft = GetDistanceToRoundedRect(x, y, 70, 210, 110, 390, 36);
                    float leftVal = dLeft <= 0f ? 1f : Mathf.Clamp01(1f - dLeft * 0.1f);

                    // Sağ taş sütun tabanı
                    float dRight = GetDistanceToRoundedRect(x, y, 302, 442, 110, 390, 36);
                    float rightVal = dRight <= 0f ? 1f : Mathf.Clamp01(1f - dRight * 0.1f);

                    // Arka kemer / tünel girişi tavan arkası
                    float dArch = GetDistanceToRoundedRect(x, y, 140, 372, 240, 420, 44);
                    float archVal = dArch <= 0f ? 0.9f : Mathf.Clamp01(0.9f - dArch * 0.1f);

                    // Tünel içi zemin oklüzyonu
                    float dCenter = GetDistanceToRoundedRect(x, y, 170, 342, 140, 320, 30);
                    float centerVal = dCenter <= 0f ? 0.55f : Mathf.Clamp01(0.55f - dCenter * 0.08f);

                    float maxVal = Mathf.Max(leftVal, Mathf.Max(rightVal, Mathf.Max(archVal, centerVal)));
                    mask[y * targetW + x] = maxVal;
                }
            }

            float[] blurred = BoxBlur(mask, targetW, targetH, 20);
            blurred = BoxBlur(blurred, targetW, targetH, 20);

            Texture2D tex = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
            tex.name = "PortalShadow";

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

            File.WriteAllBytes(PortalShadowPath, bytes);
            ConfigureSpriteImporter(PortalShadowPath);
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
                if (path == SlotShadowPath)
                {
                    importer.spriteBorder = Vector4.zero;
                }
                importer.SaveAndReimport();
            }
        }
    }
}
