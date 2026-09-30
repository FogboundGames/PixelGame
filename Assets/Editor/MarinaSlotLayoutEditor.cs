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
                "aralarındaki mesafeyi ve açısını canlı olarak kontrol eder. Slider'ları kaydırdığınızda sahne anında güncellenir.",
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

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.Slider(widthProp, 0.3f, 3.5f, new GUIContent("↔️ Slot Genişliği (Width)", "Slotların X eksenindeki yatay genişliği"));
            EditorGUILayout.Slider(lengthProp, 0.3f, 3.5f, new GUIContent("↕️ Slot Uzunluğu (Length / Height)", "Slotların Z eksenindeki dikey/uzunluk boyu"));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUIUtility.labelWidth);
            if (GUILayout.Button("🔗 1:1 Kare Yap (Genişliğe Eşitle)", EditorStyles.miniButton, GUILayout.Height(18)))
            {
                lengthProp.floatValue = widthProp.floatValue;
            }
            if (GUILayout.Button("🔗 1:1 Kare Yap (Uzunluğa Eşitle)", EditorStyles.miniButton, GUILayout.Height(18)))
            {
                widthProp.floatValue = lengthProp.floatValue;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.Slider(spacingProp, 0.6f, 2.5f, new GUIContent("📏 Slot Aralığı (Spacing)", "Slotların birbirine olan mesafesi"));
            EditorGUILayout.Slider(angleProp, -60f, 60f, new GUIContent("📐 Çapraz Marina Açısı", "Slotların yanaşma açısı"));
            EditorGUILayout.Slider(tiltProp, -90f, 0f, new GUIContent("🌊 Su Eğim Açısı", "Kamera açısına göre eğim"));

            EditorGUILayout.Space(4);
            EditorGUILayout.Slider(offsetYProp, -2f, 4f, new GUIContent("📍 Yükseklik (Y)", "Dikey konum"));
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
            if (GUILayout.Button("🔄 Slotları Yeniden Hizala"))
            {
                MarinaSlotLayout layout = (MarinaSlotLayout)target;
                layout.ApplyLayout();
                EditorUtility.SetDirty(layout);
            }

            GUI.backgroundColor = new Color(0.85f, 0.85f, 0.9f);
            if (GUILayout.Button("Varsayılana Dön"))
            {
                widthProp.floatValue = 1.15f;
                lengthProp.floatValue = 1.15f;
                spacingProp.floatValue = 1.40f;
                angleProp.floatValue = -28f;
                tiltProp.floatValue = -68f;
                offsetYProp.floatValue = 0.45f;
                offsetZProp.floatValue = 0.0f;
                serializedObject.ApplyModifiedProperties();
                MarinaSlotLayout layout = (MarinaSlotLayout)target;
                layout.ApplyLayout();
                EditorUtility.SetDirty(layout);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
    }
}
