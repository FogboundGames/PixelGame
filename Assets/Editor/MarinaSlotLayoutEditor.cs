using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    [CustomEditor(typeof(MarinaSlotLayout))]
    public class MarinaSlotLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Bu bileşen su üzerindeki tüm gemi yanaşma slotlarının (WaterSlot_1..5) boyutunu, " +
                "sahil koyu yay/kavisini, aralarındaki mesafeyi ve açısını canlı kontrol eder. 2. görseldeki geniş ve orantılı can simidi standartları uygulanmıştır.",
                MessageType.Info
            );

            EditorGUILayout.Space(4);

            SerializedProperty enableCurvedProp = serializedObject.FindProperty("m_EnableCurvedPier");
            SerializedProperty pierMat4Prop = serializedObject.FindProperty("m_PierMaterial4Slots");
            SerializedProperty pierMat5Prop = serializedObject.FindProperty("m_PierMaterial5Slots");
            SerializedProperty pierMat3Prop = serializedObject.FindProperty("m_PierMaterial3Slots");
            SerializedProperty pierW4Prop = serializedObject.FindProperty("m_PierWidth4Slots");
            SerializedProperty pierW5Prop = serializedObject.FindProperty("m_PierWidth5Slots");
            SerializedProperty pierW3Prop = serializedObject.FindProperty("m_PierWidth3Slots");
            SerializedProperty pierOffsetYProp = serializedObject.FindProperty("m_PierOffsetY");
            SerializedProperty pierOffsetZProp = serializedObject.FindProperty("m_PierOffsetZ");
            SerializedProperty pierScaleMulProp = serializedObject.FindProperty("m_PierScaleMultiplier");
            SerializedProperty baySlotOffsetYProp = serializedObject.FindProperty("m_BaySlotOffsetY");

            SerializedProperty widthProp = serializedObject.FindProperty("m_SlotWidth");
            SerializedProperty lengthProp = serializedObject.FindProperty("m_SlotLength");
            SerializedProperty spacingProp = serializedObject.FindProperty("m_SlotSpacing");
            SerializedProperty angleProp = serializedObject.FindProperty("m_SlotAngle");
            SerializedProperty tiltProp = serializedObject.FindProperty("m_WaterTiltX");
            SerializedProperty offsetYProp = serializedObject.FindProperty("m_OffsetY");
            SerializedProperty offsetZProp = serializedObject.FindProperty("m_OffsetZ");
            SerializedProperty arcCurveYProp = serializedObject.FindProperty("m_ArcCurveY");
            SerializedProperty arcAsymProp = serializedObject.FindProperty("m_ArcAsymmetry");
            SerializedProperty arcFanProp = serializedObject.FindProperty("m_ArcAngleFan");

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("🪵 Kavisli Ahşap İskele (Curved Pier)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(enableCurvedProp, new GUIContent("⚓ Kavisli İskele Aktif"));

            if (enableCurvedProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(pierMat4Prop, new GUIContent("4 Slot Materyali"));
                EditorGUILayout.PropertyField(pierMat5Prop, new GUIContent("5 Slot Materyali"));
                EditorGUILayout.PropertyField(pierMat3Prop, new GUIContent("3 Slot Materyali"));
                EditorGUILayout.Slider(pierW4Prop, 5.0f, 12.0f, new GUIContent("4 Slot İskele Genişliği"));
                EditorGUILayout.Slider(pierW5Prop, 5.0f, 12.0f, new GUIContent("5 Slot İskele Genişliği"));
                EditorGUILayout.Slider(pierW3Prop, 5.0f, 12.0f, new GUIContent("3 Slot İskele Genişliği"));
                EditorGUILayout.Slider(pierScaleMulProp, 0.5f, 2.0f, new GUIContent("İskele Genel Ölçeği"));
                EditorGUILayout.Slider(pierOffsetYProp, -3.0f, 3.0f, new GUIContent("İskele Y Ofseti"));
                EditorGUILayout.Slider(pierOffsetZProp, -2.0f, 2.0f, new GUIContent("İskele Z Ofseti"));
                EditorGUILayout.Slider(baySlotOffsetYProp, -1.0f, 1.0f, new GUIContent("Gemi Yanaşma Y Ofseti"));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.LabelField("⚓ Slot & Su Boyutları", EditorStyles.boldLabel);
            EditorGUILayout.Slider(widthProp, 0.5f, 3.0f, new GUIContent("↔️ Slot Genişliği (Width)"));
            EditorGUILayout.Slider(lengthProp, 0.5f, 3.5f, new GUIContent("↕️ Slot Uzunluğu (Length)"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("📏 Yerleşim & Açı", EditorStyles.boldLabel);
            if (!enableCurvedProp.boolValue)
            {
                EditorGUILayout.Slider(spacingProp, 0.8f, 2.5f, new GUIContent("📏 Slot Aralığı (Spacing)"));
            }
            EditorGUILayout.Slider(angleProp, -60f, 60f, new GUIContent("📐 Yanaşma Açısı"));
            EditorGUILayout.Slider(tiltProp, -90f, 0f, new GUIContent("🌊 Su Eğim Açısı"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("🌊 Sahil Kavis / Yay Eğrisi", EditorStyles.boldLabel);
            EditorGUILayout.Slider(arcCurveYProp, -0.3f, 0.3f, new GUIContent("🌊 Kavis Gücü (Arc Curve Y)"));
            EditorGUILayout.Slider(arcAsymProp, -0.2f, 0.2f, new GUIContent("📐 Kavis Asimetrisi"));
            EditorGUILayout.Slider(arcFanProp, -10f, 10f, new GUIContent("🪭 Açı Yelpazesi"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("📍 Kök Konum", EditorStyles.boldLabel);
            EditorGUILayout.Slider(offsetYProp, -5f, 5f, new GUIContent("📍 Yükseklik (Y)"));
            EditorGUILayout.Slider(offsetZProp, -3f, 3f, new GUIContent("📍 Derinlik (Z)"));

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                MarinaSlotLayout layout = (MarinaSlotLayout)target;
                layout.ApplyLayout();
                EditorUtility.SetDirty(layout);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.8f, 1.0f);
            if (GUILayout.Button("⚓ Kavisli İskele Standartlarını Uygula", GUILayout.Height(26)))
            {
                SetupCurvedMarinaPier.SetupCurvedPier();
            }

            GUI.backgroundColor = new Color(0.85f, 0.85f, 0.9f);
            if (GUILayout.Button("🔄 Yeniden Hizala", GUILayout.Height(26)))
            {
                MarinaSlotLayout layout = (MarinaSlotLayout)target;
                layout.ApplyLayout();
                EditorUtility.SetDirty(layout);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
    }
}
