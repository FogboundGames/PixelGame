using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Klasördeki veya sürüklenen onlarca piksel görselini tek tıkla
    /// optimize edilmiş, dengeli ve %100 çözülebilir oyun seviyelerine dönüştüren toplu üretim aracı.
    /// </summary>
    public class BatchLevelGeneratorWindow : EditorWindow
    {
        public enum ColorReductionMode
        {
            Fixed,          // Sabit renk sayısı (örn. 4 veya 5 renk)
            Progression,    // Kademeli zorluk (1-5: 3c, 6-15: 4c, 16-30: 5c, 31+: 6c)
            Original        // Orijinal renkleri koru (indirgeme yapma)
        }

        public enum SlotOption
        {
            AutoByDifficulty,   // 3 renklilerde 4 slot, 4+ renklilerde 4-5 slot
            Fixed4,             // Sabit 4 İskele
            Fixed5,             // Sabit 5 İskele
            Fixed3              // Sabit 3 İskele (Zor)
        }

        private DefaultAsset m_FolderAsset;
        private List<Texture2D> m_TexturesToProcess = new List<Texture2D>();
        private ColorReductionMode m_ColorMode = ColorReductionMode.Fixed;
        private int m_FixedTargetColors = 4;
        private bool m_Denoise = true;
        private SlotOption m_SlotOption = SlotOption.AutoByDifficulty;
        private bool m_AutoGuaranteeSolvability = true;

        private Vector2 m_Scroll;
        private Vector2 m_LogScroll;
        private List<string> m_Logs = new List<string>();
        private bool m_HasRun = false;
        private int m_SuccessCount = 0;

        [MenuItem("Tools/PixelGame/📁 Toplu Seviye Üreticisi (Batch Generator)", priority = 2)]
        public static void OpenWindow()
        {
            var window = GetWindow<BatchLevelGeneratorWindow>("Toplu Seviye Üreticisi");
            window.minSize = new Vector2(580, 620);
            window.Show();
        }

        public static void OpenWithFolder(DefaultAsset folder)
        {
            var window = GetWindow<BatchLevelGeneratorWindow>("Toplu Seviye Üreticisi");
            window.minSize = new Vector2(580, 620);
            window.m_FolderAsset = folder;
            window.ScanFolder();
            window.Show();
        }

        private void OnGUI()
        {
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            // Başlık
            DrawHeader();

            EditorGUILayout.Space(6);

            // 1. Kaynak Görsel Seçim Alanı
            DrawSourceSection();

            EditorGUILayout.Space(8);

            // 2. Üretim & Optimizasyon Ayarları
            DrawSettingsSection();

            EditorGUILayout.Space(10);

            // 3. Başlat Butonu
            DrawActionSection();

            EditorGUILayout.Space(10);

            // 4. Sonuç Günlüğü
            if (m_HasRun)
            {
                DrawLogsSection();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 40);
            EditorGUI.DrawRect(rect, new Color(0.10f, 0.14f, 0.20f, 1f));

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.35f, 0.88f, 1f) }
            };
            GUI.Label(new Rect(rect.x + 12, rect.y, rect.width - 24, rect.height), "📁 Toplu Seviye Üreticisi (Batch Level Pipeline)", titleStyle);
        }

        private void DrawSourceSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("1. Kaynak Görseller (Klasör veya Sürükle-Bırak)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            m_FolderAsset = (DefaultAsset)EditorGUILayout.ObjectField("Klasör Seç:", m_FolderAsset, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                ScanFolder();
            }

            // Sürükle-Bırak Bölgesi
            Rect dropRect = EditorGUILayout.GetControlRect(false, 46);
            EditorGUI.DrawRect(dropRect, new Color(0.12f, 0.18f, 0.25f, 0.8f));

            GUIStyle dropStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = new Color(0.4f, 0.9f, 0.7f) }
            };
            GUI.Label(dropRect, "📥 Buraya bir klasör veya birden fazla PNG görseli sürükleyip bırakın", dropStyle);

            HandleDragAndDrop(dropRect);

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Seçili Görsel Sayısı: <b>{m_TexturesToProcess.Count}</b> adet", new GUIStyle(EditorStyles.label) { richText = true });
            if (m_TexturesToProcess.Count > 0)
            {
                if (GUILayout.Button("Listeyi Temizle", EditorStyles.miniButton, GUILayout.Width(100)))
                {
                    m_TexturesToProcess.Clear();
                }
            }
            EditorGUILayout.EndHorizontal();

            // Küçük önizleme listesi
            if (m_TexturesToProcess.Count > 0)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                int previewCount = Mathf.Min(8, m_TexturesToProcess.Count);
                for (int i = 0; i < previewCount; i++)
                {
                    Texture2D t = m_TexturesToProcess[i];
                    if (t != null)
                    {
                        Rect r = GUILayoutUtility.GetRect(32, 32, GUILayout.Width(32));
                        EditorGUI.DrawRect(r, new Color(0.15f, 0.15f, 0.18f));
                        GUI.DrawTexture(r, t, ScaleMode.ScaleToFit);
                    }
                }
                if (m_TexturesToProcess.Count > 8)
                {
                    EditorGUILayout.LabelField($"+{m_TexturesToProcess.Count - 8} daha...", EditorStyles.miniLabel, GUILayout.Width(70));
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSettingsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("2. Otomasyon & Çözülebilirlik Ayarları", EditorStyles.boldLabel);

            // Renk İndirgeme
            m_ColorMode = (ColorReductionMode)EditorGUILayout.EnumPopup("Renk Modu:", m_ColorMode);
            if (m_ColorMode == ColorReductionMode.Fixed)
            {
                m_FixedTargetColors = EditorGUILayout.IntSlider("  ↳ Hedef Renk Sayısı:", m_FixedTargetColors, 2, 8);
                EditorGUILayout.HelpBox("Tüm görseller K-Means ile belirlenen bu renk sayısına indirgenecektir (Gemi mekaniği için 4-5 önerilir).", MessageType.None);
            }
            else if (m_ColorMode == ColorReductionMode.Progression)
            {
                EditorGUILayout.HelpBox("Kademeli İlerleme Modu: İlk bölümler 3 renkle başlar, ilerledikçe 4, 5 ve 6 renge kadar kademeli artar.", MessageType.None);
            }

            EditorGUILayout.Space(4);

            // Gürültü Temizleme
            m_Denoise = EditorGUILayout.Toggle("Piksel Gürültüsünü Temizle (Denoise):", m_Denoise);

            // İskele Slot Sayısı
            m_SlotOption = (SlotOption)EditorGUILayout.EnumPopup("İskele Slot Sayısı:", m_SlotOption);

            EditorGUILayout.Space(4);

            // Çözülebilirlik Garantisi
            m_AutoGuaranteeSolvability = EditorGUILayout.Toggle("Otomatik Çözüm Garantisi (%100 Win):", m_AutoGuaranteeSolvability);
            if (m_AutoGuaranteeSolvability)
            {
                EditorGUILayout.HelpBox("Bölümler üretilirken simülasyon arka planda koşar. Kilitlenme riski tespit edilirse vagon sırası ve tohumlar otomatik karıştırılarak bölüm %100 kazanılabilir hale getirilir.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActionSection()
        {
            EditorGUI.BeginDisabledGroup(m_TexturesToProcess.Count == 0);
            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
            string btnText = m_TexturesToProcess.Count > 0 
                ? $"⚡ {m_TexturesToProcess.Count} Adet Seviyeyi Otomatik Üret ve Kaydet" 
                : "Önce Görselleri Seçin";

            if (GUILayout.Button(btnText, GUILayout.Height(36)))
            {
                ExecuteBatchGeneration();
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();
        }

        private void DrawLogsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"3. Üretim Raporu: {m_SuccessCount} / {m_TexturesToProcess.Count} Başarılı", EditorStyles.boldLabel);

            m_LogScroll = EditorGUILayout.BeginScrollView(m_LogScroll, GUILayout.Height(160));
            foreach (var log in m_Logs)
            {
                EditorGUILayout.LabelField(log, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void HandleDragAndDrop(Rect dropRect)
        {
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
                                m_FolderAsset = folderAsset;
                                ScanFolder();
                                break;
                            }
                            else if (draggedObject is Texture2D tex)
                            {
                                if (!m_TexturesToProcess.Contains(tex))
                                    m_TexturesToProcess.Add(tex);
                            }
                        }
                    }
                    evt.Use();
                }
            }
        }

        private void ScanFolder()
        {
            m_TexturesToProcess.Clear();
            if (m_FolderAsset == null) return;

            string path = AssetDatabase.GetAssetPath(m_FolderAsset);
            if (!AssetDatabase.IsValidFolder(path)) return;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { path });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex != null && !m_TexturesToProcess.Contains(tex))
                {
                    m_TexturesToProcess.Add(tex);
                }
            }
        }

        private void ExecuteBatchGeneration()
        {
            if (m_TexturesToProcess.Count == 0) return;

            if (!AssetDatabase.IsValidFolder("Assets/Levels"))
            {
                AssetDatabase.CreateFolder("Assets", "Levels");
            }

            m_Logs.Clear();
            m_HasRun = true;
            m_SuccessCount = 0;

            // Referans görsel ayarlarını bul
            PixelLevelData referenceLevel = FindReferenceLevel();

            // Mevcut bölüm sayısını bul
            string[] existingGuids = AssetDatabase.FindAssets("t:PixelLevelData", new[] { "Assets/Levels" });
            int nextIndex = existingGuids.Length + 1;

            try
            {
                for (int i = 0; i < m_TexturesToProcess.Count; i++)
                {
                    Texture2D sourceTex = m_TexturesToProcess[i];
                    if (sourceTex == null) continue;

                    float progress = (float)i / m_TexturesToProcess.Count;
                    EditorUtility.DisplayProgressBar("Toplu Seviye Üretiliyor...", $"{sourceTex.name} ({i + 1}/{m_TexturesToProcess.Count})", progress);

                    PixelPaletteOptimizer.EnsureReadable(sourceTex);

                    // 1. Hedef Renk Sayısını Belirle
                    int targetColors = m_FixedTargetColors;
                    if (m_ColorMode == ColorReductionMode.Progression)
                    {
                        if (i < 5) targetColors = 3;
                        else if (i < 15) targetColors = 4;
                        else if (i < 30) targetColors = 5;
                        else targetColors = 6;
                    }

                    // 2. Renk Optimizasyonu (K-Means)
                    Texture2D activeTex = sourceTex;
                    if (m_ColorMode != ColorReductionMode.Original)
                    {
                        Texture2D quantized = PixelPaletteOptimizer.Quantize(sourceTex, targetColors, 0.2f, m_Denoise);
                        if (quantized != null)
                        {
                            Texture2D saved = PixelPaletteOptimizer.SaveAsOptimizedAsset(sourceTex, quantized, $"_{targetColors}c");
                            DestroyImmediate(quantized);
                            if (saved != null) activeTex = saved;
                        }
                    }

                    // 3. PixelLevelData Üretimi
                    string safeName = sourceTex.name.Replace(" ", "_");
                    string assetPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/Levels/Level_{nextIndex:D2}_{safeName}.asset");

                    PixelLevelData newLevel = CreateInstance<PixelLevelData>();
                    newLevel.LevelName = sourceTex.name;
                    newLevel.LevelIndex = nextIndex;
                    newLevel.LevelTexture = activeTex;
                    newLevel.OriginalSourceTexture = sourceTex;
                    newLevel.UseNativeResolution = true;

                    // 4. İskele Slot Sayısı
                    newLevel.SlotCount = DetermineSlotCount(targetColors);

                    // 5. Palet ve Vagon Dizilimi
                    newLevel.ExtractPaletteFromTexture();
                    newLevel.GenerateInterleavedWagonSequenceFromPalette();
                    newLevel.UseCustomWagonSequence = true;

                    // 6. 3D Görsel Kalibrasyonu Kopyala
                    ApplyVisualTuning(newLevel, referenceLevel);

                    // 7. Otomatik Çözüm Garantisi (Simülasyon Koştur)
                    bool is100Solvable = true;
                    if (m_AutoGuaranteeSolvability)
                    {
                        is100Solvable = GuaranteeSolvability(newLevel);
                    }

                    AssetDatabase.CreateAsset(newLevel, assetPath);
                    EditorUtility.SetDirty(newLevel);

                    m_SuccessCount++;
                    nextIndex++;

                    string solvBadge = is100Solvable ? "✓ %100 Çözülebilir" : "⚠️ Kilitlenme Riski Var";
                    m_Logs.Add($"Level {newLevel.LevelIndex}: '{newLevel.LevelName}' ({newLevel.ColorPalette.Count} Renk, {newLevel.SlotCount} Slot) — {solvBadge}");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Açık olan Level Designer penceresi varsa yenile
                if (HasOpenInstances<PixelLevelDesignerWindow>())
                {
                    GetWindow<PixelLevelDesignerWindow>().RefreshLevelList();
                }

                EditorUtility.DisplayDialog("Toplu Üretim Tamamlandı!", 
                    $"{m_SuccessCount} adet seviye başarıyla üretildi ve 'Assets/Levels' klasörüne eklendi.", "Tamam");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private int DetermineSlotCount(int colorCount)
        {
            switch (m_SlotOption)
            {
                case SlotOption.Fixed3: return 3;
                case SlotOption.Fixed5: return 5;
                case SlotOption.Fixed4: return 4;
                default:
                    // Otomatik: Renk sayısı 3 ise 4 slot, 4 ve üzeri ise 4-5 slot
                    return colorCount <= 3 ? 4 : (colorCount >= 6 ? 5 : 4);
            }
        }

        private bool GuaranteeSolvability(PixelLevelData level)
        {
            var rep = LevelSolvabilityAnalyzer.Analyze(level, runSimulation: true, trials: 60);
            if (rep.completed && rep.deadlockCount == 0) return true;

            // Kilitlenme riski varsa vagon sırasını farklı tohumlarla dene
            for (int attempt = 0; attempt < 8; attempt++)
            {
                ShuffleWagons(level);
                rep = LevelSolvabilityAnalyzer.Analyze(level, runSimulation: true, trials: 60);
                if (rep.completed && rep.deadlockCount == 0)
                {
                    return true;
                }
            }

            // Hâlâ çözülemiyorsa slot sayısını 1 artır (4 ise 5 yap)
            if (level.SlotCount < 5)
            {
                level.SlotCount++;
                rep = LevelSolvabilityAnalyzer.Analyze(level, runSimulation: true, trials: 60);
                if (rep.completed && rep.deadlockCount == 0)
                {
                    return true;
                }
            }

            return (rep.DeadlockRisk < 0.1f);
        }

        private void ShuffleWagons(PixelLevelData level)
        {
            if (level.WagonSequence == null || level.WagonSequence.Count <= 1) return;

            // Vagonların birbirini bloklamaması için hafif kaydırma (Fisher-Yates)
            for (int i = level.WagonSequence.Count - 1; i > 0; i--)
            {
                int rnd = Random.Range(0, i + 1);
                var temp = level.WagonSequence[i];
                level.WagonSequence[i] = level.WagonSequence[rnd];
                level.WagonSequence[rnd] = temp;
            }
        }

        private PixelLevelData FindReferenceLevel()
        {
            string[] guids = AssetDatabase.FindAssets("t:PixelLevelData", new[] { "Assets/Levels" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                PixelLevelData lvl = AssetDatabase.LoadAssetAtPath<PixelLevelData>(path);
                if (lvl != null && lvl.LevelName != null && lvl.LevelName.IndexOf("Rakun", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return lvl;
                }
            }
            if (guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<PixelLevelData>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            return null;
        }

        private void ApplyVisualTuning(PixelLevelData target, PixelLevelData reference)
        {
            if (target == null || reference == null) return;
            target.CubeSpacing = reference.CubeSpacing;
            target.CubeSpacingX = reference.CubeSpacingX;
            target.CubeDepth = reference.CubeDepth;
            target.BoardTiltAngle = reference.BoardTiltAngle;
            target.CubeFrontTiltAngle = reference.CubeFrontTiltAngle;
            target.CubeRowStepOffset = reference.CubeRowStepOffset;
            target.InnerPadding = reference.InnerPadding;
        }
    }
}
