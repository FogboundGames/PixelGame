using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Bölüm Stüdyosu — "kolay mod" bölüm oluşturma ekranı.
    ///
    /// Level Designer penceresi ince ayar içindir (onlarca float, palet stüdyosu,
    /// vagon matrisi, sahne araçları). Bu ekran ise tek bir soruya odaklanır:
    /// "elimde bir görsel var, bundan oynanabilir bir bölüm çıkar."
    ///
    /// Hiçbir sayısal ayar göstermez. Görsel + zorluk seçilir, geri kalan her şey
    /// (ızgara, palet, vagon dizilimi, eğim/boşluk gibi görsel değerler) mevcut
    /// bölümlerden türetilir. Oluşturmadan ÖNCE LevelSolvabilityAnalyzer ile
    /// bölümün gerçekten bitirilebilir olduğu doğrulanır.
    /// </summary>
    public class LevelBuilderWindow : EditorWindow
    {
        private enum Difficulty { Kolay, Orta, Zor }
        private enum SourceMode { Gorsel, Ciz }

        private SourceMode m_Mode = SourceMode.Gorsel;
        private Texture2D m_Source;          // Gorsel modunda secilen doku
        private Texture2D m_OriginalSource;  // Orijinal doku (4 secildikten sonra 5 veya 6 secilebilsin diye korunur)
        private LevelCanvas m_Canvas;        // Ciz modunda tuval
        private byte m_Brush = 1;
        private Color m_NewBrushColor = new Color(0.93f, 0.26f, 0.35f);
        private int m_CanvasSize = 16;
        private Texture2D m_CanvasPreview;   // tuvalden turetilen gecici doku
        private Difficulty m_Difficulty = Difficulty.Orta;
        private string m_LevelName = "";
        private Vector2 m_Scroll;
        private int m_TargetColorCount = 5;

        // Önizleme raporu önbelleği — simülasyon pahalı, her repaint'te koşamaz.
        private LevelSolvabilityAnalyzer.Report m_Preview;
        private Texture2D m_PreviewFor;
        private Difficulty m_PreviewDifficulty;
        private PixelLevelData m_LastCreated;
        private PixelLevelData m_EditingLevel;

        // ---- Tuval araçları ----
        private enum CanvasTool { Kalem, Damlalik, RenkDegistir }
        private CanvasTool m_Tool = CanvasTool.Kalem;
        private bool m_MirrorX;
        private bool m_ShowReference = true;
        private LevelCanvasUndoState m_UndoState;
        private Vector2Int m_HoverCell = new Vector2Int(-1, -1);
        private int m_SwatchHoverValue = -1;
        private Color32? m_ReferenceHoverColor;
        private Texture2D m_ReferenceTex;          // m_ReferencePixels'in hangi dokudan okunduğu
        private Color32[] m_ReferencePixels;

        // Kare RGB mesafesi: bunun altındaki iki renk "çok benzer" uyarısı alır
        private const float SimilarColorSqrDistance = 0.05f;
        // Görselden renk alınırken palette bu kadar yakın renk varsa yeni renk eklenmez, o seçilir
        private const float PickSnapSqrDistance = 0.01f;

        // Kolay mod artık doğrudan Level Designer içindeki bölümlerden açılıyor
        // [MenuItem("Tools/PixelGame/🎨 Bölüm Stüdyosu (Kolay Mod)", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<LevelBuilderWindow>(false, "Bölüm Stüdyosu");
            w.minSize = new Vector2(460f, 560f);
            w.Show();
        }

        public static void OpenForLevel(PixelLevelData level)
        {
            var w = GetWindow<LevelBuilderWindow>(false, "Bölüm Stüdyosu");
            w.minSize = new Vector2(460f, 560f);
            w.Show();
            w.Focus();
            w.LoadLevelForEditing(level);
        }

        public void LoadLevelForEditing(PixelLevelData level)
        {
            if (level == null) return;
            m_EditingLevel = level;
            m_Mode = SourceMode.Ciz;
            Texture2D tex = level.GetActiveTexture();
            if (tex != null)
            {
                m_Canvas = LevelCanvas.FromTexture(tex);
                m_CanvasSize = Mathf.Max(m_Canvas.Width, m_Canvas.Height);
            }
            else
            {
                m_Canvas = new LevelCanvas(16);
                m_CanvasSize = 16;
            }
            m_LevelName = level.LevelName;
            m_Brush = 1;
            ResetCanvasUndo();
            InvalidateCanvas();
            Repaint();
        }

        // ---- Zorluk ön ayarları ---------------------------------------------
        // Zorluğu belirleyen asıl şey slot sayısı: az slot = aynı anda daha az renk
        // toplanabilir = daha çok planlama. (Zorluk formülündeki slotFactor = 4/slots
        // de bu mantığı kullanıyor.)
        private static (int slots, int capacity, string blurb) Preset(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Kolay: return (5, 16, "5 yanaşma yeri — oyuncu rahatça istediği rengi toplar.");
                case Difficulty.Zor:   return (3, 12, "3 yanaşma yeri, küçük gemiler — sıra planlaması gerekir.");
                default:               return (4, 14, "4 yanaşma yeri — dengeli, standart tempo.");
            }
        }

        private void OnGUI()
        {
            // Hover vurgusu ve durum satırı fare hareketiyle güncellensin
            if (Event.current.type == EventType.MouseMove) Repaint();

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🎨 Bölüm Stüdyosu", new GUIStyle(EditorStyles.largeLabel) { fontSize = 20, fontStyle = FontStyle.Bold });
            EditorGUILayout.LabelField("Bir görsel bırak, zorluğu seç, oluştur. Gerisi otomatik.", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);

            DrawModeTabs();
            EditorGUILayout.Space(8);
            if (m_Mode == SourceMode.Gorsel) DrawStep1Image();
            else DrawStep1Canvas();
            EditorGUILayout.Space(10);
            DrawStep2Difficulty();
            EditorGUILayout.Space(10);
            DrawStep3Name();
            EditorGUILayout.Space(12);
            DrawPreview();
            EditorGUILayout.Space(10);
            DrawCreate();

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------
        private void DrawModeTabs()
        {
            EditorGUILayout.BeginHorizontal();
            foreach (SourceMode m in System.Enum.GetValues(typeof(SourceMode)))
            {
                bool on = m_Mode == m;
                Color prev = GUI.backgroundColor;
                if (on) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
                string label = m == SourceMode.Gorsel ? "🖼  Görselden" : "✏️  Kendim Çizeyim";
                if (GUILayout.Button(label, GUILayout.Height(28)))
                {
                    m_Mode = m;
                    m_Preview = null;
                    if (m_Mode == SourceMode.Ciz && m_Canvas == null)
                    {
                        m_Canvas = new LevelCanvas(m_CanvasSize);
                        ResetCanvasUndo();
                    }
                }
                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------
        private void DrawStep1Canvas()
        {
            if (m_Canvas == null)
            {
                m_Canvas = new LevelCanvas(m_CanvasSize);
                ResetCanvasUndo();
            }

            // Bu olay için hover bilgileri baştan hesaplanır
            m_SwatchHoverValue = -1;
            m_ReferenceHoverColor = null;
            HandleCanvasShortcuts();

            if (m_EditingLevel != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUIStyle editStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.25f, 0.85f, 1f) } };
                EditorGUILayout.LabelField($"✏️ Düzenlenen: {m_EditingLevel.LevelName}", editStyle);
                
                GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
                if (GUILayout.Button("💾 Seviyeye Kaydet & Uygula", GUILayout.Width(220), GUILayout.Height(24)))
                {
                    SaveCanvasChangesToEditingLevel();
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("✕ Çık", GUILayout.Width(50), GUILayout.Height(24)))
                {
                    m_EditingLevel = null;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.LabelField("1 · Çiz & Düzenle", EditorStyles.boldLabel);

            // Hazır şablonlar
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Hazır şablon:", GUILayout.Width(80));
            for (int i = 0; i < LevelCanvas.TemplateNames.Length; i++)
            {
                if (GUILayout.Button(LevelCanvas.TemplateNames[i], EditorStyles.miniButton))
                {
                    RecordCanvasUndo("Tuval: Şablon");
                    LevelCanvas template = LevelCanvas.Template(i);
                    m_Canvas.LoadState(template.Width, template.Height, template.CopyCells(), template.CustomPalette);
                    m_CanvasSize = Mathf.Max(m_Canvas.Width, m_Canvas.Height);
                    m_Brush = 1;
                    if (string.IsNullOrWhiteSpace(m_LevelName)) m_LevelName = LevelCanvas.TemplateNames[i];
                    CommitCanvasChange();
                }
            }
            EditorGUILayout.EndHorizontal();

            // Boyut + temizle
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Izgara:", GUILayout.Width(80));
            int newSize = EditorGUILayout.IntSlider(m_CanvasSize, 8, 32);
            if (newSize != m_CanvasSize)
            {
                RecordCanvasUndo("Tuval: Boyut");
                m_CanvasSize = newSize;
                m_Canvas.Resize(newSize, newSize);
                CommitCanvasChange();
            }
            if (GUILayout.Button("Temizle", EditorStyles.miniButton, GUILayout.Width(70)))
            {
                RecordCanvasUndo("Tuval: Temizle");
                m_Canvas.Clear();
                CommitCanvasChange();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            DrawCanvasToolbar();
            EditorGUILayout.Space(2);

            int[] counts = m_Canvas.CountPerColor();
            Color[] activePalette = m_Canvas.ActivePalette;

            // Fırça paleti (0 = silgi)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Renk ekleme ve düzenleme araçları
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🎨 Palet & Fırça:", EditorStyles.boldLabel, GUILayout.Width(105));

            m_NewBrushColor = EditorGUILayout.ColorField(GUIContent.none, m_NewBrushColor, false, false, false, GUILayout.Width(45));

            if (GUILayout.Button("➕ Renk Ekle", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                RecordCanvasUndo("Tuval: Renk Ekle");
                m_Brush = m_Canvas.AddColor(m_NewBrushColor);
                CommitCanvasChange();
            }

            EditorGUI.BeginDisabledGroup(m_Brush == 0);
            if (GUILayout.Button("✏️ Seçiliyi Güncelle", EditorStyles.miniButton, GUILayout.Width(115)))
            {
                if (m_Brush > 0)
                {
                    RecordCanvasUndo("Tuval: Renk Güncelle");
                    m_Canvas.UpdateColor(m_Brush, m_NewBrushColor);
                    CommitCanvasChange();
                }
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            int unusedCount = 0;
            for (int v = 1; v < counts.Length; v++) if (counts[v] == 0) unusedCount++;
            if (unusedCount > 0 && activePalette.Length > 1)
            {
                if (GUILayout.Button(new GUIContent($"🧹 Kullanılmayanları Sil ({unusedCount})", "Tuvalde hiç kullanılmayan renkleri paletten kaldırır"), EditorStyles.miniButton))
                {
                    RecordCanvasUndo("Tuval: Kullanılmayan Renkleri Sil");
                    Color brushColor = m_Brush > 0 ? m_Canvas.ColorOf(m_Brush) : Color.clear;
                    bool brushUnused = m_Brush > 0 && m_Brush < counts.Length && counts[m_Brush] == 0;
                    m_Canvas.RemoveUnusedColors();
                    if (m_Brush > 0) m_Brush = brushUnused ? (byte)1 : IndexOfColor(brushColor);
                    CommitCanvasChange();
                    GUIUtility.ExitGUI();
                }
            }

            if (m_Canvas.CustomPalette != null)
            {
                if (GUILayout.Button("Paleti Sıfırla", EditorStyles.miniButton, GUILayout.Width(85)))
                {
                    if (EditorUtility.DisplayDialog("Paleti Sıfırla", "Palet varsayılan 10 renge döndürülecek. Onaylıyor musunuz?", "Evet", "Hayır"))
                    {
                        RecordCanvasUndo("Tuval: Paleti Sıfırla");
                        m_Canvas.CustomPalette = null;
                        m_Brush = 1;
                        CommitCanvasChange();
                    }
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // Renk kutucukları (swatch listesi) - sığmayanlar otomatik alt satıra geçer
            int totalSwatches = 1 + activePalette.Length; // 0 = silgi
            float viewWidth = Mathf.Max(260f, EditorGUIUtility.currentViewWidth - 60f);
            int swatchesPerRow = Mathf.Max(6, Mathf.FloorToInt(viewWidth / 28f));

            EditorGUILayout.BeginHorizontal();
            for (int s = 0; s < totalSwatches; s++)
            {
                if (s > 0 && s % swatchesPerRow == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }

                if (s == 0)
                {
                    DrawBrushSwatch(0, Color.clear, counts[0]);
                }
                else
                {
                    DrawBrushSwatch((byte)s, activePalette[s - 1], counts[s]);
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            DrawSimilarColorWarnings(counts);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
            DrawCanvasArea();
            DrawCanvasStatusLine(counts);
        }
        private void SaveCanvasChangesToEditingLevel()
        {
            if (m_EditingLevel == null || m_Canvas == null) return;

            Texture2D baked = m_Canvas.ToTexture();
            Texture2D baseTex = m_EditingLevel.GetOriginalTexture();
            Texture2D saved = PixelPaletteOptimizer.SaveAsOptimizedAsset(baseTex, baked, "_canvas");
            DestroyImmediate(baked);

            if (saved != null)
            {
                Undo.RecordObject(m_EditingLevel, "Edit Level in Canvas");
                m_EditingLevel.LevelTexture = saved;
                m_EditingLevel.ExtractPaletteFromTexture();
                m_EditingLevel.GenerateInterleavedWagonSequenceFromPalette();
                EditorUtility.SetDirty(m_EditingLevel);
                AssetDatabase.SaveAssets();

                // Sahneyi güncelle
                BuildInScene(m_EditingLevel, out _);
                ShowNotification(new GUIContent($"'{m_EditingLevel.LevelName}' güncellendi ve sahnede yenilendi!"));
            }
        }

        // ------------------------------------------------------------------
        // Tuval araçları
        // ------------------------------------------------------------------

        private void DrawCanvasToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            DrawToolButton(CanvasTool.Kalem, "✏️ Kalem", "Sol tık boya, sağ tık sil (B)");
            DrawToolButton(CanvasTool.Damlalik, "💧 Damlalık", "Tuvalden ya da orijinal görselden renk seç (I). Her araçta Alt+tık da renk seçer.");
            DrawToolButton(CanvasTool.RenkDegistir, "🔁 Rengi Değiştir", "Tıkladığın rengin TÜM hücrelerini seçili fırça rengine çevirir (R). Silgi seçiliyse o rengi tamamen siler.");

            GUILayout.Space(8);
            Color prev = GUI.backgroundColor;
            if (m_MirrorX) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
            if (GUILayout.Button(new GUIContent("⇋ Simetri", "Yatay ayna: bir yarıya çizdiğin diğer yarıya da çizilir (M)"), GUILayout.Height(24), GUILayout.Width(80)))
            {
                m_MirrorX = !m_MirrorX;
            }
            GUI.backgroundColor = prev;

            if (GetReferenceTexture() != null)
            {
                if (m_ShowReference) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
                if (GUILayout.Button(new GUIContent("🖼 Orijinal", "Orijinal görseli tuvalin yanında göster / gizle"), GUILayout.Height(24), GUILayout.Width(80)))
                {
                    m_ShowReference = !m_ShowReference;
                }
                GUI.backgroundColor = prev;
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("↶", "Geri al (Ctrl+Z)"), GUILayout.Width(30), GUILayout.Height(24))) Undo.PerformUndo();
            if (GUILayout.Button(new GUIContent("↷", "İleri al (Ctrl+Y)"), GUILayout.Width(30), GUILayout.Height(24))) Undo.PerformRedo();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolButton(CanvasTool tool, string label, string tip)
        {
            Color prev = GUI.backgroundColor;
            if (m_Tool == tool) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
            if (GUILayout.Button(new GUIContent(label, tip), GUILayout.Height(24)))
            {
                m_Tool = tool;
            }
            GUI.backgroundColor = prev;
        }

        private void HandleCanvasShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || e.control || e.command || e.alt) return;
            if (EditorGUIUtility.editingTextField) return;

            switch (e.keyCode)
            {
                case KeyCode.B: m_Tool = CanvasTool.Kalem; break;
                case KeyCode.I: m_Tool = CanvasTool.Damlalik; break;
                case KeyCode.R: m_Tool = CanvasTool.RenkDegistir; break;
                case KeyCode.M: m_MirrorX = !m_MirrorX; break;
                default: return;
            }
            e.Use();
            Repaint();
        }

        private static GUIStyle s_SwatchCountStyle;

        private void DrawBrushSwatch(byte value, Color c, int count)
        {
            if (s_SwatchCountStyle == null)
            {
                s_SwatchCountStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter, fontSize = 9 };
            }

            Rect slot = GUILayoutUtility.GetRect(26f, 36f, GUILayout.Width(26f));
            Rect r = new Rect(slot.x + 2f, slot.y, 22f, 22f);
            bool unused = value > 0 && count == 0;

            if (value == 0)
            {
                EditorGUI.DrawRect(r, new Color(0.25f, 0.25f, 0.25f));
                GUI.Label(new Rect(r.x + 5f, r.y + 2f, 20f, 18f), "×");
            }
            else
            {
                EditorGUI.DrawRect(r, unused ? Color.Lerp(c, new Color(0.22f, 0.22f, 0.22f), 0.65f) : c);
                GUI.Label(new Rect(slot.x - 4f, r.yMax + 1f, slot.width + 8f, 13f), count.ToString(), s_SwatchCountStyle);
            }

            if (m_Brush == value) DrawOutline(r, Color.white, 2f);

            string tip = value == 0
                ? "Silgi (sağ tıkla da silebilirsin)"
                : $"Renk {value} · #{ColorUtility.ToHtmlStringRGB(c)} · {count} hücre" + (unused ? " (kullanılmıyor)" : "");
            GUI.Label(r, new GUIContent("", tip));

            Event e = Event.current;
            if (value > 0 && r.Contains(e.mousePosition)) m_SwatchHoverValue = value;

            if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
            {
                m_Brush = value;
                if (value > 0) m_NewBrushColor = c;
                e.Use();
                Repaint();
            }
        }

        private void DrawSimilarColorWarnings(int[] counts)
        {
            Color[] pal = m_Canvas.ActivePalette;
            int shown = 0;
            for (int a = 1; a <= pal.Length && shown < 4; a++)
            {
                if (counts[a] == 0) continue;
                for (int b = a + 1; b <= pal.Length && shown < 4; b++)
                {
                    if (counts[b] == 0) continue;
                    if (SqrColorDistance(pal[a - 1], pal[b - 1]) >= SimilarColorSqrDistance) continue;
                    shown++;

                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    Rect ra = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f));
                    EditorGUI.DrawRect(ra, pal[a - 1]);
                    Rect rb = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f));
                    EditorGUI.DrawRect(rb, pal[b - 1]);
                    EditorGUILayout.LabelField($"⚠ Renk {a} ({counts[a]}) ve Renk {b} ({counts[b]}) çok benzer, oyunda ayırt edilemeyebilir",
                        EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button(new GUIContent("Birleştir", "Az kullanılan rengin hücrelerini çok kullanılana çevirir ve onu paletten kaldırır"),
                        EditorStyles.miniButton, GUILayout.Width(70)))
                    {
                        byte keep = (byte)(counts[a] >= counts[b] ? a : b);
                        byte drop = (byte)(keep == a ? b : a);
                        MergeColors(drop, keep);
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void MergeColors(byte drop, byte keep)
        {
            RecordCanvasUndo("Tuval: Renkleri Birleştir");
            Color keepColor = m_Canvas.ColorOf(keep);
            Color brushColor = m_Brush == drop ? keepColor : m_Canvas.ColorOf(m_Brush);
            m_Canvas.ReplaceColor(drop, keep);
            m_Canvas.RemoveColor(drop);
            if (m_Brush > 0) m_Brush = IndexOfColor(brushColor);
            CommitCanvasChange();
        }

        private byte IndexOfColor(Color c)
        {
            Color[] pal = m_Canvas.ActivePalette;
            for (int i = 0; i < pal.Length; i++)
            {
                if (pal[i] == c) return (byte)(i + 1);
            }
            return 1;
        }

        private static float SqrColorDistance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return dr * dr + dg * dg + db * db;
        }

        private void DrawCanvasArea()
        {
            Texture2D reference = GetReferenceTexture();
            bool showRef = m_ShowReference && reference != null;
            float viewW = EditorGUIUtility.currentViewWidth - 40f;
            float refW = showRef ? Mathf.Clamp(viewW * 0.32f, 110f, 240f) : 0f;
            float maxGridW = viewW - (showRef ? refW + 10f : 0f);

            EditorGUILayout.BeginHorizontal();
            DrawCanvasGrid(maxGridW);
            if (showRef)
            {
                GUILayout.Space(10f);
                DrawReferencePanel(reference, refW);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCanvasGrid(float maxW)
        {
            float cell = Mathf.Clamp(maxW / m_Canvas.Width, 6f, 22f);
            float w = cell * m_Canvas.Width;
            float h = cell * m_Canvas.Height;
            Color background = new Color(0.18f, 0.18f, 0.20f);

            Rect area = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));
            Event e = Event.current;

            m_HoverCell = new Vector2Int(-1, -1);
            if (area.Contains(e.mousePosition))
            {
                int hx = Mathf.Clamp(Mathf.FloorToInt((e.mousePosition.x - area.x) / cell), 0, m_Canvas.Width - 1);
                // Ekranda üst satır, tuvalin ÜST satırı olsun diye y ters çevrilir
                int hy = m_Canvas.Height - 1 - Mathf.Clamp(Mathf.FloorToInt((e.mousePosition.y - area.y) / cell), 0, m_Canvas.Height - 1);
                m_HoverCell = new Vector2Int(hx, hy);
            }

            // Palette ya da (Rengi Değiştir aracında) tuvalde üzerine gelinen rengin hücreleri öne çıkar,
            // diğerleri soluklaşır. Kalem/Damlalıkta tuval hover'ı vurgulamaz; çizerken dikkat dağıtıyordu.
            int highlight = m_SwatchHoverValue > 0 ? m_SwatchHoverValue
                : (m_Tool == CanvasTool.RenkDegistir && m_HoverCell.x >= 0 ? m_Canvas.Get(m_HoverCell.x, m_HoverCell.y) : 0);

            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(area, background);
                for (int y = 0; y < m_Canvas.Height; y++)
                {
                    for (int x = 0; x < m_Canvas.Width; x++)
                    {
                        byte v = m_Canvas.Get(x, y);
                        if (v == 0) continue;
                        Color c = m_Canvas.ColorOf(v);
                        if (highlight > 0 && v != highlight) c = Color.Lerp(c, background, 0.65f);
                        EditorGUI.DrawRect(CellRect(area, cell, x, y), c);
                    }
                }

                if (m_MirrorX)
                {
                    EditorGUI.DrawRect(new Rect(area.x + w * 0.5f - 1f, area.y, 2f, h), new Color(1f, 1f, 1f, 0.35f));
                }

                if (m_HoverCell.x >= 0)
                {
                    DrawOutline(CellRect(area, cell, m_HoverCell.x, m_HoverCell.y), Color.white, 1.5f);
                    if (m_MirrorX)
                    {
                        int mx = m_Canvas.Width - 1 - m_HoverCell.x;
                        if (mx != m_HoverCell.x) DrawOutline(CellRect(area, cell, mx, m_HoverCell.y), new Color(1f, 1f, 1f, 0.45f), 1.5f);
                    }
                }
            }

            bool pointer = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && (e.button == 0 || e.button == 1);
            if (pointer && m_HoverCell.x >= 0)
            {
                ApplyToolAt(m_HoverCell.x, m_HoverCell.y, e);
                e.Use();
            }
        }

        private Rect CellRect(Rect area, float cell, int x, int y)
        {
            return new Rect(area.x + x * cell, area.y + (m_Canvas.Height - 1 - y) * cell, cell - 1f, cell - 1f);
        }

        private static void DrawOutline(Rect r, Color c, float t)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        private void ApplyToolAt(int x, int y, Event e)
        {
            bool erase = e.button == 1;
            bool pick = !erase && (m_Tool == CanvasTool.Damlalik || e.alt);

            if (pick)
            {
                if (e.type != EventType.MouseDown) return;
                byte v = m_Canvas.Get(x, y);
                m_Brush = v;
                if (v > 0) m_NewBrushColor = m_Canvas.ColorOf(v);
                if (m_Tool == CanvasTool.Damlalik) m_Tool = CanvasTool.Kalem;
                Repaint();
                return;
            }

            if (m_Tool == CanvasTool.RenkDegistir && !erase)
            {
                if (e.type != EventType.MouseDown) return;
                byte from = m_Canvas.Get(x, y);
                if (from == 0 || from == m_Brush) return;
                RecordCanvasUndo("Tuval: Rengi Değiştir");
                int changed = m_Canvas.ReplaceColor(from, m_Brush);
                CommitCanvasChange();
                ShowNotification(new GUIContent(m_Brush == 0 ? $"{changed} hücre silindi" : $"{changed} hücre Renk {m_Brush} yapıldı"), 0.8);
                return;
            }

            PaintCell(x, y, erase ? (byte)0 : m_Brush);
        }

        private void PaintCell(int x, int y, byte value)
        {
            int mx = m_Canvas.Width - 1 - x;
            bool changed = m_Canvas.Get(x, y) != value || (m_MirrorX && m_Canvas.Get(mx, y) != value);
            if (!changed) return;

            RecordCanvasUndo("Tuval: Boya");
            m_Canvas.Set(x, y, value);
            if (m_MirrorX) m_Canvas.Set(mx, y, value);
            CommitCanvasChange();
        }

        private Texture2D GetReferenceTexture()
        {
            return m_EditingLevel != null ? m_EditingLevel.GetOriginalTexture() : null;
        }

        private void DrawReferencePanel(Texture2D tex, float width)
        {
            float height = width * tex.height / Mathf.Max(1f, tex.width);

            EditorGUILayout.BeginVertical(GUILayout.Width(width));
            EditorGUILayout.LabelField("🖼 Orijinal görsel", EditorStyles.miniBoldLabel, GUILayout.Width(width));
            Rect r = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            EditorGUILayout.LabelField("Tıkla: rengi fırçaya al", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(width));
            EditorGUILayout.EndVertical();

            Event e = Event.current;
            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, new Color(0.18f, 0.18f, 0.20f));
                GUI.DrawTexture(r, tex, ScaleMode.StretchToFill);
            }

            if (!r.Contains(e.mousePosition)) return;

            Color32? sampled = SampleReference(tex, r, e.mousePosition);
            if (!sampled.HasValue) return;
            m_ReferenceHoverColor = sampled.Value;

            if (e.type == EventType.Repaint)
            {
                Rect chip = new Rect(e.mousePosition.x + 12f, e.mousePosition.y + 12f, 18f, 18f);
                EditorGUI.DrawRect(chip, Color.black);
                EditorGUI.DrawRect(new Rect(chip.x + 2f, chip.y + 2f, 14f, 14f), sampled.Value);
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                PickColorFromImage(sampled.Value);
                e.Use();
            }
        }

        private Color32? SampleReference(Texture2D tex, Rect r, Vector2 mouse)
        {
            if (m_ReferenceTex != tex || m_ReferencePixels == null)
            {
                m_ReferenceTex = tex;
                m_ReferencePixels = LevelCanvas.ReadPixels32(tex);
            }
            if (m_ReferencePixels == null || m_ReferencePixels.Length != tex.width * tex.height) return null;

            int px = Mathf.Clamp(Mathf.FloorToInt((mouse.x - r.x) / r.width * tex.width), 0, tex.width - 1);
            // Doku satırları alttan üste dizili, ekran ise yukarıdan aşağı
            int py = Mathf.Clamp(Mathf.FloorToInt((1f - (mouse.y - r.y) / r.height) * tex.height), 0, tex.height - 1);
            Color32 c = m_ReferencePixels[py * tex.width + px];
            if (c.a < 51) return null;
            return c;
        }

        /// <summary>
        /// Görselden alınan rengi fırça yapar: palette neredeyse aynı renk varsa onu seçer (JPEG gürültüsü
        /// yüzünden palete kopya renkler dolmasın diye), yoksa yeni renk olarak ekler.
        /// </summary>
        private void PickColorFromImage(Color c)
        {
            c.a = 1f;
            Color[] pal = m_Canvas.ActivePalette;
            int nearest = -1;
            float best = PickSnapSqrDistance;
            for (int i = 0; i < pal.Length; i++)
            {
                float d = SqrColorDistance(pal[i], c);
                if (d < best) { best = d; nearest = i; }
            }

            if (nearest >= 0)
            {
                m_Brush = (byte)(nearest + 1);
                m_NewBrushColor = pal[nearest];
                ShowNotification(new GUIContent($"Paletteki Renk {m_Brush} seçildi"), 0.6);
            }
            else
            {
                RecordCanvasUndo("Tuval: Görselden Renk Ekle");
                m_Brush = m_Canvas.AddColor(c);
                m_NewBrushColor = c;
                CommitCanvasChange();
                ShowNotification(new GUIContent($"Görselden yeni renk eklendi (Renk {m_Brush})"), 0.6);
            }

            if (m_Tool == CanvasTool.Damlalik) m_Tool = CanvasTool.Kalem;
            Repaint();
        }

        private void DrawCanvasStatusLine(int[] counts)
        {
            string text;
            if (m_HoverCell.x >= 0)
            {
                byte v = m_Canvas.Get(m_HoverCell.x, m_HoverCell.y);
                string pos = $"({m_HoverCell.x + 1}, {m_HoverCell.y + 1})";
                text = v == 0
                    ? $"{pos} · boş"
                    : $"{pos} · Renk {v} · #{ColorUtility.ToHtmlStringRGB(m_Canvas.ColorOf(v))} · {(v < counts.Length ? counts[v] : 0)} hücre";
            }
            else if (m_ReferenceHoverColor.HasValue)
            {
                text = $"Görsel rengi: #{ColorUtility.ToHtmlStringRGB(m_ReferenceHoverColor.Value)} · tıkla, fırçaya al";
            }
            else
            {
                text = $"{m_Canvas.FilledCount} dolu hücre · sol tık boya, sağ tık sil · Alt+tık renk seç · B / I / R araç, M simetri, Ctrl+Z geri al";
            }
            EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------------
        // Geri al / ileri al: tuval durumu Unity Undo'ya LevelCanvasUndoState üzerinden kaydedilir
        // ------------------------------------------------------------------

        private void RecordCanvasUndo(string name)
        {
            if (m_Canvas == null) return;
            if (m_UndoState == null)
            {
                m_UndoState = CreateInstance<LevelCanvasUndoState>();
                m_UndoState.hideFlags = HideFlags.HideAndDontSave;
                m_UndoState.CaptureFrom(m_Canvas);
            }
            Undo.RecordObject(m_UndoState, name);
        }

        private void CommitCanvasChange()
        {
            if (m_UndoState != null) m_UndoState.CaptureFrom(m_Canvas);
            InvalidateCanvas();
            Repaint();
        }

        /// <summary>Tuval tamamen değiştiğinde (başka bölüm yüklendi vb.) eski geçmişi bırakır.</summary>
        private void ResetCanvasUndo()
        {
            if (m_UndoState == null) return;
            Undo.ClearUndo(m_UndoState);
            m_UndoState.CaptureFrom(m_Canvas);
        }

        private void OnUndoRedo()
        {
            if (m_UndoState == null || m_Canvas == null) return;
            m_UndoState.ApplyTo(m_Canvas);
            m_CanvasSize = Mathf.Max(m_Canvas.Width, m_Canvas.Height);
            if (m_Brush > m_Canvas.ActivePalette.Length) m_Brush = 1;
            InvalidateCanvas();
            Repaint();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }
        private void InvalidateCanvas()
        {
            m_Preview = null;
            if (m_CanvasPreview != null) { DestroyImmediate(m_CanvasPreview); m_CanvasPreview = null; }
        }

        /// <summary>Aktif kaynağı doku olarak verir (çiz modunda tuvalden üretir).</summary>
        private Texture2D CurrentTexture()
        {
            if (m_Mode == SourceMode.Gorsel) return m_Source;
            if (m_Canvas == null || m_Canvas.IsEmpty) return null;
            if (m_CanvasPreview == null) m_CanvasPreview = m_Canvas.ToTexture();
            return m_CanvasPreview;
        }

        private void OnDisable()
        {
            if (m_CanvasPreview != null) { DestroyImmediate(m_CanvasPreview); m_CanvasPreview = null; }

            Undo.undoRedoPerformed -= OnUndoRedo;
            if (m_UndoState != null)
            {
                Undo.ClearUndo(m_UndoState);
                DestroyImmediate(m_UndoState);
                m_UndoState = null;
            }
            m_ReferencePixels = null;
            m_ReferenceTex = null;
        }

        // ------------------------------------------------------------------
        private void DrawStep1Image()
        {
            EditorGUILayout.LabelField("1 · Görsel", EditorStyles.boldLabel);

            Rect drop = GUILayoutUtility.GetRect(0f, 120f, GUILayout.ExpandWidth(true));
            GUI.Box(drop, GUIContent.none, EditorStyles.helpBox);

            if (m_Source != null)
            {
                float side = Mathf.Min(drop.height - 12f, 100f);
                Rect thumb = new Rect(drop.x + 10f, drop.y + 10f, side, side);
                GUI.DrawTexture(thumb, m_Source, ScaleMode.ScaleToFit);

                Rect info = new Rect(thumb.xMax + 12f, drop.y + 14f, drop.width - side - 34f, drop.height - 20f);
                GUI.Label(info, $"{m_Source.name}\n{m_Source.width} × {m_Source.height} piksel\n\nDeğiştirmek için yeni bir görsel sürükle.", EditorStyles.wordWrappedLabel);
            }
            else
            {
                var c = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 12 };
                GUI.Label(drop, "\nPiksel görselini buraya sürükle\n(PNG / Texture2D)", c);
            }

            HandleDrop(drop);

            if (GUILayout.Button(m_Source == null ? "Görsel Seç…" : "Başka Görsel Seç…", GUILayout.Height(22)))
            {
                string path = EditorUtility.OpenFilePanel("Piksel görseli seç", "Assets", "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                {
                    string rel = "Assets" + path.Substring(Application.dataPath.Length);
                    SetSource(AssetDatabase.LoadAssetAtPath<Texture2D>(rel));
                }
                else if (!string.IsNullOrEmpty(path))
                {
                    EditorUtility.DisplayDialog("Proje dışı", "Görsel projenin Assets klasörü içinde olmalı.", "Tamam");
                }
            }

            if (m_Source != null)
            {
                int colorCount = PixelPaletteOptimizer.CountUniqueColors(m_Source);
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"🎨 Görsel Renk Sayısı: {colorCount}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (colorCount > 5)
                {
                    GUI.contentColor = new Color(1f, 0.65f, 0.2f);
                    EditorGUILayout.LabelField("⚠ Çok renkli (4-5 renk önerilir)", EditorStyles.miniBoldLabel);
                    GUI.contentColor = Color.white;
                }
                else
                {
                    GUI.contentColor = new Color(0.35f, 0.85f, 0.4f);
                    EditorGUILayout.LabelField("✓ İdeal renk aralığı", EditorStyles.miniBoldLabel);
                    GUI.contentColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Hedef Renk:", GUILayout.Width(75));
                m_TargetColorCount = EditorGUILayout.IntSlider(m_TargetColorCount, 2, 16, GUILayout.Width(170));
                
                GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
                if (GUILayout.Button($"🎨 {m_TargetColorCount} Renge İndirge", GUILayout.Height(22)))
                {
                    ApplyQuantize(m_TargetColorCount);
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("🧹 Temizle", GUILayout.Width(75), GUILayout.Height(22)))
                {
                    ApplyDenoise();
                }
                if (m_OriginalSource != null && m_Source != m_OriginalSource)
                {
                    GUI.backgroundColor = new Color(0.9f, 0.9f, 0.95f);
                    if (GUILayout.Button("↩ Orijinale Dön", GUILayout.Width(95), GUILayout.Height(22)))
                    {
                        m_Source = m_OriginalSource;
                        m_Preview = null;
                        Repaint();
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();

                // Hızlı seçim butonları
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Hızlı Butonlar:", EditorStyles.miniLabel, GUILayout.Width(80));
                int[] quickPresets = { 3, 4, 5, 6, 7, 8 };
                foreach (int q in quickPresets)
                {
                    if (GUILayout.Button(q.ToString(), EditorStyles.miniButton, GUILayout.Width(30)))
                    {
                        m_TargetColorCount = q;
                        ApplyQuantize(q);
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        private void ApplyQuantize(int targetColors)
        {
            Texture2D baseTex = m_OriginalSource != null ? m_OriginalSource : m_Source;
            if (baseTex == null) return;
            Texture2D quantized = PixelPaletteOptimizer.Quantize(baseTex, targetColors, 0.2f, true);
            if (quantized != null)
            {
                Texture2D saved = PixelPaletteOptimizer.SaveAsOptimizedAsset(baseTex, quantized, $"_{targetColors}c");
                DestroyImmediate(quantized);
                if (saved != null) SetSource(saved, false);
            }
        }

        private void ApplyDenoise()
        {
            Texture2D baseTex = m_OriginalSource != null ? m_OriginalSource : m_Source;
            if (baseTex == null) return;
            Texture2D cleaned = PixelPaletteOptimizer.CleanIsolatedPixels(baseTex);
            if (cleaned != null)
            {
                Texture2D saved = PixelPaletteOptimizer.SaveAsOptimizedAsset(baseTex, cleaned, "_denoise");
                DestroyImmediate(cleaned);
                if (saved != null) SetSource(saved, false);
            }
        }

        private void HandleDrop(Rect area)
        {
            var e = Event.current;
            if (!area.Contains(e.mousePosition)) return;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;

            Texture2D found = null;
            foreach (var o in DragAndDrop.objectReferences)
            {
                if (o is Texture2D t) { found = t; break; }
                if (o is Sprite sp && sp.texture != null) { found = sp.texture; break; }
            }

            DragAndDrop.visualMode = found != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (e.type == EventType.DragPerform && found != null)
            {
                DragAndDrop.AcceptDrag();
                SetSource(found);
            }
            e.Use();
        }

        private void SetSource(Texture2D tex, bool isNewOriginal = true)
        {
            if (tex == null) return;
            if (isNewOriginal) m_OriginalSource = tex;
            m_Source = tex;
            m_LevelName = tex.name.Replace("_", " ");
            m_Preview = null;
            m_LastCreated = null;
            Repaint();
        }

        // ------------------------------------------------------------------
        private void DrawStep2Difficulty()
        {
            EditorGUILayout.LabelField("2 · Zorluk", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
            {
                bool on = m_Difficulty == d;
                var style = new GUIStyle(GUI.skin.button) { fontStyle = on ? FontStyle.Bold : FontStyle.Normal };
                Color prev = GUI.backgroundColor;
                if (on) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
                if (GUILayout.Button(d.ToString(), style, GUILayout.Height(34)))
                {
                    m_Difficulty = d;
                    m_Preview = null;
                }
                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(Preset(m_Difficulty).blurb, EditorStyles.miniLabel);
        }

        private void DrawStep3Name()
        {
            EditorGUILayout.LabelField("3 · İsim", EditorStyles.boldLabel);
            m_LevelName = EditorGUILayout.TextField(m_LevelName);
        }

        // ------------------------------------------------------------------
        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Önizleme", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            Texture2D src = CurrentTexture();
            if (src == null)
            {
                EditorGUILayout.LabelField(m_Mode == SourceMode.Gorsel ? "Önce bir görsel seç." : "Önce bir şeyler çiz.",
                                           EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            bool stale = m_Preview == null || m_PreviewFor != src || m_PreviewDifficulty != m_Difficulty;

            if (stale)
            {
                EditorGUILayout.LabelField("Bu görselden nasıl bir bölüm çıkacağını görmek için kontrol et.", EditorStyles.miniLabel);
                if (GUILayout.Button("🔍 Kontrol Et", GUILayout.Height(26)))
                {
                    m_Preview = BuildPreviewReport();
                    m_PreviewFor = src;
                    m_PreviewDifficulty = m_Difficulty;
                }
                EditorGUILayout.EndVertical();
                return;
            }

            var r = m_Preview;
            if (!r.valid)
            {
                EditorGUILayout.HelpBox(r.invalidReason, MessageType.Error);
                EditorGUILayout.EndVertical();
                return;
            }

            var (slots, cap, _) = Preset(m_Difficulty);

            EditorGUILayout.BeginHorizontal();
            Stat("Küp", r.totalCubes.ToString());
            Stat("Renk", r.budgets.Count.ToString());
            Stat("Yanaşma yeri", slots.ToString());
            Stat("Süre", $"~{Mathf.RoundToInt(r.totalCubes * 0.07f)} sn");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            float risk = r.DeadlockRisk;
            if (risk <= 0.001f)
            {
                Verdict("✓ Oynanabilir", "Bölüm sonuna kadar bitirilebiliyor, kilitlenme görülmedi.",
                        new Color(0.35f, 0.85f, 0.4f), MessageType.None);
            }
            else if (risk < 0.10f)
            {
                Verdict($"⚠ Riskli (%{risk * 100f:F0})", "Bazı oyunlarda tıkanabilir. Zorluğu düşürmeyi veya görseldeki renk sayısını azaltmayı dene.",
                        new Color(0.95f, 0.85f, 0.3f), MessageType.Warning);
            }
            else
            {
                Verdict($"✖ Kilitleniyor (%{risk * 100f:F0})", r.blockReason,
                        new Color(1f, 0.45f, 0.4f), MessageType.Error);
            }

            // Renk şeridi
            if (r.budgets.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Renkler", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                foreach (var b in r.budgets)
                {
                    Rect sw = GUILayoutUtility.GetRect(30f, 18f, GUILayout.Width(30f));
                    EditorGUI.DrawRect(sw, b.color);
                    GUI.Label(new Rect(sw.x, sw.yMax, 30f, 12f), b.cubeCount.ToString(), EditorStyles.miniLabel);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(12);
            }

            if (r.budgets.Count > slots)
            {
                EditorGUILayout.HelpBox(
                    $"Görselde {r.budgets.Count} renk var ama aynı anda sadece {slots} gemi yanaşıyor. " +
                    "Çok renkli görseller bu zorlukta tıkanmaya daha yatkın.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private static void Stat(string label, string value)
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(value, new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 });
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private static void Verdict(string title, string body, Color color, MessageType type)
        {
            GUI.contentColor = color;
            EditorGUILayout.LabelField(title, new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 });
            GUI.contentColor = Color.white;
            EditorGUILayout.HelpBox(body, type);
        }

        /// <summary>
        /// Asset oluşturmadan, bellekte geçici bir bölüm kurup analiz eder.
        /// Böylece kullanıcı "oluştur"a basmadan sonucu görür.
        /// </summary>
        private LevelSolvabilityAnalyzer.Report BuildPreviewReport()
        {
            var temp = ScriptableObject.CreateInstance<PixelLevelData>();
            try
            {
                Configure(temp, CurrentTexture(), m_Difficulty, m_LevelName, 0);
                return LevelSolvabilityAnalyzer.Analyze(temp, true, 25);
            }
            finally
            {
                DestroyImmediate(temp);
            }
        }

        // ------------------------------------------------------------------
        private void DrawCreate()
        {
            using (new EditorGUI.DisabledScope(CurrentTexture() == null))
            {
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.5f);
                if (GUILayout.Button("Bölümü Oluştur", GUILayout.Height(38)))
                {
                    m_LastCreated = CreateLevelAsset();
                }
                GUI.backgroundColor = prev;
            }

            if (m_LastCreated != null)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox($"'{m_LastCreated.LevelName}' oluşturuldu:\n{AssetDatabase.GetAssetPath(m_LastCreated)}", MessageType.Info);

                Color pv = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.45f, 0.7f, 1f);
                if (GUILayout.Button("▶  Sahnede Test Et", GUILayout.Height(30)))
                {
                    if (BuildInScene(m_LastCreated, out string msg))
                        ShowNotification(new GUIContent(msg));
                    else
                        EditorUtility.DisplayDialog("Sahne hazır değil", msg, "Tamam");
                }
                GUI.backgroundColor = pv;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Projede Göster", GUILayout.Height(24)))
                {
                    EditorGUIUtility.PingObject(m_LastCreated);
                    Selection.activeObject = m_LastCreated;
                }
                if (GUILayout.Button("İnce Ayar İçin Designer'da Aç", GUILayout.Height(24)))
                {
                    var win = GetWindow<PixelLevelDesignerWindow>(false, "Level Designer");
                    win.Show();
                    win.Focus();
                    EditorGUIUtility.PingObject(m_LastCreated);
                    Selection.activeObject = m_LastCreated;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private PixelLevelData CreateLevelAsset()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Levels"))
                AssetDatabase.CreateFolder("Assets", "Levels");

            int nextIndex = CountExistingLevels() + 1;
            string safe = string.IsNullOrWhiteSpace(m_LevelName) ? "Bolum" : m_LevelName;
            safe = safe.Replace(" ", "_");

            // Çizim modunda tuval önce PNG olarak diske yazılır; bölüm kalıcı bir
            // doku asset'ine bağlanmalı, bellekteki geçici doku işe yaramaz.
            Texture2D tex = m_Mode == SourceMode.Gorsel ? m_Source : SaveCanvasAsPng(safe, nextIndex);
            if (tex == null) return null;

            string path = AssetDatabase.GenerateUniqueAssetPath($"Assets/Levels/Level_{nextIndex:D2}_{safe}.asset");

            var level = ScriptableObject.CreateInstance<PixelLevelData>();
            Configure(level, tex, m_Difficulty, m_LevelName, nextIndex);

            AssetDatabase.CreateAsset(level, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00FFAA><b>[Bölüm Stüdyosu]</b></color> Oluşturuldu: {path}");
            return level;
        }

        /// <summary>
        /// Bölümü kurar. Sayısal görsel ayarlar (boşluk, derinlik, eğim) elle girilmez;
        /// mevcut bir bölümden kopyalanır ki yeni bölüm diğerleriyle aynı görünsün.
        /// </summary>
        private static void Configure(PixelLevelData level, Texture2D tex, Difficulty diff, string name, int index)
        {
            EnsureReadable(tex);

            var (slots, cap, _) = Preset(diff);

            level.LevelName = string.IsNullOrWhiteSpace(name) ? tex.name : name;
            level.LevelIndex = index;
            level.LevelTexture = tex;
            level.OriginalSourceTexture = tex;
            level.UseNativeResolution = true;
            level.SlotCount = slots;
            level.TruckCapacity = cap;

            CopyVisualsFromReference(level);

            level.ExtractPaletteFromTexture();
            level.GenerateInterleavedWagonSequenceFromPalette();
            level.UseCustomWagonSequence = true;
        }

        private static void CopyVisualsFromReference(PixelLevelData target)
        {
            PixelLevelData reference = null;
            foreach (string guid in AssetDatabase.FindAssets("t:PixelLevelData"))
            {
                var lvl = AssetDatabase.LoadAssetAtPath<PixelLevelData>(AssetDatabase.GUIDToAssetPath(guid));
                if (lvl == null || lvl == target) continue;
                if (reference == null) reference = lvl;
                if (lvl.LevelName != null && lvl.LevelName.IndexOf("Rakun", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    reference = lvl;
                    break;
                }
            }
            if (reference == null) return;

            target.CubeSpacing = reference.CubeSpacing;
            target.CubeSpacingX = reference.CubeSpacingX;
            target.CubeDepth = reference.CubeDepth;
            target.BoardTiltAngle = reference.BoardTiltAngle;
            target.CubeFrontTiltAngle = reference.CubeFrontTiltAngle;
            target.TargetZ = reference.TargetZ;
            target.CubeRowStepOffset = reference.CubeRowStepOffset;
            target.InnerPadding = reference.InnerPadding;
            target.SkipTransparent = reference.SkipTransparent;
            target.ColorBrightness = reference.ColorBrightness;
            target.ColorSaturation = reference.ColorSaturation;
            target.ColorContrast = reference.ColorContrast;
        }

        /// <summary>Tuvali projeye PNG olarak yazar ve import ayarlarını bölüm için uygun hale getirir.</summary>
        private Texture2D SaveCanvasAsPng(string safeName, int index)
        {
            const string dir = "Assets/PixelArt/Custom";
            if (!AssetDatabase.IsValidFolder("Assets/PixelArt"))
                AssetDatabase.CreateFolder("Assets", "PixelArt");
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/PixelArt", "Custom");

            Texture2D baked = m_Canvas.ToTexture();
            string pngPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{safeName}_{index:D2}.png");
            System.IO.File.WriteAllBytes(pngPath, baked.EncodeToPNG());
            DestroyImmediate(baked);

            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            var imp = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.isReadable = true;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        }

        /// <summary>Bölümü sahnedeki generator'a yükleyip küpleri inşa eder.</summary>
        private static bool BuildInScene(PixelLevelData level, out string message)
        {
            var gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen == null)
            {
                message = "Sahnede PixelArtGenerator yok. Önce oyun sahnesini (Gemi.unity) aç.";
                return false;
            }

            gen.LoadLevel(level);
            SceneView.RepaintAll();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            message = $"'{level.LevelName}' sahnede inşa edildi.";
            return true;
        }

        private static int CountExistingLevels()
        {
            return AssetDatabase.FindAssets("t:PixelLevelData").Length;
        }

        private static void EnsureReadable(Texture2D tex)
        {
            if (tex == null || tex.isReadable) return;
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && !imp.isReadable)
            {
                imp.isReadable = true;
                imp.SaveAndReimport();
            }
        }
    }
}
