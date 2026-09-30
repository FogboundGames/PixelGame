using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Piksel sanatı görsellerindeki renk gürültüsünü temizleyen,
    /// fazla renkleri K-Means kümeleme ile 3-6 ana renge indirgeyen
    /// ve bölümlerin kilitlenmeden (%0 deadlock) oynanabilir olmasını sağlayan optimizasyon aracı.
    /// </summary>
    public static class PixelPaletteOptimizer
    {
        public class OptimizationResult
        {
            public Texture2D ResultTexture;
            public int OriginalColorCount;
            public int FinalColorCount;
            public List<Color> Palette = new List<Color>();
            public string SavedAssetPath;
        }

        /// <summary>
        /// Verilen dokudaki renkleri K-Means kümeleme ile hedef renk sayısına (k) indirger.
        /// </summary>
        public static Texture2D Quantize(Texture2D source, int targetColors, float alphaThreshold = 0.2f, bool denoise = true)
        {
            if (source == null) return null;
            EnsureReadable(source);

            int width = source.width;
            int height = source.height;
            Color[] pixels = source.GetPixels();

            // 1. Şeffaf olmayan pikselleri topla
            List<Vector3> validLabPixels = new List<Vector3>();
            List<int> validIndices = new List<int>();

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                if (c.a >= alphaThreshold)
                {
                    validLabPixels.Add(new Vector3(c.r, c.g, c.b));
                    validIndices.Add(i);
                }
            }

            if (validLabPixels.Count == 0) return Object.Instantiate(source);

            // Hedef renk sayısı geçerli piksel sayısından fazla olamaz
            int k = Mathf.Clamp(targetColors, 1, Mathf.Min(16, validLabPixels.Count));

            // 2. K-Means++ ile Başlangıç Merkezlerini Seç
            List<Vector3> centroids = InitCentroidsKMeansPlusPlus(validLabPixels, k);

            // 3. K-Means İterasyonları (Maks 15 iterasyon genelde piksel sanatı için yeterlidir)
            int[] assignments = new int[validLabPixels.Count];
            for (int iter = 0; iter < 15; iter++)
            {
                bool changed = false;

                // En yakın merkeze ata
                for (int i = 0; i < validLabPixels.Count; i++)
                {
                    Vector3 p = validLabPixels[i];
                    int bestIdx = 0;
                    float bestDistSq = float.MaxValue;

                    for (int cIdx = 0; cIdx < centroids.Count; cIdx++)
                    {
                        float dSq = (p - centroids[cIdx]).sqrMagnitude;
                        if (dSq < bestDistSq)
                        {
                            bestDistSq = dSq;
                            bestIdx = cIdx;
                        }
                    }

                    if (assignments[i] != bestIdx)
                    {
                        assignments[i] = bestIdx;
                        changed = true;
                    }
                }

                if (!changed && iter > 0) break;

                // Merkezleri yeniden hesapla
                Vector3[] sums = new Vector3[k];
                int[] counts = new int[k];

                for (int i = 0; i < validLabPixels.Count; i++)
                {
                    int cluster = assignments[i];
                    sums[cluster] += validLabPixels[i];
                    counts[cluster]++;
                }

                for (int cIdx = 0; cIdx < k; cIdx++)
                {
                    if (counts[cIdx] > 0)
                    {
                        centroids[cIdx] = sums[cIdx] / counts[cIdx];
                    }
                }
            }

            // 4. Yeni Doku Oluştur
            Color[] outPixels = new Color[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                outPixels[i] = Color.clear;
            }

            for (int i = 0; i < validLabPixels.Count; i++)
            {
                int origIdx = validIndices[i];
                Vector3 c = centroids[assignments[i]];
                outPixels[origIdx] = new Color(c.x, c.y, c.z, 1f);
            }

            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.filterMode = FilterMode.Point;
            result.SetPixels(outPixels);
            result.Apply();

            // 5. İsteğe Bağlı: Yalnız (İzole) Tekil Pikselleri Temizle (Denoise)
            if (denoise)
            {
                result = CleanIsolatedPixels(result);
            }

            return result;
        }

        /// <summary>
        /// K-Means++ başlangıç noktası belirleme: Merkezleri olabildiğince birbirinden uzak seçer.
        /// </summary>
        private static List<Vector3> InitCentroidsKMeansPlusPlus(List<Vector3> points, int k)
        {
            List<Vector3> centroids = new List<Vector3>(k);
            if (points.Count == 0) return centroids;

            // İlk merkezi rastgele seç
            centroids.Add(points[Random.Range(0, points.Count)]);

            float[] minDistSq = new float[points.Count];

            while (centroids.Count < k)
            {
                float totalDistSq = 0f;
                Vector3 lastCentroid = centroids[centroids.Count - 1];

                for (int i = 0; i < points.Count; i++)
                {
                    float dSq = (points[i] - lastCentroid).sqrMagnitude;
                    if (centroids.Count == 1 || dSq < minDistSq[i])
                    {
                        minDistSq[i] = dSq;
                    }
                    totalDistSq += minDistSq[i];
                }

                if (totalDistSq <= 0.0001f)
                {
                    // Kalanları rastgele doldur
                    centroids.Add(points[Random.Range(0, points.Count)]);
                    continue;
                }

                float randVal = Random.value * totalDistSq;
                float cumulative = 0f;
                int chosenIdx = points.Count - 1;

                for (int i = 0; i < points.Count; i++)
                {
                    cumulative += minDistSq[i];
                    if (cumulative >= randVal)
                    {
                        chosenIdx = i;
                        break;
                    }
                }

                centroids.Add(points[chosenIdx]);
            }

            return centroids;
        }

        /// <summary>
        /// 3x3 komşuluğunda aynı renkte hiç komşusu olmayan veya tek tük kalan pikselleri
        /// en baskın komşu renge yuvarlayarak pürüzleri giderir.
        /// </summary>
        public static Texture2D CleanIsolatedPixels(Texture2D source)
        {
            int w = source.width;
            int h = source.height;
            Color[] inPixels = source.GetPixels();
            Color[] outPixels = (Color[])inPixels.Clone();

            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    Color current = inPixels[idx];
                    if (current.a < 0.1f) continue;

                    Dictionary<Color, int> neighborCounts = new Dictionary<Color, int>();
                    int sameNeighborCount = 0;

                    for (int i = 0; i < 8; i++)
                    {
                        int nx = x + dx[i];
                        int ny = y + dy[i];
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                        {
                            Color nc = inPixels[ny * w + nx];
                            if (nc.a >= 0.1f)
                            {
                                if (ColorsMatch(current, nc, 0.05f))
                                {
                                    sameNeighborCount++;
                                }

                                bool matched = false;
                                foreach (var key in neighborCounts.Keys)
                                {
                                    if (ColorsMatch(key, nc, 0.05f))
                                    {
                                        neighborCounts[key]++;
                                        matched = true;
                                        break;
                                    }
                                }
                                if (!matched) neighborCounts[nc] = 1;
                            }
                        }
                    }

                    // Eğer etrafında aynı renkten en fazla 1 piksel varsa ve baskın başka bir renk varsa onu al
                    if (sameNeighborCount <= 1 && neighborCounts.Count > 0)
                    {
                        Color bestColor = current;
                        int maxCount = 0;
                        foreach (var kvp in neighborCounts)
                        {
                            if (!ColorsMatch(kvp.Key, current, 0.05f) && kvp.Value > maxCount)
                            {
                                maxCount = kvp.Value;
                                bestColor = kvp.Key;
                            }
                        }

                        if (maxCount >= 3)
                        {
                            outPixels[idx] = bestColor;
                        }
                    }
                }
            }

            Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.filterMode = FilterMode.Point;
            result.SetPixels(outPixels);
            result.Apply();
            return result;
        }

        /// <summary>
        /// Optimize edilmiş dokuyu projeye kaydeder ve import ayarlarını point/sprite yapar.
        /// </summary>
        public static Texture2D SaveAsOptimizedAsset(Texture2D sourceAsset, Texture2D optimizedTexture, string nameSuffix = "_opt")
        {
            if (optimizedTexture == null) return null;

            string folder = "Assets/PixelArt/Optimized";
            if (!AssetDatabase.IsValidFolder("Assets/PixelArt"))
                AssetDatabase.CreateFolder("Assets", "PixelArt");
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/PixelArt", "Optimized");

            string baseName = sourceAsset != null ? sourceAsset.name : "LevelArt";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}{nameSuffix}.png");

            byte[] bytes = optimizedTexture.EncodeToPNG();
            File.WriteAllBytes(assetPath, bytes);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        /// <summary>
        /// Bir PixelLevelData seviyesinin görselini belirtilen renk sayısına indirger ve seviyeyi günceller.
        /// Her zaman orijinal kaynak görselden (OriginalSourceTexture) indirgeme yapar;
        /// böylece 4 renge indirgedikten sonra kullanıcı 5 veya 6 seçtiğinde de orijinal tonlardan türetilir!
        /// </summary>
        public static bool OptimizeLevelAsset(PixelLevelData level, int targetColors, bool denoise = true)
        {
            if (level == null) return false;

            // Orijinal dokuyu koru
            Texture2D sourceTex = level.OriginalSourceTexture;
            if (sourceTex == null)
            {
                sourceTex = level.LevelTexture;
                if (sourceTex != null)
                {
                    level.OriginalSourceTexture = sourceTex;
                }
            }
            if (sourceTex == null) return false;

            Texture2D quantized = Quantize(sourceTex, targetColors, 0.2f, denoise);
            if (quantized == null) return false;

            Texture2D savedTex = SaveAsOptimizedAsset(sourceTex, quantized, $"_{targetColors}c");
            Object.DestroyImmediate(quantized);

            if (savedTex != null)
            {
                Undo.RecordObject(level, "Optimize Level Colors");
                level.LevelTexture = savedTex;
                level.ExtractPaletteFromTexture();
                level.GenerateInterleavedWagonSequenceFromPalette();
                EditorUtility.SetDirty(level);
                AssetDatabase.SaveAssets();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Dokudaki benzersiz renk sayısını (şeffaf olmayanlar) sayar.
        /// </summary>
        public static int CountUniqueColors(Texture2D tex, float alphaThreshold = 0.2f, float matchThreshold = 0.04f)
        {
            if (tex == null) return 0;
            EnsureReadable(tex);

            Color[] pixels = tex.GetPixels();
            List<Color> uniqueColors = new List<Color>();

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                if (c.a < alphaThreshold) continue;

                bool found = false;
                for (int j = 0; j < uniqueColors.Count; j++)
                {
                    if (ColorsMatch(uniqueColors[j], c, matchThreshold))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found) uniqueColors.Add(c);
            }

            return uniqueColors.Count;
        }

        private static bool ColorsMatch(Color a, Color b, float threshold)
        {
            return Mathf.Abs(a.r - b.r) < threshold &&
                   Mathf.Abs(a.g - b.g) < threshold &&
                   Mathf.Abs(a.b - b.b) < threshold;
        }

        private static void EnsureReadable(Texture2D tex)
        {
            if (tex == null || tex.isReadable) return;
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && !imp.isReadable)
            {
                imp.isReadable = true;
                imp.SaveAndReimport();
            }
        }
    }
}
