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
        private int m_SelectedSlotForLink = -1;
        private readonly LevelDifficultyTableView m_DifficultyTable = new LevelDifficultyTableView();
        private int m_GenLinkPercent = 20;
        private int m_GenHiddenPercent = 15;
        private string m_LastSmartStatusMessage = "";

        public enum WagonDifficultyMode
        {
            Easy = 0,    // 🟢 Kolay (Bol açık renk, risksiz akış)
            Medium = 1,  // 🟡 Dengeli (Standart bulmaca deneyimi)
            Hard = 2     // 🔴 Zor (Taktiksel katman kilitleri, yüksek slot baskısı)
        }
        private WagonDifficultyMode m_WagonDifficulty = WagonDifficultyMode.Medium;
        private int m_TargetColorCount = 5;

        public enum DetailTab
        {
            SimpleMode = 0,    // ⚡ Kolay Mod (Gemi & Hızlı)
            LevelSetup = 1,    // 📋 Bölüm & Izgara
            ColorStudio = 2,   // 🎨 Piksel Renkleri & TCP2 Toon
            MysteryCubes = 3,  // ❓ Gizli Küpler
            TruckLayout = 4,   // 🚢 Gemi / Vagon Sıra Düzeni
            SceneTools = 5,    // 🛠️ Sahne & Görsel Araçları
            DifficultyTable = 6 // 📊 Bütün level'ların zorluk tablosu
        }

        private enum MysteryBrushMode { Paint = 0, Erase = 1, Toggle = 2 }
        private MysteryBrushMode m_MysteryBrushMode = MysteryBrushMode.Paint;
        private int m_MysteryRandomPercent = 25;
        private int m_MysteryColorIndex = 0;
        private Vector2 m_MysteryGridScroll = Vector2.zero;
        private Texture2D m_MysteryQuestionIconTex;

        private DetailTab m_CurrentTab = DetailTab.SimpleMode;
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
            m_CurrentTab = (DetailTab)EditorPrefs.GetInt("PixelGame_SelectedDetailTab", (int)DetailTab.SimpleMode);
            m_GenLinkPercent = EditorPrefs.GetInt("PixelGame_GenLinkPercent", 20);
            m_GenHiddenPercent = EditorPrefs.GetInt("PixelGame_GenHiddenPercent", 15);
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

            EditorGUILayout.Space(2);

            GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
            if (GUILayout.Button("📁 Toplu Seviye Üreticisi (Batch Generator)", GUILayout.Height(24)))
            {
                BatchLevelGeneratorWindow.OpenWindow();
            }
            GUI.backgroundColor = Color.white;

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

                // Zorluk Rozeti (resmin renk dağılımından; Zorluk Tablosu ile aynı) ve Denge Durumu
                var diffRep = LevelDifficultyReport.GetCached(level);

                EditorGUILayout.BeginHorizontal();
                if (diffRep.valid)
                {
                    GUI.contentColor = LevelDifficultyReport.TierColor(diffRep.tier);
                    EditorGUILayout.LabelField($"{LevelDifficultyReport.TierLabel(diffRep.tier)} ({Mathf.RoundToInt(diffRep.score)})", EditorStyles.miniBoldLabel, GUILayout.Width(110));
                }
                else
                {
                    GUI.contentColor = Color.gray;
                    EditorGUILayout.LabelField("⚪ Belirsiz", EditorStyles.miniBoldLabel, GUILayout.Width(110));
                }
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
                // Not: rowRect yalnızca son kontrolün (butonların) alanıdır; satırın tüm genişliğini kapsasın diye genişletiyoruz.
                Rect dropRect = new Rect(0f, rowRect.y, position.width, Mathf.Max(rowRect.height, 20f));
                if (m_DraggingLevel != null && Event.current.type == EventType.MouseUp && dropRect.Contains(Event.current.mousePosition))
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

                // 3. Kolay Mod & Hızlı Eylem Butonları
                EditorGUILayout.BeginVertical(GUILayout.Width(76));

                GUI.backgroundColor = new Color(0.25f, 0.85f, 0.5f);
                if (GUILayout.Button(new GUIContent("🎨 Kolay", "Bu bölümü doğrudan Kolay Modda (Tuvalde Çizim) aç"), EditorStyles.miniButton, GUILayout.Width(74), GUILayout.Height(20)))
                {
                    SelectLevel(level);
                    m_CurrentTab = DetailTab.SimpleMode;
                    EditorPrefs.SetInt("PixelGame_SelectedDetailTab", (int)m_CurrentTab);
                    LevelBuilderWindow.OpenForLevel(level);
                    return;
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.BeginHorizontal();
                // Yukarı Taşı
                GUI.enabled = (i > 0);
                if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(18), GUILayout.Height(18)))
                {
                    MoveLevel(i, i - 1);
                    return;
                }
                // Aşağı Taşı
                GUI.enabled = (i < m_AllLevels.Count - 1);
                if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(18), GUILayout.Height(18)))
                {
                    MoveLevel(i, i + 1);
                    return;
                }
                GUI.enabled = true;

                // Çoğalt (Duplicate)
                GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
                if (GUILayout.Button("📋", EditorStyles.miniButtonMid, GUILayout.Width(18), GUILayout.Height(18)))
                {
                    DuplicateLevel(level);
                    return;
                }
                // Sil (Delete)
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("🗑️", EditorStyles.miniButtonRight, GUILayout.Width(18), GUILayout.Height(18)))
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

            EditorGUILayout.Space(2);

            GUI.backgroundColor = new Color(0.85f, 0.8f, 1f);
            if (GUILayout.Button("🏷️ Kademeleri Analizden Ata (Kolay / Orta / Zor)", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog(
                    "Kademeleri Analizden Ata",
                    "Her bölüm için Zorluk Raporu (LevelDifficultyReport) yeniden hesaplanacak ve sonuç " +
                    "kalıcı olarak level asset'ine yazılacak (Kolay / Orta / Zor). HUD'daki HARD rozeti " +
                    "artık bu kademeden okunur.\n\nDevam edilsin mi?",
                    "Evet, Ata", "Vazgeç"))
                {
                    AssignDifficultyTiersFromAnalysis();
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
                            if (draggedObject is DefaultAsset folderAsset)
                            {
                                BatchLevelGeneratorWindow.OpenWithFolder(folderAsset);
                                break;
                            }
                            else if (draggedObject is Texture2D tex)
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

            // Zorluk tablosu: seçili level gerekmez; satıra tıklayınca o level seçilir
            if (m_CurrentTab == DetailTab.DifficultyTable)
            {
                EditorGUILayout.Space(6);
                DrawTabsToolbar();
                EditorGUILayout.Space(6);
                m_DifficultyTable.OnGUI(level =>
                {
                    SelectLevel(level);
                    m_CurrentTab = DetailTab.TruckLayout;
                    EditorPrefs.SetInt("PixelGame_SelectedDetailTab", (int)m_CurrentTab);
                }, m_SelectedLevel);
                EditorGUILayout.EndVertical();
                return;
            }

            // Eğer Sahne Araçları sekmesi seçiliyse: Navbar'ı daima üstte göster, asla kaybolmasın!
            if (m_CurrentTab == DetailTab.SceneTools)
            {
                EditorGUILayout.Space(6);
                if (m_SelectedLevel != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUI.backgroundColor = new Color(0.25f, 0.75f, 1f);
                    if (GUILayout.Button($"◀ {m_SelectedLevel.LevelName} Düzenleyicisine Geri Dön", GUILayout.Height(26)))
                    {
                        m_CurrentTab = DetailTab.LevelSetup;
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        return;
                    }
                    GUI.backgroundColor = Color.white;
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(4);
                }

                DrawTabsToolbar();
                EditorGUILayout.Space(6);
                DrawSceneToolsTab();
                EditorGUILayout.EndVertical();
                return;
            }

            if (m_SelectedLevel == null)
            {
                EditorGUILayout.Space(6);
                DrawTabsToolbar();
                EditorGUILayout.Space(30);
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

            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.5f);
            if (GUILayout.Button("🎨 Kolay Mod (Tuval)", GUILayout.Width(135), GUILayout.Height(24)))
            {
                m_CurrentTab = DetailTab.SimpleMode;
                EditorPrefs.SetInt("PixelGame_SelectedDetailTab", (int)m_CurrentTab);
                LevelBuilderWindow.OpenForLevel(m_SelectedLevel);
            }

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

            // 3. Sekme Seçimi (Tüm sekmeler daima görünür)
            DrawTabsToolbar();

            EditorGUILayout.Space(8);

            EditorGUI.BeginChangeCheck();

            if (m_CurrentTab == DetailTab.SimpleMode)
            {
                DrawSimpleModeCard();
            }
            else if (m_CurrentTab == DetailTab.LevelSetup)
            {
                DrawLevelSetupTab();
            }
            else if (m_CurrentTab == DetailTab.ColorStudio)
            {
                DrawColorStudioTab();
            }
            else if (m_CurrentTab == DetailTab.MysteryCubes)
            {
                DrawMysteryCubesTab();
            }
            else if (m_CurrentTab == DetailTab.TruckLayout)
            {
                DrawTruckLayoutTab();
            }
            else if (m_CurrentTab == DetailTab.SceneTools)
            {
                DrawSceneToolsTab();
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

        private void DrawTabsToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            GUIStyle tabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                fixedHeight = 30
            };

            DrawTabButton(DetailTab.SimpleMode, "⚡ Kolay Mod (Gemi)", tabStyle);
            DrawTabButton(DetailTab.LevelSetup, "📋 Bölüm & Izgara", tabStyle);
            DrawTabButton(DetailTab.ColorStudio, "🎨 Piksel Renkleri & TCP2", tabStyle);
            DrawTabButton(DetailTab.MysteryCubes, "❓ Gizli Küpler", tabStyle);
            DrawTabButton(DetailTab.TruckLayout, "🚢 Gemi / Vagon Sıra Düzeni", tabStyle);
            DrawTabButton(DetailTab.SceneTools, "🛠️ Sahne Araçları", tabStyle);
            DrawTabButton(DetailTab.DifficultyTable, "📊 Zorluk Tablosu", tabStyle);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTabButton(DetailTab tab, string label, GUIStyle style)
        {
            bool isSelected = m_CurrentTab == tab;
            if (isSelected)
            {
                GUI.backgroundColor = (tab == DetailTab.SimpleMode)
                    ? new Color(0.25f, 0.85f, 0.5f)
                    : (tab == DetailTab.MysteryCubes)
                        ? new Color(1f, 0.6f, 0.2f)
                        : new Color(0.25f, 0.75f, 1f);
            }
            else
            {
                GUI.backgroundColor = new Color(0.85f, 0.85f, 0.9f);
            }

            if (GUILayout.Button(label, style))
            {
                m_CurrentTab = tab;
                EditorPrefs.SetInt("PixelGame_SelectedDetailTab", (int)m_CurrentTab);
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
                                m_SelectedLevel.OriginalSourceTexture = newT;
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
            if (GUILayout.Button("▶️ Sahnede Yükle", GUILayout.Width(115), GUILayout.Height(22)))
            {
                BuildSelectedLevelInScene();
            }
            GUI.backgroundColor = new Color(0.25f, 0.75f, 1f);
            if (GUILayout.Button("✏️ Tuvalde Düzenle", GUILayout.Width(130), GUILayout.Height(22)))
            {
                LevelBuilderWindow.OpenForLevel(m_SelectedLevel);
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

        #region SIMPLE MODE (FAST SHIP WORKFLOW)

        private void DrawSimpleModeCard()
        {
            if (m_SelectedLevel == null) return;

            // 0. Tuval Çizim & Düzenleme Aksiyonu (Kolay Mod)
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎨 Piksel Çizimi & Düzenleme:", EditorStyles.boldLabel);
            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.5f);
            if (GUILayout.Button("✏️ Bu Bölümü Tuvalde Çiz / Düzenle", GUILayout.Width(250), GUILayout.Height(28)))
            {
                LevelBuilderWindow.OpenForLevel(m_SelectedLevel);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 1. Akıllı Palet & Renk Sayısı Kutusu
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Texture2D activeTex = m_SelectedLevel.GetActiveTexture();
            int colorCount = activeTex != null ? PixelPaletteOptimizer.CountUniqueColors(activeTex) : m_SelectedLevel.ColorPalette.Count;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"🎨 Renk Durumu: {colorCount} Renk", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (colorCount > 5)
            {
                GUI.contentColor = new Color(1f, 0.65f, 0.2f);
                EditorGUILayout.LabelField("⚠ Çok renkli (Gemi için 4-5 renk önerilir)", EditorStyles.miniBoldLabel);
                GUI.contentColor = Color.white;
            }
            else
            {
                GUI.contentColor = new Color(0.35f, 0.85f, 0.4f);
                EditorGUILayout.LabelField("✓ İdeal Renk Sayısı (Kilitlenme Riski Düşük)", EditorStyles.miniBoldLabel);
                GUI.contentColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            // Renk Şeridi
            if (m_SelectedLevel.ColorPalette.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                foreach (var p in m_SelectedLevel.ColorPalette)
                {
                    Rect sw = GUILayoutUtility.GetRect(28f, 18f, GUILayout.Width(28f));
                    EditorGUI.DrawRect(sw, p.targetColor);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🎨 Renk İndirgeme ve Temizleme:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Hedef Renk:", GUILayout.Width(80));
            m_TargetColorCount = EditorGUILayout.IntSlider(m_TargetColorCount, 2, 16, GUILayout.Width(170));
            
            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
            if (GUILayout.Button($"🎨 {m_TargetColorCount} Renge İndirge", GUILayout.Height(22)))
            {
                if (PixelPaletteOptimizer.OptimizeLevelAsset(m_SelectedLevel, m_TargetColorCount))
                {
                    m_SolvReport = null;
                    NotifyLiveSceneUpdate();
                }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("🧹 Gürültü Temizle", GUILayout.Width(110), GUILayout.Height(22)))
            {
                Texture2D baseTex = m_SelectedLevel.GetOriginalTexture();
                if (baseTex != null)
                {
                    Texture2D cleaned = PixelPaletteOptimizer.CleanIsolatedPixels(baseTex);
                    if (cleaned != null)
                    {
                        Texture2D saved = PixelPaletteOptimizer.SaveAsOptimizedAsset(baseTex, cleaned, "_denoise");
                        DestroyImmediate(cleaned);
                        if (saved != null)
                        {
                            Undo.RecordObject(m_SelectedLevel, "Denoise Level");
                            m_SelectedLevel.LevelTexture = saved;
                            m_SelectedLevel.ExtractPaletteFromTexture();
                            m_SelectedLevel.GenerateInterleavedWagonSequenceFromPalette();
                            m_SolvReport = null;
                            NotifyLiveSceneUpdate();
                        }
                    }
                }
            }
            if (m_SelectedLevel.OriginalSourceTexture != null && m_SelectedLevel.OriginalSourceTexture != activeTex)
            {
                GUI.backgroundColor = new Color(0.9f, 0.9f, 0.95f);
                if (GUILayout.Button("↩ Orijinale Dön", GUILayout.Width(110), GUILayout.Height(22)))
                {
                    Undo.RecordObject(m_SelectedLevel, "Revert to Original");
                    m_SelectedLevel.RevertToOriginalTexture();
                    m_SolvReport = null;
                    NotifyLiveSceneUpdate();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            // Hızlı butonlar (3, 4, 5, 6, 7, 8)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Hızlı Butonlar:", EditorStyles.miniLabel, GUILayout.Width(80));
            int[] quickPresets = { 3, 4, 5, 6, 7, 8 };
            foreach (int q in quickPresets)
            {
                if (GUILayout.Button(q.ToString(), EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    m_TargetColorCount = q;
                    if (PixelPaletteOptimizer.OptimizeLevelAsset(m_SelectedLevel, q))
                    {
                        m_SolvReport = null;
                        NotifyLiveSceneUpdate();
                    }
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 2. Zorluk ve Yanaşma Yeri (Slot) Seçimi
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚓ Zorluk & İskele Slot Sayısı", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Aynı anda yanaşan gemi sayısıdır. Az slot planlama gerektirir, çok slot rahat oynatır.", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            int currentSlots = m_SelectedLevel.SlotCount;
            DrawSlotPresetButton(5, "🟢 Kolay (5 İskele)", currentSlots == 5, new Color(0.35f, 0.85f, 0.45f));
            DrawSlotPresetButton(4, "🟡 Orta (4 İskele)", currentSlots == 4, new Color(0.95f, 0.85f, 0.35f));
            DrawSlotPresetButton(3, "🔴 Zor (3 İskele)", currentSlots == 3, new Color(1f, 0.45f, 0.4f));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 3. Çözülebilirlik Analiz Kartı (Canlı simülasyon)
            DrawSolvabilityCard();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.25f, 0.75f, 1f);
            if (GUILayout.Button("✏️ Pikselleri Tuvalde Çiz / Düzelt (Bölüm Stüdyosu)", GUILayout.Height(28)))
            {
                LevelBuilderWindow.OpenForLevel(m_SelectedLevel);
            }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("🛠️ Sahne & Ray Araçları", GUILayout.Width(180), GUILayout.Height(28)))
            {
                m_CurrentTab = DetailTab.SceneTools;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("💡 İpucu: Shader, boşluk ve derinlik gibi görsel ayarlar için yukarıdan 'Gelişmiş Mod'u açabilirsiniz.", EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawSlotPresetButton(int slots, string label, bool isSelected, Color activeColor)
        {
            Color prev = GUI.backgroundColor;
            if (isSelected) GUI.backgroundColor = activeColor;
            if (GUILayout.Button(label, GUILayout.Height(30)))
            {
                Undo.RecordObject(m_SelectedLevel, "Change Slot Count");
                m_SelectedLevel.SlotCount = slots;
                m_SolvReport = null;
                EditorUtility.SetDirty(m_SelectedLevel);
            }
            GUI.backgroundColor = prev;
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
            EditorGUI.BeginChangeCheck();
            bool disableTurbo = EditorGUILayout.Toggle(
                new GUIContent("2X Hızlanmayı Kapat", "Açıksa bu bölümde oyun sonundaki otomatik yerleştirme ve 2X hızlanma çalışmaz."),
                m_SelectedLevel.DisableAutoPlaceTurbo);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_SelectedLevel, "2X Hızlanma Ayarı");
                m_SelectedLevel.DisableAutoPlaceTurbo = disableTurbo;
                EditorUtility.SetDirty(m_SelectedLevel);
            }
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
            DrawSolvabilityCard();
        }

        // ---------------------------------------------------------------
        // Çözülebilirlik kartı (LevelSolvabilityAnalyzer)
        // ---------------------------------------------------------------
        // Rapor ÖNBELLEĞE alınır: simülasyon bölüm başına yüzlerce ms sürüyor,
        // her OnGUI repaint'inde çalıştırılamaz. Seçili bölüm değişince veya
        // "Yeniden Analiz Et" ile tazelenir.
        private LevelSolvabilityAnalyzer.Report m_SolvReport;
        private PixelLevelData m_SolvReportLevel;
        private bool m_SolvFoldout = true;

        private void DrawSolvabilityCard()
        {
            if (m_SelectedLevel == null) return;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            m_SolvFoldout = EditorGUILayout.Foldout(m_SolvFoldout, "🧪 Çözülebilirlik & Kilitlenme Analizi", true, EditorStyles.foldoutHeader);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(m_SolvReport == null ? "Analiz Et" : "Yeniden Analiz Et", GUILayout.Width(120)))
            {
                m_SolvReport = LevelSolvabilityAnalyzer.Analyze(m_SelectedLevel, true, 40);
                m_SolvReportLevel = m_SelectedLevel;
            }
            EditorGUILayout.EndHorizontal();

            if (!m_SolvFoldout) { EditorGUILayout.EndVertical(); return; }

            // Seçili bölüm değiştiyse eski rapor geçersiz
            if (m_SolvReport != null && m_SolvReportLevel != m_SelectedLevel) m_SolvReport = null;

            if (m_SolvReport == null)
            {
                EditorGUILayout.HelpBox(
                    "Bu bölüm gerçekten bitirilebilir mi? Analiz, oyunun kendi kurallarını " +
                    "(dış hava erişimi, gemi renk seçimi, slot sayısı) sahneye küp üretmeden oynatır.\n" +
                    "Simülasyon birkaç saniye sürebilir, o yüzden elle tetikleniyor.",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var r = m_SolvReport;

            if (!r.valid)
            {
                EditorGUILayout.HelpBox(r.invalidReason, MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            // --- Kilitlenme riski rozeti ---
            float risk = r.DeadlockRisk;
            Color riskColor = risk <= 0.001f ? new Color(0.35f, 0.85f, 0.4f)
                            : risk < 0.10f ? new Color(0.95f, 0.85f, 0.3f)
                            : new Color(1f, 0.45f, 0.4f);
            string riskBadge = risk <= 0.001f ? "✓ TEMİZ" : risk < 0.10f ? "⚠ RİSKLİ" : "✖ KİLİTLENİYOR";

            EditorGUILayout.BeginHorizontal();
            GUI.contentColor = riskColor;
            EditorGUILayout.LabelField($"{riskBadge}  —  kilitlenme riski %{risk * 100f:F0}", EditorStyles.boldLabel, GUILayout.Width(280));
            GUI.contentColor = Color.white;
            EditorGUILayout.LabelField($"{r.trials} deneme · {r.totalCubes} küp", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (risk > 0.001f)
            {
                EditorGUILayout.HelpBox(r.blockReason, MessageType.Error);
                if (r.blockedColors.Count > 0)
                {
                    EditorGUILayout.LabelField("En sık tıkayan renkler:", EditorStyles.miniBoldLabel);
                    EditorGUILayout.BeginHorizontal();
                    foreach (var c in r.blockedColors)
                    {
                        Rect sw = GUILayoutUtility.GetRect(26f, 16f, GUILayout.Width(26f));
                        EditorGUI.DrawRect(sw, c);
                    }
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.LabelField($"En kötü denemede sadece {r.collectedWorst}/{r.totalCubes} küp toplanabildi.", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4);

            // --- Renk tablosu ---
            EditorGUILayout.LabelField("Renk Dengesi", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("", GUILayout.Width(24));
            EditorGUILayout.LabelField("Renk", EditorStyles.miniBoldLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField("Küp", EditorStyles.miniBoldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Vagon Kap.", EditorStyles.miniBoldLabel, GUILayout.Width(75));
            EditorGUILayout.LabelField("Fark", EditorStyles.miniBoldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Not", EditorStyles.miniBoldLabel);
            EditorGUILayout.EndHorizontal();

            foreach (var b in r.budgets)
            {
                EditorGUILayout.BeginHorizontal();

                Rect swatch = GUILayoutUtility.GetRect(20f, 16f, GUILayout.Width(20f));
                EditorGUI.DrawRect(swatch, b.color);
                GUILayout.Space(4);

                EditorGUILayout.LabelField(b.label, GUILayout.Width(120));
                EditorGUILayout.LabelField(b.cubeCount.ToString(), GUILayout.Width(50));
                EditorGUILayout.LabelField(b.wagonCapacity.ToString(), GUILayout.Width(75));

                GUI.contentColor = b.IsShort ? new Color(1f, 0.45f, 0.4f) : Color.white;
                EditorGUILayout.LabelField((b.Diff >= 0 ? "+" : "") + b.Diff, GUILayout.Width(50));
                GUI.contentColor = Color.white;

                string note = "";
                if (b.IsStale) note = $"palet bayat (kayıtlı {b.storedPixelCount})";
                if (b.IsShort) note = (note.Length > 0 ? note + " · " : "") + "kapasite eksik";
                GUI.contentColor = b.IsStale ? new Color(0.95f, 0.8f, 0.35f) : Color.white;
                EditorGUILayout.LabelField(note, EditorStyles.miniLabel);
                GUI.contentColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(
                $"Toplam: {r.totalCubes} küp · {r.totalWagonCapacity} vagon kapasitesi",
                EditorStyles.miniLabel);

            EditorGUILayout.HelpBox(
                "Not: 'Vagon Kap.' sütunu WagonSequence'ten gelir ve yalnızca KAMYON sahnesini " +
                "(TruckDispatcher/TruckPool) bağlar. Gemi sahnesi renkleri ve kapasiteyi tahtada " +
                "kalan küplerden dinamik seçer, orada kapasite eksiği oluşmaz — oradaki risk " +
                "yukarıdaki kilitlenme oranıdır.", MessageType.None);

            foreach (var w in r.warnings)
                EditorGUILayout.HelpBox(w, MessageType.Warning);

            EditorGUILayout.EndVertical();
        }

        private void DrawLevelDifficultyStatsCard()
        {
            if (m_SelectedLevel == null) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("📊 Bölüm Zorluk & Oynanış İstatistikleri", EditorStyles.boldLabel);

            int totalCubes = m_SelectedLevel.GetTotalCubeCountInPalette();
            int colorCount = m_SelectedLevel.ColorPalette != null ? m_SelectedLevel.ColorPalette.Count : 0;
            int reqWagons = m_SelectedLevel.GetRequiredTruckCount();

            var diffRep = LevelDifficultyReport.GetCached(m_SelectedLevel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Toplam Küp: {totalCubes}", EditorStyles.boldLabel, GUILayout.Width(130));
            EditorGUILayout.LabelField($"Renk: {colorCount}", EditorStyles.boldLabel, GUILayout.Width(90));
            EditorGUILayout.LabelField($"Ray Slotu: {m_SelectedLevel.SlotCount}", EditorStyles.boldLabel, GUILayout.Width(95));
            EditorGUILayout.LabelField($"Vagon: ~{reqWagons}", EditorStyles.boldLabel, GUILayout.Width(100));

            if (diffRep.valid)
            {
                GUI.contentColor = LevelDifficultyReport.TierColor(diffRep.tier);
                EditorGUILayout.LabelField($"Zorluk: {LevelDifficultyReport.TierLabel(diffRep.tier)} ({Mathf.RoundToInt(diffRep.score)}/100)", EditorStyles.boldLabel);
                GUI.contentColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            if (diffRep.valid)
            {
                string advice = diffRep.tier == LevelDifficultyReport.Tier.Kolay
                    ? "💡 Kolay: büyük renk alanları, oyuncu düşünmeden ilerleyebilir. İlk level'lar için uygun."
                    : diffRep.tier == LevelDifficultyReport.Tier.Orta
                        ? "💡 Orta: renkler kısmen katmanlı; oyuncu hangi gemiyi göndereceğini biraz düşünmeli."
                        : "🔥 Zor: renkler dengeli, iç içe ve gömülü; yanlış gemi slotu boşuna tutar.";
                foreach (var reason in diffRep.reasons) advice += "\n• " + reason;
                EditorGUILayout.HelpBox(advice, MessageType.None);
            }

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

            EditorGUILayout.HelpBox(
                "🚢 Bu sekme hem GEMİ (Gemi.unity) hem de KAMYON (SampleScene.unity) sahnesi içindir.\n" +
                "Bölümdeki toplam küp sayısına göre gemilerin/vagonların kaçlı olacağını (16'lık, 20'lik vb.), hangi renkle ve hangi sırayla " +
                "geleceğini buradan otomatik (akıllı) dağıtabilir veya elle özelleştirebilirsiniz. 'Manuel Sıra' açık olduğunda oyunda birebir bu sıra kullanılır.", MessageType.Info);

            // 1. Üst Kontrol & Denge Şeridi
            DrawSmartGridTopBar();

            EditorGUILayout.Space(4);

            // 1b. Tasarım uyarıları
            DrawDesignWarningsCard();

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
            EditorGUILayout.LabelField("🚢 Gemi & Vagon Sıra Tasarımcısı", titleStyle, GUILayout.Width(240));

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

            // Denge rozeti: gemi kapasiteleri toplamı küp sayısıyla tutuyor mu
            GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(8, 8, 2, 2) };
            if (diff == 0 && totalCubes > 0)
            {
                badgeStyle.normal.textColor = new Color(0.35f, 0.95f, 0.5f);
                GUILayout.Label($"🟢 Dengeli ({totalCap}/{totalCubes})", badgeStyle, GUILayout.Height(22));
            }
            else if (diff > 0)
            {
                badgeStyle.normal.textColor = new Color(1f, 0.45f, 0.45f);
                GUILayout.Label($"🔴 {diff} küp eksik ({totalCap}/{totalCubes})", badgeStyle, GUILayout.Height(22));
            }
            else
            {
                badgeStyle.normal.textColor = new Color(0.45f, 0.8f, 1f);
                GUILayout.Label($"🔵 {-diff} fazla ({totalCap}/{totalCubes})", badgeStyle, GUILayout.Height(22));
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

            EditorGUILayout.Space(6);

            // 2. Ayar kartları: geniş pencerede yan yana, dar pencerede alt alta
            bool wide = position.width > 980f;
            if (wide) EditorGUILayout.BeginHorizontal();

            DrawSequenceCard("📐 Havuz & Slot", wide, () =>
            {
                int rows = DrawStepperRow("Sıra (dalga)", "Havuzda kaç sıra gemi bekler (oyunda en fazla 6 görünür).", m_TargetGridRows, 1, 12);
                if (rows != m_TargetGridRows)
                {
                    Undo.RecordObject(m_SelectedLevel, "Havuz Sırası");
                    m_TargetGridRows = rows;
                    m_SelectedLevel.PoolRows = rows;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }

                int cols = DrawStepperRow("Kolon", "Havuzda yan yana kaç sütun gemi olur.", m_GridColumnsPerRow, 1, 8);
                if (cols != m_GridColumnsPerRow)
                {
                    Undo.RecordObject(m_SelectedLevel, "Havuz Kolonu");
                    m_GridColumnsPerRow = cols;
                    m_SelectedLevel.PoolColumns = cols;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }

                int slots = DrawStepperRow("🛤️ Ray slotu", "Aynı anda yanaşabilen gemi sayısı. Az slot = daha zor.", m_SelectedLevel.SlotCount, 1, 8);
                if (slots != m_SelectedLevel.SlotCount)
                {
                    Undo.RecordObject(m_SelectedLevel, "Ray Slotu");
                    m_SelectedLevel.SlotCount = slots;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
            });

            DrawSequenceCard("🔢 Gemi Sayıları", wide, () =>
            {
                // Mod seçimi (iki parçalı buton)
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("Mod", "Sayılar nasıl dağıtılsın?"), GUILayout.Width(SequenceLabelWidth));
                bool round = m_SelectedLevel.UseRoundCapacities;
                bool newRound = round;
                GUI.backgroundColor = round ? new Color(0.45f, 0.8f, 1f) : Color.white;
                if (GUILayout.Toggle(round, new GUIContent("🔟 10'un katları", "Sayılar çoğunlukla 10, 20...; arada nadiren 12, 13, 17 gibi ara sayılar."), EditorStyles.miniButtonLeft, GUILayout.Height(22)) && !round) newRound = true;
                GUI.backgroundColor = !round ? new Color(0.45f, 0.8f, 1f) : Color.white;
                if (GUILayout.Toggle(!round, new GUIContent("🎲 Karışık", "Sayılar 'en sık' değerinin etrafında karışık dağılır."), EditorStyles.miniButtonRight, GUILayout.Height(22)) && round) newRound = false;
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                if (newRound != round)
                {
                    Undo.RecordObject(m_SelectedLevel, "Gemi Sayı Modu");
                    m_SelectedLevel.UseRoundCapacities = newRound;
                    EditorUtility.SetDirty(m_SelectedLevel);
                }

                int minCap = DrawStepperRow("En az", "Bir geminin en küçük sayısı (rengin küpü bundan azsa o gemi mecburen küçük olur).", m_SelectedLevel.MinTruckCapacity, 1, m_SelectedLevel.TruckCapacity);
                if (minCap != m_SelectedLevel.MinTruckCapacity)
                {
                    Undo.RecordObject(m_SelectedLevel, "Gemi Kapasite Alt Sınırı");
                    m_SelectedLevel.MinTruckCapacity = minCap;
                    EditorUtility.SetDirty(m_SelectedLevel);
                }

                int maxCap = DrawStepperRow("En çok", "Bir geminin en büyük sayısı (vagon kapasitesi).", m_SelectedLevel.TruckCapacity, 1, 64);
                if (maxCap != m_SelectedLevel.TruckCapacity)
                {
                    Undo.RecordObject(m_SelectedLevel, "Gemi Kapasite Üst Sınırı");
                    m_SelectedLevel.TruckCapacity = maxCap;
                    if (m_SelectedLevel.MinTruckCapacity > maxCap) m_SelectedLevel.MinTruckCapacity = maxCap;
                    if (m_SelectedLevel.PeakTruckCapacity > maxCap) m_SelectedLevel.PeakTruckCapacity = maxCap;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }

                if (m_SelectedLevel.UseRoundCapacities)
                {
                    int ratio = Mathf.RoundToInt(m_SelectedLevel.RoundCapacityRatio * 100f);
                    int newRatio = DrawPercentSliderRow("Yuvarlak %", "10'un katı olan gemilerin oranı.", ratio, 0, 100);
                    if (newRatio != ratio)
                    {
                        Undo.RecordObject(m_SelectedLevel, "Yuvarlak Sayı Oranı");
                        m_SelectedLevel.RoundCapacityRatio = newRatio / 100f;
                        EditorUtility.SetDirty(m_SelectedLevel);
                    }
                }
                else
                {
                    int peak = DrawStepperRow("En sık", "Sayıların yoğunlaştığı değer.", m_SelectedLevel.PeakTruckCapacity, m_SelectedLevel.MinTruckCapacity, m_SelectedLevel.TruckCapacity);
                    if (peak != m_SelectedLevel.PeakTruckCapacity)
                    {
                        Undo.RecordObject(m_SelectedLevel, "Gemi Kapasite Yoğunluğu");
                        m_SelectedLevel.PeakTruckCapacity = peak;
                        EditorUtility.SetDirty(m_SelectedLevel);
                    }
                }

                EditorGUILayout.Space(4);
                GUI.backgroundColor = new Color(0.55f, 0.85f, 1f);
                if (GUILayout.Button(new GUIContent("🎲 Sayıları Karıştır", "Renk sırasını bozmadan gemi sayılarını bu ayarlara göre yeniden dağıtır. Renk başına toplam küp korunur; gerekirse aynı renkten gemi eklenir/çıkarılır. Undo ile geri alınır."), GUILayout.Height(28)))
                {
                    Undo.RecordObject(m_SelectedLevel, "Gemi Sayılarını Karıştır");
                    if (!m_SelectedLevel.UseCustomWagonSequence || m_SelectedLevel.WagonSequence == null || m_SelectedLevel.WagonSequence.Count == 0)
                        m_SelectedLevel.GenerateInterleavedWagonSequenceFromPalette();
                    else
                        m_SelectedLevel.VaryWagonCapacities();
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
                GUI.backgroundColor = Color.white;
            });

            DrawSequenceCard("🔗 Halat  &  ❓ Gizli", wide, () =>
            {
                m_GenLinkPercent = DrawPercentSliderRow("🔗 Halat %", "Gemilerin yaklaşık bu kadarı halatla bağlanır (iki gemi bir halat). Bütün sıraya yayılır: yan yana çiftler her dalgada, üst üste çiftler oyun başındaki havuzda.", m_GenLinkPercent, 0, 60);
                m_GenHiddenPercent = DrawPercentSliderRow("❓ Gizli %", "Gemilerin yaklaşık bu kadarı gizli ('?') olur. En ön sıradakiler ve halatlılar gizlenmez.", m_GenHiddenPercent, 0, 60);

                int linkPairsNow = 0, hiddenNow = 0;
                if (m_SelectedLevel.WagonSequence != null)
                {
                    foreach (var w in m_SelectedLevel.WagonSequence)
                    {
                        if (w == null) continue;
                        if (w.linkId > 0) linkPairsNow++;
                        if (w.isHidden) hiddenNow++;
                    }
                }
                EditorGUILayout.LabelField($"Şu an: {linkPairsNow / 2} halat · {hiddenNow} gizli gemi", EditorStyles.miniLabel);

                EditorGUILayout.Space(4);
                GUI.backgroundColor = new Color(0.8f, 0.65f, 1f);
                if (GUILayout.Button(new GUIContent("🪄 Halat & Gizli Üret", "Mevcut halatları ve gizli gemileri silip oranlara göre yeniden dağıtır. Sayıları karıştırdıktan SONRA bas (karıştırma gemi ekleyip çıkarınca yerler kayar). Undo ile geri alınır."), GUILayout.Height(28)))
                {
                    EditorPrefs.SetInt("PixelGame_GenLinkPercent", m_GenLinkPercent);
                    EditorPrefs.SetInt("PixelGame_GenHiddenPercent", m_GenHiddenPercent);
                    Undo.RecordObject(m_SelectedLevel, "Halat & Gizli Üret");
                    m_SelectedLevel.GenerateLinksAndHidden(m_GenLinkPercent / 100f, m_GenHiddenPercent / 100f, new System.Random(),
                        out int pairs, out int hidden, out int targetPairs);
                    m_SelectedSlotForLink = -1;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                    string note = pairs < targetPairs ? $" (hedef {targetPairs} çiftti; havuzda uygun yan yana/üst üste yer bu kadar)" : "";
                    ShowNotification(new GUIContent($"🔗 {pairs} halat · ❓ {hidden} gizli gemi{note}"));
                    Debug.Log($"[Level Designer] {m_SelectedLevel.name}: {pairs} halat, {hidden} gizli gemi üretildi{note}.");
                }
                GUI.backgroundColor = Color.white;
            });

            if (wide) EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(m_LastSmartStatusMessage))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(m_LastSmartStatusMessage, MessageType.Info);
            }
        }

        // ---------------------------------------------------------------
        // Tasarım uyarıları (LevelDesignWarnings) — level değişmedikçe önbellekten
        private bool m_WarningsFoldout = true;
        private PixelLevelData m_WarningsLevel;
        private int m_WarningsDirtyCount = -1;
        private List<LevelDesignWarnings.Warning> m_Warnings;
        private LevelDifficultyReport.Result m_DifficultyReport;

        private void DrawDesignWarningsCard()
        {
            if (m_SelectedLevel == null) return;
            int dirty = EditorUtility.GetDirtyCount(m_SelectedLevel);
            if (m_Warnings == null || m_WarningsLevel != m_SelectedLevel || m_WarningsDirtyCount != dirty)
            {
                m_Warnings = LevelDesignWarnings.Compute(m_SelectedLevel);
                m_DifficultyReport = LevelDifficultyReport.Compute(m_SelectedLevel);
                m_WarningsLevel = m_SelectedLevel;
                m_WarningsDirtyCount = dirty;
            }

            int errors = 0, warns = 0;
            foreach (var w in m_Warnings)
            {
                if (w.type == MessageType.Error) errors++;
                else if (w.type == MessageType.Warning) warns++;
            }

            // 📊 Zorluk raporu (resmin renk dağılımından)
            var rep = m_DifficultyReport;
            if (rep != null && rep.valid)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"📊 Zorluk: {LevelDifficultyReport.TierLabel(rep.tier)}  ({rep.score:0}/100)", new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 });
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("📋 Tüm Level'lar", "Bütün level'ların zorluk tablosu"), EditorStyles.miniButton, GUILayout.Width(110)))
                    LevelDifficultyOverviewWindow.Open();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(
                    $"Baskın renk %{rep.dominantShare * 100f:0} ({rep.dominantName}) · Etkin renk {rep.effectiveColors:0.0}/{rep.colorCount} · " +
                    $"Bölge {rep.regionCount} (ort. {rep.avgRegionSize:0} küp) · Başta açık %{rep.exposedShare * 100f:0} · Gömülü renk {rep.buriedColors} · Kabuk {rep.shells}",
                    EditorStyles.wordWrappedMiniLabel);
                foreach (var reason in rep.reasons)
                    EditorGUILayout.LabelField("• " + reason, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            string title = m_Warnings.Count == 0
                ? "✅ Tasarım Uyarıları — sorun yok"
                : $"⚠️ Tasarım Uyarıları — {errors} hata · {warns} uyarı · {m_Warnings.Count - errors - warns} bilgi";
            m_WarningsFoldout = EditorGUILayout.Foldout(m_WarningsFoldout, title, true, EditorStyles.foldoutHeader);
            if (GUILayout.Button(new GUIContent("↻", "Yeniden hesapla"), EditorStyles.miniButton, GUILayout.Width(24)))
            {
                m_Warnings = null;
            }
            EditorGUILayout.EndHorizontal();

            if (m_WarningsFoldout && m_Warnings != null)
            {
                foreach (var w in m_Warnings)
                {
                    EditorGUILayout.HelpBox(w.text, w.type);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private const float SequenceLabelWidth = 92f;

        /// <summary>Başlıklı ayar kartı (gemi sırası sekmesi). Geniş pencerede yan yana eşit genişlikte durur.</summary>
        private void DrawSequenceCard(string title, bool wide, System.Action body)
        {
            if (wide) EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(250), GUILayout.ExpandWidth(true));
            else EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle head = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            EditorGUILayout.LabelField(title, head, GUILayout.Height(20));
            EditorGUILayout.Space(2);
            body();
            EditorGUILayout.EndVertical();
        }

        /// <summary>Etiket + [-] [sayı] [+] satırı; yeni değeri döner.</summary>
        private int DrawStepperRow(string label, string tooltip, int value, int min, int max)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(label, tooltip), GUILayout.Width(SequenceLabelWidth), GUILayout.Height(20));
            if (GUILayout.Button("−", EditorStyles.miniButtonLeft, GUILayout.Width(26), GUILayout.Height(20))) value--;
            value = EditorGUILayout.IntField(value, GUILayout.Width(44), GUILayout.Height(20));
            if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(26), GUILayout.Height(20))) value++;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            return Mathf.Clamp(value, min, Mathf.Max(min, max));
        }

        /// <summary>Etiket + yüzde kaydırıcısı satırı; yeni değeri döner.</summary>
        private int DrawPercentSliderRow(string label, string tooltip, int value, int min, int max)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(label, tooltip), GUILayout.Width(SequenceLabelWidth), GUILayout.Height(20));
            value = EditorGUILayout.IntSlider(value, min, max, GUILayout.Height(20));
            EditorGUILayout.EndHorizontal();
            return value;
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
            else if (m_SelectedSlotForLink >= 0)
            {
                EditorGUILayout.HelpBox($"🔗 Slot #{m_SelectedSlotForLink + 1} bağlamak için seçildi! Bağlamak istediğiniz 2. geminin '🔗 ile Bağla' butonuna tıklayın (İptal için aynı butona tekrar basın).", MessageType.Info);
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
                    DrawGridSlotTile(slotIndex, r, c, cols);
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

        private void DrawGridSlotTile(int slotIndex, int r, int c, int cols)
        {
            var sequence = m_SelectedLevel.WagonSequence;
            bool isFilled = (slotIndex < sequence.Count && sequence[slotIndex] != null);
            bool isSwapSelected = (m_SelectedSlotForSwap == slotIndex);

            float cardWidth = 158f;
            float cardHeight = 142f;

            if (isFilled)
            {
                var wagon = sequence[slotIndex];
                Color wc = wagon.wagonColor;
                string cDisplayName = GetColorDisplayName(wc, wagon.label, slotIndex);

                // Dış Kutu (Swap seçiliyse sarı, Bağlama modundaysa turuncu, Zaten bağlıysa ferah mavi)
                Color boxBg = Color.white;
                if (isSwapSelected) boxBg = new Color(1f, 0.92f, 0.25f);
                else if (m_SelectedSlotForLink == slotIndex) boxBg = new Color(1f, 0.82f, 0.25f);
                else if (wagon.linkId > 0) boxBg = new Color(0.85f, 0.94f, 1.0f);

                GUI.backgroundColor = boxBg;
                EditorGUILayout.BeginVertical("box", GUILayout.Width(cardWidth), GUILayout.Height(cardHeight));
                GUI.backgroundColor = Color.white;

                // 1. Üst Şerit: Vagon Rengi + Numara + Silme Butonu
                Rect topRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.DrawRect(topRect, wc);

                GUIStyle numStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 10,
                    normal = { textColor = (wc.grayscale > 0.55f) ? Color.black : Color.white }
                };
                GUI.Label(new Rect(topRect.x + 4, topRect.y + 1, topRect.width - 44, 18), $"#{slotIndex + 1} {cDisplayName}", numStyle);

                // ❓ Gizli Gemi: En ön sıraya gelene kadar renk ve yazı '?' desenli örtüyle saklanır
                GUI.backgroundColor = wagon.isHidden ? new Color(0.35f, 0.32f, 0.9f) : Color.white;
                if (GUI.Button(new Rect(topRect.xMax - 38, topRect.y + 1, 18, 16),
                    new GUIContent("?", wagon.isHidden
                        ? "Gizli gemi (açık): en ön sıraya gelene kadar rengi ve yazısı saklanır. Kapatmak için tıkla."
                        : "Gizli gemi yap: en ön sıraya gelene kadar rengi ve yazısı '?' örtüsüyle saklanır."),
                    EditorStyles.miniButton))
                {
                    Undo.RecordObject(m_SelectedLevel, "Toggle Hidden Ship");
                    wagon.isHidden = !wagon.isHidden;
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
                GUI.backgroundColor = Color.white;
                if (wagon.isHidden)
                {
                    // Renk şeridinin altında lacivert çizgi: gizli geminin bir bakışta fark edilmesi için
                    EditorGUI.DrawRect(new Rect(topRect.x, topRect.yMax - 3, topRect.width - 40, 3), new Color(0.13f, 0.12f, 0.31f));
                }

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
                    m_SelectedSlotForLink = -1;
                    m_SelectedLevel.UseCustomWagonSequence = true;
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                    EditorGUILayout.EndVertical();
                    return;
                }

                // 2. Takas / Seçim Butonu
                string swapBtnText = isSwapSelected ? "⭐ SEÇİLDİ (Hedefe Tıkla)" : "⇄ Taşı / Takas";
                GUI.backgroundColor = isSwapSelected ? new Color(1f, 0.88f, 0.2f) : Color.white;
                if (GUILayout.Button(swapBtnText, EditorStyles.miniButton, GUILayout.Height(18)))
                {
                    m_SelectedSlotForLink = -1;
                    HandleSlotClick(slotIndex);
                }
                GUI.backgroundColor = Color.white;

                // 3. Tekil Yön Okları Pad'i: ◀ (Sol) | ▲ (Üst Kolon) | ▼ (Alt Kolon) | ▶ (Sağ)
                EditorGUILayout.BeginHorizontal();

                GUI.enabled = slotIndex > 0;
                if (GUILayout.Button(new GUIContent("◀", "Solundaki vagonla yer değiştir"), EditorStyles.miniButton, GUILayout.Width(28), GUILayout.Height(18)))
                {
                    SwapWagons(slotIndex, slotIndex - 1);
                }

                GUI.enabled = slotIndex >= cols;
                if (GUILayout.Button(new GUIContent("▲", "Üst dalgadaki aynı kolonla yer değiştir"), EditorStyles.miniButton, GUILayout.Width(28), GUILayout.Height(18)))
                {
                    SwapWagons(slotIndex, slotIndex - cols);
                }

                GUI.enabled = true;
                if (GUILayout.Button(new GUIContent("▼", "Alt dalgadaki aynı kolonla yer değiştir"), EditorStyles.miniButton, GUILayout.Width(28), GUILayout.Height(18)))
                {
                    SwapWagons(slotIndex, slotIndex + cols);
                }

                GUI.enabled = true;
                if (GUILayout.Button(new GUIContent("▶", "Sağındaki vagonla yer değiştir"), EditorStyles.miniButton, GUILayout.Width(28), GUILayout.Height(18)))
                {
                    SwapWagons(slotIndex, slotIndex + 1);
                }
                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();

                // 3.5. 🔗 Bağla / Birleştir Butonu
                EditorGUILayout.BeginHorizontal();
                bool isLinked = (wagon.linkId > 0);
                bool isLinkSelected = (m_SelectedSlotForLink == slotIndex);

                if (isLinked)
                {
                    GUI.backgroundColor = new Color(0.2f, 0.85f, 0.95f);
                    GUILayout.Label($"🔗 Bağ #{wagon.linkId}", EditorStyles.miniBoldLabel, GUILayout.Height(17));
                    GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                    if (GUILayout.Button(new GUIContent("✕", "Bağlantıyı kopar"), EditorStyles.miniButton, GUILayout.Width(20), GUILayout.Height(16)))
                    {
                        Undo.RecordObject(m_SelectedLevel, "Unlink Wagon");
                        m_SelectedLevel.UnlinkWagon(slotIndex);
                        m_SelectedLevel.UseCustomWagonSequence = true;
                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    if (isLinkSelected)
                    {
                        GUI.backgroundColor = new Color(1f, 0.85f, 0.2f);
                        if (GUILayout.Button("⭐ Seçildi (İptal)", EditorStyles.miniButton, GUILayout.Height(17)))
                        {
                            m_SelectedSlotForLink = -1;
                        }
                        GUI.backgroundColor = Color.white;
                    }
                    else if (m_SelectedSlotForLink != -1)
                    {
                        GUI.backgroundColor = new Color(0.35f, 0.95f, 0.45f);
                        if (GUILayout.Button($"🔗 #{m_SelectedSlotForLink + 1} ile Bağla", EditorStyles.miniButton, GUILayout.Height(17)))
                        {
                            Undo.RecordObject(m_SelectedLevel, "Link Wagons");
                            m_SelectedLevel.LinkWagons(m_SelectedSlotForLink, slotIndex);
                            m_SelectedSlotForLink = -1;
                            m_SelectedLevel.UseCustomWagonSequence = true;
                            EditorUtility.SetDirty(m_SelectedLevel);
                            NotifyLiveSceneUpdate();
                        }
                        GUI.backgroundColor = Color.white;
                    }
                    else
                    {
                        if (GUILayout.Button(new GUIContent("🔗 Bağla", "Başka bir gemiyle bağlamak için tıkla"), EditorStyles.miniButton, GUILayout.Height(17)))
                        {
                            m_SelectedSlotForLink = slotIndex;
                            m_SelectedSlotForSwap = -1;
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                // 4. Kapasite Stepper Kontrolü
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

                // 5. Hızlı Renk Noktaları (Tek tıkla rengi doğrudan değiştir)
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

                string emptyBtnLabel = (m_SelectedSlotForSwap != -1) ? $"⬇ Buraya Taşı (#{m_SelectedSlotForSwap + 1})" : "+ Vagon Ekle";
                GUI.backgroundColor = (m_SelectedSlotForSwap != -1) ? new Color(0.3f, 0.88f, 0.5f) : (m_ActiveBrushPaletteIndex >= 0 ? new Color(0.3f, 0.88f, 0.5f) : Color.white);
                if (m_ActiveBrushPaletteIndex >= 0 && m_ActiveBrushPaletteIndex < m_SelectedLevel.ColorPalette.Count && m_SelectedSlotForSwap == -1)
                {
                    var bEntry = m_SelectedLevel.ColorPalette[m_ActiveBrushPaletteIndex];
                    string bName = GetColorDisplayName(bEntry.targetColor, bEntry.label, m_ActiveBrushPaletteIndex);
                    emptyBtnLabel = $"🖌️ {bName} Yerleştir";
                }

                if (GUILayout.Button(emptyBtnLabel, EditorStyles.miniButton, GUILayout.Height(28)))
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

        private void SwapWagons(int a, int b)
        {
            var sequence = m_SelectedLevel.WagonSequence;
            if (sequence == null) sequence = m_SelectedLevel.WagonSequence = new List<WagonSequenceEntry>();
            if (a < 0 || b < 0) return;

            Undo.RecordObject(m_SelectedLevel, "Swap Wagons");

            while (sequence.Count <= Mathf.Max(a, b))
            {
                sequence.Add(null);
            }

            (sequence[a], sequence[b]) = (sequence[b], sequence[a]);

            // Listenin sonundaki boşlukları temizle
            while (sequence.Count > 0 && sequence[sequence.Count - 1] == null)
            {
                sequence.RemoveAt(sequence.Count - 1);
            }

            // Etiketleri sıra numarasına göre güncelle (#1, #2, ...)
            for (int i = 0; i < sequence.Count; i++)
            {
                if (sequence[i] != null)
                {
                    string cName = GetColorDisplayName(sequence[i].wagonColor, sequence[i].label, i);
                    sequence[i].label = $"{cName} (#{i + 1})";
                }
            }

            m_SelectedSlotForSwap = -1;
            m_SelectedLevel.UseCustomWagonSequence = true;
            EditorUtility.SetDirty(m_SelectedLevel);
            NotifyLiveSceneUpdate();
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
                SwapWagons(m_SelectedSlotForSwap, slotIndex);
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

        private Dictionary<Vector2Int, Color> GetLevelPixelGrid()
        {
            var grid = new Dictionary<Vector2Int, Color>();
            if (m_SelectedLevel == null) return grid;

            // 1. Önce doğrudan 2D seviye dokusundan piksel haritası oku
            Texture2D tex = m_SelectedLevel.GetActiveTexture();
            if (tex != null)
            {
                EnsureTextureReadable(tex);
                int w = tex.width;
                int h = tex.height;
                Color32[] raw = tex.GetPixels32();

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        Color32 p32 = raw[y * w + x];
                        if (p32.a < 25) continue;
                        Color c = p32;

                        Color matched = c;
                        if (m_SelectedLevel.ColorPalette != null)
                        {
                            for (int p = 0; p < m_SelectedLevel.ColorPalette.Count; p++)
                            {
                                var pEntry = m_SelectedLevel.ColorPalette[p];
                                if (pEntry != null && PaletteColorOverride.ColorsMatch(pEntry.targetColor, c, 0.08f))
                                {
                                    matched = pEntry.targetColor;
                                    break;
                                }
                            }
                        }
                        grid[new Vector2Int(x, y)] = matched;
                    }
                }
                if (grid.Count > 0) return grid;
            }

            // 2. Doku yoksa sahnedeki 3D küplerden oku
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            PixelCube[] sceneCubes = null;
            if (gen != null && gen.CubesContainer != null)
            {
                sceneCubes = gen.CubesContainer.GetComponentsInChildren<PixelCube>();
            }
            if (sceneCubes == null || sceneCubes.Length == 0)
            {
                sceneCubes = Object.FindObjectsByType<PixelCube>(FindObjectsSortMode.None);
            }
            if (sceneCubes != null)
            {
                foreach (var cube in sceneCubes)
                {
                    if (cube == null || !cube.gameObject.activeSelf || cube.IsPopped) continue;
                    grid[new Vector2Int(cube.GridX, cube.GridY)] = cube.CurrentColor;
                }
            }

            return grid;
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

            var grid = GetLevelPixelGrid();
            if (grid.Count > 0)
            {
                result.fromScene = true;
                foreach (var kvp in grid)
                {
                    Vector2Int pos = kvp.Key;
                    Color c = kvp.Value;

                    int palIdx = 0;
                    string label = "";
                    if (m_SelectedLevel.ColorPalette != null)
                    {
                        for (int p = 0; p < m_SelectedLevel.ColorPalette.Count; p++)
                        {
                            var pEntry = m_SelectedLevel.ColorPalette[p];
                            if (pEntry != null && PaletteColorOverride.ColorsMatch(pEntry.targetColor, c, 0.08f))
                            {
                                label = pEntry.label;
                                palIdx = p;
                                break;
                            }
                        }
                    }

                    if (!result.totalPerColor.ContainsKey(c))
                    {
                        result.totalPerColor[c] = 0;
                        result.exposedPerColor[c] = 0;
                        result.colorNames[c] = GetColorDisplayName(c, label, palIdx);
                        result.paletteIndices[c] = palIdx;
                    }

                    result.totalPerColor[c]++;
                    result.totalCubes++;

                    bool isExposed = !grid.ContainsKey(new Vector2Int(pos.x - 1, pos.y)) ||
                                     !grid.ContainsKey(new Vector2Int(pos.x + 1, pos.y)) ||
                                     !grid.ContainsKey(new Vector2Int(pos.x, pos.y - 1)) ||
                                     !grid.ContainsKey(new Vector2Int(pos.x, pos.y + 1));

                    if (isExposed)
                    {
                        result.exposedPerColor[c]++;
                    }
                }
            }
            else if (m_SelectedLevel.ColorPalette != null)
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

            // Simülasyon için ızgarayı al
            Dictionary<Vector2Int, Color> simGrid = GetLevelPixelGrid();
            Dictionary<Color, int> remainingTotal = new Dictionary<Color, int>(scan.totalPerColor);

            var sequence = new List<WagonSequenceEntry>();

            if (simGrid.Count == 0)
            {
                foreach (var kvp in remainingTotal)
                {
                    int rem = kvp.Value;
                    while (rem > 0)
                    {
                        int load = Mathf.Min(capPerWagon, rem);
                        sequence.Add(new WagonSequenceEntry(kvp.Key, load, scan.paletteIndices[kvp.Key], $"{scan.colorNames[kvp.Key]} ({load})"));
                        rem -= load;
                    }
                }
            }
            else
            {
                // Katman Katman Dıştan İçe Peeling Simülasyonu
                Color lastChosenColor = Color.clear;

                while (simGrid.Count > 0)
                {
                    // 1. Dış havaya temas eden (exposed) pikselleri bul
                    Dictionary<Color, List<Vector2Int>> exposedByColor = new Dictionary<Color, List<Vector2Int>>();
                    foreach (var kvp in simGrid)
                    {
                        Vector2Int p = kvp.Key;
                        Color c = kvp.Value;

                        bool exp = !simGrid.ContainsKey(new Vector2Int(p.x - 1, p.y)) ||
                                   !simGrid.ContainsKey(new Vector2Int(p.x + 1, p.y)) ||
                                   !simGrid.ContainsKey(new Vector2Int(p.x, p.y - 1)) ||
                                   !simGrid.ContainsKey(new Vector2Int(p.x, p.y + 1));

                        if (exp)
                        {
                            if (!exposedByColor.ContainsKey(c)) exposedByColor[c] = new List<Vector2Int>();
                            exposedByColor[c].Add(p);
                        }
                    }

                    if (exposedByColor.Count == 0)
                    {
                        foreach (var kvp in simGrid)
                        {
                            if (!exposedByColor.ContainsKey(kvp.Value)) exposedByColor[kvp.Value] = new List<Vector2Int>();
                            exposedByColor[kvp.Value].Add(kvp.Key);
                            break;
                        }
                    }

                    // 2. Sadece şu anda dışa AÇIK olan renkler arasından seçim yap
                    Color chosenColor = Color.clear;

                    if (m_WagonDifficulty == WagonDifficultyMode.Easy)
                    {
                        // Kolay Mod: En çok açık yüzeyi olan rengi al, bitene kadar sürdür
                        if (lastChosenColor != Color.clear && exposedByColor.ContainsKey(lastChosenColor) && remainingTotal[lastChosenColor] > 0)
                        {
                            chosenColor = lastChosenColor;
                        }
                        else
                        {
                            int maxExp = -1;
                            foreach (var kvp in exposedByColor)
                            {
                                if (kvp.Value.Count > maxExp)
                                {
                                    maxExp = kvp.Value.Count;
                                    chosenColor = kvp.Key;
                                }
                            }
                        }
                    }
                    else if (m_WagonDifficulty == WagonDifficultyMode.Medium)
                    {
                        // Dengeli Mod: Açık yüzeyi olan renkler arasında dönüşümlü (round-robin) dağıt
                        // Ama ASLA açık yüzeyi 0 olan bir rengi erkenden alma!
                        List<Color> candidates = new List<Color>(exposedByColor.Keys);
                        candidates.Sort((a, b) => exposedByColor[b].Count.CompareTo(exposedByColor[a].Count));

                        if (lastChosenColor != Color.clear && candidates.Count > 1 && candidates[0] == lastChosenColor)
                        {
                            chosenColor = candidates[1];
                        }
                        else
                        {
                            chosenColor = candidates[0];
                        }
                    }
                    else // Hard
                    {
                        List<Color> candidates = new List<Color>(exposedByColor.Keys);
                        candidates.Sort((a, b) => exposedByColor[a].Count.CompareTo(exposedByColor[b].Count));
                        chosenColor = candidates[0];
                    }

                    lastChosenColor = chosenColor;

                    // 3. Vagon oluştur
                    int remForColor = remainingTotal[chosenColor];
                    int wagonLoad = Mathf.Min(capPerWagon, remForColor);
                    int palIdx = scan.paletteIndices[chosenColor];
                    string cName = scan.colorNames[chosenColor];

                    sequence.Add(new WagonSequenceEntry(chosenColor, wagonLoad, palIdx, $"{cName} ({wagonLoad})"));
                    remainingTotal[chosenColor] -= wagonLoad;

                    // 4. Bu renkten 'wagonLoad' kadar açık pikseli simülasyondan soy (sil)
                    var peelList = exposedByColor[chosenColor];
                    int peeled = 0;
                    foreach (var pt in peelList)
                    {
                        simGrid.Remove(pt);
                        peeled++;
                        if (peeled >= wagonLoad) break;
                    }

                    if (peeled < wagonLoad)
                    {
                        List<Vector2Int> extraSame = new List<Vector2Int>();
                        foreach (var kvp in simGrid)
                        {
                            if (kvp.Value == chosenColor) extraSame.Add(kvp.Key);
                        }
                        for (int i = 0; i < extraSame.Count && peeled < wagonLoad; i++)
                        {
                            simGrid.Remove(extraSame[i]);
                            peeled++;
                        }
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
            string sourceText = scan.fromScene ? "🎯 Katman Katman Dıştan İçe Soyma Analizi" : "🎨 Renk Paleti";
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

            if (m_SelectedLevel != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
                if (GUILayout.Button($"◀ Seviye Düzenleyiciye Geri Dön ({m_SelectedLevel.LevelName})", GUILayout.Height(28)))
                {
                    m_CurrentTab = DetailTab.LevelSetup;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.Space(6);

            // ⚓ MARİNA SU SLOTLARI BOYUT & DÜZEN CANLI AYARLAYICI
            DrawMarinaSlotLiveControllerCard();

            EditorGUILayout.Space(6);

            // 1. 🏝️ GEMİ SAHNESİ YÖNETİMİ
            DrawSceneToolCard("🏝️ Gemi Sahnesi Yönetimi", new (string, string, System.Action)[]
            {
                ("⚓ Konsept 2: Yüzen Şamandıra & Su Üstü Zincir Izgarasını Kur", "Slotlara kırmızı-beyaz yüzen şamandıraları, deniz zincirlerini ve ahşap numara tabelalarını kurar.", () => SetupMarinaDockSlots.SetupMarinaSlots()),
                ("🏝️ Gemi Sahnesini Sıfırdan Kur & Tüm Öğeleri Getir", "Gemi sahnesini, slotları, kum çerçevesini ve piksel sanatını kurar.", () => GemiSceneSetup.SetupGemiSceneMenu()),
                ("🎨 Kum Alanındaki Piksel Resmi Yenile (Regenerate)", "Kum alanındaki mevcut piksel sanatını ve gölgeleri anında yeniden üretir.", () => GemiSceneSetup.RegeneratePixelArtMenu()),
                ("➡️ Sonraki Seviyeyi Yükle", "Bir sonraki seviyeyi sahneye çağırır.", () => GemiSceneSetup.NextLevelMenu()),
                ("⬅️ Önceki Seviyeyi Yükle", "Bir önceki seviyeyi sahneye çağırır.", () => GemiSceneSetup.PrevLevelMenu())
            });

            EditorGUILayout.Space(6);

            // 2. 🎨 GÖRSEL, IŞIK & SHADER DÖNÜŞÜMÜ
            DrawSceneToolCard("🎨 Görsel, Işık & Shader Dönüşümü", new (string, string, System.Action)[]
            {
                ("🎨 Cartoon Shader'a Geçir", "Ana küp materyaline toon/cartoon cel-shader'ı uygular.", () => SetupCartoonShader.Apply()),
                ("🔧 Mor Kaplamaları Düzelt (Fix Scifi URP Materials)", "URP'de mor görünen eski materyalleri otomatik onarır.", () => UpgradeScifiMaterialsToURP.UpgradeMaterials(false)),
                ("🛥️ Gemi Sahte Gölgelerini Kur", "Gemi prefabına pürüzsüz sahte gölge oluşturur.", () => SetupShipFakeShadow.ApplyToPrefab()),
                ("🧹 Pano ve Obje Arkasındaki Gölgeleri Temizle", "Eski artık pano ve obje arkası gölgelerini sahneden temizler.", () => CleanupSceneShadows.RunPurge())
            });

            EditorGUILayout.Space(6);

            // 3. ⚓ GEMİ & RİHTIM DÜZENİ
            DrawSceneToolCard("⚓ Gemi & Rıhtım Araçları", new (string, string, System.Action)[]
            {
                ("⚓ Marina İskele & Slotlarını Kur", "Marina rıhtımını ve su slotlarını kurar.", () => SetupMarinaDockSlots.SetupMarinaSlots()),
                ("🛥️ Gemi Sahte Gölgelerini Yenile", "Gemi prefabına pürüzsüz su gölgesi uygular.", () => SetupShipFakeShadow.ApplyToPrefab()),
                ("🌊 Su Efektini Yapılandır", "Hypercasual deniz ve su materyalini yapılandırır.", () => WaterSlotFoamSetup.SpreadFoamToAll5Slots())
            });

            EditorGUILayout.Space(6);

            // 4. 🎯 HUD, ARAYÜZ & TESTLER
            DrawSceneToolCard("🎯 HUD, Arayüz & Testler", new (string, string, System.Action)[]
            {
                ("📱 Gemi Sahnesi Üst HUD'ını Kur (Image 1)", "Gemi sahnesine ayarlar düğmesi, LEVEL 1, can ve altın şeridini kurar.", () => SetupGemiTopHUD.BuildTopHUD()),
                ("🏆 Win & Fail Panellerini Sahneye Ekle / Güncelle", "HUD_Canvas altına Win ve Fail popup nesnelerini fiziksel olarak ekler ve bağlar.", () => SetupGemiWinLosePopups.SetupModalsInScene()),
                ("👁️ Win (Complete) Panelini Aç / Kapat", "Sahnede Win modalının görünürlüğünü açıp kapatır.", () => SetupGemiWinLosePopups.ToggleWinPanel()),
                ("👁️ Fail (Yenilgi) Panelini Aç / Kapat", "Sahnede Fail modalının görünürlüğünü açıp kapatır.", () => SetupGemiWinLosePopups.ToggleFailPanel()),
                ("🖼️ Casual HUD & Arkaplan Kur", "Casual HUD panellerini ve renkli arka planı yapılandırır.", () => SetupCasualHud.Apply()),
                ("🧪 Gemi Kalkış Testi (Ship Departure Test)", "Bölüm tamamlandığında geminin kalkış animasyonunu canlı test eder.", () => TestShipDeparture.RunTest()),
                ("📸 9:16 Ekran Görüntüsü Al (Capture Screenshot)", "Game görünümünden tam 1080x1920 dikey ekran görüntüsü alır.", () => CaptureGameViewScreenshot.Capture())
            });

            EditorGUILayout.Space(16);
            EditorGUILayout.EndScrollView();
        }

        private void DrawMarinaSlotLiveControllerCard()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            Rect titleRect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(titleRect, new Color(0.12f, 0.22f, 0.32f, 1f));
            GUIStyle cardHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.4f, 0.95f, 0.85f) },
                alignment = TextAnchor.MiddleLeft
            };
            GUI.Label(new Rect(titleRect.x + 8, titleRect.y + 1, titleRect.width - 16, titleRect.height), "⚓ Marina Su Slotları Canlı Boyut & Düzen Ayarlayıcı", cardHeaderStyle);

            EditorGUILayout.Space(4);

            MarinaSlotLayout layout = Object.FindFirstObjectByType<MarinaSlotLayout>();
            if (layout == null)
            {
                var anySlot = Object.FindFirstObjectByType<ShipSlot>();
                if (anySlot != null && anySlot.transform.parent != null)
                {
                    EditorGUILayout.HelpBox("Sahnede ShipSlot bulundu ancak [WaterSlotsRow] üzerinde MarinaSlotLayout bileşeni yok.", MessageType.Warning);
                    if (GUILayout.Button("➕ [WaterSlotsRow] Üzerine Marina Düzenleyici Ekle", GUILayout.Height(26)))
                    {
                        var parentGo = anySlot.transform.parent.gameObject;
                        layout = parentGo.AddComponent<MarinaSlotLayout>();
                        layout.ApplyLayout();
                        EditorUtility.SetDirty(parentGo);
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(parentGo.scene);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Sahnede aktif bir Marina Slotu veya Gemi sahnesi açık görünmüyor. 'Gemi Sahnesini Sıfırdan Kur' butonuna basarak sahneyi oluşturabilirsiniz.", MessageType.Info);
                }
            }

            if (layout != null)
            {
                EditorGUILayout.HelpBox("Tüm su slotlarının (WaterSlot_1..5) boyutunu, aralarındaki mesafeyi ve açısını tek seferde buradan canlı ayarlayabilirsiniz.", MessageType.None);
                EditorGUILayout.Space(2);

                EditorGUI.BeginChangeCheck();

                float width = EditorGUILayout.Slider(new GUIContent("↔️ Slot Genişliği (Width)", "Slotların X eksenindeki yatay genişliği (en)."), layout.SlotWidth, 0.3f, 3.5f);
                float length = EditorGUILayout.Slider(new GUIContent("↕️ Slot Uzunluğu (Length / Height)", "Slotların Z eksenindeki boyu / uzunluğu."), layout.SlotLength, 0.3f, 3.5f);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUIUtility.labelWidth);
                if (GUILayout.Button("🔗 1:1 (Genişliğe Eşitle)", EditorStyles.miniButton, GUILayout.Height(18)))
                {
                    Undo.RecordObject(layout, "Sync Slot Aspect Ratio");
                    layout.SlotLength = layout.SlotWidth;
                    EditorUtility.SetDirty(layout);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
                }
                if (GUILayout.Button("🔗 1:1 (Uzunluğa Eşitle)", EditorStyles.miniButton, GUILayout.Height(18)))
                {
                    Undo.RecordObject(layout, "Sync Slot Aspect Ratio");
                    layout.SlotWidth = layout.SlotLength;
                    EditorUtility.SetDirty(layout);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                float spacing = EditorGUILayout.Slider(new GUIContent("↔️ Slot Aralığı (Spacing)", "Slotların birbirine olan yatay mesafesi."), layout.SlotSpacing, 0.6f, 2.5f);
                float angle = EditorGUILayout.Slider(new GUIContent("📐 Yanaşma Açısı (Angle)", "Slotların ve gemilerin yanaşma açısı."), layout.SlotAngle, -60f, 60f);
                float tilt = EditorGUILayout.Slider(new GUIContent("🌊 Su Düzlemi Eğim Açısı", "Kamera perspektifine göre su yüzeyi eğimi."), layout.WaterTiltX, -90f, 0f);
                float offsetY = EditorGUILayout.Slider(new GUIContent("↕️ Yükseklik Ofseti (Y)", "Slot şeridinin Y eksenindeki yüksekliği."), layout.OffsetY, -2f, 4f);
                float offsetZ = EditorGUILayout.Slider(new GUIContent("↕️ Derinlik Ofseti (Z)", "Slot şeridinin Z eksenindeki derinliği."), layout.OffsetZ, -3f, 3f);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(layout, "Change Marina Slot Layout");
                    layout.SlotWidth = width;
                    layout.SlotLength = length;
                    layout.SlotSpacing = spacing;
                    layout.SlotAngle = angle;
                    layout.WaterTiltX = tilt;
                    layout.OffsetY = offsetY;
                    layout.OffsetZ = offsetZ;
                    layout.ApplyLayout();
                    EditorUtility.SetDirty(layout);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(0.35f, 0.75f, 1f);
                if (GUILayout.Button("🎯 Sahnede [WaterSlotsRow]'u Seç", GUILayout.Height(24)))
                {
                    Selection.activeGameObject = layout.gameObject;
                    EditorGUIUtility.PingObject(layout.gameObject);
                }

                GUI.backgroundColor = new Color(0.85f, 0.85f, 0.9f);
                if (GUILayout.Button("🔁 Varsayılanlara Dön", GUILayout.Height(24)))
                {
                    Undo.RecordObject(layout, "Reset Marina Slot Layout");
                    layout.SlotWidth = 1.15f;
                    layout.SlotLength = 1.15f;
                    layout.SlotSpacing = 1.40f;
                    layout.SlotAngle = -28f;
                    layout.WaterTiltX = -68f;
                    layout.OffsetY = 0.45f;
                    layout.OffsetZ = 0.0f;
                    layout.ApplyLayout();
                    EditorUtility.SetDirty(layout);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
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

        #region TAB 5: MYSTERY CUBES (GİZLİ KÜPLER)

        private void DrawMysteryCubesTab()
        {
            if (m_SelectedLevel == null) return;

            Texture2D activeTex = m_SelectedLevel.GetActiveTexture();
            Vector2Int gridRes = m_SelectedLevel.GetGridResolution();
            int cols = gridRes.x;
            int rows = gridRes.y;

            int totalCubes = m_SelectedLevel.GetTotalCubeCountInPalette();
            int mysteryCount = m_SelectedLevel.GetMysteryCubeCount();
            int normalCount = Mathf.Max(0, totalCubes - mysteryCount);
            float mysteryRatio = totalCubes > 0 ? (mysteryCount / (float)totalCubes) * 100f : 0f;

            // 1. Üst Bilgi & Durum Kartı (Hero Banner)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("❓", GUILayout.Width(30), GUILayout.Height(30));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Gizli / Soru İşaretli Küp Tasarımcısı (Mystery Cubes Studio)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Bölüm başında '?' simgesiyle gizlenen ve dış katman temizlenip açığa çıktığında rengi beliren küpleri tasarlayın.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // İstatistik Rozetleri
            EditorGUILayout.BeginHorizontal();
            DrawBadgeCard("Toplam Küp", $"{totalCubes}", new Color(0.2f, 0.5f, 0.9f));
            DrawBadgeCard("❓ Gizli Küp", $"{mysteryCount} (%{mysteryRatio:F1})", new Color(0.95f, 0.5f, 0.15f));
            DrawBadgeCard("Normal Küp", $"{normalCount}", new Color(0.25f, 0.75f, 0.4f));
            DrawBadgeCard("Çözünürlük", $"{cols} x {rows}", new Color(0.55f, 0.4f, 0.85f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Temel Ayarlar (Açılma Koşulu & Koyu Küp Rengi)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚙️ Gizli Küp Davranış Ayarları", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            var newCond = (MysteryRevealCondition)EditorGUILayout.EnumPopup(new GUIContent("🔓 Açılma Koşulu", "Küplerin gizliliğini bozup gerçek rengini ortaya çıkarma şartı"), m_SelectedLevel.MysteryRevealCondition);
            var newColor = EditorGUILayout.ColorField(new GUIContent("🎨 Gizli Küp Rengi", "Soru işaretinin arkasındaki koyu arka plan rengi"), m_SelectedLevel.MysteryCubeColor);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_SelectedLevel, "Change Mystery Settings");
                m_SelectedLevel.MysteryRevealCondition = newCond;
                m_SelectedLevel.MysteryCubeColor = newColor;
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // 2. Hızlı Toplu İşlem Araçları (Batch Operations)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚡ Hızlı & Otomatik Toplu Araçlar", EditorStyles.boldLabel);

            if (activeTex != null && !activeTex.isReadable)
            {
                EnsureTextureReadable(activeTex);
            }

            EditorGUILayout.BeginHorizontal();

            // A: İç Küpleri Otomatik Gizle
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.5f);
            if (GUILayout.Button("⭕ Tüm İç Küpleri Gizle", GUILayout.Height(28)))
            {
                Undo.RecordObject(m_SelectedLevel, "Hide All Inner Cubes");
                AutoMarkInnerCubesAsMystery(cols, rows, activeTex);
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }

            // B: Rastgele İç Küp Gizle
            GUI.backgroundColor = new Color(0.3f, 0.75f, 1f);
            if (GUILayout.Button($"🎲 Rastgele %{m_MysteryRandomPercent} İç Küp Gizle", GUILayout.Height(28)))
            {
                Undo.RecordObject(m_SelectedLevel, "Random Inner Mystery");
                RandomizeInnerCubesAsMystery(cols, rows, activeTex, m_MysteryRandomPercent);
                EditorUtility.SetDirty(m_SelectedLevel);
                NotifyLiveSceneUpdate();
            }

            // C: Tüm Gizlilikleri Temizle
            GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
            if (GUILayout.Button("🧹 Tüm Gizlilikleri Temizle", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Gizli Küpleri Temizle", "Bu bölümdeki tüm '?' gizli küpler normal renge dönecek. Onaylıyor musunuz?", "Evet, Temizle", "Vazgeç"))
                {
                    Undo.RecordObject(m_SelectedLevel, "Clear Mystery Cubes");
                    m_SelectedLevel.ClearMysteryCubes();
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Renge Göre Gizle / Aç ve Rastgele % Slider
            EditorGUILayout.BeginHorizontal();
            m_MysteryRandomPercent = EditorGUILayout.IntSlider("Rastgele Oran (%)", m_MysteryRandomPercent, 5, 80);

            // Renk Paletinden Seçerek Gizle
            if (m_SelectedLevel.ColorPalette != null && m_SelectedLevel.ColorPalette.Count > 0)
            {
                string[] colorNames = new string[m_SelectedLevel.ColorPalette.Count];
                for (int i = 0; i < colorNames.Length; i++)
                {
                    var p = m_SelectedLevel.ColorPalette[i];
                    colorNames[i] = string.IsNullOrEmpty(p.label) ? $"Renk #{i + 1}" : p.label;
                }
                m_MysteryColorIndex = Mathf.Clamp(m_MysteryColorIndex, 0, colorNames.Length - 1);
                m_MysteryColorIndex = EditorGUILayout.Popup(m_MysteryColorIndex, colorNames, GUILayout.Width(130));

                if (GUILayout.Button("Bu Rengi Gizle", EditorStyles.miniButton, GUILayout.Width(100)))
                {
                    Undo.RecordObject(m_SelectedLevel, "Hide Color");
                    Color target = m_SelectedLevel.ColorPalette[m_MysteryColorIndex].targetColor;
                    m_SelectedLevel.SetMysteryByColor(target, true);
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
                if (GUILayout.Button("Gizliliği Aç", EditorStyles.miniButton, GUILayout.Width(80)))
                {
                    Undo.RecordObject(m_SelectedLevel, "Unhide Color");
                    Color target = m_SelectedLevel.ColorPalette[m_MysteryColorIndex].targetColor;
                    m_SelectedLevel.SetMysteryByColor(target, false);
                    EditorUtility.SetDirty(m_SelectedLevel);
                    NotifyLiveSceneUpdate();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // 3. İnteraktif 2D Piksel Boyama Izgarası (Interactive Pixel Board Painter)
            DrawInteractiveMysteryGrid(cols, rows, activeTex);
        }

        private void DrawBadgeCard(string title, string value, Color accent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            GUIStyle valStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = accent }
            };
            GUIStyle titleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label(value, valStyle);
            GUILayout.Label(title, titleStyle);
            EditorGUILayout.EndVertical();
        }

        private void AutoMarkInnerCubesAsMystery(int cols, int rows, Texture2D tex)
        {
            if (tex == null) return;
            for (int x = 1; x < cols - 1; x++)
            {
                for (int y = 1; y < rows - 1; y++)
                {
                    if (IsSolidPixel(tex, x, y, cols, rows) &&
                        IsSolidPixel(tex, x - 1, y, cols, rows) &&
                        IsSolidPixel(tex, x + 1, y, cols, rows) &&
                        IsSolidPixel(tex, x, y - 1, cols, rows) &&
                        IsSolidPixel(tex, x, y + 1, cols, rows))
                    {
                        m_SelectedLevel.SetMysteryCube(x, y, true);
                    }
                }
            }
        }

        private bool IsSolidPixel(Texture2D tex, int x, int y, int cols, int rows)
        {
            if (x < 0 || x >= cols || y < 0 || y >= rows) return false;
            int px = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) / cols * tex.width), 0, tex.width - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) / rows * tex.height), 0, tex.height - 1);
            Color c = tex.GetPixel(px, py);
            return !(m_SelectedLevel.SkipTransparent && c.a < 0.1f);
        }

        private void RandomizeInnerCubesAsMystery(int cols, int rows, Texture2D tex, int percent)
        {
            if (tex == null) return;
            var innerCubes = new List<Vector2Int>();
            for (int x = 1; x < cols - 1; x++)
            {
                for (int y = 1; y < rows - 1; y++)
                {
                    if (IsSolidPixel(tex, x, y, cols, rows) &&
                        IsSolidPixel(tex, x - 1, y, cols, rows) &&
                        IsSolidPixel(tex, x + 1, y, cols, rows) &&
                        IsSolidPixel(tex, x, y - 1, cols, rows) &&
                        IsSolidPixel(tex, x, y + 1, cols, rows))
                    {
                        innerCubes.Add(new Vector2Int(x, y));
                    }
                }
            }

            if (innerCubes.Count == 0) return;
            var rng = new System.Random();
            for (int i = innerCubes.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var temp = innerCubes[i];
                innerCubes[i] = innerCubes[j];
                innerCubes[j] = temp;
            }

            int targetCount = Mathf.RoundToInt(innerCubes.Count * (percent / 100f));
            for (int i = 0; i < innerCubes.Count; i++)
            {
                m_SelectedLevel.SetMysteryCube(innerCubes[i].x, innerCubes[i].y, i < targetCount);
            }
        }

        private void DrawInteractiveMysteryGrid(int cols, int rows, Texture2D activeTex)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Araç Çubuğu (Brush, Eraser, Toggle)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🎨 İnteraktif Izgara (Tıklayarak / Sürükleyerek Boyayın):", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = (m_MysteryBrushMode == MysteryBrushMode.Paint) ? new Color(0.3f, 0.88f, 0.5f) : Color.white;
            if (GUILayout.Button("✏️ Soru İşareti Çiz (?)", EditorStyles.miniButtonLeft, GUILayout.Width(140)))
            {
                m_MysteryBrushMode = MysteryBrushMode.Paint;
            }

            GUI.backgroundColor = (m_MysteryBrushMode == MysteryBrushMode.Erase) ? new Color(1f, 0.45f, 0.45f) : Color.white;
            if (GUILayout.Button("🧹 Sil (Normal Yap)", EditorStyles.miniButtonMid, GUILayout.Width(130)))
            {
                m_MysteryBrushMode = MysteryBrushMode.Erase;
            }

            GUI.backgroundColor = (m_MysteryBrushMode == MysteryBrushMode.Toggle) ? new Color(0.35f, 0.75f, 1f) : Color.white;
            if (GUILayout.Button("🔄 Tıkla Değiştir", EditorStyles.miniButtonRight, GUILayout.Width(110)))
            {
                m_MysteryBrushMode = MysteryBrushMode.Toggle;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            if (cols <= 0 || rows <= 0 || activeTex == null)
            {
                EditorGUILayout.HelpBox("Izgara çözünürlüğü tespit edilemedi veya görsel atanmadı.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            if (m_MysteryQuestionIconTex == null)
            {
                m_MysteryQuestionIconTex = Resources.Load<Texture2D>("mystery_cube_question");
            }

            float availableWidth = EditorGUIUtility.currentViewWidth - 380f;
            float cellSize = Mathf.Clamp(Mathf.Floor(availableWidth / cols), 14f, 28f);
            float gridWidth = cols * cellSize;
            float gridHeight = rows * cellSize;

            m_MysteryGridScroll = EditorGUILayout.BeginScrollView(m_MysteryGridScroll, GUILayout.Height(Mathf.Min(gridHeight + 30, 480)));

            Rect gridRect = GUILayoutUtility.GetRect(gridWidth, gridHeight, GUILayout.Width(gridWidth), GUILayout.Height(gridHeight));
            // Izgara arka planı
            EditorGUI.DrawRect(new Rect(gridRect.x - 2, gridRect.y - 2, gridWidth + 4, gridHeight + 4), new Color(0.12f, 0.15f, 0.2f));
            EditorGUI.DrawRect(gridRect, new Color(0.92f, 0.90f, 0.85f));

            Color mysteryBgColor = m_SelectedLevel.MysteryCubeColor;
            GUIStyle qStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(cellSize * 0.72f), 9, 20),
                normal = { textColor = Color.white }
            };

            // Hücreleri çiz (Y=rows-1 en üstte, Y=0 en altta)
            for (int r = 0; r < rows; r++)
            {
                int y = rows - 1 - r;
                for (int x = 0; x < cols; x++)
                {
                    Rect cellRect = new Rect(gridRect.x + x * cellSize, gridRect.y + r * cellSize, cellSize - 1, cellSize - 1);

                    int px = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) / cols * activeTex.width), 0, activeTex.width - 1);
                    int py = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) / rows * activeTex.height), 0, activeTex.height - 1);
                    Color rawColor = activeTex.GetPixel(px, py);

                    if (m_SelectedLevel.SkipTransparent && rawColor.a < 0.1f)
                    {
                        bool checker = ((x + y) % 2 == 0);
                        EditorGUI.DrawRect(cellRect, checker ? new Color(0.88f, 0.88f, 0.88f, 0.6f) : new Color(0.82f, 0.82f, 0.82f, 0.6f));
                        continue;
                    }

                    bool isMystery = m_SelectedLevel.IsMysteryCube(x, y);

                    Color finalColor = m_SelectedLevel.ApplyColorPipeline(rawColor);
                    EditorGUI.DrawRect(cellRect, finalColor);

                    if (isMystery)
                    {
                        // Altındaki rengin kaybolmaması için opak siyah yerine yarı saydam şık bir karartma ve soru işareti çiz
                        Color overlayTint = new Color(mysteryBgColor.r, mysteryBgColor.g, mysteryBgColor.b, 0.45f);
                        EditorGUI.DrawRect(cellRect, overlayTint);

                        // İnce altın sarısı kenarlıkla gizli küpü netleştir
                        Handles.color = new Color(1f, 0.85f, 0.15f, 0.75f);
                        Handles.DrawWireCube(new Vector3(cellRect.center.x, cellRect.center.y, 0f), new Vector3(cellRect.width, cellRect.height, 0f));

                        if (m_MysteryQuestionIconTex != null)
                        {
                            float pad = Mathf.Max(1f, cellSize * 0.12f);
                            Rect iconRect = new Rect(cellRect.x + pad, cellRect.y + pad, cellRect.width - pad * 2, cellRect.height - pad * 2);
                            GUI.DrawTexture(iconRect, m_MysteryQuestionIconTex, ScaleMode.ScaleToFit);
                        }
                        else
                        {
                            GUI.Label(cellRect, "?", qStyle);
                        }
                    }
                }
            }

            // Fare ile etkileşim (Tıklama ve Sürükleme ile Boyama)
            Event evt = Event.current;
            if ((evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag) && gridRect.Contains(evt.mousePosition))
            {
                float localX = evt.mousePosition.x - gridRect.x;
                float localY = evt.mousePosition.y - gridRect.y;
                int clickedX = Mathf.FloorToInt(localX / cellSize);
                int clickedY = rows - 1 - Mathf.FloorToInt(localY / cellSize);

                if (clickedX >= 0 && clickedX < cols && clickedY >= 0 && clickedY < rows)
                {
                    int px = Mathf.Clamp(Mathf.FloorToInt((clickedX + 0.5f) / cols * activeTex.width), 0, activeTex.width - 1);
                    int py = Mathf.Clamp(Mathf.FloorToInt((clickedY + 0.5f) / rows * activeTex.height), 0, activeTex.height - 1);
                    Color c = activeTex.GetPixel(px, py);

                    if (!(m_SelectedLevel.SkipTransparent && c.a < 0.1f))
                    {
                        Undo.RecordObject(m_SelectedLevel, "Paint Mystery Cube");
                        if (m_MysteryBrushMode == MysteryBrushMode.Paint)
                        {
                            m_SelectedLevel.SetMysteryCube(clickedX, clickedY, true);
                        }
                        else if (m_MysteryBrushMode == MysteryBrushMode.Erase)
                        {
                            m_SelectedLevel.SetMysteryCube(clickedX, clickedY, false);
                        }
                        else if (m_MysteryBrushMode == MysteryBrushMode.Toggle && evt.type == EventType.MouseDown)
                        {
                            m_SelectedLevel.ToggleMysteryCube(clickedX, clickedY);
                        }

                        EditorUtility.SetDirty(m_SelectedLevel);
                        NotifyLiveSceneUpdate();
                        Repaint();
                        evt.Use();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        #endregion

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
                gen.SyncMysteryCubesLive();
            }

            // Sahnedeki ShipQueuePool'u anında senkronize et
            ShipQueuePool shipPool = Object.FindFirstObjectByType<ShipQueuePool>();
            if (shipPool != null && m_SelectedLevel != null)
            {
                shipPool.RebuildSpots(m_SelectedLevel.PoolColumns, m_SelectedLevel.PoolRows);
                shipPool.InitializeQueue();
            }

            // Sahnedeki MarinaSlotLayout ve ShipDispatcher slot sayısını anında senkronize et
            if (m_SelectedLevel != null)
            {
                MarinaSlotLayout marina = Object.FindFirstObjectByType<MarinaSlotLayout>();
                if (marina != null)
                {
                    marina.SetSlotCount(m_SelectedLevel.SlotCount);
                }

                ShipDispatcher dispatcher = Object.FindFirstObjectByType<ShipDispatcher>();
                if (dispatcher != null)
                {
                    dispatcher.EnsureReferences();
                }
            }

            SceneView.RepaintAll();
        }

        public void RefreshLevelList()
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
                SelectLevel(m_AllLevels[0], saveAsPlayerProgress: false);
            }

            SyncWithSceneLevelManager();
        }

        private void SelectLevel(PixelLevelData level, bool saveAsPlayerProgress = true)
        {
            m_SelectedLevel = level;
            Selection.activeObject = level;
            if (saveAsPlayerProgress) SaveLevelAsPlayerProgress(level);
            if (level != null)
            {
                m_GridColumnsPerRow = Mathf.Clamp(level.PoolColumns, 1, 8);
                m_TargetGridRows = Mathf.Max(1, level.PoolRows);
            }
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
            newLevel.OriginalSourceTexture = tex;
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


        /// <summary>
        /// Bölümleri akıllı zorluk puanına (Küp Sayısı × Renk Çeşitliliği × Slot Sayısı Baskısı) göre
        /// kolaydan zora sıralar ve numaralandırır.
        /// </summary>
        private void SortLevelsByDifficulty()
        {
            m_AllLevels.Sort((a, b) =>
            {
                var ra = LevelDifficultyReport.GetCached(a);
                var rb = LevelDifficultyReport.GetCached(b);
                int byTier = ((int)ra.tier).CompareTo((int)rb.tier);
                return byTier != 0 ? byTier : ra.score.CompareTo(rb.score);
            });

            AutoRenumberLevels();
            Debug.Log("<color=#00FFAA><b>[LevelDesigner]</b></color> Bölümler zorluk kademesine (Kolay → Orta → Zor, aynı kademede puana göre) sıralandı.");
        }

        /// <summary>
        /// Her bölüm için Zorluk Raporunu hesaplayıp sonucu (Kolay/Orta/Zor) kalıcı olarak
        /// level asset'ine yazar. HUD'daki HARD rozeti ve zorluk bilgisi bu kademeden okunur.
        /// </summary>
        private void AssignDifficultyTiersFromAnalysis()
        {
            int kolay = 0, orta = 0, zor = 0, atlanan = 0;

            foreach (var lvl in m_AllLevels)
            {
                if (lvl == null) continue;

                var r = LevelDifficultyReport.Compute(lvl);
                if (!r.valid)
                {
                    atlanan++;
                    continue;
                }

                LevelDifficultyTier tier;
                switch (r.tier)
                {
                    case LevelDifficultyReport.Tier.Orta: tier = LevelDifficultyTier.Orta; break;
                    case LevelDifficultyReport.Tier.Zor: tier = LevelDifficultyTier.Zor; break;
                    default: tier = LevelDifficultyTier.Kolay; break;
                }

                if (lvl.DifficultyTier != tier)
                    Undo.RecordObject(lvl, "Zorluk Kademesi Ata");

                lvl.DifficultyTier = tier;
                EditorUtility.SetDirty(lvl);

                switch (tier)
                {
                    case LevelDifficultyTier.Kolay: kolay++; break;
                    case LevelDifficultyTier.Orta: orta++; break;
                    default: zor++; break;
                }
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"<color=#00FFAA><b>[LevelDesigner]</b></color> Zorluk kademeleri analizden atandı — " +
                      $"🟢 Kolay: {kolay} · 🟡 Orta: {orta} · 🔴 Zor: {zor}" +
                      (atlanan > 0 ? $" · ⚪ atlanan (geçersiz analiz): {atlanan}" : ""));

            EditorUtility.DisplayDialog(
                "Kademeler Atandı",
                $"🟢 Kolay: {kolay}\n🟡 Orta: {orta}\n🔴 Zor: {zor}" +
                (atlanan > 0 ? $"\n⚪ Atlanan (görsel analiz edilemedi): {atlanan}" : ""),
                "Tamam");
        }

        private const string LevelSequenceAssetPath = "Assets/Levels/LevelSequence.asset";
        private const string ProgressPrefKey = "PixelGame_CurrentLevelIndex";

        /// <summary>
        /// Tasarımcıda seçilen bölümü oyuncu ilerlemesi (PlayerPrefs) olarak yazar; böylece Play'e
        /// basınca kayıtlı ilerleme yerine bu bölüm açılır. Bölüm sırada yoksa dokunmaz.
        /// </summary>
        private static void SaveLevelAsPlayerProgress(PixelLevelData level)
        {
            if (level == null || Application.isPlaying) return;
            LevelSequence sequence = AssetDatabase.LoadAssetAtPath<LevelSequence>(LevelSequenceAssetPath);
            if (sequence == null) return;
            int index = sequence.Levels.IndexOf(level);
            if (index < 0) return;
            PlayerPrefs.SetInt(ProgressPrefKey, index);
            PlayerPrefs.Save();
        }

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
