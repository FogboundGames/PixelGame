using System.Collections.Generic;
using UnityEngine;

namespace PixelGame.Editor.Studio
{
    /// <summary>Tuval araçlarının hücre hesapları; arayüzden ve asset'ten bağımsız.</summary>
    public static class CanvasShapes
    {
        public static void Line(int x0, int y0, int x1, int y1, List<Vector2Int> into)
        {
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                into.Add(new Vector2Int(x0, y0));
                if (x0 == x1 && y0 == y1) return;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public static void Rectangle(int x0, int y0, int x1, int y1, bool filled, List<Vector2Int> into)
        {
            int ax = Mathf.Min(x0, x1), bx = Mathf.Max(x0, x1), ay = Mathf.Min(y0, y1), by = Mathf.Max(y0, y1);
            for (int y = ay; y <= by; y++)
                for (int x = ax; x <= bx; x++)
                    if (filled || x == ax || x == bx || y == ay || y == by) into.Add(new Vector2Int(x, y));
        }

        public static void Ellipse(int x0, int y0, int x1, int y1, bool filled, List<Vector2Int> into)
        {
            int ax = Mathf.Min(x0, x1), bx = Mathf.Max(x0, x1), ay = Mathf.Min(y0, y1), by = Mathf.Max(y0, y1);
            float cx = (ax + bx) * 0.5f, cy = (ay + by) * 0.5f, rx = (bx - ax) * 0.5f + 0.5f, ry = (by - ay) * 0.5f + 0.5f;
            bool Inside(int x, int y)
            {
                float u = (x - cx) / rx, v = (y - cy) / ry;
                return u * u + v * v <= 1f;
            }
            for (int y = ay; y <= by; y++)
                for (int x = ax; x <= bx; x++)
                {
                    if (!Inside(x, y)) continue;
                    if (filled || !Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1))
                        into.Add(new Vector2Int(x, y));
                }
        }

        /// <summary>4 yönlü doldurma: başlangıç hücresiyle aynı değerdeki bağlı hücreler (oyundaki komşulukla aynı).</summary>
        public static List<Vector2Int> FloodFill(LevelCanvas canvas, int startX, int startY)
        {
            var result = new List<Vector2Int>();
            int w = canvas.Width, h = canvas.Height;
            if (startX < 0 || startY < 0 || startX >= w || startY >= h) return result;
            byte from = canvas.Get(startX, startY);
            var seen = new bool[w * h];
            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(startX, startY));
            seen[startY * w + startX] = true;
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                result.Add(c);
                for (int d = 0; d < 4; d++)
                {
                    int x = c.x + (d == 0 ? 1 : d == 1 ? -1 : 0), y = c.y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (x < 0 || y < 0 || x >= w || y >= h || seen[y * w + x] || canvas.Get(x, y) != from) continue;
                    seen[y * w + x] = true;
                    stack.Push(new Vector2Int(x, y));
                }
            }
            return result;
        }

        /// <summary>Bir hücrenin fırça kalınlığı ve simetri uygulanmış hâli.</summary>
        public static void Stamp(Vector2Int cell, int size, bool mirrorX, bool mirrorY, int width, int height, HashSet<Vector2Int> into)
        {
            int low = -(size - 1) / 2, high = size / 2;
            for (int dy = low; dy <= high; dy++)
            for (int dx = low; dx <= high; dx++)
            {
                int x = cell.x + dx, y = cell.y + dy;
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                into.Add(new Vector2Int(x, y));
                int mx = width - 1 - x, my = height - 1 - y;
                if (mirrorX) into.Add(new Vector2Int(mx, y));
                if (mirrorY) into.Add(new Vector2Int(x, my));
                if (mirrorX && mirrorY) into.Add(new Vector2Int(mx, my));
            }
        }
    }
}
