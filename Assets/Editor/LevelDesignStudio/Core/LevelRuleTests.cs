using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor.Studio
{
    /// <summary>
    /// docs/level-tasarim/tools/rule_tests/scenarios.json senaryolarını kural katmanından geçirir.
    /// Aynı dosyayı simülatör için tools/run_rule_tests.py kullanır.
    /// </summary>
    public static class LevelRuleTests
    {
        private const string ScenarioPath = "docs/level-tasarim/tools/rule_tests/scenarios.json";

        [Serializable] private class ShipData { public int c; public int cap; public int link; public int hid; public int c2 = -1; public int cap2; }
        [Serializable] private class Scenario
        {
            public string name; public int cols = 4; public int rows = 3; public int tier = 1;
            public string[] grid; public ShipData[] ships; public string[] expect; public string[] forbid; public int solvable = -1;
        }
        [Serializable] private class ScenarioFile { public Scenario[] scenarios; }

        [MenuItem("Tools/PixelGame/🧪 Kural Katmanı Testleri", priority = 3)]
        private static void RunFromMenu()
        {
            string report = Run(out bool ok);
            if (ok) Debug.Log(report); else Debug.LogError(report);
        }

        public static string Run(out bool ok)
        {
            ok = true;
            var sb = new StringBuilder();
            if (!File.Exists(ScenarioPath)) { ok = false; return "Senaryo dosyası yok: " + ScenarioPath; }
            var file = JsonUtility.FromJson<ScenarioFile>(File.ReadAllText(ScenarioPath));
            int pass = 0;
            foreach (var sc in file.scenarios)
            {
                var found = new HashSet<string>();
                foreach (var f in LevelRules.Check(ToSnapshot(sc))) found.Add(f.code);
                var problems = new List<string>();
                if (sc.expect != null) foreach (var e in sc.expect) if (!found.Contains(e)) problems.Add("eksik " + e);
                if (sc.forbid != null) foreach (var e in sc.forbid) if (found.Contains(e)) problems.Add("fazla " + e);
                if (problems.Count == 0) pass++;
                else { ok = false; sb.AppendLine($"KALDI {sc.name}: {string.Join(", ", problems)} (bulunan: {string.Join(", ", found)})"); }
            }
            sb.Insert(0, $"Kural testleri: {pass}/{file.scenarios.Length} geçti\n");
            return sb.ToString();
        }

        private static LevelSnapshot ToSnapshot(Scenario sc)
        {
            int h = sc.grid.Length;
            int w = sc.grid[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var s = new LevelSnapshot { width = w, height = h, columns = sc.cols, rows = sc.rows, tier = sc.tier, cells = new int[w * h] };
            int maxColor = -1;
            for (int r = 0; r < h; r++)
            {
                // Senaryoda ilk satır resmin üstüdür; hücre dizisinde y aşağıdan yukarı
                var parts = sc.grid[r].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int x = 0; x < w; x++)
                {
                    int v = int.Parse(parts[x]);
                    s.cells[(h - 1 - r) * w + x] = v;
                    if (v > maxColor) maxColor = v;
                }
            }
            s.colorCount = maxColor + 1;
            s.colorNames = new string[s.colorCount];
            for (int i = 0; i < s.colorCount; i++) s.colorNames[i] = "Renk " + i;
            foreach (var d in sc.ships)
            {
                s.ships.Add(new LevelSnapshot.Ship
                {
                    empty = d.cap <= 0, color = d.c, capacity = d.cap, link = d.link, hidden = d.hid != 0,
                    dual = d.cap2 > 0, color2 = d.c2, capacity2 = d.cap2
                });
            }
            return s;
        }
    }
}
