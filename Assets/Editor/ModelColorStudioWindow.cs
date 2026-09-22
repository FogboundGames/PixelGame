using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Vagon ve robot modellerinin gövde renklerini, yüz/göz detaylarını, materyallerini
    /// ve Toony Colors Pro 2 (TCP2) toon shader ışık/gölge/plastik efektlerini
    /// anlık 3B interaktif önizleme ile kolayca ayarlama ve kalıcı kaydetme aracı.
    /// </summary>
    public class ModelColorStudioWindow : EditorWindow
    {
        private const string CyberCubePath = "Assets/Prefabs/CyberCubeWagon.prefab";
        private const string BlueBotWithNeckPath = "Assets/Prefabs/BlueBotWithNeckWagon.prefab";
        private const string BlueBotPath = "Assets/Prefabs/BlueBotWagon.prefab";
        private const string VacuumCannonPath = "Assets/Prefabs/VacuumCannon.prefab";

        private enum ModelTab
        {
            CyberCube,
            BlueBotWithNeck,
            BlueBotClassic,
            VacuumCannon,
            CustomSelection
        }

        private ModelTab m_CurrentTab = ModelTab.CyberCube;
        private GameObject m_CustomTarget;
        private PreviewRenderUtility m_PreviewUtility;
        private Vector2 m_PreviewDir = new Vector2(145f, -18f);
        private float m_PreviewZoomFactor = 1.0f;
        private Vector2 m_ScrollPos;

        // Model Render Önbelleği
        private GameObject m_PreviewInstance;
        private GameObject m_LastTargetPrefab;
        private Bounds m_ModelBounds;

        // Materyal Önizleme Slotları
        private Material m_PreviewMaterial;       // Slot 0: Gövde (Dinamik Renk + TCP2)
        private Material m_DarkPreviewMaterial;   // Slot 1: Göz, Pupil, Ağız, Eklemler (Koyu Grafit)
        private Material m_WhitePreviewMaterial;  // Slot 2: Göz Parlaması, Diş, Beyaz Detaylar
        private Material m_AccentPreviewMaterial; // Slot 3+: Metalik halka / eklem

        // Gövde ve Karakter Detay Renkleri
        private Color m_BaseColor = new Color32(255, 210, 20, 255);
        private Color m_DarkColor = new Color32(22, 24, 32, 255);
        private Color m_WhiteColor = Color.white;

        // Toony Colors Pro 2 Cel-Shading Ayarları (Açık, parlak hypercasual tonları)
        private Color m_HColor = Color.white;
        private Color m_SColor = new Color(0.84f, 0.82f, 0.88f, 1f);
        private float m_RampThreshold = 0.383f;
        private float m_RampSmoothing = 0.908f;

        // Plastik Cila & Speküler Yansıma
        private bool m_StylizedPlastic = true;
        private float m_TopLight = 0.25f;
        private float m_BevelAO = 0.45f;
        private float m_BevelWidth = 0.05f;
        private float m_BevelIntensity = 0.80f;
        private Color m_SpecularColor = Color.white;
        private float m_Roughness = 0.18f;

        [MenuItem("Tools/PixelGame/🎨 Model & Toon Renk Stüdyosu (TCP2 Preview)", priority = 1)]
        [MenuItem("Window/PixelGame/Model & Toon Renk Stüdyosu")]
        public static void OpenWindow()
        {
            var window = GetWindow<ModelColorStudioWindow>("Model & Toon Renk Stüdyosu");
            window.minSize = new Vector2(840, 660);
            window.Show();
        }

        private void OnEnable()
        {
            InitPreview();
            LoadSettingsFromCurrentModel();
        }

        private void OnDisable()
        {
            CleanupPreview();
        }

        private void InitPreview()
        {
            if (m_PreviewUtility == null)
            {
                m_PreviewUtility = new PreviewRenderUtility();
                m_PreviewUtility.cameraFieldOfView = 32f;
                m_PreviewUtility.camera.nearClipPlane = 0.05f;
                m_PreviewUtility.camera.farClipPlane = 100f;
                m_PreviewUtility.camera.clearFlags = CameraClearFlags.Color;
                m_PreviewUtility.camera.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);
                m_PreviewUtility.ambientColor = new Color(0.42f, 0.45f, 0.52f, 1f);
            }
        }

        private void CleanupPreview()
        {
            if (m_PreviewInstance != null)
            {
                DestroyImmediate(m_PreviewInstance);
                m_PreviewInstance = null;
            }
            if (m_PreviewMaterial != null) DestroyImmediate(m_PreviewMaterial);
            if (m_DarkPreviewMaterial != null) DestroyImmediate(m_DarkPreviewMaterial);
            if (m_WhitePreviewMaterial != null) DestroyImmediate(m_WhitePreviewMaterial);
            if (m_AccentPreviewMaterial != null) DestroyImmediate(m_AccentPreviewMaterial);
            m_PreviewMaterial = null;
            m_DarkPreviewMaterial = null;
            m_WhitePreviewMaterial = null;
            m_AccentPreviewMaterial = null;

            if (m_PreviewUtility != null)
            {
                m_PreviewUtility.Cleanup();
                m_PreviewUtility = null;
            }
        }

        private GameObject GetTargetPrefab()
        {
            switch (m_CurrentTab)
            {
                case ModelTab.CyberCube:
                    return AssetDatabase.LoadAssetAtPath<GameObject>(CyberCubePath);
                case ModelTab.BlueBotWithNeck:
                    return AssetDatabase.LoadAssetAtPath<GameObject>(BlueBotWithNeckPath);
                case ModelTab.BlueBotClassic:
                    return AssetDatabase.LoadAssetAtPath<GameObject>(BlueBotPath);
                case ModelTab.VacuumCannon:
                    return AssetDatabase.LoadAssetAtPath<GameObject>(VacuumCannonPath);
                case ModelTab.CustomSelection:
                    return m_CustomTarget != null ? m_CustomTarget : (Selection.activeGameObject != null ? Selection.activeGameObject : AssetDatabase.LoadAssetAtPath<GameObject>(CyberCubePath));
                default:
                    return AssetDatabase.LoadAssetAtPath<GameObject>(CyberCubePath);
            }
        }

        private void LoadSettingsFromCurrentModel()
        {
            GameObject prefab = GetTargetPrefab();
            if (prefab == null) return;

            TruckPaint paint = prefab.GetComponent<TruckPaint>();
            if (paint != null)
            {
                m_BaseColor = paint.GetColor(TruckPart.Cabin);
            }

            RecreatePreviewInstance(prefab);
        }

        private void RecreatePreviewInstance(GameObject targetPrefab)
        {
            if (m_PreviewInstance != null)
            {
                DestroyImmediate(m_PreviewInstance);
                m_PreviewInstance = null;
            }

            if (targetPrefab == null) return;

            if (m_PreviewUtility == null)
            {
                InitPreview();
            }

            m_PreviewInstance = Instantiate(targetPrefab);
            m_PreviewInstance.name = "StudioPreviewModel";
            m_PreviewInstance.hideFlags = HideFlags.HideAndDontSave;
            m_LastTargetPrefab = targetPrefab;

            if (m_PreviewUtility != null)
            {
                m_PreviewUtility.AddSingleGO(m_PreviewInstance);
            }
            else
            {
                m_PreviewInstance.transform.position = new Vector3(99999f, 99999f, 99999f);
            }

            // Model ve tüm alt nesneleri aktif et
            foreach (Transform t in m_PreviewInstance.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.SetActive(true);
            }

            // Rozet Canvas'ını önizlemede gizle
            Transform canvasTrans = m_PreviewInstance.transform.Find("CapacityBadgeCanvas");
            if (canvasTrans != null) canvasTrans.gameObject.SetActive(false);

            // Tüm renderer'ları aç
            foreach (Renderer r in m_PreviewInstance.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
            }

            CalculateModelBounds();
            UpdatePreviewMaterialProperties();
        }

        private void CalculateModelBounds()
        {
            if (m_PreviewInstance == null)
            {
                m_ModelBounds = new Bounds(Vector3.zero, Vector3.one);
                return;
            }

            Bounds b = new Bounds();
            bool found = false;

            foreach (Renderer r in m_PreviewInstance.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled) continue;
                if (r.name.StartsWith("CapacityBadge") || r.name.StartsWith("Badge")) continue;

                if (!found)
                {
                    b = r.bounds;
                    found = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            if (!found || b.size.magnitude < 0.01f)
            {
                b = new Bounds(m_PreviewInstance.transform.position + Vector3.up * 0.5f, Vector3.one * 1.5f);
            }

            m_ModelBounds = b;
        }

        private void UpdatePreviewMaterialProperties()
        {
            Shader cartoonShader = CartoonShader.Get();

            // 1. Gövde Materyali (Slot 0)
            if (m_PreviewMaterial == null)
            {
                m_PreviewMaterial = new Material(cartoonShader) { name = "Preview_Body_Mat", hideFlags = HideFlags.HideAndDontSave };
            }
            m_PreviewMaterial.SetColor("_BaseColor", m_BaseColor);
            m_PreviewMaterial.SetColor("_HColor", m_HColor);
            m_PreviewMaterial.SetColor("_SColor", m_SColor);
            m_PreviewMaterial.SetFloat("_RampThreshold", m_RampThreshold);
            m_PreviewMaterial.SetFloat("_RampSmoothing", m_RampSmoothing);
            if (m_StylizedPlastic)
            {
                m_PreviewMaterial.SetFloat("_StylizedPlasticOn", 1f);
                m_PreviewMaterial.SetFloat("_PlasticHighlightIntensity", 2.85f);
                m_PreviewMaterial.SetFloat("_PlasticHighlightSize", 0.26f);
                m_PreviewMaterial.SetFloat("_PlasticTopLight", m_TopLight);
                m_PreviewMaterial.SetFloat("_PlasticBevelAO", m_BevelAO);
                m_PreviewMaterial.SetFloat("_ProceduralBevelWidth", m_BevelWidth);
                m_PreviewMaterial.SetFloat("_ProceduralBevelIntensity", m_BevelIntensity);
                m_PreviewMaterial.SetFloat("_PillowRoundness", 0.50f);
                m_PreviewMaterial.SetFloat("_PlasticAngleX", -0.45f);
                m_PreviewMaterial.SetColor("_PlasticHighlightColor", m_SpecularColor);
                m_PreviewMaterial.SetColor("_SpecularColor", m_SpecularColor);
                m_PreviewMaterial.SetFloat("_SpecularRoughnessPBR", m_Roughness);
                m_PreviewMaterial.SetFloat("_Smoothness", 0.85f);
            }
            else
            {
                m_PreviewMaterial.SetFloat("_StylizedPlasticOn", 0f);
            }

            // 2. Koyu Göz / Pupil / Ağız Materyali (Slot 1)
            if (m_DarkPreviewMaterial == null)
            {
                m_DarkPreviewMaterial = new Material(cartoonShader) { name = "Preview_Dark_Mat", hideFlags = HideFlags.HideAndDontSave };
            }
            m_DarkPreviewMaterial.SetColor("_BaseColor", m_DarkColor);
            m_DarkPreviewMaterial.SetColor("_HColor", Color.white);
            m_DarkPreviewMaterial.SetColor("_SColor", new Color(0.12f, 0.12f, 0.16f, 1f));
            m_DarkPreviewMaterial.SetFloat("_RampThreshold", 0.5f);
            m_DarkPreviewMaterial.SetFloat("_RampSmoothing", 0.15f);
            if (m_DarkPreviewMaterial.HasProperty("_Smoothness")) m_DarkPreviewMaterial.SetFloat("_Smoothness", 0.85f);
            if (m_DarkPreviewMaterial.HasProperty("_SpecularRoughnessPBR")) m_DarkPreviewMaterial.SetFloat("_SpecularRoughnessPBR", 0.15f);

            // 3. Parlak Beyaz Göz Parlaması Materyali (Slot 2)
            if (m_WhitePreviewMaterial == null)
            {
                m_WhitePreviewMaterial = new Material(cartoonShader) { name = "Preview_White_Mat", hideFlags = HideFlags.HideAndDontSave };
            }
            m_WhitePreviewMaterial.SetColor("_BaseColor", m_WhiteColor);
            m_WhitePreviewMaterial.SetColor("_HColor", Color.white);
            m_WhitePreviewMaterial.SetColor("_SColor", new Color(0.85f, 0.88f, 0.95f, 1f));
            m_WhitePreviewMaterial.SetFloat("_RampThreshold", 0.3f);
            m_WhitePreviewMaterial.SetFloat("_RampSmoothing", 0.1f);
            if (m_WhitePreviewMaterial.HasProperty("_Smoothness")) m_WhitePreviewMaterial.SetFloat("_Smoothness", 0.95f);
            if (m_WhitePreviewMaterial.HasProperty("_SpecularRoughnessPBR")) m_WhitePreviewMaterial.SetFloat("_SpecularRoughnessPBR", 0.05f);

            // 4. Metalik Eklem / Rim Materyali (Slot 3+)
            if (m_AccentPreviewMaterial == null)
            {
                m_AccentPreviewMaterial = new Material(cartoonShader) { name = "Preview_Accent_Mat", hideFlags = HideFlags.HideAndDontSave };
            }
            m_AccentPreviewMaterial.SetColor("_BaseColor", new Color(0.78f, 0.80f, 0.85f, 1f));
            m_AccentPreviewMaterial.SetColor("_HColor", Color.white);
            m_AccentPreviewMaterial.SetColor("_SColor", new Color(0.45f, 0.48f, 0.55f, 1f));
        }

        private void OnGUI()
        {
            DrawTopHeader();

            EditorGUILayout.BeginHorizontal();

            // SOL PANEL: 3B İnteraktif Model Önizlemesi (Orbit, Zoom, Multi-Submesh)
            DrawLeftPreviewPanel();

            // SAĞ PANEL: TCP2, Yüz Detayları ve Renk Kontrolleri
            DrawRightControlsPanel();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTopHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("🤖", GUILayout.Width(30));
            EditorGUILayout.BeginVertical();
            GUILayout.Label("MODEL & TOON RENK STÜDYOSU (TCP2 Multi-Tone)", EditorStyles.boldLabel);
            GUILayout.Label("Robot ve vagon karakterlerinin gövde, göz ve yüz detay renklerini Toony Colors Pro 2 ile canlı önizleyin ve özelleştirin.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            m_CurrentTab = (ModelTab)GUILayout.Toolbar((int)m_CurrentTab, new string[]
            {
                "🧊 CyberCube (Aktif)",
                "🤖 Boyunlu Robot Bot",
                "🤖 Klasik Robot Bot",
                "🔫 Vakum Topu",
                "🎯 Özel / Seçili Model"
            }, GUILayout.Height(28));

            if (EditorGUI.EndChangeCheck())
            {
                LoadSettingsFromCurrentModel();
            }
            EditorGUILayout.EndHorizontal();

            if (m_CurrentTab == ModelTab.CustomSelection)
            {
                EditorGUILayout.Space(2);
                EditorGUI.BeginChangeCheck();
                m_CustomTarget = (GameObject)EditorGUILayout.ObjectField("Özel Model / Prefab:", m_CustomTarget, typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck())
                {
                    LoadSettingsFromCurrentModel();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLeftPreviewPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(390));
            GUILayout.Label("🎥 3B Canlı Karakter Önizlemesi (Fareyle Döndür / Yakınlaş)", EditorStyles.boldLabel);

            Rect previewRect = GUILayoutUtility.GetRect(370, 440, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // Fare Kontrolleri (Orbit & Zoom)
            Event evt = Event.current;
            if (previewRect.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.MouseDrag && evt.button == 0)
                {
                    m_PreviewDir.x -= evt.delta.x * 0.85f;
                    m_PreviewDir.y += evt.delta.y * 0.85f;
                    m_PreviewDir.y = Mathf.Clamp(m_PreviewDir.y, -85f, 85f);
                    Repaint();
                    evt.Use();
                }
                else if (evt.type == EventType.ScrollWheel)
                {
                    m_PreviewZoomFactor += evt.delta.y * 0.08f;
                    m_PreviewZoomFactor = Mathf.Clamp(m_PreviewZoomFactor, 0.4f, 3.5f);
                    Repaint();
                    evt.Use();
                }
            }

            if (m_PreviewUtility != null)
            {
                if (m_PreviewInstance == null)
                {
                    RecreatePreviewInstance(GetTargetPrefab());
                }

                m_PreviewUtility.BeginPreview(previewRect, GUIStyle.none);

                // Kamera Odaklanması
                Vector3 targetCenter = m_ModelBounds.center;
                float radius = Mathf.Max(m_ModelBounds.size.x, m_ModelBounds.size.y, m_ModelBounds.size.z, 0.6f);
                float distance = radius * 2.3f * m_PreviewZoomFactor;

                Quaternion camRot = Quaternion.Euler(-m_PreviewDir.y, -m_PreviewDir.x, 0f);
                Vector3 camPos = targetCenter + camRot * (Vector3.forward * -distance);

                m_PreviewUtility.camera.transform.position = camPos;
                m_PreviewUtility.camera.transform.rotation = camRot;
                m_PreviewUtility.camera.transform.LookAt(targetCenter);

                // Işıklandırma
                m_PreviewUtility.lights[0].enabled = true;
                m_PreviewUtility.lights[0].intensity = 1.45f;
                m_PreviewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 35f, 0f);
                m_PreviewUtility.lights[0].color = new Color(1f, 0.98f, 0.92f, 1f);

                m_PreviewUtility.lights[1].enabled = true;
                m_PreviewUtility.lights[1].intensity = 0.75f;
                m_PreviewUtility.lights[1].transform.rotation = Quaternion.Euler(-25f, -145f, 0f);
                m_PreviewUtility.lights[1].color = new Color(0.7f, 0.8f, 1f, 1f);

                // Çoklu Submesh Model Çizimi
                if (m_PreviewInstance != null)
                {
                    Renderer[] rends = m_PreviewInstance.GetComponentsInChildren<Renderer>(true);
                    for (int i = 0; i < rends.Length; i++)
                    {
                        Renderer r = rends[i];
                        if (r == null) continue;
                        if (r.name.StartsWith("CapacityBadge") || r.name.StartsWith("Badge")) continue;

                        Mesh mesh = null;
                        if (r is MeshRenderer mr)
                        {
                            MeshFilter mf = mr.GetComponent<MeshFilter>();
                            if (mf != null) mesh = mf.sharedMesh;
                        }
                        else if (r is SkinnedMeshRenderer smr)
                        {
                            mesh = smr.sharedMesh;
                        }

                        if (mesh == null) continue;

                        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                        {
                            Material matToDraw = m_PreviewMaterial;
                            if (mesh.subMeshCount >= 3)
                            {
                                if (submesh == 0) matToDraw = m_PreviewMaterial;
                                else if (submesh == 1) matToDraw = m_DarkPreviewMaterial;
                                else if (submesh == 2) matToDraw = m_WhitePreviewMaterial;
                                else matToDraw = m_AccentPreviewMaterial;
                            }
                            else if (mesh.subMeshCount == 2)
                            {
                                if (submesh == 0) matToDraw = m_PreviewMaterial;
                                else matToDraw = m_DarkPreviewMaterial;
                            }
                            else
                            {
                                string n = r.name;
                                if (n.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    n.IndexOf("Pupil", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    n.IndexOf("Brow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    n.IndexOf("Snout", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    n.IndexOf("Dark", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    n.IndexOf("Mouth", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    matToDraw = m_DarkPreviewMaterial;
                                }
                                else if (n.IndexOf("Glint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         n.IndexOf("White", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         n.IndexOf("Highlight", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         n.IndexOf("Teeth", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    matToDraw = m_WhitePreviewMaterial;
                                }
                                else
                                {
                                    matToDraw = m_PreviewMaterial;
                                }
                            }

                            if (matToDraw != null)
                            {
                                m_PreviewUtility.DrawMesh(mesh, r.transform.localToWorldMatrix, matToDraw, submesh);
                            }
                        }
                    }
                }

                m_PreviewUtility.camera.Render();
                Texture renderedTex = m_PreviewUtility.EndPreview();
                GUI.DrawTexture(previewRect, renderedTex, ScaleMode.StretchToFill, false);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Açıyı Sıfırla", EditorStyles.miniButton))
            {
                m_PreviewDir = new Vector2(145f, -18f);
                m_PreviewZoomFactor = 1.0f;
                CalculateModelBounds();
            }
            if (GUILayout.Button("🔍 Yakınlaştır", EditorStyles.miniButton)) m_PreviewZoomFactor = 0.65f;
            if (GUILayout.Button("🔎 Uzaklaştır", EditorStyles.miniButton)) m_PreviewZoomFactor = 1.5f;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawRightControlsPanel()
        {
            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ==========================================
            // 1. ANA GÖVDE RENGİ & HAZIR PALETLER
            // ==========================================
            GUILayout.Label("🎨 1. Ana Gövde Rengi & Hazır Paletler", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            m_BaseColor = EditorGUILayout.ColorField("Model Ana Rengi (_BaseColor):", m_BaseColor);

            EditorGUILayout.Space(2);
            GUILayout.Label("Hızlı Gövde Renkleri:", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (DrawColorButton("⭐ Sarı", new Color32(255, 218, 16, 255))) m_BaseColor = new Color32(255, 218, 16, 255);
            if (DrawColorButton("🔵 Mavi", new Color32(40, 140, 245, 255))) m_BaseColor = new Color32(40, 140, 245, 255);
            if (DrawColorButton("🔴 Kırmızı", new Color32(240, 48, 48, 255))) m_BaseColor = new Color32(240, 48, 48, 255);
            if (DrawColorButton("🟢 Yeşil", new Color32(50, 205, 80, 255))) m_BaseColor = new Color32(50, 205, 80, 255);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (DrawColorButton("🟣 Mor", new Color32(165, 75, 235, 255))) m_BaseColor = new Color32(165, 75, 235, 255);
            if (DrawColorButton("🟠 Turuncu", new Color32(255, 130, 25, 255))) m_BaseColor = new Color32(255, 130, 25, 255);
            if (DrawColorButton("⚪ Beyaz", new Color32(245, 245, 250, 255))) m_BaseColor = new Color32(245, 245, 250, 255);
            if (DrawColorButton("🖤 Mat Siyah", new Color32(34, 36, 42, 255))) m_BaseColor = new Color32(34, 36, 42, 255);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // ==========================================
            // 2. GÖZ, YÜZ VE DETAY RENKLERİ
            // ==========================================
            GUILayout.Label("👀 2. Gözler, Yüz & Detay Renkleri (Multi-Tone)", EditorStyles.boldLabel);

            m_DarkColor = EditorGUILayout.ColorField("Göz / Ağız / Pupil Rengi (Mat_Dark):", m_DarkColor);
            EditorGUILayout.BeginHorizontal();
            if (DrawColorButton("🖤 Grafit Siyah", new Color32(22, 24, 32, 255))) m_DarkColor = new Color32(22, 24, 32, 255);
            if (DrawColorButton("🫐 Koyu Safir", new Color32(18, 30, 60, 255))) m_DarkColor = new Color32(18, 30, 60, 255);
            if (DrawColorButton("🍫 Koyu Çikolata", new Color32(48, 28, 18, 255))) m_DarkColor = new Color32(48, 28, 18, 255);
            if (DrawColorButton("🩶 Antrasit", new Color32(45, 48, 56, 255))) m_DarkColor = new Color32(45, 48, 56, 255);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            m_WhiteColor = EditorGUILayout.ColorField("Göz Parlaması / Diş (Mat_White):", m_WhiteColor);

            EditorGUILayout.Space(10);

            // ==========================================
            // 3. TOONY COLORS PRO 2 CEL-SHADING AYARLARI
            // ==========================================
            GUILayout.Label("☀️ 3. Toony Colors Pro Cel-Shading Ayarları", EditorStyles.boldLabel);

            m_HColor = EditorGUILayout.ColorField("Aydınlık Tonu (_HColor):", m_HColor);
            m_SColor = EditorGUILayout.ColorField("Gölge Tonu (_SColor):", m_SColor);
            m_RampThreshold = EditorGUILayout.Slider("Gölge Eşiği (_RampThreshold):", m_RampThreshold, 0.05f, 0.95f);
            m_RampSmoothing = EditorGUILayout.Slider("Gölge Yumuşaklığı (_RampSmoothing):", m_RampSmoothing, 0.001f, 0.8f);

            EditorGUILayout.Space(10);

            // ==========================================
            // 4. 3B PLASTİK OYUNCAK & PARLAMA (STYLIZED PLASTIC)
            // ==========================================
            GUILayout.Label("✨ 4. Parlak Plastik Efekti (Stylized Plastic & Specular)", EditorStyles.boldLabel);

            m_StylizedPlastic = EditorGUILayout.Toggle("Plastik Efektini Aç (_StylizedPlasticOn):", m_StylizedPlastic);

            if (m_StylizedPlastic)
            {
                EditorGUI.indentLevel++;
                m_TopLight = EditorGUILayout.Slider("Üst Işık Artışı (_PlasticTopLight):", m_TopLight, 0f, 1f);
                m_BevelAO = EditorGUILayout.Slider("Köşe Kararması (_PlasticBevelAO):", m_BevelAO, 0f, 1f);
                m_BevelWidth = EditorGUILayout.Slider("Köşe Genişliği (_ProceduralBevelWidth):", m_BevelWidth, 0.005f, 0.15f);
                m_BevelIntensity = EditorGUILayout.Slider("Köşe Parlama Gücü (_ProceduralBevelIntensity):", m_BevelIntensity, 0f, 2f);
                m_SpecularColor = EditorGUILayout.ColorField("Parlama Rengi (_PlasticHighlightColor):", m_SpecularColor);
                m_Roughness = EditorGUILayout.Slider("Pürüzsüzlük / Parlaklık:", m_Roughness, 0.05f, 0.95f);
                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck())
            {
                UpdatePreviewMaterialProperties();
            }

            EditorGUILayout.Space(16);

            // ==========================================
            // 5. KAYDETME & UYGULAMA BUTONLARI
            // ==========================================
            GUILayout.Label("💾 5. Değişiklikleri Kaydet & Uygula", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.45f, 1f);
            if (GUILayout.Button("💾 Prefab & Materyallere Kalıcı Kaydet", GUILayout.Height(36)))
            {
                SaveToPrefabAndMaterial();
            }

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f, 1f);
            if (GUILayout.Button("🔄 Sahnedeki Tüm Vagonlara Canlı Uygula", GUILayout.Height(36)))
            {
                ApplyToSceneInstances();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (GUILayout.Button("↩️ Standart Varsayılan Ayarlara Sıfırla", EditorStyles.miniButton))
            {
                ResetToDefaults();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private bool DrawColorButton(string label, Color color)
        {
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = color;
            bool clicked = GUILayout.Button(label, GUILayout.Height(24));
            GUI.backgroundColor = oldBg;
            return clicked;
        }

        private void ResetToDefaults()
        {
            m_BaseColor = new Color32(255, 218, 16, 255);
            m_DarkColor = new Color32(22, 24, 32, 255);
            m_WhiteColor = Color.white;
            m_HColor = Color.white;
            m_SColor = new Color(0.643f, 0.655f, 0.714f, 1f);
            m_RampThreshold = 0.5f;
            m_RampSmoothing = 0.2f;
            m_StylizedPlastic = true;
            m_TopLight = 0.25f;
            m_BevelAO = 0.40f;
            m_BevelWidth = 0.05f;
            m_BevelIntensity = 0.80f;
            m_SpecularColor = Color.white;
            m_Roughness = 0.30f;

            UpdatePreviewMaterialProperties();
        }

        private void SaveToPrefabAndMaterial()
        {
            Shader cartoonShader = CartoonShader.Get();

            // 1. Mat_Blue.mat, Mat_Dark.mat, Mat_White.mat dosyalarını güncelle
            UpdateMaterialAsset("Assets/Models/Materials/Mat_Blue.mat", cartoonShader, m_BaseColor, m_HColor, m_SColor, m_RampThreshold, m_RampSmoothing, true);
            UpdateMaterialAsset("Assets/Models/Materials/Mat_Dark.mat", cartoonShader, m_DarkColor, Color.white, new Color(0.12f, 0.12f, 0.16f, 1f), 0.5f, 0.15f, false);
            UpdateMaterialAsset("Assets/Models/Materials/Mat_White.mat", cartoonShader, m_WhiteColor, Color.white, new Color(0.85f, 0.88f, 0.95f, 1f), 0.3f, 0.1f, false);

            // 2. Prefab'ı güncelle
            GameObject targetPrefab = GetTargetPrefab();
            if (targetPrefab == null) return;

            string prefabPath = AssetDatabase.GetAssetPath(targetPrefab);
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return;

            try
            {
                TruckPaint paint = root.GetComponent<TruckPaint>();
                if (paint != null)
                {
                    paint.SetBodyColor(m_BaseColor);
                    paint.Apply();
                }

                Material matBlue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Materials/Mat_Blue.mat");
                Material matDark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Materials/Mat_Dark.mat");
                Material matWhite = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Materials/Mat_White.mat");

                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    if (r.name.StartsWith("CapacityBadge") || r.name.StartsWith("Badge")) continue;

                    Mesh mesh = null;
                    if (r is MeshRenderer mr)
                    {
                        MeshFilter mf = mr.GetComponent<MeshFilter>();
                        if (mf != null) mesh = mf.sharedMesh;
                    }
                    else if (r is SkinnedMeshRenderer smr)
                    {
                        mesh = smr.sharedMesh;
                    }

                    int subCount = (mesh != null) ? mesh.subMeshCount : r.sharedMaterials.Length;
                    if (subCount >= 3)
                    {
                        r.sharedMaterials = new Material[] { matBlue, matDark, matWhite };
                    }
                    else if (subCount == 2)
                    {
                        r.sharedMaterials = new Material[] { matBlue, matDark };
                    }
                    else
                    {
                        r.sharedMaterial = matBlue;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=#00FFAA><b>[ModelColorStudio]</b></color> Renk, Toon ve Multi-Tone ayarları '{targetPrefab.name}' prefabına ve materyallerine başarıyla kaydedildi!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private void UpdateMaterialAsset(string path, Shader shader, Color baseColor, Color hColor, Color sColor, float rampThreshold, float rampSmoothing, bool isPlastic)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_HColor", hColor);
            mat.SetColor("_SColor", sColor);
            mat.SetFloat("_RampThreshold", rampThreshold);
            mat.SetFloat("_RampSmoothing", rampSmoothing);

            if (isPlastic && m_StylizedPlastic)
            {
                mat.SetFloat("_StylizedPlasticOn", 1f);
                mat.SetFloat("_PlasticHighlightIntensity", 2.85f);
                mat.SetFloat("_PlasticHighlightSize", 0.26f);
                mat.SetFloat("_PlasticTopLight", m_TopLight);
                mat.SetFloat("_PlasticBevelAO", m_BevelAO);
                mat.SetFloat("_ProceduralBevelWidth", m_BevelWidth);
                mat.SetFloat("_ProceduralBevelIntensity", m_BevelIntensity);
                mat.SetFloat("_PillowRoundness", 0.50f);
                mat.SetFloat("_PlasticAngleX", -0.45f);
                mat.SetColor("_PlasticHighlightColor", m_SpecularColor);
                mat.SetColor("_SpecularColor", m_SpecularColor);
                mat.SetFloat("_SpecularRoughnessPBR", m_Roughness);
                mat.SetFloat("_Smoothness", 0.85f);
            }
            else
            {
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
                if (mat.HasProperty("_SpecularRoughnessPBR")) mat.SetFloat("_SpecularRoughnessPBR", 0.15f);
            }

            EditorUtility.SetDirty(mat);
        }

        private void ApplyToSceneInstances()
        {
            foreach (TruckPaint paint in FindObjectsByType<TruckPaint>(FindObjectsSortMode.None))
            {
                if (paint == null) continue;
                paint.SetBodyColor(m_BaseColor);
                paint.Apply();
                EditorUtility.SetDirty(paint);
            }

            foreach (WagonCapacityBadge badge in FindObjectsByType<WagonCapacityBadge>(FindObjectsSortMode.None))
            {
                if (badge == null) continue;
                badge.ApplyStyle();
                badge.UpdatePlacement();
                EditorUtility.SetDirty(badge);
            }

            Debug.Log("<color=#00FFAA><b>[ModelColorStudio]</b></color> Sahnedeki tüm vagon ve robot modellerine canlı renk ve Toon ayarları anında uygulandı!");
        }
    }
}
