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
    /// </summary>
    public static class CollectionOrder
    {
        private static readonly (int, int)[] s_Dirs = { (0, -1), (-1, 0), (1, 0), (0, 1) };

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
                foreach (var d in s_Dirs)
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
            return outsideAir.Contains((x - 1, y)) || outsideAir.Contains((x + 1, y)) ||
                   outsideAir.Contains((x, y - 1)) || outsideAir.Contains((x, y + 1));
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
