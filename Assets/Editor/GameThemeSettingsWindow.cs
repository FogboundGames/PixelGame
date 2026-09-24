using System;
using UnityEngine;
using UnityEditor;

namespace PixelGame.Editor
{
    /// <summary>
    /// Tüm oyun genelindeki Vagon, Madenci, Ray/Çevre parça renklerini ve
    /// Toony Colors Pro cel-shaded ışık/gölge ayarlarını merkezi olarak yöneten pencere.
    /// Bölümlerden bağımsız, global bir kontrol merkezidir.
    /// </summary>
    public class GameThemeSettingsWindow : EditorWindow
    {
        private Vector2 m_ScrollPos;
        private Color m_TestColor = new Color32(230, 40, 40, 255);

        // [MenuItem("Tools/PixelGame/🎨 Genel Tema & Renk Ayarları", priority = 10)]
        [MenuItem("Window/PixelGame/Genel Tema Ayarları")]
        public static void OpenWindow()
        {
            var window = GetWindow<GameThemeSettingsWindow>("Tema & Renk Ayarları");
            window.minSize = new Vector2(620, 500);
            window.Show();
        }

        private void OnEnable()
        {
            var settings = GameThemeSettings.Instance;
            if (settings != null)
            {
                m_TestColor = settings.PreviewBlockColor;
            }
        }

        private void OnGUI()
        {
            var settings = GameThemeSettings.Instance;
            if (settings == null)
            {
                EditorGUILayout.HelpBox("GameThemeSettings asset'i yüklenemedi.", MessageType.Error);
                return;
            }

            LevelColorTheme theme = settings.Theme;

            // Üst Başlık Barı
            DrawHeader();

            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);
            EditorGUILayout.Space(8);

            // Bilgilendirme Kutusu
            EditorGUILayout.HelpBox(
                "💡 Burada yaptığınız renk ve Toon shader ayarları OYUNUN TÜM BÖLÜMLERİNDE geçerlidir.\n" +
                "Her bölüm için tek tek ayar yapmanıza gerek kalmaz. Parçaları '✓ Vagon Rengini Kullan' " +
                "yaparak vagonun/küpün o anki rengini dinamik olarak almasını sağlayabilir veya sabit özel renk verebilirsiniz.",
                MessageType.Info
            );
            EditorGUILayout.Space(6);

            // 🧪 Canlı Test / Önizleme Bloğu Çubuğu
            DrawTestColorBar(settings);

            EditorGUILayout.Space(10);

            EditorGUI.BeginChangeCheck();

            // 1. 🚚 VAGON PARÇALARI
            DrawSectionHeader("🚚 Vagon (MineCart / ToyTruck) Renkleri");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawPartRow(theme, TruckPart.Cabin, "Kabin (Kabin + Kaput)", "🚛");
            DrawPartRow(theme, TruckPart.Cargo, "Kasa (Kargo + Arka Kapak)", "📦");
            DrawPartRow(theme, TruckPart.Rims, "Jantlar (Rims)", "⚙️");
            DrawPartRow(theme, TruckPart.Tires, "Tekerlekler (Tires)", "🛞");
            DrawPartRow(theme, TruckPart.Glass, "Camlar (Glass)", "🪟");
            DrawPartRow(theme, TruckPart.Headlights, "Ön Farlar (Headlights)", "💡");
            DrawPartRow(theme, TruckPart.Taillights, "Arka Stoplar (Taillights)", "🔴");
            DrawPartRow(theme, TruckPart.Chassis, "Şasi / Alt Gövde", "🔧");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 2. ⛏️ MADENCİ KARAKTERİ
            DrawSectionHeader("⛏️ Madenci (MechaMiner) Renkleri");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawPartRow(theme, TruckPart.MechaBody, "Karakter Gövdesi (Zırh)", "🤖");
            DrawPartRow(theme, TruckPart.Helmet, "Baret (Kubbe & Siperlik)", "⛑️");
            DrawPartRow(theme, TruckPart.HelmetDark, "Baret Detayı / Koyu Ton", "🕶️");
            DrawPartRow(theme, TruckPart.Lamp, "Baret Fener Lambası", "🔦");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 3. 🛤️ RAY VE MADEN GİRİŞİ
            DrawSectionHeader("🛤️ Ray & Maden Girişi (Portal) Renkleri");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawPartRow(theme, TruckPart.Stone, "Portal Taşları", "🪨");
            DrawPartRow(theme, TruckPart.StoneDark, "Taşların Koyu Tonu", "⬛");
            DrawPartRow(theme, TruckPart.Wood, "Ahşap Traversler & Direkler", "🪵");
            DrawPartRow(theme, TruckPart.Dark, "Maden Girişi İç Karanlığı", "🕳️");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 4. 🎨 TOONY COLORS PRO (KÜP MATERYAL & TOON AYARLARI)
            DrawToonyColorsProSection();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
                ApplyThemeToSceneLive(theme);
            }

