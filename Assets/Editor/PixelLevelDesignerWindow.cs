using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace PixelGame.Editor
{
    /// <summary>
    /// Kapsamlı, modern ve kullanıcı dostu Piksel Oyunu Seviye & Sahne Tasarımcısı (Level Designer).
    /// Bölüm oluşturma, 1:1 piksel uyumu, renk değiştirme (Recolor), Toony Colors Pro entegrasyonu,
    /// 2D Vagon & Dalga ızgara matrisi, AI dengeleme asistanı ve tüm sahne/model kurulum araçlarını
    /// tek bir stüdyo penceresinde toplar.
    /// </summary>
    public class PixelLevelDesignerWindow : EditorWindow
    {
        private List<PixelLevelData> m_AllLevels = new List<PixelLevelData>();
        private PixelLevelData m_SelectedLevel;
        private Vector2 m_SidebarScroll;
        private Vector2 m_DetailScroll;
        private string m_SearchFilter = "";
        private PixelLevelData m_DraggingLevel;

        private int m_GridColumnsPerRow = 2;
        private int m_TargetGridRows = 4;
        private int m_ActiveBrushPaletteIndex = -1;
        private int m_SelectedSlotForSwap = -1;
        private string m_LastSmartStatusMessage = "";

        public enum WagonDifficultyMode
        {
            Easy = 0,    // 🟢 Kolay (Bol açık renk, risksiz akış)
            Medium = 1,  // 🟡 Dengeli (Standart bulmaca deneyimi)
            Hard = 2     // 🔴 Zor (Taktiksel katman kilitleri, yüksek slot baskısı)
        }
        private WagonDifficultyMode m_WagonDifficulty = WagonDifficultyMode.Medium;

        public enum DetailTab
        {
            LevelSetup = 0,    // 📋 Bölüm & Izgara
            ColorStudio = 1,   // 🎨 Piksel Renkleri & TCP2 Toon
            TruckLayout = 2,   // 🚚 Vagon & Ray Düzeni
            SceneTools = 3     // 🛠️ Sahne & Görsel Araçları
        }

        private DetailTab m_CurrentTab = DetailTab.LevelSetup;
        private Color m_PreviewBlockColor = new Color32(230, 40, 40, 255);

        // Menüde sadece Level Designer kalacak şekilde tek MenuItem bırakıldı
        [MenuItem("Tools/PixelGame/🛠️ Level Designer (Bölüm Tasarımcısı)", priority = 1)]
        [MenuItem("Window/PixelGame/Level Designer")]
        public static void OpenWindow()
        {
            var window = GetWindow<PixelLevelDesignerWindow>("Level Designer");
            window.minSize = new Vector2(980, 680);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshLevelList();
        }

        private void OnGUI()
        {
            // 1. Üst Başlık & Hızlı Eylem Çubuğu
            DrawTopToolbar();

            // 2. İki Sütunlu Düzen: Sol (Bölüm Listesi & Hızlı Ekleme), Sağ (Düzenleyici Paneli)
            EditorGUILayout.BeginHorizontal();

            // Sol Sütun: Seviyeler Listesi (Görsel Drop-Zone & İstatistik Rozetleri)
            DrawSidebar(330);

            // Ayırıcı çizgi
            DrawVerticalDivider();

            // Sağ Sütun: Seçili Bölüm Düzenleyici veya Sahne Araçları
            DrawDetailPanel();

            EditorGUILayout.EndHorizontal();
        }

        #region TOP TOOLBAR

        private void DrawTopToolbar()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 42);
            EditorGUI.DrawRect(rect, new Color(0.09f, 0.12f, 0.17f, 1f));

            // Sol Başlık & İkon
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.35f, 0.88f, 1f) }
            };
            GUI.Label(new Rect(rect.x + 12, rect.y, 280, rect.height), "🛠️ Pixel Game — Seviye Tasarımcısı", titleStyle);

            float rightX = rect.xMax - 10;

            // 1. Listeyi Yenile
            rightX -= 85;
            if (GUI.Button(new Rect(rightX, rect.y + 8, 80, 26), "🔄 Yenile"))
            {
                RefreshLevelList();
            }

            // 2. Yeni Bölüm Ekle
            rightX -= 105;
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.45f);
            if (GUI.Button(new Rect(rightX, rect.y + 8, 100, 26), "➕ Yeni Bölüm"))
            {
                CreateNewLevel();
            }
            GUI.backgroundColor = Color.white;

            // 3. Genel Tema Ayarları
            rightX -= 155;
            GUI.backgroundColor = new Color(0.25f, 0.75f, 1f);
            if (GUI.Button(new Rect(rightX, rect.y + 8, 150, 26), "🎨 Genel Tema Ayarları"))
            {
                GameThemeSettingsWindow.OpenWindow();
            }
            GUI.backgroundColor = Color.white;

            // 4. 9:16 Ekran Görüntüsü Al
            rightX -= 120;
            if (GUI.Button(new Rect(rightX, rect.y + 8, 115, 26), "📸 9:16 Fotoğraf"))
            {
                CaptureGameViewScreenshot.Capture();
            }

            // 5. Test Et (Play Mode) Toggle
            rightX -= 115;
            bool isPlaying = EditorApplication.isPlaying;
            GUI.backgroundColor = isPlaying ? new Color(1f, 0.4f, 0.4f) : new Color(0.3f, 0.85f, 0.5f);
            string playBtnText = isPlaying ? "⏹️ Oyunu Durdur" : "▶️ Oyunu Başlat";
            if (GUI.Button(new Rect(rightX, rect.y + 8, 110, 26), playBtnText))
            {
                EditorApplication.isPlaying = !EditorApplication.isPlaying;
            }
            GUI.backgroundColor = Color.white;

            // 6. Orta Bölüm: Hızlı Bölüm Değiştirici (◀ Seviye X / Y ▶)
            if (m_AllLevels.Count > 0 && m_SelectedLevel != null)
            {
                int currIdx = m_AllLevels.IndexOf(m_SelectedLevel);
                float navWidth = 190;
                float navX = Mathf.Max(300, (rect.x + rightX) * 0.5f - navWidth * 0.5f);

                GUI.enabled = currIdx > 0;
                if (GUI.Button(new Rect(navX, rect.y + 8, 30, 26), "◀"))
                {
                    SelectLevel(m_AllLevels[currIdx - 1]);
                }
                GUI.enabled = true;

                GUIStyle navLabel = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                    fontSize = 11
                };
                GUI.Label(new Rect(navX + 32, rect.y + 8, navWidth - 64, 26), $"Bölüm {currIdx + 1} / {m_AllLevels.Count}", navLabel);

                GUI.enabled = currIdx < m_AllLevels.Count - 1;
                if (GUI.Button(new Rect(navX + navWidth - 30, rect.y + 8, 30, 26), "▶"))
                {
                    SelectLevel(m_AllLevels[currIdx + 1]);
                }
                GUI.enabled = true;
            }
        }

        #endregion

        #region SIDEBAR (LEVEL LIST & DROP ZONE)

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

            // 📥 Görsel Sürükleyip Bırakarak Anında Seviye Üretme Kutusu (Drag & Drop Zone)
            DrawImageDropZone();

            EditorGUILayout.Space(4);

            m_SidebarScroll = EditorGUILayout.BeginScrollView(m_SidebarScroll, GUILayout.ExpandHeight(true));

            if (m_AllLevels.Count == 0)
            {
                EditorGUILayout.HelpBox("Henüz oluşturulmuş bir level yok. Yukarıdaki kutuya görsel sürükleyebilir veya 'Yeni Level Ekle'ye basabilirsiniz.", MessageType.Info);
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
                bool isBeingDragged = (m_DraggingLevel == level);

                // Seçili olana şık mavi arkaplan, sürüklenene sarı, diğerlerine hafif açık kutu
                GUI.backgroundColor = isBeingDragged ? new Color(1f, 0.85f, 0.3f) : (isSelected ? new Color(0.18f, 0.55f, 0.95f) : new Color(0.92f, 0.92f, 0.92f));

                EditorGUILayout.BeginHorizontal("box", GUILayout.Height(52));

                // 0. Sürükle-Bırak Tutamacı (satırları fare ile yeniden sıralamak için)
                Rect dragHandleRect = EditorGUILayout.GetControlRect(false, 46, GUILayout.Width(16));
                GUIStyle dragHandleStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
                };
                GUI.Label(dragHandleRect, "⠿", dragHandleStyle);
                EditorGUIUtility.AddCursorRect(dragHandleRect, MouseCursor.Pan);
                if (Event.current.type == EventType.MouseDown && dragHandleRect.Contains(Event.current.mousePosition))
                {
                    m_DraggingLevel = level;
                    Event.current.Use();
                }

                // 1. Thumbnail Önizleme
                Texture2D tex = level.GetActiveTexture();
                Rect thumbRect = EditorGUILayout.GetControlRect(false, 46, GUILayout.Width(46));
                EditorGUI.DrawRect(thumbRect, new Color(0.12f, 0.14f, 0.18f, 1f));
                if (tex != null)
                {
                    GUI.DrawTexture(thumbRect, tex, ScaleMode.ScaleToFit);
                }
                else
                {
                    GUIStyle emptyIcon = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.gray }
                    };
                    GUI.Label(thumbRect, "🖼️", emptyIcon);
                }

                // 2. Level Bilgileri & Denge Rozeti
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"#{level.LevelIndex} — {level.LevelName}", EditorStyles.boldLabel);

                Vector2Int res = level.GetGridResolution();
                int totalCubes = level.GetTotalCubeCountInPalette();
                int cap = level.GetTotalWagonCapacity();

                string stats = (tex != null) ? $"{res.x}x{res.y} | {level.ColorPalette.Count} Renk | {totalCubes} Küp" : "Görsel Atanmadı";
                EditorGUILayout.LabelField(stats, EditorStyles.miniLabel);

                // Zorluk Rozeti ve Denge Durumu
                float diffScore = CalculateLevelDifficultyScore(level);
                var (diffBadge, diffColor) = GetDifficultyBadge(diffScore);

                EditorGUILayout.BeginHorizontal();
                GUI.contentColor = diffColor;
                EditorGUILayout.LabelField($"{diffBadge} ({Mathf.RoundToInt(diffScore)}p)", EditorStyles.miniBoldLabel, GUILayout.Width(110));
                GUI.contentColor = Color.white;

                if (totalCubes > 0)
                {
                    if (cap == totalCubes)
                    {
                        GUI.contentColor = new Color(0.2f, 0.95f, 0.35f);
                        EditorGUILayout.LabelField("✓ Dengeli", EditorStyles.miniLabel);
                    }
                    else if (cap < totalCubes)
                    {
                        GUI.contentColor = new Color(1f, 0.35f, 0.35f);
                        EditorGUILayout.LabelField($"-{totalCubes - cap} küp", EditorStyles.miniLabel);
                    }
                    else
                    {
                        GUI.contentColor = new Color(0.35f, 0.75f, 1f);
                        EditorGUILayout.LabelField($"+{cap - totalCubes}", EditorStyles.miniLabel);
                    }
                    GUI.contentColor = Color.white;
                }
                else
                {
                    EditorGUILayout.LabelField("⚠️ Boş", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                // Seçim Algılama Alanı
                Rect rowRect = GUILayoutUtility.GetLastRect();
                if (m_DraggingLevel == null && Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    SelectLevel(level);
                    Event.current.Use();
                }

                // Sürükle-Bırak Bırakma Algılama: fare bu satırın üzerinde bırakılırsa, sürüklenen
                // level'i buraya taşı ve otomatik yeniden numaralandır (AutoRenumberLevels MoveLevel içinde çağrılıyor).
                if (m_DraggingLevel != null && Event.current.type == EventType.MouseUp && rowRect.Contains(Event.current.mousePosition))
                {
                    int fromIndex = m_AllLevels.IndexOf(m_DraggingLevel);
                    int toIndex = i;
                    m_DraggingLevel = null;
                    Event.current.Use();
                    if (fromIndex >= 0 && fromIndex != toIndex)
                    {
                        MoveLevel(fromIndex, toIndex);
                    }
                    return;
                }

                // 3. Hızlı Eylem Butonları: Yukarı, Aşağı, Çoğalt, Sil
                EditorGUILayout.BeginVertical(GUILayout.Width(48));
                EditorGUILayout.BeginHorizontal();

                // Yukarı Taşı
                GUI.enabled = (i > 0);
                if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(23), GUILayout.Height(18)))
                {
                    MoveLevel(i, i - 1);
                    return;
                }
                // Aşağı Taşı
                GUI.enabled = (i < m_AllLevels.Count - 1);
                if (GUILayout.Button("▼", EditorStyles.miniButtonRight, GUILayout.Width(23), GUILayout.Height(18)))
                {
                    MoveLevel(i, i + 1);
                    return;
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                // Çoğalt (Duplicate)
                GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
                if (GUILayout.Button("📋", EditorStyles.miniButtonLeft, GUILayout.Width(23), GUILayout.Height(18)))
                {
                    DuplicateLevel(level);
                    return;
                }
                // Sil (Delete)
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("🗑️", EditorStyles.miniButtonRight, GUILayout.Width(23), GUILayout.Height(18)))
                {
                    DeleteLevel(level);
                    return;
                }
                GUI.backgroundColor = isSelected ? new Color(0.18f, 0.55f, 0.95f) : Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
            }

            // Sürükleme, herhangi bir satırın üzerinde değil de boşlukta bırakılırsa takılı kalmasın.
            if (m_DraggingLevel != null && Event.current.type == EventType.MouseUp)
            {
                m_DraggingLevel = null;
                Event.current.Use();
            }
            if (m_DraggingLevel != null)
            {
                Repaint();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);

            // Alt Eylemler
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.45f);
            if (GUILayout.Button("➕ Yeni Level Ekle", GUILayout.Height(32)))
            {
                CreateNewLevel();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.85f, 0.9f, 0.95f);
            if (GUILayout.Button("🔢 Sırala (1..N)", GUILayout.Height(24)))
            {
                AutoRenumberLevels();
            }
            if (GUILayout.Button("🔄 Eşitle", GUILayout.Height(24)))
            {
                SyncWithSceneLevelManager();
                EditorUtility.DisplayDialog("Eşitleme Başarılı", "Tüm seviyeler sahnedeki LevelManager ile başarıyla eşitlendi.", "Tamam");
            }
            if (GUILayout.Button("⚡ Onar", GUILayout.Height(24)))
            {
                BatchFixAllLevels();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            GUI.backgroundColor = new Color(0.95f, 0.9f, 0.65f);
            if (GUILayout.Button("🎯 Akıllı Zorluk Sıralaması Yap (Küp × Renk × Slot)", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog(
                    "Akıllı Zorluk Sıralaması",
                    "Tüm bölümler akıllı zorluk puanına (Toplam Küp Sayısı × Renk Çeşitliliği × Ray Slotu Sayısı) göre kolaydan zora (Level 1..N) yeniden sıralanacak ve kaydedilecek.\n\nDevam edilsin mi?",
                    "Evet, Sırala", "Vazgeç"))
                {
                    SortLevelsByDifficulty();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Sürükle-bırak yöntemiyle görsellerden anında yeni seviye üreten etkileşimli alan.
        /// </summary>
        private void DrawImageDropZone()
        {
            Rect dropRect = EditorGUILayout.GetControlRect(false, 36);
            EditorGUI.DrawRect(dropRect, new Color(0.12f, 0.18f, 0.25f, 0.8f));

            GUIStyle dropStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = new Color(0.35f, 0.85f, 1f) }
            };
            GUI.Label(dropRect, "📥 Görsel Bırak (Otomatik Seviye Üret)", dropStyle);

            Event evt = Event.current;
            if (dropRect.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (Object draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject is Texture2D tex)
                            {
                                CreateLevelFromTexture(tex);
                            }
                        }
                    }
                    evt.Use();
                }
            }
        }

        #endregion

        #region DETAIL PANEL (SELECTED LEVEL & TABS)

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // Eğer Sahne Araçları sekmesi seçiliyse seviye seçilmemiş olsa bile gösterilebilir
            if (m_CurrentTab == DetailTab.SceneTools)
            {
                DrawSceneToolsTab();
                EditorGUILayout.EndVertical();
                return;
            }

            if (m_SelectedLevel == null)
            {
                EditorGUILayout.Space(40);
                GUIStyle emptyStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.gray }
                };
                EditorGUILayout.LabelField("Düzenlemek veya sahnede test etmek için\nsoldaki listeden bir level seçin veya 'Yeni Level Ekle'ye tıklayın.\n\nYa da doğrudan Sahne Araçları sekmesine geçebilirsiniz.", emptyStyle, GUILayout.Height(80));

                EditorGUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("🛠️ Sahne & Görsel Araçlarını Aç", GUILayout.Width(240), GUILayout.Height(34)))
                {
                    m_CurrentTab = DetailTab.SceneTools;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                return;
            }

            m_DetailScroll = EditorGUILayout.BeginScrollView(m_DetailScroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.Space(8);

            // 1. Üst Başlık & Hızlı Çoğalt/Sil Butonları
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"✏️ Düzenlenen: Level {m_SelectedLevel.LevelIndex} - {m_SelectedLevel.LevelName}", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
            if (GUILayout.Button("📋 Bölümü Çoğalt", GUILayout.Width(115), GUILayout.Height(24)))
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

            // 2. Sabit Hero Önizleme Kartı
            DrawHeroPreviewCard();

            EditorGUILayout.Space(6);

            // 3. Sekme Seçimi (Tabs)
            EditorGUILayout.BeginHorizontal();
            GUIStyle tabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                fixedHeight = 32
            };

            DrawTabButton(DetailTab.LevelSetup, "📋 Bölüm & Izgara", tabStyle);
            DrawTabButton(DetailTab.ColorStudio, "🎨 Piksel Renkleri & TCP2", tabStyle);
            DrawTabButton(DetailTab.TruckLayout, "🚚 Vagon & Ray Düzeni", tabStyle);
            DrawTabButton(DetailTab.SceneTools, "🛠️ Sahne & Görsel Araçları", tabStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            EditorGUI.BeginChangeCheck();

            if (m_CurrentTab == DetailTab.LevelSetup)
            {
                DrawLevelSetupTab();
            }
            else if (m_CurrentTab == DetailTab.ColorStudio)
            {
                DrawColorStudioTab();
            }
            else if (m_CurrentTab == DetailTab.TruckLayout)
            {
                DrawTruckLayoutTab();
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }

            EditorGUILayout.Space(12);

            // 4. Alt Aksiyon Butonları (İnşa Et, Kaydet, Temizle)
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

        #endregion

        #region HERO PREVIEW CARD

        private void DrawHeroPreviewCard()
        {
            if (m_SelectedLevel == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            Texture2D activeTex = m_SelectedLevel.GetActiveTexture();

            EditorGUILayout.BeginHorizontal();

            // 1. Sol: Büyük Resim Önizleme Kutusu (100x100) — Sürükle Bırak Kabul Eder
            Rect previewRect = EditorGUILayout.GetControlRect(false, 100, GUILayout.Width(100));
            EditorGUI.DrawRect(previewRect, new Color(0.1f, 0.12f, 0.16f, 1f));

            if (activeTex != null)
            {
                GUI.DrawTexture(previewRect, activeTex, ScaleMode.ScaleToFit);
            }
            else
            {
                GUIStyle emptyText = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    wordWrap = true
                };
                GUI.Label(previewRect, "🖼️\nGörsel\nSürükle", emptyText);
            }

            // Sürükle Bırak kontrolü (Görseli bu kutuya bırakıp değiştirebilir)
            Event evt = Event.current;
            if (previewRect.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (Object dragged in DragAndDrop.objectReferences)
                        {
                            if (dragged is Texture2D newT)
                            {
                                m_SelectedLevel.LevelTexture = newT;
                                EnsureTextureReadable(newT);
                                m_SelectedLevel.ExtractPaletteFromTexture();
                                EditorUtility.SetDirty(m_SelectedLevel);
                                NotifyLiveSceneUpdate();
                            }
                        }
                    }
                    evt.Use();
                }
            }

            GUILayout.Space(10);

            // 2. Sağ: Bilgiler & Görsel Değiştirici
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.25f, 0.85f, 1f) }
            };
            EditorGUILayout.LabelField($"Level {m_SelectedLevel.LevelIndex}: {m_SelectedLevel.LevelName}", nameStyle);

            // Hızlı Sahnede İnşa Butonu
            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
            if (GUILayout.Button("▶️ Sahnede Yükle", GUILayout.Width(125), GUILayout.Height(22)))
            {
                BuildSelectedLevelInScene();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // Kaynak Görsel Seçici
            Texture2D newTex = (Texture2D)EditorGUILayout.ObjectField("Kaynak Piksel Görseli", m_SelectedLevel.LevelTexture, typeof(Texture2D), false);
            if (newTex != m_SelectedLevel.LevelTexture)
            {
                m_SelectedLevel.LevelTexture = newTex;
                if (newTex != null)
                {
                    EnsureTextureReadable(newTex);
                    m_SelectedLevel.ExtractPaletteFromTexture();
                }
            }

            if (activeTex != null)
            {
                Vector2Int res = m_SelectedLevel.GetGridResolution();
                int cubes = activeTex.width * activeTex.height;
                EditorGUILayout.LabelField($"📐 Görsel: {activeTex.width} x {activeTex.height} Piksel  |  Izgara: {res.x} x {res.y}  |  Küp: {cubes} adet  |  Palet: {m_SelectedLevel.ColorPalette.Count} Renk", EditorStyles.miniBoldLabel);

                string path = AssetDatabase.GetAssetPath(activeTex);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && !importer.isReadable)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.HelpBox("⚠️ Görsel 'Read/Write' iznine sahip değil (Pikseller okunamaz).", MessageType.Warning);
                    if (GUILayout.Button("🔧 Okunabilir Yap", GUILayout.Width(130), GUILayout.Height(24)))
                    {
                        EnsureTextureReadable(activeTex);
                        m_SelectedLevel.ExtractPaletteFromTexture();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField("✅ Görsel piksel okumaya ve küp üretmeye hazır.", EditorStyles.miniLabel);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Lütfen bu levelde çizilecek piksel görselini yukarıdaki kutucuğa sürükleyin.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region TAB 0: LEVEL SETUP & GRID

        private void DrawLevelSetupTab()
        {
            // 1. Temel Bilgiler
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📋 Genel Bilgiler", EditorStyles.boldLabel);
            m_SelectedLevel.LevelName = EditorGUILayout.TextField("Level Adı", m_SelectedLevel.LevelName);
            m_SelectedLevel.LevelIndex = EditorGUILayout.IntField("Level Numarası", m_SelectedLevel.LevelIndex);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 2. Izgara & 3B Küp Ayarları
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📐 Piksel Uyumu & 3D Izgara Düzeni", EditorStyles.boldLabel);

            m_SelectedLevel.UseNativeResolution = EditorGUILayout.Toggle(
                new GUIContent("1:1 Doğal Piksel Boyutu (Önerilen)", "Görselin kendi piksel çözünürlüğünü korur, basıklık ve bozulmayı engeller."), 
                m_SelectedLevel.UseNativeResolution
            );

            if (!m_SelectedLevel.UseNativeResolution)
            {
                m_SelectedLevel.CustomResolution = EditorGUILayout.Vector2IntField("Özel Çözünürlük (X, Y)", m_SelectedLevel.CustomResolution);

                Texture2D activeTexForRes = m_SelectedLevel.GetActiveTexture();
                if (activeTexForRes != null &&
                    (activeTexForRes.width != m_SelectedLevel.CustomResolution.x || activeTexForRes.height != m_SelectedLevel.CustomResolution.y))
                {
                    EditorGUILayout.HelpBox(
                        $"⚠️ Özel çözünürlük ({m_SelectedLevel.CustomResolution.x}x{m_SelectedLevel.CustomResolution.y}) görselin gerçek boyutuyla " +
                        $"({activeTexForRes.width}x{activeTexForRes.height}) uyuşmuyor. Izgara boyutu doku boyutuna tam eşit olmadığında " +
                        "piksel örnekleme (resample) devreye girer ve şekil kenarlarında basamaklanma/bozulma oluşabilir. " +
                        "Düzeltmek için 'Özel Çözünürlük'ü görselle aynı yap ya da '1:1 Doğal Piksel Boyutu'nu aç.",
                        MessageType.Warning);
                }
            }

            m_SelectedLevel.CubeSpacing = EditorGUILayout.Slider("Küp Boşluğu (Dikey / Y)", m_SelectedLevel.CubeSpacing, -0.1f, 0.25f);
            m_SelectedLevel.CubeSpacingX = EditorGUILayout.Slider("Küp Boşluğu (Yatay / X)", m_SelectedLevel.CubeSpacingX, -0.1f, 0.25f);
            m_SelectedLevel.CubeDepth = EditorGUILayout.Slider("Küp 3D Derinliği (Thickness)", m_SelectedLevel.CubeDepth, 0.05f, 1.5f);
            m_SelectedLevel.BoardTiltAngle = EditorGUILayout.Slider("Pano Eğim Açısı (Board Tilt)", m_SelectedLevel.BoardTiltAngle, -45f, 45f);
            m_SelectedLevel.CubeFrontTiltAngle = EditorGUILayout.Slider("Küp Ön Yüz Eğim Açısı", m_SelectedLevel.CubeFrontTiltAngle, -180f, 180f);
            m_SelectedLevel.InnerPadding = EditorGUILayout.Slider("Mavi Çerçeve Payı", m_SelectedLevel.InnerPadding, 0f, 0.25f);
            m_SelectedLevel.SkipTransparent = EditorGUILayout.Toggle("Şeffaf Pikselleri Atla", m_SelectedLevel.SkipTransparent);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 3. Bölüm İstatistik & Zorluk Değerlendirmesi
            DrawLevelDifficultyStatsCard();
        }

        private void DrawLevelDifficultyStatsCard()
        {
            if (m_SelectedLevel == null) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("📊 Bölüm Zorluk & Oynanış İstatistikleri", EditorStyles.boldLabel);

            int totalCubes = m_SelectedLevel.GetTotalCubeCountInPalette();
            int colorCount = m_SelectedLevel.ColorPalette != null ? m_SelectedLevel.ColorPalette.Count : 0;
            int reqWagons = m_SelectedLevel.GetRequiredTruckCount();

            float diffScore = CalculateLevelDifficultyScore(m_SelectedLevel);
            var (diffBadge, diffColor) = GetDifficultyBadge(diffScore);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Toplam Küp: {totalCubes}", EditorStyles.boldLabel, GUILayout.Width(130));
            EditorGUILayout.LabelField($"Renk: {colorCount}", EditorStyles.boldLabel, GUILayout.Width(90));
            EditorGUILayout.LabelField($"Ray Slotu: {m_SelectedLevel.SlotCount}", EditorStyles.boldLabel, GUILayout.Width(95));
            EditorGUILayout.LabelField($"Vagon: ~{reqWagons}", EditorStyles.boldLabel, GUILayout.Width(100));

            GUI.contentColor = diffColor;
            EditorGUILayout.LabelField($"Zorluk: {diffBadge} ({Mathf.RoundToInt(diffScore)}p)", EditorStyles.boldLabel);
            GUI.contentColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            string advice = (diffScore < 250f) ? "💡 Yeni başlayanlar ve casual oyuncular için ideal rahatlıkta bir seviye." :
                            (diffScore < 550f) ? "💡 Standart dengeli bölüm. Oyuncu hafif planlama yaparak keyifle çözebilir." :
                            (diffScore < 950f) ? "⚠️ Zorlayıcı bölüm! Renk çeşitliliği ve slot kısıtlaması nedeniyle vagon sırasının kilitlenmemesi için dikkatli tasarlanmalıdır." :
                            "🔥 Uzman/Boss seviyesi! Çok yüksek küp sayısı ve renk çeşitliliği içerir.";
            EditorGUILayout.HelpBox(advice, MessageType.None);

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region TAB 1: COLOR STUDIO & TOONY COLORS PRO

        private void DrawColorStudioTab()
        {
            // 1. Bölüm Renk Paleti ve Recolor (Renk Değiştirme)
            DrawColorPaletteSection();

            EditorGUILayout.Space(6);

            // 2. Genel Işık & Renk Ayarları (Parlaklık, Doygunluk, Kontrast)
            DrawGlobalColorSettingsSection();

            EditorGUILayout.Space(6);

            // 3. Toony Colors Pro (Toon Görünüm & Gölgelendirme) Entegrasyonu
            DrawToonyColorsProSection();

            EditorGUILayout.Space(6);

            // 4. Vagon & Madenci Teması
            DrawWagonThemeOverrideSection();
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
            EditorGUILayout.HelpBox("Görseldeki herhangi bir rengi değiştirmek için sağdaki renk kutucuğuna tıklayın. Örneğin kürk rengini maviye, arka planı mora dönüştürebilirsiniz!", MessageType.None);
            EditorGUILayout.Space(4);

            // Toplu Renk Hazır Ayarları (Palette Presets)
            EditorGUILayout.LabelField("Hızlı Palet Dönüşümleri (Palette Presets):", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("🌈 Neon Cyber"))
            {
                ApplyPalettePreset(PresetType.Neon);
            }
            if (GUILayout.Button("🍬 Şeker Pastel"))
            {
                ApplyPalettePreset(PresetType.Pastel);
            }
            if (GUILayout.Button("🌅 Günbatımı Sıcak"))
            {
                ApplyPalettePreset(PresetType.Sunset);
            }
            if (GUILayout.Button("🌲 Doğa Mint"))
            {
                ApplyPalettePreset(PresetType.Forest);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

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

                    // 4. Tek Renk Sıfırlama Butonu
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

        private enum PresetType { Neon, Pastel, Sunset, Forest }

        private void ApplyPalettePreset(PresetType type)
        {
            if (m_SelectedLevel == null || m_SelectedLevel.ColorPalette == null) return;
            Undo.RecordObject(m_SelectedLevel, "Apply Palette Preset");

            foreach (var item in m_SelectedLevel.ColorPalette)
            {
                Color.RGBToHSV(item.originalColor, out float h, out float s, out float v);
                switch (type)
                {
                    case PresetType.Neon:
                        s = Mathf.Clamp01(s * 1.5f + 0.2f);
                        v = Mathf.Clamp01(v * 1.3f + 0.1f);
                        break;
                    case PresetType.Pastel:
                        s = Mathf.Clamp01(s * 0.45f);
                        v = Mathf.Clamp01(v * 0.85f + 0.2f);
                        break;
                    case PresetType.Sunset:
                        h = Mathf.Lerp(0.02f, 0.12f, h); // Kırmızı-turuncu-sarı spektrumu
                        s = Mathf.Clamp01(s * 1.2f);
                        break;
                    case PresetType.Forest:
                        h = Mathf.Lerp(0.28f, 0.45f, h); // Yeşil-mint spektrumu
                        s = Mathf.Clamp01(s * 1.1f);
                        break;
                }
                item.targetColor = Color.HSVToRGB(h, s, v);
            }

            EditorUtility.SetDirty(m_SelectedLevel);
            NotifyLiveSceneUpdate();
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

        private void DrawWagonThemeOverrideSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🎨 Vagon, Madenci & Ray Teması", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.25f, 0.75f, 1f);
            if (GUILayout.Button("🎨 Genel Tema Ayarlarını Aç", GUILayout.Width(190), GUILayout.Height(24)))
            {
                GameThemeSettingsWindow.OpenWindow();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Vagon, madenci ve çevre renkleri tüm oyun genelinde tek merkezden (GameThemeSettings) yönetilir.", EditorStyles.miniLabel);

            m_SelectedLevel.UseCustomColorTheme = EditorGUILayout.ToggleLeft("⚠️ Bu bölüme özel tema tanımla (Global Temayı Ez)", m_SelectedLevel.UseCustomColorTheme, EditorStyles.boldLabel);
            if (m_SelectedLevel.UseCustomColorTheme)
            {
                EditorGUILayout.HelpBox("Bu bölüme özel tema aktif. Aşağıdaki renkler sadece bu level için geçerli olacaktır.", MessageType.Warning);
                DrawThemeColorSection();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawThemeColorSection()
        {
            if (m_SelectedLevel == null) return;
            LevelColorTheme theme = m_SelectedLevel.ColorTheme;
            if (theme == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.2f, 0.8f, 1f) }
            };
            EditorGUILayout.LabelField("🎨 Parça Renkleri", headerStyle);

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

            EditorGUILayout.Space(6);
            DrawTestBlockColorBar();
            EditorGUILayout.Space(8);

            DrawPartGroupHeader("🚚 Vagon (MineCart / ToyTruck) Parçaları");
            DrawPartColorRow(theme, TruckPart.Cabin, "Kabin (Kabin + Kaput)", "🚛");
            DrawPartColorRow(theme, TruckPart.Cargo, "Kasa (Cargo + Arka Kapak)", "📦");
            DrawPartColorRow(theme, TruckPart.Rims, "Jantlar (Rims)", "⚙️");
            DrawPartColorRow(theme, TruckPart.Tires, "Tekerlekler (Tires)", "🛞");

            EditorGUILayout.Space(6);
            DrawPartGroupHeader("⛏️ Madenci (MechaMiner) Parçaları");
            DrawPartColorRow(theme, TruckPart.MechaBody, "Karakter Gövdesi", "🤖");
            DrawPartColorRow(theme, TruckPart.Helmet, "Baret", "⛑️");

            EditorGUILayout.EndVertical();
        }

        private void DrawTestBlockColorBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("🧪 Test Rengi:", EditorStyles.boldLabel, GUILayout.Width(110));

            Color[] quickColors = new Color[]
            {
                new Color32(230, 40, 40, 255),
                new Color32(40, 110, 235, 255),
                new Color32(255, 196, 30, 255),
                new Color32(60, 190, 80, 255),
                new Color32(150, 70, 220, 255),
                new Color32(255, 128, 30, 255)
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
            Color newTest = EditorGUILayout.ColorField(GUIContent.none, m_PreviewBlockColor, false, false, false, GUILayout.Width(60));
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
            Rect r = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.DrawRect(r, new Color(0.18f, 0.22f, 0.28f, 1f));
            GUIStyle st = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.4f, 0.85f, 1f) },
                alignment = TextAnchor.MiddleLeft
            };
            GUI.Label(new Rect(r.x + 8, r.y + 1, r.width - 16, r.height), title, st);
        }

        private void DrawPartColorRow(LevelColorTheme theme, TruckPart part, string displayName, string icon)
        {
            TruckPartColorSetting setting = theme.GetSetting(part);
            Color resolvedColor = theme.ResolveColor(part, m_PreviewBlockColor);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"{icon} {displayName}", GUILayout.Width(200));

            GUI.backgroundColor = setting.matchBlockColor ? new Color(0.25f, 0.85f, 0.45f) : new Color(0.85f, 0.85f, 0.88f);
            string btnText = setting.matchBlockColor ? "✓ Vagon Rengini Kullan" : "  Vagon Rengini Kullan";
            if (GUILayout.Button(btnText, GUILayout.Width(165), GUILayout.Height(20)))
            {
                setting.matchBlockColor = !setting.matchBlockColor;
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(8);

            if (setting.matchBlockColor)
            {
                Rect badgeRect = EditorGUILayout.GetControlRect(false, 18, GUILayout.Width(80));
                EditorGUI.DrawRect(badgeRect, resolvedColor);
                GUIStyle badgeText = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = (resolvedColor.grayscale > 0.5f) ? Color.black : Color.white }
                };
                GUI.Label(badgeRect, "⚡ Dinamik", badgeText);
            }
            else
            {
                Color newCol = EditorGUILayout.ColorField(GUIContent.none, setting.customColor, false, false, false, GUILayout.Width(80));
                if (newCol != setting.customColor)
                {
                    setting.customColor = newCol;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region TAB 2: TRUCK LAYOUT & 2D MATRIX

        private void DrawTruckLayoutTab()
        {
            if (m_SelectedLevel == null) return;

            // Havuz ve Izgara senkronizasyonu
            if (m_GridColumnsPerRow <= 0) m_GridColumnsPerRow = Mathf.Clamp(m_SelectedLevel.PoolColumns, 1, 8);
            if (m_TargetGridRows <= 0) m_TargetGridRows = Mathf.Max(1, m_SelectedLevel.PoolRows);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 1. Üst Kontrol & Denge Şeridi
            DrawSmartGridTopBar();

            EditorGUILayout.Space(4);

            // 2. Renk Paleti & Fırça Şeridi
            DrawSmartGridPaletteBar();

            EditorGUILayout.Space(6);

            // 3. 2D Görsel Izgara Tahtası (The Visual Grid Canvas)
            DrawVisualGridBoard();

            EditorGUILayout.EndVertical();
        }

        private void DrawSmartGridTopBar()
        {
            int totalCubes = m_SelectedLevel.GetTotalCubeCountInPalette();
            int totalCap = m_SelectedLevel.GetTotalWagonCapacity();
            int diff = totalCubes - totalCap;

            // 1. Satır: Başlık + Manuel Aç/Kapa + Zorluk Seçici + Akıllı Sırala + Temizle
            EditorGUILayout.BeginHorizontal();

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.2f, 0.85f, 1f) }
            };
            EditorGUILayout.LabelField("🚚 Vagon & Havuz Matris Tasarımcısı", titleStyle, GUILayout.Width(235));

            EditorGUI.BeginChangeCheck();
            bool manual = EditorGUILayout.ToggleLeft("⚡ Manuel Sıra", m_SelectedLevel.UseCustomWagonSequence, EditorStyles.boldLabel, GUILayout.Width(115));
            if (EditorGUI.EndChangeCheck())
            {
                m_SelectedLevel.UseCustomWagonSequence = manual;
                if (manual && (m_SelectedLevel.WagonSequence == null || m_SelectedLevel.WagonSequence.Count == 0))
                {
                    AutoDistributeSmartPuzzle();
                }
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }

            GUILayout.FlexibleSpace();

            // Zorluk Modu Seçici
            GUILayout.Label("Zorluk:", EditorStyles.miniBoldLabel, GUILayout.Width(45));

            GUI.backgroundColor = (m_WagonDifficulty == WagonDifficultyMode.Easy) ? new Color(0.25f, 0.95f, 0.45f) : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button(new GUIContent("🟢 Kolay", "Açık dış renkler ön dalgalara yerleşir, gömülü renkler ertelenir. Sıfır kilitlenme riski."), EditorStyles.miniButtonLeft, GUILayout.Width(68), GUILayout.Height(24)))
            {
                m_WagonDifficulty = WagonDifficultyMode.Easy;
            }

            GUI.backgroundColor = (m_WagonDifficulty == WagonDifficultyMode.Medium) ? new Color(0.98f, 0.85f, 0.25f) : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button(new GUIContent("🟡 Dengeli", "Açık renkler ilk dalgaya yayılır, ardından ardışık dengeli vagon akışı oluşturulur."), EditorStyles.miniButtonMid, GUILayout.Width(72), GUILayout.Height(24)))
            {
                m_WagonDifficulty = WagonDifficultyMode.Medium;
            }

            GUI.backgroundColor = (m_WagonDifficulty == WagonDifficultyMode.Hard) ? new Color(1f, 0.45f, 0.4f) : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button(new GUIContent("🔴 Zor", "Gömülü renkler erkenden ray slotuna sokulur. Taktiksel slot yönetimi ve kilit çözme gerektirir."), EditorStyles.miniButtonRight, GUILayout.Width(62), GUILayout.Height(24)))
            {
                m_WagonDifficulty = WagonDifficultyMode.Hard;
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(6);

            // 🧠 Akıllı Sırala Butonu (Zorluk moduna duyarlı)
            string btnText = m_WagonDifficulty == WagonDifficultyMode.Easy ? "🧠 Kolay Sırala" :
                             m_WagonDifficulty == WagonDifficultyMode.Medium ? "🧠 Dengeli Sırala" : "🧠 Zor Sırala";
            string btnTooltip = m_WagonDifficulty == WagonDifficultyMode.Easy 
                ? "Dış yüzeydeki açık renkleri ön dalgalara yerleştirir, gömülü renkleri sonraya bırakır. Sıfır kilitlenme riski!"
                : (m_WagonDifficulty == WagonDifficultyMode.Medium 
                    ? "Dış renkleri ilk dalgaya yayar ve ardışık dengeli vagon akışı oluşturur."
                    : "Gömülü renkleri erkenden sokarak ray slotlarında taktiksel baskı ve kilit mücadelesi oluşturur!");

            Color btnColor = m_WagonDifficulty == WagonDifficultyMode.Easy ? new Color(0.2f, 0.88f, 0.45f) :
                             m_WagonDifficulty == WagonDifficultyMode.Medium ? new Color(0.95f, 0.82f, 0.2f) : new Color(1f, 0.5f, 0.45f);

            GUI.backgroundColor = btnColor;
            if (GUILayout.Button(new GUIContent(btnText, btnTooltip), GUILayout.Height(24), GUILayout.Width(125)))
            {
                GenerateSmartSequenceFromSceneCubes();
            }

            // ⚡ Eksikleri Tamamla (Eğer eksik varsa)
            if (diff > 0)
            {
                GUI.backgroundColor = new Color(0.95f, 0.7f, 0.15f);
                if (GUILayout.Button(new GUIContent($"⚡ Tamamla (+{diff})", "Eksik kalan küpler için vagonları otomatik ekler"), GUILayout.Height(24), GUILayout.Width(105)))
                {
                    AutoBalanceMissingWagons();
                }
            }

            // 🧹 Temizle
            GUI.backgroundColor = new Color(0.95f, 0.35f, 0.35f);
            if (GUILayout.Button(new GUIContent("🧹 Temizle", "Tüm ızgarayı sıfırlar"), GUILayout.Height(24), GUILayout.Width(68)))
            {
                if (EditorUtility.DisplayDialog("Izgarayı Temizle", "Tüm vagon sırasını silmek istediğinize emin misiniz?", "Evet", "Hayır"))
                {
                    Undo.RecordObject(m_SelectedLevel, "Clear Wagons");
                    if (m_SelectedLevel.WagonSequence != null) m_SelectedLevel.WagonSequence.Clear();
                    m_SelectedSlotForSwap = -1;
                    m_ActiveBrushPaletteIndex = -1;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // 2. Satır: Boyutlar & Ayarlar Stepper Şeridi (Tek Kompakt Toolbar)
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("📐 Havuz/Izgara:", EditorStyles.miniBoldLabel, GUILayout.Width(92));

            GUILayout.Label("Sıra:", GUILayout.Width(28));
            if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(18)))
            {
                m_TargetGridRows = Mathf.Max(1, m_TargetGridRows - 1);
                m_SelectedLevel.PoolRows = m_TargetGridRows;
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }
            m_TargetGridRows = EditorGUILayout.IntField(m_TargetGridRows, GUILayout.Width(26));
            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(18)))
            {
                m_TargetGridRows = Mathf.Min(12, m_TargetGridRows + 1);
                m_SelectedLevel.PoolRows = m_TargetGridRows;
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }

            GUILayout.Space(6);

            GUILayout.Label("Kolon:", GUILayout.Width(38));
            if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(18)))
            {
                m_GridColumnsPerRow = Mathf.Max(1, m_GridColumnsPerRow - 1);
                m_SelectedLevel.PoolColumns = m_GridColumnsPerRow;
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }
            m_GridColumnsPerRow = EditorGUILayout.IntField(m_GridColumnsPerRow, GUILayout.Width(26));
            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(18)))
            {
                m_GridColumnsPerRow = Mathf.Min(8, m_GridColumnsPerRow + 1);
                m_SelectedLevel.PoolColumns = m_GridColumnsPerRow;
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }

            GUILayout.Space(10);

            GUILayout.Label("📦 Vagon Kapasitesi:", EditorStyles.miniBoldLabel, GUILayout.Width(115));
            if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(18)))
            {
                m_SelectedLevel.TruckCapacity = Mathf.Max(1, m_SelectedLevel.TruckCapacity - 1);
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }
            m_SelectedLevel.TruckCapacity = EditorGUILayout.IntField(m_SelectedLevel.TruckCapacity, GUILayout.Width(30));
            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(18)))
            {
                m_SelectedLevel.TruckCapacity = Mathf.Min(64, m_SelectedLevel.TruckCapacity + 1);
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }

            GUILayout.Space(10);

            GUILayout.Label("🛤️ Ray Slotu:", EditorStyles.miniBoldLabel, GUILayout.Width(72));
            if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(18)))
            {
                m_SelectedLevel.SlotCount = Mathf.Max(1, m_SelectedLevel.SlotCount - 1);
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }
            m_SelectedLevel.SlotCount = EditorGUILayout.IntField(m_SelectedLevel.SlotCount, GUILayout.Width(26));
            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(18)))
            {
                m_SelectedLevel.SlotCount = Mathf.Min(8, m_SelectedLevel.SlotCount + 1);
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }

            GUILayout.FlexibleSpace();

            // Denge Rozeti
            if (diff == 0 && totalCubes > 0)
            {
                GUI.backgroundColor = new Color(0.2f, 0.9f, 0.4f);
                GUILayout.Label($"🟢 %100 DENGELİ ({totalCap}/{totalCubes})", EditorStyles.miniBoldLabel);
            }
            else if (diff > 0)
            {
                GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                GUILayout.Label($"🔴 {diff} KÜP EKSİK ({totalCap}/{totalCubes})", EditorStyles.miniBoldLabel);
            }
            else
            {
                GUI.backgroundColor = new Color(0.3f, 0.75f, 1f);
                GUILayout.Label($"🔵 +{-diff} FAZLA ({totalCap}/{totalCubes})", EditorStyles.miniBoldLabel);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(m_LastSmartStatusMessage))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(m_LastSmartStatusMessage, MessageType.Info);
            }
        }

        private void DrawSmartGridPaletteBar()
        {
            if (m_SelectedLevel.ColorPalette == null || m_SelectedLevel.ColorPalette.Count == 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("🎨 Palet & Fırça:", EditorStyles.boldLabel, GUILayout.Width(95));

            for (int i = 0; i < m_SelectedLevel.ColorPalette.Count; i++)
            {
                var entry = m_SelectedLevel.ColorPalette[i];
                if (entry == null || entry.pixelCount <= 0) continue;
                Color c = entry.targetColor;
                string cName = GetColorDisplayName(c, entry.label, i);
                int assigned = m_SelectedLevel.GetTotalAssignedCapacityForColor(c);
                int diff = entry.pixelCount - assigned;
                bool isSelected = (m_ActiveBrushPaletteIndex == i);

                // Renk Kartı Butonu
                GUI.backgroundColor = isSelected ? new Color(1f, 0.85f, 0.2f) : (diff > 0 ? new Color(1f, 0.92f, 0.92f) : new Color(0.92f, 1f, 0.92f));

                string badge = diff == 0 ? "✓" : (diff > 0 ? $"-{diff}" : $"+{-diff}");
                string btnLabel = isSelected ? $"🖌️ {cName} ({assigned}/{entry.pixelCount}) [{badge}]" : $"{cName} ({assigned}/{entry.pixelCount}) [{badge}]";

                EditorGUILayout.BeginHorizontal("box");
                Rect swatch = EditorGUILayout.GetControlRect(false, 16, GUILayout.Width(16));
                EditorGUI.DrawRect(swatch, c);

                if (GUILayout.Button(btnLabel, EditorStyles.miniButton, GUILayout.Height(18)))
                {
                    m_ActiveBrushPaletteIndex = isSelected ? -1 : i;
                    m_SelectedSlotForSwap = -1;
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            GUI.backgroundColor = Color.white;

            GUILayout.Space(6);

            // Silgi Butonu
            bool isEraser = (m_ActiveBrushPaletteIndex == -2);
            GUI.backgroundColor = isEraser ? new Color(1f, 0.4f, 0.4f) : Color.white;
            if (GUILayout.Button(isEraser ? "🧹 Silgi Aktif" : "🧹 Silgi", EditorStyles.miniButton, GUILayout.Width(75), GUILayout.Height(20)))
            {
                m_ActiveBrushPaletteIndex = isEraser ? -1 : -2;
                m_SelectedSlotForSwap = -1;
            }

            // Seç / Takas (Swap) Butonu
            bool isSwapMode = (m_ActiveBrushPaletteIndex == -1);
            GUI.backgroundColor = isSwapMode ? new Color(0.7f, 0.85f, 1f) : Color.white;
            if (GUILayout.Button("✋ Takas / Taşı", EditorStyles.miniButton, GUILayout.Width(85), GUILayout.Height(20)))
            {
                m_ActiveBrushPaletteIndex = -1;
            }
            GUI.backgroundColor = Color.white;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Aktif Mod İpuçları
            if (m_ActiveBrushPaletteIndex >= 0 && m_ActiveBrushPaletteIndex < m_SelectedLevel.ColorPalette.Count)
            {
                var brushEntry = m_SelectedLevel.ColorPalette[m_ActiveBrushPaletteIndex];
                string bName = GetColorDisplayName(brushEntry.targetColor, brushEntry.label, m_ActiveBrushPaletteIndex);
                EditorGUILayout.HelpBox($"🖌️ Fırça Aktif: '{bName}'. Aşağıdaki ızgara slotlarına tıklayarak bu rengi tek tıkla yerleştirebilirsiniz.", MessageType.Info);
            }
            else if (m_ActiveBrushPaletteIndex == -2)
            {
                EditorGUILayout.HelpBox("🧹 Silgi Aktif: Izgaradaki herhangi bir vagon slotuna tıklayarak onu anında boşaltabilirsiniz.", MessageType.Warning);
            }
            else if (m_SelectedSlotForSwap >= 0)
            {
                EditorGUILayout.HelpBox($"🔄 Slot #{m_SelectedSlotForSwap + 1} seçildi! Yerini değiştirmek istediğiniz başka bir slota tıklayın.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private string GetColorDisplayName(Color c, string existingLabel, int fallbackIndex)
        {
            if (!string.IsNullOrEmpty(existingLabel) && 
                existingLabel != "Renk" && 
                !existingLabel.StartsWith("Renk #") &&
                !existingLabel.StartsWith("Vagon #"))
            {
                return existingLabel;
            }

            Color.RGBToHSV(c, out float h, out float s, out float v);
            string name;

            if (v < 0.18f) name = "Siyah";
            else if (s < 0.12f && v > 0.85f) name = "Beyaz";
            else if (s < 0.15f) name = "Gri";
            else
            {
                float deg = h * 360f;
                if (deg < 15f || deg >= 345f) name = "Kırmızı";
                else if (deg < 42f) name = "Turuncu";
                else if (deg < 68f) name = "Sarı";
                else if (deg < 155f) name = "Yeşil";
                else if (deg < 195f) name = "Turkuaz";
                else if (deg < 255f) name = "Mavi";
                else if (deg < 285f) name = "Mor";
                else if (deg < 320f) name = "Magenta";
                else name = "Pembe";

                if (s > 0.2f && v > 0.75f && (name == "Kırmızı" || name == "Magenta" || name == "Pembe"))
                {
                    if (v > 0.88f && s < 0.45f) name = "Açık Pembe";
                    else if (s > 0.6f && deg > 315f) name = "Fuşya";
                    else name = "Pembe";
                }
            }

            return $"{name} (#{fallbackIndex + 1})";
        }

        private int GetTotalCubeCountForColor(Color targetColor, float threshold = 0.08f)
        {
            if (m_SelectedLevel == null || m_SelectedLevel.ColorPalette == null) return 0;
            foreach (var entry in m_SelectedLevel.ColorPalette)
            {
                if (entry != null && PaletteColorOverride.ColorsMatch(entry.targetColor, targetColor, threshold))
                {
                    return entry.pixelCount;
                }
            }
            return 0;
        }

        private void AutoBalanceMissingWagons()
        {
            if (m_SelectedLevel == null || m_SelectedLevel.ColorPalette == null) return;
            Undo.RecordObject(m_SelectedLevel, "Auto Balance Missing Wagons");

            if (m_SelectedLevel.WagonSequence == null)
            {
                m_SelectedLevel.WagonSequence = new List<WagonSequenceEntry>();
            }

            int capPerWagon = Mathf.Max(1, m_SelectedLevel.TruckCapacity);

            for (int i = 0; i < m_SelectedLevel.ColorPalette.Count; i++)
            {
                var entry = m_SelectedLevel.ColorPalette[i];
                if (entry == null || entry.pixelCount <= 0) continue;
                Color c = entry.targetColor;
                string cName = GetColorDisplayName(c, entry.label, i);
                int assigned = m_SelectedLevel.GetTotalAssignedCapacityForColor(c);
                int diff = entry.pixelCount - assigned;

                while (diff > 0)
                {
                    int thisCap = Mathf.Min(capPerWagon, diff);
                    m_SelectedLevel.WagonSequence.Add(new WagonSequenceEntry(c, thisCap, i, $"{cName} (#{m_SelectedLevel.WagonSequence.Count + 1})"));
                    diff -= thisCap;
                }
            }

            m_SelectedLevel.UseCustomWagonSequence = true;
            EditorUtility.SetDirty(m_SelectedLevel);
            NotifyLiveSceneUpdate();
        }

        private void DrawVisualGridBoard()
        {
            if (m_SelectedLevel == null) return;
            if (m_SelectedLevel.WagonSequence == null) m_SelectedLevel.WagonSequence = new List<WagonSequenceEntry>();

            var sequence = m_SelectedLevel.WagonSequence;
            int cols = Mathf.Clamp(m_GridColumnsPerRow, 1, 8);
            int rows = Mathf.Max(m_TargetGridRows, Mathf.CeilToInt((float)sequence.Count / cols), 1);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Bilgilendirme Çubuğu
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"🎛️ 2D Izgara Matrisi — {rows} Dalga (Sıra) × {cols} Kolon (Toplam {rows * cols} Slot)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("🔗 Havuz Boyutlarını Bu Izgaraya Eşitle", EditorStyles.miniButton, GUILayout.Width(220)))
            {
                Undo.RecordObject(m_SelectedLevel, "Sync Pool to Grid");
                m_SelectedLevel.PoolRows = rows;
                m_SelectedLevel.PoolColumns = cols;
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Izgara Satırları (Her satır bir dalga)
            for (int r = 0; r < rows; r++)
            {
                int startIdx = r * cols;

                EditorGUILayout.BeginVertical("box");

                // Satır Başlığı ve Kontrolleri
                EditorGUILayout.BeginHorizontal();

                string waveLabel = (r == 0) ? "📦 Dalga #1 (Ön Sıra - Başlangıç)" : $"📦 Dalga #{r + 1}";
                GUIStyle rowHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 11,
                    normal = { textColor = (r == 0) ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 0.85f, 1f) }
                };
                EditorGUILayout.LabelField(waveLabel, rowHeaderStyle, GUILayout.Width(230));

                GUILayout.FlexibleSpace();

                // Yukarı Taşı
                GUI.enabled = r > 0;
                if (GUILayout.Button("▲ Yukarı", EditorStyles.miniButton, GUILayout.Width(58), GUILayout.Height(18)))
                {
                    MoveWagonRow(r, r - 1, cols);
                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                // Aşağı Taşı
                GUI.enabled = r < rows - 1;
                if (GUILayout.Button("▼ Aşağı", EditorStyles.miniButton, GUILayout.Width(58), GUILayout.Height(18)))
                {
                    MoveWagonRow(r, r + 1, cols);
                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                GUI.enabled = true;

                // Boşlukları Doldur
                if (GUILayout.Button("➕ Doldur", EditorStyles.miniButton, GUILayout.Width(58), GUILayout.Height(18)))
                {
                    FillRowSlots(r, cols);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                // Satırı Sil
                GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
                if (GUILayout.Button("🗑️ Sil", EditorStyles.miniButton, GUILayout.Width(45), GUILayout.Height(18)))
                {
                    DeleteWagonRow(r, cols);
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);

                // Slot Kartları (Yan Yana)
                EditorGUILayout.BeginHorizontal();

                for (int c = 0; c < cols; c++)
                {
                    int slotIndex = startIdx + c;
                    DrawGridSlotTile(slotIndex, r, c);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            // Alt Butonlar
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
            if (GUILayout.Button($"➕ Yeni Dalga (Sıra #{rows + 1}) Ekle ({cols} Vagon)", GUILayout.Height(26)))
            {
                AddWagonRow(cols);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawGridSlotTile(int slotIndex, int r, int c)
        {
            var sequence = m_SelectedLevel.WagonSequence;
            bool isFilled = (slotIndex < sequence.Count && sequence[slotIndex] != null);
            bool isSwapSelected = (m_SelectedSlotForSwap == slotIndex);

            float cardWidth = 152f;
            float cardHeight = 92f;

            if (isFilled)
            {
                var wagon = sequence[slotIndex];
                Color wc = wagon.wagonColor;
                string cDisplayName = GetColorDisplayName(wc, wagon.label, slotIndex);

                // Dış Kutu (Swap seçiliyse sarı kenarlık)
                GUI.backgroundColor = isSwapSelected ? Color.yellow : Color.white;
                EditorGUILayout.BeginVertical("box", GUILayout.Width(cardWidth), GUILayout.Height(cardHeight));
                GUI.backgroundColor = Color.white;

                // Üst Şerit: Vagon Rengi + Numara + Silme Butonu
                Rect topRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.DrawRect(topRect, wc);

                GUIStyle numStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 10,
                    normal = { textColor = (wc.grayscale > 0.55f) ? Color.black : Color.white }
                };
                GUI.Label(new Rect(topRect.x + 4, topRect.y + 1, topRect.width - 24, 18), $"#{slotIndex + 1} {cDisplayName}", numStyle);

                // Silme [✕] (Slotu boşaltır)
                if (GUI.Button(new Rect(topRect.xMax - 18, topRect.y + 1, 16, 16), "✕", EditorStyles.miniButton))
                {
                    Undo.RecordObject(m_SelectedLevel, "Clear Slot");
                    sequence[slotIndex] = null;
                    while (sequence.Count > 0 && sequence[sequence.Count - 1] == null)
                    {
                        sequence.RemoveAt(sequence.Count - 1);
                    }
                    m_SelectedSlotForSwap = -1;
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                    EditorGUILayout.EndVertical();
                    return;
                }

                // Orta Alan: Tıklanabilir Gövde (Fırça / Takas / Boşalt)
                GUIStyle bodyBtnStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    fontStyle = FontStyle.Bold
                };

                string actionPrompt = "🛒 Vagon";
                if (m_ActiveBrushPaletteIndex >= 0 && m_ActiveBrushPaletteIndex < m_SelectedLevel.ColorPalette.Count)
                {
                    var brushEntry = m_SelectedLevel.ColorPalette[m_ActiveBrushPaletteIndex];
                    string bName = GetColorDisplayName(brushEntry.targetColor, brushEntry.label, m_ActiveBrushPaletteIndex);
                    actionPrompt = $"🖌️ {bName} Yap";
                }
                else if (m_ActiveBrushPaletteIndex == -2)
                {
                    actionPrompt = "🧹 Boşalt";
                }
                else if (isSwapSelected)
                {
                    actionPrompt = "⭐ Taşınacak";
                }

                if (GUILayout.Button(actionPrompt, bodyBtnStyle, GUILayout.Height(18)))
                {
                    HandleSlotClick(slotIndex);
                }

                // Kapasite Stepper Kontrolü
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Kap:", EditorStyles.miniBoldLabel, GUILayout.Width(26));

                if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(18), GUILayout.Height(16)))
                {
                    wagon.capacity = Mathf.Max(1, wagon.capacity - 1);
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }

                int newCap = EditorGUILayout.IntField(wagon.capacity, GUILayout.Width(28));
                if (newCap != wagon.capacity)
                {
                    wagon.capacity = Mathf.Max(1, newCap);
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }

                if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(18), GUILayout.Height(16)))
                {
                    wagon.capacity += 1;
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }

                // Eksik tamamlama simgesi
                int assigned = m_SelectedLevel.GetTotalAssignedCapacityForColor(wc);
                int needed = GetTotalCubeCountForColor(wc);
                int diff = needed - assigned;
                if (diff > 0)
                {
                    GUI.backgroundColor = new Color(0.95f, 0.75f, 0.2f);
                    if (GUILayout.Button(new GUIContent("⚡", $"+{diff} küpü bu vagona yükle"), EditorStyles.miniButton, GUILayout.Width(18), GUILayout.Height(16)))
                    {
                        wagon.capacity += diff;
                        m_SelectedLevel.UseCustomWagonSequence = true;
                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                    }
                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndHorizontal();

                // Hızlı Renk Noktaları (Tek tıkla rengi doğrudan değiştir)
                if (m_SelectedLevel.ColorPalette != null && m_SelectedLevel.ColorPalette.Count > 0)
                {
                    EditorGUILayout.Space(1);
                    EditorGUILayout.BeginHorizontal();
                    for (int p = 0; p < m_SelectedLevel.ColorPalette.Count; p++)
                    {
                        var pEntry = m_SelectedLevel.ColorPalette[p];
                        if (pEntry == null || pEntry.pixelCount <= 0) continue;
                        Rect dotRect = EditorGUILayout.GetControlRect(false, 13, GUILayout.Width(13));
                        EditorGUI.DrawRect(dotRect, pEntry.targetColor);
                        if (Event.current.type == EventType.MouseDown && dotRect.Contains(Event.current.mousePosition))
                        {
                            wagon.wagonColor = pEntry.targetColor;
                            wagon.paletteIndex = p;
                            string dotName = GetColorDisplayName(pEntry.targetColor, pEntry.label, p);
                            wagon.label = $"{dotName} (#{slotIndex + 1})";
                            m_SelectedLevel.UseCustomWagonSequence = true;
                            EditorUtility.SetDirty(m_SelectedLevel);
                            NotifyLiveSceneUpdate();
                            Event.current.Use();
                        }
                    }
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
            else
            {
                // Boş Slot Kartı
                GUI.backgroundColor = isSwapSelected ? Color.yellow : new Color(0.92f, 0.92f, 0.92f);
                EditorGUILayout.BeginVertical("box", GUILayout.Width(cardWidth), GUILayout.Height(cardHeight));
                GUI.backgroundColor = Color.white;

                GUIStyle emptyTitle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold
                };
                GUILayout.Label($"⭕ Boş Slot #{slotIndex + 1}", emptyTitle, GUILayout.Height(18));

                GUI.backgroundColor = (m_ActiveBrushPaletteIndex >= 0) ? new Color(0.3f, 0.88f, 0.5f) : Color.white;
                string emptyBtnLabel = "+ Vagon Ekle";
                if (m_ActiveBrushPaletteIndex >= 0 && m_ActiveBrushPaletteIndex < m_SelectedLevel.ColorPalette.Count)
                {
                    var bEntry = m_SelectedLevel.ColorPalette[m_ActiveBrushPaletteIndex];
                    string bName = GetColorDisplayName(bEntry.targetColor, bEntry.label, m_ActiveBrushPaletteIndex);
                    emptyBtnLabel = $"🖌️ {bName} Yerleştir";
                }

                if (GUILayout.Button(emptyBtnLabel, EditorStyles.miniButton, GUILayout.Height(30)))
                {
                    HandleSlotClick(slotIndex);
                }
                GUI.backgroundColor = Color.white;

                // Boş slot hızlı renk noktaları (Doğrudan o renkten vagon oluşturur)
                if (m_SelectedLevel.ColorPalette != null && m_SelectedLevel.ColorPalette.Count > 0)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.BeginHorizontal();
                    for (int p = 0; p < m_SelectedLevel.ColorPalette.Count; p++)
                    {
                        var pEntry = m_SelectedLevel.ColorPalette[p];
                        if (pEntry == null || pEntry.pixelCount <= 0) continue;
                        Rect dotRect = EditorGUILayout.GetControlRect(false, 13, GUILayout.Width(13));
                        EditorGUI.DrawRect(dotRect, pEntry.targetColor);
                        if (Event.current.type == EventType.MouseDown && dotRect.Contains(Event.current.mousePosition))
                        {
                            int cap = Mathf.Max(1, m_SelectedLevel.TruckCapacity);
                            string dotName = GetColorDisplayName(pEntry.targetColor, pEntry.label, p);
                            InsertWagonAtSlot(slotIndex, pEntry.targetColor, cap, $"{dotName} (#{slotIndex + 1})");
                            Event.current.Use();
                        }
                    }
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void HandleSlotClick(int slotIndex)
        {
            var sequence = m_SelectedLevel.WagonSequence;
            if (sequence == null) sequence = m_SelectedLevel.WagonSequence = new List<WagonSequenceEntry>();

            // 1. Silgi Modu
            if (m_ActiveBrushPaletteIndex == -2)
            {
                if (slotIndex < sequence.Count && sequence[slotIndex] != null)
                {
                    Undo.RecordObject(m_SelectedLevel, "Erase Wagon");
                    sequence[slotIndex] = null;
                    while (sequence.Count > 0 && sequence[sequence.Count - 1] == null)
                    {
                        sequence.RemoveAt(sequence.Count - 1);
                    }
                    m_SelectedSlotForSwap = -1;
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
                return;
            }

            // 2. Fırça Modu: Seçili rengi bu slota bas
            if (m_ActiveBrushPaletteIndex >= 0 && m_ActiveBrushPaletteIndex < m_SelectedLevel.ColorPalette.Count)
            {
                var brushEntry = m_SelectedLevel.ColorPalette[m_ActiveBrushPaletteIndex];
                Color bc = brushEntry.targetColor;
                string bName = GetColorDisplayName(bc, brushEntry.label, m_ActiveBrushPaletteIndex);
                int cap = Mathf.Max(1, m_SelectedLevel.TruckCapacity);

                InsertWagonAtSlot(slotIndex, bc, cap, $"{bName} (#{slotIndex + 1})");
                m_SelectedSlotForSwap = -1;
                return;
            }

            // 3. Takas / Seçim Modu (Swap / Move)
            if (m_SelectedSlotForSwap == -1)
            {
                if (slotIndex < sequence.Count && sequence[slotIndex] != null)
                {
                    m_SelectedSlotForSwap = slotIndex;
                }
                else
                {
                    // Boş slota tıklandıysa otomatik sıradaki eksik rengi koy
                    Color missingColor = GetNextMissingColorOrDefault();
                    string defName = GetColorDisplayName(missingColor, "", slotIndex);
                    int cap = Mathf.Max(1, m_SelectedLevel.TruckCapacity);
                    InsertWagonAtSlot(slotIndex, missingColor, cap, $"{defName} (#{slotIndex + 1})");
                }
            }
            else if (m_SelectedSlotForSwap == slotIndex)
            {
                m_SelectedSlotForSwap = -1;
            }
            else
            {
                // İki slotu takas et / taşı
                Undo.RecordObject(m_SelectedLevel, "Swap Wagons");

                while (sequence.Count <= Mathf.Max(m_SelectedSlotForSwap, slotIndex))
                {
                    sequence.Add(null);
                }

                (sequence[m_SelectedSlotForSwap], sequence[slotIndex]) = (sequence[slotIndex], sequence[m_SelectedSlotForSwap]);

                // Listenin sonundaki null'ları temizle
                while (sequence.Count > 0 && sequence[sequence.Count - 1] == null)
                {
                    sequence.RemoveAt(sequence.Count - 1);
                }

                m_SelectedSlotForSwap = -1;
                m_SelectedLevel.UseCustomWagonSequence = true;
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }
        }

        private struct SceneCubeCountResult
        {
            public bool fromScene;
            public int totalCubes;
            public Dictionary<Color, int> totalPerColor;
            public Dictionary<Color, int> exposedPerColor;
            public Dictionary<Color, string> colorNames;
            public Dictionary<Color, int> paletteIndices;
        }

        private SceneCubeCountResult CountCubesFromSceneOrPalette()
        {
            var result = new SceneCubeCountResult
            {
                fromScene = false,
                totalCubes = 0,
                totalPerColor = new Dictionary<Color, int>(),
                exposedPerColor = new Dictionary<Color, int>(),
                colorNames = new Dictionary<Color, string>(),
                paletteIndices = new Dictionary<Color, int>()
            };

            if (m_SelectedLevel == null) return result;

            // 1. Sahnedeki 3D küpleri topla
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            PixelCube[] sceneCubes = null;
            if (gen != null && gen.CubesContainer != null && gen.CubesContainer.childCount > 0)
            {
                sceneCubes = gen.CubesContainer.GetComponentsInChildren<PixelCube>();
            }
            if (sceneCubes == null || sceneCubes.Length == 0)
            {
                sceneCubes = Object.FindObjectsByType<PixelCube>(FindObjectsSortMode.None);
            }

            if (sceneCubes != null && sceneCubes.Length > 0)
            {
                Dictionary<(int, int), PixelCube> gridMap = null;
                try
                {
                    gridMap = Miner.BuildCubeGridMap(sceneCubes);
                }
                catch { }

                foreach (var cube in sceneCubes)
                {
                    if (cube == null || !cube.gameObject.activeSelf || cube.IsPopped) continue;
                    Color rawColor = cube.CurrentColor;
                    if (rawColor.a < 0.1f) continue;

                    Color matchedColor = rawColor;
                    string label = "";
                    int palIdx = 0;

                    if (m_SelectedLevel.ColorPalette != null && m_SelectedLevel.ColorPalette.Count > 0)
                    {
                        for (int p = 0; p < m_SelectedLevel.ColorPalette.Count; p++)
                        {
                            var pEntry = m_SelectedLevel.ColorPalette[p];
                            if (pEntry == null) continue;
                            if (PaletteColorOverride.ColorsMatch(pEntry.targetColor, rawColor, 0.08f))
                            {
                                matchedColor = pEntry.targetColor;
                                label = pEntry.label;
                                palIdx = p;
                                break;
                            }
                        }
                    }

                    if (!result.totalPerColor.ContainsKey(matchedColor))
                    {
                        result.totalPerColor[matchedColor] = 0;
                        result.exposedPerColor[matchedColor] = 0;
                        result.colorNames[matchedColor] = GetColorDisplayName(matchedColor, label, palIdx);
                        result.paletteIndices[matchedColor] = palIdx;
                    }

                    result.totalPerColor[matchedColor]++;
                    result.totalCubes++;

                    bool isExposed = true;
                    if (gridMap != null)
                    {
                        try
                        {
                            isExposed = Miner.IsCubeExposed(cube, gridMap);
                        }
                        catch { isExposed = true; }
                    }

                    if (isExposed)
                    {
                        result.exposedPerColor[matchedColor]++;
                    }
                }

                if (result.totalPerColor.Count > 0)
                {
                    result.fromScene = true;
                }
            }

            // 2. Sahneden bulunamadıysa Bölümün Renk Paleti verisini kullan (Fallback)
            if (!result.fromScene && m_SelectedLevel.ColorPalette != null)
            {
                for (int p = 0; p < m_SelectedLevel.ColorPalette.Count; p++)
                {
                    var pEntry = m_SelectedLevel.ColorPalette[p];
                    if (pEntry == null || pEntry.pixelCount <= 0) continue;
                    Color c = pEntry.targetColor;
                    result.totalPerColor[c] = pEntry.pixelCount;
                    result.exposedPerColor[c] = pEntry.pixelCount;
                    result.colorNames[c] = GetColorDisplayName(c, pEntry.label, p);
                    result.paletteIndices[c] = p;
                    result.totalCubes += pEntry.pixelCount;
                }
            }

            return result;
        }

        private void GenerateSmartSequenceFromSceneCubes()
        {
            if (m_SelectedLevel == null) return;
            Undo.RecordObject(m_SelectedLevel, "Generate Smart Sequence From Scene Cubes");

            SceneCubeCountResult scan = CountCubesFromSceneOrPalette();

            if (scan.totalPerColor.Count == 0 || scan.totalCubes <= 0)
            {
                EditorUtility.DisplayDialog("Akıllı Sıralama", "Sahnede veya palette sayılacak küp bulunamadı! Önce sahneyi inşa edin veya bölüme bir piksel görseli yükleyin.", "Tamam");
                return;
            }

            int capPerWagon = Mathf.Max(1, m_SelectedLevel.TruckCapacity);

            // Her renk için gereken vagon listesi oluştur
            Dictionary<Color, List<WagonSequenceEntry>> colorWagons = new Dictionary<Color, List<WagonSequenceEntry>>();
            int totalGeneratedWagons = 0;

            foreach (var kvp in scan.totalPerColor)
            {
                Color c = kvp.Key;
                int remaining = kvp.Value;
                string name = scan.colorNames[c];
                int palIdx = scan.paletteIndices[c];

                var wList = new List<WagonSequenceEntry>();
                while (remaining > 0)
                {
                    int load = Mathf.Min(capPerWagon, remaining);
                    wList.Add(new WagonSequenceEntry(c, load, palIdx, $"{name} ({load})"));
                    remaining -= load;
                }
                colorWagons[c] = wList;
                totalGeneratedWagons += wList.Count;
            }

            // Bulmaca akışını seçilen zorluk moduna göre oluştur:
            var sequence = new List<WagonSequenceEntry>();

            if (m_WagonDifficulty == WagonDifficultyMode.Easy)
            {
                // 🟢 KOLAY MOD (Risksiz & Akıcı):
                // En çok dış yüzeyi (exposed) olan renkler en başa gelir.
                // Tamamen gömülü renkler (exposed == 0) en son dalgalara ertelenir.
                // Aynı renkten 1-2 vagon peş peşe verilerek oyuncunun o rengi hızla bitirip raydan göndermesi sağlanır (sıfır kilit).
                List<Color> sortedColors = new List<Color>(scan.totalPerColor.Keys);
                sortedColors.Sort((a, b) =>
                {
                    int expA = scan.exposedPerColor.ContainsKey(a) ? scan.exposedPerColor[a] : 0;
                    int expB = scan.exposedPerColor.ContainsKey(b) ? scan.exposedPerColor[b] : 0;
                    if (expA != expB) return expB.CompareTo(expA);
                    return scan.totalPerColor[b].CompareTo(scan.totalPerColor[a]);
                });

                List<Color> exposedColors = new List<Color>();
                List<Color> buriedColors = new List<Color>();
                foreach (var c in sortedColors)
                {
                    int exp = scan.exposedPerColor.ContainsKey(c) ? scan.exposedPerColor[c] : 0;
                    if (exp > 0) exposedColors.Add(c);
                    else buriedColors.Add(c);
                }

                while (exposedColors.Count > 0)
                {
                    for (int i = 0; i < exposedColors.Count; i++)
                    {
                        Color c = exposedColors[i];
                        var bucket = colorWagons[c];
                        if (bucket.Count > 0)
                        {
                            int take = Mathf.Min(bucket.Count, 2);
                            for (int t = 0; t < take; t++)
                            {
                                sequence.Add(bucket[0]);
                                bucket.RemoveAt(0);
                            }
                        }
                    }

                    for (int i = exposedColors.Count - 1; i >= 0; i--)
                    {
                        if (colorWagons[exposedColors[i]].Count == 0)
                        {
                            exposedColors.RemoveAt(i);
                        }
                    }
                }

                // Gömülü renkler en sonda gelir
                foreach (var c in buriedColors)
                {
                    var bucket = colorWagons[c];
                    while (bucket.Count > 0)
                    {
                        sequence.Add(bucket[0]);
                        bucket.RemoveAt(0);
                    }
                }
            }
            else if (m_WagonDifficulty == WagonDifficultyMode.Hard)
            {
                // 🔴 ZOR MOD (Taktiksel & Slot Baskısı):
                // Açık renklerin arasına, henüz az açık veya gömülü renklerin vagonları erken enjekte edilir.
                // Bu vagonlar ray slotlarını işgal ederek oyuncuyu diğer slotları dikkatle kullanıp yolu açmaya zorlar.
                List<Color> highExposed = new List<Color>();
                List<Color> lowOrBuried = new List<Color>();

                foreach (var kvp in scan.totalPerColor)
                {
                    Color c = kvp.Key;
                    int tot = kvp.Value;
                    int exp = scan.exposedPerColor.ContainsKey(c) ? scan.exposedPerColor[c] : 0;
                    float ratio = tot > 0 ? (float)exp / tot : 1f;

                    if (ratio >= 0.4f && exp > 0)
                        highExposed.Add(c);
                    else
                        lowOrBuried.Add(c);
                }

                if (highExposed.Count == 0) highExposed.AddRange(lowOrBuried);
                if (lowOrBuried.Count == 0) lowOrBuried.AddRange(highExposed);

                int step = 0;
                while (sequence.Count < totalGeneratedWagons)
                {
                    step++;
                    // 2. adımda ve her 3 adımda bir gömülü vagon enjekte et (ray slotunu bağlama taktiği)
                    bool injectBuried = (step == 2 || step % 3 == 0) && lowOrBuried.Count > 0;
                    Color pickColor = Color.black;
                    bool found = false;

                    if (injectBuried)
                    {
                        for (int i = 0; i < lowOrBuried.Count; i++)
                        {
                            Color c = lowOrBuried[i];
                            if (colorWagons[c].Count > 0)
                            {
                                pickColor = c;
                                found = true;
                                lowOrBuried.RemoveAt(i);
                                lowOrBuried.Add(c);
                                break;
                            }
                        }
                    }

                    if (!found)
                    {
                        for (int i = 0; i < highExposed.Count; i++)
                        {
                            Color c = highExposed[i];
                            if (colorWagons[c].Count > 0)
                            {
                                pickColor = c;
                                found = true;
                                highExposed.RemoveAt(i);
                                highExposed.Add(c);
                                break;
                            }
                        }
                    }

                    if (!found)
                    {
                        foreach (var kvp in colorWagons)
                        {
                            if (kvp.Value.Count > 0)
                            {
                                pickColor = kvp.Key;
                                found = true;
                                break;
                            }
                        }
                    }

                    if (found && colorWagons[pickColor].Count > 0)
                    {
                        sequence.Add(colorWagons[pickColor][0]);
                        colorWagons[pickColor].RemoveAt(0);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else
            {
                // 🟡 DENGELİ MOD (Standart Dağılım - Casual Puzzle):
                // Açık yüzeyi yüksek renkler ilk dalgaya yayılır, ardından dönüşümlü (round-robin) dağıtılır.
                List<Color> colorPriorityList = new List<Color>(scan.totalPerColor.Keys);
                colorPriorityList.Sort((a, b) =>
                {
                    int expA = scan.exposedPerColor.ContainsKey(a) ? scan.exposedPerColor[a] : 0;
                    int expB = scan.exposedPerColor.ContainsKey(b) ? scan.exposedPerColor[b] : 0;
                    return expB.CompareTo(expA);
                });

                int colorIdx = 0;
                while (colorPriorityList.Count > 0)
                {
                    Color curColor = colorPriorityList[colorIdx % colorPriorityList.Count];
                    var bucket = colorWagons[curColor];

                    if (bucket.Count > 0)
                    {
                        var wagon = bucket[0];
                        bucket.RemoveAt(0);
                        sequence.Add(wagon);
                    }

                    if (bucket.Count == 0)
                    {
                        colorPriorityList.RemoveAt(colorIdx % colorPriorityList.Count);
                    }
                    else
                    {
                        colorIdx++;
                    }
                }
            }

            // Vagon etiketlerini sıra numarasına göre düzenle (#1, #2, ...)
            for (int i = 0; i < sequence.Count; i++)
            {
                Color c = sequence[i].wagonColor;
                string cName = scan.colorNames.ContainsKey(c) ? scan.colorNames[c] : "Vagon";
                sequence[i].label = $"{cName} (#{i + 1})";
            }

            m_SelectedLevel.WagonSequence = sequence;
            m_SelectedLevel.UseCustomWagonSequence = true;

            // Izgara ve havuz boyutlarını otomatik güncelle
            int cols = Mathf.Clamp(m_GridColumnsPerRow, 1, 8);
            m_TargetGridRows = Mathf.Max(1, Mathf.CeilToInt((float)sequence.Count / cols));
            m_SelectedLevel.PoolRows = m_TargetGridRows;
            m_SelectedLevel.PoolColumns = cols;

            m_ActiveBrushPaletteIndex = -1;
            m_SelectedSlotForSwap = -1;

            EditorUtility.SetDirty(m_SelectedLevel);
            NotifyLiveSceneUpdate();

            string modeName = m_WagonDifficulty == WagonDifficultyMode.Easy ? "🟢 Kolay (Risksiz)" :
                              m_WagonDifficulty == WagonDifficultyMode.Medium ? "🟡 Dengeli (Standart)" : "🔴 Zor (Taktiksel)";
            string sourceText = scan.fromScene ? "🎯 Sahne 3D Derinlik Analizi" : "🎨 Renk Paleti";
            m_LastSmartStatusMessage = $"✓ Akıllı Sıralama [{modeName}]: {sourceText} ile {scan.totalCubes} küp için {sequence.Count} vagon ve {m_TargetGridRows} dalga başarıyla oluşturuldu! ({m_TargetGridRows} Dalga × {cols} Kolon)";
            Debug.Log($"<color=#00FFAA><b>[LevelDesigner]</b></color> {m_LastSmartStatusMessage}");
        }

        private void AutoDistributeSmartPuzzle()
        {
            GenerateSmartSequenceFromSceneCubes();
        }

        private void InsertWagonAtSlot(int slotIndex, Color color, int capacity, string label)
        {
            Undo.RecordObject(m_SelectedLevel, "Insert Wagon At Slot");
            if (m_SelectedLevel.WagonSequence == null) m_SelectedLevel.WagonSequence = new List<WagonSequenceEntry>();
            var sequence = m_SelectedLevel.WagonSequence;

            int cap = capacity > 0 ? capacity : Mathf.Max(1, m_SelectedLevel.TruckCapacity);

            while (sequence.Count < slotIndex)
            {
                Color padColor = GetNextMissingColorOrDefault();
                string padName = GetColorDisplayName(padColor, "", sequence.Count);
                sequence.Add(new WagonSequenceEntry(padColor, cap, 0, $"{padName} (#{sequence.Count + 1})"));
            }

            if (slotIndex < sequence.Count)
            {
                sequence[slotIndex] = new WagonSequenceEntry(color, cap, 0, label);
            }
            else
            {
                sequence.Add(new WagonSequenceEntry(color, cap, 0, label));
            }

            m_SelectedLevel.UseCustomWagonSequence = true;
            EditorUtility.SetDirty(m_SelectedLevel);
            NotifyLiveSceneUpdate();
        }

        private Color GetNextMissingColorOrDefault()
        {
            if (m_SelectedLevel == null || m_SelectedLevel.ColorPalette == null || m_SelectedLevel.ColorPalette.Count == 0)
                return Color.yellow;

            foreach (var entry in m_SelectedLevel.ColorPalette)
            {
                if (entry == null || entry.pixelCount <= 0) continue;
                int assigned = m_SelectedLevel.GetTotalAssignedCapacityForColor(entry.targetColor);
                if (assigned < entry.pixelCount)
                {
                    return entry.targetColor;
                }
            }

            return m_SelectedLevel.ColorPalette[0].targetColor;
        }


        private void FillRowSlots(int rowIndex, int colCount)
        {
            Undo.RecordObject(m_SelectedLevel, "Fill Row Slots");
            int startIdx = rowIndex * colCount;
            int cap = Mathf.Max(1, m_SelectedLevel.TruckCapacity);

            for (int c = 0; c < colCount; c++)
            {
                int slotIndex = startIdx + c;
                if (slotIndex >= m_SelectedLevel.WagonSequence.Count || m_SelectedLevel.WagonSequence[slotIndex] == null)
                {
                    Color missing = GetNextMissingColorOrDefault();
                    string defName = GetColorDisplayName(missing, "", 0);
                    InsertWagonAtSlot(slotIndex, missing, cap, $"{defName} (#{slotIndex + 1})");
                }
            }
        }


        private void AddWagonRow(int colCount)
        {
            if (m_SelectedLevel == null) return;
            Undo.RecordObject(m_SelectedLevel, "Add Wagon Row");
            if (m_SelectedLevel.WagonSequence == null) m_SelectedLevel.WagonSequence = new List<WagonSequenceEntry>();

            var sequence = m_SelectedLevel.WagonSequence;
            var palette = m_SelectedLevel.ColorPalette;
            int cap = Mathf.Max(1, m_SelectedLevel.TruckCapacity);

            for (int i = 0; i < colCount; i++)
            {
                Color wColor = Color.yellow;
                string wLabel = $"Vagon #{sequence.Count + 1}";
                if (palette != null && palette.Count > 0)
                {
                    var pEntry = palette[sequence.Count % palette.Count];
                    wColor = pEntry.targetColor;
                    if (!string.IsNullOrEmpty(pEntry.label))
                    {
                        wLabel = $"{pEntry.label} (#{sequence.Count + 1})";
                    }
                }
                sequence.Add(new WagonSequenceEntry(wColor, cap, 0, wLabel));
            }
            m_SelectedLevel.UseCustomWagonSequence = true;
            EditorUtility.SetDirty(m_SelectedLevel);
            NotifyLiveSceneUpdate();
        }

        private void DeleteWagonRow(int rowIndex, int colCount)
        {
            if (m_SelectedLevel == null || m_SelectedLevel.WagonSequence == null) return;
            Undo.RecordObject(m_SelectedLevel, "Delete Wagon Row");
            var sequence = m_SelectedLevel.WagonSequence;
            int start = rowIndex * colCount;
            if (start < 0 || start >= sequence.Count) return;

            int count = Mathf.Min(colCount, sequence.Count - start);
            sequence.RemoveRange(start, count);
            m_SelectedLevel.UseCustomWagonSequence = true;
            EditorUtility.SetDirty(m_SelectedLevel);
            NotifyLiveSceneUpdate();
        }

        private void MoveWagonRow(int rowIndex, int targetRowIndex, int colCount)
        {
            if (m_SelectedLevel == null || m_SelectedLevel.WagonSequence == null) return;
            Undo.RecordObject(m_SelectedLevel, "Move Wagon Row");
            var sequence = m_SelectedLevel.WagonSequence;

            int row1Start = rowIndex * colCount;
            int row1Count = Mathf.Min(colCount, sequence.Count - row1Start);

            int row2Start = targetRowIndex * colCount;
            int row2Count = Mathf.Min(colCount, sequence.Count - row2Start);

            if (row1Start < 0 || row2Start < 0 || row1Start >= sequence.Count || row2Start >= sequence.Count) return;

            List<WagonSequenceEntry> row1 = sequence.GetRange(row1Start, row1Count);
            List<WagonSequenceEntry> row2 = sequence.GetRange(row2Start, row2Count);

            if (rowIndex < targetRowIndex)
            {
                sequence.RemoveRange(row2Start, row2Count);
                sequence.RemoveRange(row1Start, row1Count);
                sequence.InsertRange(row1Start, row2);
                sequence.InsertRange(row1Start + row2Count, row1);
            }
            else
            {
                sequence.RemoveRange(row1Start, row1Count);
                sequence.RemoveRange(row2Start, row2Count);
                sequence.InsertRange(row2Start, row1);
                sequence.InsertRange(row2Start + row1Count, row2);
            }
            m_SelectedLevel.UseCustomWagonSequence = true;
            EditorUtility.SetDirty(m_SelectedLevel);
            NotifyLiveSceneUpdate();
        }

        #endregion

        #region TAB 3: SCENE & VISUAL TOOLS (CONSOLIDATED STUDIO)

        private void DrawSceneToolsTab()
        {
            m_DetailScroll = EditorGUILayout.BeginScrollView(m_DetailScroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.Space(8);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.35f, 0.88f, 1f) }
            };
            EditorGUILayout.LabelField("🛠️ Sahne, Ray & Görsel Araçları Stüdyosu", titleStyle);
            EditorGUILayout.HelpBox(
                "Menü çubuğundaki tüm karmaşık araçlar buraya düzenli kartlar halinde taşınmıştır. " +
                "Sahne kurulumu, raylar, vagon modelleri, URP materyalleri ve gölgeleri buradan tek tıkla yönetebilirsiniz.",
                MessageType.Info
            );
            EditorGUILayout.Space(6);

            // 1. 🏝️ GEMİ SAHNESİ & RAY YÖNETİMİ
            DrawSceneToolCard("🏝️ Gemi Sahnesi & Ray Yönetimi", new (string, string, System.Action)[]
            {
                ("🏝️ Gemi Sahnesini Sıfırdan Kur & Tüm Öğeleri Getir", "Gemi sahnesini, slotları, kum çerçevesini ve piksel sanatını kurar.", () => GemiSceneSetup.SetupGemiSceneMenu()),
                ("🎨 Kum Alanındaki Piksel Resmi Yenile (Regenerate)", "Kum alanındaki mevcut piksel sanatını ve gölgeleri anında yeniden üretir.", () => GemiSceneSetup.RegeneratePixelArtMenu()),
                ("🛤️ Vagon Döngüsünü Kur (Ray + Havuz)", "Slot şeridi, alt havuz ve ray vagon döngüsünü sahneye kurar.", () => SetupTruckSlots.Setup()),
                ("🛤️ Çevresel Rayları Döşe (Mavi Çerçeve)", "Mavi resim çerçevesinin etrafına çevresel rayları otomatik döşer.", () => TrackSystemSetup.BuildSceneRails()),
                ("🗑️ Vagon Döngüsünü / Rayları Kaldır", "Sahnedeki vagon döngüsünü ve rayları temizler.", () => SetupTruckSlots.Remove()),
                ("➡️ Sonraki Seviyeyi Yükle", "Bir sonraki seviyeyi sahneye çağırır.", () => GemiSceneSetup.NextLevelMenu()),
                ("⬅️ Önceki Seviyeyi Yükle", "Bir önceki seviyeyi sahneye çağırır.", () => GemiSceneSetup.PrevLevelMenu())
            });

            EditorGUILayout.Space(6);

            // 2. 🎨 GÖRSEL, IŞIK & SHADER DÖNÜŞÜMÜ
            DrawSceneToolCard("🎨 Görsel, Işık & Shader Dönüşümü", new (string, string, System.Action)[]
            {
                ("✨ Komple Görsel Dönüşümü Uygula", "Tüm oyun görsellerini, 9-slice panoları ve arayüzü en son casual tasarıma dönüştürür.", () => SetupVisualOverhaul.ApplyOverhaul()),
                ("🌟 Hypercasual Parlak Işıklandırma & Renkler", "Sahne ışıklarını ve karakter materyallerini parlak casual stile geçirir.", () => SetupHypercasualLightingAndMaterials.ApplyHypercasualOverhaul()),
                ("🎨 Cartoon Shader'a Geçir", "Ana küp materyaline toon/cartoon cel-shader'ı uygular.", () => SetupCartoonShader.Apply()),
                ("🔧 Mor Kaplamaları Düzelt (Fix Scifi URP Materials)", "URP'de mor görünen eski materyalleri otomatik onarır.", () => UpgradeScifiMaterialsToURP.UpgradeMaterials(false)),
                ("🌑 Havuz Slot Sahte Gölgelerini Kur", "Alt havuzdaki vagon slotlarının altına yumuşak fake shadow uygular.", () => SetupPoolSlotShadows.ApplyPoolShadows()),
                ("🌑 Mavi Ray Sahte Gölgesini Güncelle", "Mavi çevresel rayların altındaki zemin sahte gölgesini günceller.", () => TrackFakeShadow.CreateOrUpdateShadowMenu()),
                ("🧹 Pano ve Obje Arkasındaki Gölgeleri Temizle", "Eski artık pano ve obje arkası gölgelerini sahneden temizler.", () => CleanupSceneShadows.RunPurge())
            });

            EditorGUILayout.Space(6);

            // 3. 🤖 VAGON & MODEL STİLİ SEÇİMİ
            DrawSceneToolCard("🤖 Vagon & Model Stili Seçimi", new (string, string, System.Action)[]
            {
                ("🤖 CyberCube Modelini Kur & Aktif Et", "Fütüristik robotik CyberCube vagon modelini sahneye kurar.", () => SetupCyberCubeWagon.ExecuteSetup(false)),
                ("🐱 KawaiiCube Modelini Kur & Aktif Et", "Sevimli kedi KawaiiCube vagon modelini sahneye kurar.", () => SetupKawaiiCubeWagon.ExecuteSetup(false)),
                ("🚀 Vakum Topu Vagonunu Kur (object_005)", "Vakum topu / topçu vagon modelini aktif eder.", () => SetupVacuumCannonWagon.SetupManual()),
                ("⛏️ Mecha Miner Karakterini Kur", "Kazıcı robot karakter modelini ve animatörünü sahneye ekler.", () => MechaMinerSetup.Setup()),
                ("🧹 Sahnedeki Gizli Önizleme Robotlarını Temizle", "Model stüdyosundan sahnede kalan hayalet önizleme objelerini temizler.", () => PurgeStrayStudioPreview.ExecutePurge())
            });

            EditorGUILayout.Space(6);

            // 4. 🎯 HUD, ARAYÜZ & TESTLER
            DrawSceneToolCard("🎯 HUD, Arayüz & Testler", new (string, string, System.Action)[]
            {
                ("🎯 Köşe Sayacını & Üst HUD'ı Düzenle", "LilitaOne fontu ve şık sayaç ile üst arayüzü yapılandırır.", () => SetupCleanCornerCounter.ApplyCleanCorner()),
                ("🖼️ Casual HUD & Arkaplan Kur", "Casual HUD panellerini ve renkli arka planı yapılandırır.", () => SetupCasualHud.Apply()),
                ("🧪 Gemi Kalkış Testi (Ship Departure Test)", "Bölüm tamamlandığında geminin kalkış animasyonunu canlı test eder.", () => TestShipDeparture.RunTest()),
                ("📸 9:16 Ekran Görüntüsü Al (Capture Screenshot)", "Game görünümünden tam 1080x1920 dikey ekran görüntüsü alır.", () => CaptureGameViewScreenshot.Capture())
            });

            EditorGUILayout.Space(16);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSceneToolCard(string cardTitle, (string title, string desc, System.Action action)[] items)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            Rect titleRect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(titleRect, new Color(0.14f, 0.18f, 0.24f, 1f));
            GUIStyle cardHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.35f, 0.88f, 1f) },
                alignment = TextAnchor.MiddleLeft
            };
            GUI.Label(new Rect(titleRect.x + 8, titleRect.y + 1, titleRect.width - 16, titleRect.height), cardTitle, cardHeaderStyle);

            EditorGUILayout.Space(4);

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(0.9f, 0.94f, 1f);
                if (GUILayout.Button(item.title, GUILayout.Width(330), GUILayout.Height(24)))
                {
                    item.action?.Invoke();
                }
                GUI.backgroundColor = Color.white;

                GUILayout.Space(8);
                EditorGUILayout.LabelField(item.desc, EditorStyles.miniLabel);

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region ACTION BUTTONS & HELPERS

        private void DrawActionButtons()
        {
            // 1. Büyük Sahnede İnşa Et ve Test Et Butonu
            GUI.backgroundColor = new Color(0.2f, 0.88f, 0.45f);
            if (GUILayout.Button("▶️ Bu Leveli Sahnede İnşa Et ve Odaklan (Build Level)", GUILayout.Height(44)))
            {
                BuildSelectedLevelInScene();
            }

            EditorGUILayout.Space(4);

            // 2. Kaydet ve Sahneyi Temizle Butonları
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("💾 Level Değişikliklerini Kaydet", GUILayout.Height(30)))
            {
                EditorUtility.SetDirty(m_SelectedLevel);
                AssetDatabase.SaveAssets();
                SyncWithSceneLevelManager();
                Debug.Log($"<color=#00FF00>[LevelDesigner]</color> '{m_SelectedLevel.LevelName}' başarıyla kaydedildi.");
            }

            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("🧹 Sahnedeki Küpleri Temizle", GUILayout.Height(30)))
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
            EditorGUI.DrawRect(dividerRect, new Color(0.2f, 0.22f, 0.26f, 1f));
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

            // Sahnedeki TruckPool'u ve önizleme karolarını anında senkronize et
            TruckPool pool = Object.FindFirstObjectByType<TruckPool>();
            if (pool != null && m_SelectedLevel != null)
            {
                pool.RebuildPlaces(m_SelectedLevel.PoolColumns, m_SelectedLevel.PoolRows);
                pool.RefreshEditorPreview();
            }

            // Sahnedeki vagon veya parçaların rengini anında güncelle
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
                        EnsureTextureReadable(level.GetActiveTexture());
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
                EnsureTextureReadable(level.GetActiveTexture());
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
            ApplyReferenceVisualTuning(newLevel);

            AssetDatabase.CreateAsset(newLevel, assetPath);
            AssetDatabase.SaveAssets();

            RefreshLevelList();
            SelectLevel(newLevel);

            Debug.Log($"<color=#00FFAA><b>[LevelDesigner]</b></color> Yeni level oluşturuldu: {assetPath}");
        }

        public void CreateLevelFromTexture(Texture2D tex)
        {
            if (tex == null) return;
            if (!AssetDatabase.IsValidFolder("Assets/Levels"))
            {
                AssetDatabase.CreateFolder("Assets", "Levels");
            }

            EnsureTextureReadable(tex);

            int nextIndex = m_AllLevels.Count + 1;
            string safeName = tex.name.Replace(" ", "_");
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/Levels/Level_{nextIndex:D2}_{safeName}.asset");

            PixelLevelData newLevel = ScriptableObject.CreateInstance<PixelLevelData>();
            newLevel.LevelName = tex.name;
            newLevel.LevelIndex = nextIndex;
            newLevel.LevelTexture = tex;
            newLevel.UseNativeResolution = true;
            newLevel.ExtractPaletteFromTexture();
            newLevel.GenerateInterleavedWagonSequenceFromPalette();
            newLevel.UseCustomWagonSequence = true;
            ApplyReferenceVisualTuning(newLevel);

            AssetDatabase.CreateAsset(newLevel, assetPath);
            AssetDatabase.SaveAssets();

            RefreshLevelList();
            SelectLevel(newLevel);

            Debug.Log($"<color=#00FFAA><b>[LevelDesigner]</b></color> Görselden yeni seviye otomatik üretildi: {assetPath}");
        }

        /// <summary>
        /// Yeni oluşturulan bir levelin 3D görsel ayarlarını (derinlik, boşluk/örtüşme, eğim açıları)
        /// çıplak kod varsayılanları yerine, elle ayarlanmış referans levelden (Rakun) kopyalar.
        /// Bu olmadan her yeni level düz/ince görünüyordu çünkü CreateInstance sadece kod
        /// varsayılanlarını (CubeDepth=0.4, CubeSpacing=0.04) veriyordu.
        /// </summary>
        private void ApplyReferenceVisualTuning(PixelLevelData target)
        {
            if (target == null) return;

            PixelLevelData reference = null;
            foreach (var lvl in m_AllLevels)
            {
                if (lvl != null && lvl.LevelName != null && lvl.LevelName.IndexOf("Rakun", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    reference = lvl;
                    break;
                }
            }
            if (reference == null && m_AllLevels.Count > 0)
            {
                reference = m_AllLevels[0];
            }
            if (reference == null) return;

            target.CubeSpacing = reference.CubeSpacing;
            target.CubeSpacingX = reference.CubeSpacingX;
            target.CubeDepth = reference.CubeDepth;
            target.BoardTiltAngle = reference.BoardTiltAngle;
            target.CubeFrontTiltAngle = reference.CubeFrontTiltAngle;
            target.CubeRowStepOffset = reference.CubeRowStepOffset;
            target.InnerPadding = reference.InnerPadding;
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

        public static float CalculateLevelDifficultyScore(PixelLevelData level)
        {
            if (level == null) return 0f;

            int cubeCount = level.GetTotalCubeCountInPalette();
            if (cubeCount <= 0 && level.GetActiveTexture() != null)
            {
                Vector2Int dims = level.GetGridResolution();
                cubeCount = dims.x * dims.y;
            }
            if (cubeCount <= 0) return 0f;

            int colorCount = Mathf.Max(1, level.ColorPalette != null ? level.ColorPalette.Count : 1);
            Vector2Int res = level.GetGridResolution();
            int area = Mathf.Max(1, res.x * res.y);

            // 1. Renk Çeşitliliği Çarpanı: Renk sayısı arttıkça aynı anda 3-4 slotta tıkanma riski hızla yükselir
            float colorMultiplier = 1f + Mathf.Max(0, colorCount - 2) * 0.35f;

            // 2. Slot Sayısı Baskısı: Daha az ray slotu (örn 3 slot) oyunu çok daha zor yapar
            int slots = Mathf.Clamp(level.SlotCount, 2, 8);
            float slotFactor = 4f / slots; // 3 slot -> 1.33x, 4 slot -> 1.0x, 5 slot -> 0.8x

            // 3. Matris Boyut Faktörü
            float dimensionFactor = Mathf.Sqrt(area) / 8f;

            // 4. Vagon Kapasitesi / Ortalama Küp Oranı
            int cap = Mathf.Max(1, level.TruckCapacity);
            float wagonCycleFactor = Mathf.Clamp01(1f + (float)cubeCount / (cap * 10f));

            float finalScore = (cubeCount * colorMultiplier * slotFactor) + (dimensionFactor * 15f) + (wagonCycleFactor * 20f);
            return finalScore;
        }

        public static (string badge, Color color) GetDifficultyBadge(float score)
        {
            if (score <= 0f) return ("⚪ Belirsiz", Color.gray);
            if (score < 250f) return ("🟢 Kolay", new Color(0.2f, 0.95f, 0.4f));
            if (score < 550f) return ("🟡 Orta", new Color(0.95f, 0.85f, 0.2f));
            if (score < 950f) return ("🔴 Zor", new Color(1f, 0.45f, 0.35f));
            return ("🟣 Uzman", new Color(0.85f, 0.4f, 1f));
        }

        /// <summary>
        /// Bölümleri akıllı zorluk puanına (Küp Sayısı × Renk Çeşitliliği × Slot Sayısı Baskısı) göre
        /// kolaydan zora sıralar ve numaralandırır.
        /// </summary>
        private void SortLevelsByDifficulty()
        {
            m_AllLevels.Sort((a, b) =>
            {
                float scoreA = CalculateLevelDifficultyScore(a);
                float scoreB = CalculateLevelDifficultyScore(b);
                return scoreA.CompareTo(scoreB);
            });

            AutoRenumberLevels();
            Debug.Log("<color=#00FFAA><b>[LevelDesigner]</b></color> Bölümler akıllı zorluk eğrisine (Küp Sayısı × Renk Çeşitliliği × Slot Sayısı) göre kolaydan zora sıralandı.");
        }

        private const string LevelSequenceAssetPath = "Assets/Levels/LevelSequence.asset";

        /// <summary>
        /// Sıralamanın tek doğruluk kaynağı olan LevelSequence asset'ini bulur, yoksa oluşturur.
        /// </summary>
        private LevelSequence GetOrCreateLevelSequenceAsset()
        {
            LevelSequence sequence = AssetDatabase.LoadAssetAtPath<LevelSequence>(LevelSequenceAssetPath);
            if (sequence == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Levels"))
                {
                    AssetDatabase.CreateFolder("Assets", "Levels");
                }
                sequence = ScriptableObject.CreateInstance<LevelSequence>();
                AssetDatabase.CreateAsset(sequence, LevelSequenceAssetPath);
            }
            return sequence;
        }

        /// <summary>
        /// Level Designer'daki liste sırasını tek doğruluk kaynağı olan LevelSequence asset'ine yazar
        /// ve sahnedeki LevelManager'ı bu asset'e bağlayıp önbelleğini (m_Levels) hemen tazeler.
        /// Böylece LevelManager'ın kendi Inspector'ında elle yapılmış bir sıralama varsa bir sonraki
        /// eşitlemede/Awake'te bu asset tarafından ezilir; ayrı bir "kaynak" belirsizliği kalmaz.
        /// </summary>
        private void SyncWithSceneLevelManager()
        {
            LevelSequence sequence = GetOrCreateLevelSequenceAsset();
            sequence.SetLevels(m_AllLevels);
            EditorUtility.SetDirty(sequence);
            AssetDatabase.SaveAssets();

            LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
            if (lm != null)
            {
                SerializedObject so = new SerializedObject(lm);

                SerializedProperty seqProp = so.FindProperty("m_LevelSequence");
                if (seqProp != null)
                {
                    seqProp.objectReferenceValue = sequence;
                }

                SerializedProperty prop = so.FindProperty("m_Levels");
                if (prop != null)
                {
                    prop.ClearArray();
                    for (int i = 0; i < m_AllLevels.Count; i++)
                    {
                        prop.InsertArrayElementAtIndex(i);
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = m_AllLevels[i];
                    }
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(lm);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        private void BatchFixAllLevels()
        {
            int fixedCount = 0;
            foreach (var lvl in m_AllLevels)
            {
                if (lvl == null) continue;
                Texture2D tex = lvl.GetActiveTexture();
                if (tex != null)
                {
                    EnsureTextureReadable(tex);
                    if (lvl.ColorPalette.Count == 0)
                    {
                        lvl.ExtractPaletteFromTexture();
                        EditorUtility.SetDirty(lvl);
                        fixedCount++;
                    }
                }
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Onarım Tamamlandı", $"Tüm seviyeler tarandı ve {fixedCount} adet eksik palet/izin onarıldı.", "Harika");
        }

        private static void EnsureTextureReadable(Texture2D tex)
        {
            if (tex == null) return;
            string path = AssetDatabase.GetAssetPath(tex);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (!importer.isReadable || importer.filterMode != FilterMode.Point))
            {
                importer.isReadable = true;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
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
                if (gen.ActiveLevelData != m_SelectedLevel && gen.HasUnsavedLevelChanges())
                {
                    string activeLevelName = gen.ActiveLevelData != null ? gen.ActiveLevelData.LevelName : "aktif seviye";
                    bool proceed = EditorUtility.DisplayDialog(
                        "Kaydedilmemiş Değişiklikler Var",
                        $"Sahnedeki '{activeLevelName}' için henüz seviyeye kaydedilmemiş canlı ayar değişiklikleri var. " +
                        $"'{m_SelectedLevel.LevelName}' seviyesi sahnede inşa edilirse bu değişiklikler kaybolur.\n\n" +
                        "Yine de devam edilsin mi?",
                        "Evet, Değişiklikleri Kaybet ve Devam Et",
                        "Vazgeç");
                    if (!proceed) return;
                }

                gen.LoadLevel(m_SelectedLevel);
                SceneView.RepaintAll();
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected();
                }
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log($"<color=#00FFAA><b>[LevelDesigner]</b></color> '{m_SelectedLevel.LevelName}' sahnede başarıyla inşa edildi!");
            }
        }

        #endregion
    }
}
