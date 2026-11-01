using System.Collections.Generic;
using UnityEngine;

namespace PixelGame.Editor.Studio
{
    /// <summary>
    /// Kural kontrolü için bir level'ın salt okunur görüntüsü. Asset'e yazmaz, sahneye ve arayüze bağlı değildir;
    /// test senaryoları da aynı yapıyı kurar.
    /// </summary>
    public sealed class LevelSnapshot
    {
        public sealed class Ship
        {
            public bool empty;      // null ya da kapasitesi 0: oyun bu girdiyi atlar
            public int color = -1;  // palet sırası, -1: palette yok
            public int capacity;
            public int link;
            public bool hidden;
            public bool dual;
            public int color2 = -1;
            public int capacity2;
        }

        public int width, height;
        public int[] cells = new int[0];    // y * width + x, y aşağıdan yukarı; boş hücre -1
        public int unmappedCubes;           // paletteki hiçbir renge uymayan küp sayısı
        public int colorCount;
        public string[] colorNames = new string[0];
        public int columns = 4, rows = 3;
        public int tier;                    // 0 Kolay, 1 Orta, 2 Zor
        public bool tierManual = true;
        public List<Ship> ships = new List<Ship>();
        public List<Vector2Int> mysteryCells = new List<Vector2Int>();

        private const float GameColorTolerance = 0.04f;

        public string ColorName(int index) =>
            index >= 0 && index < colorNames.Length ? colorNames[index] : "bilinmeyen renk";

        /// <summary>Oyunun renk eşleştirme toleransıyla paletteki sıra; yoksa -1.</summary>
        public static int FindColor(IList<PaletteColorOverride> palette, Color c)
        {
            for (int i = 0; i < palette.Count; i++)
            {
                var e = palette[i];
                if (e == null) continue;
                if (PaletteColorOverride.ColorsMatch(e.targetColor, c, GameColorTolerance) ||
                    PaletteColorOverride.ColorsMatch(e.originalColor, c, GameColorTolerance)) return i;
            }
            return -1;
        }

        public static LevelSnapshot From(PixelLevelData level)
        {
            var s = new LevelSnapshot();
            var palette = level.ColorPalette ?? new List<PaletteColorOverride>();
            s.colorCount = palette.Count;
            s.colorNames = new string[palette.Count];
            for (int i = 0; i < palette.Count; i++)
                s.colorNames[i] = palette[i] != null ? StudioColors.Name(palette[i].targetColor, i) : "boş";
            s.columns = Mathf.Max(1, level.PoolColumns);
            s.rows = Mathf.Max(1, level.PoolRows);
            s.tier = (int)level.DifficultyTier;
            s.tierManual = level.DifficultyIsManual;

            var tex = level.GetActiveTexture();
            if (tex != null && tex.isReadable)
            {
                s.width = tex.width;
                s.height = tex.height;
                var px = tex.GetPixels32();
                s.cells = new int[px.Length];
                var cache = new Dictionary<Color32Key, int>();
                for (int i = 0; i < px.Length; i++)
                {
                    if (px[i].a == 0) { s.cells[i] = -1; continue; }
                    var key = new Color32Key(px[i]);
                    if (!cache.TryGetValue(key, out int idx))
                    {
                        idx = FindOriginal(palette, px[i]);
                        cache[key] = idx;
                    }
                    s.cells[i] = idx;
                    if (idx < 0) s.unmappedCubes++;
                }
            }

            if (level.WagonSequence != null)
            {
                foreach (var w in level.WagonSequence)
                {
                    if (w == null || w.capacity <= 0) { s.ships.Add(new Ship { empty = true }); continue; }
                    var ship = new Ship { color = FindColor(palette, w.wagonColor), capacity = w.capacity, link = w.linkId, hidden = w.isHidden };
                    if (w.isDualColor)
                    {
                        ship.dual = true;
                        ship.color2 = FindColor(palette, w.secondaryColor);
                        ship.capacity2 = w.secondaryCapacity > 0 ? w.secondaryCapacity : w.capacity;
                    }
                    s.ships.Add(ship);
                }
            }

            if (level.MysteryCubeCoordinates != null) s.mysteryCells.AddRange(level.MysteryCubeCoordinates);
            return s;
        }

        // Pikseller resmin kendi rengindedir: palet eşleşmesi orijinal renkle yapılır
        private static int FindOriginal(IList<PaletteColorOverride> palette, Color32 c)
        {
            Color col = c;
            for (int i = 0; i < palette.Count; i++)
                if (palette[i] != null && PaletteColorOverride.ColorsMatch(palette[i].originalColor, col, GameColorTolerance)) return i;
            return -1;
        }

        private readonly struct Color32Key : System.IEquatable<Color32Key>
        {
            private readonly int v;
            public Color32Key(Color32 c) { v = c.r | c.g << 8 | c.b << 16; }
            public bool Equals(Color32Key o) => v == o.v;
            public override bool Equals(object o) => o is Color32Key k && Equals(k);
            public override int GetHashCode() => v;
        }
    }
}
