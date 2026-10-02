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
        /// İki kolu sıralar (her kol baştan kuyruğa). Toplam küp sayısı <paramref name="maxCount"/>'u aşmaz.
        /// </summary>
        public static void BuildArms(List<PixelCube> exposed, HashSet<(int, int)> outsideAir,
                                     CubeGridFrame frame, Vector3 shoreTarget, int maxCount,
                                     List<PixelCube> leftArm, List<PixelCube> rightArm)
        {
            leftArm.Clear();
            rightArm.Clear();
            if (exposed == null || exposed.Count == 0 || maxCount <= 0) return;

            var pool = new Dictionary<(int, int), PixelCube>(exposed.Count);
            foreach (var c in exposed) pool[(c.GridX, c.GridY)] = c;

            // Başlangıç: gemiye (kıyıya) en yakın kenar küpü
            PixelCube start = null;
            float bestSq = float.MaxValue;
            foreach (var c in exposed)
            {
                Vector3 d = c.transform.position - shoreTarget;
                d.z = 0f;
                float sq = d.sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; start = c; }
            }

            var visited = new HashSet<(int, int)> { (start.GridX, start.GridY) };
            leftArm.Add(start);
            int total = 1;

            (int, int) dirL = (-1, 0);
            (int, int) dirR = (1, 0);
            bool leftStuck = false, rightStuck = false;

            // Kolları sırayla birer küp uzat ki iki taraf dengeli dolsun
            while (total < maxCount && (!leftStuck || !rightStuck))
            {
                if (!leftStuck)
                {
                    if (TryGrow(leftArm, start, ref dirL, pool, visited, outsideAir)) total++;
                    else leftStuck = true;
                }
                if (total >= maxCount) break;
                if (!rightStuck)
                {
                    if (TryGrow(rightArm, start, ref dirR, pool, visited, outsideAir)) total++;
                    else rightStuck = true;
                }
            }
        }

        /// <summary>
        /// Kolun ucuna kenarı takip eden bir sonraki komşu küpü ekler. Tercih sırası:
        /// kenar komşusu (köşegen değil) → dış havaya daha çok değen (daha dışta) → aynı yönde devam.
        /// </summary>
        private static bool TryGrow(List<PixelCube> arm, PixelCube start, ref (int, int) dir,
                                    Dictionary<(int, int), PixelCube> pool,
                                    HashSet<(int, int)> visited, HashSet<(int, int)> outsideAir)
        {
            PixelCube tip = arm.Count > 0 ? arm[arm.Count - 1] : start;
            int tx = tip.GridX, ty = tip.GridY;

            PixelCube best = null;
            (int, int) bestDir = dir;
            float bestScore = float.MinValue;

            foreach (var (dx, dy) in s_Neighbors8)
            {
                var cell = (tx + dx, ty + dy);
                if (visited.Contains(cell) || !pool.TryGetValue(cell, out PixelCube cand)) continue;

                bool diagonal = dx != 0 && dy != 0;
                float score = diagonal ? 0f : 4f;

                int airCount = 0;
                foreach (var (ax, ay) in s_Neighbors4)
                {
                    if (outsideAir.Contains((cell.Item1 + ax, cell.Item2 + ay))) airCount++;
                }
                score += airCount * 0.75f;

                float dot = dx * dir.Item1 + dy * dir.Item2;
                score += dot * 0.5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = cand;
                    bestDir = (dx, dy);
                }
            }

            if (best == null) return false;

            visited.Add((best.GridX, best.GridY));
            arm.Add(best);
            dir = bestDir;
            return true;
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
                                      float exitShift, CubeGridFrame frame, IList<Vector3> approach, bool entersLeft)
        {
            const int samplesPerSegment = 12;

            var waypoints = new List<Vector3>(arm.Count + exitCells.Count + approach.Count);
            for (int i = arm.Count - 1; i >= 0; i--)
            {
                waypoints.Add(arm[i].transform.position);
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
                int waypointIndex = arm.Count - 1 - k;
                rope.StartDistances[k] = rope.Path.DistanceAtIndex(waypointIndex * samplesPerSegment);
            }
            rope.BoardExitDistance = rope.Path.DistanceAtIndex(boardExitIndex * samplesPerSegment);
            return rope;
        }
    }
}
