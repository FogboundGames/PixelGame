using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PixelGame.Editor.Studio
{
    /// <summary>
    /// Gemi Sırası Stüdyosu: level listesi, sütun ızgarası, seçili geminin ayarları ve kural bulguları.
    /// Eski Level Designer ile aynı asset'leri kullanır. Level sırasını (LevelSequence) değiştirmez.
    /// </summary>
    public sealed class SequenceStudioWindow : EditorWindow
    {
        private const string LevelSequencePath = "Assets/Levels/LevelSequence.asset";
        private const string StylePath = "Assets/Editor/LevelDesignStudio/Styles/Studio.uss";
        private const string SelectedKey = "PixelGame_Studio_SelectedLevel";

        // Fırça: palet sırası, ya da özel değerler
        private const int BrushNone = -1, BrushEraser = -2, BrushDual = -3;

        private readonly SequenceEditService m_Edit = new SequenceEditService();
        private readonly List<PixelLevelData> m_AllLevels = new List<PixelLevelData>();
        private readonly List<PixelLevelData> m_Visible = new List<PixelLevelData>();
        private List<RuleFinding> m_Findings = new List<RuleFinding>();
        private readonly Dictionary<PixelLevelData, int> m_LevelNumbers = new Dictionary<PixelLevelData, int>();

        private ListView m_List;
        private LaneBoardPanel m_Board;
        private InspectorPanel m_Inspector;
        private VisualElement m_Palette, m_Issues;
        private Label m_Title, m_SaveState, m_Summary;
        private Button m_SaveButton;
        private string m_Search = "";
        private int m_Selected = -1, m_LinkFrom = -1, m_Brush = BrushNone;

        [MenuItem("Tools/PixelGame/🚢 Gemi Sırası Stüdyosu", priority = 2)]
        public static void Open()
        {
            var w = GetWindow<SequenceStudioWindow>("Gemi Sırası Stüdyosu");
            w.minSize = new Vector2(980, 560);
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            m_Edit.Changed += OnEdited;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            m_Edit.Changed -= OnEdited;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (sheet != null) root.styleSheets.Add(sheet);
            root.AddToClassList("studio");
            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            root.Add(BuildToolbar());

            var body = new VisualElement();
            body.AddToClassList("studio__body");
            root.Add(body);
            body.Add(BuildLevelList());

            var center = new VisualElement();
            center.AddToClassList("studio__center");
            m_Palette = new VisualElement();
            m_Palette.AddToClassList("palette");
            center.Add(m_Palette);
            m_Board = new LaneBoardPanel();
            m_Board.CardClicked += OnCardClicked;
            m_Board.SwapRequested += (a, b) =>
            {
                m_Edit.Swap(a, b);
                m_Selected = b;
                RefreshAll();
            };
            m_Board.MoveToEndRequested += i =>
            {
                m_Edit.MoveToEnd(i);
                m_Selected = m_Edit.Sequence.Count - 1;
                ShowNotification(new GUIContent("Gemi sıranın sonuna taşındı, aradakiler bir sıra kaydı"));
                RefreshAll();
            };
            center.Add(m_Board);
            body.Add(center);

            m_Inspector = new InspectorPanel(m_Edit);
            m_Inspector.LinkModeRequested += i =>
            {
                m_LinkFrom = i;
                ShowNotification(new GUIContent($"#{i + 1} için bağlanacak gemiye tıkla (Esc iptal)"));
                RefreshAll();
            };
            m_Inspector.Notify += text => ShowNotification(new GUIContent(text));
            body.Add(m_Inspector);

            m_Issues = new ScrollView();
            m_Issues.AddToClassList("issues");
            root.Add(m_Issues);

            LoadLevels();
        }

        private VisualElement BuildToolbar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("toolbar");
            m_Title = new Label();
            m_Title.AddToClassList("toolbar__title");
            bar.Add(m_Title);
            m_SaveState = new Label();
            m_SaveState.AddToClassList("toolbar__state");
            bar.Add(m_SaveState);
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            bar.Add(spacer);
            m_Summary = new Label();
            m_Summary.AddToClassList("toolbar__summary");
            bar.Add(m_Summary);
            m_SaveButton = new Button(() => { m_Edit.Save(); RefreshState(); }) { text = "Kaydet" };
            bar.Add(m_SaveButton);
            return bar;
        }

        private VisualElement BuildLevelList()
        {
            var panel = new VisualElement();
            panel.AddToClassList("levels");
            var search = new ToolbarSearchField();
            search.style.width = StyleKeyword.Auto;
            search.RegisterValueChangedCallback(e => { m_Search = e.newValue ?? ""; ApplyFilter(); });
            panel.Add(search);

            m_List = new ListView(m_Visible, 44, MakeLevelRow, BindLevelRow) { selectionType = SelectionType.Single };
            m_List.style.flexGrow = 1;
            m_List.selectionChanged += items =>
            {
                foreach (var item in items) { SelectLevel((PixelLevelData)item); break; }
            };
            panel.Add(m_List);
            return panel;
        }

        private static VisualElement MakeLevelRow()
        {
            var row = new VisualElement();
            row.AddToClassList("level-row");
            var thumb = new Image { name = "thumb", scaleMode = ScaleMode.ScaleToFit };
            thumb.AddToClassList("level-row__thumb");
            row.Add(thumb);
            var text = new VisualElement();
            text.style.flexGrow = 1;
            var title = new Label { name = "title" };
            title.AddToClassList("level-row__title");
            text.Add(title);
            var sub = new Label { name = "sub" };
            sub.AddToClassList("level-row__sub");
            text.Add(sub);
            row.Add(text);
            return row;
        }

        private void BindLevelRow(VisualElement row, int i)
        {
            var level = m_Visible[i];
            row.Q<Image>("thumb").image = level.GetActiveTexture();
            m_LevelNumbers.TryGetValue(level, out int number);
            row.Q<Label>("title").text = $"{number}  {level.LevelName}";
            string[] tiers = { "Kolay", "Orta", "Zor" };
            int cubes = level.GetTotalCubeCountInPalette();
            row.Q<Label>("sub").text = $"{tiers[Mathf.Clamp((int)level.DifficultyTier, 0, 2)]} · {cubes} küp · {level.WagonSequence?.Count ?? 0} gemi";
        }

        private void LoadLevels()
        {
            m_AllLevels.Clear();
            m_LevelNumbers.Clear();
            var sequence = AssetDatabase.LoadAssetAtPath<LevelSequence>(LevelSequencePath);
            if (sequence != null)
                foreach (var level in sequence.Levels)
                    if (level != null)
                    {
                        m_AllLevels.Add(level);
                        m_LevelNumbers[level] = m_AllLevels.Count;
                    }
            ApplyFilter();

            string saved = SessionState.GetString(SelectedKey, "");
            var previous = string.IsNullOrEmpty(saved) ? null : AssetDatabase.LoadAssetAtPath<PixelLevelData>(AssetDatabase.GUIDToAssetPath(saved));
            var start = previous != null && m_AllLevels.Contains(previous) ? previous : (m_AllLevels.Count > 0 ? m_AllLevels[0] : null);
            if (start == null) { RefreshAll(); return; }
            SelectLevel(start);
            int idx = m_Visible.IndexOf(start);
            if (idx >= 0)
            {
                m_List.SetSelectionWithoutNotify(new[] { idx });
                m_List.ScrollToItem(idx);
            }
        }

        private void ApplyFilter()
        {
            m_Visible.Clear();
            string term = m_Search.Trim().ToLowerInvariant();
            foreach (var level in m_AllLevels)
            {
                m_LevelNumbers.TryGetValue(level, out int number);
                if (term.Length == 0 || level.LevelName.ToLowerInvariant().Contains(term) || number.ToString() == term)
                    m_Visible.Add(level);
            }
            m_List.RefreshItems();
        }

        private void SelectLevel(PixelLevelData level)
        {
            if (level == m_Edit.Level) return;
            m_Edit.SetLevel(level);
            m_Selected = -1;
            m_LinkFrom = -1;
            m_Brush = BrushNone;
            string path = AssetDatabase.GetAssetPath(level);
            SessionState.SetString(SelectedKey, AssetDatabase.AssetPathToGUID(path));
            RefreshAll();
        }

        private void OnEdited() => RefreshAll();

        private void OnUndoRedo()
        {
            m_LinkFrom = -1;
            if (m_Edit.Sequence != null && m_Selected >= m_Edit.Sequence.Count) m_Selected = -1;
            RefreshAll();
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            if (e.keyCode != KeyCode.Escape) return;
            m_LinkFrom = -1;
            m_Brush = BrushNone;
            RefreshAll();
            e.StopPropagation();
        }

        private void OnCardClicked(int index)
        {
            var seq = m_Edit.Sequence;
            if (seq == null) return;
            bool empty = index >= seq.Count;

            if (m_LinkFrom >= 0)
            {
                int from = m_LinkFrom;
                m_LinkFrom = -1;
                if (empty || index == from) { RefreshAll(); return; }
                if (seq[index].isDualColor || seq[index].isHidden)
                {
                    ShowNotification(new GUIContent("Güçlü ya da gizli gemi halatlanmaz"));
                    RefreshAll();
                    return;
                }
                if (!m_Edit.Link(from, index)) ShowNotification(new GUIContent("Bu iki gemi bağlanamadı: bir halata en fazla 2 gemi"));
                RefreshAll();
                return;
            }

            if (m_Brush == BrushEraser)
            {
                if (!empty) m_Edit.Remove(index);
                return;
            }
            if (m_Brush == BrushDual)
            {
                if (!empty && !m_Edit.MakeDual(index)) ShowNotification(new GUIContent("Güçlü Konteyner için bölümde en az 2 farklı renk olmalı"));
                return;
            }
            if (m_Brush >= 0)
            {
                if (empty) m_Edit.AddAtEnd(m_Brush);
                else m_Edit.SetColor(index, m_Brush);
                return;
            }

            if (empty)
            {
                int color = NextMissingColor();
                if (color >= 0)
                {
                    m_Edit.AddAtEnd(color);
                    m_Selected = seq.Count - 1;
                    RefreshAll();
                }
                return;
            }
            m_Selected = index == m_Selected ? -1 : index;
            RefreshAll();
        }

        /// <summary>Kapasitesi eksik ilk renk; hepsi tamamsa paletin ilk rengi.</summary>
        private int NextMissingColor()
        {
            var palette = m_Edit.Level.ColorPalette;
            int first = -1;
            for (int p = 0; p < palette.Count; p++)
            {
                var e = palette[p];
                if (e == null || e.pixelCount <= 0) continue;
                if (first < 0) first = p;
                if (m_Edit.Level.GetTotalAssignedCapacityForColor(e.targetColor) < e.pixelCount) return p;
            }
            return first;
        }

        private void RefreshAll()
        {
            if (m_Board == null) return;
            var level = m_Edit.Level;
            int columns = level != null ? Mathf.Max(1, level.PoolColumns) : 4;
            m_Findings = level != null ? LevelRules.Check(LevelSnapshot.From(level)) : new List<RuleFinding>();
            m_Board.Rebuild(m_Edit.Sequence, columns, level != null ? Mathf.Max(1, level.PoolRows) : 3, m_Selected, m_LinkFrom, m_Findings);
            m_Inspector.Show(m_Selected, columns);
            RefreshPalette();
            RefreshIssues();
            RefreshState();
        }

        private void RefreshState()
        {
            var level = m_Edit.Level;
            if (level == null) { m_Title.text = "Level yok"; m_SaveState.text = ""; m_Summary.text = ""; return; }
            m_LevelNumbers.TryGetValue(level, out int number);
            m_Title.text = $"Level {number}  {level.LevelName}";
            bool dirty = EditorUtility.IsDirty(level);
            m_SaveState.text = dirty ? "● Kaydedilmedi" : "Kaydedildi";
            m_SaveState.EnableInClassList("toolbar__state--dirty", dirty);
            m_SaveButton.SetEnabled(dirty);
            int critical = 0, warning = 0;
            foreach (var f in m_Findings)
            {
                if (f.severity == RuleSeverity.Critical) critical++;
                else if (f.severity == RuleSeverity.Warning) warning++;
            }
            m_Summary.text = (critical == 0 ? "Yayına hazır" : "Yayına hazır değil") + $" · {critical} kritik · {warning} uyarı";
            m_Summary.EnableInClassList("toolbar__summary--bad", critical > 0);
        }

        private void RefreshPalette()
        {
            m_Palette.Clear();
            var level = m_Edit.Level;
            if (level == null) return;
            var palette = level.ColorPalette;
            for (int p = 0; p < palette.Count; p++)
            {
                var e = palette[p];
                if (e == null || e.pixelCount <= 0) continue;
                int assigned = level.GetTotalAssignedCapacityForColor(e.targetColor);
                int index = p;
                var chip = BrushChip($"{assigned}/{e.pixelCount}", index, () => m_Brush = m_Brush == index ? BrushNone : index);
                chip.tooltip = $"{StudioColors.Name(e.targetColor, p)}: fırça. Karta tıklayınca rengi değişir, boş yere tıklayınca gemi ekler.";
                var swatch = new VisualElement();
                swatch.AddToClassList("swatch");
                swatch.style.backgroundColor = e.targetColor;
                chip.Insert(0, swatch);
                chip.EnableInClassList("chip--short", assigned < e.pixelCount);
                chip.EnableInClassList("chip--over", assigned > e.pixelCount);
                m_Palette.Add(chip);
            }
            var dual = BrushChip("Güçlü", BrushDual, () => m_Brush = m_Brush == BrushDual ? BrushNone : BrushDual);
            dual.tooltip = "Fırça: tıklanan gemi Güçlü Konteyner olur";
            m_Palette.Add(dual);
            var eraser = BrushChip("Silgi", BrushEraser, () => m_Brush = m_Brush == BrushEraser ? BrushNone : BrushEraser);
            eraser.tooltip = "Fırça: tıklanan gemi silinir, sonrakiler bir sıra kayar";
            m_Palette.Add(eraser);
        }

        private Button BrushChip(string text, int brush, System.Action toggle)
        {
            var chip = new Button(() => { toggle(); m_Selected = -1; RefreshAll(); });
            chip.AddToClassList("chip");
            chip.Add(new Label(text));
            chip.EnableInClassList("chip--active", m_Brush == brush);
            return chip;
        }

        private void RefreshIssues()
        {
            m_Issues.Clear();
            if (m_Findings.Count == 0)
            {
                m_Issues.Add(new Label("Bu level için bulgu yok.") { name = "none" });
                return;
            }
            foreach (var f in m_Findings)
            {
                var row = new VisualElement();
                row.AddToClassList("issue");
                row.AddToClassList(f.severity == RuleSeverity.Critical ? "issue--critical" : f.severity == RuleSeverity.Warning ? "issue--warning" : "issue--info");
                string mark = f.severity == RuleSeverity.Critical ? "KRİTİK" : f.severity == RuleSeverity.Warning ? "UYARI" : "BİLGİ";
                var tag = new Label(mark);
                tag.AddToClassList("issue__tag");
                row.Add(tag);
                var text = new Label($"{f.what} {f.fix}") { tooltip = f.why };
                text.AddToClassList("issue__text");
                row.Add(text);
                if (f.slots.Length > 0)
                {
                    int slot = f.slots[0];
                    row.RegisterCallback<ClickEvent>(_ => { m_Selected = slot; RefreshAll(); });
                }
                m_Issues.Add(row);
            }
        }
    }
}
