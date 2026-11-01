using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PixelGame.Editor.Studio
{
    /// <summary>
    /// Level Stüdyosu: solda level listesi; sekmelerde gemi sırası düzenleme, resim ve analiz.
    /// Eski Level Designer ile aynı asset'leri kullanır. Level sırasını (LevelSequence) değiştirmez.
    /// </summary>
    public sealed class SequenceStudioWindow : EditorWindow
    {
        private const string LevelSequencePath = "Assets/Levels/LevelSequence.asset";
        private const string StylePath = "Assets/Editor/LevelDesignStudio/Styles/Studio.uss";
        private const string SelectedKey = "PixelGame_Studio_SelectedLevel";
        private const string InsertModeKey = "PixelGame_Studio_InsertMode";
        private const string TabKey = "PixelGame_Studio_Tab";
        private const int TabSequence = 0, TabAnalysis = 1, TabImage = 2, TabCanvas = 3;

        // Fırça: palet sırası, ya da özel değerler
        private const int BrushNone = -1, BrushEraser = -2, BrushDual = -3;

        private readonly SequenceEditService m_Edit = new SequenceEditService();
        private SimulatorService m_Sim;
        private AnalysisPanel m_Analysis;
        private LevelPreview m_Preview;
        private AutomationPanel m_Automation;
        private Button m_Reroll;
        private VisualElement m_PageSequence;
        private Button m_TabSequence, m_TabAnalysis, m_TabImage;
        private ImagePanel m_Image;
        private CanvasPanel m_Canvas;
        private Button m_TabCanvas;
        private int m_Tab;
        private readonly List<PixelLevelData> m_AllLevels = new List<PixelLevelData>();
        private readonly List<PixelLevelData> m_Visible = new List<PixelLevelData>();
        private List<RuleFinding> m_Findings = new List<RuleFinding>();
        private readonly Dictionary<PixelLevelData, int> m_LevelNumbers = new Dictionary<PixelLevelData, int>();

        private ListView m_List;
        private LaneBoardPanel m_Board;
        private InspectorPanel m_Inspector;
        private VisualElement m_Palette, m_Issues, m_PreviewBar;
        private Label m_PreviewTitle, m_PreviewDetail;
        private Button m_SwapMode, m_InsertMode;
        private bool m_Insert;
        private SequenceEditService.PendingChange m_Pending;
        private Label m_Title, m_SaveState, m_Summary;
        private Button m_SaveButton;
        private string m_Search = "";
        private int m_Selected = -1, m_LinkFrom = -1, m_Brush = BrushNone;

        [MenuItem("Tools/PixelGame/🚢 Level Stüdyosu", priority = 2)]
        public static void Open()
        {
            var w = GetWindow<SequenceStudioWindow>("Level Stüdyosu");
            w.minSize = new Vector2(980, 560);
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            m_Edit.Changed += OnEdited;
            AssemblyReloadEvents.beforeAssemblyReload += StopSimulations;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            m_Edit.Changed -= OnEdited;
            AssemblyReloadEvents.beforeAssemblyReload -= StopSimulations;
            StopSimulations();
        }

        private void StopSimulations() => m_Sim?.Shutdown();

        private void CreateGUI()
        {
            m_Sim = new SimulatorService();
            m_Sim.ResultsChanged += () => { if (m_Summary != null) RefreshState(); };
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

            m_PageSequence = new VisualElement();
            m_PageSequence.AddToClassList("studio__page");
            body.Add(m_PageSequence);
            m_Analysis = new AnalysisPanel(m_Sim);
            m_Analysis.LevelPicked += level => SelectAndShow(level, false);
            m_Analysis.LevelOpened += level => SelectAndShow(level, true);
            body.Add(m_Analysis);
            m_Image = new ImagePanel(m_Edit);
            m_Image.CanvasRequested += () => SetTab(TabCanvas);
            body.Add(m_Image);
            m_Canvas = new CanvasPanel(m_Edit);
            m_Canvas.Notify += text => ShowNotification(new GUIContent(text));
            m_Canvas.DirtyChanged += () => hasUnsavedChanges = m_Canvas.IsDirty;
            body.Add(m_Canvas);

            var center = new VisualElement();
            center.AddToClassList("studio__center");
            m_Palette = new VisualElement();
            m_Palette.AddToClassList("palette");
            center.Add(m_Palette);
            m_Automation = new AutomationPanel(m_Edit);
            m_Automation.ProposalReady += StartPreview;
            center.Add(m_Automation);
            center.Add(BuildPreviewBar());
            m_Board = new LaneBoardPanel();
            m_Board.CardClicked += OnCardClicked;
            m_Board.SwapRequested += (a, b) =>
            {
                if (m_Insert) { StartPreview(m_Edit.ProposeInsert(a, b)); return; }
                m_Edit.Swap(a, b);
                m_Selected = b;
                RefreshAll();
            };
            m_Board.MoveToEndRequested += i => StartPreview(m_Edit.ProposeMoveToEnd(i));
            center.Add(m_Board);
            m_PageSequence.Add(center);

            m_Inspector = new InspectorPanel(m_Edit);
            m_Inspector.LinkModeRequested += i =>
            {
                m_LinkFrom = i;
                ShowNotification(new GUIContent($"#{i + 1} için bağlanacak gemiye tıkla (Esc iptal)"));
                RefreshAll();
            };
            m_Inspector.Notify += text => ShowNotification(new GUIContent(text));
            m_Inspector.RemoveRequested += i => StartPreview(m_Edit.ProposeRemove(i));
            var side = new VisualElement();
            side.AddToClassList("studio__side");
            m_Preview = new LevelPreview();
            side.Add(m_Preview);
            side.Add(m_Inspector);
            m_PageSequence.Add(side);

            m_Issues = new ScrollView();
            m_Issues.AddToClassList("issues");
            root.Add(m_Issues);

            LoadLevels();
            m_Analysis.SetLevels(m_AllLevels, level => m_LevelNumbers.TryGetValue(level, out int n) ? n : 0);
            m_Analysis.SetCurrent(m_Edit.Level);
            SetTab(SessionState.GetInt(TabKey, TabSequence));
        }

        private void SetTab(int tab)
        {
            m_Tab = tab;
            SessionState.SetInt(TabKey, tab);
            m_Pending = null;
            bool sequence = tab == TabSequence;
            m_PageSequence.style.display = sequence ? DisplayStyle.Flex : DisplayStyle.None;
            m_Issues.style.display = sequence ? DisplayStyle.Flex : DisplayStyle.None;
            m_Analysis.style.display = tab == TabAnalysis ? DisplayStyle.Flex : DisplayStyle.None;
            m_Image.style.display = tab == TabImage ? DisplayStyle.Flex : DisplayStyle.None;
            m_Canvas.style.display = tab == TabCanvas ? DisplayStyle.Flex : DisplayStyle.None;
            if (tab == TabCanvas) m_Canvas.Sync();
            m_SwapMode.style.display = sequence ? DisplayStyle.Flex : DisplayStyle.None;
            m_InsertMode.style.display = sequence ? DisplayStyle.Flex : DisplayStyle.None;
            m_TabSequence.EnableInClassList("tab--active", sequence);
            m_TabAnalysis.EnableInClassList("tab--active", tab == TabAnalysis);
            m_TabImage.EnableInClassList("tab--active", tab == TabImage);
            m_TabCanvas.EnableInClassList("tab--active", tab == TabCanvas);
            if (tab == TabAnalysis) m_Analysis.RefreshData();
            else RefreshAll();
        }

        /// <summary>Tablodan gelen seçim: soldaki listeyi de eşitler.</summary>
        private void SelectAndShow(PixelLevelData level, bool openSequence)
        {
            SelectLevel(level);
            int i = m_Visible.IndexOf(level);
            if (i >= 0)
            {
                m_List.SetSelectionWithoutNotify(new[] { i });
                m_List.ScrollToItem(i);
            }
            if (openSequence) SetTab(TabSequence);
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
            m_TabSequence = new Button(() => SetTab(TabSequence)) { text = "Gemi Sırası" };
            m_TabSequence.AddToClassList("tab");
            bar.Add(m_TabSequence);
            m_TabAnalysis = new Button(() => SetTab(TabAnalysis)) { text = "Analiz" };
            m_TabAnalysis.AddToClassList("tab");
            bar.Add(m_TabAnalysis);
            m_TabImage = new Button(() => SetTab(TabImage)) { text = "Resim" };
            m_TabImage.AddToClassList("tab");
            bar.Add(m_TabImage);
            m_TabCanvas = new Button(() => SetTab(TabCanvas)) { text = "Tuval" };
            m_TabCanvas.AddToClassList("tab");
            bar.Add(m_TabCanvas);
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            bar.Add(spacer);
            m_Summary = new Label();
            m_Summary.AddToClassList("toolbar__summary");
            bar.Add(m_Summary);
            m_Insert = SessionState.GetBool(InsertModeKey, false);
            m_SwapMode = new Button(() => SetInsertMode(false))
            {
                text = "Takas",
                tooltip = "Sürüklenen gemi hedefle yer değiştirir, başka gemi kaymaz. Hemen uygulanır."
            };
            m_SwapMode.AddToClassList("mode");
            bar.Add(m_SwapMode);
            m_InsertMode = new Button(() => SetInsertMode(true))
            {
                text = "Araya ekle",
                tooltip = "Sürüklenen gemi hedefin yerine girer, aradakiler kayar. Önce önizleme gösterilir."
            };
            m_InsertMode.AddToClassList("mode");
            bar.Add(m_InsertMode);
            m_SaveButton = new Button(() => { m_Edit.Save(); RefreshState(); }) { text = "Kaydet" };
            bar.Add(m_SaveButton);
            return bar;
        }

        private VisualElement BuildPreviewBar()
        {
            m_PreviewBar = new VisualElement();
            m_PreviewBar.AddToClassList("preview");
            var text = new VisualElement();
            text.style.flexGrow = 1;
            text.style.flexShrink = 1;
            m_PreviewTitle = new Label();
            m_PreviewTitle.AddToClassList("preview__title");
            text.Add(m_PreviewTitle);
            m_PreviewDetail = new Label();
            m_PreviewDetail.AddToClassList("preview__detail");
            text.Add(m_PreviewDetail);
            m_PreviewBar.Add(text);
            var apply = new Button(ApplyPreview) { text = "Uygula" };
            apply.AddToClassList("preview__apply");
            m_PreviewBar.Add(apply);
            m_Reroll = new Button(() => StartPreview(m_Pending?.reroll?.Invoke())) { text = "Yeniden üret" };
            m_PreviewBar.Add(m_Reroll);
            m_PreviewBar.Add(new Button(CancelPreview) { text = "Vazgeç" });
            m_PreviewBar.style.display = DisplayStyle.None;
            return m_PreviewBar;
        }

        private void SetInsertMode(bool insert)
        {
            m_Insert = insert;
            SessionState.SetBool(InsertModeKey, insert);
            RefreshModeButtons();
        }

        private void RefreshModeButtons()
        {
            m_SwapMode.EnableInClassList("mode--active", !m_Insert);
            m_InsertMode.EnableInClassList("mode--active", m_Insert);
        }

        private void StartPreview(SequenceEditService.PendingChange change)
        {
            if (change == null) return;
            m_Pending = change;
            m_Selected = -1;
            m_LinkFrom = -1;
            RefreshAll();
        }

        private void ApplyPreview()
        {
            var change = m_Pending;
            if (change == null) return;
            m_Pending = null;
            m_Selected = change.focusIndex;
            change.apply();
            RefreshAll();
        }

        private void CancelPreview()
        {
            m_Pending = null;
            RefreshAll();
        }

        private static readonly Dictionary<string, string> s_RuleTitles = new Dictionary<string, string>
        {
            { "ROPE_POSITION", "komşu olmayan halat" }, { "ROPE_SAME_COLOR", "aynı renkli halat" }, { "ROPE_SIZE", "eksik halat" },
            { "SAME_COLOR_IN_LANE", "sütunda arka arkaya aynı renk" }, { "HIDDEN_IN_FRONT", "ön sırada gizli gemi" },
            { "FRONT_OUTER", "ön sırada dış katman" }, { "OPEN_OUTER", "ilk iki sırada dış katman" },
            { "OPENING_ROPES", "açılışta fazla halat" }, { "FRONT_ROPES", "ön sırada fazla halat" },
            { "CAPACITY_MISMATCH", "kapasite uyuşmazlığı" }, { "SEQUENCE_EMPTY", "boş sıra" }
        };

        /// <summary>Şimdiki ve önerilen sıra arasındaki fark: sütunu değişen gemi sayısı ve artan/azalan bulgular.</summary>
        private static bool SameShip(WagonSequenceEntry a, WagonSequenceEntry b) =>
            a != null && b != null && a.wagonColor == b.wagonColor && a.capacity == b.capacity && a.linkId == b.linkId && a.isHidden == b.isHidden &&
            a.isDualColor == b.isDualColor && (!a.isDualColor || (a.secondaryColor == b.secondaryColor && a.secondaryCapacity == b.secondaryCapacity));

        private string DescribePreview(List<RuleFinding> before, List<RuleFinding> after, int movedCount, string regenerated)
        {
            var delta = new SortedDictionary<string, int>();
            foreach (var f in after) { delta.TryGetValue(f.code, out int n); delta[f.code] = n + 1; }
            foreach (var f in before) { delta.TryGetValue(f.code, out int n); delta[f.code] = n - 1; }
            var worse = new List<string>();
            var better = new List<string>();
            foreach (var kv in delta)
            {
                if (kv.Value == 0 || !s_RuleTitles.TryGetValue(kv.Key, out string title)) continue;
                if (kv.Value > 0) worse.Add($"+{kv.Value} {title}");
                else better.Add($"{kv.Value} {title}");
            }
            string text = regenerated != null
                ? $"{regenerated}, {movedCount} kart değişiyor (mavi). Şimdiki sıranın yerine geçer."
                : movedCount == 0 ? "Hiçbir gemi sütun değiştirmiyor." : $"{movedCount} gemi sütun değiştiriyor (mavi).";
            if (worse.Count > 0) text += "  Kötüleşen: " + string.Join(", ", worse) + ".";
            if (better.Count > 0) text += "  İyileşen: " + string.Join(", ", better) + ".";
            if (worse.Count == 0 && better.Count == 0) text += "  Kural bulguları değişmiyor.";
            return text;
        }

        private VisualElement BuildLevelList()
        {
            var panel = new VisualElement();
            panel.AddToClassList("levels");
            var search = new ToolbarSearchField();
            search.style.width = StyleKeyword.Auto;
            search.RegisterValueChangedCallback(e => { m_Search = e.newValue ?? ""; ApplyFilter(); });
            panel.Add(search);
            panel.Add(BuildCreateZone());

            m_List = new ListView(m_Visible, 44, MakeLevelRow, BindLevelRow) { selectionType = SelectionType.Single };
            m_List.style.flexGrow = 1;
            m_List.selectionChanged += items =>
            {
                foreach (var item in items) { SelectLevel((PixelLevelData)item); break; }
            };
            panel.Add(m_List);
            return panel;
        }

        private VisualElement BuildCreateZone()
        {
            var zone = new VisualElement();
            zone.AddToClassList("create-zone");
            var drop = new Label("Görsel bırak: yeni level")
            {
                tooltip = "Bir resim bırakınca ondan level üretilir ve sona eklenir. Klasör bırakınca Toplu Üretici açılır."
            };
            drop.AddToClassList("create-zone__drop");
            drop.RegisterCallback<DragUpdatedEvent>(_ => DragAndDrop.visualMode = DragAndDropVisualMode.Copy);
            drop.RegisterCallback<DragPerformEvent>(_ =>
            {
                DragAndDrop.AcceptDrag();
                Texture2D created = null;
                foreach (var dragged in DragAndDrop.objectReferences)
                {
                    if (dragged is DefaultAsset folder)
                    {
                        BatchLevelGeneratorWindow.OpenWithFolder(folder);
                        break;
                    }
                    if (dragged is Texture2D texture)
                    {
                        LevelCreationService.CreateFromTexture(texture);
                        created = texture;
                    }
                }
                if (created != null) ReloadAfterCreate();
            });
            zone.Add(drop);
            var buttons = new VisualElement();
            buttons.AddToClassList("automation__row");
            buttons.Add(new Button(() =>
            {
                LevelCreationService.CreateEmpty();
                ReloadAfterCreate();
            }) { text = "Boş level" });
            buttons.Add(new Button(BatchLevelGeneratorWindow.OpenWindow) { text = "Toplu üretici" });
            zone.Add(buttons);
            return zone;
        }

        /// <summary>Yeni level sıranın sonuna eklenir: listeyi yenileyip onu seçer.</summary>
        private void ReloadAfterCreate()
        {
            LoadLevels();
            m_Analysis.RefreshData();
            if (m_AllLevels.Count > 0) SelectAndShow(m_AllLevels[m_AllLevels.Count - 1], false);
        }

        private void OnFocus()
        {
            // Tuval ya da eski araçta yapılan değişiklikleri göster
            if (m_Board == null || m_Pending != null) return;
            if (m_Tab == TabCanvas) m_Canvas.Sync();
            RefreshAll();
            m_List?.RefreshItems();
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
            ResolveCanvasDraft();
            m_Pending = null;
            m_Image?.CancelProposal();
            m_Edit.SetLevel(level);
            m_Selected = -1;
            m_LinkFrom = -1;
            m_Brush = BrushNone;
            string path = AssetDatabase.GetAssetPath(level);
            SessionState.SetString(SelectedKey, AssetDatabase.AssetPathToGUID(path));
            m_Analysis?.SetCurrent(level);
            if (m_Tab == TabCanvas) m_Canvas.Sync();
            RefreshAll();
        }

        /// <summary>Level değişmeden önce uygulanmamış tuval çizimi için karar sorar.</summary>
        private void ResolveCanvasDraft()
        {
            if (m_Canvas == null || !m_Canvas.IsDirty) return;
            bool apply = EditorUtility.DisplayDialog("Tuvalde uygulanmamış çizim var",
                "Level değişmeden önce çizim ne olsun? Uygula: gemi sırası korunur.", "Uygula", "Çizimi at");
            if (apply) m_Canvas.Apply(false);
            else m_Canvas.Discard();
        }

        public override void SaveChanges()
        {
            m_Canvas?.Apply(false);
            base.SaveChanges();
        }

        private void OnEdited() => RefreshAll();

        private void OnUndoRedo()
        {
            m_Canvas?.OnUndoRedo();
            m_Pending = null;
            m_LinkFrom = -1;
            if (m_Edit.Sequence != null && m_Selected >= m_Edit.Sequence.Count) m_Selected = -1;
            RefreshAll();
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            if (e.keyCode != KeyCode.Escape) return;
            m_Canvas?.CancelInteraction();
            m_Pending = null;
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
                if (!empty)
                {
                    m_Edit.Remove(index);
                    ShowNotification(new GUIContent($"#{index + 1} silindi, sonraki gemiler bir sıra kaydı (Ctrl+Z geri alır)"));
                }
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
            int poolRows = level != null ? Mathf.Max(1, level.PoolRows) : 3;
            RefreshModeButtons();
            m_Findings = level != null ? LevelRules.Check(LevelSnapshot.From(level)) : new List<RuleFinding>();
            RefreshPreview();
            if (m_Pending != null && level != null)
            {
                // Önizleme: pano ve bulgular önerilen sırayı gösterir, asset'e dokunulmaz
                var after = LevelRules.Check(LevelSnapshot.From(level, m_Pending.order, m_Pending.droppedLink));
                var current = m_Edit.Sequence;
                var moved = new HashSet<int>();
                for (int i = 0; i < m_Pending.order.Count; i++)
                {
                    if (m_Pending.regenerated)
                    {
                        if (i >= current.Count || !SameShip(current[i], m_Pending.order[i])) moved.Add(i);
                    }
                    else if (current.IndexOf(m_Pending.order[i]) % columns != i % columns) moved.Add(i);
                }
                m_PreviewTitle.text = m_Pending.title;
                m_PreviewDetail.text = DescribePreview(m_Findings, after, moved.Count, m_Pending.regenerated ? $"{current.Count} gemi → {m_Pending.order.Count} gemi" : null);
                m_Reroll.style.display = m_Pending.reroll != null ? DisplayStyle.Flex : DisplayStyle.None;
                m_PreviewBar.style.display = DisplayStyle.Flex;
                m_Findings = after;
                m_Board.Rebuild(m_Pending.order, columns, poolRows, -1, -1, m_Findings, moved);
                m_Inspector.Show(-1, columns);
            }
            else
            {
                m_PreviewBar.style.display = DisplayStyle.None;
                m_Board.Rebuild(m_Edit.Sequence, columns, poolRows, m_Selected, m_LinkFrom, m_Findings);
                m_Inspector.Show(m_Selected, columns);
            }
            m_Automation.Refresh();
            if (m_Tab == TabImage) m_Image?.Refresh();
            m_Automation.SetEnabled(m_Pending == null && level != null);
            m_Palette.SetEnabled(m_Pending == null);
            m_Inspector.SetEnabled(m_Pending == null);
            RefreshPalette();
            RefreshIssues();
            RefreshState();
        }

        /// <summary>Seçili geminin ya da etkin fırçanın renkleri resimde vurgulanır.</summary>
        private void RefreshPreview()
        {
            var level = m_Edit.Level;
            if (level == null) { m_Preview.Show(null, null, null); return; }
            var palette = level.ColorPalette;
            var highlight = new HashSet<int>();
            var seq = m_Edit.Sequence;
            if (m_Pending == null && seq != null && m_Selected >= 0 && m_Selected < seq.Count && seq[m_Selected] != null)
            {
                var w = seq[m_Selected];
                int first = LevelSnapshot.FindColor(palette, w.wagonColor);
                if (first >= 0) highlight.Add(first);
                if (w.isDualColor)
                {
                    int second = LevelSnapshot.FindColor(palette, w.secondaryColor);
                    if (second >= 0) highlight.Add(second);
                }
            }
            else if (m_Pending == null && m_Brush >= 0) highlight.Add(m_Brush);
            m_Preview.Show(LevelSnapshot.From(level), palette, highlight);
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
            bool stale = false;
            var sim = m_Sim != null ? m_Sim.Get(level, LevelSnapshot.From(level), out stale) : null;
            bool unsolvable = sim != null && !stale && sim.solvable == 0;
            string simText = sim == null ? "ölçülmedi" : $"{sim.random:0.00} / {sim.smart:0.00}" + (stale ? " (eski)" : "");
            m_Summary.text = (critical == 0 && !unsolvable ? "Yayına hazır" : "Yayına hazır değil") + $" · {critical} kritik · {warning} uyarı · Sim: {simText}";
            m_Summary.EnableInClassList("toolbar__summary--bad", critical > 0 || unsolvable);
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
                    row.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (m_Pending != null) return;
                        m_Selected = slot;
                        RefreshAll();
                    });
                }
                m_Issues.Add(row);
            }
        }
    }
}
