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

        public (int, int) ToGrid(Vector3 worldPos)
        {
            Vector3 diff = worldPos - Origin;
            float gx = Vector3.Dot(diff, StepX) / Mathf.Max(1e-6f, StepX.sqrMagnitude);
            float gy = Vector3.Dot(diff, StepY) / Mathf.Max(1e-6f, StepY.sqrMagnitude);
            return (Mathf.RoundToInt(gx), Mathf.RoundToInt(gy));
        }

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

            // 4. Her iki kolu çıkış koridoruna doğru pürüzsüz akış sırasına göre diz
            BuildArmChain(leftList, shoreTarget, outsideAir, depthMap, true, leftArm);
            BuildArmChain(rightList, shoreTarget, outsideAir, depthMap, false, rightArm);
        }

        private static void BuildArmChain(List<PixelCube> cubes, Vector3 shoreTarget, HashSet<(int, int)> outsideAir,
                                          Dictionary<PixelCube, int> depthMap, bool isLeftArm, List<PixelCube> arm)
        {
            arm.Clear();
            if (cubes == null || cubes.Count == 0) return;

            // Küpleri çıkış koridoruna (aşağı ve dış kanada) doğru pürüzsüz akış sırasına göre diz.
            // arm[0] = çıkışa en yakın baş küp; arm[N-1] = kuyruktaki en son küp.
            // Bu sıralama tek yönlü (monotonik) olduğu için ASLA girdap (spiral/vorteks) veya zikzak oluşturmaz.
            cubes.Sort((a, b) =>
            {
                // 1. Y ekseni (Aşağıya/çıkışa yakınlık): Aşağıdaki küpler çıkışa daha yakındır, önden yürür.
                float ya = a.transform.position.y;
                float yb = b.transform.position.y;
                if (Mathf.Abs(ya - yb) > 0.04f)
                {
                    return ya.CompareTo(yb);
                }

                // 2. X ekseni (Dış kanada yakınlık):
                // Sol kol için en soldaki (dış şeride en yakın) küp öne geçer.
                // Sağ kol için en sağdaki (dış şeride en yakın) küp öne geçer.
                float xa = a.transform.position.x;
                float xb = b.transform.position.x;
                if (isLeftArm)
                    return xa.CompareTo(xb);
                else
                    return xb.CompareTo(xa);
            });

            arm.AddRange(cubes);
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
        /// Kolun yolunu ve kuyruk slotlarını kurar:
        /// Küpler panonun dış kanadındaki (flank) temiz koridorda tek sıra ("ipe dizilmiş boncuklar")
        /// halinde dizilir ve buradan kıyıya/gemiye doğru kesintisiz, pürüzsüzce akar.
        /// Asla resmin içinde girdap veya zikzak oluşturmaz.
        /// </summary>
        public static CargoRope Build(List<PixelCube> arm, CubeGridFrame frame, Vector3 shoreCenter,
                                      Vector3 shoreLaneOffset, bool entersLeft,
                                      float boardMinX, float boardMaxX, float boardBottomY, float boardTopY)
        {
            if (arm == null || arm.Count == 0) return null;

            const int samplesPerSegment = 12;
            float pitch = frame.Pitch > 0.001f ? frame.Pitch : 0.2605f;
            float flankMargin = pitch * 0.90f;
            float flankX = entersLeft ? (boardMinX - flankMargin) : (boardMaxX + flankMargin);
            float bottomY = boardBottomY - pitch * 0.5f;
            float z = arm[0].transform.position.z;

            // Slot 0 (baş küp) konumu:
            float minSlot0Y = boardBottomY + pitch * 0.35f;
            float maxSlot0Y = boardTopY + pitch * 0.5f - (arm.Count - 1) * pitch;
            float lowestCubeY = arm[0].transform.position.y;
            float slot0Y = Mathf.Clamp(lowestCubeY, minSlot0Y, Mathf.Max(minSlot0Y, maxSlot0Y));

            Vector3 exitPoint = new Vector3(flankX, bottomY, z);
            IList<Vector3> approach = ShipDispatcher.BuildApproach(exitPoint, shoreCenter, shoreLaneOffset);

            var waypoints = new List<Vector3>(arm.Count + approach.Count + 4);

            // 1. Lead-in: kuyruktaki en üst küpün biraz yukarısında başlar
            Vector3 leadIn = new Vector3(flankX, slot0Y + arm.Count * pitch, z);
            waypoints.Add(leadIn);

            // 2. Kuyruktan başa (k = N-1 down to 0) kuyruk slotları:
            // Yol boyunca ilerledikçe gemiye doğru mesafe artar
            for (int k = arm.Count - 1; k >= 0; k--)
            {
                waypoints.Add(new Vector3(flankX, slot0Y + k * pitch, z));
            }

            // 3. Pano alt çıkış noktası
            waypoints.Add(exitPoint);
            int boardExitIndex = waypoints.Count - 1;

            // 4. Sahil ve gemi giriş rotası
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
                int waypointIdx = arm.Count - k;
                float d = rope.Path.DistanceAtIndex(waypointIdx * samplesPerSegment);
                rope.StartDistances[k] = Mathf.Max(0.01f, d);
            }

            rope.BoardExitDistance = rope.Path.DistanceAtIndex(boardExitIndex * samplesPerSegment);
            return rope;
        }

        /// <summary>Yürüme şeridinin pano kenarından dışarı uzaklığı (ızgara adımı cinsinden).</summary>
        public const float LaneOutwardPitch = 0.65f;

        /// <summary>
        /// Küpün dış şerit ofseti: Her zaman kendi kolunun dış kanadına (sol kol sola, sağ kol sağa)
        /// ve çıkış yönüne (aşağıya) doğru kaydırılır. Asla içeriye veya yukarıya doğru sapmaz.
        /// </summary>
        private static Vector3 OutwardOffset(PixelCube cube, HashSet<(int, int)> outsideAir, CubeGridFrame frame, bool entersLeft)
        {
            float sideSign = entersLeft ? -1f : 1f;
            Vector3 sideDir = frame.StepX.normalized * sideSign;
            Vector3 downDir = -frame.StepY.normalized;

            int x = cube.GridX, y = cube.GridY;
            bool airSide = outsideAir != null && outsideAir.Contains((entersLeft ? x - 1 : x + 1, y));
            bool airDown = outsideAir != null && outsideAir.Contains((x, y - 1));

            float sideMag = airSide ? 0.70f : 0.40f;
            float downMag = airDown ? 0.25f : 0.0f;

            return (sideDir * sideMag + downDir * downMag) * (frame.Pitch * LaneOutwardPitch);
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
