using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Bütün bölümlerin zorluk ölçümlerini (renk dağılımı + varsa otomatik oyun testi sonuçları) oyundaki
    /// sırasıyla tek tabloda gösterir. Hem Level Designer'ın "Zorluk Tablosu" sekmesi hem de ayrı pencere kullanır.
    /// </summary>
    public class LevelDifficultyTableView
    {
        private class Row
        {
            public int index;
            public PixelLevelData level;
            public LevelDifficultyReport.Result rep;
            public int ships;
            public int slots;
            public string randomFail = "-", smartFail = "-";
        }

        private List<Row> m_Rows;
        private Vector2 m_Scroll;

        public void Refresh()
        {
            m_Rows = new List<Row>();
            var lm = UnityEngine.Object.FindFirstObjectByType<LevelManager>();
            if (lm == null || lm.Levels == null) return;

            var tests = AutoPlaytestRunner.LoadResults();
            for (int i = 0; i < lm.Levels.Count; i++)
            {
                var l = lm.Levels[i];
                var row = new Row { index = i, level = l };
                if (l != null)
                {
                    row.rep = LevelDifficultyReport.GetCached(l);
                    row.ships = l.WagonSequence != null ? l.WagonSequence.Count : 0;
                    row.slots = l.SlotCount;
                    var rnd = tests.Where(t => t.levelIndex == i && t.player == "random").ToList();
                    var sm = tests.Where(t => t.levelIndex == i && t.player == "smart").ToList();
                    if (rnd.Count > 0) row.randomFail = $"%{100 * rnd.Count(t => t.outcome != "KAZANDI") / rnd.Count} ({rnd.Count})";
                    if (sm.Count > 0) row.smartFail = $"%{100 * sm.Count(t => t.outcome != "KAZANDI") / sm.Count} ({sm.Count})";
                }
                m_Rows.Add(row);
            }
        }

        /// <param name="onSelect">Satıra (bölüm adına) tıklanınca çağrılır.</param>
        /// <param name="selected">Vurgulanacak (seçili) bölüm.</param>
        public void OnGUI(Action<PixelLevelData> onSelect, PixelLevelData selected = null)
        {
            if (m_Rows == null) Refresh();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("📊 Level Zorluk Tablosu (oyundaki sırayla)", new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 });
            if (GUILayout.Button("↻ Yenile", GUILayout.Width(80))) Refresh();
            if (GUILayout.Button("🤖 Oyun Testi", GUILayout.Width(100))) AutoPlaytestWindow.Open();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Kademe resmin renk dağılımından hesaplanır (baskın renk, etkin renk sayısı, bölge büyüklüğü, gömülü renkler). " +
                                    "'Kayıp' sütunları son Otomatik Oyun Testi'nden gelir: dikkatsiz oyuncu için kolayda ~%0, ortada ~%15–35, zorda ~%40+ hedeflenebilir; akıllı oyuncu her zaman kazanabilmeli.", MessageType.Info);

            if (m_Rows.Count == 0)
            {
                EditorGUILayout.HelpBox("Level listesi bulunamadı (Gemi sahnesini aç).", MessageType.Warning);
                return;
            }

            int kolay = m_Rows.Count(r => r.rep != null && r.rep.valid && r.rep.tier == LevelDifficultyReport.Tier.Kolay);
            int orta = m_Rows.Count(r => r.rep != null && r.rep.valid && r.rep.tier == LevelDifficultyReport.Tier.Orta);
            int zor = m_Rows.Count(r => r.rep != null && r.rep.valid && r.rep.tier == LevelDifficultyReport.Tier.Zor);
            EditorGUILayout.LabelField($"🟢 {kolay} Kolay · 🟡 {orta} Orta · 🔴 {zor} Zor", EditorStyles.miniBoldLabel);

            string[] heads = { "#", "Bölüm", "Kademe", "Puan", "Baskın renk", "Etkin renk", "Bölge (ort.)", "Başta açık", "Gömülü renk", "Gemi", "Slot", "🎲 Kayıp", "🧠 Kayıp" };
            float[] w = { 28, 210, 70, 42, 90, 75, 90, 75, 80, 45, 35, 70, 70 };
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            for (int i = 0; i < heads.Length; i++) GUILayout.Label(heads[i], EditorStyles.miniBoldLabel, GUILayout.Width(w[i]));
            EditorGUILayout.EndHorizontal();

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll, GUILayout.MinHeight(200));
            LevelDifficultyReport.Tier? prevTier = null;
            foreach (var r in m_Rows)
            {
                bool isSel = selected != null && r.level == selected;
                if (isSel) GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                EditorGUILayout.BeginHorizontal(isSel ? EditorStyles.helpBox : GUIStyle.none);
                GUI.backgroundColor = Color.white;
                if (r.level == null || r.rep == null || !r.rep.valid)
                {
                    GUILayout.Label((r.index + 1).ToString(), GUILayout.Width(w[0]));
                    GUILayout.Label(r.level ? r.level.name + " (resim yok)" : "(boş)", GUILayout.Width(w[1]));
                    EditorGUILayout.EndHorizontal();
                    continue;
                }
                var rep = r.rep;
                bool dip = prevTier.HasValue && rep.tier < prevTier.Value;   // zordan sonra daha kolay geliyor
                prevTier = rep.tier;

                var cells = new[]
                {
                    (r.index + 1).ToString(), r.level.name, LevelDifficultyReport.TierLabel(rep.tier), $"{rep.score:0}",
                    $"%{rep.dominantShare * 100f:0}", $"{rep.effectiveColors:0.0}/{rep.colorCount}", $"{rep.regionCount} ({rep.avgRegionSize:0})",
                    $"%{rep.exposedShare * 100f:0}", rep.buriedColors.ToString(), r.ships.ToString(), r.slots.ToString(), r.randomFail, r.smartFail
                };
                for (int i = 0; i < cells.Length; i++)
                {
                    var st = EditorStyles.label;
                    if (i == 2) st = Colored(dip ? new Color(0.6f, 0.8f, 1f) : LevelDifficultyReport.TierColor(rep.tier));
                    if (i == 4 && rep.dominantShare >= 0.45f) st = Colored(new Color(1f, 0.6f, 0.35f));
                    if (i == 12 && r.smartFail != "-" && !r.smartFail.StartsWith("%0")) st = Colored(new Color(1f, 0.45f, 0.4f));
                    if (i == 1)
                    {
                        if (GUILayout.Button(new GUIContent(cells[i], "Tıkla: bu bölümü seç"), EditorStyles.label, GUILayout.Width(w[i])))
                            onSelect?.Invoke(r.level);
                        continue;
                    }
                    GUILayout.Label(cells[i], st, GUILayout.Width(w[i]));
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField("Turuncu: baskın renk ≥%45 · Mavi kademe: bir önceki level'dan daha kolay (sıra geri gidiyor) · Kırmızı: akıllı oyuncu bile kaybetmiş.", EditorStyles.miniLabel);
        }

        private static GUIStyle Colored(Color c) => new GUIStyle(EditorStyles.label) { normal = { textColor = c } };
    }

    /// <summary>Zorluk tablosunu ayrı pencerede açar (Level Designer'daki "Zorluk Tablosu" sekmesinin aynısı).</summary>
    public class LevelDifficultyOverviewWindow : EditorWindow
    {
        [MenuItem("Tools/PixelGame/📊 Level Zorluk Tablosu", priority = 31)]
        public static void Open() => GetWindow<LevelDifficultyOverviewWindow>("📊 Zorluk Tablosu").minSize = new Vector2(980, 300);

        private readonly LevelDifficultyTableView m_View = new LevelDifficultyTableView();

        private void OnEnable() => m_View.Refresh();

        private void OnGUI() => m_View.OnGUI(level => { Selection.activeObject = level; EditorGUIUtility.PingObject(level); });
    }
}
