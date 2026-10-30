using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Oyun panosunun (Board Podium) çerçeve, iç tuval, bordür ve derinlik çizgisi renklerini
    /// görsel ve canlı olarak düzenlemeyi sağlayan özel editör aracı.
    /// Renk değişikliklerini anında Scene/Game görünümüne yansıtır ve istenildiğinde dokulara (PNG) kalıcı kaydeder.
    /// </summary>
    public class BoardFrameColorEditorWindow : EditorWindow
    {
        private const string MaterialPath = "Assets/Materials/BoardPodium_Mat.mat";
        private const string MaskPath = "Assets/Textures/BoardPodium_Mask.png";
        private const string TexPath = "Assets/Textures/BoardPodium_Reference.png";
        private const string ResPath = "Assets/Resources/BoardPodium_Reference.png";

        [SerializeField] private Color m_OuterColor = new Color(0.706f, 0.761f, 0.804f, 1f);     // #B4C2CD
        [SerializeField] private Color m_CanvasColor = new Color(0.898f, 0.925f, 0.949f, 1f);    // #E5ECF2
        [SerializeField] private Color m_RimColor = new Color(0.686f, 0.745f, 0.792f, 1f);       // #AFBECA
        [SerializeField] private Color m_GrooveColor = new Color(0.412f, 0.463f, 0.510f, 1f);    // #697682
        [SerializeField] private Color m_ShadowColor = new Color(0.078f, 0.110f, 0.149f, 1f);    // #141C26

        private Material m_CachedMaterial;
        private PixelArtGenerator m_CachedGenerator;
        private Vector2 m_ScrollPos;
        private string m_StatusMessage = "";
        private MessageType m_StatusType = MessageType.Info;
        private double m_StatusTime = 0;

        // [MenuItem("PixelGame/🎨 Çerçeve ve Pano Renkleri (Board Frame Colors)", priority = 1)]
        [MenuItem("Window/PixelGame/🎨 Çerçeve Renkleri")]
        public static void Open()
        {
            var window = GetWindow<BoardFrameColorEditorWindow>(false, "🎨 Çerçeve Renkleri", true);
            window.minSize = new Vector2(420, 560);
            window.Show();
        }

        private void OnEnable()
        {
            LoadFromMaterialOrScene();
        }

        private void OnFocus()
        {
            FindSceneReferences();
        }

        private void FindSceneReferences()
        {
            if (m_CachedGenerator == null)
            {
                m_CachedGenerator = UnityEngine.Object.FindFirstObjectByType<PixelArtGenerator>();
            }

            if (m_CachedMaterial == null)
            {
                m_CachedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            }
        }

        private void LoadFromMaterialOrScene()
        {
            FindSceneReferences();

            if (m_CachedMaterial != null)
            {
                if (m_CachedMaterial.HasProperty("_OuterColor")) m_OuterColor = m_CachedMaterial.GetColor("_OuterColor");
                if (m_CachedMaterial.HasProperty("_CanvasColor")) m_CanvasColor = m_CachedMaterial.GetColor("_CanvasColor");
                if (m_CachedMaterial.HasProperty("_RimColor")) m_RimColor = m_CachedMaterial.GetColor("_RimColor");
                if (m_CachedMaterial.HasProperty("_GrooveColor")) m_GrooveColor = m_CachedMaterial.GetColor("_GrooveColor");
                if (m_CachedMaterial.HasProperty("_ShadowColor")) m_ShadowColor = m_CachedMaterial.GetColor("_ShadowColor");
            }
            else if (m_CachedGenerator != null)
            {
                m_OuterColor = m_CachedGenerator.BoardOuterColor;
                m_CanvasColor = m_CachedGenerator.BoardCanvasColor;
                m_RimColor = m_CachedGenerator.BoardRimColor;
                m_GrooveColor = m_CachedGenerator.BoardGrooveColor;
                m_ShadowColor = m_CachedGenerator.BoardOuterShadowColor;
            }
        }

        private void OnGUI()
        {
            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);

            DrawHeader();
            EditorGUILayout.Space(6);

            DrawConnectionStatus();
            EditorGUILayout.Space(8);

            DrawColorControls();
            EditorGUILayout.Space(12);

            DrawMiniPreview();
            EditorGUILayout.Space(12);

            DrawPresetsSection();
            EditorGUILayout.Space(12);

            DrawActionButtons();
            EditorGUILayout.Space(10);

            DrawStatusMessage();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(4);
            EditorGUILayout.LabelField("🎨 Pano & Çerçeve Renkleri Düzenleyici", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Piksel sanat panosunun çerçeve, iç tuval, bordür ve derinlik çizgisi renklerini buradan " +
                "canlı olarak değiştirebilir, önizleyebilir ve dokuya kaydedebilirsiniz.",
                EditorStyles.wordWrappedMiniLabel
            );
            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        private void DrawConnectionStatus()
        {
            FindSceneReferences();

            EditorGUILayout.BeginHorizontal();
            if (m_CachedGenerator != null)
            {
                GUI.color = new Color(0.3f, 1f, 0.4f);
                GUILayout.Label("●", GUILayout.Width(14));
                GUI.color = Color.white;
                EditorGUILayout.LabelField($"Sahne Jeneratörü: {m_CachedGenerator.gameObject.name} (Canlı Senkronize)", EditorStyles.miniLabel);
            }
            else
            {
                GUI.color = new Color(1f, 0.7f, 0.2f);
                GUILayout.Label("●", GUILayout.Width(14));
                GUI.color = Color.white;
                EditorGUILayout.LabelField("Sahne jeneratörü aranıyor... (Materyal üzerinden canlı çalışıyor)", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawColorControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🖌️ Renk Ayarları", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();

            m_OuterColor = EditorGUILayout.ColorField(
                new GUIContent("🖼️ Dış Podyum Rengi", "Çerçevenin en dışındaki podyum / zemin rengi."),
                m_OuterColor, true, false, false
            );

            m_CanvasColor = EditorGUILayout.ColorField(
                new GUIContent("⬜ İç Tuval / Zemin Rengi", "Küplerin oturduğu iç oyun alanı zemin rengi."),
                m_CanvasColor, true, false, false
            );

            m_RimColor = EditorGUILayout.ColorField(
                new GUIContent("🔲 Çerçeve Bordür / Çıta", "Tuval ile podyum arasındaki ana çerçeve bordür rengi."),
                m_RimColor, true, false, false
            );

            m_GrooveColor = EditorGUILayout.ColorField(
                new GUIContent("📐 Derinlik Çizgisi Rengi", "Çerçeve çıtası ile podyum arasındaki ince derinlik/oluk çizgisi."),
                m_GrooveColor, true, false, false
            );

            m_ShadowColor = EditorGUILayout.ColorField(
                new GUIContent("🌑 Dış Gölge Rengi", "Podyumun en dışındaki yumuşak sahte gölge rengi."),
                m_ShadowColor, true, true, false
            );

            if (EditorGUI.EndChangeCheck())
            {
                ApplyColorsLive();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        private void DrawMiniPreview()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("👁️ Canlı Önizleme Simülasyonu", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            Rect previewRect = GUILayoutUtility.GetRect(position.width - 40, 110);
            if (Event.current.type == EventType.Repaint)
            {
                // Arka plan (Sahil / Masa rengi)
                EditorGUI.DrawRect(previewRect, new Color(0.85f, 0.90f, 0.94f));

                // 1. Dış Gölge
                Rect shadowRect = new Rect(previewRect.x + 22, previewRect.y + 12, previewRect.width - 44, previewRect.height - 20);
                EditorGUI.DrawRect(shadowRect, m_ShadowColor);

                // 2. Dış Podyum
                Rect outerRect = new Rect(previewRect.x + 20, previewRect.y + 10, previewRect.width - 44, previewRect.height - 24);
                EditorGUI.DrawRect(outerRect, m_OuterColor);

                // 3. Oluk / Derinlik Çizgisi
                Rect grooveRect = new Rect(outerRect.x + 12, outerRect.y + 10, outerRect.width - 24, outerRect.height - 20);
                EditorGUI.DrawRect(grooveRect, m_GrooveColor);

                // 4. Çerçeve Bordürü
                Rect rimRect = new Rect(grooveRect.x + 2, grooveRect.y + 2, grooveRect.width - 4, grooveRect.height - 4);
                EditorGUI.DrawRect(rimRect, m_RimColor);

                // 5. İç Tuval Zemin
                Rect canvasRect = new Rect(rimRect.x + 10, rimRect.y + 8, rimRect.width - 20, rimRect.height - 16);
                EditorGUI.DrawRect(canvasRect, m_CanvasColor);

                // 6. Örnek Piksel Küpleri
                float cubeW = 14f;
                float cubeH = 14f;
                float startX = canvasRect.center.x - 22f;
                float startY = canvasRect.center.y - 7f;

                EditorGUI.DrawRect(new Rect(startX, startY, cubeW, cubeH), new Color(0.92f, 0.28f, 0.24f)); // Kırmızı
                EditorGUI.DrawRect(new Rect(startX + 16, startY, cubeW, cubeH), new Color(0.96f, 0.72f, 0.20f)); // Sarı
                EditorGUI.DrawRect(new Rect(startX + 32, startY, cubeW, cubeH), new Color(0.22f, 0.65f, 0.94f)); // Mavi
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        private void DrawPresetsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⭐ Hazır Renk Paletleri (Presets)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Tek tıkla deneyebileceğiniz modern uyumlu temalar:", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔘 Modern Arduvaz Gri", GUILayout.Height(28)))
            {
                ApplyPreset(
                    new Color(0.706f, 0.761f, 0.804f, 1f), // Dış
                    new Color(0.898f, 0.925f, 0.949f, 1f), // Tuval
                    new Color(0.686f, 0.745f, 0.792f, 1f), // Bordür
                    new Color(0.412f, 0.463f, 0.510f, 1f), // Çizgi
                    new Color(0.078f, 0.110f, 0.149f, 0.65f) // Gölge
                );
            }

            if (GUILayout.Button("⚪ Minimal Açık Beyaz", GUILayout.Height(28)))
            {
                ApplyPreset(
                    new Color(0.886f, 0.910f, 0.941f, 1f),
                    new Color(0.990f, 0.995f, 1.000f, 1f),
                    new Color(0.796f, 0.835f, 0.882f, 1f),
                    new Color(0.580f, 0.639f, 0.722f, 1f),
                    new Color(0.060f, 0.090f, 0.165f, 0.55f)
                );
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🏖️ Sıcak Kumsal Bej", GUILayout.Height(28)))
            {
                ApplyPreset(
                    new Color(0.910f, 0.863f, 0.812f, 1f),
                    new Color(0.980f, 0.965f, 0.941f, 1f),
                    new Color(0.855f, 0.769f, 0.686f, 1f),
                    new Color(0.569f, 0.490f, 0.416f, 1f),
                    new Color(0.173f, 0.125f, 0.086f, 0.65f)
                );
            }

            if (GUILayout.Button("🌊 Deniz Turkuazı", GUILayout.Height(28)))
            {
                ApplyPreset(
                    new Color(0.737f, 0.878f, 0.910f, 1f),
                    new Color(0.949f, 0.980f, 0.988f, 1f),
                    new Color(0.647f, 0.824f, 0.863f, 1f),
                    new Color(0.329f, 0.525f, 0.588f, 1f),
                    new Color(0.063f, 0.149f, 0.188f, 0.65f)
                );
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🌑 Koyu Karbon Slate", GUILayout.Height(28)))
            {
                ApplyPreset(
                    new Color(0.173f, 0.212f, 0.267f, 1f),
                    new Color(0.102f, 0.133f, 0.176f, 1f),
                    new Color(0.267f, 0.322f, 0.400f, 1f),
                    new Color(0.086f, 0.110f, 0.141f, 1f),
                    new Color(0.020f, 0.030f, 0.050f, 0.85f)
                );
            }

            if (GUILayout.Button("↩️ Sahneden Geri Yükle", GUILayout.Height(28)))
            {
                LoadFromMaterialOrScene();
                ShowStatus("Renkler sahneden başarıyla geri yüklendi.", MessageType.Info);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginVertical();

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.45f);
            if (GUILayout.Button("💾 DOKULARA KALICI KAYDET (Bake to PNG)", GUILayout.Height(36)))
            {
                BakeColorsToTexture();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Sahneye Canlı Uygula", GUILayout.Height(26)))
            {
                ApplyColorsLive();
                ShowStatus("Renkler materyale ve sahneye canlı olarak uygulandı.", MessageType.Info);
            }

            if (GUILayout.Button("📸 9:16 Ekran Görüntüsü Al", GUILayout.Height(26)))
            {
                CaptureGameViewScreenshot.CaptureGemiScene();
                ShowStatus("9:16 Ekran görüntüsü alındı (scratch/gameplay_view_9_16.png).", MessageType.Info);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawStatusMessage()
        {
            if (!string.IsNullOrEmpty(m_StatusMessage))
            {
                if (EditorApplication.timeSinceStartup - m_StatusTime < 8.0)
                {
                    EditorGUILayout.HelpBox(m_StatusMessage, m_StatusType);
                }
            }
        }

        private void ShowStatus(string message, MessageType type)
        {
            m_StatusMessage = message;
            m_StatusType = type;
            m_StatusTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void ApplyPreset(Color outer, Color canvas, Color rim, Color groove, Color shadow)
        {
            m_OuterColor = outer;
            m_CanvasColor = canvas;
            m_RimColor = rim;
            m_GrooveColor = groove;
            m_ShadowColor = shadow;
            ApplyColorsLive();
            ShowStatus("Hazır palet uygulandı! Canlı olarak sahneye yansıtıldı.", MessageType.Info);
        }

        /// <summary>
        /// Seçilen renkleri hem Material'a hem de sahnedeki PixelArtGenerator'a anında canlı uygular.
        /// </summary>
        public void ApplyColorsLive()
        {
            FindSceneReferences();

            // 1. Materyal Properties
            if (m_CachedMaterial != null)
            {
                Undo.RecordObject(m_CachedMaterial, "Change Board Frame Colors");
                m_CachedMaterial.SetColor("_OuterColor", m_OuterColor);
                m_CachedMaterial.SetColor("_CanvasColor", m_CanvasColor);
                m_CachedMaterial.SetColor("_RimColor", m_RimColor);
                m_CachedMaterial.SetColor("_GrooveColor", m_GrooveColor);
                m_CachedMaterial.SetColor("_ShadowColor", m_ShadowColor);
                EditorUtility.SetDirty(m_CachedMaterial);
            }

            // 2. Sahne Jeneratörü
            if (m_CachedGenerator != null)
            {
                Undo.RecordObject(m_CachedGenerator, "Change Board Frame Colors");
                m_CachedGenerator.BoardOuterColor = m_OuterColor;
                m_CachedGenerator.BoardCanvasColor = m_CanvasColor;
                m_CachedGenerator.BoardRimColor = m_RimColor;
                m_CachedGenerator.BoardGrooveColor = m_GrooveColor;
                m_CachedGenerator.BoardOuterShadowColor = m_ShadowColor;
                m_CachedGenerator.UpdateBoardFrameColorsLive();
                EditorUtility.SetDirty(m_CachedGenerator);
            }

            SceneView.RepaintAll();
        }

        /// <summary>
        /// Renkleri BoardPodium_Mask.png üzerinden hesaplayarak BoardPodium_Reference.png dokusuna kalıcı olarak yazar.
        /// </summary>
        public void BakeColorsToTexture()
        {
            ApplyColorsLive();

            if (!File.Exists(MaskPath))
            {
                ShowStatus($"Hata: Maske dosyası bulunamadı: {MaskPath}", MessageType.Error);
                return;
            }

            try
            {
                byte[] maskBytes = File.ReadAllBytes(MaskPath);
                Texture2D maskTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!maskTex.LoadImage(maskBytes))
                {
                    DestroyImmediate(maskTex);
                    ShowStatus("Hata: Maske görseli yüklenemedi!", MessageType.Error);
                    return;
                }

                int w = maskTex.width;
                int h = maskTex.height;
                Color32[] maskPix = maskTex.GetPixels32();
                Color32[] outPix = new Color32[maskPix.Length];

                Color cOuter = m_OuterColor;
                Color cRim = m_RimColor;
                Color cGroove = m_GrooveColor;
                Color cCanvas = m_CanvasColor;
                Color cShadow = m_ShadowColor;

                for (int i = 0; i < maskPix.Length; i++)
                {
                    Color32 m = maskPix[i];
                    float r = m.r / 255f;
                    float g = m.g / 255f;
                    float b = m.b / 255f;
                    float a = m.a / 255f;

                    float isSolid = Mathf.Clamp01(r + g + b);
                    float isGroove = Mathf.Clamp01(1f - isSolid) * (a > 0.9f ? 1f : 0f);

                    if (a > 0.95f)
                    {
                        Color col = cOuter * r + cRim * g + cCanvas * b + cGroove * isGroove;
                        col.a = 1f;
                        outPix[i] = col;
                    }
                    else if (a > 0.001f)
                    {
                        Color col = cShadow;
                        col.a = a;
                        outPix[i] = col;
                    }
                    else
                    {
                        outPix[i] = new Color32(0, 0, 0, 0);
                    }
                }

                Texture2D outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                outTex.SetPixels32(outPix);
                outTex.Apply();

                byte[] pngBytes = outTex.EncodeToPNG();
                File.WriteAllBytes(TexPath, pngBytes);
                if (File.Exists(ResPath) || Directory.Exists("Assets/Resources"))
                {
                    File.WriteAllBytes(ResPath, pngBytes);
                }

                DestroyImmediate(maskTex);
                DestroyImmediate(outTex);

                AssetDatabase.ImportAsset(TexPath, ImportAssetOptions.ForceUpdate);
                if (File.Exists(ResPath))
                {
                    AssetDatabase.ImportAsset(ResPath, ImportAssetOptions.ForceUpdate);
                }

                ShowStatus("✓ Renkler BoardPodium_Reference.png dokusuna kalıcı olarak başarıyla kaydedildi!", MessageType.Info);
                Debug.Log($"<color=#00FFAA><b>[PixelGame]</b></color> Çerçeve renkleri dokulara kaydedildi! Dış: #{ColorUtility.ToHtmlStringRGB(cOuter)}, Tuval: #{ColorUtility.ToHtmlStringRGB(cCanvas)}");
            }
            catch (Exception ex)
            {
                ShowStatus($"Bake hatası: {ex.Message}", MessageType.Error);
                Debug.LogException(ex);
            }
        }
    }
}
