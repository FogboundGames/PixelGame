using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PixelGame.Editor.Studio
{
    /// <summary>
    /// Piksel tuvali. Çizim önce belleğe yapılır, Uygula'ya basılana kadar level'a ve diske yazılmaz. Geri alma Unity Undo ile
    /// çalışır (LevelCanvasUndoState). Uygularken gemi sırası korunabilir; renk başına küp ve gemi kapasitesi canlı gösterilir.
    /// </summary>
    public sealed class CanvasPanel : VisualElement
    {
        private enum Tool { Pencil, Eraser, Fill, Line, Rectangle, Ellipse, Picker, Replace }

        // Oyunun ayırt ettiği renk farkı 0.04; kanal başına 14/255'ten yakın tonları oyuncu da ayıramaz (REHBER kural 3)
        private const float SimilarColorChannel = 14f / 255f;
        private const int DefaultSize = 32;

        private readonly SequenceEditService m_Edit;
        public event Action<string> Notify;
        public event Action DirtyChanged;

        private readonly CanvasSurface m_Surface = new CanvasSurface();
        private readonly Dictionary<Tool, Button> m_ToolButtons = new Dictionary<Tool, Button>();
        private readonly VisualElement m_Palette = new VisualElement(), m_Warnings = new VisualElement(), m_ApplyBar = new VisualElement();
        private readonly Image m_Thumb = new Image { scaleMode = ScaleMode.ScaleToFit };
        private readonly Label m_Status = new Label(), m_Hover = new Label();
        private readonly ColorField m_ColorField = new ColorField { showAlpha = false, hdr = false };
        private readonly IntegerField m_WidthField = new IntegerField("G") { isDelayed = true }, m_HeightField = new IntegerField("Y") { isDelayed = true };
        private readonly Toggle m_MirrorX = new Toggle("Yatay simetri"), m_MirrorY = new Toggle("Dikey simetri"), m_Filled = new Toggle("Dolu şekil"), m_Grid = new Toggle("Izgara") { value = true }, m_ShowRef = new Toggle("Orijinal");
        private readonly SliderInt m_BrushSize = new SliderInt("Kalınlık", 1, 5) { showInputField = true };
        private readonly Slider m_RefOpacity = new Slider(0.1f, 1f) { value = 0.5f };

        private LevelCanvas m_Canvas;
        private LevelCanvasUndoState m_UndoState;
        private Texture2D m_Texture;
        private PixelLevelData m_Level;
        private Texture2D m_LoadedFrom;
        private byte[] m_BaseCells;
        private Color[] m_BasePalette;
        private int m_BaseWidth, m_BaseHeight;

        private Tool m_Tool = Tool.Pencil, m_ToolBeforePicker = Tool.Pencil;
        private byte m_Brush = 1;
        private bool m_Erasing, m_Stroking;
        private Vector2Int m_ShapeStart, m_LastStroke;
        private readonly List<Vector2Int> m_LineBuffer = new List<Vector2Int>();
        private readonly HashSet<Vector2Int> m_StampBuffer = new HashSet<Vector2Int>();

        public bool IsDirty { get; private set; }

        public CanvasPanel(SequenceEditService edit)
        {
            m_Edit = edit;
            AddToClassList("canvas-panel");
            focusable = true;

            Add(BuildTools());

            var center = new VisualElement();
            center.AddToClassList("canvas-panel__center");
            center.Add(BuildTopBar());
            m_Surface.style.flexGrow = 1;
            m_Surface.CellDown += OnCellDown;
            m_Surface.CellDrag += OnCellDrag;
            m_Surface.CellUp += OnCellUp;
            m_Surface.CellHover += OnHover;
            center.Add(m_Surface);
            center.Add(BuildApplyBar());
            Add(center);

            Add(BuildSide());
            RegisterCallback<KeyDownEvent>(OnKey);
            RegisterCallback<DetachFromPanelEvent>(_ => Release());
        }

        /// <summary>Esc: yarım kalan şekli bırakır.</summary>
        public void CancelInteraction()
        {
            if (!m_Stroking) return;
            bool shape = m_Tool == Tool.Line || m_Tool == Tool.Rectangle || m_Tool == Tool.Ellipse;
            if (shape)
            {
                m_Stroking = false;
                m_Surface.SetPreview(null, Color.clear);
            }
        }

        private void Release()
        {
            if (m_Texture != null) UnityEngine.Object.DestroyImmediate(m_Texture);
            m_Texture = null;
            if (m_UndoState != null) UnityEngine.Object.DestroyImmediate(m_UndoState);
            m_UndoState = null;
        }

        // ------------------------------------------------------------------ arayüz

        private VisualElement BuildTools()
        {
            var bar = new VisualElement();
            bar.AddToClassList("canvas-panel__tools");
            AddTool(bar, Tool.Pencil, "Kalem", "B: çizer. Sağ tuş siler, Alt+tık renk alır.");
            AddTool(bar, Tool.Eraser, "Silgi", "E: küpü siler");
            AddTool(bar, Tool.Fill, "Kova", "G: bağlı aynı renkli alanı doldurur");
            AddTool(bar, Tool.Line, "Çizgi", "L: sürükleyerek düz çizgi");
            AddTool(bar, Tool.Rectangle, "Kare", "U: sürükleyerek dikdörtgen");
            AddTool(bar, Tool.Ellipse, "Daire", "O: sürükleyerek elips");
            AddTool(bar, Tool.Picker, "Damlalık", "I: tıklanan rengi seçer");
            AddTool(bar, Tool.Replace, "Renk değiş", "R: tıklanan rengin bütün küplerini seçili renge çevirir");

            m_BrushSize.value = 1;
            m_BrushSize.tooltip = "Kalem ve silgi kalınlığı. [ ve ] tuşları.";
            bar.Add(m_BrushSize);
            m_Filled.tooltip = "Kare ve daire içi dolu çizilir";
            bar.Add(m_Filled);
            m_MirrorX.tooltip = "M: sol-sağ aynalı çizer";
            bar.Add(m_MirrorX);
            bar.Add(m_MirrorY);
            return bar;
        }

        private void AddTool(VisualElement bar, Tool tool, string text, string tip)
        {
            var button = new Button(() => SetTool(tool)) { text = text, tooltip = tip };
            button.AddToClassList("canvas-tool");
            m_ToolButtons[tool] = button;
            bar.Add(button);
        }

        private VisualElement BuildTopBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("canvas-panel__top");
            m_WidthField.tooltip = "Tuval genişliği (4–64)";
            m_HeightField.tooltip = "Tuval yüksekliği (4–64)";
            m_WidthField.RegisterValueChangedCallback(e => Resize(e.newValue, m_Canvas != null ? m_Canvas.Height : DefaultSize));
            m_HeightField.RegisterValueChangedCallback(e => Resize(m_Canvas != null ? m_Canvas.Width : DefaultSize, e.newValue));
            bar.Add(m_WidthField);
            bar.Add(m_HeightField);
            bar.Add(Small("←", "Resmi sola kaydır", () => Shift(-1, 0)));
            bar.Add(Small("→", "Resmi sağa kaydır", () => Shift(1, 0)));
            bar.Add(Small("↑", "Resmi yukarı kaydır", () => Shift(0, 1)));
            bar.Add(Small("↓", "Resmi aşağı kaydır", () => Shift(0, -1)));
            bar.Add(Small("Yatay çevir", "Sol-sağ çevirir", () => Flip(true)));
            bar.Add(Small("Dikey çevir", "Alt-üst çevirir", () => Flip(false)));
            bar.Add(Small("Temizle", "Bütün küpleri siler", () => Mutate("Tuvali Temizle", () => m_Canvas.Clear())));
            bar.Add(Small("Sığdır", "Resmi pencereye sığdırır", () => m_Surface.Fit()));
            m_Grid.RegisterValueChangedCallback(e => { m_Surface.ShowGrid = e.newValue; m_Surface.RepaintOverlay(); });
            bar.Add(m_Grid);
            m_ShowRef.tooltip = "Orijinal resmi tuvalin üstünde saydam gösterir";
            m_ShowRef.RegisterValueChangedCallback(_ => RefreshReference());
            bar.Add(m_ShowRef);
            m_RefOpacity.style.width = 70;
            m_RefOpacity.tooltip = "Orijinal resmin saydamlığı";
            m_RefOpacity.RegisterValueChangedCallback(_ => RefreshReference());
            bar.Add(m_RefOpacity);
            m_Hover.AddToClassList("canvas-panel__hover");
            bar.Add(m_Hover);
            return bar;
        }

        private static Button Small(string text, string tip, Action action) => new Button(action) { text = text, tooltip = tip };

        private VisualElement BuildApplyBar()
        {
            m_ApplyBar.AddToClassList("preview");
            var text = new Label("Uygulanmamış değişiklik var. Uygula: resim Assets/PixelArt/Optimized'e yazılır, level'ı Kaydet ile kaydedersin.");
            text.AddToClassList("preview__detail");
            text.style.flexGrow = 1;
            text.style.flexShrink = 1;
            m_ApplyBar.Add(text);
            var keep = new Button(() => Apply(false)) { text = "Uygula, gemileri koru", tooltip = "Gemi sırası aynen kalır. Kapasite farkı oluşursa Gemi Sırası'nda kritik hata olarak görünür." };
            keep.AddToClassList("preview__apply");
            m_ApplyBar.Add(keep);
            m_ApplyBar.Add(new Button(() => Apply(true)) { text = "Uygula, gemileri yeniden üret", tooltip = "Gemi sırası palete göre baştan üretilir; Güçlü Konteyner, halat ve gizli gider." });
            m_ApplyBar.Add(new Button(Discard) { text = "Değişiklikleri at" });
            return m_ApplyBar;
        }

        private VisualElement BuildSide()
        {
            var side = new VisualElement();
            side.AddToClassList("canvas-panel__side");
            m_Thumb.AddToClassList("canvas-panel__thumb");
            side.Add(m_Thumb);
            m_Status.AddToClassList("level-preview__caption");
            side.Add(m_Status);

            var pick = new VisualElement();
            pick.AddToClassList("automation__row");
            pick.style.marginTop = 6;
            m_ColorField.style.flexGrow = 1;
            pick.Add(m_ColorField);
            side.Add(pick);
            var colorButtons = new VisualElement();
            colorButtons.AddToClassList("automation__row");
            colorButtons.Add(new Button(AddColor) { text = "Yeni renk ekle" });
            colorButtons.Add(new Button(UpdateColor) { text = "Seçiliyi değiştir", tooltip = "Seçili rengin bütün küpleri bu renge döner" });
            side.Add(colorButtons);

            var title = new Label("Renkler (küp / gemi)");
            title.AddToClassList("automation__title");
            title.style.marginTop = 8;
            side.Add(title);
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.Add(m_Palette);
            scroll.Add(m_Warnings);
            side.Add(scroll);
            side.Add(new Button(RemoveUnused) { text = "Kullanılmayan renkleri at" });
            return side;
        }

        // ------------------------------------------------------------------ yükleme

        /// <summary>Sekme açıldığında ya da level değiştiğinde çağrılır; uygulanmamış çizimi ezmez.</summary>
        public void Sync()
        {
            var level = m_Edit.Level;
            SetEnabled(level != null);
            if (level == null) return;
            if (IsDirty && level == m_Level) return;
            if (level == m_Level && m_LoadedFrom == level.GetActiveTexture() && m_Canvas != null) return;
            Load(level);
        }

        private void Load(PixelLevelData level)
        {
            m_Level = level;
            m_LoadedFrom = level.GetActiveTexture();
            m_Canvas = m_LoadedFrom != null ? LevelCanvas.FromTexture(m_LoadedFrom) : new LevelCanvas(DefaultSize);
            m_Canvas.CustomPalette = (Color[])m_Canvas.ActivePalette.Clone();
            if (m_UndoState == null)
            {
                m_UndoState = ScriptableObject.CreateInstance<LevelCanvasUndoState>();
                m_UndoState.hideFlags = HideFlags.HideAndDontSave;
            }
            m_UndoState.CaptureFrom(m_Canvas);
            m_Brush = (byte)Mathf.Clamp(m_Brush, 1, m_Canvas.ActivePalette.Length);
            StoreBaseline();
            SetDirty(false);
            RefreshAll();
            m_Surface.Fit();
        }

        private void StoreBaseline()
        {
            m_BaseCells = m_Canvas.CopyCells();
            m_BasePalette = (Color[])m_Canvas.ActivePalette.Clone();
            m_BaseWidth = m_Canvas.Width;
            m_BaseHeight = m_Canvas.Height;
        }

        private void SetDirty(bool dirty)
        {
            IsDirty = dirty;
            m_ApplyBar.style.display = dirty ? DisplayStyle.Flex : DisplayStyle.None;
            DirtyChanged?.Invoke();
        }

        private void UpdateDirty()
        {
            bool dirty = m_Canvas.Width != m_BaseWidth || m_Canvas.Height != m_BaseHeight;
            var pal = m_Canvas.ActivePalette;
            if (!dirty)
            {
                var cells = m_Canvas.CopyCells();
                for (int i = 0; i < cells.Length && !dirty; i++)
                {
                    // Palet sırası aynıysa sayı karşılaştırması yeter; renk değiştiyse aşağıdaki kontrol yakalar
                    dirty = cells[i] != m_BaseCells[i];
                }
                if (!dirty)
                {
                    dirty = pal.Length != m_BasePalette.Length;
                    for (int i = 0; i < pal.Length && !dirty; i++) dirty = pal[i] != m_BasePalette[i];
                }
            }
            if (dirty != IsDirty) SetDirty(dirty);
        }

        // ------------------------------------------------------------------ düzenleme ve geri alma

        private void BeginUndo(string name) => Undo.RegisterCompleteObjectUndo(m_UndoState, name);

        private void EndEdit()
        {
            m_UndoState.CaptureFrom(m_Canvas);
            UpdateDirty();
            RefreshAll();
        }

        private void Mutate(string name, Action action)
        {
            if (m_Canvas == null) return;
            BeginUndo(name);
            action();
            EndEdit();
        }

        /// <summary>Ctrl+Z / Ctrl+Y sonrası çizimi geri alınan durumdan yükler.</summary>
        public void OnUndoRedo()
        {
            if (m_Canvas == null || m_UndoState == null) return;
            m_UndoState.ApplyTo(m_Canvas);
            m_Brush = (byte)Mathf.Clamp(m_Brush, 1, m_Canvas.ActivePalette.Length);
            UpdateDirty();
            RefreshAll();
        }

        private void Paint(IEnumerable<Vector2Int> cells, byte value)
        {
            foreach (var c in cells) m_Canvas.Set(c.x, c.y, value);
        }

        private void StampLine(Vector2Int from, Vector2Int to, byte value)
        {
            m_LineBuffer.Clear();
            CanvasShapes.Line(from.x, from.y, to.x, to.y, m_LineBuffer);
            m_StampBuffer.Clear();
            foreach (var c in m_LineBuffer)
                CanvasShapes.Stamp(c, m_BrushSize.value, m_MirrorX.value, m_MirrorY.value, m_Canvas.Width, m_Canvas.Height, m_StampBuffer);
            Paint(m_StampBuffer, value);
        }

        private void ShapeCells(Vector2Int a, Vector2Int b, HashSet<Vector2Int> into)
        {
            m_LineBuffer.Clear();
            if (m_Tool == Tool.Line) CanvasShapes.Line(a.x, a.y, b.x, b.y, m_LineBuffer);
            else if (m_Tool == Tool.Rectangle) CanvasShapes.Rectangle(a.x, a.y, b.x, b.y, m_Filled.value, m_LineBuffer);
            else CanvasShapes.Ellipse(a.x, a.y, b.x, b.y, m_Filled.value, m_LineBuffer);
            into.Clear();
            foreach (var c in m_LineBuffer)
                CanvasShapes.Stamp(c, 1, m_MirrorX.value, m_MirrorY.value, m_Canvas.Width, m_Canvas.Height, into);
        }

        private void OnCellDown(Vector2Int cell, int button)
        {
            if (m_Canvas == null) return;
            m_Erasing = button == 1;
            if (m_Tool == Tool.Picker || (m_Surface.AltHeld && !m_Erasing))
            {
                Pick(cell);
                return;
            }
            if (m_Tool == Tool.Replace && !m_Erasing)
            {
                byte from = m_Canvas.Get(cell.x, cell.y);
                if (from != 0 && from != m_Brush) Mutate("Tuval: Renk Değiştir", () => m_Canvas.ReplaceColor(from, m_Brush));
                return;
            }
            if (m_Tool == Tool.Fill && !m_Erasing)
            {
                byte from = m_Canvas.Get(cell.x, cell.y);
                if (from != m_Brush) Mutate("Tuval: Doldur", () => Paint(CanvasShapes.FloodFill(m_Canvas, cell.x, cell.y), m_Brush));
                return;
            }

            byte value = m_Erasing || m_Tool == Tool.Eraser ? (byte)0 : m_Brush;
            if (m_Tool == Tool.Line || m_Tool == Tool.Rectangle || m_Tool == Tool.Ellipse)
            {
                m_ShapeStart = cell;
                m_Stroking = true;
                UpdateShapePreview(cell);
                return;
            }
            BeginUndo("Tuval: Çizim");
            m_Stroking = true;
            m_LastStroke = cell;
            StampLine(cell, cell, value);
            RefreshTexture();
        }

        private void OnCellDrag(Vector2Int cell)
        {
            if (!m_Stroking || m_Canvas == null) return;
            if (m_Tool == Tool.Line || m_Tool == Tool.Rectangle || m_Tool == Tool.Ellipse)
            {
                UpdateShapePreview(ClampCell(cell));
                return;
            }
            byte value = m_Erasing || m_Tool == Tool.Eraser ? (byte)0 : m_Brush;
            StampLine(m_LastStroke, cell, value);
            m_LastStroke = cell;
            RefreshTexture();
        }

        private void OnCellUp(Vector2Int cell)
        {
            if (!m_Stroking || m_Canvas == null) return;
            m_Stroking = false;
            if (m_Tool == Tool.Line || m_Tool == Tool.Rectangle || m_Tool == Tool.Ellipse)
            {
                var cells = new HashSet<Vector2Int>();
                ShapeCells(m_ShapeStart, ClampCell(cell), cells);
                byte value = m_Erasing ? (byte)0 : m_Brush;
                m_Surface.SetPreview(null, Color.clear);
                Mutate("Tuval: Şekil", () => Paint(cells, value));
                return;
            }
            EndEdit();
        }

        private Vector2Int ClampCell(Vector2Int c) => new Vector2Int(Mathf.Clamp(c.x, 0, m_Canvas.Width - 1), Mathf.Clamp(c.y, 0, m_Canvas.Height - 1));

        private void UpdateShapePreview(Vector2Int cell)
        {
            var cells = new HashSet<Vector2Int>();
            ShapeCells(m_ShapeStart, cell, cells);
            m_Surface.SetPreview(cells, m_Erasing ? new Color(0.1f, 0.1f, 0.12f) : m_Canvas.ColorOf(m_Brush));
        }

        private void OnHover(Vector2Int? cell)
        {
            if (m_Canvas == null) return;
            if (!cell.HasValue)
            {
                m_Hover.text = "";
                m_Surface.SetPreview(null, Color.clear);
                return;
            }
            var c = cell.Value;
            byte v = m_Canvas.Get(c.x, c.y);
            m_Hover.text = v == 0 ? $"({c.x}, {c.y}) boş" : $"({c.x}, {c.y}) {StudioColors.Name(m_Canvas.ColorOf(v), v - 1)}";
            if (m_Tool == Tool.Pencil || m_Tool == Tool.Eraser)
            {
                m_StampBuffer.Clear();
                CanvasShapes.Stamp(c, m_BrushSize.value, m_MirrorX.value, m_MirrorY.value, m_Canvas.Width, m_Canvas.Height, m_StampBuffer);
                m_Surface.SetPreview(m_StampBuffer, m_Tool == Tool.Eraser ? new Color(0.1f, 0.1f, 0.12f) : m_Canvas.ColorOf(m_Brush));
            }
            else m_Surface.SetPreview(null, Color.clear);
        }

        private void Pick(Vector2Int cell)
        {
            byte v = m_Canvas.Get(cell.x, cell.y);
            if (v == 0) return;
            m_Brush = v;
            if (m_Tool == Tool.Picker) SetTool(m_ToolBeforePicker);
            RefreshPalette();
        }

        private void SetTool(Tool tool)
        {
            if (tool == Tool.Picker && m_Tool != Tool.Picker) m_ToolBeforePicker = m_Tool;
            m_Tool = tool;
            foreach (var kv in m_ToolButtons) kv.Value.EnableInClassList("canvas-tool--active", kv.Key == tool);
            m_Surface.SetPreview(null, Color.clear);
        }

        private void OnKey(KeyDownEvent e)
        {
            if (m_Canvas == null || e.ctrlKey || e.commandKey) return;
            // Sayı kutularına yazarken kısayol çalışmasın
            if (e.target is VisualElement target && target.GetFirstAncestorOfType<TextInputBaseField<int>>() != null) return;
            switch (e.keyCode)
            {
                case KeyCode.B: SetTool(Tool.Pencil); break;
                case KeyCode.E: SetTool(Tool.Eraser); break;
                case KeyCode.G: SetTool(Tool.Fill); break;
                case KeyCode.L: SetTool(Tool.Line); break;
                case KeyCode.U: SetTool(Tool.Rectangle); break;
                case KeyCode.O: SetTool(Tool.Ellipse); break;
                case KeyCode.I: SetTool(Tool.Picker); break;
                case KeyCode.R: SetTool(Tool.Replace); break;
                case KeyCode.M: m_MirrorX.value = !m_MirrorX.value; break;
                case KeyCode.LeftBracket: m_BrushSize.value = Mathf.Max(1, m_BrushSize.value - 1); break;
                case KeyCode.RightBracket: m_BrushSize.value = Mathf.Min(5, m_BrushSize.value + 1); break;
                default: return;
            }
            e.StopPropagation();
        }

        // ------------------------------------------------------------------ resim işlemleri

        private void Resize(int w, int h)
        {
            if (m_Canvas == null) return;
            w = Mathf.Clamp(w, 4, 64);
            h = Mathf.Clamp(h, 4, 64);
            if (w == m_Canvas.Width && h == m_Canvas.Height) { RefreshAll(); return; }
            Mutate("Tuval Boyutu", () => m_Canvas.Resize(w, h));
            m_Surface.Fit();
        }

        private void Shift(int dx, int dy)
        {
            if (m_Canvas == null) return;
            Mutate("Tuval: Kaydır", () =>
            {
                int w = m_Canvas.Width, h = m_Canvas.Height;
                var old = m_Canvas.CopyCells();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int sx = x - dx, sy = y - dy;
                        m_Canvas.Set(x, y, sx >= 0 && sy >= 0 && sx < w && sy < h ? old[sy * w + sx] : (byte)0);
                    }
            });
        }

        private void Flip(bool horizontal)
        {
            if (m_Canvas == null) return;
            Mutate("Tuval: Çevir", () =>
            {
                int w = m_Canvas.Width, h = m_Canvas.Height;
                var old = m_Canvas.CopyCells();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        m_Canvas.Set(x, y, horizontal ? old[y * w + (w - 1 - x)] : old[(h - 1 - y) * w + x]);
            });
        }

        private void AddColor()
        {
            if (m_Canvas == null) return;
            Color c = m_ColorField.value;
            Mutate("Tuval: Renk Ekle", () => m_Brush = m_Canvas.AddColor(c));
        }

        private void UpdateColor()
        {
            if (m_Canvas == null) return;
            Color c = m_ColorField.value;
            Mutate("Tuval: Rengi Değiştir", () => m_Canvas.UpdateColor(m_Brush, c));
        }

        private void RemoveUnused()
        {
            if (m_Canvas == null) return;
            Color current = m_Canvas.ColorOf(m_Brush);
            Mutate("Tuval: Kullanılmayan Renkler", () =>
            {
                m_Canvas.RemoveUnusedColors();
                m_Brush = IndexOf(current);
            });
        }

        private byte IndexOf(Color c)
        {
            var pal = m_Canvas.ActivePalette;
            for (int i = 0; i < pal.Length; i++) if (pal[i] == c) return (byte)(i + 1);
            return 1;
        }

        private void Merge(byte drop, byte keep)
        {
            Color keepColor = m_Canvas.ColorOf(keep);
            Color brush = m_Brush == drop ? keepColor : m_Canvas.ColorOf(m_Brush);
            Mutate("Tuval: Renkleri Birleştir", () =>
            {
                m_Canvas.ReplaceColor(drop, keep);
                m_Canvas.RemoveColor(drop);
                m_Brush = IndexOf(brush);
            });
        }

        /// <summary>Tuvali level'a yazar. Boş tuval uygulanmaz.</summary>
        public void Apply(bool regenerateShips)
        {
            if (m_Canvas == null || m_Level == null) return;
            if (m_Canvas.IsEmpty)
            {
                Notify?.Invoke("Tuval boş, uygulanmadı");
                return;
            }
            var baked = m_Canvas.ToTexture();
            var saved = PixelPaletteOptimizer.SaveAsOptimizedAsset(m_Level.GetOriginalTexture(), baked, "_canvas");
            UnityEngine.Object.DestroyImmediate(baked);
            if (saved == null)
            {
                Notify?.Invoke("Resim kaydedilemedi");
                return;
            }
            m_Edit.ReplaceImage(saved, regenerateShips);
            m_LoadedFrom = m_Level.GetActiveTexture();
            StoreBaseline();
            SetDirty(false);
            RefreshAll();
            Notify?.Invoke(regenerateShips ? "Resim uygulandı, gemiler yeniden üretildi" : "Resim uygulandı, gemiler korundu");
        }

        public void Discard()
        {
            if (m_Level != null) Load(m_Level);
        }

        // ------------------------------------------------------------------ çizim

        private void RefreshAll()
        {
            RefreshTexture();
            RefreshPalette();
            RefreshReference();
            m_WidthField.SetValueWithoutNotify(m_Canvas.Width);
            m_HeightField.SetValueWithoutNotify(m_Canvas.Height);
            foreach (var kv in m_ToolButtons) kv.Value.EnableInClassList("canvas-tool--active", kv.Key == m_Tool);
        }

        private void RefreshTexture()
        {
            if (m_Canvas == null) return;
            int w = m_Canvas.Width, h = m_Canvas.Height;
            if (m_Texture == null || m_Texture.width != w || m_Texture.height != h)
            {
                if (m_Texture != null) UnityEngine.Object.DestroyImmediate(m_Texture);
                m_Texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
                };
            }
            var cells = m_Canvas.CopyCells();
            var pixels = new Color32[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                pixels[i] = cells[i] == 0 ? new Color32(0, 0, 0, 0) : (Color32)m_Canvas.ColorOf(cells[i]);
            m_Texture.SetPixels32(pixels);
            m_Texture.Apply(false);
            m_Surface.SetTexture(m_Texture);
            m_Thumb.image = m_Texture;
            m_Thumb.MarkDirtyRepaint();
            m_Status.text = $"{w}×{h} · {m_Canvas.FilledCount} küp";
        }

        private void RefreshReference()
        {
            var original = m_Level != null ? m_Level.GetOriginalTexture() : null;
            m_Surface.SetReference(m_ShowRef.value ? original : null, m_RefOpacity.value);
        }

        private void RefreshPalette()
        {
            m_Palette.Clear();
            m_Warnings.Clear();
            if (m_Canvas == null) return;
            var pal = m_Canvas.ActivePalette;
            var counts = m_Canvas.CountPerColor();
            for (int i = 0; i < pal.Length; i++)
            {
                byte value = (byte)(i + 1);
                Color color = pal[i];
                int cubes = value < counts.Length ? counts[value] : 0;
                int capacity = m_Level != null ? m_Level.GetTotalAssignedCapacityForColor(color, 0.04f) : 0;
                var row = new VisualElement();
                row.AddToClassList("canvas-color");
                row.EnableInClassList("canvas-color--active", value == m_Brush);
                var swatch = new VisualElement();
                swatch.AddToClassList("swatch");
                swatch.style.backgroundColor = color;
                row.Add(swatch);
                var label = new Label($"{StudioColors.Name(color, i)}");
                label.style.flexGrow = 1;
                row.Add(label);
                var numbers = new Label($"{cubes} / {capacity}") { tooltip = "Bu renkteki küp sayısı / gemilerin toplam kapasitesi" };
                numbers.EnableInClassList("canvas-color--mismatch", cubes != capacity);
                row.Add(numbers);
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    m_Brush = value;
                    m_ColorField.SetValueWithoutNotify(color);
                    RefreshPalette();
                });
                m_Palette.Add(row);
            }
            m_ColorField.SetValueWithoutNotify(m_Canvas.ColorOf(m_Brush));

            // Oyuncunun ayıramayacağı kadar yakın renkler
            for (int a = 1; a <= pal.Length; a++)
            {
                if (a >= counts.Length || counts[a] == 0) continue;
                for (int b = a + 1; b <= pal.Length; b++)
                {
                    if (b >= counts.Length || counts[b] == 0) continue;
                    Color ca = pal[a - 1], cb = pal[b - 1];
                    if (Mathf.Max(Mathf.Abs(ca.r - cb.r), Mathf.Abs(ca.g - cb.g), Mathf.Abs(ca.b - cb.b)) >= SimilarColorChannel) continue;
                    byte keep = (byte)(counts[a] >= counts[b] ? a : b), drop = (byte)(keep == a ? b : a);
                    var warn = new VisualElement();
                    warn.AddToClassList("canvas-warning");
                    var text = new Label($"{StudioColors.Name(ca, a - 1)} ve {StudioColors.Name(cb, b - 1)} ayırt edilemeyecek kadar yakın.");
                    text.style.whiteSpace = WhiteSpace.Normal;
                    text.style.flexShrink = 1;
                    warn.Add(text);
                    warn.Add(new Button(() => Merge(drop, keep)) { text = "Birleştir" });
                    m_Warnings.Add(warn);
                }
            }
        }
    }
}
