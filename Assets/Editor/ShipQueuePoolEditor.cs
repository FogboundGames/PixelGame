using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    [CustomEditor(typeof(ShipQueuePool))]
    public class ShipQueuePoolEditor : UnityEditor.Editor
    {
        private int m_SelectedPresetIndex = 2; // Varsayılan olarak 4 sütun (index 2) seçili
        private bool m_ShowAllPresetsList = false;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "🚢 Gemi Kuyruk Havuzu Kontrolü\n" +
                "Buradaki slider'ları kaydırdığınızda gemiler sahne üzerinde ANINDA canlı olarak güncellenir.\n" +
                "Her sütun sayısı (3, 4, 5 vb.) için özel boyut ve aralık profilleri belirleyebilirsiniz.",
                MessageType.Info
            );

            EditorGUILayout.Space(6);

            SerializedProperty usePresetsProp = serializedObject.FindProperty("m_UseColumnPresets");
            SerializedProperty presetsListProp = serializedObject.FindProperty("m_ColumnPresets");
            SerializedProperty scaleProp = serializedObject.FindProperty("m_ShipScale");
            SerializedProperty spacingXProp = serializedObject.FindProperty("m_SpacingX");
            SerializedProperty spacingYProp = serializedObject.FindProperty("m_SpacingY");
            SerializedProperty offsetYProp = serializedObject.FindProperty("m_OffsetY");
            SerializedProperty colsProp = serializedObject.FindProperty("m_Columns");
            SerializedProperty rowsProp = serializedObject.FindProperty("m_Rows");
            SerializedProperty prefabProp = serializedObject.FindProperty("m_ShipPrefab");

            EditorGUI.BeginChangeCheck();

            // 1. Sütun Profilleri Bölümü
            if (usePresetsProp != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(usePresetsProp, new GUIContent("🎛️ Sütuna Göre Otomatik Boyut/Aralık", "Bölümde kaç sütun gemi varsa (3, 4, 5 vb.) ona özel belirlediğiniz ayarları otomatik uygular."));

                if (usePresetsProp.boolValue && presetsListProp != null && presetsListProp.arraySize > 0)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("🎯 Sütun Profili Seç & Düzenle:", EditorStyles.boldLabel);

                    string[] presetLabels = new string[presetsListProp.arraySize];
                    for (int i = 0; i < presetsListProp.arraySize; i++)
                    {
                        var elem = presetsListProp.GetArrayElementAtIndex(i);
                        int c = elem.FindPropertyRelative("columns").intValue;
                        presetLabels[i] = $"{c} Sütun";
                    }

                    if (m_SelectedPresetIndex >= presetsListProp.arraySize) m_SelectedPresetIndex = 0;
                    m_SelectedPresetIndex = GUILayout.Toolbar(m_SelectedPresetIndex, presetLabels, GUILayout.Height(24));

                    SerializedProperty activePreset = presetsListProp.GetArrayElementAtIndex(m_SelectedPresetIndex);
                    if (activePreset != null)
                    {
                        EditorGUILayout.Space(4);
                        SerializedProperty pCols = activePreset.FindPropertyRelative("columns");
                        SerializedProperty pScale = activePreset.FindPropertyRelative("shipScale");
                        SerializedProperty pSpX = activePreset.FindPropertyRelative("spacingX");
                        SerializedProperty pSpY = activePreset.FindPropertyRelative("spacingY");
                        SerializedProperty pOffY = activePreset.FindPropertyRelative("offsetY");

                        EditorGUILayout.LabelField($"⚙️ {pCols.intValue} Sütun İçin Özel Ayarlar:", EditorStyles.boldLabel);
                        EditorGUILayout.Slider(pScale, 0.10f, 0.40f, new GUIContent("📏 Gemi Boyutu (Ship Scale)", "Bu sütun sayısındaki gemi ölçeği"));
                        EditorGUILayout.Slider(pSpX, 0.5f, 2.5f, new GUIContent("↔️ Yatay Aralık (Spacing X)", "Yan yana gemiler arası mesafe"));
                        EditorGUILayout.Slider(pSpY, 0.5f, 2.5f, new GUIContent("↕️ Dikey Aralık (Spacing Y)", "Sıralar arası dikey mesafe"));
                        EditorGUILayout.Slider(pOffY, -8.0f, 0.0f, new GUIContent("📍 Dikey Konum (Offset Y)", "Deniz üzerindeki yükseklik"));

                        EditorGUILayout.Space(4);
                        if (GUILayout.Button($"▶️ Bu Profili ({pCols.intValue} Sütun) Sahneye Canlı Uygula", GUILayout.Height(24)))
                        {
                            colsProp.intValue = pCols.intValue;
                            scaleProp.floatValue = pScale.floatValue;
                            spacingXProp.floatValue = pSpX.floatValue;
                            spacingYProp.floatValue = pSpY.floatValue;
                            offsetYProp.floatValue = pOffY.floatValue;
                        }
                    }

                    EditorGUILayout.Space(4);
                    m_ShowAllPresetsList = EditorGUILayout.Foldout(m_ShowAllPresetsList, "📋 Tüm Profil Listesi (Ekle / Kaldır)");
                    if (m_ShowAllPresetsList)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(presetsListProp, true);
                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(6);

            // 2. Sahnedeki Aktif Değerler (Canlı Kontrol)
            EditorGUILayout.LabelField("🎮 Sahnedeki Canlı Ayarlar", EditorStyles.boldLabel);
            if (colsProp != null)
            {
                EditorGUILayout.IntSlider(colsProp, 1, 8, new GUIContent("🏛️ Aktif Sütun Sayısı (Columns)", "Sahnedeki yatay gemi sayısı"));
            }
            if (rowsProp != null)
            {
                EditorGUILayout.IntSlider(rowsProp, 1, 6, new GUIContent("🧱 Aktif Satır Sayısı (Rows)", "Sahnedeki dikey sıra sayısı"));
            }

            EditorGUILayout.Space(4);
            if (scaleProp != null)
            {
                EditorGUILayout.Slider(scaleProp, 0.10f, 0.40f, new GUIContent("📏 Aktif Gemi Boyutu", "Tüm gemilerin mevcut ölçeği"));
            }
            if (spacingXProp != null)
            {
                EditorGUILayout.Slider(spacingXProp, 0.5f, 2.5f, new GUIContent("↔️ Aktif Yatay Aralık (X)", "Mevcut yatay aralık"));
            }
            if (spacingYProp != null)
            {
                EditorGUILayout.Slider(spacingYProp, 0.5f, 2.5f, new GUIContent("↕️ Aktif Dikey Aralık (Y)", "Mevcut dikey sıra aralığı"));
            }
            if (offsetYProp != null)
            {
                EditorGUILayout.Slider(offsetYProp, -8.0f, 0.0f, new GUIContent("📍 Aktif Yükseklik (Offset Y)", "Mevcut havuz yüksekliği"));
            }

            EditorGUILayout.Space(6);
            if (prefabProp != null)
            {
                EditorGUILayout.PropertyField(prefabProp, new GUIContent("📦 Gemi Prefabı"));
            }

            bool changed = EditorGUI.EndChangeCheck();

            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
                ShipQueuePool pool = (ShipQueuePool)target;

                // Eğer preset seçiliyse ve o preset üzerinde oynandıysa ve sahnedeki sütunla uyuşuyorsa aktar
                if (usePresetsProp.boolValue)
                {
                    pool.ApplyPresetForColumns(pool.Capacity > 0 ? colsProp.intValue : 4);
                }

                pool.ApplyLiveSettings();
                EditorUtility.SetDirty(pool);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Canlı Yenile / Hizala", GUILayout.Height(28)))
            {
                serializedObject.ApplyModifiedProperties();
                ShipQueuePool pool = (ShipQueuePool)target;
                if (usePresetsProp.boolValue)
                {
                    pool.ApplyPresetForColumns(colsProp.intValue);
                }
                pool.ApplyLiveSettings();
                EditorUtility.SetDirty(pool);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("⚓ Varsayılan Profilleri Yükle", GUILayout.Height(28)))
            {
                ShipQueuePool pool = (ShipQueuePool)target;
                pool.ColumnPresets.Clear();
                pool.ColumnPresets.Add(new ColumnLayoutPreset(2, 0.472f, 1.25f, 2.60f, -6.88f));
                pool.ColumnPresets.Add(new ColumnLayoutPreset(3, 0.354f, 1.18f, 2.00f, -7.40f));
                pool.ColumnPresets.Add(new ColumnLayoutPreset(4, 0.330f, 0.96f, 2.00f, -6.88f));
                pool.ColumnPresets.Add(new ColumnLayoutPreset(5, 0.295f, 0.729f, 2.48f, -8.15f));
                pool.ColumnPresets.Add(new ColumnLayoutPreset(6, 0.278f, 0.58f, 2.00f, -6.88f));
                pool.ApplyPresetForColumns(colsProp.intValue);
                pool.ApplyLiveSettings();
                EditorUtility.SetDirty(pool);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("📍 Spot ve Kuyruk Listeleri", EditorStyles.miniBoldLabel);
            SerializedProperty spotsProp = serializedObject.FindProperty("m_QueueSpots");
            SerializedProperty shipsProp = serializedObject.FindProperty("m_WaitingShips");
            if (spotsProp != null) EditorGUILayout.PropertyField(spotsProp, false);
            if (shipsProp != null) EditorGUILayout.PropertyField(shipsProp, false);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