            EditorGUILayout.Space(14);

            // Alt Butonlar
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUILayout.Button("🔄 Varsayılan Temaya Sıfırla", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Temayı Sıfırla", "Tüm genel parça renkleri varsayılan fabrika ayarlarına dönecek. Emin misiniz?", "Evet, Sıfırla", "İptal"))
                {
                    theme.ResetToDefault();
                    EditorUtility.SetDirty(settings);
                    ApplyThemeToSceneLive(theme);
                }
            }

            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.45f);
            if (GUILayout.Button("💾 Ayarları Kaydet", GUILayout.Height(28)))
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                ApplyThemeToSceneLive(theme);
                Debug.Log("<color=#00FFAA><b>[GameThemeSettings]</b></color> Global tema ayarları kaydedildi.");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(16);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 42);
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.16f, 0.24f, 1f));

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.35f, 0.9f, 1f) }
            };

            GUI.Label(new Rect(rect.x + 14, rect.y, rect.width - 200, rect.height), "🎨 Pixel Game - Genel Tema & Renk Ayarları", titleStyle);

            if (GUI.Button(new Rect(rect.xMax - 180, rect.y + 8, 168, 26), "🛠️ Level Designer'ı Aç"))
            {
                PixelLevelDesignerWindow.OpenWindow();
            }
        }

        private void DrawSectionHeader(string title)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 24);
            EditorGUI.DrawRect(r, new Color(0.18f, 0.22f, 0.30f, 1f));
            GUIStyle st = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.4f, 0.88f, 1f) },
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12
            };
            GUI.Label(new Rect(r.x + 10, r.y + 2, r.width - 20, r.height), title, st);
        }

        private void DrawTestColorBar(GameThemeSettings settings)
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
                    m_TestColor = qc;
                    settings.PreviewBlockColor = qc;
                    EditorUtility.SetDirty(settings);
                    ApplyThemeToSceneLive(settings.Theme);
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(6);
            Color newTest = EditorGUILayout.ColorField(GUIContent.none, m_TestColor, false, false, false, GUILayout.Width(70));
            if (newTest != m_TestColor)
            {
                m_TestColor = newTest;
                settings.PreviewBlockColor = newTest;
                EditorUtility.SetDirty(settings);
                ApplyThemeToSceneLive(settings.Theme);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPartRow(LevelColorTheme theme, TruckPart part, string displayName, string icon)
        {
            TruckPartColorSetting setting = theme.GetSetting(part);
            Color resolvedColor = theme.ResolveColor(part, m_TestColor);

            EditorGUILayout.BeginHorizontal();

            // İkon ve İsim
            GUILayout.Label($"{icon} {displayName}", GUILayout.Width(220));

            // "Vagon Rengini Kullan" Butonu / Toggle
            GUI.backgroundColor = setting.matchBlockColor ? new Color(0.25f, 0.85f, 0.45f) : new Color(0.85f, 0.85f, 0.88f);
            string btnText = setting.matchBlockColor ? "✓ Vagon Rengini Kullan" : "  Vagon Rengini Kullan";
            if (GUILayout.Button(btnText, GUILayout.Width(170), GUILayout.Height(20)))
            {
                setting.matchBlockColor = !setting.matchBlockColor;
                ApplyThemeToSceneLive(theme);
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
                    ApplyThemeToSceneLive(theme);
                }
                GUILayout.Label("(Sabit özel renk)", EditorStyles.miniLabel);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToonyColorsProSection()
        {
            DrawSectionHeader("🎨 Toony Colors Pro (Küp Çizgi Film Işık & Gölgelendirme)");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Küp Materyali Canlı Ayarları (PixelCube_Cartoon)", EditorStyles.boldLabel);

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

            EditorGUILayout.HelpBox("Piksel küplerinin çizgi film / cel-shaded tonlarını ve gölgelerini buradan anında canlı ayarlayabilirsiniz.", MessageType.None);
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

            EditorGUILayout.Space(8);

            // 🍬 JELİBON & PLASTİK KÜP AYARLARI (HER KÜPTE EŞİT PARLAMA)
            EditorGUILayout.LabelField("🍬 Plastik & Jelibon Görünümü (Her Küpte Sabit Parlama):", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Kamera açısına veya ışık yönüne bağlı olmadan, her bir küpün sol-üst-ön kavisinde sabit plastik parlama ve kenar gölgesi oluşturur.", MessageType.None);

            bool plasticOn = cartoonMat.HasProperty("_StylizedPlasticOn") && cartoonMat.GetFloat("_StylizedPlasticOn") > 0.5f;
            bool newPlasticOn = EditorGUILayout.ToggleLeft("✨ Plastik Parlama & Bevel Efektini Aktif Et", plasticOn, EditorStyles.boldLabel);
            if (newPlasticOn != plasticOn)
            {
                Undo.RecordObject(cartoonMat, "Toggle Stylized Plastic");
                cartoonMat.SetFloat("_StylizedPlasticOn", newPlasticOn ? 1f : 0f);
                EditorUtility.SetDirty(cartoonMat);
                SceneView.RepaintAll();
            }

            if (newPlasticOn)
            {
                // Pillow Roundness (Küresel Dolgunluk - Küpün ön yüzünü yastık gibi bombeli gösterip parlamayı doğal oturtur)
                float pRoundness = cartoonMat.HasProperty("_PillowRoundness") ? cartoonMat.GetFloat("_PillowRoundness") : 0.5f;
                float newPRoundness = EditorGUILayout.Slider("🍬 Jelibon Dolgunluğu (Pillow)", pRoundness, 0f, 1f);

                // Highlight Angle X (Sol-Sağ Çapraz Açı)
                float pAngleX = cartoonMat.HasProperty("_PlasticAngleX") ? cartoonMat.GetFloat("_PlasticAngleX") : 0.42f;
                float newPAngleX = EditorGUILayout.Slider("📐 Parlama Açısı (Sol / Sağ)", pAngleX, -1f, 1f);

                // Highlight Intensity
                float pIntensity = cartoonMat.HasProperty("_PlasticHighlightIntensity") ? cartoonMat.GetFloat("_PlasticHighlightIntensity") : 1.5f;
                float newPIntensity = EditorGUILayout.Slider("💡 Parlama Gücü (Intensity)", pIntensity, 0f, 5f);

                // Roughness / Pürüzsüzlük
                float pRoughness = cartoonMat.HasProperty("_SpecularRoughnessPBR") ? cartoonMat.GetFloat("_SpecularRoughnessPBR") : 0.35f;
                float newPRoughness = EditorGUILayout.Slider("✨ Cila / Parlaklık (Roughness)", pRoughness, 0.05f, 0.95f);

                // Top Light Boost
                float pTop = cartoonMat.HasProperty("_PlasticTopLight") ? cartoonMat.GetFloat("_PlasticTopLight") : 0.22f;
                float newPTop = EditorGUILayout.Slider("☀️ Tavan Aydınlığı (Top Light)", pTop, 0f, 1f);

                // Bevel AO
                float pAO = cartoonMat.HasProperty("_PlasticBevelAO") ? cartoonMat.GetFloat("_PlasticBevelAO") : 0.4f;
                float newPAO = EditorGUILayout.Slider("🌑 Kenar Ayrımı / Gölge (Bevel AO)", pAO, 0f, 1f);

                // Procedural Bevel Intensity (0 = temiz jelibon nokta parlaması, >0 = beyaz kenar çizgisi)
                float pProcBevel = cartoonMat.HasProperty("_ProceduralBevelIntensity") ? cartoonMat.GetFloat("_ProceduralBevelIntensity") : 0f;
                float newPProcBevel = EditorGUILayout.Slider("📐 Beyaz Kenar Çizgisi (Bevel)", pProcBevel, 0f, 2f);

                // Highlight Color
                Color pHlCol = cartoonMat.HasProperty("_PlasticHighlightColor") ? cartoonMat.GetColor("_PlasticHighlightColor") : Color.white;
                Color newPHlCol = EditorGUILayout.ColorField("✨ Parlama Rengi", pHlCol);

                if (newPRoundness != pRoundness || newPAngleX != pAngleX || newPIntensity != pIntensity || newPRoughness != pRoughness || newPTop != pTop || newPAO != pAO || newPProcBevel != pProcBevel || newPHlCol != pHlCol)
                {
                    Undo.RecordObject(cartoonMat, "Modify Plastic Settings");
                    cartoonMat.SetFloat("_PillowRoundness", newPRoundness);
                    cartoonMat.SetFloat("_PlasticAngleX", newPAngleX);
                    cartoonMat.SetFloat("_PlasticHighlightIntensity", newPIntensity);
                    cartoonMat.SetFloat("_SpecularRoughnessPBR", newPRoughness);
                    cartoonMat.SetFloat("_PlasticTopLight", newPTop);
                    cartoonMat.SetFloat("_PlasticBevelAO", newPAO);
                    cartoonMat.SetFloat("_ProceduralBevelIntensity", newPProcBevel);
                    cartoonMat.SetColor("_PlasticHighlightColor", newPHlCol);
                    EditorUtility.SetDirty(cartoonMat);
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.Space(6);

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

        public void ApplyThemeToSceneLive(LevelColorTheme theme)
        {
            if (theme == null) return;

            TruckPaint[] paints = UnityEngine.Object.FindObjectsByType<TruckPaint>(FindObjectsSortMode.None);
            foreach (var paint in paints)
            {
                if (paint != null)
                {
                    paint.ApplyTheme(theme, m_TestColor);
                }
            }

            SceneView.RepaintAll();
        }
    }
}
