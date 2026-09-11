using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace PixelGame.Editor
{
    [CustomEditor(typeof(PixelArtGenerator))]
    public class PixelArtGeneratorEditor : UnityEditor.Editor
    {
        private PixelArtGenerator m_Target;

        private void OnEnable()
        {
            m_Target = (PixelArtGenerator)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Özel Başlık
            DrawCustomHeader();

            // Bölüm Seçici (Hızlı Geçiş)
            DrawLevelSelector();

            // Varsayılan alanlar
            DrawDefaultInspector();

            // Renk Hazır Ayarları (Presets)
            DrawColorPresets();

            // Texture Okunabilirlik Kontrolü
            CheckTextureReadability();

            // Canlı Bilgi Paneli
            DrawInfoBox();

            // Aksiyon Butonları
            DrawActionButtons();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCustomHeader()
        {
            EditorGUILayout.Space(6);
            Rect rect = EditorGUILayout.GetControlRect(false, 42);
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.14f, 0.2f, 1f));

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.25f, 0.85f, 1f) }
            };

            GUI.Label(rect, "🎮 Pixel Art Generator (3D Küp Dizici)", titleStyle);
            EditorGUILayout.Space(6);
        }

        private void DrawLevelSelector()
        {
            LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
            if (lm != null && lm.Levels != null && lm.Levels.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("🎮 Bölüm Seçici (Levels - Tek Tıkla Sahnede Canlı Geçiş)", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                for (int i = 0; i < lm.Levels.Count; i++)
                {
                    PixelLevelData level = lm.Levels[i];
                    if (level == null) continue;

                    bool isCurrent = m_Target.ActiveLevelData == level;
                    GUI.backgroundColor = isCurrent ? new Color(0.2f, 0.9f, 0.5f) : Color.white;
                    if (GUILayout.Button($"{i + 1}. {level.LevelName}", GUILayout.Height(30)))
                    {
                        Undo.RecordObject(m_Target, "Switch Level");
                        m_Target.LoadLevel(level);
                        m_Target.ApplyShadowsToAllExistingCubes();
                        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                        SceneView.RepaintAll();
                    }
                }
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
                EditorGUILayout.Space(6);
            }
        }

        private void DrawColorPresets()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🎨 Hızlı Renk & Canlılık Ön Ayarları", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("🌟 Canlı & Zengin", GUILayout.Height(28)))
            {
                Undo.RecordObject(m_Target, "Apply Vibrant Preset");
                m_Target.ColorBrightness = 1.25f;
                m_Target.ColorSaturation = 1.3f;
                m_Target.ColorContrast = 1.05f;
                m_Target.EmissionIntensity = 0.4f;
                m_Target.Sampling = SamplingMode.Point;
                m_Target.UpdateExistingCubesLive();
                EditorUtility.SetDirty(m_Target);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("🎨 Saf / Doğal", GUILayout.Height(28)))
            {
                Undo.RecordObject(m_Target, "Apply Pure Preset");
                m_Target.ColorBrightness = 1.0f;
                m_Target.ColorSaturation = 1.0f;
                m_Target.ColorContrast = 1.0f;
                m_Target.EmissionIntensity = 0.0f;
                m_Target.Sampling = SamplingMode.Point;
                m_Target.UpdateExistingCubesLive();
                EditorUtility.SetDirty(m_Target);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("☀️ Ekstra Işıltılı", GUILayout.Height(28)))
            {
                Undo.RecordObject(m_Target, "Apply Glow Preset");
                m_Target.ColorBrightness = 1.45f;
                m_Target.ColorSaturation = 1.35f;
                m_Target.ColorContrast = 1.1f;
                m_Target.EmissionIntensity = 0.65f;
                m_Target.Sampling = SamplingMode.Point;
                m_Target.UpdateExistingCubesLive();
                EditorUtility.SetDirty(m_Target);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private void CheckTextureReadability()
        {
            Texture2D tex = m_Target.SourceTexture;
            if (tex == null) return;

            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("⚠️ Seçili görsel 'Read/Write Enabled' değil. Pikselleri okumak için okunabilir yapılmalıdır.", MessageType.Warning);
                if (GUILayout.Button("🔧 Görseli Otomatik Okunabilir Yap (Fix Read/Write)", GUILayout.Height(28)))
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    Debug.Log($"<color=#00FF00>[PixelArtGenerator]</color> '{tex.name}' görseli okunabilir yapıldı!");
                }
            }
        }

        private void DrawInfoBox()
        {
            EditorGUILayout.Space(8);
            int currentChildCount = m_Target.CubesContainer != null ? m_Target.CubesContainer.childCount : 0;
            Vector2Int res = m_Target.GridResolution;
            int estimatedCubes = (res.x > 0 && res.y > 0) ? (res.x * res.y) : 0;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📊 Durum & Bilgi", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Hedef Çerçeve:", m_Target.TargetFrameRect != null ? $"✅ {m_Target.TargetFrameRect.name}" : "❌ Bulunamadı (MainPlane)");
            EditorGUILayout.LabelField("Hedef Prefab:", m_Target.CubePrefab != null ? $"✅ {m_Target.CubePrefab.name}" : "❌ MainCube atanmadı");
            EditorGUILayout.LabelField("Izgara Boyutu:", res.x > 0 ? $"{res.x} x {res.y} (En fazla ~{estimatedCubes} Küp)" : "Görsel Çözünürlüğü");
            EditorGUILayout.LabelField("Sahnede Aktif Küp:", $"{currentChildCount} adet");
            EditorGUILayout.EndVertical();
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.Space(10);

            // 0. Sahnede Canlı Önizle Butonu (Oyunu başlatmadan Edit Mode'da önizleme)
            GUI.backgroundColor = new Color(0.15f, 0.75f, 1.0f);
            if (GUILayout.Button("👁️ Sahnede Piksel Resmini ve Gölgeleri Canlı Önizle", GUILayout.Height(38)))
            {
                m_Target.GeneratePixelArt();
                m_Target.ApplyShadowsToAllExistingCubes();
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            EditorGUILayout.Space(4);

            // 1. Resmi Oluştur Butonu (Büyük yeşil buton)
            GUI.backgroundColor = new Color(0.2f, 0.88f, 0.45f);
            if (GUILayout.Button("🎨 Resmi Küplerle Yeniden Oluştur (Generate)", GUILayout.Height(40)))
            {
                m_Target.GeneratePixelArt();
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            EditorGUILayout.Space(4);

            // 2. Canlı Renk Güncelle Butonu (Küp silip yaratmadan renkleri yeniler)
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1.0f);
            if (GUILayout.Button("🔄 Mevcut Küplerin Renklerini Canlı Güncelle", GUILayout.Height(32)))
            {
                m_Target.UpdateExistingCubesLive();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(4);

            // 2b. Küplere Fake Shadow Ekle / Güncelle Butonu
            GUI.backgroundColor = new Color(0.7f, 0.5f, 1.0f);
            if (GUILayout.Button("🌑 Küplere Fake Shadow (Gölge) Ekle / Güncelle", GUILayout.Height(32)))
            {
                m_Target.ApplyShadowsToAllExistingCubes();
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            // 3. Küpleri Temizle Butonu
            GUI.backgroundColor = new Color(0.95f, 0.35f, 0.35f);
            if (GUILayout.Button("🧹 Küpleri Temizle (Clear)", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Küpleri Temizle", "Sahnede oluşturulan tüm küpler silinsin mi?", "Evet, Temizle", "Vazgeç"))
                {
                    m_Target.ClearCubes();
                    SceneView.RepaintAll();
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(8);
        }
    }

    [InitializeOnLoad]
    public static class PixelArtAutoSetup
    {
        private const string SessionKey = "PixelArtAutoSetup_RunDone_v2";

        static PixelArtAutoSetup()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;

            SessionState.SetBool(SessionKey, true);
            SetupPixelArtManager(isAuto: true);
        }

        [MenuItem("Tools/PixelGame/🎨 Piksel Resim Yöneticisini Kur veya Seç")]
        public static void SetupManual()
        {
            SetupPixelArtManager(isAuto: false);
        }

        [MenuItem("Tools/PixelGame/🔄 Sahnede Küp Renklerini Canlı Güncelle")]
        public static void UpdateColorsManual()
        {
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null)
            {
                gen.UpdateExistingCubesLive();
                SceneView.RepaintAll();
                Debug.Log("<color=#00FFAA>[PixelGame]</color> Küp renkleri canlı güncellendi!");
            }
        }

        [MenuItem("Tools/PixelGame/🌑 Sahnede Küplere Fake Shadow Ekle veya Güncelle")]
        public static void ApplyShadowsManual()
        {
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null)
            {
                gen.ApplyShadowsToAllExistingCubes();
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        [MenuItem("Tools/PixelGame/🧹 Sahnede Oluşturulan Küpleri Temizle")]
        public static void ClearManual()
        {
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null)
            {
                gen.ClearCubes();
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("<color=#FFAA00>[PixelGame]</color> Küpler temizlendi.");
            }
        }

        public static void SetupPixelArtManager(bool isAuto)
        {
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            bool newlyCreated = false;

            if (gen == null)
            {
                GameObject managerObj = new GameObject("[PixelArtGenerator]");
                Undo.RegisterCreatedObjectUndo(managerObj, "Create [PixelArtGenerator]");
                gen = managerObj.AddComponent<PixelArtGenerator>();
                newlyCreated = true;
            }

            // Prefab bağla
            if (gen.CubePrefab == null)
            {
                string[] cubeGuids = AssetDatabase.FindAssets("MainCube t:Prefab");
                if (cubeGuids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(cubeGuids[0]);
                    gen.CubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }

            // MainPlane çerçevesini bağla
            if (gen.TargetFrameRect == null)
            {
                GameObject mainPlane = GameObject.Find("MainPlane");
                if (mainPlane != null)
                {
                    gen.TargetFrameRect = mainPlane.GetComponent<RectTransform>();
                }
            }

            // Raccoon texture bağla
            if (gen.SourceTexture == null)
            {
                string[] texGuids = AssetDatabase.FindAssets("PixelArt_Raccoon t:Texture2D");
                if (texGuids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(texGuids[0]);
                    gen.SourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            // Etkileşim bileşenini sağla (Tıklayınca partiküllere ayrılarak patlama)
            if (gen.GetComponent<PixelCubeInteraction>() == null)
            {
                gen.gameObject.AddComponent<PixelCubeInteraction>();
            }

            // Eğer yeni oluşturulduysa veya küpleri yoksa otomatik oluştur
            if (newlyCreated || (gen.CubesContainer == null || gen.CubesContainer.childCount == 0))
            {
                gen.GeneratePixelArt();
            }
            else
            {
                // Mevcut küpleri yeni canlı renklerle güncelle
                gen.UpdateExistingCubesLive();
            }

            Selection.activeGameObject = gen.gameObject;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("<color=#00FFAA><b>[PixelGame]</b></color> [PixelArtGenerator] başarıyla kuruldu ve canlı renklerle güncellendi!");

            if (!isAuto)
            {
                EditorUtility.DisplayDialog("Piksel Sanatı Hazır!", 
                    "[PixelArtGenerator] nesnesi seçildi!\n\n" +
                    "Inspector panelindeki Renk Ön Ayarları (Canlı & Zengin, Saf/Doğal, Ekstra Işıltılı) ile renk tonlarını değiştirebilir, " +
                    "'Resmi Küplerle Yeniden Oluştur' butonuyla güncelleyebilirsiniz.", "Harika!");
            }
        }
    }
}
