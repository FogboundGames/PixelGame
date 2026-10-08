using System.Collections.Generic;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Bir bölümün görselinden zorluk ölçümleri çıkarır. Zorluk büyük ölçüde resmin renk dağılımından gelir:
    /// tek bir renk ekranın çoğunu kaplıyorsa o renkten art arda gemi gelse bile oyuncu hata yapamaz.
    /// </summary>
    public static class LevelDifficultyReport
    {
        public enum Tier { Kolay, Orta, Zor }

        public class Result
        {
            public bool valid;
            public int totalCubes;
            public int colorCount;              // küpü olan renk sayısı
            public float dominantShare;         // en baskın rengin oranı (0–1)
            public string dominantName;
            public float effectiveColors;       // exp(Shannon entropisi): dengeli dağılımda renk sayısına yaklaşır
            public int regionCount;             // aynı renkli bitişik bölge sayısı (4 komşu)
            public float avgRegionSize;
            public float exposedShare;          // başta açıkta olan küp oranı
            public int shells;                  // dıştan içe kaç tur soyulunca biter
            public int buriedColors;            // başta hiç açık küpü olmayan renk sayısı
            public float score;                 // 0–100
            public Tier tier;
            public List<string> reasons = new List<string>();
        }

        public static string TierLabel(Tier t) => t == Tier.Kolay ? "🟢 Kolay" : t == Tier.Orta ? "🟡 Orta" : "🔴 Zor";
        public static Color TierColor(Tier t) => t == Tier.Kolay ? new Color(0.3f, 0.95f, 0.45f) : t == Tier.Orta ? new Color(0.95f, 0.85f, 0.25f) : new Color(1f, 0.45f, 0.35f);

        // Liste ve tablolar her çizimde resim okumasın: bölüm değişmedikçe sonuç önbellekten
        private static readonly Dictionary<PixelLevelData, (int dirty, int tex, Result result)> s_Cache = new Dictionary<PixelLevelData, (int, int, Result)>();

        public static Result GetCached(PixelLevelData level)
        {
            if (level == null) return new Result();
            int dirty = UnityEditor.EditorUtility.GetDirtyCount(level);
            var tex = level.GetActiveTexture();
            int texId = tex != null ? tex.GetInstanceID() : 0;
            if (s_Cache.TryGetValue(level, out var c) && c.dirty == dirty && c.tex == texId) return c.result;
            var r = Compute(level);
            s_Cache[level] = (dirty, texId, r);
            return r;
        }

        public static Result Compute(PixelLevelData level)
        {
            var r = new Result();
            if (level == null) return r;
            var grid = LevelSolvabilityAnalyzer.BuildGrid(level, out int minX, out int maxX, out int minY, out int maxY);
            if (grid.Count == 0) return r;
            r.valid = true;
            r.totalCubes = grid.Count;

            // Renk payları
            var counts = new Dictionary<int, int>();
            foreach (var kv in grid) { counts.TryGetValue(kv.Value, out int c); counts[kv.Value] = c + 1; }
            r.colorCount = counts.Count;
            int domIdx = -1, domCount = 0;
            double entropy = 0;
            foreach (var kv in counts)
            {
                if (kv.Value > domCount) { domCount = kv.Value; domIdx = kv.Key; }
                double p = kv.Value / (double)r.totalCubes;
                entropy -= p * System.Math.Log(p);
            }
            r.dominantShare = domCount / (float)r.totalCubes;
            r.effectiveColors = (float)System.Math.Exp(entropy);
            var pal = level.ColorPalette;
            r.dominantName = pal != null && domIdx >= 0 && domIdx < pal.Count && pal[domIdx] != null
                ? "#" + ColorUtility.ToHtmlStringRGB(pal[domIdx].targetColor) : $"Renk #{domIdx + 1}";

            // Renk bölgeleri
            var seen = new HashSet<(int, int)>();
            var q = new Queue<(int, int)>();
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            foreach (var kv in grid)
            {
                if (seen.Contains(kv.Key)) continue;
                r.regionCount++;
                seen.Add(kv.Key); q.Enqueue(kv.Key);
                while (q.Count > 0)
                {
                    var (x, y) = q.Dequeue();
                    for (int i = 0; i < 4; i++)
                    {
                        var n = (x + dx[i], y + dy[i]);
                        if (seen.Contains(n) || !grid.TryGetValue(n, out int c) || c != kv.Value) continue;
                        seen.Add(n); q.Enqueue(n);
                    }
                }
            }
            r.avgRegionSize = r.totalCubes / (float)Mathf.Max(1, r.regionCount);

            // Açık küpler, gömülü renkler ve kabuk sayısı (her turda dışarıdan erişilen bütün küpler alınır)
            var remaining = new Dictionary<(int, int), int>(grid);
            var exposedColorsAtStart = new HashSet<int>();
            bool first = true;
            while (remaining.Count > 0 && r.shells < 200)
            {
                var air = LevelSolvabilityAnalyzer.ComputeOutsideAir(remaining, minX, maxX, minY, maxY);
                var peel = new List<(int, int)>();
                foreach (var kv in remaining)
                    if (LevelSolvabilityAnalyzer.IsExposed(air, kv.Key.Item1, kv.Key.Item2)) peel.Add(kv.Key);
                if (peel.Count == 0) break;
                if (first)
                {
                    r.exposedShare = peel.Count / (float)r.totalCubes;
                    foreach (var p in peel) exposedColorsAtStart.Add(remaining[p]);
                    first = false;
                }
                foreach (var p in peel) remaining.Remove(p);
                r.shells++;
            }
            foreach (var kv in counts) if (!exposedColorsAtStart.Contains(kv.Key)) r.buriedColors++;

            // Puan: her ölçüt 0–1'e çekilip ağırlıklandırılır
            float sDominant = Mathf.InverseLerp(0.70f, 0.25f, r.dominantShare);   // baskın renk azaldıkça zor
            float sEffective = Mathf.InverseLerp(1.5f, 6f, r.effectiveColors);     // dengeli renk çeşitliliği
            float sRegions = Mathf.InverseLerp(40f, 6f, r.avgRegionSize);          // küçük parçalı bölgeler
            float sBuried = Mathf.InverseLerp(0.5f, 0.85f, 1f - r.exposedShare);   // başta gömülü küp oranı
            float sLayers = Mathf.InverseLerp(0f, 3f, r.buriedColors);              // gömülü renkler (katman)
            r.score = 100f * (0.30f * sDominant + 0.25f * sEffective + 0.15f * sRegions + 0.10f * sBuried + 0.20f * sLayers);
            // Kademe doğrudan puandan: 0–50 Kolay, 50–80 Orta, 80–100 Zor. (Eski baskın renk / renk sayısı
            // tavanları kaldırıldı; puan ile kademe çelişip "puanı yüksek ama Kolay" gibi kafa karıştırıyordu.)
            r.tier = r.score < 50f ? Tier.Kolay : r.score < 80f ? Tier.Orta : Tier.Zor;

            if (r.colorCount <= 3) r.reasons.Add($"Sadece {r.colorCount} renk — oyuncunun seçebileceği az gemi rengi var, hata yapma şansı düşük.");
            if (r.dominantShare >= 0.45f) r.reasons.Add($"Tek renk ({r.dominantName}) küplerin %{Mathf.RoundToInt(r.dominantShare * 100)}'ini kaplıyor — o renk hep açıkta, hata yapma şansı az.");
            if (r.effectiveColors < 3f && r.colorCount >= 3) r.reasons.Add($"{r.colorCount} renk var ama dağılım dengesiz: etkin renk sayısı {r.effectiveColors:0.0}.");
            if (r.avgRegionSize > 25f) r.reasons.Add($"Renk bölgeleri büyük (ortalama {r.avgRegionSize:0} küp) — renkler iç içe değil.");
            if (r.buriedColors == 0) r.reasons.Add("Başta gömülü renk yok — katman / sıra baskısı oluşmuyor.");
            if (r.dominantShare <= 0.30f && r.buriedColors >= 2) r.reasons.Add("Renkler dengeli ve katmanlı — puzzle hissi için iyi.");
            return r;
        }
    }
}
