using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PixelGame.Editor.Studio
{
    /// <summary>Seçili level'ın simülasyon sonucu ve bütün level'ların sıralanıp süzülebilen tablosu.</summary>
    public sealed class AnalysisPanel : VisualElement
    {
        private sealed class Row
        {
            public PixelLevelData level;
            public int number, tier, cubes, ships, colors, ropes, hidden, dual, mystery, critical, warning;
            public SimResult sim;
            public bool stale;
            public BandVerdict verdict;
        }

        private readonly SimulatorService m_Sim;
        public event Action<PixelLevelData> LevelPicked;
        public event Action<PixelLevelData> LevelOpened;

        private readonly List<Row> m_AllRows = new List<Row>();
        private readonly List<Row> m_Rows = new List<Row>();
        private readonly MultiColumnListView m_Table;
        private readonly Dictionary<string, Func<Row, IComparable>> m_SortKeys = new Dictionary<string, Func<Row, IComparable>>();
        private IList<PixelLevelData> m_Levels;
        private Func<PixelLevelData, int> m_NumberOf;
        private PixelLevelData m_Current;

        private readonly Label m_Title, m_Note, m_Progress;
        private Label m_Count;
        private readonly VisualElement m_Metrics;
        private readonly Button m_RunOne, m_RunAll, m_Stop;
        private int m_TierFilter = -1;
        private bool m_OnlyOutOfBand, m_OnlyDual, m_OnlyCritical;
        private string m_Search = "";

        public AnalysisPanel(SimulatorService sim)
        {
            m_Sim = sim;
            AddToClassList("analysis");

            var head = new VisualElement();
            head.AddToClassList("analysis__head");
            var left = new VisualElement();
            left.style.flexGrow = 1;
            left.style.flexShrink = 1;
            m_Title = new Label();
            m_Title.AddToClassList("analysis__title");
            left.Add(m_Title);
            m_Note = new Label();
            m_Note.AddToClassList("analysis__note");
            left.Add(m_Note);
            head.Add(left);
            m_RunOne = new Button(() => { if (m_Current != null) m_Sim.Enqueue(new[] { m_Current }, false); }) { text = "Bu level'ı simüle et" };
            head.Add(m_RunOne);
            m_RunAll = new Button(() => m_Sim.Enqueue(m_Levels, true))
            {
                text = "Eksikleri simüle et",
                tooltip = "Hiç ölçülmemiş ya da ölçümden sonra değişmiş level'ları simüle eder"
            };
            head.Add(m_RunAll);
            m_Stop = new Button(() => m_Sim.CancelAll()) { text = "Durdur" };
            head.Add(m_Stop);
            Add(head);

            m_Progress = new Label();
            m_Progress.AddToClassList("analysis__progress");
            Add(m_Progress);

            m_Metrics = new VisualElement();
            m_Metrics.AddToClassList("metrics");
            Add(m_Metrics);

            Add(BuildFilters());

            var columns = new Columns();
            AddColumn(columns, "number", "#", 38, r => r.number.ToString(), r => r.number);
            AddColumn(columns, "name", "Level", 150, r => r.level.LevelName, r => r.level.LevelName);
            AddColumn(columns, "tier", "Zorluk", 54, r => DifficultyBands.TierNames[Mathf.Clamp(r.tier, 0, 2)], r => r.tier);
            AddColumn(columns, "cubes", "Küp", 46, r => r.cubes.ToString(), r => r.cubes);
            AddColumn(columns, "colors", "Renk", 42, r => r.colors.ToString(), r => r.colors);
            AddColumn(columns, "ships", "Gemi", 44, r => r.ships.ToString(), r => r.ships);
            AddColumn(columns, "ropes", "Halat", 44, r => r.ropes.ToString(), r => r.ropes);
            AddColumn(columns, "hidden", "Gizli", 42, r => r.hidden.ToString(), r => r.hidden);
            AddColumn(columns, "dual", "Güçlü", 46, r => r.dual.ToString(), r => r.dual);
            AddColumn(columns, "mystery", "Gizli küp", 58, r => r.mystery.ToString(), r => r.mystery);
            AddColumn(columns, "critical", "Kritik", 46, r => r.critical.ToString(), r => r.critical);
            AddColumn(columns, "warning", "Uyarı", 44, r => r.warning.ToString(), r => r.warning);
            AddColumn(columns, "random", "Rastgele", 60, r => Rate(r, r.sim?.random), r => r.sim?.random ?? -1f);
            AddColumn(columns, "smart", "Dikkatli", 58, r => Rate(r, r.sim?.smart), r => r.sim?.smart ?? -1f);
            AddColumn(columns, "tap", "Hızlı", 50, r => Rate(r, r.sim?.tapSmart), r => r.sim?.tapSmart ?? -1f);
            AddColumn(columns, "verdict", "Durum", 80, r => r.sim == null ? "Ölçülmedi" : DifficultyBands.VerdictText(r.verdict) + (r.stale ? " (eski)" : ""), r => (int)r.verdict);

            m_Table = new MultiColumnListView(columns)
            {
                itemsSource = m_Rows,
                fixedItemHeight = 22,
                selectionType = SelectionType.Single,
                sortingMode = ColumnSortingMode.Custom,
                showBoundCollectionSize = false
            };
            m_Table.AddToClassList("analysis__table");
            m_Table.columnSortingChanged += () => { ApplySort(); m_Table.RefreshItems(); };
            m_Table.selectionChanged += items =>
            {
                foreach (var item in items) { LevelPicked?.Invoke(((Row)item).level); break; }
            };
            m_Table.itemsChosen += items =>
            {
                foreach (var item in items) { LevelOpened?.Invoke(((Row)item).level); break; }
            };
            Add(m_Table);

            m_Sim.ResultsChanged += RefreshData;
            RegisterCallback<DetachFromPanelEvent>(_ => m_Sim.ResultsChanged -= RefreshData);
        }

        private static string Rate(Row r, float? value) => value.HasValue ? value.Value.ToString("0.00") : "–";

        private void AddColumn(Columns columns, string name, string title, float width, Func<Row, string> text, Func<Row, IComparable> sortKey)
        {
            m_SortKeys[name] = sortKey;
            columns.Add(new Column
            {
                name = name, title = title, width = width, sortable = true,
                makeCell = () => new Label { name = "cell" },
                bindCell = (cell, i) =>
                {
                    var row = m_Rows[i];
                    var label = (Label)cell;
                    label.text = text(row);
                    bool verdict = name == "verdict" && row.sim != null;
                    label.EnableInClassList("cell--good", verdict && row.verdict == BandVerdict.InBand && !row.stale);
                    label.EnableInClassList("cell--bad", (verdict && row.verdict == BandVerdict.Unsolvable) || (name == "critical" && row.critical > 0));
                    label.EnableInClassList("cell--warn", verdict && (row.stale || (row.verdict != BandVerdict.InBand && row.verdict != BandVerdict.Unsolvable)));
                }
            });
        }

        private VisualElement BuildFilters()
        {
            var bar = new VisualElement();
            bar.AddToClassList("analysis__filters");
            var search = new ToolbarSearchField();
            search.style.width = 170;
            search.RegisterValueChangedCallback(e => { m_Search = e.newValue ?? ""; ApplyFilter(); });
            bar.Add(search);
            var tier = new DropdownField(new List<string> { "Tüm zorluklar", "Kolay", "Orta", "Zor" }, 0);
            tier.style.width = 110;
            tier.RegisterValueChangedCallback(_ => { m_TierFilter = tier.index - 1; ApplyFilter(); });
            bar.Add(tier);
            bar.Add(Filter("Bant dışı", v => m_OnlyOutOfBand = v));
            bar.Add(Filter("Kritik hatalı", v => m_OnlyCritical = v));
            bar.Add(Filter("Güçlü Konteyner'li", v => m_OnlyDual = v));
            m_Count = new Label();
            m_Count.AddToClassList("analysis__count");
            bar.Add(m_Count);
            return bar;
        }

        private Toggle Filter(string text, Action<bool> set)
        {
            var toggle = new Toggle(text);
            toggle.AddToClassList("analysis__filter");
            toggle.RegisterValueChangedCallback(e => { set(e.newValue); ApplyFilter(); });
            return toggle;
        }

        public void SetLevels(IList<PixelLevelData> levels, Func<PixelLevelData, int> numberOf)
        {
            m_Levels = levels;
            m_NumberOf = numberOf;
            RefreshData();
        }

        public void SetCurrent(PixelLevelData level)
        {
            m_Current = level;
            RefreshHead();
            int i = m_Rows.FindIndex(r => r.level == level);
            if (i >= 0)
            {
                m_Table.SetSelectionWithoutNotify(new[] { i });
                m_Table.ScrollToItem(i);
            }
            else m_Table.ClearSelection();
        }

        /// <summary>Tablo verisini yeniden hesaplar; ekran görünürken ve simülasyon sonuçları geldikçe çağrılır.</summary>
        public void RefreshData()
        {
            if (m_Levels == null) return;
            m_AllRows.Clear();
            foreach (var level in m_Levels)
            {
                if (level == null) continue;
                var s = LevelSnapshot.From(level);
                var row = new Row { level = level, number = m_NumberOf(level), tier = s.tier, colors = s.colorCount, mystery = s.mysteryCells.Count };
                foreach (int c in s.cells) if (c != -1) row.cubes++;
                var ropes = new HashSet<int>();
                foreach (var w in s.ships)
                {
                    if (w.empty) continue;
                    row.ships++;
                    if (w.link > 0) ropes.Add(w.link);
                    if (w.hidden) row.hidden++;
                    if (w.dual) row.dual++;
                }
                row.ropes = ropes.Count;
                foreach (var f in LevelRules.Check(s))
                {
                    if (f.severity == RuleSeverity.Critical) row.critical++;
                    else if (f.severity == RuleSeverity.Warning) row.warning++;
                }
                row.sim = m_Sim.Get(level, s, out row.stale);
                row.verdict = row.sim != null ? DifficultyBands.Judge(s.tier, row.sim.random, row.sim.smart, row.sim.solvable) : BandVerdict.NoData;
                m_AllRows.Add(row);
            }
            ApplyFilter();
            RefreshHead();
        }

        private void ApplyFilter()
        {
            m_Rows.Clear();
            string term = m_Search.Trim().ToLowerInvariant();
            foreach (var r in m_AllRows)
            {
                if (term.Length > 0 && !r.level.LevelName.ToLowerInvariant().Contains(term) && r.number.ToString() != term) continue;
                if (m_TierFilter >= 0 && r.tier != m_TierFilter) continue;
                if (m_OnlyOutOfBand && (r.sim == null || r.verdict == BandVerdict.InBand)) continue;
                if (m_OnlyCritical && r.critical == 0) continue;
                if (m_OnlyDual && r.dual == 0) continue;
                m_Rows.Add(r);
            }
            ApplySort();
            m_Table.RefreshItems();
            int measured = 0, inBand = 0;
            foreach (var r in m_Rows)
            {
                if (r.sim == null) continue;
                measured++;
                if (r.verdict == BandVerdict.InBand) inBand++;
            }
            m_Count.text = $"{m_Rows.Count} level · {measured} ölçüldü · {inBand} bantta";
            int current = m_Rows.FindIndex(r => r.level == m_Current);
            if (current >= 0) m_Table.SetSelectionWithoutNotify(new[] { current });
        }

        private void ApplySort()
        {
            foreach (var sort in m_Table.sortedColumns)
            {
                if (!m_SortKeys.TryGetValue(sort.columnName, out var key)) continue;
                int dir = sort.direction == SortDirection.Ascending ? 1 : -1;
                m_Rows.Sort((a, b) =>
                {
                    int c = key(a).CompareTo(key(b));
                    return c != 0 ? c * dir : a.number.CompareTo(b.number);
                });
                return;
            }
            m_Rows.Sort((a, b) => a.number.CompareTo(b.number));
        }

        private void RefreshHead()
        {
            bool available = SimulatorService.IsAvailable;
            bool busy = m_Sim.IsBusy;
            m_RunOne.SetEnabled(available && m_Current != null && !m_Sim.IsRunning(m_Current));
            m_RunAll.SetEnabled(available && !busy);
            m_Stop.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
            m_Progress.style.display = busy || !string.IsNullOrEmpty(m_Sim.LastError) || !available ? DisplayStyle.Flex : DisplayStyle.None;
            if (!available)
                m_Progress.text = $"Simülatör bulunamadı: {SimulatorService.BinaryPath}. Kurallar ve tablo çalışır, kazanma oranları ölçülemez.";
            else if (busy)
                m_Progress.text = $"Simülasyon sürüyor: {m_Sim.Finished}/{m_Sim.TotalQueued}. Çalışmaya devam edebilirsin.";
            else
                m_Progress.text = "Son hata: " + m_Sim.LastError;
            m_Progress.EnableInClassList("analysis__progress--bad", !available || (!busy && !string.IsNullOrEmpty(m_Sim.LastError)));

            m_Metrics.Clear();
            if (m_Current == null) { m_Title.text = "Level seçilmedi"; m_Note.text = ""; return; }
            var row = m_AllRows.Find(r => r.level == m_Current);
            if (row == null) return;
            m_Title.text = $"Level {row.number}  {m_Current.LevelName} · {DifficultyBands.TierNames[Mathf.Clamp(row.tier, 0, 2)]}";
            if (row.sim == null)
            {
                m_Note.text = "Bu level henüz ölçülmedi.";
                return;
            }
            DifficultyBands.Get(row.tier, out float rl, out float rh, out float sl, out float sh);
            m_Metrics.Add(Metric("Rastgele oyuncu", row.sim.random, rl, rh));
            m_Metrics.Add(Metric("Dikkatli oyuncu", row.sim.smart, sl, sh));
            m_Metrics.Add(Metric("Hızlı basan oyuncu", row.sim.tapSmart, sl, sh));
            string solvable = row.sim.solvable == 1 ? "Çözülebilir" : row.sim.solvable == 0 ? "ÇÖZÜMSÜZ" : "Çözüm araması sınıra takıldı";
            string age = row.stale ? " Ölçümden sonra sıra değişti, yeniden simüle et." : "";
            m_Note.text = $"{DifficultyBands.VerdictText(row.verdict)} · {solvable}.{age} Gizli küp ve gizli gemi etkisi ölçülmez.";
        }

        /// <summary>Oran göstergesi: açık bölge hedef bant, çizgi ölçülen değer.</summary>
        private static VisualElement Metric(string title, float value, float low, float high)
        {
            var box = new VisualElement();
            box.AddToClassList("metric");
            var head = new VisualElement();
            head.AddToClassList("metric__head");
            head.Add(new Label(title));
            var number = new Label(value.ToString("0.00"));
            number.AddToClassList("metric__value");
            head.Add(number);
            box.Add(head);
            var track = new VisualElement();
            track.AddToClassList("metric__track");
            var band = new VisualElement();
            band.AddToClassList("metric__band");
            band.style.left = Length.Percent(low * 100f);
            band.style.width = Length.Percent((high - low) * 100f);
            track.Add(band);
            var marker = new VisualElement();
            marker.AddToClassList("metric__marker");
            marker.style.left = Length.Percent(Mathf.Clamp01(value) * 100f);
            track.Add(marker);
            box.Add(track);
            var target = new Label($"Hedef {low:0.00}–{high:0.00}");
            target.AddToClassList("metric__target");
            box.Add(target);
            return box;
        }
    }
}
