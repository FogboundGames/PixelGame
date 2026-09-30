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
                    if (m_Mode == SourceMode.Ciz && m_Canvas == null) m_Canvas = new LevelCanvas(m_CanvasSize);
                }
                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------
        private void DrawStep1Canvas()
        {
            if (m_Canvas == null) m_Canvas = new LevelCanvas(m_CanvasSize);

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
                    m_Canvas = LevelCanvas.Template(i);
                    m_CanvasSize = Mathf.Max(m_Canvas.Width, m_Canvas.Height);
                    if (string.IsNullOrWhiteSpace(m_LevelName)) m_LevelName = LevelCanvas.TemplateNames[i];
                    InvalidateCanvas();
                }
            }
            EditorGUILayout.EndHorizontal();

            // Boyut + temizle
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Izgara:", GUILayout.Width(80));
            int newSize = EditorGUILayout.IntSlider(m_CanvasSize, 8, 32);
            if (newSize != m_CanvasSize)
            {
                m_CanvasSize = newSize;
                m_Canvas.Resize(newSize, newSize);
                InvalidateCanvas();
            }
            if (GUILayout.Button("Temizle", EditorStyles.miniButton, GUILayout.Width(70)))
            {
                m_Canvas.Clear();
                InvalidateCanvas();
            }
            EditorGUILayout.EndHorizontal();

            // Fırça paleti (0 = silgi)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Renk ekleme ve düzenleme araçları
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🎨 Palet & Fırça:", EditorStyles.boldLabel, GUILayout.Width(105));

            m_NewBrushColor = EditorGUILayout.ColorField(GUIContent.none, m_NewBrushColor, false, false, false, GUILayout.Width(45));

            if (GUILayout.Button("➕ Renk Ekle", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                if (m_Canvas != null)
                {
                    m_Brush = m_Canvas.AddColor(m_NewBrushColor);
                    InvalidateCanvas();
                    Repaint();
                }
            }

            EditorGUI.BeginDisabledGroup(m_Brush == 0);
            if (GUILayout.Button("✏️ Seçiliyi Güncelle", EditorStyles.miniButton, GUILayout.Width(115)))
            {
                if (m_Canvas != null && m_Brush > 0)
                {
                    m_Canvas.UpdateColor(m_Brush, m_NewBrushColor);
                    InvalidateCanvas();
                    Repaint();
                }
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            if (m_Canvas != null && m_Canvas.CustomPalette != null)
            {
                if (GUILayout.Button("Paleti Sıfırla", EditorStyles.miniButton, GUILayout.Width(85)))
                {
                    if (EditorUtility.DisplayDialog("Paleti Sıfırla", "Palet varsayılan 10 renge döndürülecek. Onaylıyor musunuz?", "Evet", "Hayır"))
                    {
                        m_Canvas.CustomPalette = null;
                        m_Brush = 1;
                        InvalidateCanvas();
                        Repaint();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // Renk kutucukları (swatch listesi) - sığmayanlar otomatik alt satıra geçer
            Color[] activePalette = m_Canvas != null ? m_Canvas.ActivePalette : LevelCanvas.Palette;
            int totalSwatches = 1 + activePalette.Length; // 0 = silgi
            float viewWidth = Mathf.Max(260f, EditorGUIUtility.currentViewWidth - 60f);
            int swatchesPerRow = Mathf.Max(6, Mathf.FloorToInt(viewWidth / 26f));

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
                    DrawBrushSwatch(0, Color.clear, "Silgi (sağ tıkla da silebilirsin)");
                }
                else
                {
                    byte idx = (byte)s;
                    DrawBrushSwatch(idx, activePalette[s - 1], $"Renk {s}");
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
            DrawCanvasGrid();

            EditorGUILayout.LabelField($"{m_Canvas.FilledCount} dolu hücre · sol tık boya, sağ tık sil", EditorStyles.miniLabel);
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

        private void DrawBrushSwatch(byte value, Color c, string tip)
        {
            Rect r = GUILayoutUtility.GetRect(22f, 22f, GUILayout.Width(22f));
            if (value == 0)
            {
                EditorGUI.DrawRect(r, new Color(0.25f, 0.25f, 0.25f));
                GUI.Label(new Rect(r.x + 5f, r.y + 2f, 20f, 18f), "×");
            }
            else EditorGUI.DrawRect(r, c);

            if (m_Brush == value)
            {
                Handles.BeginGUI();
                Handles.color = Color.white;
                Handles.DrawAAPolyLine(2.5f,
                    new Vector3(r.x, r.y), new Vector3(r.xMax, r.y),
                    new Vector3(r.xMax, r.yMax), new Vector3(r.x, r.yMax), new Vector3(r.x, r.y));
                Handles.EndGUI();
            }

            if (!string.IsNullOrEmpty(tip))
            {
                GUI.Label(r, new GUIContent("", tip));
            }

            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                m_Brush = value;
                if (value > 0)
                {
                    m_NewBrushColor = c;
                }
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawCanvasGrid()
        {
            float maxW = EditorGUIUtility.currentViewWidth - 40f;
            float cell = Mathf.Clamp(maxW / m_Canvas.Width, 6f, 22f);
            float w = cell * m_Canvas.Width;
            float h = cell * m_Canvas.Height;

            Rect area = GUILayoutUtility.GetRect(w, h, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(area, new Color(0.18f, 0.18f, 0.20f));

            for (int y = 0; y < m_Canvas.Height; y++)
            {
                for (int x = 0; x < m_Canvas.Width; x++)
                {
                    byte v = m_Canvas.Get(x, y);
                    if (v == 0) continue;
                    // Ekranda üst satır, tuvalin ÜST satırı olsun diye y ters çevrilir
                    var cr = new Rect(area.x + x * cell, area.y + (m_Canvas.Height - 1 - y) * cell, cell - 1f, cell - 1f);
                    EditorGUI.DrawRect(cr, m_Canvas.ColorOf(v));
                }
            }

            var e = Event.current;
            bool paint = e.type == EventType.MouseDown || e.type == EventType.MouseDrag;
            if (paint && area.Contains(e.mousePosition) && (e.button == 0 || e.button == 1))
            {
                int gx = Mathf.FloorToInt((e.mousePosition.x - area.x) / cell);
                int gy = m_Canvas.Height - 1 - Mathf.FloorToInt((e.mousePosition.y - area.y) / cell);
                m_Canvas.Set(gx, gy, e.button == 1 ? (byte)0 : m_Brush);
                InvalidateCanvas();
                e.Use();
                Repaint();
            }
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
