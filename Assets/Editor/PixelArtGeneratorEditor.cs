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

            // Bu Seviyeye Kaydet Butonu (Inspector'daki canlı ayarları kalıcı yapar)
            DrawSaveToLevelButton();

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool inspectorChanged = EditorGUI.EndChangeCheck();

            // Renk Hazır Ayarları (Presets)
            DrawColorPresets();

            // Texture Okunabilirlik Kontrolü
            CheckTextureReadability();

            // Canlı Bilgi Paneli
            DrawInfoBox();

            // Aksiyon Butonları
            DrawActionButtons();

            if (serializedObject.ApplyModifiedProperties() || inspectorChanged)
            {
                m_Target.UpdateExistingCubesTransforms();
                m_Target.UpdateExistingCubesLive();
                SceneView.RepaintAll();
            }
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
            EditorGUILayout.LabelField("Sahne Koruması:", m_Target.PreserveSceneEdits ? "🔒 Açık (Sildikleriniz & Taşıdıklarınız Play'de Korunur)" : "⚠️ Kapalı");
            EditorGUILayout.LabelField("Hedef Çerçeve:", m_Target.TargetFrameRect != null ? $"✅ {m_Target.TargetFrameRect.name}" : "Serbest 3D Konum (Transform)");
            EditorGUILayout.LabelField("Hedef Prefab:", m_Target.CubePrefab != null ? $"✅ {m_Target.CubePrefab.name}" : "❌ MainCube atanmadı");
            EditorGUILayout.LabelField("Izgara Boyutu:", res.x > 0 ? $"{res.x} x {res.y} (En fazla ~{estimatedCubes} Küp)" : "Görsel Çözünürlüğü");
            EditorGUILayout.LabelField("Sahnede Aktif Küp:", $"{currentChildCount} adet");
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Inspector'da canlı yapılan düzen ayarlarını (spacing, tilt, padding vb.) o an aktif olan
        /// PixelLevelData asset'ine kalıcı olarak yazar. Bu basılmadan yapılan ayarlar sadece sahnedeki
        /// canlı objede kalır ve başka bir seviyeye geçilip geri dönüldüğünde kaybolur.
        /// </summary>
        private void DrawSaveToLevelButton()
        {
            var activeLevelProp = serializedObject.FindProperty("m_ActiveLevelData");
            PixelLevelData activeLevel = activeLevelProp != null ? activeLevelProp.objectReferenceValue as PixelLevelData : null;

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(activeLevel == null))
            {
                GUI.backgroundColor = new Color(1.0f, 0.75f, 0.15f);
                string label = activeLevel != null
                    ? $"💾 Bu Seviyeye Kaydet ({activeLevel.LevelName})"
                    : "💾 Bu Seviyeye Kaydet (Aktif seviye yok)";
                if (GUILayout.Button(label, GUILayout.Height(34)))
                {
                    m_Target.SaveToActiveLevel();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.HelpBox(
                "Yukarıdaki ayarları (boşluk, eğim, derinlik, kayma vb.) denedikten sonra beğendiysen bu " +
                "seviyeye kalıcı olarak kaydetmek için bas — yoksa başka bir seviyeye geçip geri döndüğünde " +
                "değişiklikler kaybolur.",
                MessageType.Info);
            EditorGUILayout.Space(4);
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

            // 2a. Canlı Spacing / Boyut Güncelle Butonu
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.9f);
            if (GUILayout.Button("📐 Küp Boyut ve Boşluklarını Canlı Güncelle (Update Spacing)", GUILayout.Height(32)))
            {
                m_Target.UpdateExistingCubesTransforms();
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
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

            EditorGUILayout.Space(4);

            // 2c. Gizli / Patlatılmış Küpleri Sahnede Geri Aç (Restore All) Butonu
            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.55f);
            if (GUILayout.Button("🔄 Gizli / Patlamış Küpleri Sahnede Geri Aç (Restore All)", GUILayout.Height(32)))
            {
                m_Target.RestoreAllPoppedCubes();
                m_Target.EnsureWorldFramePreview(true);
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            EditorGUILayout.Space(4);

            // 2d. 3D Sahne Çerçeve Önizlemesini Yenile Butonu
            GUI.backgroundColor = new Color(0.3f, 0.75f, 1f);
            if (GUILayout.Button("🎯 Çerçeveyi & Panoyu Küplere Kilitle (Snap Frame to Art)", GUILayout.Height(32)))
            {
                m_Target.EnsureWorldFramePreview(true);
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            EditorGUILayout.Space(4);

            // 2e. Küpleri Jeneratör Merkezine Hizala Butonu
            GUI.backgroundColor = new Color(0.9f, 0.75f, 0.2f);
            if (GUILayout.Button("📍 Küpleri Merkeze Hizala (Snap Art to Generator)", GUILayout.Height(30)))
            {
                m_Target.CenterPixelArtToOrigin();
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🎮 Sahne Konumu & Düzen Kontrolü", EditorStyles.boldLabel);

            // Sahneyi Grupla
            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.95f);
            if (GUILayout.Button("🔗 Tüm Sahneyi Birlikte Hareket Edecek Şekilde Grupla", GUILayout.Height(30)))
            {
                m_Target.OrganizeSceneHierarchy();
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.9f, 0.7f, 0.4f);
            if (GUILayout.Button("⬇️ Aşağı Kaydır (-0.5m)", GUILayout.Height(28)))
            {
                m_Target.ShiftEntireScene(new Vector3(0f, -0.5f, 0f));
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            if (GUILayout.Button("⬆️ Yukarı Kaydır (+0.5m)", GUILayout.Height(28)))
            {
                m_Target.ShiftEntireScene(new Vector3(0f, 0.5f, 0f));
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
            if (GUILayout.Button("📍 İdeal Oyun Konumuna Hizala (Y = 1.6m)", GUILayout.Height(30)))
            {
                m_Target.SetSceneCenter(new Vector3(0f, 1.60f, m_Target.TargetZ));
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            EditorGUILayout.Space(4);

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

        private void OnSceneGUI()
        {
            if (m_Target == null) return;
            Camera cam = m_Target.GetActiveCamera();
            if (cam == null) return;

            if (m_Target.CalculateTargetWorldBounds(cam, out Vector3 center, out float width, out float height))
            {
                float fullW = width / Mathf.Max(0.01f, 1f - m_Target.InnerPadding * 2f);
                float fullH = height / Mathf.Max(0.01f, 1f - m_Target.InnerPadding * 2f);

                GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = new Color(0.2f, 0.9f, 1f) },
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter
                };

                Vector3 labelPos = new Vector3(center.x, center.y + fullH * 0.5f + 0.35f, m_Target.TargetZ);
                Handles.Label(labelPos, $"🖼️ PANO ÇERÇEVESİ ({fullW:F2}m x {fullH:F2}m)", labelStyle);
            }
        }
    }

    [InitializeOnLoad]
    public static class PixelArtAutoSetup
    {
        private const string SessionKey = "PixelArtAutoSetup_RunDone_v2";

        static PixelArtAutoSetup()
        {
            // Otomatik tetikleme kapatıldı: proje her açıldığında sahneyi elle onay
            // almadan değiştirip kaydediyordu. Gerekirse elle çalıştırılır.
            // EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;

            SessionState.SetBool(SessionKey, true);
            PixelArtGenerator existingGen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (existingGen != null && existingGen.PreserveSceneEdits)
                return;

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

            // Eğer yeni oluşturulduysa veya küpleri yoksa ve sahne koruması kapalıysa otomatik oluştur
            if (newlyCreated || (gen.CubesContainer == null || gen.CubesContainer.childCount == 0))
            {
                if (!isAuto || !gen.PreserveSceneEdits)
                {
                    gen.GeneratePixelArt();
                }
            }
            else
            {
                // Mevcut küpleri sadece sahne koruması kapalıysa veya manuel çağrıldıysa güncelle
                if (!isAuto || !gen.PreserveSceneEdits)
                {
                    gen.UpdateExistingCubesLive();
                }
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
