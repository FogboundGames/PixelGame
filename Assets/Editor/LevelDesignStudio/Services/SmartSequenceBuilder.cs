using System.Collections.Generic;
using UnityEngine;

namespace PixelGame.Editor.Studio
{
    public enum SmartSortMode { Easy, Balanced, Hard }

    /// <summary>
    /// Eski Level Designer'daki "Akıllı Sırala": resmi dıştan içe soyarak gemi sırası üretir. Aynı girdiyle eski araçla aynı
    /// sırayı üretir, ama asset'e yazmaz ve havuz satır sayısını değiştirmez (eskisi satır sayısını gemi sayısına çıkarıyordu).
    /// </summary>
    public static class SmartSequenceBuilder
    {
        private const float PaletteTolerance = 0.08f;

        public static List<WagonSequenceEntry> Build(PixelLevelData level, SmartSortMode mode)
        {
            var sequence = new List<WagonSequenceEntry>();
            var grid = ReadGrid(level);
            if (grid.Count == 0) return sequence;

            var remaining = new Dictionary<Color, int>();
            var paletteIndex = new Dictionary<Color, int>();
            foreach (var kv in grid)
            {
                remaining.TryGetValue(kv.Value, out int n);
                remaining[kv.Value] = n + 1;
                if (!paletteIndex.ContainsKey(kv.Value)) paletteIndex[kv.Value] = PaletteIndex(level, kv.Value);
            }

            int capPerShip = Mathf.Max(1, level.TruckCapacity);
            Color last = Color.clear;
            while (grid.Count > 0)
            {
                var exposed = ExposedByColor(grid);
                Color chosen = Choose(exposed, remaining, last, mode);
                last = chosen;

                int load = Mathf.Min(capPerShip, remaining[chosen]);
                sequence.Add(new WagonSequenceEntry(chosen, load, paletteIndex[chosen]));
                remaining[chosen] -= load;

                int peeled = 0;
                foreach (var cell in exposed[chosen])
                {
                    grid.Remove(cell);
                    if (++peeled >= load) break;
                }
                if (peeled < load)
                {
                    var rest = new List<Vector2Int>();
                    foreach (var kv in grid) if (kv.Value == chosen) rest.Add(kv.Key);
                    for (int i = 0; i < rest.Count && peeled < load; i++, peeled++) grid.Remove(rest[i]);
                }
            }
            return sequence;
        }

        private static Dictionary<Vector2Int, Color> ReadGrid(PixelLevelData level)
        {
            var grid = new Dictionary<Vector2Int, Color>();
            var tex = level.GetActiveTexture();
            if (tex == null || !tex.isReadable) return grid;
            Color32[] raw = tex.GetPixels32();
            for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++)
            {
                Color32 p = raw[y * tex.width + x];
                if (p.a < 25) continue;
                Color c = p;
                foreach (var e in level.ColorPalette)
                {
                    if (e != null && PaletteColorOverride.ColorsMatch(e.targetColor, c, PaletteTolerance))
                    {
                        c = e.targetColor;
                        break;
                    }
                }
                grid[new Vector2Int(x, y)] = c;
            }
            return grid;
        }

        private static int PaletteIndex(PixelLevelData level, Color c)
        {
            for (int p = 0; p < level.ColorPalette.Count; p++)
            {
                var e = level.ColorPalette[p];
                if (e != null && PaletteColorOverride.ColorsMatch(e.targetColor, c, PaletteTolerance)) return p;
            }
            return 0;
        }

        private static Dictionary<Color, List<Vector2Int>> ExposedByColor(Dictionary<Vector2Int, Color> grid)
        {
            var exposed = new Dictionary<Color, List<Vector2Int>>();
            foreach (var kv in grid)
            {
                Vector2Int p = kv.Key;
                bool open = !grid.ContainsKey(new Vector2Int(p.x - 1, p.y)) || !grid.ContainsKey(new Vector2Int(p.x + 1, p.y)) ||
                            !grid.ContainsKey(new Vector2Int(p.x, p.y - 1)) || !grid.ContainsKey(new Vector2Int(p.x, p.y + 1));
                if (!open) continue;
                if (!exposed.TryGetValue(kv.Value, out var list)) exposed[kv.Value] = list = new List<Vector2Int>();
                list.Add(p);
            }
            if (exposed.Count == 0)
            {
                foreach (var kv in grid)
                {
                    exposed[kv.Value] = new List<Vector2Int> { kv.Key };
                    break;
                }
            }
            return exposed;
        }

        private static Color Choose(Dictionary<Color, List<Vector2Int>> exposed, Dictionary<Color, int> remaining, Color last, SmartSortMode mode)
        {
            if (mode == SmartSortMode.Easy)
            {
                // En çok açık yüzeyi olan renk, bitene kadar sürdürülür
                if (last != Color.clear && exposed.ContainsKey(last) && remaining[last] > 0) return last;
                Color best = Color.clear;
                int max = -1;
                foreach (var kv in exposed)
                {
                    if (kv.Value.Count <= max) continue;
                    max = kv.Value.Count;
                    best = kv.Key;
                }
                return best;
            }

            var candidates = new List<Color>(exposed.Keys);
            if (mode == SmartSortMode.Balanced)
            {
                // Açık yüzeyi olan renkler arasında dönüşümlü
                candidates.Sort((a, b) => exposed[b].Count.CompareTo(exposed[a].Count));
                return last != Color.clear && candidates.Count > 1 && candidates[0] == last ? candidates[1] : candidates[0];
            }

            // Zor: açık yüzeyi en az olan renk erkenden girer
            candidates.Sort((a, b) => exposed[a].Count.CompareTo(exposed[b].Count));
            return candidates[0];
        }
    }
}
