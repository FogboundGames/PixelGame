using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Panodan gemiye "ipe dizilmiş boncuklar" gibi akan küp kolu.
    ///
    /// Referans oyunda gemi yanaşınca rengine uyan dış kenar küpleri tek tek değil,
    /// kenar boyunca yan yana dizilmiş bir tren olarak çekilir: hepsi aynı anda ve aynı
    /// hızda kayar, aralarında hep bir küp boyu kalır. Böylece kimse kimseyi sollayamaz,
    /// iç içe geçme ya da kıyıda yığılma olmaz.
    ///
    /// Yol: kuyruk küpü → ... → baş küp (hepsi küplerin kendi hücreleri) → dış havadaki
    /// boş hücrelerden panonun altına iniş → kıyı. Küpler boşalttıkları hücrelerin
    /// içinden geçtiği için resmin üstünden hiç geçmez.
    /// </summary>
    public sealed class CargoRope
    {
        /// <summary>Baştan kuyruğa küpler (0 = gemiye en yakın).</summary>
        public readonly List<PixelCube> Cubes = new List<PixelCube>();
        /// <summary>Her küpün yol başından uzaklığı (Cubes ile aynı sıra).</summary>
        public float[] StartDistances;
        public ShoreLanePath Path;
        /// <summary>Kol geminin sol tarafına mı giriyor?</summary>
        public bool EntersLeft;
        /// <summary>Yolun panodan çıktığı nokta (alt sınır hücresi) — kuyruk bunu geçince pano temizdir.</summary>
        public float BoardExitDistance;
        /// <summary>Yol kurulurken geminin kıyı noktası; gemi sonradan kayarsa fark buna göre hesaplanır.</summary>
        public Vector3 ShoreAtLaunch;

        /// <summary>
        /// Yolda <paramref name="distance"/> noktasının, kıyı <paramref name="shoreShift"/> kadar kaydığında
        /// alacağı konum. Pano içindeki kısım hiç değişmez; panodan çıkıştan kıyıya doğru kayma
        /// yumuşakça 0'dan tama çıkar, böylece yol gemiye doğru bükülür.
        /// </summary>
        public Vector3 PointAt(float distance, Vector3 shoreShift)
        {
            Vector3 p = Path.PointAtDistance(distance);
            if (shoreShift.sqrMagnitude < 1e-10f) return p;
            float span = Mathf.Max(1e-4f, Path.Length - BoardExitDistance);
            float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((distance - BoardExitDistance) / span));
            return p + shoreShift * w;
        }

        public float HeadDistance => StartDistances[0];
        public float TailDistance => StartDistances[StartDistances.Length - 1];
    }

    /// <summary>
    /// Izgara koordinatlarını dünya konumuna çevirir. Küplerin kendi konumlarından
    /// (sağ ve üst komşu farkları) çıkarılır; böylece generator ayarları ne olursa olsun
    /// boş hücrelerin dünya konumu da küplerle birebir hizalı olur.
    /// </summary>
    public struct CubeGridFrame
    {
        public Vector3 Origin;
        public Vector3 StepX;
        public Vector3 StepY;

        public Vector3 ToWorld(float gx, float gy) => Origin + StepX * gx + StepY * gy;
        public float Pitch => StepX.magnitude;

        public static bool TryBuild(PixelCube[] allCubes, out CubeGridFrame frame)
        {
            frame = default;
            if (allCubes == null || allCubes.Length == 0) return false;

            var byCell = new Dictionary<(int, int), PixelCube>(allCubes.Length);
            foreach (var c in allCubes)
            {
                if (c != null) byCell[(c.GridX, c.GridY)] = c;
            }

            bool hasX = false, hasY = false;
            Vector3 stepX = Vector3.zero, stepY = Vector3.zero;
            PixelCube anchor = null;
            foreach (var kvp in byCell)
            {
                PixelCube c = kvp.Value;
                if (!hasX && byCell.TryGetValue((c.GridX + 1, c.GridY), out PixelCube right))
                {
                    stepX = right.transform.position - c.transform.position;
                    hasX = true;
                    anchor = c;
                }
                if (!hasY && byCell.TryGetValue((c.GridX, c.GridY + 1), out PixelCube up))
                {
                    stepY = up.transform.position - c.transform.position;
                    hasY = true;
                }
                if (hasX && hasY) break;
            }

            if (!hasX || !hasY || anchor == null) return false;

            frame.StepX = stepX;
            frame.StepY = stepY;
            frame.Origin = anchor.transform.position - stepX * anchor.GridX - stepY * anchor.GridY;
            return true;
        }
    }

    /// <summary>
    /// Dış kenardaki küplerden iki kollu ip kurar: alt ortadaki (gemiye en yakın) küpten
    /// başlayıp bir kol sola, bir kol sağa doğru kenarı takip eder.
    /// </summary>
    public static class CargoRopeBuilder
    {
        private static readonly (int, int)[] s_Neighbors4 = { (-1, 0), (1, 0), (0, -1), (0, 1) };
        // Aşağı yön önce: eşit uzunluktaki yollardan aşağı inen seçilsin
        private static readonly (int, int)[] s_ExitOrder = { (0, -1), (-1, 0), (1, 0), (0, 1) };
        private static readonly (int, int)[] s_Neighbors8 =
        {
            (-1, 0), (1, 0), (0, -1), (0, 1),
            (-1, -1), (1, -1), (-1, 1), (1, 1)
        };

        /// <summary>
        /// Gemi slotuna oturduğu anda üzerinde yazan sayı kadar (veya tahtada kalan tüm eşleşen)
        /// küpü dıştan içe doğru katman sırasıyla seçer ve iki kola kesintisiz yılan (snake) zinciri olarak paylaştırır.
        /// </summary>
        public static void BuildArms(List<PixelCube> matchingCubes, HashSet<(int, int)> outsideAir,
                                     CubeGridFrame frame, Vector3 shoreTarget, int maxCount,
                                     List<PixelCube> leftArm, List<PixelCube> rightArm)
        {
            leftArm.Clear();
            rightArm.Clear();
            if (matchingCubes == null || matchingCubes.Count == 0 || maxCount <= 0) return;

            int targetCount = Mathf.Min(maxCount, matchingCubes.Count);

            // 1. Dış havaya olan katman derinliğini (peel depth) BFS ile hesapla
            var depthMap = new Dictionary<PixelCube, int>(matchingCubes.Count);
            var queue = new Queue<PixelCube>();
            var inQueue = new HashSet<PixelCube>();

            // Katman 0: Dış havaya doğrudan 4 yönde temas eden küpler
            foreach (var cube in matchingCubes)
            {
                int x = cube.GridX, y = cube.GridY;
                if (outsideAir.Contains((x - 1, y)) || outsideAir.Contains((x + 1, y)) ||
                    outsideAir.Contains((x, y - 1)) || outsideAir.Contains((x, y + 1)))
                {
                    depthMap[cube] = 0;
                    queue.Enqueue(cube);
                    inQueue.Add(cube);
                }
            }

            // Eğer 4 yönde temas eden yoksa 8 yönde temas edenleri dene
            if (inQueue.Count == 0)
            {
                foreach (var cube in matchingCubes)
                {
                    int x = cube.GridX, y = cube.GridY;
                    foreach (var (dx, dy) in s_Neighbors8)
                    {
                        if (outsideAir.Contains((x + dx, y + dy)))
                        {
                            depthMap[cube] = 0;
                            queue.Enqueue(cube);
                            inQueue.Add(cube);
                            break;
                        }
                    }
                }
            }

            if (inQueue.Count == 0) return;

            // BFS ile derinlikleri yay
            var cubeByCoord = new Dictionary<(int, int), PixelCube>(matchingCubes.Count);
            foreach (var c in matchingCubes) cubeByCoord[(c.GridX, c.GridY)] = c;

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                int curDepth = depthMap[cur];
                int cx = cur.GridX, cy = cur.GridY;

                foreach (var (dx, dy) in s_Neighbors4)
                {
                    var nCoord = (cx + dx, cy + dy);
                    if (cubeByCoord.TryGetValue(nCoord, out PixelCube neighbor) && !inQueue.Contains(neighbor))
                    {
                        depthMap[neighbor] = curDepth + 1;
                        queue.Enqueue(neighbor);
                        inQueue.Add(neighbor);
                    }
                }
            }

            foreach (var cube in matchingCubes)
            {
                if (!depthMap.ContainsKey(cube)) depthMap[cube] = 99;
            }

            // 2. Dış katmandan içe ve çıkışa/aşağıya yakın olana göre sırala
            var sortedCandidates = new List<PixelCube>(matchingCubes);
            sortedCandidates.Sort((a, b) =>
            {
                int da = depthMap[a], db = depthMap[b];
                if (da != db) return da.CompareTo(db);

                float distA = (a.transform.position - shoreTarget).sqrMagnitude;
                float distB = (b.transform.position - shoreTarget).sqrMagnitude;
                return distA.CompareTo(distB);
            });

            var selected = new List<PixelCube>(targetCount);
            for (int i = 0; i < targetCount; i++)
            {
                selected.Add(sortedCandidates[i]);
            }

            // 3. Seçilen küpleri Sol ve Sağ kollara paylaştır
            float midX = 0f;
            foreach (var c in selected) midX += c.GridX;
            midX /= selected.Count;

            var leftList = new List<PixelCube>();
            var rightList = new List<PixelCube>();

            foreach (var c in selected)
            {
                if (c.GridX <= midX) leftList.Add(c);
                else rightList.Add(c);
            }

            // Tek tarafta hiç küp kalmadıysa yarı yarıya böl
            if (leftList.Count == 0 && rightList.Count > 1)
            {
                int half = rightList.Count / 2;
                leftList.AddRange(rightList.GetRange(0, half));
                rightList.RemoveRange(0, half);
            }
            else if (rightList.Count == 0 && leftList.Count > 1)
            {
                int half = leftList.Count / 2;
                rightList.AddRange(leftList.GetRange(0, half));
                leftList.RemoveRange(0, half);
            }

            // 4. Her iki kolu başından kuyruğuna kesintisiz yılan (snake) zinciri olarak diz
            BuildArmChain(leftList, shoreTarget, outsideAir, leftArm);
            BuildArmChain(rightList, shoreTarget, outsideAir, rightArm);
        }

        private static void BuildArmChain(List<PixelCube> cubes, Vector3 shoreTarget, HashSet<(int, int)> outsideAir, List<PixelCube> arm)
        {
            arm.Clear();
            if (cubes == null || cubes.Count == 0) return;

            // arm[0] (HEAD) seçimi: Dış havaya temas eden küpler arasından shoreTarget'a en yakın olan
            PixelCube head = null;
            float bestHeadDist = float.MaxValue;
            foreach (var c in cubes)
            {
                int x = c.GridX, y = c.GridY;
                bool touchesAir = outsideAir.Contains((x - 1, y)) || outsideAir.Contains((x + 1, y)) ||
                                  outsideAir.Contains((x, y - 1)) || outsideAir.Contains((x, y + 1));
                float d = (c.transform.position - shoreTarget).sqrMagnitude;
                if (touchesAir) d -= 1000f;

                if (d < bestHeadDist)
                {
                    bestHeadDist = d;
                    head = c;
                }
            }

            if (head == null) head = cubes[0];

            arm.Add(head);
            var remaining = new HashSet<PixelCube>(cubes);
            remaining.Remove(head);

            // Kuyruk oluşturma: Zincirdeki son küpe en yakın komşu küpü ekleyerek kesintisiz akış kur
            while (remaining.Count > 0)
            {
                PixelCube tip = arm[arm.Count - 1];
                int tx = tip.GridX, ty = tip.GridY;

                PixelCube bestNext = null;
                float bestDist = float.MaxValue;

                foreach (var cand in remaining)
                {
                    int dx = Mathf.Abs(cand.GridX - tx);
                    int dy = Mathf.Abs(cand.GridY - ty);

                    float dist;
                    if (dx + dy == 1) dist = 1.0f; // 4-komşu
                    else if (dx == 1 && dy == 1) dist = 1.414f; // 8-komşu
                    else dist = Mathf.Sqrt(dx * dx + dy * dy) + 5.0f;

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestNext = cand;
                    }
                }

                if (bestNext == null) break;

                arm.Add(bestNext);
                remaining.Remove(bestNext);
            }
        }

        /// <summary>
        /// Baş küpün hücresinden dış havadaki boş hücreler üzerinden panonun alt sınırına
        /// en kısa yolu bulur (resmin içinden geçmez). Dönen liste baş küpün komşusundan
        /// alt sınır hücresine kadardır; yol bulunamazsa boş döner.
        /// </summary>
        public static List<(int, int)> FindExitRoute((int, int) head, HashSet<(int, int)> outsideAir, int bottomRowY)
        {
            var result = new List<(int, int)>();
            var cameFrom = new Dictionary<(int, int), (int, int)>();
            var queue = new Queue<(int, int)>();

            foreach (var (dx, dy) in s_Neighbors4)
            {
                var n = (head.Item1 + dx, head.Item2 + dy);
                if (outsideAir.Contains(n) && !cameFrom.ContainsKey(n))
                {
                    cameFrom[n] = head;
                    queue.Enqueue(n);
                }
            }

            if (queue.Count == 0)
            {
                foreach (var (dx, dy) in s_Neighbors8)
                {
                    var n = (head.Item1 + dx, head.Item2 + dy);
                    if (outsideAir.Contains(n) && !cameFrom.ContainsKey(n))
                    {
                        cameFrom[n] = head;
                        queue.Enqueue(n);
                    }
                }
            }

            (int, int)? goal = null;
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur.Item2 <= bottomRowY) { goal = cur; break; }

                foreach (var (dx, dy) in s_ExitOrder)
                {
                    var n = (cur.Item1 + dx, cur.Item2 + dy);
                    if (!outsideAir.Contains(n) || cameFrom.ContainsKey(n)) continue;
                    cameFrom[n] = cur;
                    queue.Enqueue(n);
                }
            }

            if (!goal.HasValue) return result;

            var step = goal.Value;
            while (step != head)
            {
                result.Add(step);
                step = cameFrom[step];
            }
            result.Reverse();
            return result;
        }

        /// <summary>
        /// Hücre merdivenini sadeleştirir: aradaki hücrelerin hepsi boşsa doğrudan ileriye
        /// atlar (string pulling). Böylece yol zikzak yerine düz/yumuşak olur.
        /// </summary>
        public static List<(int, int)> SimplifyRoute((int, int) from, List<(int, int)> route, HashSet<(int, int)> outsideAir)
        {
            var simplified = new List<(int, int)>();
            if (route.Count == 0) return simplified;

            var anchor = from;
            int i = 0;
            while (i < route.Count)
            {
                int farthest = i;
                for (int j = route.Count - 1; j > i; j--)
                {
                    if (HasClearLine(anchor, route[j], outsideAir, from)) { farthest = j; break; }
                }
                simplified.Add(route[farthest]);
                anchor = route[farthest];
                i = farthest + 1;
            }
            return simplified;
        }

        private static bool HasClearLine((int, int) a, (int, int) b, HashSet<(int, int)> outsideAir, (int, int) allowed)
        {
            float dx = b.Item1 - a.Item1;
            float dy = b.Item2 - a.Item2;
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 4f);
            for (int s = 1; s < steps; s++)
            {
                float t = s / (float)steps;
                var cell = (Mathf.RoundToInt(a.Item1 + dx * t), Mathf.RoundToInt(a.Item2 + dy * t));
                if (cell != allowed && !outsideAir.Contains(cell)) return false;
            }
            return true;
        }

        /// <summary>
        /// Kolun yolunu kurar: kuyruk → ... → baş → dış hava çıkışı → <paramref name="approach"/> (son nokta kıyı).
        /// <paramref name="exitShift"/> iki kolun ortak çıkış hücrelerinde yan yana
        /// (iç içe değil) akması için dünya biriminde yatay kaydırmadır.
        /// </summary>
        public static CargoRope Build(List<PixelCube> arm, List<(int, int)> exitCells, HashSet<(int, int)> sharedCells,
                                      float exitShift, CubeGridFrame frame, IList<Vector3> approach, bool entersLeft,
                                      HashSet<(int, int)> walkable = null)
        {
            const int samplesPerSegment = 12;

            // Kolun kendi hücreleri de yürünebilir: küpler boşalttıkları hücrelerin içinden geçer
            HashSet<(int, int)> armCells = null;
            if (walkable != null)
            {
                armCells = new HashSet<(int, int)>(walkable);
                foreach (var c in arm) armCells.Add((c.GridX, c.GridY));
            }

            var waypoints = new List<Vector3>(arm.Count + exitCells.Count + approach.Count);
            var cubeWaypointIndex = new int[arm.Count];
            for (int i = arm.Count - 1; i >= 0; i--)
            {
                // Zincirde komşu olmayan iki küp arasına düz çizgi çekmek yolu resmin üstünden
                // geçiriyordu (küpler başka küplerin içinden yürüyordu). Aradaki boş hücrelerden
                // gerçek bir zemin yolu bulunur ve ara nokta olarak eklenir.
                if (armCells != null && i < arm.Count - 1)
                {
                    var from = (arm[i + 1].GridX, arm[i + 1].GridY);
                    var to = (arm[i].GridX, arm[i].GridY);
                    if (Mathf.Abs(from.Item1 - to.Item1) > 1 || Mathf.Abs(from.Item2 - to.Item2) > 1)
                    {
                        foreach (var cell in FindGroundLink(from, to, armCells))
                        {
                            waypoints.Add(frame.ToWorld(cell.Item1, cell.Item2));
                        }
                    }
                }
                cubeWaypointIndex[i] = waypoints.Count;
                // Referans oyundaki gibi tren, küplerin kendi hücreleri üzerinde değil panonun hemen
                // DIŞINDAKİ ayrı bir şeritte yürür (yoksa her küp öndekinin boşalttığı yere kaydığı için
                // kenar "yerinde sıra sıra kayıyor" gibi görünüyordu). Küp, açık (hava) tarafa kaydırılır.
                waypoints.Add(arm[i].transform.position + OutwardOffset(arm[i], walkable, frame));
            }

            Vector3 shift = frame.StepX.normalized * exitShift;
            foreach (var cell in exitCells)
            {
                Vector3 p = frame.ToWorld(cell.Item1, cell.Item2);
                if (sharedCells != null && sharedCells.Contains(cell)) p += shift;
                waypoints.Add(p);
            }
            int boardExitIndex = waypoints.Count - 1;

            waypoints.AddRange(approach);

            var rope = new CargoRope
            {
                Path = ShoreLanePath.BuildThrough(waypoints, samplesPerSegment),
                EntersLeft = entersLeft,
                StartDistances = new float[arm.Count]
            };

            for (int k = 0; k < arm.Count; k++)
            {
                rope.Cubes.Add(arm[k]);
                rope.StartDistances[k] = rope.Path.DistanceAtIndex(cubeWaypointIndex[k] * samplesPerSegment);
            }
            rope.BoardExitDistance = rope.Path.DistanceAtIndex(boardExitIndex * samplesPerSegment);
            return rope;
        }

        /// <summary>Yürüme şeridinin pano kenarından dışarı uzaklığı (ızgara adımı cinsinden).</summary>
        public const float LaneOutwardPitch = 0.8f;

        /// <summary>
        /// Küpün açık havaya bakan yönü (dört komşudan boş olanların toplamı) × şerit mesafesi.
        /// Köşe küplerinde iki yön toplanır, şerit köşeyi çapraz döner. Açık komşusu yoksa kaydırma yok.
        /// </summary>
        private static Vector3 OutwardOffset(PixelCube cube, HashSet<(int, int)> outsideAir, CubeGridFrame frame)
        {
            if (outsideAir == null) return Vector3.zero;
            int x = cube.GridX, y = cube.GridY;
            Vector2 dir = Vector2.zero;
            if (outsideAir.Contains((x - 1, y))) dir.x -= 1f;
            if (outsideAir.Contains((x + 1, y))) dir.x += 1f;
            if (outsideAir.Contains((x, y - 1))) dir.y -= 1f;
            if (outsideAir.Contains((x, y + 1))) dir.y += 1f;
            if (dir.sqrMagnitude < 1e-4f) return Vector3.zero;
            dir.Normalize();
            Vector3 world = frame.StepX.normalized * dir.x + frame.StepY.normalized * dir.y;
            return world * (frame.Pitch * LaneOutwardPitch);
        }

        /// <summary>
        /// İki hücre arasında yalnızca yürünebilir (boş/kolun kendi) hücrelerden geçen en kısa yol.
        /// Çapraz adım ancak iki yan hücre de boşsa atılır (köşeden küp kesmesin).
        /// Dönen liste uç hücreleri içermez; yol yoksa boş döner (eski düz bağlantı kullanılır).
        /// </summary>
        private static List<(int, int)> FindGroundLink((int, int) from, (int, int) to, HashSet<(int, int)> walkable)
        {
            var result = new List<(int, int)>();
            var cameFrom = new Dictionary<(int, int), (int, int)> { [from] = from };
            var queue = new Queue<(int, int)>();
            queue.Enqueue(from);
            const int maxVisited = 4096;

            bool found = false;
            while (queue.Count > 0 && cameFrom.Count < maxVisited)
            {
                var cur = queue.Dequeue();
                if (cur == to) { found = true; break; }

                foreach (var (dx, dy) in s_Neighbors8)
                {
                    var n = (cur.Item1 + dx, cur.Item2 + dy);
                    if (cameFrom.ContainsKey(n)) continue;
                    if (n != to && !walkable.Contains(n)) continue;
                    if (dx != 0 && dy != 0 &&
                        (!walkable.Contains((cur.Item1 + dx, cur.Item2)) || !walkable.Contains((cur.Item1, cur.Item2 + dy))))
                        continue;
                    cameFrom[n] = cur;
                    queue.Enqueue(n);
                }
            }

            if (!found) return result;

            var step = cameFrom[to];
            while (step != from)
            {
                result.Add(step);
                step = cameFrom[step];
            }
            result.Reverse();

            // Merdiven basamaklarını sadeleştir: düz giden ardışık hücrelerin yalnızca dönüş noktaları kalsın
            if (result.Count > 2)
            {
                var simplified = new List<(int, int)>();
                var prev = from;
                for (int i = 0; i < result.Count; i++)
                {
                    var next = i + 1 < result.Count ? result[i + 1] : to;
                    var d1 = (result[i].Item1 - prev.Item1, result[i].Item2 - prev.Item2);
                    var d2 = (next.Item1 - result[i].Item1, next.Item2 - result[i].Item2);
                    if (d1 != d2) simplified.Add(result[i]);
                    prev = result[i];
                }
                result = simplified;
            }
            return result;
        }
    }
}
