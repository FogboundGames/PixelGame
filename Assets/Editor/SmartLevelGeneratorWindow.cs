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

        // 2. Havuz & Slot Ayarları (Kullanıcının İstediği Birebir Kontroller)
        private int m_PoolRows = 3;       // Sıra (dalga) [−] [3] [+] — ekranda her şeridin ilk 3 gemisi görünür
        private int m_PoolColumns = 4;    // Kolon        [−] [4] [+] — rehber: çoğunlukla 4, Kolay'da 3
        private int m_SlotCount = 5;      // ⚓ Yanaşma Slotu [−] [5] [+]

        // 3. Gemi Kapasiteleri (Sayı Dağılımı)
        private int m_MinCapacity = 10;   // Min Kapasite
        private int m_PeakCapacity = 20;  // En Sık (Peak) Kapasite
        private int m_MaxCapacity = 50;   // Maks Kapasite (rehber: Kolay 30 / büyükte 40, Orta 40, Zor 50)
        private bool m_PreferRoundCapacities = true;
        private float m_RoundRatio = 0.70f;

        // 4. Mekanik Kuralları
        private bool m_GenerateLinks = true;
        private bool m_GenerateHidden = true;
        private bool m_GenerateMysteryCubes = true;
        private int m_MysteryCubePercent = 5;
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
        private Texture2D m_GeneratedTexture;   // kuyruğun üretildiği doku (renk azaltma açıksa optimize edilmiş kopya); kayıt da bununla yapılır
        private HashSet<int> m_OuterColors = new HashSet<int>();   // dış katman renkleri (açılış kuralı için)
        private int m_SourceCubes;     // kaynak resimdeki opak piksel sayısı (sütun / maks kapasite varsayılanı için)
        private int m_SequenceCount;   // LevelSequence'teki level sayısı

        // Son kural + simülasyon kontrolü (kuyruk elle değişince geçersiz olur)
        private bool m_CheckValid;
        private List<string> m_CheckErrors = new List<string>();
        private LevelQueueSimulator.Result m_CheckSim;
        private List<string> m_CheckWarnings = new List<string>();   // zorluk bandı / tap testi / PONR (kayda engel değil)

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
                m_SequenceCount = seq.Levels.Count(l => l != null);
                m_TargetLevelIndex = m_SequenceCount + 1;
            }
        }

        private int SequenceCount() => m_SequenceCount;
        private int CountSourceCubes() => m_SourceCubes;

        private static int TierToolbarIndex(LevelBenchmarkEngine.DifficultyPreset preset)
        {
            if (preset == LevelBenchmarkEngine.DifficultyPreset.Kolay) return 0;
            if (preset == LevelBenchmarkEngine.DifficultyPreset.Zor) return 2;
            return 1;
        }

        private static LevelBenchmarkEngine.PuzzleDurationPacing PacingFor(LevelBenchmarkEngine.DifficultyPreset preset)
        {
            if (preset == LevelBenchmarkEngine.DifficultyPreset.Kolay) return LevelBenchmarkEngine.PuzzleDurationPacing.FastCasual;
            if (preset == LevelBenchmarkEngine.DifficultyPreset.Zor) return LevelBenchmarkEngine.PuzzleDurationPacing.TacticalDeep;
            return LevelBenchmarkEngine.PuzzleDurationPacing.StandardPuzzle;
        }

        /// <summary>Zorluğa göre rehber varsayılanları: sütun (Kolay 3, küçük resimde 3, yoksa 4), maks kapasite, gizli küp.</summary>
        private void ApplyTierDefaults(LevelBenchmarkEngine.DifficultyPreset preset)
        {
            m_Preset = preset;
            var tier = CurrentTier();
            m_PoolColumns = LevelBenchmarkEngine.RecommendedPoolColumns(tier, m_SourceCubes);
            m_MaxCapacity = LevelBenchmarkEngine.TierMaxCapacity(tier, m_SourceCubes);
            m_PeakCapacity = Mathf.Min(m_PeakCapacity, m_MaxCapacity);
            m_GenerateMysteryCubes = tier != LevelDifficultyTier.Kolay;
            m_MysteryCubePercent = tier == LevelDifficultyTier.Zor ? 5 : 4;
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
            GUILayout.Label("Ort. 659 Küp • Ort. 23 Gemi (en fazla 36) • %71.5 Yuvarlak Kapasite • %92 Halat • %94 Gizli • %59 Gizli Küpler (?)", EditorStyles.miniLabel);
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

            m_SlotCount = PixelLevelData.FixedSlotCount;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("⚓ Yanaşma Slotu", "Tasarım kuralı: Her seviyede 5 slot sabittir."), GUILayout.Width(125), GUILayout.Height(20));
            GUI.color = new Color(0.3f, 0.85f, 1f);
            GUILayout.Label("<b>5 Slot (Sabit)</b>", GetRichMiniLabel(), GUILayout.Height(20));
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

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

            bool custom = m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.Custom;
            int tierMax = custom ? 60 : LevelBenchmarkEngine.TierMaxCapacity(CurrentTier(), m_TotalCubes > 0 ? m_TotalCubes : CountSourceCubes());
            m_MaxCapacity = DrawStepperRow("Maks Kapasite", "Gemi başına en fazla küp sayısı (rehber: Kolay 30 / büyükte 40, Orta 40, Zor 50).", m_MaxCapacity, 20, tierMax);
            if (custom)
            {
                m_MinCapacity = DrawStepperRow("Min Kapasite", "Gemi başına en az küp sayısı.", m_MinCapacity, 4, m_PeakCapacity);
                m_PeakCapacity = DrawStepperRow("En Sık (Peak)", "Kapasitelerin yoğunlaştığı tepe değer.", m_PeakCapacity, m_MinCapacity, m_MaxCapacity);
                m_PreferRoundCapacities = EditorGUILayout.Toggle(new GUIContent("10'un Katları (10, 20, 30...):", "Sayılar ağırlıklı olarak 10, 20, 30... olur, kalan küpler tek gemide toplanır."), m_PreferRoundCapacities);
            }
            else
            {
                EditorGUILayout.LabelField("Rehber bölmesi: renk başına en az gemi, renk başına en fazla 1 küsuratlı gemi (örn. max 30: 66 → 30, 20, 16).", EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // -------------------------------------------------------------
            // ⏱️ Bulmaca Süresi & Pacing Seçici
            // -------------------------------------------------------------
            GUILayout.Label("🎯 4. Zorluk & Bulmaca Süresi", EditorStyles.boldLabel);

            // Kaydedilecek zorluk her zaman görünür; eskiden yalnızca en son basılan süre butonundan çıkıyordu
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Zorluk:", "Level'a kaydedilecek zorluk. Sütun, maks kapasite, halat/gizli sayısı ve gizli küp buna göre ayarlanır."), GUILayout.Width(60), GUILayout.Height(24));
            int curTier = TierToolbarIndex(m_Preset);
            int newTier = GUILayout.Toolbar(curTier, new[] { "Kolay", "Orta", "Zor" }, GUILayout.Height(24));
            EditorGUILayout.EndHorizontal();
            if (newTier != curTier)
            {
                var preset = newTier == 0 ? LevelBenchmarkEngine.DifficultyPreset.Kolay
                    : newTier == 1 ? LevelBenchmarkEngine.DifficultyPreset.Orta
                    : LevelBenchmarkEngine.DifficultyPreset.Zor;
                if (m_DurationPacing != LevelBenchmarkEngine.PuzzleDurationPacing.Custom) m_DurationPacing = PacingFor(preset);
                ApplyTierDefaults(preset);
                if (m_SourceTexture != null) TriggerSmartGeneration();
            }

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = (m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.FastCasual) ? new Color(0.4f, 0.85f, 0.5f) : Color.white;
            if (GUILayout.Button("⚡ Hızlı\n(Kolay)", GUILayout.Height(34)))
            {
                m_DurationPacing = LevelBenchmarkEngine.PuzzleDurationPacing.FastCasual;
                ApplyTierDefaults(LevelBenchmarkEngine.DifficultyPreset.Kolay);
                TriggerSmartGeneration();
            }

            GUI.backgroundColor = (m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.StandardPuzzle) ? new Color(0.95f, 0.85f, 0.3f) : Color.white;
            if (GUILayout.Button("🧩 Standart\n(Orta)", GUILayout.Height(34)))
            {
                m_DurationPacing = LevelBenchmarkEngine.PuzzleDurationPacing.StandardPuzzle;
                ApplyTierDefaults(LevelBenchmarkEngine.DifficultyPreset.Orta);
                TriggerSmartGeneration();
            }

            GUI.backgroundColor = (m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.TacticalDeep) ? new Color(1f, 0.5f, 0.4f) : Color.white;
            if (GUILayout.Button("🧠 Zorlu\n(Zor)", GUILayout.Height(34)))
            {
                m_DurationPacing = LevelBenchmarkEngine.PuzzleDurationPacing.TacticalDeep;
                ApplyTierDefaults(LevelBenchmarkEngine.DifficultyPreset.Zor);
                TriggerSmartGeneration();
            }

            GUI.backgroundColor = (m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.Custom) ? new Color(0.5f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button("🎛️ Özel\n(Gemi Sayısı)", GUILayout.Height(34)))
            {
                m_DurationPacing = LevelBenchmarkEngine.PuzzleDurationPacing.Custom;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.Custom)
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
                m_MysteryCubePercent = EditorGUILayout.IntSlider(m_MysteryCubePercent, 0, LevelBenchmarkEngine.MaxMysteryCubePercent);
                EditorGUILayout.EndHorizontal();

                m_MysteryRevealCondition = (MysteryRevealCondition)EditorGUILayout.EnumPopup("Açılma Kuralı:", m_MysteryRevealCondition);
            }

            EditorGUILayout.Space(6);

            // -------------------------------------------------------------
            // ⚙️ Bölüm Bilgileri & Mekanikler
            // -------------------------------------------------------------
            GUILayout.Label("⚙️ 6. Seviye Adı & Mekanikler", EditorStyles.boldLabel);
            m_LevelName = EditorGUILayout.TextField("Bölüm Adı:", m_LevelName);
            m_TargetLevelIndex = EditorGUILayout.IntField(new GUIContent("Bölüm No (Sıra):", "Level LevelSequence'te bu konuma eklenir, arkasındakiler bir kayar ve yeniden numaralanır."), m_TargetLevelIndex);
            m_TargetLevelIndex = Mathf.Clamp(m_TargetLevelIndex, 1, SequenceCount() + 1);

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
                    m_SourceCubes = 0;
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

            m_SourceCubes = LevelBenchmarkEngine.CountOpaquePixels(tex);
            ApplyTierDefaults(m_Preset);   // sütun ve maks kapasite resmin büyüklüğüne bağlı
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

            var config = new LevelBenchmarkEngine.GenerationConfig
            {
                preset = m_Preset,
                durationPacing = m_DurationPacing,
                targetShipCount = m_DurationPacing == LevelBenchmarkEngine.PuzzleDurationPacing.Custom ? m_TargetShipCount : 0,
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
                m_GeneratedTexture = activeTex;
                m_PoolColumns = m_CurrentResult.poolColumns;   // Kolay'da motor 3'e zorlar
                m_OuterColors = m_CurrentResult.outerColors;
                // Çözüm garantisi kapalıysa simülasyon yapılmadı: rozet "Doğrulanmadı" der
                m_CheckValid = m_CurrentResult.simulated;
                m_CheckErrors = m_CurrentResult.ruleErrors;
                m_CheckSim = m_CurrentResult.sim;
                m_CheckWarnings = m_CurrentResult.difficultyWarnings;
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
            // Kapasite / halat / gizli / sıra değişikliği son doğrulamayı geçersiz kılar
            EditorGUI.BeginChangeCheck();
            if (m_ViewCards)
            {
                DrawWagonCardsGrid();
            }
            else
            {
                DrawWagonTableView();
            }
            if (EditorGUI.EndChangeCheck()) m_CheckValid = false;
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
            bool ok = m_CheckValid && m_CheckErrors.Count == 0 && m_CheckSim.Solvable;
            string badge = !m_CheckValid ? "⚠️ Doğrulanmadı"
                : !m_CheckSim.Solvable ? "⛔ Çözülemiyor"
                : m_CheckErrors.Count > 0 ? $"⚠️ {m_CheckErrors.Count} kural hatası"
                : $"✓ Çözülebilir · rastgele %{m_CheckSim.randomWinRate * 100:F0} · akıllı %{m_CheckSim.smartWinRate * 100:F0}" +
                  (m_CheckSim.extended ? $" · tap %{m_CheckSim.tapSmartWinRate * 100:F0}" + (m_CheckSim.ponrMoves >= 0 ? $" · hata→kayıp {m_CheckSim.ponrMoves:F1} hamle" : "") : "");
            GUI.color = ok ? new Color(0.2f, 0.9f, 0.4f) : new Color(1f, 0.7f, 0.2f);
            GUILayout.Label(new GUIContent(badge, m_CheckErrors.Count > 0 ? string.Join("\n", m_CheckErrors) : null), EditorStyles.boldLabel);
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();

            if (m_CheckValid && m_CheckWarnings.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", m_CheckWarnings), MessageType.Warning);
            }
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
                m_CheckValid = false;
            }

            if (GUILayout.Button(new GUIContent("🔍 Doğrula", "Kuralları ve kuyruk simülatörünü çalıştırır"), EditorStyles.miniButton, GUILayout.Width(80)))
            {
                RunCheck();
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
                m_CheckValid = false;
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
                    m_CheckValid = false;
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

            // Kuyruk elle değişmiş olabilir: kaydetmeden önce yeniden ölç, sorun varsa sor
            RunCheck();
            var placement = LevelBenchmarkEngine.CheckSequencePlacement(m_TargetLevelIndex, tier, m_TotalCubes);
            if (m_CheckErrors.Count > 0 || !m_CheckSim.Solvable || placement.Count > 0 || m_CheckWarnings.Count > 0)
            {
                string problems = (!m_CheckSim.Solvable ? "• Kuyruk simülatöründe çözülemiyor\n" : "") +
                                  string.Join("\n", m_CheckErrors.Take(8).Select(e => "• " + e)) +
                                  (placement.Count > 0 ? "\n\n" + $"Sıra {m_TargetLevelIndex}:\n" + string.Join("\n", placement.Select(e => "• " + e)) : "") +
                                  (m_CheckWarnings.Count > 0 ? "\n\nZorluk:\n" + string.Join("\n", m_CheckWarnings.Select(e => "• " + e)) : "");
                problems = problems.Trim();
                if (!EditorUtility.DisplayDialog("Kaydetmeden önce kontrol et", problems, "Yine de kaydet", "Vazgeç")) return;
            }

            string savedPath;
            var createdLevel = LevelBenchmarkEngine.SaveAndRegisterNewLevel(
                m_GeneratedTexture != null ? m_GeneratedTexture : m_SourceTexture,
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

        private LevelDifficultyTier CurrentTier()
        {
            if (m_Preset == LevelBenchmarkEngine.DifficultyPreset.Kolay) return LevelDifficultyTier.Kolay;
            if (m_Preset == LevelBenchmarkEngine.DifficultyPreset.Zor) return LevelDifficultyTier.Zor;
            return LevelDifficultyTier.Orta;
        }

        private void RunCheck()
        {
            var (errors, sim) = LevelBenchmarkEngine.CheckWorkingSequence(
                m_GeneratedTexture != null ? m_GeneratedTexture : m_SourceTexture,
                m_WorkingPalette, m_WorkingSequence, m_PoolRows, m_PoolColumns, m_SlotCount);
            m_CheckErrors = errors;
            m_CheckSim = sim;
            m_CheckWarnings = LevelBenchmarkEngine.DifficultyWarnings(CurrentTier(), sim);
            m_CheckValid = true;
            Repaint();
        }

        private void SwapWagons(int a, int b)
        {
            if (a < 0 || a >= m_WorkingSequence.Count || b < 0 || b >= m_WorkingSequence.Count) return;
            var temp = m_WorkingSequence[a];
            m_WorkingSequence[a] = m_WorkingSequence[b];
            m_WorkingSequence[b] = temp;
            m_CheckValid = false;
        }

        /// <summary>Halatı kaldırır ya da geçerli bir komşuya (sağ, sol, alt, üst; farklı renk) bağlar.</summary>
        private void ToggleWagonLink(int index)
        {
            m_CheckValid = false;
            var w = m_WorkingSequence[index];
            if (w.linkId > 0)
            {
                int oldId = w.linkId;
                foreach (var x in m_WorkingSequence) if (x != null && x.linkId == oldId) x.linkId = 0;
                return;
            }

            int cols = Mathf.Max(1, m_PoolColumns);
            foreach (int p in new[] { index + 1, index - 1, index + cols, index - cols })
            {
                if (p < 0 || p >= m_WorkingSequence.Count || !LevelBenchmarkEngine.IsValidLinkPosition(index, p, cols)) continue;
                var partner = m_WorkingSequence[p];
                if (partner == null || partner.linkId != 0 || partner.isHidden || partner.isDualColor || w.isHidden || w.isDualColor) continue;
                if (partner.paletteIndex == w.paletteIndex) continue;

                int newId = 1;
                while (m_WorkingSequence.Any(x => x != null && x.linkId == newId)) newId++;
                w.linkId = newId;
                partner.linkId = newId;
                return;
            }

            m_StatusMessage = $"{index + 1}. gemi için geçerli halat partneri yok (yan yana ya da başlangıç havuzunda alt alta, farklı renk, gizli/Güçlü olmayan gemi gerekir).";
            m_StatusType = MessageType.Warning;
        }

        /// <summary>Halatsız gemileri karıştırır, sonra sütun kuralını onarır ve ön sıraya düşen gizli gemiyi açar.</summary>
        private void SafeShuffleWorkingSequence()
        {
            var rng = new System.Random();
            int cols = Mathf.Max(1, m_PoolColumns);
            for (int i = m_WorkingSequence.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                if (m_WorkingSequence[i].linkId == 0 && m_WorkingSequence[j].linkId == 0)
                {
                    (m_WorkingSequence[i], m_WorkingSequence[j]) = (m_WorkingSequence[j], m_WorkingSequence[i]);
                }
            }
            LevelBenchmarkEngine.FixColumnRepeats(m_WorkingSequence, cols, rng);
            LevelBenchmarkEngine.FixOpening(m_WorkingSequence, cols, m_OuterColors, rng);
            LevelBenchmarkEngine.FixColumnRepeats(m_WorkingSequence, cols, rng, m_OuterColors);
            for (int i = 0; i < Mathf.Min(cols, m_WorkingSequence.Count); i++) m_WorkingSequence[i].isHidden = false;
            m_CheckValid = false;
        }

        private void ReassignLinks()
        {
            foreach (var w in m_WorkingSequence) w.linkId = 0;
            var (min, max) = LevelBenchmarkEngine.MechanicRange(CurrentTier(), m_WorkingSequence.Count);
            var rng = new System.Random();
            LevelBenchmarkEngine.ApplyStrategicLinks(m_WorkingSequence, rng.Next(min, max + 1), Mathf.Max(1, m_PoolColumns), rng);
            m_CheckValid = false;
        }

        private void ReassignHidden()
        {
            foreach (var w in m_WorkingSequence) w.isHidden = false;
            var (min, max) = LevelBenchmarkEngine.MechanicRange(CurrentTier(), m_WorkingSequence.Count);
            var rng = new System.Random();
            LevelBenchmarkEngine.ApplyBenchmarkHidden(m_WorkingSequence, rng.Next(min, max + 1), Mathf.Max(1, m_PoolColumns), rng);
            m_CheckValid = false;
        }

        private void ToggleDualColor(int index)
        {
            if (index < 0 || index >= m_WorkingSequence.Count) return;
            var w = m_WorkingSequence[index];
            if (w == null) return;
            m_CheckValid = false;

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

                // Güçlü konteyner halatlı olmaz: partnerin halatı da kalkar (yoksa tek başına "halatlı" gemi kalıyordu)
                if (w.linkId > 0)
                {
                    int oldId = w.linkId;
                    foreach (var x in m_WorkingSequence) if (x != null && x.linkId == oldId) x.linkId = 0;
                }
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
            m_CheckValid = false;
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
