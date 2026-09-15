using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace PixelGame.Editor
{
    /// <summary>
    /// Kullanıcı dostu, görsel Level Tasarımcısı (Level Designer) Editör Penceresi.
    /// Farklı görseller üzerinden kolayca yeni bölümler üretilmesini, seviyelerin silinmesini/çoğaltılmasını/sıralanmasını,
    /// palet renklerinin tek tek değiştirilmesini (recolor), ton kaydırmayı (hue shift), Toony Colors Pro entegrasyonunu
    /// ve canlı test edilmesini sağlar.
    /// </summary>
    public class PixelLevelDesignerWindow : EditorWindow
    {
        private List<PixelLevelData> m_AllLevels = new List<PixelLevelData>();
        private PixelLevelData m_SelectedLevel;
        private Vector2 m_SidebarScroll;
        private Vector2 m_DetailScroll;
        private string m_SearchFilter = "";

        private enum DetailTab
        {
            LevelSetup = 0,
            ColorStudio = 1,
            TruckLayout = 2
        }

        private DetailTab m_CurrentTab = DetailTab.ColorStudio;
        private Color m_PreviewBlockColor = new Color32(230, 40, 40, 255);

        [MenuItem("Tools/PixelGame/🛠️ Level Designer (Bölüm Tasarımcısı)", priority = 5)]
        [MenuItem("Window/PixelGame/Level Designer")]
        public static void OpenWindow()
        {
            var window = GetWindow<PixelLevelDesignerWindow>("Level Designer");
            window.minSize = new Vector2(860, 600);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshLevelList();
        }

        private void OnGUI()
        {
            // Üst Başlık Barı
            DrawTopToolbar();

            // İki sütunlu düzen: Sol (Level Listesi), Sağ (Seçili Level Düzenleyici)
            EditorGUILayout.BeginHorizontal();

            // 1. Sol Sidebar: Level Listesi & Hızlı Eylemler
            DrawSidebar(310);

            // Ayırıcı çizgi
            DrawVerticalDivider();

            // 2. Sağ Panel: Seçili Level Detayları, Renk Ayarları & Toony Colors Pro
            DrawDetailPanel();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTopToolbar()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 40);
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.14f, 0.2f, 1f));

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.3f, 0.88f, 1f) }
            };

            GUI.Label(new Rect(rect.x + 12, rect.y, rect.width - 320, rect.height), "🛠️ Pixel Game - Bölüm Tasarımcısı & Renk Editörü", titleStyle);

            if (GUI.Button(new Rect(rect.xMax - 225, rect.y + 7, 105, 26), "➕ Yeni Bölüm"))
            {
                CreateNewLevel();
            }

            if (GUI.Button(new Rect(rect.xMax - 110, rect.y + 7, 95, 26), "🔄 Listeyi Yenile"))
            {
                RefreshLevelList();
            }
        }

        private void DrawSidebar(float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.ExpandHeight(true));
            EditorGUILayout.Space(6);

            // Başlık & Arama Çubuğu
            EditorGUILayout.LabelField($"📋 Kayıtlı Bölümler ({m_AllLevels.Count})", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            m_SearchFilter = EditorGUILayout.TextField(m_SearchFilter, EditorStyles.toolbarSearchField);
            if (!string.IsNullOrEmpty(m_SearchFilter))
            {
                if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    m_SearchFilter = "";
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            m_SidebarScroll = EditorGUILayout.BeginScrollView(m_SidebarScroll, GUILayout.ExpandHeight(true));

            if (m_AllLevels.Count == 0)
            {
                EditorGUILayout.HelpBox("Henüz oluşturulmuş bir level yok. Aşağıdaki butondan ilk levelinizi oluşturabilirsiniz!", MessageType.Info);
            }

            for (int i = 0; i < m_AllLevels.Count; i++)
            {
                PixelLevelData level = m_AllLevels[i];
                if (level == null) continue;

                // Arama filtresi kontrolü
                if (!string.IsNullOrEmpty(m_SearchFilter))
                {
                    string term = m_SearchFilter.ToLowerInvariant();
                    bool matches = level.LevelName.ToLowerInvariant().Contains(term) || 
                                   level.LevelIndex.ToString().Contains(term);
                    if (!matches) continue;
                }

                bool isSelected = (m_SelectedLevel == level);

                GUI.backgroundColor = isSelected ? new Color(0.2f, 0.65f, 1f) : new Color(0.92f, 0.92f, 0.92f);

                EditorGUILayout.BeginHorizontal("box", GUILayout.Height(46));

                // Küçük resim önizlemesi (Thumbnail)
                Texture2D tex = level.GetActiveTexture();
                if (tex != null)
                {
                    Rect thumbRect = EditorGUILayout.GetControlRect(false, 38, GUILayout.Width(38));
                    GUI.DrawTexture(thumbRect, tex, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUILayout.LabelField("🖼️", GUILayout.Width(26));
                }

                // Level adı ve boyut bilgisi
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"Level {level.LevelIndex}: {level.LevelName}", EditorStyles.boldLabel);
                Vector2Int res = level.GetGridResolution();
                EditorGUILayout.LabelField($"{res.x}x{res.y} Piksel | {level.ColorPalette.Count} Renk", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                // Seçim algılama alanı
                Rect rowRect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    SelectLevel(level);
                    Event.current.Use();
                }

                // Hızlı Eylem Butonları: Yukarı, Aşağı, Çoğalt, Sil
                EditorGUILayout.BeginVertical(GUILayout.Width(50));
                EditorGUILayout.BeginHorizontal();

                // Yukarı Taşı
                GUI.enabled = (i > 0);
                if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24), GUILayout.Height(18)))
                {
                    MoveLevel(i, i - 1);
                    return;
                }
                // Aşağı Taşı
                GUI.enabled = (i < m_AllLevels.Count - 1);
                if (GUILayout.Button("▼", EditorStyles.miniButtonRight, GUILayout.Width(24), GUILayout.Height(18)))
                {
                    MoveLevel(i, i + 1);
                    return;
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                // Çoğalt (Duplicate)
                GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
                if (GUILayout.Button("📋", EditorStyles.miniButtonLeft, GUILayout.Width(24), GUILayout.Height(18)))
                {
                    DuplicateLevel(level);
                    return;
                }
                // Sil (Delete)
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("🗑️", EditorStyles.miniButtonRight, GUILayout.Width(24), GUILayout.Height(18)))
                {
                    DeleteLevel(level);
                    return;
                }
                GUI.backgroundColor = isSelected ? new Color(0.2f, 0.65f, 1f) : Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);

            // Yeni Level Ekle Butonu
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.45f);
            if (GUILayout.Button("➕ Yeni Level Ekle", GUILayout.Height(34)))
            {
                CreateNewLevel();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.85f, 0.9f, 0.95f);
            if (GUILayout.Button("🔢 Numaraları Sırala (1..N)", GUILayout.Height(24)))
            {
                AutoRenumberLevels();
            }
            if (GUILayout.Button("🔄 LevelManager'a Eşitle", GUILayout.Height(24)))
            {
                SyncWithSceneLevelManager();
                EditorUtility.DisplayDialog("Senkronizasyon Başarılı", "Tüm seviyeler sahnedeki LevelManager ile başarıyla eşitlendi.", "Tamam");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.EndVertical();
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (m_SelectedLevel == null)
            {
                EditorGUILayout.Space(40);
                GUIStyle emptyStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.gray }
                };
                EditorGUILayout.LabelField("Düzenlemek veya sahnede test etmek için\nsoldaki listeden bir level seçin veya 'Yeni Level Ekle'ye tıklayın.", emptyStyle, GUILayout.Height(60));
                EditorGUILayout.EndVertical();
                return;
            }

            m_DetailScroll = EditorGUILayout.BeginScrollView(m_DetailScroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.Space(8);

            // Başlık, Çoğalt ve Sil Butonları
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"✏️ Düzenlenen: Level {m_SelectedLevel.LevelIndex} - {m_SelectedLevel.LevelName}", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
            if (GUILayout.Button("📋 Bölümü Çoğalt", GUILayout.Width(110), GUILayout.Height(24)))
            {
                DuplicateLevel(m_SelectedLevel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            GUI.backgroundColor = new Color(0.95f, 0.35f, 0.35f);
            if (GUILayout.Button("🗑️ Bölümü Sil", GUILayout.Width(95), GUILayout.Height(24)))
            {
                DeleteLevel(m_SelectedLevel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Sekme Seçimi (Tabs)
            EditorGUILayout.BeginHorizontal();
            GUIStyle tabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                fixedHeight = 30
            };

            DrawTabButton(DetailTab.LevelSetup, "📋 Bölüm & Izgara", tabStyle);
            DrawTabButton(DetailTab.ColorStudio, "🎨 Renk Ayarları (Color Studio)", tabStyle);
            DrawTabButton(DetailTab.TruckLayout, "🚚 Vagon & Ray Düzeni", tabStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            EditorGUI.BeginChangeCheck();

            if (m_CurrentTab == DetailTab.LevelSetup)
            {
                // 1. Temel Bilgiler
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("📋 Genel Bilgiler", EditorStyles.boldLabel);
                m_SelectedLevel.LevelName = EditorGUILayout.TextField("Level Adı", m_SelectedLevel.LevelName);
                m_SelectedLevel.LevelIndex = EditorGUILayout.IntField("Level Numarası", m_SelectedLevel.LevelIndex);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(6);

                // 2. Görsel Seçici & Canlı Önizleme
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("🖼️ Kaynak Piksel Görseli", EditorStyles.boldLabel);

                Texture2D newTex = (Texture2D)EditorGUILayout.ObjectField("Görsel (Texture2D)", m_SelectedLevel.LevelTexture, typeof(Texture2D), false);
                if (newTex != m_SelectedLevel.LevelTexture)
                {
                    m_SelectedLevel.LevelTexture = newTex;
                    if (newTex != null)
                    {
                        m_SelectedLevel.ExtractPaletteFromTexture();
                    }
                }

                Texture2D activeTex = m_SelectedLevel.GetActiveTexture();
                if (activeTex != null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.BeginHorizontal();

                    // Önizleme kutusu
                    Rect previewRect = EditorGUILayout.GetControlRect(false, 96, GUILayout.Width(96));
                    EditorGUI.DrawRect(previewRect, new Color(0.12f, 0.12f, 0.12f, 1f));
                    GUI.DrawTexture(previewRect, activeTex, ScaleMode.ScaleToFit);

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField($"Boyut: {activeTex.width} x {activeTex.height} Piksel", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Toplam Küp: {activeTex.width * activeTex.height} adet");

                    string path = AssetDatabase.GetAssetPath(activeTex);
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && !importer.isReadable)
                    {
                        EditorGUILayout.HelpBox("⚠️ Görsel 'Read/Write' iznine sahip değil.", MessageType.Warning);
                        if (GUILayout.Button("🔧 Görseli Okunabilir Yap", GUILayout.Height(22)))
                        {
                            importer.isReadable = true;
                            importer.SaveAndReimport();
                            m_SelectedLevel.ExtractPaletteFromTexture();
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("✅ Görsel piksel okumaya hazır.", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.HelpBox("Lütfen bu levelde küplerle çizilecek bir piksel resmi sürükleyip bırakın.", MessageType.Info);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(6);

                // 3. 📐 PİKSEL UYUMU & IZGARA AYARLARI
                DrawGridSettingsSection();
            }
            else if (m_CurrentTab == DetailTab.ColorStudio)
            {
                // 1. 🎨 VAGON, MADENCİ & RAY ÖZEL RENK AYARLARI ("Vagon Ne Renkse O" Dinamik Ayarı Dahil)
                DrawThemeColorSection();

                EditorGUILayout.Space(6);

                // 2. 🎨 BÖLÜM RENK PALETİ VE RENK DEĞİŞTİRME
                DrawColorPaletteSection();

                EditorGUILayout.Space(6);

                // 3. ☀️ GENEL RENK & IŞIK AYARLARI (Parlaklık, Doygunluk, Kontrast)
                DrawGlobalColorSettingsSection();

                EditorGUILayout.Space(6);

                // 4. 🎨 TOONY COLORS PRO
                DrawToonyColorsProSection();
            }
            else if (m_CurrentTab == DetailTab.TruckLayout)
            {
                // 🚚 VAGON DÜZENİ (slot sayısı, havuz sıraları, kapasite)
                DrawTruckLayoutSection();
            }

            // Değişiklik algılandıysa kaydet ve canlı güncelle
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }

            EditorGUILayout.Space(12);

            // 8. AKSİYON BUTONLARI (Büyük ve Belirgin)
            DrawActionButtons();

            EditorGUILayout.Space(16);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawTabButton(DetailTab tab, string label, GUIStyle style)
        {
            bool isSelected = m_CurrentTab == tab;
            GUI.backgroundColor = isSelected ? new Color(0.25f, 0.75f, 1f) : new Color(0.85f, 0.85f, 0.9f);
            if (GUILayout.Button(label, style))
            {
                m_CurrentTab = tab;
            }
            GUI.backgroundColor = Color.white;
        }

        /// <summary>
        /// Vagon, Madenci, Ray parçalarının renklerini ve dinamik 'Vagon Rengini Kullan' ayarlarını yöneten özel sekme.
        /// </summary>
        private void DrawThemeColorSection()
        {
            if (m_SelectedLevel == null) return;
            LevelColorTheme theme = m_SelectedLevel.ColorTheme;
            if (theme == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Üst Başlık & Hızlı Butonlar
            EditorGUILayout.BeginHorizontal();
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.2f, 0.8f, 1f) }
            };
            EditorGUILayout.LabelField("🎨 Vagon, Madenci & Ray Parça Renkleri", headerStyle);

            GUI.backgroundColor = new Color(0.3f, 0.88f, 0.45f);
            if (GUILayout.Button("⚡ Tümünü Vagon Rengine Bağla", GUILayout.Width(195), GUILayout.Height(22)))
            {
                foreach (var p in theme.Parts)
                {
                    p.matchBlockColor = true;
                }
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }

            GUI.backgroundColor = new Color(0.85f, 0.9f, 0.98f);
            if (GUILayout.Button("🎯 Klasik Şablon", GUILayout.Width(110), GUILayout.Height(22)))
            {
                theme.ResetToDefault();
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox(
                "Her bir parça için sabit bir özel renk belirleyebilir veya '✓ Vagon Rengini Kullan' " +
                "butonuna basarak parçanın o an gelen vagonun/hedef bloğun rengine otomatik bürünmesini sağlayabilirsiniz.",
                MessageType.Info
            );
            EditorGUILayout.Space(6);

            // 🧪 Canlı Test / Önizleme Bloğu Çubuğu
            DrawTestBlockColorBar();

            EditorGUILayout.Space(8);

            // 1. 🚚 VAGON PARÇALARI
            DrawPartGroupHeader("🚚 Vagon (MineCart / ToyTruck) Parçaları");
            DrawPartColorRow(theme, TruckPart.Cabin, "Kabin (Kabin + Kaput)", "🚛");
            DrawPartColorRow(theme, TruckPart.Cargo, "Kasa (Cargo + Arka Kapak)", "📦");
            DrawPartColorRow(theme, TruckPart.Rims, "Jantlar (Rims)", "⚙️");
            DrawPartColorRow(theme, TruckPart.Tires, "Tekerlekler (Tires)", "🛞");
            DrawPartColorRow(theme, TruckPart.Glass, "Camlar (Glass)", "🪟");
            DrawPartColorRow(theme, TruckPart.Headlights, "Ön Farlar (Headlights)", "💡");
            DrawPartColorRow(theme, TruckPart.Taillights, "Arka Stoplar (Taillights)", "🔴");
            DrawPartColorRow(theme, TruckPart.Chassis, "Şasi / Alt Gövde", "🔧");

            EditorGUILayout.Space(8);

            // 2. ⛏️ MADENCİ KARAKTERİ
            DrawPartGroupHeader("⛏️ Madenci (MechaMiner) Parçaları");
            DrawPartColorRow(theme, TruckPart.MechaBody, "Karakter Gövdesi (Zırh)", "🤖");
            DrawPartColorRow(theme, TruckPart.Helmet, "Baret (Kubbe & Siperlik)", "⛑️");
            DrawPartColorRow(theme, TruckPart.HelmetDark, "Baret Detayı / Koyu Ton", "🕶️");
            DrawPartColorRow(theme, TruckPart.Lamp, "Baret Fener Lambası", "🔦");

            EditorGUILayout.Space(8);

            // 3. 🛤️ RAY VE MADEN GİRİŞİ
            DrawPartGroupHeader("🛤️ Ray & Maden Girişi (Portal) Parçaları");
            DrawPartColorRow(theme, TruckPart.Stone, "Portal Taşları", "🪨");
            DrawPartColorRow(theme, TruckPart.StoneDark, "Taşların Koyu Tonu", "⬛");
            DrawPartColorRow(theme, TruckPart.Wood, "Ahşap Traversler & Direkler", "🪵");
            DrawPartColorRow(theme, TruckPart.Dark, "Maden Girişi İç Karanlığı", "🕳️");

            EditorGUILayout.EndVertical();
        }

        private void DrawTestBlockColorBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("🧪 Canlı Test Rengi (Önizleme):", EditorStyles.boldLabel, GUILayout.Width(190));

            Color[] quickColors = new Color[]
            {
                new Color32(230, 40, 40, 255),  // Kırmızı
                new Color32(40, 110, 235, 255), // Mavi
                new Color32(255, 196, 30, 255), // Sarı
                new Color32(60, 190, 80, 255),  // Yeşil
                new Color32(150, 70, 220, 255), // Mor
                new Color32(255, 128, 30, 255), // Turuncu
            };

            foreach (Color qc in quickColors)
            {
                GUI.backgroundColor = qc;
                if (GUILayout.Button("", GUILayout.Width(22), GUILayout.Height(18)))
                {
                    m_PreviewBlockColor = qc;
                    NotifyLiveSceneUpdate();
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(6);
            Color newTest = EditorGUILayout.ColorField(GUIContent.none, m_PreviewBlockColor, false, false, false, GUILayout.Width(70));
            if (newTest != m_PreviewBlockColor)
            {
                m_PreviewBlockColor = newTest;
                NotifyLiveSceneUpdate();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPartGroupHeader(string title)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(r, new Color(0.18f, 0.22f, 0.28f, 1f));
            GUIStyle st = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.4f, 0.85f, 1f) },
                alignment = TextAnchor.MiddleLeft
            };
            GUI.Label(new Rect(r.x + 8, r.y + 2, r.width - 16, r.height), title, st);
        }

        private void DrawPartColorRow(LevelColorTheme theme, TruckPart part, string displayName, string icon)
        {
            TruckPartColorSetting setting = theme.GetSetting(part);
            Color resolvedColor = theme.ResolveColor(part, m_PreviewBlockColor);

            EditorGUILayout.BeginHorizontal();

            // İkon ve İsim
            GUILayout.Label($"{icon} {displayName}", GUILayout.Width(210));

            // "Vagon / Blok Rengini Kullan" Butonu / Toggle
            GUI.backgroundColor = setting.matchBlockColor ? new Color(0.25f, 0.85f, 0.45f) : new Color(0.85f, 0.85f, 0.88f);
            string btnText = setting.matchBlockColor ? "✓ Vagon Rengini Kullan" : "  Vagon Rengini Kullan";
            if (GUILayout.Button(btnText, GUILayout.Width(170), GUILayout.Height(20)))
            {
                setting.matchBlockColor = !setting.matchBlockColor;
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(8);

            // Renk Seçici veya Dinamik Çözümleme Rozeti
            if (setting.matchBlockColor)
            {
                Rect badgeRect = EditorGUILayout.GetControlRect(false, 18, GUILayout.Width(90));
                EditorGUI.DrawRect(badgeRect, resolvedColor);
                GUIStyle badgeText = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = (resolvedColor.grayscale > 0.5f) ? Color.black : Color.white }
                };
                GUI.Label(badgeRect, "⚡ Vagon Rengi", badgeText);
                GUILayout.Label("(Dinamik: blok rengini alır)", EditorStyles.miniLabel);
            }
            else
            {
                Color newCol = EditorGUILayout.ColorField(GUIContent.none, setting.customColor, false, false, false, GUILayout.Width(90));
                if (newCol != setting.customColor)
                {
                    setting.customColor = newCol;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
                GUILayout.Label("(Sabit özel renk)", EditorStyles.miniLabel);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Toony Colors Pro tool ve shader parametrelerinin doğrudan Level Designer içinden ayarlanması.
        /// </summary>
        private void DrawToonyColorsProSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🎨 Toony Colors Pro (Toon Görünüm & Gölgelendirme)", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
            if (GUILayout.Button("🚀 TCP2 Tool'u Aç", GUILayout.Width(130), GUILayout.Height(22)))
            {
                EditorApplication.ExecuteMenuItem("Tools/Toony Colors Pro/Shader Generator 2");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            Material cartoonMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PixelCube_Cartoon.mat");
            if (cartoonMat == null)
            {
                string[] guids = AssetDatabase.FindAssets("PixelCube_Cartoon t:Material");
                if (guids.Length > 0)
                {
                    cartoonMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            if (cartoonMat == null)
            {
                EditorGUILayout.HelpBox("PixelCube_Cartoon.mat materyali bulunamadı.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.HelpBox("Küp parçalarının Toon (çizgi roman / cel-shaded) ton, ışık ve gölgelendirme eşiklerini buradan canlı olarak ayarlayabilirsiniz.", MessageType.None);
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();

            // Highlight Color (_HColor)
            Color hColor = cartoonMat.HasProperty("_HColor") ? cartoonMat.GetColor("_HColor") : Color.white;
            Color newHColor = EditorGUILayout.ColorField("Aydınlık Tonu (Highlight / HColor)", hColor);

            // Shadow Color (_SColor)
            Color sColor = cartoonMat.HasProperty("_SColor") ? cartoonMat.GetColor("_SColor") : new Color(0.643f, 0.655f, 0.714f);
            Color newSColor = EditorGUILayout.ColorField("Toon Gölge Rengi (Shadow / SColor)", sColor);

            // Ramp Threshold (_RampThreshold)
            float rampThreshold = cartoonMat.HasProperty("_RampThreshold") ? cartoonMat.GetFloat("_RampThreshold") : 0.5f;
            float newRampThreshold = EditorGUILayout.Slider("Gölge Eşiği (Ramp Threshold)", rampThreshold, 0f, 1f);

            // Ramp Smoothing (_RampSmoothing)
            float rampSmoothing = cartoonMat.HasProperty("_RampSmoothing") ? cartoonMat.GetFloat("_RampSmoothing") : 0.5f;
            float newRampSmoothing = EditorGUILayout.Slider("Toon Yumuşatma (Ramp Smoothing)", rampSmoothing, 0.001f, 1f);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(cartoonMat, "Modify Toony Colors Material");
                if (cartoonMat.HasProperty("_HColor")) cartoonMat.SetColor("_HColor", newHColor);
                if (cartoonMat.HasProperty("_SColor")) cartoonMat.SetColor("_SColor", newSColor);
                if (cartoonMat.HasProperty("_RampThreshold")) cartoonMat.SetFloat("_RampThreshold", newRampThreshold);
                if (cartoonMat.HasProperty("_RampSmoothing")) cartoonMat.SetFloat("_RampSmoothing", newRampSmoothing);
                EditorUtility.SetDirty(cartoonMat);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(4);

            // Hazır Toon Ayarları (Presets)
            EditorGUILayout.LabelField("Toony Colors Hızlı Hazır Ayarları (Presets):", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("💥 Çizgi Roman (Cel)"))
            {
                Undo.RecordObject(cartoonMat, "Toon Preset Cel");
                cartoonMat.SetColor("_HColor", Color.white);
                cartoonMat.SetColor("_SColor", new Color(0.55f, 0.57f, 0.68f));
                cartoonMat.SetFloat("_RampThreshold", 0.50f);
                cartoonMat.SetFloat("_RampSmoothing", 0.05f);
                EditorUtility.SetDirty(cartoonMat);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("☁️ Yumuşak Toon"))
            {
                Undo.RecordObject(cartoonMat, "Toon Preset Soft");
                cartoonMat.SetColor("_HColor", Color.white);
                cartoonMat.SetColor("_SColor", new Color(0.70f, 0.72f, 0.78f));
                cartoonMat.SetFloat("_RampThreshold", 0.50f);
                cartoonMat.SetFloat("_RampSmoothing", 0.65f);
                EditorUtility.SetDirty(cartoonMat);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("☀️ Sıcak Gün Işığı"))
            {
                Undo.RecordObject(cartoonMat, "Toon Preset Warm");
                cartoonMat.SetColor("_HColor", new Color(1f, 0.98f, 0.92f));
                cartoonMat.SetColor("_SColor", new Color(0.75f, 0.65f, 0.58f));
                cartoonMat.SetFloat("_RampThreshold", 0.45f);
                cartoonMat.SetFloat("_RampSmoothing", 0.35f);
                EditorUtility.SetDirty(cartoonMat);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("🎮 Keskin Anime"))
            {
                Undo.RecordObject(cartoonMat, "Toon Preset Anime");
                cartoonMat.SetColor("_HColor", Color.white);
                cartoonMat.SetColor("_SColor", new Color(0.48f, 0.50f, 0.60f));
                cartoonMat.SetFloat("_RampThreshold", 0.52f);
                cartoonMat.SetFloat("_RampSmoothing", 0.005f);
                EditorUtility.SetDirty(cartoonMat);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎨 Ramp Generator'ı Aç", GUILayout.Height(22)))
            {
                EditorApplication.ExecuteMenuItem("Tools/Toony Colors Pro/Ramp Generator");
            }
            if (GUILayout.Button("🔍 Materyali Inspector'da Göster", GUILayout.Height(22)))
            {
                Selection.activeObject = cartoonMat;
                EditorGUIUtility.PingObject(cartoonMat);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawColorPaletteSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"🎨 Bölüm Renk Paleti ({m_SelectedLevel.ColorPalette.Count} Renk)", EditorStyles.boldLabel);

            if (GUILayout.Button("🔄 Paleti Yenile", GUILayout.Width(110), GUILayout.Height(20)))
            {
                m_SelectedLevel.ExtractPaletteFromTexture();
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }

            if (GUILayout.Button("↺ Renkleri Sıfırla", GUILayout.Width(115), GUILayout.Height(20)))
            {
                m_SelectedLevel.ResetPalette();
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox("Görseldeki herhangi bir rengi değiştirmek için sağdaki renk kutucuğuna tıklayın. Örneğin mavi kürk rengini kırmızıya, sarı arka planı mora dönüştürebilirsiniz!", MessageType.None);
            EditorGUILayout.Space(4);

            if (m_SelectedLevel.ColorPalette.Count == 0)
            {
                if (m_SelectedLevel.GetActiveTexture() != null)
                {
                    if (GUILayout.Button("🔍 Görselden Renk Paletini Çıkar", GUILayout.Height(28)))
                    {
                        m_SelectedLevel.ExtractPaletteFromTexture();
                        EditorUtility.SetDirty(m_SelectedLevel);
                    }
                }
            }
            else
            {
                int totalPixels = 0;
                foreach (var p in m_SelectedLevel.ColorPalette) totalPixels += p.pixelCount;
                if (totalPixels == 0) totalPixels = 1;

                for (int i = 0; i < m_SelectedLevel.ColorPalette.Count; i++)
                {
                    var item = m_SelectedLevel.ColorPalette[i];

                    EditorGUILayout.BeginHorizontal("box");

                    // 1. Orijinal Renk Kutusu
                    Rect origRect = EditorGUILayout.GetControlRect(false, 20, GUILayout.Width(28));
                    EditorGUI.DrawRect(origRect, item.originalColor);

                    EditorGUILayout.LabelField("Orijinal", GUILayout.Width(50));
                    EditorGUILayout.LabelField("➔", GUILayout.Width(18));

                    // 2. Hedef Renk Seçici (Color Field)
                    Color newTarget = EditorGUILayout.ColorField(GUIContent.none, item.targetColor, true, false, false, GUILayout.Width(75));
                    if (newTarget != item.targetColor)
                    {
                        item.targetColor = newTarget;
                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                    }

                    // 3. Küp Sayısı ve Yüzde Bilgisi
                    float pct = (float)item.pixelCount / totalPixels * 100f;
                    EditorGUILayout.LabelField($"{item.pixelCount} küp (%{pct:F0})", EditorStyles.miniLabel, GUILayout.Width(85));

                    // 4. Tek Renk Sıfırlama Butonu (Değiştirildiyse)
                    if (item.IsOverridden)
                    {
                        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
                        if (GUILayout.Button("↺", GUILayout.Width(22), GUILayout.Height(18)))
                        {
                            item.targetColor = item.originalColor;
                            EditorUtility.SetDirty(m_SelectedLevel);
                            NotifyLiveSceneUpdate();
                        }
                        GUI.backgroundColor = Color.white;
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGlobalColorSettingsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("☀️ Genel Renk Tonu & Işık Ayarları", EditorStyles.boldLabel);

            m_SelectedLevel.HueShift = EditorGUILayout.Slider("Renk Tonunu Kaydır (Hue Shift)", m_SelectedLevel.HueShift, -180f, 180f);
            m_SelectedLevel.TintColor = EditorGUILayout.ColorField("Genel Renk Filtresi (Tint)", m_SelectedLevel.TintColor);

            m_SelectedLevel.ColorBrightness = EditorGUILayout.Slider("Parlaklık (Brightness)", m_SelectedLevel.ColorBrightness, 0.5f, 2.5f);
            m_SelectedLevel.ColorSaturation = EditorGUILayout.Slider("Doygunluk (Saturation)", m_SelectedLevel.ColorSaturation, 0f, 2.5f);
            m_SelectedLevel.ColorContrast = EditorGUILayout.Slider("Kontrast", m_SelectedLevel.ColorContrast, 0.5f, 2f);
            m_SelectedLevel.EmissionIntensity = EditorGUILayout.Slider("Işıma / Glow (Emission)", m_SelectedLevel.EmissionIntensity, 0f, 2f);

            // Hızlı Hazır Ayarlar
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Hızlı Tema Hazır Ayarları (Presets):", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("🌟 Canlı & Zengin"))
            {
                m_SelectedLevel.ColorBrightness = 1.25f;
                m_SelectedLevel.ColorSaturation = 1.3f;
                m_SelectedLevel.ColorContrast = 1.05f;
                m_SelectedLevel.EmissionIntensity = 0.35f;
                m_SelectedLevel.TintColor = Color.white;
                m_SelectedLevel.HueShift = 0f;
                NotifyLiveSceneUpdate();
            }
            if (GUILayout.Button("🎨 Orijinal / Saf"))
            {
                m_SelectedLevel.ColorBrightness = 1.0f;
                m_SelectedLevel.ColorSaturation = 1.0f;
                m_SelectedLevel.ColorContrast = 1.0f;
                m_SelectedLevel.EmissionIntensity = 0.0f;
                m_SelectedLevel.TintColor = Color.white;
                m_SelectedLevel.HueShift = 0f;
                NotifyLiveSceneUpdate();
            }
            if (GUILayout.Button("☀️ Ekstra Işıltılı"))
            {
                m_SelectedLevel.ColorBrightness = 1.45f;
                m_SelectedLevel.ColorSaturation = 1.35f;
                m_SelectedLevel.ColorContrast = 1.1f;
                m_SelectedLevel.EmissionIntensity = 0.65f;
                NotifyLiveSceneUpdate();
            }
            if (GUILayout.Button("🌸 Pastel Ton"))
            {
                m_SelectedLevel.ColorBrightness = 1.2f;
                m_SelectedLevel.ColorSaturation = 0.75f;
                m_SelectedLevel.ColorContrast = 0.95f;
                m_SelectedLevel.EmissionIntensity = 0.2f;
                NotifyLiveSceneUpdate();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawGridSettingsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📐 Piksel Uyumu & Izgara Düzeni", EditorStyles.boldLabel);

            m_SelectedLevel.UseNativeResolution = EditorGUILayout.Toggle(new GUIContent("1:1 Doğal Piksel Boyutu (Önerilen)", "Görselin kendi piksel çözünürlüğünü korur, basıklık ve bozulmayı engeller."), m_SelectedLevel.UseNativeResolution);

            if (!m_SelectedLevel.UseNativeResolution)
            {
                m_SelectedLevel.CustomResolution = EditorGUILayout.Vector2IntField("Özel Çözünürlük (X, Y)", m_SelectedLevel.CustomResolution);
            }

            m_SelectedLevel.CubeSpacing = EditorGUILayout.Slider("Küp Boşluğu (Spacing)", m_SelectedLevel.CubeSpacing, 0f, 0.2f);
            m_SelectedLevel.CubeDepth = EditorGUILayout.Slider("Küp Derinliği (3D Thickness)", m_SelectedLevel.CubeDepth, 0.05f, 1.5f);
            m_SelectedLevel.InnerPadding = EditorGUILayout.Slider("Mavi Çerçeve Payı", m_SelectedLevel.InnerPadding, 0f, 0.2f);
            m_SelectedLevel.SkipTransparent = EditorGUILayout.Toggle("Şeffaf Pikselleri Atla", m_SelectedLevel.SkipTransparent);

            EditorGUILayout.EndVertical();
        }

        private void DrawTruckLayoutSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🛤️ Vagon Düzeni", EditorStyles.boldLabel);

            m_SelectedLevel.SlotCount = EditorGUILayout.IntSlider(
                new GUIContent("Raydaki Vagon Sayısı",
                    "Ray üzerinde aynı anda kaç vagon doldurulabilir. Bölümün zorluğunu en çok " +
                    "bu belirler: az vagon, aynı anda az renge çalışabilmek demektir."),
                m_SelectedLevel.SlotCount, 1, 8);

            EditorGUILayout.Space(2);

            m_SelectedLevel.PoolColumns = EditorGUILayout.IntSlider(
                new GUIContent("Havuz: Yan Yana", "Havuzda yan yana kaç vagon beklesin"),
                m_SelectedLevel.PoolColumns, 1, 8);

            m_SelectedLevel.PoolRows = EditorGUILayout.IntSlider(
                new GUIContent("Havuz: Sıra Sayısı",
                    "Kaç sıra halinde gelsinler. Sıra arttıkça oyuncu sıradaki vagonların " +
                    "daha fazlasını önceden görür, yani daha rahat plan yapar."),
                m_SelectedLevel.PoolRows, 1, 5);

            EditorGUILayout.LabelField(
                $"Havuzda aynı anda görünen: {m_SelectedLevel.PoolPlaceCount} vagon",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(2);

            m_SelectedLevel.TruckCapacity = EditorGUILayout.IntSlider(
                new GUIContent("Vagon Kapasitesi", "Bir vagonun kasasına kaç küp sığar"),
                m_SelectedLevel.TruckCapacity, 1, 64);

            DrawTruckSummary();

            EditorGUILayout.EndVertical();
        }

        private void DrawTruckSummary()
        {
            int required = m_SelectedLevel.GetRequiredTruckCount();

            if (m_SelectedLevel.ColorPalette.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Renk paleti boş. Kamyonlar paletten üretildiği için önce paleti çıkarman gerekir.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField(
                $"Bu bölüm toplam {required} vagon gerektiriyor " +
                $"({m_SelectedLevel.ColorPalette.Count} renk).",
                EditorStyles.miniLabel);

            if (required > 60)
            {
                EditorGUILayout.HelpBox(
                    $"{required} vagon oldukça uzun bir bölüm demek. " +
                    "Kısaltmak için vagon kapasitesini artır.",
                    MessageType.Info);
            }

            if (m_SelectedLevel.PoolPlaceCount < m_SelectedLevel.SlotCount)
            {
                EditorGUILayout.HelpBox(
                    "Havuzdaki vagon sayısı raydaki yer sayısından az. " +
                    "Oyuncu tüm yerleri dolduramaz.",
                    MessageType.Warning);
            }
        }

        private void DrawActionButtons()
        {
            // 1. Büyük İnşa Et Butonu
            GUI.backgroundColor = new Color(0.2f, 0.88f, 0.45f);
            if (GUILayout.Button("▶️ Bu Leveli Sahnede İnşa Et ve Test Et (Build Level)", GUILayout.Height(46)))
            {
                BuildSelectedLevelInScene();
            }

            EditorGUILayout.Space(4);

            // 2. Kaydet ve Temizle Butonları
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("💾 Level Değişikliklerini Kaydet", GUILayout.Height(32)))
            {
                EditorUtility.SetDirty(m_SelectedLevel);
                AssetDatabase.SaveAssets();
                SyncWithSceneLevelManager();
                Debug.Log($"<color=#00FF00>[LevelDesigner]</color> '{m_SelectedLevel.LevelName}' başarıyla kaydedildi.");
            }

            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("🧹 Sahnedeki Küpleri Temizle", GUILayout.Height(32)))
            {
                PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
                if (gen != null)
                {
                    gen.ClearCubes();
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = Color.white;
        }

        private void DrawVerticalDivider()
        {
            Rect dividerRect = EditorGUILayout.GetControlRect(false, GUILayout.Width(2), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(dividerRect, new Color(0.2f, 0.2f, 0.2f, 1f));
        }

        private void NotifyLiveSceneUpdate()
        {
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null && gen.ActiveLevelData == m_SelectedLevel)
            {
                gen.ColorBrightness = m_SelectedLevel.ColorBrightness;
                gen.ColorSaturation = m_SelectedLevel.ColorSaturation;
                gen.ColorContrast = m_SelectedLevel.ColorContrast;
                gen.EmissionIntensity = m_SelectedLevel.EmissionIntensity;
                gen.CubeSpacing = m_SelectedLevel.CubeSpacing;
                gen.CubeDepth = m_SelectedLevel.CubeDepth;
                gen.InnerPadding = m_SelectedLevel.InnerPadding;
                gen.UpdateExistingCubesLive();
            }

            // Sahnedeki vagon, madenci veya ray parçalarını canlı güncelle
            TruckPaint[] paints = Object.FindObjectsByType<TruckPaint>(FindObjectsSortMode.None);
            foreach (var paint in paints)
            {
                if (paint != null && m_SelectedLevel != null && m_SelectedLevel.ColorTheme != null)
                {
                    paint.ApplyTheme(m_SelectedLevel.ColorTheme, m_PreviewBlockColor);
                }
            }

            SceneView.RepaintAll();
        }

        private void RefreshLevelList()
        {
            m_AllLevels.Clear();
            string[] guids = AssetDatabase.FindAssets("t:PixelLevelData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                PixelLevelData level = AssetDatabase.LoadAssetAtPath<PixelLevelData>(path);
                if (level != null)
                {
                    if (level.ColorPalette.Count == 0 && level.GetActiveTexture() != null)
                    {
                        level.ExtractPaletteFromTexture();
                        EditorUtility.SetDirty(level);
                    }
                    m_AllLevels.Add(level);
                }
            }

            m_AllLevels.Sort((a, b) => a.LevelIndex.CompareTo(b.LevelIndex));

            if (m_SelectedLevel == null && m_AllLevels.Count > 0)
            {
                SelectLevel(m_AllLevels[0]);
            }

            SyncWithSceneLevelManager();
        }

        private void SelectLevel(PixelLevelData level)
        {
            m_SelectedLevel = level;
            Selection.activeObject = level;
            if (level != null && level.ColorPalette.Count == 0 && level.GetActiveTexture() != null)
            {
                level.ExtractPaletteFromTexture();
                EditorUtility.SetDirty(level);
            }
        }

        public void CreateNewLevel()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Levels"))
            {
                AssetDatabase.CreateFolder("Assets", "Levels");
            }

            int nextIndex = m_AllLevels.Count + 1;
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/Levels/Level_{nextIndex:D2}_New.asset");

            PixelLevelData newLevel = ScriptableObject.CreateInstance<PixelLevelData>();
            newLevel.LevelName = $"Bölüm {nextIndex}";
            newLevel.LevelIndex = nextIndex;
            newLevel.UseNativeResolution = true;

            AssetDatabase.CreateAsset(newLevel, assetPath);
            AssetDatabase.SaveAssets();

            RefreshLevelList();
            SelectLevel(newLevel);

            Debug.Log($"<color=#00FFAA><b>[LevelDesigner]</b></color> Yeni level oluşturuldu: {assetPath}");
        }

        public void DuplicateLevel(PixelLevelData source)
        {
            if (source == null) return;

            if (!AssetDatabase.IsValidFolder("Assets/Levels"))
            {
                AssetDatabase.CreateFolder("Assets", "Levels");
            }

            int nextIndex = m_AllLevels.Count + 1;
            string safeName = source.LevelName.Replace(" ", "_");
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/Levels/Level_{nextIndex:D2}_{safeName}_Copy.asset");

            PixelLevelData newLevel = ScriptableObject.Instantiate(source);
            newLevel.LevelName = $"{source.LevelName} (Kopya)";
            newLevel.LevelIndex = nextIndex;

            AssetDatabase.CreateAsset(newLevel, assetPath);
            AssetDatabase.SaveAssets();

            RefreshLevelList();
            SelectLevel(newLevel);

            Debug.Log($"<color=#00FFAA><b>[LevelDesigner]</b></color> Bölüm başarıyla çoğaltıldı: {assetPath}");
        }

        public void DeleteLevel(PixelLevelData level)
        {
            if (level == null) return;

            if (!EditorUtility.DisplayDialog("Bölümü Sil", 
                $"'{level.LevelName}' (Level {level.LevelIndex}) kalıcı olarak silinsin mi?", 
                "Evet, Kalıcı Olarak Sil", "Vazgeç"))
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(level);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
                if (m_SelectedLevel == level) m_SelectedLevel = null;
                RefreshLevelList();
                Debug.Log($"<color=yellow><b>[LevelDesigner]</b></color> Bölüm silindi: {path}");
            }
        }

        private void MoveLevel(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= m_AllLevels.Count || toIndex < 0 || toIndex >= m_AllLevels.Count) return;

            PixelLevelData item = m_AllLevels[fromIndex];
            m_AllLevels.RemoveAt(fromIndex);
            m_AllLevels.Insert(toIndex, item);

            AutoRenumberLevels();
        }

        private void AutoRenumberLevels()
        {
            for (int i = 0; i < m_AllLevels.Count; i++)
            {
                if (m_AllLevels[i] != null)
                {
                    m_AllLevels[i].LevelIndex = i + 1;
                    EditorUtility.SetDirty(m_AllLevels[i]);
                }
            }
            AssetDatabase.SaveAssets();
            SyncWithSceneLevelManager();
            Debug.Log("<color=#00FFAA><b>[LevelDesigner]</b></color> Seviye numaraları sıralandı (1.." + m_AllLevels.Count + ").");
        }

        private void SyncWithSceneLevelManager()
        {
            LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
            if (lm != null)
            {
                SerializedObject so = new SerializedObject(lm);
                SerializedProperty prop = so.FindProperty("m_Levels");
                if (prop != null)
                {
                    prop.ClearArray();
                    for (int i = 0; i < m_AllLevels.Count; i++)
                    {
                        prop.InsertArrayElementAtIndex(i);
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = m_AllLevels[i];
                    }
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(lm);
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
            }
        }

        private void BuildSelectedLevelInScene()
        {
            if (m_SelectedLevel == null) return;

            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen == null)
            {
                PixelArtAutoSetup.SetupPixelArtManager(isAuto: true);
                gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            }

            if (gen != null)
            {
                gen.LoadLevel(m_SelectedLevel);
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log($"<color=#00FFAA><b>[LevelDesigner]</b></color> '{m_SelectedLevel.LevelName}' sahnede başarıyla inşa edildi!");
            }
        }
    }
}
