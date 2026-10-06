using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    [CustomEditor(typeof(MarinaSlotLayout))]
    public class MarinaSlotLayoutEditor : UnityEditor.Editor
    {
        private static int s_SelectedSlotTab = 2; // 0: 3 Slot, 1: 4 Slot, 2: 5 Slot
        private static readonly string[] TabLabels = new string[] { "⚓ 3 Slot", "⚓ 4 Slot", "⚓ 5 Slot" };
        private static readonly int[] TabSlotCounts = new int[] { 3, 4, 5 };

        private bool m_ShowGlobalSettings = false;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            MarinaSlotLayout layout = (MarinaSlotLayout)target;
            layout.EnsureConfigsInitialized();

            EditorGUILayout.HelpBox(
                "Bu bileşen slot sayısına göre (3, 4, 5 slot) iskele ve slotların kıyıya (sahile) tam oturmasını, " +
                "sahil koyunun kavisinden kaynaklanan kaymaların bağımsız olarak düzeltilmesini sağlar.",
                MessageType.Info
            );

            EditorGUILayout.Space(4);

            SerializedProperty usePerCountProp = serializedObject.FindProperty("m_UsePerCountSettings");
            SerializedProperty enableCurvedProp = serializedObject.FindProperty("m_EnableCurvedPier");
            SerializedProperty tiltProp = serializedObject.FindProperty("m_WaterTiltX");
            SerializedProperty pierRotXProp = serializedObject.FindProperty("m_PierRotationX");
            SerializedProperty arcAsymProp = serializedObject.FindProperty("m_ArcAsymmetry");
            SerializedProperty arcFanProp = serializedObject.FindProperty("m_ArcAngleFan");
            SerializedProperty fitToScreenProp = serializedObject.FindProperty("m_FitToScreenWidth");
            SerializedProperty slotCountProp = serializedObject.FindProperty("m_SlotCount");

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(usePerCountProp, new GUIContent("🎯 Slot Sayısına Göre Özel Kıyı Ayarları"));

            if (usePerCountProp.boolValue)
            {
                EditorGUILayout.Space(6);

                // Slot Tab Selector
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("🛠️ Düzenlenen Slot Sayısı:", EditorStyles.boldLabel, GUILayout.Width(170));
                s_SelectedSlotTab = GUILayout.Toolbar(s_SelectedSlotTab, TabLabels, GUILayout.Height(26));
                EditorGUILayout.EndHorizontal();

                int currentTabCount = TabSlotCounts[s_SelectedSlotTab];

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                string activeStatus = layout.SlotCount == currentTabCount ? "🟢 Sahnede Aktif" : $"⚪ Sahne şu an: {layout.SlotCount} Slot";
                EditorGUILayout.LabelField(activeStatus, EditorStyles.miniLabel);

                if (layout.SlotCount != currentTabCount)
                {
                    GUI.backgroundColor = new Color(0.3f, 0.85f, 1f);
                    if (GUILayout.Button($"👁️ Sahnede {currentTabCount} Slot'u Önizle", EditorStyles.miniButton, GUILayout.Height(20)))
                    {
                        Undo.RecordObject(layout, $"Preview {currentTabCount} Slots");
                        layout.SetSlotCount(currentTabCount);
                        EditorUtility.SetDirty(layout);
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(6);

                // Seçili konfigürasyon mülkünü bul
                string configPropName = currentTabCount == 3 ? "m_Config3Slots" : (currentTabCount == 4 ? "m_Config4Slots" : "m_Config5Slots");
                SerializedProperty configProp = serializedObject.FindProperty(configPropName);

                if (configProp != null)
                {
                    SerializedProperty rowOffsetYProp = configProp.FindPropertyRelative("RowOffsetY");
                    SerializedProperty rowOffsetZProp = configProp.FindPropertyRelative("RowOffsetZ");
                    SerializedProperty pierMatProp = configProp.FindPropertyRelative("PierMaterial");
                    SerializedProperty pierWProp = configProp.FindPropertyRelative("PierWidth");
                    SerializedProperty pierScaleProp = configProp.FindPropertyRelative("PierScaleMultiplier");
                    SerializedProperty pierOffsetYProp = configProp.FindPropertyRelative("PierOffsetY");
                    SerializedProperty pierOffsetZProp = configProp.FindPropertyRelative("PierOffsetZ");
                    SerializedProperty baySpacingPxProp = configProp.FindPropertyRelative("BaySpacingPx");
                    SerializedProperty bayOffsetYProp = configProp.FindPropertyRelative("BaySlotOffsetY");
                    SerializedProperty arcCurveYProp = configProp.FindPropertyRelative("ArcCurveY");
                    SerializedProperty slotAngleProp = configProp.FindPropertyRelative("SlotAngle");
                    SerializedProperty slotWProp = configProp.FindPropertyRelative("SlotWidth");
                    SerializedProperty slotLProp = configProp.FindPropertyRelative("SlotLength");
                    SerializedProperty legacySpacingProp = configProp.FindPropertyRelative("LegacySpacing");

                    // 1. Kıyı Yerleşimi
                    EditorGUILayout.LabelField($"🏖️ {currentTabCount} Slot Kıyı & Sahil Yerleşimi", EditorStyles.boldLabel);
                    EditorGUILayout.Slider(rowOffsetYProp, -4.0f, 0.0f, new GUIContent("↕️ Sahil Kıyı Yüksekliği (RowOffsetY)", "Tüm slot şeridinin Y yüksekliği (Kumsal çizgisine oturma seviyesi)."));
                    EditorGUILayout.Slider(rowOffsetZProp, -1.0f, 1.0f, new GUIContent("↔️ Kıyı Derinliği (RowOffsetZ)", "Slot şeridinin Z derinliği."));

                    EditorGUILayout.Space(6);

                    // 2. İskele Ayarları
                    EditorGUILayout.LabelField($"🪵 {currentTabCount} Slot İskele Görseli (Pier Visual)", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(pierMatProp, new GUIContent("🎨 İskele Materyali"));
                    EditorGUILayout.Slider(pierWProp, 4.0f, 12.0f, new GUIContent("↔️ İskele Genişliği (PierWidth)"));
                    EditorGUILayout.Slider(pierScaleProp, 0.5f, 2.0f, new GUIContent("🔍 İskele Ölçeği (Multiplier)"));
                    EditorGUILayout.Slider(pierOffsetYProp, -3.0f, 3.0f, new GUIContent("↕️ İskele Y Ofseti"));
                    EditorGUILayout.Slider(pierOffsetZProp, -2.0f, 2.0f, new GUIContent("↔️ İskele Z Ofseti"));
                    EditorGUILayout.Slider(baySpacingPxProp, 140f, 210f, new GUIContent("📏 Doku Koy Aralığı (px)", "Doku üzerindeki parmak iskeleler arası mesafe."));

                    EditorGUILayout.Space(6);

                    // 3. Gemi Yanaşma & Kavis
                    EditorGUILayout.LabelField($"⚓ {currentTabCount} Slot Gemi Yanaşma & Kavis", EditorStyles.boldLabel);
                    EditorGUILayout.Slider(bayOffsetYProp, -1.5f, 1.5f, new GUIContent("↕️ Gemi Yanaşma Y Ofseti", "Gemilerin iskele kolları arasında açık suya oturma derinliği."));
                    EditorGUILayout.Slider(arcCurveYProp, -0.3f, 0.3f, new GUIContent("🌊 Sahil Koyu Kavis Gücü (ArcCurveY)", "Slotların sahil yayına uyumlu kavis eğriliği."));
                    EditorGUILayout.Slider(slotAngleProp, -60f, 60f, new GUIContent("📐 Slot Yanaşma Açısı"));

                    EditorGUILayout.Space(6);

                    // 4. Slot Boyutları
                    EditorGUILayout.LabelField($"📐 {currentTabCount} Slot Boyutları", EditorStyles.boldLabel);
                    EditorGUILayout.Slider(slotWProp, 0.5f, 3.0f, new GUIContent("↔️ Slot Genişliği (Width)"));
                    EditorGUILayout.Slider(slotLProp, 0.5f, 3.5f, new GUIContent("↕️ Slot Uzunluğu (Length)"));
                    if (!enableCurvedProp.boolValue)
                    {
                        EditorGUILayout.Slider(legacySpacingProp, 0.8f, 2.5f, new GUIContent("📏 Klasik Aralık (Spacing)"));
                    }

                    EditorGUILayout.Space(6);
                    EditorGUILayout.BeginHorizontal();
                    GUI.backgroundColor = new Color(0.95f, 0.9f, 0.7f);
                    if (GUILayout.Button($"✨ {currentTabCount} Slot Ayarlarını Varsayılana Sıfırla", GUILayout.Height(22)))
                    {
                        Undo.RecordObject(layout, $"Reset {currentTabCount} Slots to Defaults");
                        layout.ResetToCalibratedDefaults(currentTabCount);
                        EditorUtility.SetDirty(layout);
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                // Global / Eski Tekil Mod
                EditorGUILayout.HelpBox("Tekil mod aktif: Tüm slot sayıları tek bir global yükseklik ve aralık değeri kullanır.", MessageType.Warning);

                SerializedProperty widthProp = serializedObject.FindProperty("m_SlotWidth");
                SerializedProperty lengthProp = serializedObject.FindProperty("m_SlotLength");
                SerializedProperty spacingProp = serializedObject.FindProperty("m_SlotSpacing");
                SerializedProperty offsetYProp = serializedObject.FindProperty("m_OffsetY");
                SerializedProperty offsetZProp = serializedObject.FindProperty("m_OffsetZ");
                SerializedProperty baySlotOffsetYProp = serializedObject.FindProperty("m_BaySlotOffsetY");
                SerializedProperty arcCurveYProp = serializedObject.FindProperty("m_ArcCurveY");

                EditorGUILayout.Slider(offsetYProp, -5f, 5f, new GUIContent("📍 Sahil Yüksekliği (Y)"));
                EditorGUILayout.Slider(offsetZProp, -3f, 3f, new GUIContent("📍 Derinlik (Z)"));
                EditorGUILayout.Slider(widthProp, 0.5f, 3.0f, new GUIContent("↔️ Slot Genişliği"));
                EditorGUILayout.Slider(lengthProp, 0.5f, 3.5f, new GUIContent("↕️ Slot Uzunluğu"));
                EditorGUILayout.Slider(spacingProp, 0.8f, 2.5f, new GUIContent("📏 Slot Aralığı"));
                EditorGUILayout.Slider(baySlotOffsetYProp, -1.0f, 1.0f, new GUIContent("Gemi Yanaşma Y Ofseti"));
                EditorGUILayout.Slider(arcCurveYProp, -0.3f, 0.3f, new GUIContent("🌊 Kavis Gücü"));
            }

            EditorGUILayout.Space(8);

            // Ortak Parametreler (Collapsible)
            m_ShowGlobalSettings = EditorGUILayout.Foldout(m_ShowGlobalSettings, "🌐 Ortak Sahne & Su Parametreleri (Tüm Slotlar İçin)", true);
            if (m_ShowGlobalSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(enableCurvedProp, new GUIContent("🪵 Kavisli İskele Aktif"));
                EditorGUILayout.Slider(tiltProp, -90f, 0f, new GUIContent("🌊 Su Eğim Açısı"));
                EditorGUILayout.Slider(pierRotXProp, -90f, 0f, new GUIContent("🪵 İskele Rotasyon X"));
                EditorGUILayout.Slider(arcAsymProp, -0.2f, 0.2f, new GUIContent("📐 Kavis Asimetrisi"));
                EditorGUILayout.Slider(arcFanProp, -10f, 10f, new GUIContent("🪭 Açı Yelpazesi"));
                EditorGUILayout.PropertyField(fitToScreenProp, new GUIContent("📱 Ekrana Sığdır (Fit to Screen)"));
                EditorGUILayout.IntSlider(slotCountProp, 1, 8, new GUIContent("⚓ Sahnedeki Aktif Slot Sayısı"));
                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                layout.ApplyLayout();
                EditorUtility.SetDirty(layout);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            }

            EditorGUILayout.Space(10);

            // Aksiyon Butonları
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.8f, 1.0f);
            if (GUILayout.Button("⚓ Kavisli İskele Standartlarını Kur", GUILayout.Height(28)))
            {
                SetupCurvedMarinaPier.SetupCurvedPier();
            }

            GUI.backgroundColor = new Color(0.85f, 0.85f, 0.9f);
            if (GUILayout.Button("🔄 Yeniden Hizala (Apply)", GUILayout.Height(28)))
            {
                layout.ApplyLayout();
                EditorUtility.SetDirty(layout);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.75f, 1.0f, 0.85f);
            if (GUILayout.Button("✨ Tüm Slot Sayılarını Kıyı Standartlarına Sıfırla (3, 4, 5)", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("Kıyı Standartlarını Sıfırla",
                    "3, 4 ve 5 slot kıyı ve iskele parametreleri test edilmiş ve kıyıya tam oturan standart değerlere sıfırlansın mı?",
                    "Evet, Sıfırla", "İptal"))
                {
                    Undo.RecordObject(layout, "Reset All Slots to Calibrated Defaults");
                    layout.ResetToCalibratedDefaults(0);
                    EditorUtility.SetDirty(layout);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
                }
            }
            GUI.backgroundColor = Color.white;
        }
    }
}
