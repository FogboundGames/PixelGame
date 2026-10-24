using System;
using System.Collections.Generic;

namespace PixelGame
{
    /// <summary>
    /// Küp toplama sırası: "önce en alt ve en dış, en son en üst ve en iç".
    ///
    /// İki ayrı kavram:
    /// 1. Erişilebilirlik — küpün dört komşusundan (sol, sağ, üst, alt) en az biri tahtanın dışına bağlı boşluktaysa
    ///    küp alınabilir. Dış boşluk, tahtanın çevresinden 4 yönlü flood fill ile bulunur; kapalı iç boşluklar dış
    ///    boşluk sayılmaz. Çapraz komşuluk sayılmaz.
    /// 2. Öncelik — erişilebilir adaylar arasında sıra:
    ///    a) En alt sıra önce (ızgarada GridY küçüldükçe aşağı; dünya y'si de azalır).
    ///    b) Aynı sırada en dıştaki önce: o sıradaki küplerin (her renk) sol ve sağ ucuna olan uzaklığın küçüğü.
    ///    c) Eşitlikte (iki uca eşit uzak) sol taraf önce, sonra GridX artan. Böylece sonuç her çalıştırmada aynıdır;
    ///       sözlük sırasına, nesne bulunma sırasına veya animasyon zamanlamasına bağlı değildir.
    ///
    /// Bağımlılık: tahtada fiziksel olarak duran her küp (rezerve edilmiş olsa da) hem erişimi hem sıranın uçlarını
    /// belirler. Bir küp kaldırılınca liste baştan hesaplanır: önündeki dış/alt küp gidince içteki/üstteki küp
    /// erişilebilir olur ve listeye ancak o zaman girer.
    ///
    /// Not: Katman (ComputePeelingLayers) ve açıklık (GetOpenness) yardımcıları yalnızca editör test aracı içindir,
    /// sırayı belirlemez.
    /// </summary>
    public static class CollectionOrder
    {
        private static readonly (int, int)[] s_Dirs4 = { (0, -1), (-1, 0), (1, 0), (0, 1) };
        private static readonly (int, int)[] s_DirsDiag = { (-1, -1), (-1, 1), (1, -1), (1, 1) };

        private static int s_CachedOccupiedCount = -1;
        private static int s_CachedOccupiedHash = 0;
        private static Dictionary<(int, int), int> s_CachedPeelingLayers = null;

        /// <summary>
        /// Tahtanın dışına bağlı boş hücreler (sınır kutusu her yandan 1 hücre genişletilerek 4 yönlü flood fill).
        /// </summary>
        public static HashSet<(int, int)> ComputeOutsideAir(ICollection<(int, int)> occupied)
        {
            var air = new HashSet<(int, int)>();
            if (occupied == null || occupied.Count == 0) return air;

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in occupied)
            {
                if (c.Item1 < minX) minX = c.Item1;
                if (c.Item1 > maxX) maxX = c.Item1;
                if (c.Item2 < minY) minY = c.Item2;
                if (c.Item2 > maxY) maxY = c.Item2;
            }
            minX--; maxX++; minY--; maxY++;

