using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Level'ın gemi sırasını (WagonSequence) oyunun kurallarıyla oynatan simülatör.
    /// docs/level-tasarim/tools/sim.cpp'nin C# karşılığıdır; kurallar orada ve REHBER.md §1'de:
    ///  - i. gemi i % sütun şeridine gider, her şeridin ilk min(satır, 3) gemisi görünür
    ///  - yalnızca şeridin en öndeki gemisi gönderilir; halatlı çift birlikte gider ve 2 boş slot ister,
    ///    partner görünür olmalı ve önünde kendi grubundan başka gemi olmamalı
    ///  - yanaşan gemi açıktaki kendi renk küplerini alır: en alt satır önce, sonra satır ucuna en yakın, sol önce
    ///  - Güçlü konteyner iki rengi alır (ihtiyacı büyük olan yarı önce)
    ///  - dolan gemi kalkar; dolan HALATLI gemi partneri de dolana kadar slotu işgal eder
    ///  - gönderilecek gemi yok ve hiçbir gemi toplayamıyorsa kayıp
    /// LevelSolvabilityAnalyzer gemi sırasını okumaz; üretilen kuyruğun çözülebilirliği bununla ölçülür.
    /// </summary>
    public static class LevelQueueSimulator
    {
        public struct Result
        {
            public float randomWinRate;  // rastgele hamle yapan oyuncu
            public float smartWinRate;   // bir hamle ileriye bakan oyuncu
            public int dfs;              // 1 = çözüm var, 0 = yok, -1 = arama bütçesi bitti
            public string error;         // level simüle edilemedi (renk/kapasite uyuşmazlığı vb.)

            public bool Solvable => error == null && (dfs == 1 || smartWinRate > 0f || randomWinRate > 0f);
        }

        private struct Ship { public int color, cap, color2, cap2, partner; }

        private sealed class Board
        {
            public int W, H, cols, rows, slots;
            public int[] grid0;          // y*W+x, y=0 en alt satır
            public Ship[] ships;
        }

        private sealed class State
        {
            public int[] occ;
            public bool[] air;
            public int[] head;
            public int[] slotShip, slotRem, slotRem2;
            public int left;

            public State Clone()
            {
                return new State
                {
                    occ = (int[])occ.Clone(), air = (bool[])air.Clone(), head = (int[])head.Clone(),
                    slotShip = (int[])slotShip.Clone(), slotRem = (int[])slotRem.Clone(), slotRem2 = (int[])slotRem2.Clone(),
                    left = left,
                };
            }
        }

        public static Result Simulate(PixelLevelData level, IList<WagonSequenceEntry> sequence, int runs = 60, int seed = 9, int dfsBudget = 20000)
        {
            var result = new Result { dfs = 0 };
            Board b = BuildBoard(level, sequence, out string error);
            if (b == null)
            {
                result.error = error;
                return result;
            }

            var rng = new System.Random(seed);
            int randomWins = 0, smartWins = 0;
            for (int r = 0; r < runs; r++)
            {
                if (Play(b, rng, false)) randomWins++;
                if (Play(b, rng, true)) smartWins++;
            }
            result.randomWinRate = randomWins / (float)Mathf.Max(1, runs);
            result.smartWinRate = smartWins / (float)Mathf.Max(1, runs);
            result.dfs = smartWins > 0 ? 1 : Dfs(b, dfsBudget);
            return result;
        }

        // ------------------------------------------------------------------ kurulum

        private static Board BuildBoard(PixelLevelData level, IList<WagonSequenceEntry> sequence, out string error)
        {
            error = null;
            var cells = LevelSolvabilityAnalyzer.BuildGrid(level, out int minX, out int maxX, out int minY, out int maxY);
            if (cells == null || cells.Count == 0 || sequence == null || sequence.Count == 0)
            {
                error = "Tahta ya da gemi sırası boş";
                return null;
            }

            var palette = level.ColorPalette;
            int ColorIndex(Color c)
            {
                for (int i = 0; i < palette.Count; i++)
                {
                    if (palette[i] != null && (PaletteColorOverride.ColorsMatch(palette[i].targetColor, c, 0.04f)
                                               || PaletteColorOverride.ColorsMatch(palette[i].originalColor, c, 0.04f))) return i;
                }
                return -1;
            }

            var b = new Board
            {
                W = maxX - minX + 1, H = maxY - minY + 1,
                cols = Mathf.Max(1, level.PoolColumns),
                rows = Mathf.Clamp(level.PoolRows, 1, 3),
                slots = Mathf.Clamp(level.SlotCount, 1, 8),
            };
            b.grid0 = new int[b.W * b.H];
            for (int i = 0; i < b.grid0.Length; i++) b.grid0[i] = -1;
            var cubes = new Dictionary<int, int>();
            foreach (var kv in cells)
            {
                b.grid0[(kv.Key.Item2 - minY) * b.W + (kv.Key.Item1 - minX)] = kv.Value;
                cubes.TryGetValue(kv.Value, out int n);
                cubes[kv.Value] = n + 1;
            }

            b.ships = new Ship[sequence.Count];
            var capacity = new Dictionary<int, int>();
            var linkFirst = new Dictionary<int, int>();
            for (int i = 0; i < sequence.Count; i++)
            {
                var w = sequence[i];
                var s = new Ship { color = ColorIndex(w.wagonColor), cap = Mathf.Max(0, w.capacity), color2 = -1, cap2 = 0, partner = -1 };
                if (s.color < 0)
                {
                    error = $"{i + 1}. geminin rengi palette yok";
                    return null;
                }
                if (w.isDualColor)
                {
                    s.color2 = ColorIndex(w.secondaryColor);
                    s.cap2 = w.secondaryCapacity > 0 ? w.secondaryCapacity : w.capacity;
                    if (s.color2 < 0)
                    {
                        error = $"{i + 1}. Güçlü konteynerin ikinci rengi palette yok";
                        return null;
                    }
                }
                capacity.TryGetValue(s.color, out int c1);
                capacity[s.color] = c1 + s.cap;
                if (s.color2 >= 0)
                {
                    capacity.TryGetValue(s.color2, out int c2);
                    capacity[s.color2] = c2 + s.cap2;
                }
                if (w.linkId > 0)
                {
                    if (linkFirst.TryGetValue(w.linkId, out int other))
                    {
                        s.partner = other;
                        b.ships[other].partner = i;
                    }
                    else linkFirst[w.linkId] = i;
                }
                b.ships[i] = s;
            }

            foreach (var kv in cubes)
            {
                capacity.TryGetValue(kv.Key, out int cap);
                if (cap != kv.Value)
                {
                    error = $"Renk {kv.Key + 1}: {kv.Value} küp, gemi kapasitesi {cap}";
                    return null;
                }
            }
            return b;
        }

        private static State Init(Board b)
        {
            var s = new State
            {
                occ = (int[])b.grid0.Clone(),
                air = new bool[(b.W + 2) * (b.H + 2)],
                head = new int[b.cols],
                slotShip = new int[b.slots], slotRem = new int[b.slots], slotRem2 = new int[b.slots],
            };
            for (int i = 0; i < s.occ.Length; i++) if (s.occ[i] >= 0) s.left++;
            for (int i = 0; i < b.slots; i++) s.slotShip[i] = -1;
            Flood(b, s, 0, 0);
            return s;
        }

        // ------------------------------------------------------------------ tahta

        private static int AirIndex(Board b, int x, int y) => (y + 1) * (b.W + 2) + (x + 1);

        private static void Flood(Board b, State s, int ax, int ay)
        {
            int aw = b.W + 2, ah = b.H + 2, start = ay * aw + ax;
            if (s.air[start]) return;
            var stack = new Stack<int>();
            s.air[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                int c = stack.Pop(), cx = c % aw, cy = c / aw;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= aw || ny >= ah) continue;
                    int n = ny * aw + nx;
                    if (s.air[n]) continue;
                    bool occupied = nx >= 1 && ny >= 1 && nx <= b.W && ny <= b.H && s.occ[(ny - 1) * b.W + (nx - 1)] >= 0;
                    if (occupied) continue;
                    s.air[n] = true;
                    stack.Push(n);
                }
            }
        }

        private static bool Accessible(Board b, State s, int x, int y)
        {
            return s.air[AirIndex(b, x - 1, y)] || s.air[AirIndex(b, x + 1, y)] || s.air[AirIndex(b, x, y - 1)] || s.air[AirIndex(b, x, y + 1)];
        }

        private static void RemoveCube(Board b, State s, int x, int y)
        {
            s.occ[y * b.W + x] = -1;
            s.left--;
            if (Accessible(b, s, x, y)) Flood(b, s, x + 1, y + 1);
        }

        /// <summary>CollectionOrder: en alt satır, sonra satırın uçlarına en yakın, eşitlikte sol, sonra x.</summary>
        private static int Pick(Board b, State s, int color)
        {
            for (int y = 0; y < b.H; y++)
            {
                int mn = int.MaxValue, mx = int.MinValue;
                for (int x = 0; x < b.W; x++) if (s.occ[y * b.W + x] >= 0) { mn = Math.Min(mn, x); mx = Math.Max(mx, x); }
                if (mn == int.MaxValue) continue;
                int best = -1, bestD = int.MaxValue, bestSide = 0, bestX = 0;
                for (int x = mn; x <= mx; x++)
                {
                    if (s.occ[y * b.W + x] != color || !Accessible(b, s, x, y)) continue;
                    int d = Math.Min(x - mn, mx - x), side = (x - mn <= mx - x) ? 0 : 1;
                    if (best < 0 || d < bestD || (d == bestD && (side < bestSide || (side == bestSide && x < bestX))))
                    {
                        best = y * b.W + x; bestD = d; bestSide = side; bestX = x;
                    }
                }
                if (best >= 0) return best;
            }
            return -1;
        }

        // ------------------------------------------------------------------ gemiler

        private static bool SlotDone(State s, int i) => s.slotRem[i] == 0 && s.slotRem2[i] == 0;

        private static int PickForSlot(Board b, State s, int i, out bool second)
        {
            Ship sh = b.ships[s.slotShip[i]];
            int a = s.slotRem[i] > 0 ? Pick(b, s, sh.color) : -1;
            int c = s.slotRem2[i] > 0 ? Pick(b, s, sh.color2) : -1;
            if (a >= 0 && c >= 0)
            {
                second = s.slotRem2[i] > s.slotRem[i];
                return second ? c : a;
            }
            second = c >= 0;
            return c >= 0 ? c : a;
        }

        private static void FinishOne(Board b, State s, int i)
        {
            int p = b.ships[s.slotShip[i]].partner;
            if (p < 0)
            {
                s.slotShip[i] = -1;
                return;
            }
            for (int j = 0; j < b.slots; j++)
            {
                if (s.slotShip[j] == p && SlotDone(s, j))
                {
                    s.slotShip[j] = -1;
                    s.slotShip[i] = -1;
                    return;
                }
            }
        }

        private static void Collect(Board b, State s)
        {
            bool progress = true;
            while (progress)
            {
                progress = false;
                for (int i = 0; i < b.slots; i++)
                {
                    if (s.slotShip[i] < 0 || SlotDone(s, i)) continue;
                    int c = PickForSlot(b, s, i, out bool second);
                    if (c < 0) continue;
                    RemoveCube(b, s, c % b.W, c / b.W);
                    progress = true;
                    if (second) s.slotRem2[i]--; else s.slotRem[i]--;
                    if (SlotDone(s, i)) FinishOne(b, s, i);
                }
            }
        }

        private static int EmptySlots(Board b, State s)
        {
            int n = 0;
            for (int i = 0; i < b.slots; i++) if (s.slotShip[i] < 0) n++;
            return n;
        }

        private static int LaneCount(Board b, int lane) => (b.ships.Length - lane + b.cols - 1) / b.cols;
        private static int LaneShip(Board b, int lane, int k) => lane + k * b.cols;

        private static List<int> Legal(Board b, State s)
        {
            var moves = new List<int>();
            int empty = EmptySlots(b, s);
            if (empty == 0) return moves;
            for (int c = 0; c < b.cols; c++)
            {
                if (s.head[c] >= LaneCount(b, c)) continue;
                int id = LaneShip(b, c, s.head[c]), p = b.ships[id].partner;
                if (p < 0)
                {
                    moves.Add(c);
                    continue;
                }
                if (empty < 2) continue;
                int pc = p % b.cols, pos = p / b.cols - s.head[pc];
                if (pos < 0 || pos >= b.rows) continue;
                bool clear = true;
                for (int k = 0; k < pos; k++) if (LaneShip(b, pc, s.head[pc] + k) != id) { clear = false; break; }
                if (!clear) continue;
                if ((pc == c && pos == 1) || (pc != c && pos == 0)) moves.Add(c);
            }
            return moves;
        }

        private static void Dock(Board b, State s, int id)
        {
            for (int i = 0; i < b.slots; i++)
            {
                if (s.slotShip[i] >= 0) continue;
                s.slotShip[i] = id;
                s.slotRem[i] = b.ships[id].cap;
                s.slotRem2[i] = b.ships[id].cap2;
                return;
            }
        }

        private static void Apply(Board b, State s, int lane)
        {
            int id = LaneShip(b, lane, s.head[lane]), p = b.ships[id].partner;
            s.head[lane]++;
            Dock(b, s, id);
            if (p >= 0)
            {
                s.head[p % b.cols]++;
                Dock(b, s, p);
            }
            Collect(b, s);
        }

        private static double ScoreAfter(Board b, State s0, int lane)
        {
            State s = s0.Clone();
            int before = s.left;
            Apply(b, s, lane);
            if (s.left == 0) return 1e9;
            if (Legal(b, s).Count == 0) return -1e9;
            int busy = 0;
            for (int i = 0; i < b.slots; i++) if (s.slotShip[i] >= 0) busy++;
            return (before - s.left) - busy * 3.0 + EmptySlots(b, s) * 2.0;
        }

        private static bool Play(Board b, System.Random rng, bool smart)
        {
            State s = Init(b);
            while (s.left > 0)
            {
                var moves = Legal(b, s);
                if (moves.Count == 0) return false;
                int lane = moves[rng.Next(moves.Count)];
                if (smart)
                {
                    double best = double.MinValue;
                    foreach (int c in moves)
                    {
                        double v = ScoreAfter(b, s, c) + rng.Next(1000) * 1e-4;
                        if (v > best) { best = v; lane = c; }
                    }
                }
                Apply(b, s, lane);
            }
            return true;
        }

        private static int Dfs(Board b, int budget)
        {
            int nodes = 0;
            var seen = new HashSet<string>();

            string Key(State s)
            {
                var parts = new List<string>();
                for (int i = 0; i < b.slots; i++) if (s.slotShip[i] >= 0) parts.Add(s.slotShip[i] + ":" + s.slotRem[i] + ":" + s.slotRem2[i]);
                parts.Sort(StringComparer.Ordinal);
                return string.Join(",", s.head) + "|" + string.Join(",", parts);
            }

            int Search(State s)
            {
                if (s.left == 0) return 1;
                if (++nodes > budget) return -1;
                if (!seen.Add(Key(s))) return 0;
                var order = new List<(double score, int lane)>();
                foreach (int c in Legal(b, s)) order.Add((-ScoreAfter(b, s, c), c));
                order.Sort((x, y) => x.score.CompareTo(y.score));
                int res = 0;
                foreach (var (score, lane) in order)
                {
                    if (score > 1e8) continue;
                    State t = s.Clone();
                    Apply(b, t, lane);
                    int r = Search(t);
                    if (r == 1) return 1;
                    if (r < 0) res = -1;
                }
                return res;
            }

            return Search(Init(b));
        }
    }
}
