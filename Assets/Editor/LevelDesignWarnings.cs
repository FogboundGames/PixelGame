using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Bir bölümün tasarımındaki sorunları (küçük renkler, dengesiz gemi sırası, gömülü renkler, bozuk halatlar...)
    /// tespit eder. Level Designer'daki "Tasarım Uyarıları" kartı bunu gösterir.
    /// </summary>
    public static class LevelDesignWarnings
    {
        public struct Warning
        {
            public MessageType type;
            public string text;
            public Warning(MessageType type, string text) { this.type = type; this.text = text; }
        }

        private const int TinyColorCubes = 5;   // bundan az küplü renk tek ve çok küçük bir gemi olur
        private const int TooManyColors = 8;

        public static List<Warning> Compute(PixelLevelData level)
        {
            var list = new List<Warning>();
            if (level == null) return list;

            var rep = LevelSolvabilityAnalyzer.Analyze(level, false);
            if (!rep.valid)
            {
                list.Add(new Warning(MessageType.Error, rep.invalidReason));
                return list;
            }

            var palette = level.ColorPalette;
            var seq = level.WagonSequence;
            bool custom = level.UseCustomWagonSequence && seq != null && seq.Count > 0;

            if (level.UseCustomWagonSequence && (seq == null || seq.Count == 0))
                list.Add(new Warning(MessageType.Error, "Manuel sıra açık ama gemi sırası boş — oyunda hiç gemi gelmez."));

            foreach (var w in rep.warnings)
                list.Add(new Warning(MessageType.Warning, w));

            // Renk başına küp / kapasite
            int realColors = 0;
            foreach (var b in rep.budgets)
            {
                string name = ColorName(palette, b.paletteIndex);
                if (b.cubeCount > 0) realColors++;

                if (b.IsStale && b.cubeCount > 0)
                    list.Add(new Warning(MessageType.Warning, $"{name}: palette kayıtlı sayı ({b.storedPixelCount}) gerçek küp sayısıyla ({b.cubeCount}) uyuşmuyor — paleti yeniden tara."));

                if (custom && b.wagonCapacity != b.cubeCount)
                {
                    int d = b.cubeCount - b.wagonCapacity;
                    list.Add(new Warning(MessageType.Error, d > 0
                        ? $"{name}: gemiler {d} küp eksik taşıyor ({b.wagonCapacity}/{b.cubeCount}) — bölüm bitmez."
                        : $"{name}: gemi kapasitesi {-d} fazla ({b.wagonCapacity}/{b.cubeCount}) — son gemi dolamadan kalkar."));
                }

                if (b.cubeCount == 1)
                    list.Add(new Warning(MessageType.Warning, $"{name}: sadece 1 küp — 1'lik bir gemi gelecek."));
                else if (b.cubeCount > 0 && b.cubeCount < TinyColorCubes)
                    list.Add(new Warning(MessageType.Info, $"{name}: sadece {b.cubeCount} küp — tek ve çok küçük bir gemi olacak."));
                else if (level.UseRoundCapacities && b.cubeCount > 0 && b.cubeCount < level.MinTruckCapacity)
                    list.Add(new Warning(MessageType.Info, $"{name}: {b.cubeCount} küp, 'en az' ({level.MinTruckCapacity}) değerinin altında — bu renkte küçük bir gemi olacak."));
            }

            if (realColors > TooManyColors)
                list.Add(new Warning(MessageType.Warning, $"{realColors} renk var — {TooManyColors}'den fazlası oyuncuyu yorar ve çok sayıda küçük gemi üretir. Renk indirgemeyi dene."));

            // Başta gömülü renkler ve başta toplayamayacak ön sıra gemileri
            var grid = LevelSolvabilityAnalyzer.BuildGrid(level, out int minX, out int maxX, out int minY, out int maxY);
            if (grid.Count > 0)
            {
                var air = LevelSolvabilityAnalyzer.ComputeOutsideAir(grid, minX, maxX, minY, maxY);
                var exposed = new HashSet<int>();
                var present = new HashSet<int>();
                foreach (var kv in grid)
                {
                    present.Add(kv.Value);
                    if (LevelSolvabilityAnalyzer.IsExposed(air, kv.Key.Item1, kv.Key.Item2)) exposed.Add(kv.Value);
                }

                var buried = new List<string>();
                foreach (int pi in present)
                    if (pi >= 0 && !exposed.Contains(pi)) buried.Add(ColorName(palette, pi));
                if (buried.Count > 0)
                    list.Add(new Warning(MessageType.Info, $"Başta tamamen gömülü renkler: {string.Join(", ", buried)}. Bu renklerin gemileri erken gelirse açılana kadar slotta bekler."));

                if (custom)
                {
                    int cols = Mathf.Clamp(level.PoolColumns, 1, 8);
                    var blockedFront = new List<string>();
                    int openFront = 0;
                    for (int i = 0; i < Mathf.Min(cols, seq.Count); i++)
                    {
                        var w = seq[i];
                        if (w == null) continue;
                        if (exposed.Contains(w.paletteIndex)) openFront++;
                        else blockedFront.Add($"#{i + 1} {ColorName(palette, w.paletteIndex)}");
                    }
                    if (openFront == 0 && blockedFront.Count > 0)
                        list.Add(new Warning(MessageType.Error, $"Ön sıradaki hiçbir geminin rengi başta açıkta değil ({string.Join(", ", blockedFront)}) — oyuncu ilk hamlede slotları boşuna doldurur, erken kilit riski yüksek."));
                    else if (blockedFront.Count > 0 && blockedFront.Count * 2 >= blockedFront.Count + openFront)
                        list.Add(new Warning(MessageType.Warning, $"Ön sıradaki gemilerin çoğunun rengi başta açıkta değil ({string.Join(", ", blockedFront)}) — dikkatsiz oyuncu erken kilitlenebilir."));
                    else if (blockedFront.Count > 0)
                        list.Add(new Warning(MessageType.Info, $"Ön sırada rengi başta açıkta olmayan gemiler: {string.Join(", ", blockedFront)}."));
                }
            }

            // Halatlar ve gizli gemiler
            if (custom)
            {
                int cols = Mathf.Clamp(level.PoolColumns, 1, 8);
                int poolSize = Mathf.Min(seq.Count, cols * Mathf.Clamp(level.PoolRows, 1, 6));
                var byLink = new Dictionary<int, List<int>>();
                for (int i = 0; i < seq.Count; i++)
                {
                    var w = seq[i];
                    if (w == null) continue;
                    if (w.linkId > 0)
                    {
                        if (!byLink.TryGetValue(w.linkId, out var l)) byLink[w.linkId] = l = new List<int>();
                        l.Add(i);
                    }
                    if (w.isHidden && i < cols)
                        list.Add(new Warning(MessageType.Info, $"#{i + 1} gizli gemi en ön sırada — oyun başlar başlamaz açılır, gizlilik etkisi olmaz."));
                }
                foreach (var kv in byLink)
                {
                    if (kv.Value.Count != 2)
                    {
                        list.Add(new Warning(MessageType.Error, $"Halat #{kv.Key}: {kv.Value.Count} gemi bağlı (2 olmalı)."));
                        continue;
                    }
                    int a = kv.Value[0], b = kv.Value[1];
                    bool sideBySide = b == a + 1 && a / cols == b / cols;
                    bool stackedInPool = b == a + cols && b < poolSize;
                    if (!sideBySide && !stackedInPool)
                        list.Add(new Warning(MessageType.Warning, $"Halat #{kv.Key} (#{a + 1} ↔ #{b + 1}) yan yana ya da havuzda üst üste değil — ortağı geç gelirse ilk gemi halatsız gönderilebilir."));
                }
            }

            return list;
        }

        private static string ColorName(List<PaletteColorOverride> palette, int index)
        {
            if (palette == null || index < 0 || index >= palette.Count || palette[index] == null) return $"Renk #{index + 1}";
            var e = palette[index];
            string hex = ColorUtility.ToHtmlStringRGB(e.targetColor);
            return string.IsNullOrEmpty(e.label) || e.label == "Renk" ? $"Renk #{index + 1} (#{hex})" : $"{e.label} (#{hex})";
        }
    }
}