            var queue = new Queue<(int, int)>();
            var start = (minX, minY);
            air.Add(start);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var d in s_Dirs4)
                {
                    var n = (cur.Item1 + d.Item1, cur.Item2 + d.Item2);
                    if (n.Item1 < minX || n.Item1 > maxX || n.Item2 < minY || n.Item2 > maxY) continue;
                    if (air.Contains(n) || occupied.Contains(n)) continue;
                    air.Add(n);
                    queue.Enqueue(n);
                }
            }
            return air;
        }

        public static bool IsAccessible(HashSet<(int, int)> outsideAir, int x, int y)
        {
            if (outsideAir == null) return false;
            return outsideAir.Contains((x - 1, y)) || outsideAir.Contains((x + 1, y)) ||
                   outsideAir.Contains((x, y - 1)) || outsideAir.Contains((x, y + 1));
        }

        /// <summary>
        /// Hücrenin dış açıklık derecesini hesaplar (4 yönlü kardinal * 3 + 4 yönlü diyagonal).
        /// Köşe ve çıkıntılarda değer daha yüksektir.
        /// </summary>
        public static int GetOpenness(HashSet<(int, int)> air, int x, int y)
        {
            if (air == null) return 0;
            int card = 0;
            for (int i = 0; i < s_Dirs4.Length; i++)
                if (air.Contains((x + s_Dirs4[i].Item1, y + s_Dirs4[i].Item2))) card++;

            int diag = 0;
            for (int i = 0; i < s_DirsDiag.Length; i++)
                if (air.Contains((x + s_DirsDiag[i].Item1, y + s_DirsDiag[i].Item2))) diag++;

            return card * 3 + diag;
        }

        /// <summary>
        /// Tüm tahta için topolojik soyma katmanlarını (peeling layers) hesaplar.
        /// Katman 0: En dış çevre / silüet.
        /// Katman 1: Katman 0 alındıktan sonra açılan çevre...
        /// </summary>
        public static Dictionary<(int, int), int> ComputePeelingLayers(ICollection<(int, int)> allCubes)
        {
            var layers = new Dictionary<(int, int), int>(allCubes.Count);
            if (allCubes == null || allCubes.Count == 0) return layers;

            var remaining = new HashSet<(int, int)>(allCubes);
            int currentLayer = 0;

            while (remaining.Count > 0)
            {
                var air = ComputeOutsideAir(remaining);
                var shell = new List<(int, int)>();

                foreach (var c in remaining)
                {
                    if (IsAccessible(air, c.Item1, c.Item2))
                    {
                        shell.Add(c);
                    }
                }

                if (shell.Count == 0)
                {
                    // Dış hava teması kalmayan iç izole adacıklar
                    foreach (var c in remaining)
                    {
                        layers[c] = currentLayer;
                    }
                    break;
                }

                for (int i = 0; i < shell.Count; i++)
                {
                    var c = shell[i];
                    layers[c] = currentLayer;
                    remaining.Remove(c);
                }

                currentLayer++;
            }

            return layers;
        }

        private static Dictionary<(int, int), int> GetOrComputePeelingLayers(ICollection<(int, int)> occupied)
        {
            int count = occupied.Count;
            int hash = 17;
            int iter = 0;
            foreach (var c in occupied)
            {
                hash = unchecked(hash * 31 + c.Item1 * 397 ^ c.Item2);
                if (++iter > 30) break; // İlk 30 hücre ile hızlı hash parmak izi
            }

            if (s_CachedPeelingLayers != null && s_CachedOccupiedCount == count && s_CachedOccupiedHash == hash)
            {
                return s_CachedPeelingLayers;
            }

            s_CachedPeelingLayers = ComputePeelingLayers(occupied);
            s_CachedOccupiedCount = count;
            s_CachedOccupiedHash = hash;
            return s_CachedPeelingLayers;
        }

        /// <summary>
        /// Sıradaki toplanabilir küpler, öncelik sırasıyla. <paramref name="occupied"/>: tahtada fiziksel olarak duran
        /// tüm hücreler (renk fark etmez). <paramref name="isCandidate"/>: bu hücre aranan renkte mi ve henüz kimseye
        /// ayrılmamış mı. Erişilemeyen adaylar dönmez; aday yoksa liste boştur (işçi beklemeli).
        /// </summary>
        public static List<(int, int)> GetNextCollectibleCells(ICollection<(int, int)> occupied, Func<(int, int), bool> isCandidate)
        {
            var result = new List<(int, int)>();
            if (occupied == null || occupied.Count == 0) return result;

            var air = ComputeOutsideAir(occupied);

            // Sıra uçları: her satırda duran küplerin (her renk) en sol ve en sağ hücresi
            var rowMin = new Dictionary<int, int>();
            var rowMax = new Dictionary<int, int>();
            foreach (var c in occupied)
            {
                if (!rowMin.TryGetValue(c.Item2, out int mn) || c.Item1 < mn) rowMin[c.Item2] = c.Item1;
                if (!rowMax.TryGetValue(c.Item2, out int mx) || c.Item1 > mx) rowMax[c.Item2] = c.Item1;
            }

            foreach (var c in occupied)
            {
                if (!isCandidate(c)) continue;
                if (!IsAccessible(air, c.Item1, c.Item2)) continue;
                result.Add(c);
            }

            result.Sort((a, b) =>
            {
                if (a.Item2 != b.Item2) return a.Item2.CompareTo(b.Item2);                 // a) en alt sıra önce
                int da = Math.Min(a.Item1 - rowMin[a.Item2], rowMax[a.Item2] - a.Item1);
                int db = Math.Min(b.Item1 - rowMin[b.Item2], rowMax[b.Item2] - b.Item1);
                if (da != db) return da.CompareTo(db);                                    // b) en dıştaki önce
                bool aLeft = a.Item1 - rowMin[a.Item2] <= rowMax[a.Item2] - a.Item1;
                bool bLeft = b.Item1 - rowMin[b.Item2] <= rowMax[b.Item2] - b.Item1;
                if (aLeft != bLeft) return aLeft ? -1 : 1;                                // c) eşitlikte sol önce
                return a.Item1.CompareTo(b.Item1);
            });
            return result;
        }
    }
}
