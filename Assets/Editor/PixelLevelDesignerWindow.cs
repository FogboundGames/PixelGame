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

        private bool m_WagonGridViewMode = true;
        private int m_GridColumnsPerRow = 4;

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

                // Denge Durumu Rozeti
                if (totalCubes > 0)
                {
                    if (cap == totalCubes)
                    {
                        GUI.contentColor = new Color(0.2f, 0.95f, 0.35f);
                        EditorGUILayout.LabelField("🟢 %100 Dengeli", EditorStyles.miniBoldLabel);
                    }
                    else if (cap < totalCubes)
                    {
                        GUI.contentColor = new Color(1f, 0.35f, 0.35f);
                        EditorGUILayout.LabelField($"🔴 Eksik (-{totalCubes - cap} küp)", EditorStyles.miniBoldLabel);
                    }
                    else
                    {
                        GUI.contentColor = new Color(0.35f, 0.75f, 1f);
                        EditorGUILayout.LabelField($"🔵 Fazla (+{cap - totalCubes})", EditorStyles.miniBoldLabel);
                    }
                    GUI.contentColor = Color.white;
                }
                else
                {
                    EditorGUILayout.LabelField("⚠️ Palet Boş", EditorStyles.miniLabel);
                }

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

            GUI.backgroundColor = new Color(0.95f, 0.9f, 0.7f);
            if (GUILayout.Button("🎯 Zorluğa Göre Sırala (Az Küp → Çok Küp)", GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog(
                    "Zorluğa Göre Sırala",
                    "Tüm bölümler, palet içindeki toplam küp sayısına göre (kolay → zor) yeniden sıralanacak ve numaralandırılacak. Devam edilsin mi?",
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
            m_SelectedLevel.CubeFrontTiltAngle = EditorGUILayout.Slider("Küp Ön Yüz Eğim Açısı", m_SelectedLevel.CubeFrontTiltAngle, 0f, 45f);
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

            string difficultyLabel;
            Color diffColor;

            if (totalCubes < 180)
            {
                difficultyLabel = "🟢 Başlangıç Seviyesi (Kolay)";
                diffColor = new Color(0.2f, 0.9f, 0.35f);
            }
            else if (totalCubes < 380)
            {
                difficultyLabel = "🟡 Standart Seviye (Orta)";
                diffColor = new Color(0.95f, 0.8f, 0.2f);
            }
            else if (totalCubes < 650)
            {
                difficultyLabel = "🔴 Zor Seviye (Yüksek Küp Sayısı)";
                diffColor = new Color(1f, 0.4f, 0.3f);
            }
            else
            {
                difficultyLabel = "🟣 Master / Uzun Bölüm";
                diffColor = new Color(0.85f, 0.4f, 1f);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Toplam Küp: {totalCubes}", EditorStyles.boldLabel, GUILayout.Width(140));
            EditorGUILayout.LabelField($"Renk Çeşidi: {colorCount}", EditorStyles.boldLabel, GUILayout.Width(130));
            EditorGUILayout.LabelField($"Gerekli Vagon: ~{reqWagons}", EditorStyles.boldLabel, GUILayout.Width(140));

            GUI.contentColor = diffColor;
            EditorGUILayout.LabelField(difficultyLabel, EditorStyles.boldLabel);
            GUI.contentColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox($"Öneri: Bu seviye için vagon kapasitesini {Mathf.Clamp(totalCubes / Mathf.Max(1, colorCount * 2), 12, 32)} civarında tutarak oyun akışını dengeli yapabilirsiniz.", MessageType.None);

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
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🛤️ Vagon Düzeni (Slotlar, Havuz ve Kapasite)", EditorStyles.boldLabel);

            m_SelectedLevel.SlotCount = EditorGUILayout.IntSlider(
                new GUIContent("Raydaki Vagon Sayısı", "Ray üzerinde aynı anda kaç vagon doldurulabilir."),
                m_SelectedLevel.SlotCount, 1, 8
            );

            m_SelectedLevel.PoolColumns = EditorGUILayout.IntSlider(
                new GUIContent("Havuz: Yan Yana", "Havuzda yan yana kaç vagon beklesin"),
                m_SelectedLevel.PoolColumns, 1, 8
            );

            m_SelectedLevel.PoolRows = EditorGUILayout.IntSlider(
                new GUIContent("Havuz: Sıra Sayısı", "Kaç sıra halinde gelsinler."),
                m_SelectedLevel.PoolRows, 1, 5
            );

            m_SelectedLevel.TruckCapacity = EditorGUILayout.IntSlider(
                new GUIContent("Vagon Kapasitesi", "Bir vagonun kasasına kaç küp sığar"),
                m_SelectedLevel.TruckCapacity, 1, 64
            );

            DrawTruckSummary();
            DrawWagonSequenceSection();

            EditorGUILayout.EndVertical();
        }

        private void DrawTruckSummary()
        {
            int required = m_SelectedLevel.GetRequiredTruckCount();
            if (m_SelectedLevel.ColorPalette.Count == 0)
            {
                EditorGUILayout.HelpBox("Renk paleti boş. Önce paleti çıkarmanız gerekir.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField($"Bu bölüm toplam ~{required} vagon gerektiriyor ({m_SelectedLevel.ColorPalette.Count} renk).", EditorStyles.miniLabel);

            if (m_SelectedLevel.PoolPlaceCount < m_SelectedLevel.SlotCount)
            {
                EditorGUILayout.HelpBox("Havuzdaki vagon sayısı raydaki yer sayısından az. Oyuncu tüm yerleri dolduramaz.", MessageType.Warning);
            }
        }

        private void DrawWagonSequenceSection()
        {
            if (m_SelectedLevel == null) return;

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Başlık
            EditorGUILayout.BeginHorizontal();
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.2f, 0.85f, 1f) }
            };
            EditorGUILayout.LabelField("🚚 Manuel Vagon & Maden Arabası Sıra Tasarımı", headerStyle);

            EditorGUI.BeginChangeCheck();
            bool useCustom = EditorGUILayout.ToggleLeft("⚡ Manuel Sıra Aktif", m_SelectedLevel.UseCustomWagonSequence, EditorStyles.boldLabel, GUILayout.Width(170));
            if (EditorGUI.EndChangeCheck())
            {
                m_SelectedLevel.UseCustomWagonSequence = useCustom;
                if (useCustom && (m_SelectedLevel.WagonSequence == null || m_SelectedLevel.WagonSequence.Count == 0))
                {
                    m_SelectedLevel.GenerateInterleavedWagonSequenceFromPalette();
                }
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // AI vs Sıfırdan Tuval Butonları
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.95f, 0.65f, 0.15f);
            if (GUILayout.Button("🤖 AI İle Akıllı Sıra Tasarla (Round-Robin)", GUILayout.Height(32)))
            {
                Undo.RecordObject(m_SelectedLevel, "AI Generate Smart Wagon Sequence");
                m_SelectedLevel.GenerateInterleavedWagonSequenceFromPalette();
                m_SelectedLevel.UseCustomWagonSequence = true;
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }

            GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
            if (GUILayout.Button("✨ Sıfırdan Boş Tuval Başlat", GUILayout.Height(32)))
            {
                if (EditorUtility.DisplayDialog("Sıfırdan Boş Tuval", "Vagon sırası temizlenip boş bir alan oluşturulacak. Onaylıyor musunuz?", "Evet", "İptal"))
                {
                    Undo.RecordObject(m_SelectedLevel, "Start Blank Wagon Sequence");
                    m_SelectedLevel.WagonSequence.Clear();
                    AddWagonRow(m_GridColumnsPerRow);
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // 1. AI Level Asistanı & Canlı Analizör
            DrawAIAssistantSection();

            EditorGUILayout.Space(6);

            // 2. Denge Paneli (Dashboard)
            DrawBalanceDashboard();

            EditorGUILayout.Space(6);

            // 3. 2D Izgara Matrisi
            Draw2DGridWagonMatrixSection();

            EditorGUILayout.EndVertical();
        }

        private void DrawAIAssistantSection()
        {
            if (m_SelectedLevel == null) return;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            GUIStyle aiHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.95f, 0.75f, 0.2f) }
            };
            EditorGUILayout.LabelField("🤖 Yapay Zeka (AI) Level Asistanı & Analizör", aiHeaderStyle);

            GUI.backgroundColor = new Color(0.9f, 0.6f, 0.1f);
            if (GUILayout.Button("⚡ Eksikleri Tamamla (Auto-Balance)", GUILayout.Width(220), GUILayout.Height(22)))
            {
                AutoBalanceMissingWagons();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            int totalCubes = m_SelectedLevel.GetTotalCubeCountInPalette();
            int totalCap = m_SelectedLevel.GetTotalWagonCapacity();

            List<string> missingColorsInfo = new List<string>();
            int unassignedTotal = 0;

            if (m_SelectedLevel.ColorPalette != null)
            {
                foreach (var entry in m_SelectedLevel.ColorPalette)
                {
                    if (entry == null || entry.pixelCount <= 0) continue;
                    Color c = entry.targetColor;
                    int assigned = m_SelectedLevel.GetTotalAssignedCapacityForColor(c);
                    int diff = entry.pixelCount - assigned;
                    string cName = string.IsNullOrEmpty(entry.label) ? "Renk" : entry.label;

                    if (diff > 0)
                    {
                        missingColorsInfo.Add($"{cName} ({diff} küp)");
                        unassignedTotal += diff;
                    }
                }
            }

            string aiStatusMessage;
            MessageType aiMsgType;

            if (totalCap == totalCubes && totalCubes > 0 && missingColorsInfo.Count == 0)
            {
                aiStatusMessage = $"AI Asistan: \"'{m_SelectedLevel.LevelName}' bölümü TAM DENGEDE! Toplam {totalCubes} küp için {totalCap} vagon kapasitesi mevcut. Oyuncular takılmadan bitirebilir.\"";
                aiMsgType = MessageType.Info;
            }
            else if (missingColorsInfo.Count > 0)
            {
                string missingListStr = string.Join(", ", missingColorsInfo);
                aiStatusMessage = $"AI Uyarısı: Kırılması gereken {totalCubes} küp var ancak vagonların kapasitesi {totalCap} ({unassignedTotal} küp eksik)!\nEksikler: {missingListStr}\n💡 Tavsiye: Yukarıdaki '⚡ Eksikleri Tamamla' butonuna basarak eksik vagonları otomatik ekleyebilirsiniz.";
                aiMsgType = MessageType.Warning;
            }
            else
            {
                aiStatusMessage = $"AI Bilgisi: Toplam {totalCubes} küp için vagon kapasitesi {totalCap} (+{totalCap - totalCubes} fazla kapasite).";
                aiMsgType = MessageType.Info;
            }

            EditorGUILayout.HelpBox(aiStatusMessage, aiMsgType);
            EditorGUILayout.EndVertical();
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

            foreach (var entry in m_SelectedLevel.ColorPalette)
            {
                if (entry == null || entry.pixelCount <= 0) continue;
                Color c = entry.targetColor;
                int assigned = m_SelectedLevel.GetTotalAssignedCapacityForColor(c);
                int diff = entry.pixelCount - assigned;

                while (diff > 0)
                {
                    int thisCap = Mathf.Min(capPerWagon, diff);
                    string lbl = string.IsNullOrEmpty(entry.label) ? $"Vagon #{m_SelectedLevel.WagonSequence.Count + 1}" : $"{entry.label} (#{m_SelectedLevel.WagonSequence.Count + 1})";
                    m_SelectedLevel.WagonSequence.Add(new WagonSequenceEntry(c, thisCap, 0, lbl));
                    diff -= thisCap;
                }
            }

            m_SelectedLevel.UseCustomWagonSequence = true;
            EditorUtility.SetDirty(m_SelectedLevel);
            NotifyLiveSceneUpdate();
        }

        private void DrawBalanceDashboard()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("📊 Level Küp & Vagon Kapasite Denge Paneli", EditorStyles.boldLabel);

            int totalCubes = m_SelectedLevel.GetTotalCubeCountInPalette();
            int totalCapacity = m_SelectedLevel.GetTotalWagonCapacity();
            int totalWagonCount = m_SelectedLevel.WagonSequence != null ? m_SelectedLevel.WagonSequence.Count : 0;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Total Küp: {totalCubes}", EditorStyles.boldLabel, GUILayout.Width(130));
            EditorGUILayout.LabelField($"Vagon Kapasitesi: {totalCapacity}", EditorStyles.boldLabel, GUILayout.Width(160));
            EditorGUILayout.LabelField($"Vagon Sayısı: {totalWagonCount}", EditorStyles.boldLabel, GUILayout.Width(120));

            if (totalCapacity == totalCubes && totalCubes > 0)
            {
                GUI.backgroundColor = new Color(0.2f, 0.9f, 0.3f);
                GUILayout.Box("🟢 %100 DENGELİ", EditorStyles.boldLabel, GUILayout.Height(20));
            }
            else if (totalCapacity < totalCubes)
            {
                GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
                GUILayout.Box($"🔴 EKSİK (-{totalCubes - totalCapacity} Küp)", EditorStyles.boldLabel, GUILayout.Height(20));
            }
            else
            {
                GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
                GUILayout.Box($"🔵 FAZLA (+{totalCapacity - totalCubes})", EditorStyles.boldLabel, GUILayout.Height(20));
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // Renk Bazlı Satırlar
            if (m_SelectedLevel.ColorPalette != null && m_SelectedLevel.ColorPalette.Count > 0)
            {
                EditorGUILayout.Space(4);
                foreach (var entry in m_SelectedLevel.ColorPalette)
                {
                    if (entry == null || entry.pixelCount <= 0) continue;
                    Color c = entry.targetColor;
                    int assignedCap = m_SelectedLevel.GetTotalAssignedCapacityForColor(c);
                    int requiredCubes = entry.pixelCount;
                    string colorName = string.IsNullOrEmpty(entry.label) ? "Renk" : entry.label;

                    EditorGUILayout.BeginHorizontal();
                    Rect r = EditorGUILayout.GetControlRect(false, 18, GUILayout.Width(22));
                    EditorGUI.DrawRect(r, c);

                    EditorGUILayout.LabelField(colorName, EditorStyles.boldLabel, GUILayout.Width(110));
                    EditorGUILayout.LabelField($"Kırılacak: {requiredCubes} Küp", GUILayout.Width(130));
                    EditorGUILayout.LabelField($"Vagon Kapasitesi: {assignedCap}", GUILayout.Width(140));

                    if (assignedCap == requiredCubes)
                    {
                        GUI.contentColor = new Color(0.1f, 0.8f, 0.2f);
                        EditorGUILayout.LabelField("✓ Tam", EditorStyles.boldLabel, GUILayout.Width(90));
                    }
                    else if (assignedCap < requiredCubes)
                    {
                        GUI.contentColor = new Color(1f, 0.2f, 0.2f);
                        EditorGUILayout.LabelField($"⚠ Eksik (-{requiredCubes - assignedCap})", EditorStyles.boldLabel, GUILayout.Width(90));
                    }
                    else
                    {
                        GUI.contentColor = new Color(0.2f, 0.6f, 1f);
                        EditorGUILayout.LabelField($"ℹ Fazla (+{assignedCap - requiredCubes})", EditorStyles.boldLabel, GUILayout.Width(90));
                    }
                    GUI.contentColor = Color.white;

                    GUI.backgroundColor = new Color(0.3f, 0.85f, 0.5f);
                    if (GUILayout.Button("➕ Vagon Ekle", GUILayout.Width(95), GUILayout.Height(18)))
                    {
                        Undo.RecordObject(m_SelectedLevel, "Add Wagon for Color");
                        int defaultCap = Mathf.Min(m_SelectedLevel.TruckCapacity, Mathf.Max(1, requiredCubes - assignedCap));
                        m_SelectedLevel.WagonSequence.Add(new WagonSequenceEntry(c, defaultCap, 0, $"{colorName} (#{m_SelectedLevel.WagonSequence.Count + 1})"));
                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                    }
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void Draw2DGridWagonMatrixSection()
        {
            if (m_SelectedLevel == null) return;
            if (m_SelectedLevel.WagonSequence == null) m_SelectedLevel.WagonSequence = new List<WagonSequenceEntry>();

            var sequence = m_SelectedLevel.WagonSequence;

            // Paletten Hızlı Vagon Ekleme Butonları
            DrawQuickPaletteWagonAdder();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginVertical("box");

            // Izgara Kontrol Başlığı
            EditorGUILayout.BeginHorizontal();
            GUIStyle subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.2f, 0.85f, 1f) }
            };
            EditorGUILayout.LabelField("🎛️ 2D Izgara Matrisi (Vagon & Dalga Tasarımı):", subHeaderStyle, GUILayout.Width(350));

            EditorGUILayout.LabelField("Satır Başına Kolon:", GUILayout.Width(115));
            m_GridColumnsPerRow = EditorGUILayout.IntSlider(m_GridColumnsPerRow, 2, 6, GUILayout.Width(130));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(m_WagonGridViewMode ? "📋 Düz Liste Modu" : "🎛️ 2D Grid Modu", GUILayout.Width(130), GUILayout.Height(22)))
            {
                m_WagonGridViewMode = !m_WagonGridViewMode;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (!m_WagonGridViewMode)
            {
                DrawWagonFlatListEditor();
                EditorGUILayout.EndVertical();
                return;
            }

            int colCount = Mathf.Clamp(m_GridColumnsPerRow, 2, 6);
            int totalWagons = sequence.Count;
            int rowCount = Mathf.Max(0, Mathf.CeilToInt((float)totalWagons / colCount));

            for (int r = 0; r < rowCount; r++)
            {
                int startIdx = r * colCount;
                int endIdx = Mathf.Min(totalWagons, (r + 1) * colCount);

                EditorGUILayout.BeginVertical("box");

                // Satır Başlığı ve Butonları
                EditorGUILayout.BeginHorizontal();
                GUIStyle rowHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = new Color(0.3f, 0.8f, 1f) }
                };
                EditorGUILayout.LabelField($"📦 Satır #{r + 1} (Dalga #{r + 1}) — Vagonlar #{startIdx + 1} .. #{endIdx}", rowHeaderStyle);

                GUILayout.FlexibleSpace();

                // Yukarı Taşı
                GUI.enabled = r > 0;
                if (GUILayout.Button("⬆️ Yukarı", GUILayout.Width(75), GUILayout.Height(20)))
                {
                    MoveWagonRow(r, r - 1, colCount);
                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                // Aşağı Taşı
                GUI.enabled = r < rowCount - 1;
                if (GUILayout.Button("⬇️ Aşağı", GUILayout.Width(75), GUILayout.Height(20)))
                {
                    MoveWagonRow(r, r + 1, colCount);
                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                GUI.enabled = true;

                // Satıra Vagon Ekle
                GUI.backgroundColor = new Color(0.3f, 0.85f, 0.5f);
                if (GUILayout.Button("➕ Vagon Ekle", GUILayout.Width(95), GUILayout.Height(20)))
                {
                    Undo.RecordObject(m_SelectedLevel, "Add Wagon to Row");
                    Color defaultColor = (m_SelectedLevel.ColorPalette.Count > 0) ? m_SelectedLevel.ColorPalette[endIdx % m_SelectedLevel.ColorPalette.Count].targetColor : Color.yellow;
                    sequence.Insert(endIdx, new WagonSequenceEntry(defaultColor, m_SelectedLevel.TruckCapacity, 0, $"Vagon #{endIdx + 1}"));
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                // Satırı Sil
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("🗑️ Satırı Sil", GUILayout.Width(85), GUILayout.Height(20)))
                {
                    DeleteWagonRow(r, colCount);
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);

                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < colCount; c++)
                {
                    int index = startIdx + c;
                    if (index >= totalWagons)
                    {
                        GUILayout.FlexibleSpace();
                        continue;
                    }

                    var wagon = sequence[index];
                    if (wagon == null) continue;

                    EditorGUILayout.BeginVertical("box", GUILayout.Width(190));

                    // Üst: Rozet & Label & Renk
                    EditorGUILayout.BeginHorizontal();
                    Rect badgeRect = EditorGUILayout.GetControlRect(false, 20, GUILayout.Width(28));
                    EditorGUI.DrawRect(badgeRect, wagon.wagonColor);
                    GUIStyle numStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = (wagon.wagonColor.grayscale > 0.5f) ? Color.black : Color.white }
                    };
                    GUI.Label(badgeRect, $"#{index + 1}", numStyle);

                    wagon.label = EditorGUILayout.TextField(wagon.label, GUILayout.Width(90));

                    Color newColor = EditorGUILayout.ColorField(GUIContent.none, wagon.wagonColor, true, false, false, GUILayout.Width(40));
                    if (newColor != wagon.wagonColor)
                    {
                        wagon.wagonColor = newColor;
                        m_SelectedLevel.UseCustomWagonSequence = true;
                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                    }
                    EditorGUILayout.EndHorizontal();

                    // Orta: Kapasite
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Kap:", GUILayout.Width(30));

                    if (GUILayout.Button("-1", GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        wagon.capacity = Mathf.Max(1, wagon.capacity - 1);
                        m_SelectedLevel.UseCustomWagonSequence = true;
                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                    }

                    int newCap = EditorGUILayout.IntField(wagon.capacity, GUILayout.Width(35));
                    if (newCap != wagon.capacity)
                    {
                        wagon.capacity = Mathf.Max(1, newCap);
                        m_SelectedLevel.UseCustomWagonSequence = true;
                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                    }

                    if (GUILayout.Button("+1", GUILayout.Width(24), GUILayout.Height(18)))
                    {
                        wagon.capacity += 1;
                        m_SelectedLevel.UseCustomWagonSequence = true;
                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                    }
                    if (GUILayout.Button("+5", GUILayout.Width(24), GUILayout.Height(18)))
                    {
                        wagon.capacity += 5;
                        m_SelectedLevel.UseCustomWagonSequence = true;
                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                    }
                    EditorGUILayout.EndHorizontal();

                    // Alt: Yön (◄ ▲ ▼ ►) & Sil
                    EditorGUILayout.BeginHorizontal();

                    GUI.enabled = index > 0;
                    if (GUILayout.Button("◄", GUILayout.Width(22), GUILayout.Height(20)))
                    {
                        (sequence[index], sequence[index - 1]) = (sequence[index - 1], sequence[index]);
                        m_SelectedLevel.UseCustomWagonSequence = true;
                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                    }

                    GUI.enabled = index >= colCount;
                    if (GUILayout.Button("▲", GUILayout.Width(22), GUILayout.Height(20)))
                    {
                        (sequence[index], sequence[index - colCount]) = (sequence[index - colCount], sequence[index]);
                        m_SelectedLevel.UseCustomWagonSequence = true;
                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                    }

                    GUI.enabled = index + colCount < totalWagons;
                    if (GUILayout.Button("▼", GUILayout.Width(22), GUILayout.Height(20)))
                    {
                        (sequence[index], sequence[index + colCount]) = (sequence[index + colCount], sequence[index]);
                        m_SelectedLevel.UseCustomWagonSequence = true;
                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                    }

                    GUI.enabled = index < totalWagons - 1;
                    if (GUILayout.Button("►", GUILayout.Width(22), GUILayout.Height(20)))
                    {
                        (sequence[index], sequence[index + 1]) = (sequence[index + 1], sequence[index]);
                        m_SelectedLevel.UseCustomWagonSequence = true;
                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                    }
                    GUI.enabled = true;

                    GUILayout.FlexibleSpace();

                    GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(20)))
                    {
                        sequence.RemoveAt(index);
                        m_SelectedLevel.UseCustomWagonSequence = true;
                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        break;
                    }
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            // Alt Eylem Butonları
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
            if (GUILayout.Button($"➕ Yeni Satır (Dalga #{rowCount + 1}) Ekle ({colCount} Vagon)", GUILayout.Height(28)))
            {
                AddWagonRow(colCount);
            }

            GUI.backgroundColor = new Color(0.9f, 0.6f, 0.1f);
            if (GUILayout.Button("🧠 AI İle Sırayı Yeniden Dengele", GUILayout.Height(28)))
            {
                Undo.RecordObject(m_SelectedLevel, "AI Rebalance Sequence");
                m_SelectedLevel.GenerateInterleavedWagonSequenceFromPalette();
                m_SelectedLevel.UseCustomWagonSequence = true;
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }

            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("🧹 Sırayı Sıfırla", GUILayout.Width(110), GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Vagon Sırasını Temizle", "Tüm manuel vagon sırasını silmek istediğinize emin misiniz?", "Evet", "Hayır"))
                {
                    Undo.RecordObject(m_SelectedLevel, "Clear Wagon Sequence");
                    sequence.Clear();
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawQuickPaletteWagonAdder()
        {
            if (m_SelectedLevel == null || m_SelectedLevel.ColorPalette == null || m_SelectedLevel.ColorPalette.Count == 0) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("🎨 Paletten Hızlı Vagon Ekle:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            foreach (var entry in m_SelectedLevel.ColorPalette)
            {
                if (entry == null || entry.pixelCount <= 0) continue;
                Color c = entry.targetColor;
                string colorName = string.IsNullOrEmpty(entry.label) ? "Renk" : entry.label;

                GUI.backgroundColor = c;
                GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 11,
                    normal = { textColor = (c.grayscale > 0.5f) ? Color.black : Color.white }
                };

                if (GUILayout.Button($"➕ {colorName}", btnStyle, GUILayout.Height(24), GUILayout.MinWidth(80)))
                {
                    Undo.RecordObject(m_SelectedLevel, "Add Palette Wagon");
                    int cap = Mathf.Max(1, m_SelectedLevel.TruckCapacity);
                    m_SelectedLevel.WagonSequence.Add(new WagonSequenceEntry(c, cap, 0, $"{colorName} (#{m_SelectedLevel.WagonSequence.Count + 1})"));
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawWagonFlatListEditor()
        {
            var sequence = m_SelectedLevel.WagonSequence;
            EditorGUILayout.LabelField("📋 Vagon Geliş Sırası (Sahnede Bu Sırayla Gelirler):", EditorStyles.boldLabel);

            for (int i = 0; i < sequence.Count; i++)
            {
                var wagon = sequence[i];
                if (wagon == null) continue;

                EditorGUILayout.BeginHorizontal("box");

                Rect badgeRect = EditorGUILayout.GetControlRect(false, 22, GUILayout.Width(36));
                EditorGUI.DrawRect(badgeRect, wagon.wagonColor);
                GUIStyle numStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = (wagon.wagonColor.grayscale > 0.5f) ? Color.black : Color.white }
                };
                GUI.Label(badgeRect, $"#{i + 1}", numStyle);

                wagon.label = EditorGUILayout.TextField(wagon.label, GUILayout.Width(110));

                Color newColor = EditorGUILayout.ColorField(GUIContent.none, wagon.wagonColor, true, false, false, GUILayout.Width(50));
                if (newColor != wagon.wagonColor)
                {
                    wagon.wagonColor = newColor;
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }

                EditorGUILayout.LabelField("Kapasite:", GUILayout.Width(55));

                if (GUILayout.Button("-5", GUILayout.Width(24), GUILayout.Height(20)))
                {
                    wagon.capacity = Mathf.Max(1, wagon.capacity - 5);
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
                if (GUILayout.Button("-1", GUILayout.Width(24), GUILayout.Height(20)))
                {
                    wagon.capacity = Mathf.Max(1, wagon.capacity - 1);
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }

                int newCap = EditorGUILayout.IntField(wagon.capacity, GUILayout.Width(40));
                if (newCap != wagon.capacity)
                {
                    wagon.capacity = Mathf.Max(1, newCap);
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }

                if (GUILayout.Button("+1", GUILayout.Width(26), GUILayout.Height(20)))
                {
                    wagon.capacity += 1;
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
                if (GUILayout.Button("+5", GUILayout.Width(26), GUILayout.Height(20)))
                {
                    wagon.capacity += 5;
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }

                GUILayout.FlexibleSpace();

                GUI.enabled = i > 0;
                if (GUILayout.Button("▲", GUILayout.Width(24), GUILayout.Height(20)))
                {
                    (sequence[i], sequence[i - 1]) = (sequence[i - 1], sequence[i]);
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }

                GUI.enabled = i < sequence.Count - 1;
                if (GUILayout.Button("▼", GUILayout.Width(24), GUILayout.Height(20)))
                {
                    (sequence[i], sequence[i + 1]) = (sequence[i + 1], sequence[i]);
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
                GUI.enabled = true;

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(20)))
                {
                    sequence.RemoveAt(i);
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
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

            AssetDatabase.CreateAsset(newLevel, assetPath);
            AssetDatabase.SaveAssets();

            RefreshLevelList();
            SelectLevel(newLevel);

            Debug.Log($"<color=#00FFAA><b>[LevelDesigner]</b></color> Görselden yeni seviye otomatik üretildi: {assetPath}");
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

        /// <summary>
        /// Bölümleri paletteki toplam küp sayısına (basit bir zorluk göstergesine) göre kolaydan
        /// zora sıralar ve numaralandırır. Oyuncunun kolay bölümlerle başlayıp giderek zorlaşan
        /// bir ilerleme yaşaması için önerilen sırayı otomatik kurar.
        /// </summary>
        private void SortLevelsByDifficulty()
        {
            m_AllLevels.Sort((a, b) =>
            {
                int cubesA = a != null ? a.GetTotalCubeCountInPalette() : 0;
                int cubesB = b != null ? b.GetTotalCubeCountInPalette() : 0;
                return cubesA.CompareTo(cubesB);
            });

            AutoRenumberLevels();
            Debug.Log("<color=#00FFAA><b>[LevelDesigner]</b></color> Bölümler zorluğa (küp sayısına) göre yeniden sıralandı.");
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
