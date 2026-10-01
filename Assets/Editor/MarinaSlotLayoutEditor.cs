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

            EditorGUILayout.LabelField("⚓ Slot Boyutları (2. Görsel Standart)", EditorStyles.boldLabel);
            EditorGUILayout.Slider(widthProp, 0.5f, 3.0f, new GUIContent("↔️ Slot Genişliği (Width)", "Slotların X eksenindeki yatay genişliği (2. görsel: 1.72f)"));
            EditorGUILayout.Slider(lengthProp, 0.5f, 3.5f, new GUIContent("↕️ Slot Uzunluğu (Length / Height)", "Slotların Z eksenindeki dikey/uzunluk boyu (2. görsel: 2.35f)"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("📏 Yerleşim & Açı", EditorStyles.boldLabel);
            EditorGUILayout.Slider(spacingProp, 0.8f, 2.5f, new GUIContent("📏 Slot Aralığı (Spacing)", "Slotların birbirine olan mesafesi"));
            EditorGUILayout.Slider(angleProp, -60f, 60f, new GUIContent("📐 Çapraz Marina Açısı", "Slotların yanaşma açısı (varsayılan: -28°)"));
            EditorGUILayout.Slider(tiltProp, -90f, 0f, new GUIContent("🌊 Su Eğim Açısı", "Kamera açısına göre eğim (varsayılan: -28°)"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("🌊 Sahil Kavis / Yay Eğrisi (Shoreline Arc)", EditorStyles.boldLabel);
            EditorGUILayout.Slider(arcCurveYProp, -0.3f, 0.3f, new GUIContent("🌊 Kavis Gücü (Arc Curve Y)", "U-şeklinde sahil koyu eğriliği"));
            EditorGUILayout.Slider(arcAsymProp, -0.2f, 0.2f, new GUIContent("📐 Kavis Asimetrisi", "Sol sahilin sağ sahile göre yükseklik farkı"));
            EditorGUILayout.Slider(arcFanProp, -10f, 10f, new GUIContent("🪭 Açı Yelpazesi (Fan Angle)", "Kavis boyunca slotların hafifçe fırlama/dönme açısı"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("📍 Dikey Konum & Derinlik", EditorStyles.boldLabel);
            EditorGUILayout.Slider(offsetYProp, -5f, 5f, new GUIContent("📍 Yükseklik (Y)", "Dikey konum (Sahil için -2.83f)"));
            EditorGUILayout.Slider(offsetZProp, -3f, 3f, new GUIContent("📍 Derinlik (Z)", "İleri/Geri konum"));

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
            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
            if (GUILayout.Button("📸 2. Görsel Boyutunu Uygula", GUILayout.Height(24)))
            {
                widthProp.floatValue = 1.72f;
                lengthProp.floatValue = 2.35f;
                spacingProp.floatValue = 1.42f;
                angleProp.floatValue = -28f;
                tiltProp.floatValue = -68f;
                offsetYProp.floatValue = 2.82f;
                offsetZProp.floatValue = 0.53f;
                arcCurveYProp.floatValue = 0.055f;
                arcAsymProp.floatValue = -0.055f;
                arcFanProp.floatValue = 0f;
                serializedObject.ApplyModifiedProperties();
                MarinaSlotLayout layout = (MarinaSlotLayout)target;
                layout.ApplyLayout();
                EditorUtility.SetDirty(layout);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            }

            GUI.backgroundColor = new Color(0.85f, 0.85f, 0.9f);
            if (GUILayout.Button("🔄 Yeniden Hizala", GUILayout.Height(24)))
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
