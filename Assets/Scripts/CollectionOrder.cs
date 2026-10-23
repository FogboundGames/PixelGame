using System;
using System.Collections.Generic;

namespace PixelGame
{
    /// <summary>
    /// Küp toplama sırası: "Her zaman en dıştan en içe doğru, en açık ve en yakın olanlar".
    /// 
    /// 1. Topolojik Katmanlama (Topological Peeling Layers):
    ///    - Katman 0: Modelin en dış silüet çeperindeki küpler (dış havaya ilk temas edenler).
    ///    - Katman 1: Katman 0 soyulduktan sonra açılan iç çeper.
    ///    - Katman 2: Daha içteki çekirdek küpler...
    ///    Gemiciler bu renkte Katman 0'da küp varken KESİNLİKLE Katman 1 veya daha içteki küplere gitmez.
    ///    Böylece asla modelin içine tünel kazıp dıştakileri arkada bırakmazlar; her zaman en dıştan en içe toplanır.
    ///
    /// 2. Açıklık Derecesi (Exposure / Openness):
    ///    - 4 yönlü kardinal ve 8 yönlü diyagonal dış hava komşulukları hesaplanır.
    ///    - Köşeler, uçlar ve dışa doğru çıkıntılı olan (dış havaya en çok yüzü bakan) küpler önceliklidir.
    ///
    /// 3. En Yakınlık (Proximity):
    ///    - Referans hücresi (denizcinin o anki konumu) verildiğinde, en dış katmandaki küpler arasından
    ///      denizciye en yakın olanı seçilir.
    ///    - Başlangıçta sahile/iskeleye (alt kenara) en yakın dış küpler önce alınır.
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
        /// Sıradaki toplanabilir küpleri "her zaman en dıştan en içe doğru, en açık ve en yakın" kuralıyla döner.
        /// <paramref name="occupied"/>: tahtada fiziksel olarak duran tüm hücreler.
        /// <paramref name="isCandidate"/>: bu hücre aranan renkte mi ve henüz kimseye ayrılmamış mı.
        /// <paramref name="refCell"/>: isteğe bağlı referans hücresi (ör. denizcinin bulunduğu hücre).
        /// </summary>
        public static List<(int, int)> GetNextCollectibleCells(
            ICollection<(int, int)> occupied, 
            Func<(int, int), bool> isCandidate,
            (int, int)? refCell = null)
        {
            var result = new List<(int, int)>();
            if (occupied == null || occupied.Count == 0) return result;

            var air = ComputeOutsideAir(occupied);
            var layers = GetOrComputePeelingLayers(occupied);

            // 1. Dış havaya erişimi olan ve aday şartını sağlayan tüm küpleri topla
            var candidates = new List<(int, int)>();
            int minLayer = int.MaxValue;

            foreach (var c in occupied)
            {
                if (!isCandidate(c)) continue;
                if (!IsAccessible(air, c.Item1, c.Item2)) continue;

                int layer = layers.TryGetValue(c, out int lyr) ? lyr : 0;
                if (layer < minLayer) minLayer = layer;
                candidates.Add(c);
            }

            if (candidates.Count == 0) return result;

            // 2. KULLANICI KURALI: "Her zaman en dıştan en içe doğru toplayacaklar"
            // Sadece mevcut en dış katmanda (minLayer) olan adayları filtrele.
            // Dıştakiler bitmeden iç katmandakiler ASLA listeye dahil edilmez!
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                int layer = layers.TryGetValue(c, out int lyr) ? lyr : 0;
                if (layer == minLayer)
                {
                    result.Add(c);
                }
            }

            // 3. Sıralama: En yakın ve en açık olanlar
            result.Sort((a, b) =>
            {
                // a) Referans hücreye (denizcinin konumuna) en yakın olan önce
                if (refCell.HasValue)
                {
                    int distA = Math.Abs(a.Item1 - refCell.Value.Item1) + Math.Abs(a.Item2 - refCell.Value.Item2);
                    int distB = Math.Abs(b.Item1 - refCell.Value.Item1) + Math.Abs(b.Item2 - refCell.Value.Item2);
                    if (distA != distB) return distA.CompareTo(distB);
                }

                // b) Açıklık derecesi: Dış havaya en çok yüzü açık olan (köşeler/çıkıntılar) önce
                int openA = GetOpenness(air, a.Item1, a.Item2);
                int openB = GetOpenness(air, b.Item1, b.Item2);
                if (openA != openB) return openB.CompareTo(openA); // büyük olan önce

                // c) İskele/kıyı önceliği (y değeri küçük olan sahile ve gemilere daha yakındır)
                if (a.Item2 != b.Item2) return a.Item2.CompareTo(b.Item2);

                // d) Belirlilik (soldan sağa)
                return a.Item1.CompareTo(b.Item1);
            });

            return result;
        }
    }
}
