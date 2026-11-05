using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Görselleri sürükleyip bırakarak 162 seviye benchmark kurallarına dayalı,
    /// %100 çözülebilir, bulmaca derinliği, havuz (sıra/kolon) ve yanaşma slotları
    /// serbestçe belirlenebilir interaktif seviye stüdyosu.
    /// </summary>
    public class SmartLevelGeneratorWindow : EditorWindow
    {
        // 1. Kaynak Görsel & Seviye Bilgileri
        private Texture2D m_SourceTexture;
        private string m_LevelName = "Yeni Seviye";
        private int m_TargetLevelIndex = 163;
        private LevelBenchmarkEngine.DifficultyPreset m_Preset = LevelBenchmarkEngine.DifficultyPreset.Zor;
        private LevelBenchmarkEngine.PuzzleDurationPacing m_DurationPacing = LevelBenchmarkEngine.PuzzleDurationPacing.TacticalDeep;
        private int m_TargetShipCount = 45;
        private int m_TargetColorCount = 0;
        private bool m_DenoiseKMeans = true;

        // 2. Havuz & Slot Ayarları (Kullanıcının İstediği Birebir Kontroller)
        private int m_PoolRows = 2;       // Sıra (dalga) [−] [2] [+]
        private int m_PoolColumns = 5;    // Kolon        [−] [5] [+]
        private int m_SlotCount = 5;      // ⚓ Yanaşma Slotu [−] [5] [+]

        // 3. Gemi Kapasiteleri (Sayı Dağılımı)
        private int m_MinCapacity = 10;   // Min Kapasite
        private int m_PeakCapacity = 20;  // En Sık (Peak) Kapasite
        private int m_MaxCapacity = 30;   // Maks Kapasite
        private bool m_PreferRoundCapacities = true;
        private float m_RoundRatio = 0.70f;

        // 4. Mekanik Kuralları
        private bool m_GenerateLinks = true;
        private bool m_GenerateHidden = true;
        private bool m_GenerateMysteryCubes = true;
        private int m_MysteryCubePercent = 35;
        private MysteryRevealCondition m_MysteryRevealCondition = MysteryRevealCondition.WhenExposed;
        private bool m_AllowDualColor = false;
        private bool m_GuaranteeSolvability = true;
        private int m_SolvabilityTrials = 80;

        // 5. Üretilen Durum (State)
        private LevelBenchmarkEngine.GenerationResult m_CurrentResult;
        private List<WagonSequenceEntry> m_WorkingSequence = new List<WagonSequenceEntry>();
        private List<PaletteColorOverride> m_WorkingPalette = new List<PaletteColorOverride>();
        private List<Vector2Int> m_WorkingMysteryCubes = new List<Vector2Int>();
        private int m_TotalCubes = 0;

        // 6. GUI & Scroll Durumları
        private Vector2 m_MainScroll;
        private Vector2 m_QueueScroll;
        private bool m_ViewCards = true;
        private string m_StatusMessage = "";
        private MessageType m_StatusType = MessageType.Info;
        private PixelLevelData m_LastSavedLevel;

        // Renkler & Stiller
        private GUIStyle m_HeaderStyle;
        private GUIStyle m_CardStyle;
        private GUIStyle m_PoolCardStyle;
        private GUIStyle m_DropZoneStyle;

        [MenuItem("Tools/PixelGame/🚀 Akıllı Seviye & Sıra Üreticisi (Smart Level Studio)", priority = 3)]
        public static void OpenWindow()
        {
            var win = GetWindow<SmartLevelGeneratorWindow>("Akıllı Seviye Üreticisi");
            win.minSize = new Vector2(1000, 760);
            win.Show();
        }

        private void OnEnable()
        {
            RefreshTargetIndexFromSequence();
            m_StatusMessage = "162 Seviye Analiz Kuralları ve Havuz/Slot Kontrolleri Hazır. Bir görsel sürükleyin!";
            m_StatusType = MessageType.Info;
        }

        private void RefreshTargetIndexFromSequence()
        {
            var seq = AssetDatabase.LoadAssetAtPath<LevelSequence>("Assets/Levels/LevelSequence.asset");
            if (seq != null && seq.Levels != null)
            {
                m_TargetLevelIndex = seq.Levels.Count + 1;
            }
        }

        private void OnGUI()
        {
            InitStyles();

            m_MainScroll = EditorGUILayout.BeginScrollView(m_MainScroll);

            // 1. Üst Başlık & Benchmark İstatistik Çubuğu
            DrawHeaderBanner();

            EditorGUILayout.Space(6);

            // 2. İki Ana Sütun: Sol (Görsel & Havuz/Slot/Kapasite Ayarları), Sağ (Vagon Kuyruğu)
            EditorGUILayout.BeginHorizontal();

            // Sol Panel: 410 piksel genişlik
            EditorGUILayout.BeginVertical(GUILayout.Width(415));
            DrawSourceAndSettingsPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Sağ Panel: Esnek genişlik (Vagon Kuyruğu & İskele Simülasyonu)
            EditorGUILayout.BeginVertical();
            DrawSequenceInspectorPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 3. Durum Bildirimi & Kaydetme Çubuğu
            DrawBottomBar();

            EditorGUILayout.EndScrollView();
        }

        private void InitStyles()
        {
            if (m_HeaderStyle == null)
            {
                m_HeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    normal = { textColor = new Color(0.9f, 0.95f, 1f) }
                };
            }

            if (m_CardStyle == null)
            {
                m_CardStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(5, 5, 5, 5),
                    margin = new RectOffset(2, 2, 2, 2)
                };
            }

            if (m_PoolCardStyle == null)
            {
                m_PoolCardStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(5, 5, 5, 5),
                    margin = new RectOffset(2, 2, 2, 2)
                };
            }

            if (m_DropZoneStyle == null)
            {
                m_DropZoneStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.85f, 0.9f, 1f) }
                };
            }
        }

        #region 1. Üst Başlık & Benchmark Banner

        private void DrawHeaderBanner()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("🚀 Akıllı Seviye & Sıra Üretim Stüdyosu", m_HeaderStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("🔄 Sırayı Güncelle", EditorStyles.miniButton, GUILayout.Width(120)))
            {
                RefreshTargetIndexFromSequence();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            GUI.color = new Color(0.4f, 0.9f, 0.6f);
            GUILayout.Label("📊 162 Seviye Benchmark Referansı:", EditorStyles.boldLabel, GUILayout.Width(220));
            GUI.color = Color.white;
            GUILayout.Label("Ort. 659 Küp • Zor Seviyelerde 40-55 Gemi • %71.5 Yuvarlak Kapasite • %92 Halat • %94 Gizli • %90 Gizli Küpler (?)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 2. Sol Panel: Görsel & Havuz/Slot/Kapasite Ayarları

        private void DrawSourceAndSettingsPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("🖼️ 1. Kaynak Görsel (Piksel Sanatı)", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            // Sürükle - Bırak Alanı
            DrawDropZone();

            EditorGUILayout.Space(4);

            if (m_SourceTexture != null)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Boyut: <b>{m_SourceTexture.width}x{m_SourceTexture.height}</b>", GetRichMiniLabel());
                GUILayout.FlexibleSpace();
                int unique = PixelPaletteOptimizer.CountUniqueColors(m_SourceTexture);
                GUILayout.Label($"Algılanan Renk: <b>{unique}</b>", GetRichMiniLabel());
                EditorGUILayout.EndHorizontal();

                if (m_TotalCubes > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"Kırılacak Toplam Küp: <b>{m_TotalCubes}</b>", GetRichMiniLabel());
                    GUILayout.FlexibleSpace();
                    if (m_WorkingMysteryCubes.Count > 0)
                    {
                        GUI.color = new Color(0.8f, 0.7f, 1f);
                        GUILayout.Label($"Gizli Küpler: <b>{m_WorkingMysteryCubes.Count} adet (?)</b>", GetRichMiniLabel());
                        GUI.color = Color.white;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(6);

            // -------------------------------------------------------------
            // KULLANICININ İSTEDİĞİ: 📐 Havuz & Slot Ayarları
            // -------------------------------------------------------------
            GUILayout.Label("📐 2. Havuz & Slot Ayarları", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);

            int newRows = DrawStepperRow("Sıra (dalga)", "Havuzda kaç sıra gemi bekler.", m_PoolRows, 1, 6);
            if (newRows != m_PoolRows) m_PoolRows = newRows;

            int newCols = DrawStepperRow("Kolon", "Havuzda yan yana kaç sütun gemi olur.", m_PoolColumns, 1, 8);
            if (newCols != m_PoolColumns) m_PoolColumns = newCols;

            int newSlots = DrawStepperRow("⚓ Yanaşma Slotu", "Aynı anda yanaşabilen açık iskele gemi yuvası sayısı.", m_SlotCount, 2, 8);
            if (newSlots != m_SlotCount) m_SlotCount = newSlots;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Havuz Kapasitesi:", EditorStyles.miniLabel, GUILayout.Width(125));
            GUI.color = new Color(0.3f, 0.85f, 1f);
            GUILayout.Label($"<b>{m_PoolRows * m_PoolColumns} Gemi</b> (Aynı anda görünen)", GetRichMiniLabel());
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // -------------------------------------------------------------
            // 🔢 Gemi Kapasiteleri (Min / Peak / Max)
            // -------------------------------------------------------------
            GUILayout.Label("🔢 3. Gemi Kapasiteleri (Sayı Dağılımı)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);

            m_MinCapacity = DrawStepperRow("Min Kapasite", "Gemi başına en az küp sayısı.", m_MinCapacity, 4, m_PeakCapacity);
            m_PeakCapacity = DrawStepperRow("En Sık (Peak)", "Kapasitelerin yoğunlaştığı tepe değer.", m_PeakCapacity, m_MinCapacity, m_MaxCapacity);
            m_MaxCapacity = DrawStepperRow("Maks Kapasite", "Gemi başına en fazla küp sayısı.", m_MaxCapacity, m_PeakCapacity, 60);

            m_PreferRoundCapacities = EditorGUILayout.Toggle(new GUIContent("10'un Katları (10, 20, 30...):", "Sayılar ağırlıklı olarak 10, 20, 30... olur, kalan küpler tek gemide toplanır."), m_PreferRoundCapacities);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // -------------------------------------------------------------
            // ⏱️ Bulmaca Süresi & Pacing Seçici
            // -------------------------------------------------------------
            GUILayout.Label("⏱️ 4. Bulmaca Süresi & Derinlik Modu", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = (m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.FastCasual) ? new Color(0.4f, 0.85f, 0.5f) : Color.white;
            if (GUILayout.Button("⚡ Hızlı\n(~1.5 Dk)", GUILayout.Height(34)))
            {
                m_DurationPacing = LevelBenchmarkEngine.PuzzleDurationPacing.FastCasual;
                m_Preset = LevelBenchmarkEngine.DifficultyPreset.Kolay;
                m_MaxCapacity = 40;
                m_MinCapacity = 10;
                m_PeakCapacity = 20;
                m_GenerateMysteryCubes = false;
                TriggerSmartGeneration();
            }

            GUI.backgroundColor = (m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.StandardPuzzle) ? new Color(0.95f, 0.85f, 0.3f) : Color.white;
            if (GUILayout.Button("🧩 Standart\n(~3-4 Dk)", GUILayout.Height(34)))
            {
                m_DurationPacing = LevelBenchmarkEngine.PuzzleDurationPacing.StandardPuzzle;
                m_Preset = LevelBenchmarkEngine.DifficultyPreset.Orta;
                m_MaxCapacity = 30;
                m_MinCapacity = 10;
                m_PeakCapacity = 20;
                m_GenerateMysteryCubes = true;
                m_MysteryCubePercent = 25;
                TriggerSmartGeneration();
            }

            GUI.backgroundColor = (m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.TacticalDeep) ? new Color(1f, 0.5f, 0.4f) : Color.white;
            if (GUILayout.Button("🧠 Zorlu & Uzun\n(~5-8+ Dk)", GUILayout.Height(34)))
            {
                m_DurationPacing = LevelBenchmarkEngine.PuzzleDurationPacing.TacticalDeep;
                m_Preset = LevelBenchmarkEngine.DifficultyPreset.Zor;
                m_MaxCapacity = 30;
                m_MinCapacity = 10;
                m_PeakCapacity = 20;
                m_GenerateMysteryCubes = true;
                m_MysteryCubePercent = 35;
                TriggerSmartGeneration();
            }

            GUI.backgroundColor = (m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.Custom) ? new Color(0.5f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button("🎛️ Özel\n(Slider)", GUILayout.Height(34)))
            {
                m_DurationPacing = LevelBenchmarkEngine.PuzzleDurationPacing.Custom;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.Custom || m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.TacticalDeep)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Hedef Gemi Sayısı:", GUILayout.Width(125));
                m_TargetShipCount = EditorGUILayout.IntSlider(m_TargetShipCount, 18, 70);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6);

            // -------------------------------------------------------------
            // ❓ Gizli Küpler (?)
            // -------------------------------------------------------------
            GUILayout.Label("❓ 5. Gizli Küpler (Mystery Cubes - '?')", EditorStyles.boldLabel);
            m_GenerateMysteryCubes = EditorGUILayout.Toggle("Gizli Küpler (?) Ekle:", m_GenerateMysteryCubes);
            if (m_GenerateMysteryCubes)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("İç Küp Gizleme Oranı:", GUILayout.Width(135));
                m_MysteryCubePercent = EditorGUILayout.IntSlider(m_MysteryCubePercent, 10, 60);
                EditorGUILayout.EndHorizontal();

                m_MysteryRevealCondition = (MysteryRevealCondition)EditorGUILayout.EnumPopup("Açılma Kuralı:", m_MysteryRevealCondition);
            }

            EditorGUILayout.Space(6);

            // -------------------------------------------------------------
            // ⚙️ Bölüm Bilgileri & Mekanikler
            // -------------------------------------------------------------
            GUILayout.Label("⚙️ 6. Seviye Adı & Mekanikler", EditorStyles.boldLabel);
            m_LevelName = EditorGUILayout.TextField("Bölüm Adı:", m_LevelName);
            m_TargetLevelIndex = EditorGUILayout.IntField("Bölüm No (Index):", m_TargetLevelIndex);

            EditorGUILayout.Space(2);
            m_GenerateLinks = EditorGUILayout.Toggle("🔗 Halatlı Gemiler (Linked):", m_GenerateLinks);
            m_GenerateHidden = EditorGUILayout.Toggle("👁️ Gizli Gemiler (Hidden):", m_GenerateHidden);
            m_AllowDualColor = EditorGUILayout.Toggle("🌓 Çift Renk Konteyner:", m_AllowDualColor);
            m_GuaranteeSolvability = EditorGUILayout.Toggle("🛡️ %100 Çözüm Garantisi:", m_GuaranteeSolvability);

            EditorGUILayout.Space(8);

            GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
            if (GUILayout.Button("⚡ Akıllı Sıra & Mekanik Üret", GUILayout.Height(36)))
            {
                TriggerSmartGeneration();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        private int DrawStepperRow(string label, string tooltip, int value, int min, int max)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(label, tooltip), GUILayout.Width(125), GUILayout.Height(20));
            if (GUILayout.Button("−", EditorStyles.miniButtonLeft, GUILayout.Width(28), GUILayout.Height(20))) value--;
            value = EditorGUILayout.IntField(value, GUILayout.Width(44), GUILayout.Height(20));
            if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(28), GUILayout.Height(20))) value++;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            return Mathf.Clamp(value, min, Mathf.Max(min, max));
        }

        private void DrawDropZone()
        {
            Rect dropRect = GUILayoutUtility.GetRect(0f, 90f, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, m_SourceTexture != null
                ? $"Seçili: {m_SourceTexture.name}\n(Farklı bir görsel sürükleyerek değiştirebilirsiniz)"
                : "📥 Buraya Piksel Resmi Sürükleyin\n(Texture2D veya PNG)\nveya Gözat'a tıklayın", m_DropZoneStyle);

            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropRect.Contains(evt.mousePosition)) break;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject is Texture2D tex)
                            {
                                SetSourceTexture(tex);
                                break;
                            }
                        }
                    }
                    Event.current.Use();
                    break;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📁 Projeden Seç...", EditorStyles.miniButton))
            {
                string path = EditorUtility.OpenFilePanel("Piksel Resmi Seç", "Assets", "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(path))
                {
                    string relPath = "Assets" + path.Substring(Application.dataPath.Length);
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(relPath);
                    if (tex != null) SetSourceTexture(tex);
                }
            }

            if (m_SourceTexture != null)
            {
                if (GUILayout.Button("Temizle", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    m_SourceTexture = null;
                    m_WorkingSequence.Clear();
                    m_WorkingPalette.Clear();
                    m_WorkingMysteryCubes.Clear();
                    m_TotalCubes = 0;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void SetSourceTexture(Texture2D tex)
        {
            m_SourceTexture = tex;
            string clean = tex.name.Replace("_", " ").Replace("-", " ");
            if (clean.Length > 0)
            {
                clean = System.Text.RegularExpressions.Regex.Replace(clean, @"^\d+\s*", "");
                m_LevelName = char.ToUpper(clean[0]) + clean.Substring(1);
            }

            TriggerSmartGeneration();
        }

        private void TriggerSmartGeneration()
        {
            if (m_SourceTexture == null)
            {
                m_StatusMessage = "Lütfen önce bir piksel görseli sürükleyin!";
                m_StatusType = MessageType.Warning;
                return;
            }

            Texture2D activeTex = m_SourceTexture;

            if (m_TargetColorCount > 0)
            {
                Texture2D quantized = PixelPaletteOptimizer.Quantize(m_SourceTexture, m_TargetColorCount, 0.2f, m_DenoiseKMeans);
                if (quantized != null)
                {
                    Texture2D saved = PixelPaletteOptimizer.SaveAsOptimizedAsset(m_SourceTexture, quantized, $"_{m_TargetColorCount}c");
                    DestroyImmediate(quantized);
                    if (saved != null) activeTex = saved;
                }
            }

            var config = new LevelBenchmarkEngine.GenerationConfig
            {
                preset = m_Preset,
                durationPacing = m_DurationPacing,
                targetShipCount = (m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.Custom || m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.TacticalDeep) ? m_TargetShipCount : 0,
                targetLevelIndex = m_TargetLevelIndex,
                levelName = m_LevelName,
                poolRows = m_PoolRows,
                poolColumns = m_PoolColumns,
                slotCount = m_SlotCount,
                minCapacity = m_MinCapacity,
                peakCapacity = m_PeakCapacity,
                maxCapacity = m_MaxCapacity,
                generateLinks = m_GenerateLinks,
                generateHidden = m_GenerateHidden,
                generateMysteryCubes = m_GenerateMysteryCubes,
                mysteryCubePercent = m_MysteryCubePercent,
                mysteryRevealCondition = m_MysteryRevealCondition,
                preferRoundCapacities = m_PreferRoundCapacities,
                roundRatio = m_RoundRatio,
                allowDualColor = m_AllowDualColor,
                guaranteeSolvability = m_GuaranteeSolvability,
                solvabilityTrials = m_SolvabilityTrials
            };

            m_CurrentResult = LevelBenchmarkEngine.GenerateFromTexture(activeTex, config);

            if (m_CurrentResult.success)
            {
                m_WorkingSequence = m_CurrentResult.sequence;
                m_WorkingPalette = m_CurrentResult.palette;
                m_WorkingMysteryCubes = m_CurrentResult.mysteryCubes;
                m_TotalCubes = m_CurrentResult.totalCubes;

                m_StatusMessage = m_CurrentResult.benchmarkSummary;
                m_StatusType = m_CurrentResult.is100PercentSolvable ? MessageType.Info : MessageType.Warning;
            }
            else
            {
                m_StatusMessage = m_CurrentResult.errorMessage;
                m_StatusType = MessageType.Error;
            }

            Repaint();
        }

        #endregion

        #region 3. Sağ Panel: Vagon Sıra İnceleyici & İnteraktif Editör

        private void DrawSequenceInspectorPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("📋 Vagon & Gemi Kuyruğu (İnteraktif Düzenleyici)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = m_ViewCards ? new Color(0.3f, 0.85f, 0.5f) : Color.white;
            if (GUILayout.Button("🗂️ Kartlar", EditorStyles.miniButtonLeft, GUILayout.Width(65))) m_ViewCards = true;
            GUI.backgroundColor = !m_ViewCards ? new Color(0.3f, 0.85f, 0.5f) : Color.white;
            if (GUILayout.Button("📑 Tablo", EditorStyles.miniButtonRight, GUILayout.Width(65))) m_ViewCards = false;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            if (m_WorkingSequence == null || m_WorkingSequence.Count == 0)
            {
                EditorGUILayout.HelpBox("Henüz vagon sırası oluşturulmadı. Soldaki alana bir görsel sürükleyin veya 'Akıllı Sıra Üret' butonuna basın.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            // Metrik Rozetleri
            DrawSequenceBadges();

            EditorGUILayout.Space(4);

            // Hızlı Eylem Butonları
            DrawQuickActionsBar();

            EditorGUILayout.Space(4);

            // Vagon Listesi
            m_QueueScroll = EditorGUILayout.BeginScrollView(m_QueueScroll, GUILayout.MinHeight(380));
            if (m_ViewCards)
            {
                DrawWagonCardsGrid();
            }
            else
            {
                DrawWagonTableView();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawSequenceBadges()
        {
            int totalAssigned = m_WorkingSequence.Sum(w => w.capacity + (w.isDualColor ? (w.secondaryCapacity > 0 ? w.secondaryCapacity : w.capacity) : 0));
            int linkedCount = m_WorkingSequence.Count(w => w.linkId > 0);
            int hiddenCount = m_WorkingSequence.Count(w => w.isHidden);

            EditorGUILayout.BeginHorizontal(GUI.skin.box);

            // Tahmini Süre
            GUI.color = new Color(0.95f, 0.85f, 0.35f);
            string timeStr = m_CurrentResult != null && !string.IsNullOrEmpty(m_CurrentResult.estimatedPlaytime)
                ? m_CurrentResult.estimatedPlaytime
                : "~3-5 Dk";
            GUILayout.Label($"⏱️ <b>{timeStr}</b>", GetRichMiniLabel(), GUILayout.Width(155));
            GUI.color = Color.white;

            // Toplam Gemi Sayısı
            GUILayout.Label($"🚢 <b>{m_WorkingSequence.Count}</b> Gemi", GetRichMiniLabel(), GUILayout.Width(75));

            // İskele Slotu
            GUI.color = new Color(0.4f, 0.85f, 1f);
            GUILayout.Label($"⚓ <b>{m_SlotCount}</b> İskele Slot", GetRichMiniLabel(), GUILayout.Width(100));
            GUI.color = Color.white;

            // Kapasite & Küp Dengesi
            bool balanced = (totalAssigned == m_TotalCubes);
            GUI.color = balanced ? new Color(0.3f, 0.9f, 0.5f) : new Color(1f, 0.4f, 0.4f);
            GUILayout.Label($"📦 Kapasite: <b>{totalAssigned} / {m_TotalCubes}</b> {(balanced ? "✓" : "⚠️")}", GetRichMiniLabel(), GUILayout.Width(155));
            GUI.color = Color.white;

            // Gizli Küpler (?)
            if (m_WorkingMysteryCubes.Count > 0)
            {
                GUI.color = new Color(0.7f, 0.6f, 1f);
                GUILayout.Label($"❓ <b>{m_WorkingMysteryCubes.Count}</b> Gizli Küp", GetRichMiniLabel(), GUILayout.Width(105));
                GUI.color = Color.white;
            }

            // Halatlı Gemi Sayısı
            GUILayout.Label($"🔗 <b>{linkedCount / 2}</b> Çift Halat", GetRichMiniLabel(), GUILayout.Width(90));

            // Gizli Gemi Sayısı
            GUILayout.Label($"👁️ <b>{hiddenCount}</b> Gizli", GetRichMiniLabel(), GUILayout.Width(70));

            // Çift Renk Konteyner Sayısı
            int dualCount = m_WorkingSequence.Count(w => w.isDualColor);
            if (dualCount > 0)
            {
                GUI.color = new Color(0.95f, 0.6f, 1f);
                GUILayout.Label($"🌓 <b>{dualCount}</b> Güçlü", GetRichMiniLabel(), GUILayout.Width(75));
                GUI.color = Color.white;
            }

            GUILayout.FlexibleSpace();

            // Çözülebilirlik Rozeti
            bool solvable = (m_CurrentResult != null && m_CurrentResult.is100PercentSolvable);
            GUI.color = solvable ? new Color(0.2f, 0.9f, 0.4f) : new Color(1f, 0.7f, 0.2f);
            GUILayout.Label(solvable ? "✓ %100 Çözülebilir" : "⚠️ Kilitlenme İhtimali", EditorStyles.boldLabel);
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawQuickActionsBar()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("🎲 Güvenli Karıştır (Shuffle)", EditorStyles.miniButton, GUILayout.Width(160)))
            {
                SafeShuffleWorkingSequence();
            }

            if (GUILayout.Button("🔗 Halatları Yenile", EditorStyles.miniButton, GUILayout.Width(105)))
            {
                ReassignLinks();
            }

            if (GUILayout.Button("👁️ Gizli Gemileri Yenile", EditorStyles.miniButton, GUILayout.Width(130)))
            {
                ReassignHidden();
            }

            if (GUILayout.Button("⚖️ Kapasiteyi Eşitle", EditorStyles.miniButton, GUILayout.Width(120)))
            {
                AutoBalanceCapacities();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("➕ Gemi Ekle", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                AddNewWagon();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawWagonCardsGrid()
        {
            int columns = Mathf.Clamp(m_PoolColumns, 1, 8);
            int total = m_WorkingSequence.Count;
            int initialPoolSize = Mathf.Min(total, m_PoolRows * columns);

            // Başlangıç Havuzu Başlığı
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"🌊 Başlangıç Havuzu ({initialPoolSize} Gemi - {m_PoolRows} Sıra x {columns} Kolon)", EditorStyles.miniBoldLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);

            for (int i = 0; i < total; i += columns)
            {
                if (i == initialPoolSize && initialPoolSize < total)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                    GUILayout.Label($"📦 Sonraki Dalgalar Kuyruğu ({total - initialPoolSize} Gemi)", EditorStyles.miniBoldLabel);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(2);
                }

                EditorGUILayout.BeginHorizontal();
                for (int col = 0; col < columns; col++)
                {
                    int index = i + col;
                    if (index < total)
                    {
                        DrawSingleWagonCard(index, index < initialPoolSize);
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }
        }

        private void DrawSingleWagonCard(int index, bool isInInitialPool)
        {
            var wagon = m_WorkingSequence[index];
            if (wagon == null) return;

            GUIStyle cardStyle = isInInitialPool ? m_PoolCardStyle : m_CardStyle;
            if (isInInitialPool) GUI.backgroundColor = new Color(0.9f, 0.95f, 1f);

            EditorGUILayout.BeginVertical(cardStyle, GUILayout.Width(96));
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();
            string labelPrefix = isInInitialPool ? "🌊" : "";
            GUILayout.Label($"<b>{labelPrefix}#{index + 1}</b>", GetRichMiniLabel(), GUILayout.Width(35));

            GUI.enabled = (index > 0);
            if (GUILayout.Button("◀", EditorStyles.miniButton, GUILayout.Width(18)))
            {
                SwapWagons(index, index - 1);
            }

            GUI.enabled = (index < m_WorkingSequence.Count - 1);
            if (GUILayout.Button("▶", EditorStyles.miniButton, GUILayout.Width(18)))
            {
                SwapWagons(index, index + 1);
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // Renk Swatch Kutusu
            Rect swatchRect = GUILayoutUtility.GetRect(84f, 26f);
            if (wagon.isDualColor)
            {
                Rect leftHalf = new Rect(swatchRect.x, swatchRect.y, swatchRect.width * 0.5f, swatchRect.height);
                Rect rightHalf = new Rect(swatchRect.x + swatchRect.width * 0.5f, swatchRect.y, swatchRect.width * 0.5f, swatchRect.height);
                EditorGUI.DrawRect(leftHalf, wagon.wagonColor);
                EditorGUI.DrawRect(rightHalf, wagon.secondaryColor);

                // Orta ayrım çizgisi
                Rect divider = new Rect(swatchRect.x + swatchRect.width * 0.5f - 0.5f, swatchRect.y, 1f, swatchRect.height);
                EditorGUI.DrawRect(divider, new Color(0f, 0f, 0f, 0.5f));

                // 1. Kapasite
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("1:", EditorStyles.miniLabel, GUILayout.Width(12));
                if (GUILayout.Button("-", EditorStyles.miniButtonLeft, GUILayout.Width(16)))
                {
                    wagon.capacity = Mathf.Max(1, wagon.capacity - 1);
                }
                wagon.capacity = EditorGUILayout.IntField(wagon.capacity, GUILayout.Width(36));
                if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(16)))
                {
                    wagon.capacity++;
                }
                EditorGUILayout.EndHorizontal();

                // 2. Kapasite
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("2:", EditorStyles.miniLabel, GUILayout.Width(12));
                if (GUILayout.Button("-", EditorStyles.miniButtonLeft, GUILayout.Width(16)))
                {
                    wagon.secondaryCapacity = Mathf.Max(1, wagon.secondaryCapacity - 1);
                }
                wagon.secondaryCapacity = EditorGUILayout.IntField(wagon.secondaryCapacity, GUILayout.Width(36));
                if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(16)))
                {
                    wagon.secondaryCapacity++;
                }
                EditorGUILayout.EndHorizontal();

                // 2. Renk Değiştir Menüsü
                if (GUILayout.Button("🎨 2. Renk ▾", EditorStyles.miniButton))
                {
                    ShowSecondaryColorMenu(wagon);
                }

                // Çift Renk Butonu (Aktif)
                GUI.backgroundColor = new Color(0.3f, 0.9f, 0.5f);
                if (GUILayout.Button("🌓 Güçlü", EditorStyles.miniButton))
                {
                    ToggleDualColor(index);
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                EditorGUI.DrawRect(swatchRect, wagon.wagonColor);

                // Kapasite Düzenleme
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("-", EditorStyles.miniButtonLeft, GUILayout.Width(18)))
                {
                    wagon.capacity = Mathf.Max(1, wagon.capacity - 1);
                }
                wagon.capacity = EditorGUILayout.IntField(wagon.capacity, GUILayout.Width(44));
                if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(18)))
                {
                    wagon.capacity++;
                }
                EditorGUILayout.EndHorizontal();

                // Halat Durumu
                GUI.backgroundColor = wagon.linkId > 0 ? new Color(1f, 0.85f, 0.3f) : Color.white;
                string linkText = wagon.linkId > 0 ? $"🔗 Bağ #{wagon.linkId}" : "Halatsız";
                if (GUILayout.Button(linkText, EditorStyles.miniButton))
                {
                    ToggleWagonLink(index);
                }

                // Gizli Gemi Durumu
                GUI.backgroundColor = wagon.isHidden ? new Color(0.6f, 0.7f, 1f) : Color.white;
                string hiddenText = wagon.isHidden ? "👁️ Gizli" : "Açık";
                if (GUILayout.Button(hiddenText, EditorStyles.miniButton))
                {
                    wagon.isHidden = !wagon.isHidden;
                }
                GUI.backgroundColor = Color.white;

                // Çift Renk Yap Butonu
                if (GUILayout.Button("🌓 Çift Yap", EditorStyles.miniButton))
                {
                    ToggleDualColor(index);
                }
            }

            // Silme Butonu
            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("✕ Sil", EditorStyles.miniButton))
            {
                m_WorkingSequence.RemoveAt(index);
                GUIUtility.ExitGUI();
            }
            GUI.color = Color.white;

            EditorGUILayout.EndVertical();
        }

        private void DrawWagonTableView()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("#", GUILayout.Width(35));
            GUILayout.Label("Renk", GUILayout.Width(65));
            GUILayout.Label("1. Kap", GUILayout.Width(55));
            GUILayout.Label("2. Kap", GUILayout.Width(55));
            GUILayout.Label("Halat (Link)", GUILayout.Width(90));
            GUILayout.Label("Gizli", GUILayout.Width(50));
            GUILayout.Label("Çift Renk", GUILayout.Width(75));
            GUILayout.Label("İşlemler", GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < m_WorkingSequence.Count; i++)
            {
                var w = m_WorkingSequence[i];
                if (w == null) continue;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"#{i + 1}", GUILayout.Width(35));

                Rect colRect = GUILayoutUtility.GetRect(55f, 16f, GUILayout.Width(55));
                if (w.isDualColor)
                {
                    Rect left = new Rect(colRect.x, colRect.y, colRect.width * 0.5f, colRect.height);
                    Rect right = new Rect(colRect.x + colRect.width * 0.5f, colRect.y, colRect.width * 0.5f, colRect.height);
                    EditorGUI.DrawRect(left, w.wagonColor);
                    EditorGUI.DrawRect(right, w.secondaryColor);
                }
                else
                {
                    EditorGUI.DrawRect(colRect, w.wagonColor);
                }

                w.capacity = EditorGUILayout.IntField(w.capacity, GUILayout.Width(50));
                if (w.isDualColor)
                {
                    w.secondaryCapacity = EditorGUILayout.IntField(w.secondaryCapacity, GUILayout.Width(50));
                }
                else
                {
                    GUILayout.Label("-", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(50));
                }

                w.linkId = EditorGUILayout.IntField(w.linkId, GUILayout.Width(80));
                w.isHidden = EditorGUILayout.Toggle(w.isHidden, GUILayout.Width(45));

                bool newDual = EditorGUILayout.Toggle(w.isDualColor, GUILayout.Width(65));
                if (newDual != w.isDualColor)
                {
                    ToggleDualColor(i);
                }

                if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(22)) && i > 0)
                {
                    SwapWagons(i, i - 1);
                }
                if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(22)) && i < m_WorkingSequence.Count - 1)
                {
                    SwapWagons(i, i + 1);
                }
                if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22)))
                {
                    m_WorkingSequence.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 4. Alt Çubuk: Durum Bildirimi & Kaydetme

        private void DrawBottomBar()
        {
            if (!string.IsNullOrEmpty(m_StatusMessage))
            {
                EditorGUILayout.HelpBox(m_StatusMessage, m_StatusType);
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();

            if (m_LastSavedLevel != null)
            {
                GUI.backgroundColor = new Color(0.3f, 0.9f, 0.5f);
                if (GUILayout.Button($"🔍 '{m_LastSavedLevel.LevelName}' Level Designer'da Göster", GUILayout.Height(38), GUILayout.Width(290)))
                {
                    Selection.activeObject = m_LastSavedLevel;
                    EditorGUIUtility.PingObject(m_LastSavedLevel);
                    PixelLevelDesignerWindow.OpenWindow();
                }
                GUI.backgroundColor = Color.white;
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = (m_WorkingSequence.Count > 0 && m_SourceTexture != null);
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);

            string saveLabel = $"📁 Bölümü Oluştur ve LevelSequence'e Ekle (Level {m_TargetLevelIndex})";
            if (GUILayout.Button(saveLabel, GUILayout.Height(38), GUILayout.Width(440)))
            {
                ExecuteSaveLevel();
            }

            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private void ExecuteSaveLevel()
        {
            LevelDifficultyTier tier = LevelDifficultyTier.Orta;
            if (m_Preset == LevelBenchmarkEngine.DifficultyPreset.Kolay) tier = LevelDifficultyTier.Kolay;
            else if (m_Preset == LevelBenchmarkEngine.DifficultyPreset.Zor) tier = LevelDifficultyTier.Zor;

            string savedPath;
            var createdLevel = LevelBenchmarkEngine.SaveAndRegisterNewLevel(
                m_SourceTexture,
                m_LevelName,
                m_TargetLevelIndex,
                tier,
                m_WorkingPalette,
                m_WorkingSequence,
                m_WorkingMysteryCubes,
                m_MysteryRevealCondition,
                m_PoolRows,
                m_PoolColumns,
                m_SlotCount,
                m_MinCapacity,
                m_PeakCapacity,
                m_MaxCapacity,
                out savedPath
            );

            if (createdLevel != null)
            {
                m_LastSavedLevel = createdLevel;
                m_StatusMessage = $"✓ Seviye başarıyla kaydedildi: '{savedPath}' ve LevelSequence.asset'e eklendi!";
                m_StatusType = MessageType.Info;

                EditorUtility.DisplayDialog("Bölüm Başarıyla Oluşturuldu!", 
                    $"Bölüm {m_TargetLevelIndex}: '{m_LevelName}' başarıyla oluşturuldu!\n\n" +
                    $"• Konum: {savedPath}\n" +
                    $"• Gemi Sayısı: {m_WorkingSequence.Count}\n" +
                    $"• Havuz: {m_PoolRows} Sıra x {m_PoolColumns} Kolon\n" +
                    $"• Yanaşma Slotu: {m_SlotCount}\n" +
                    $"• Gizli Küpler (?): {m_WorkingMysteryCubes.Count} adet\n" +
                    $"• LevelSequence listesine otomatik eklendi.", "Harika!");

                RefreshTargetIndexFromSequence();
            }
        }

        #endregion

        #region Yardımcı Metotlar

        private void SwapWagons(int a, int b)
        {
            if (a < 0 || a >= m_WorkingSequence.Count || b < 0 || b >= m_WorkingSequence.Count) return;
            var temp = m_WorkingSequence[a];
            m_WorkingSequence[a] = m_WorkingSequence[b];
            m_WorkingSequence[b] = temp;
        }

        private void ToggleWagonLink(int index)
        {
            var w = m_WorkingSequence[index];
            if (w.linkId > 0)
            {
                int oldId = w.linkId;
                w.linkId = 0;
                var partner = m_WorkingSequence.FirstOrDefault(x => x != null && x.linkId == oldId);
                if (partner != null) partner.linkId = 0;
            }
            else
            {
                int newId = 1;
                while (m_WorkingSequence.Any(x => x != null && x.linkId == newId)) newId++;

                w.linkId = newId;
                int partnerIdx = (index + 1 < m_WorkingSequence.Count) ? (index + 1) : (index - 1);
                if (partnerIdx >= 0 && partnerIdx < m_WorkingSequence.Count)
                {
                    m_WorkingSequence[partnerIdx].linkId = newId;
                }
            }
        }

        private void SafeShuffleWorkingSequence()
        {
            var rng = new System.Random();
            for (int i = m_WorkingSequence.Count - 1; i > 3; i--)
            {
                int j = 3 + rng.Next(i - 2);
                if (m_WorkingSequence[i].linkId == 0 && m_WorkingSequence[j].linkId == 0)
                {
                    var temp = m_WorkingSequence[i];
                    m_WorkingSequence[i] = m_WorkingSequence[j];
                    m_WorkingSequence[j] = temp;
                }
            }
        }

        private void ReassignLinks()
        {
            var rng = new System.Random();
            foreach (var w in m_WorkingSequence) w.linkId = 0;

            int targetPairs = Mathf.Clamp(m_WorkingSequence.Count / 5, 3, 7);
            int linkId = 1;
            for (int i = 2; i < m_WorkingSequence.Count - 1 && linkId <= targetPairs; i += 3)
            {
                m_WorkingSequence[i].linkId = linkId;
                m_WorkingSequence[i + 1].linkId = linkId;
                linkId++;
            }
        }

        private void ReassignHidden()
        {
            foreach (var w in m_WorkingSequence) w.isHidden = false;
            var rng = new System.Random();

            int targetHidden = Mathf.Clamp(m_WorkingSequence.Count / 6, 4, 8);
            int count = 0;
            for (int i = 5; i < m_WorkingSequence.Count && count < targetHidden; i++)
            {
                if (m_WorkingSequence[i].linkId == 0 && rng.NextDouble() > 0.35)
                {
                    m_WorkingSequence[i].isHidden = true;
                    count++;
                }
            }
        }

        private void ToggleDualColor(int index)
        {
            if (index < 0 || index >= m_WorkingSequence.Count) return;
            var w = m_WorkingSequence[index];
            if (w == null) return;

            if (w.isDualColor)
            {
                w.isDualColor = false;
                w.secondaryCapacity = 0;
                w.label = "Vagon";
                AutoBalanceCapacities();
            }
            else
            {
                if (m_WorkingPalette == null || m_WorkingPalette.Count < 2) return;
                // Paletten bu vagondan farklı ve en yüksek küpe sahip rengi bul
                var partner = m_WorkingPalette.Where(p => p != null && p.pixelCount > 0 && !PaletteColorOverride.ColorsMatch(p.targetColor, w.wagonColor))
                                              .OrderByDescending(p => p.pixelCount)
                                              .FirstOrDefault();
                if (partner == null) return;

                w.linkId = 0;
                w.isHidden = false;
                w.isDualColor = true;
                w.secondaryColor = partner.targetColor;
                w.secondaryCapacity = Mathf.Clamp(w.capacity, 10, 40);
                w.label = "Güçlü Konteyner";
                AutoBalanceCapacities();
            }
        }

        private void ShowSecondaryColorMenu(WagonSequenceEntry w)
        {
            if (w == null || m_WorkingPalette == null) return;
            GenericMenu menu = new GenericMenu();
            for (int p = 0; p < m_WorkingPalette.Count; p++)
            {
                var entry = m_WorkingPalette[p];
                if (entry == null || entry.pixelCount <= 0) continue;
                if (PaletteColorOverride.ColorsMatch(entry.targetColor, w.wagonColor)) continue;

                string name = string.IsNullOrEmpty(entry.label) ? $"Renk #{p + 1} ({entry.pixelCount} Küp)" : $"{entry.label} ({entry.pixelCount} Küp)";
                Color col = entry.targetColor;
                menu.AddItem(new GUIContent(name), PaletteColorOverride.ColorsMatch(col, w.secondaryColor), () =>
                {
                    w.secondaryColor = col;
                    AutoBalanceCapacities();
                });
            }
            menu.ShowAsContext();
        }

        private void AutoBalanceCapacities()
        {
            if (m_WorkingPalette == null || m_WorkingPalette.Count == 0 || m_WorkingSequence.Count == 0) return;

            for (int p = 0; p < m_WorkingPalette.Count; p++)
            {
                var entry = m_WorkingPalette[p];
                if (entry == null || entry.pixelCount <= 0) continue;

                int targetCubes = entry.pixelCount;

                // 1. Bu renge atanmış mevcut kapasiteyi topla (birincil + ikincil)
                int curCap = 0;
                foreach (var w in m_WorkingSequence)
                {
                    if (w == null) continue;
                    if (PaletteColorOverride.ColorsMatch(w.wagonColor, entry.targetColor) || w.paletteIndex == p)
                    {
                        curCap += w.capacity;
                    }
                    if (w.isDualColor && PaletteColorOverride.ColorsMatch(w.secondaryColor, entry.targetColor))
                    {
                        curCap += (w.secondaryCapacity > 0 ? w.secondaryCapacity : w.capacity);
                    }
                }

                int diff = targetCubes - curCap;
                if (diff == 0) continue;

                // 2. Farkı kapatacak vagon bul:
                // Önce bu rengin son tek-renkli vagonuna uygula
                var singleWagon = m_WorkingSequence.LastOrDefault(w => w != null && !w.isDualColor && (PaletteColorOverride.ColorsMatch(w.wagonColor, entry.targetColor) || w.paletteIndex == p));
                if (singleWagon != null)
                {
                    singleWagon.capacity = Mathf.Max(1, singleWagon.capacity + diff);
                }
                else
                {
                    // Tek renkli yoksa, bu rengin çift-renkli tarafına uygula
                    var dualWagon = m_WorkingSequence.LastOrDefault(w => w != null && w.isDualColor && (PaletteColorOverride.ColorsMatch(w.wagonColor, entry.targetColor) || PaletteColorOverride.ColorsMatch(w.secondaryColor, entry.targetColor)));
                    if (dualWagon != null)
                    {
                        if (PaletteColorOverride.ColorsMatch(dualWagon.secondaryColor, entry.targetColor))
                        {
                            dualWagon.secondaryCapacity = Mathf.Max(1, dualWagon.secondaryCapacity + diff);
                        }
                        else
                        {
                            dualWagon.capacity = Mathf.Max(1, dualWagon.capacity + diff);
                        }
                    }
                }
            }
        }

        private void AddNewWagon()
        {
            if (m_WorkingPalette.Count == 0) return;
            var p = m_WorkingPalette[0];
            m_WorkingSequence.Add(new WagonSequenceEntry(p.targetColor, 20, 0, "Yeni Vagon"));
        }

        private static GUIStyle s_RichMiniLabel;
        private static GUIStyle GetRichMiniLabel()
        {
            if (s_RichMiniLabel == null)
            {
                s_RichMiniLabel = new GUIStyle(EditorStyles.miniLabel)
                {
                    richText = true
                };
            }
            return s_RichMiniLabel;
        }

        #endregion
    }
}
