using UnityEngine;
using UnityEditor;

namespace PixelGame.Editor
{
    [CustomEditor(typeof(UIFakeShadow))]
    public class UIFakeShadowEditor : UnityEditor.Editor
    {
        private SerializedProperty m_ShapeMode;
        private SerializedProperty m_EnableNotch;
        private SerializedProperty m_NotchWidth;
        private SerializedProperty m_NotchHeight;
        private SerializedProperty m_NotchRadius;
        private SerializedProperty m_CornerRadius;

        private SerializedProperty m_ShadowColor;
        private SerializedProperty m_Intensity;
        private SerializedProperty m_UniformSize;
        private SerializedProperty m_GlobalSize;
        private SerializedProperty m_TopSize;
        private SerializedProperty m_BottomSize;
        private SerializedProperty m_LeftSize;
        private SerializedProperty m_RightSize;
        private SerializedProperty m_EnableTop;
        private SerializedProperty m_EnableBottom;
        private SerializedProperty m_EnableLeft;
        private SerializedProperty m_EnableRight;
        private SerializedProperty m_Falloff;
        private SerializedProperty m_QualitySteps;
        private SerializedProperty m_CornerSegments;
        private SerializedProperty m_OffsetTop;
        private SerializedProperty m_OffsetBottom;
        private SerializedProperty m_OffsetLeft;
        private SerializedProperty m_OffsetRight;

        private void OnEnable()
        {
            m_ShapeMode = serializedObject.FindProperty("m_ShapeMode");
            m_EnableNotch = serializedObject.FindProperty("m_EnableNotch");
            m_NotchWidth = serializedObject.FindProperty("m_NotchWidth");
            m_NotchHeight = serializedObject.FindProperty("m_NotchHeight");
            m_NotchRadius = serializedObject.FindProperty("m_NotchRadius");
            m_CornerRadius = serializedObject.FindProperty("m_CornerRadius");

            m_ShadowColor = serializedObject.FindProperty("m_ShadowColor");
            m_Intensity = serializedObject.FindProperty("m_Intensity");
            m_UniformSize = serializedObject.FindProperty("m_UniformSize");
            m_GlobalSize = serializedObject.FindProperty("m_GlobalSize");
            m_TopSize = serializedObject.FindProperty("m_TopSize");
            m_BottomSize = serializedObject.FindProperty("m_BottomSize");
            m_LeftSize = serializedObject.FindProperty("m_LeftSize");
            m_RightSize = serializedObject.FindProperty("m_RightSize");
            m_EnableTop = serializedObject.FindProperty("m_EnableTop");
            m_EnableBottom = serializedObject.FindProperty("m_EnableBottom");
            m_EnableLeft = serializedObject.FindProperty("m_EnableLeft");
            m_EnableRight = serializedObject.FindProperty("m_EnableRight");
            m_Falloff = serializedObject.FindProperty("m_Falloff");
            m_QualitySteps = serializedObject.FindProperty("m_QualitySteps");
            m_CornerSegments = serializedObject.FindProperty("m_CornerSegments");
            m_OffsetTop = serializedObject.FindProperty("m_OffsetTop");
            m_OffsetBottom = serializedObject.FindProperty("m_OffsetBottom");
            m_OffsetLeft = serializedObject.FindProperty("m_OffsetLeft");
            m_OffsetRight = serializedObject.FindProperty("m_OffsetRight");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            UIFakeShadow shadowTarget = (UIFakeShadow)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("Telefonun çentiği (notch) ve yuvarlatılmış köşelerine göre iç gölgeyi canlı olarak buradan ayarlayabilirsiniz.", MessageType.Info);
            EditorGUILayout.Space(4);

            // Şekil Seçimi
            EditorGUILayout.LabelField("Gölge Modu", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ShapeMode, new GUIContent("Gölge Şekli"));

            EditorGUILayout.Space(4);

            // Otomatik Algılama Butonu
            if (m_ShapeMode.enumValueIndex == (int)ShadowShape.PhoneWithNotch)
            {
                if (GUILayout.Button("📱 Simülatörden Çentiği Otomatik Algıla", GUILayout.Height(28)))
                {
                    shadowTarget.AutoDetectNotchFromSafeArea();
                    EditorUtility.SetDirty(shadowTarget);
                }
                EditorGUILayout.Space(4);

                EditorGUILayout.LabelField("Çentik (Notch) & Ekran Köşeleri", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(m_EnableNotch, new GUIContent("Çentik Gölgesi Aktif"));

                if (m_EnableNotch.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(m_NotchWidth, new GUIContent("Çentik Genişliği"));
                    EditorGUILayout.PropertyField(m_NotchHeight, new GUIContent("Çentik Derinliği (Aşağı İniş)"));
                    EditorGUILayout.PropertyField(m_NotchRadius, new GUIContent("Çentik Köşe Yumuşatma"));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.PropertyField(m_CornerRadius, new GUIContent("Ekran Köşe Yuvarlama (Radius)"));
            }

            EditorGUILayout.Space(8);

            // Hızlı Şablonlar
            EditorGUILayout.LabelField("Hızlı Hazır Ayarlar (Presets)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Doğal (55px)"))
            {
                m_UniformSize.boolValue = true;
                m_GlobalSize.floatValue = 55f;
                m_Intensity.floatValue = 0.85f;
                m_Falloff.floatValue = 1.5f;
            }
            if (GUILayout.Button("Belirgin (75px)"))
            {
                m_UniformSize.boolValue = true;
                m_GlobalSize.floatValue = 75f;
                m_Intensity.floatValue = 1.0f;
                m_Falloff.floatValue = 1.3f;
            }
            if (GUILayout.Button("Hafif (35px)"))
            {
                m_UniformSize.boolValue = true;
                m_GlobalSize.floatValue = 35f;
                m_Intensity.floatValue = 0.65f;
                m_Falloff.floatValue = 1.8f;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Renk ve Yoğunluk
            EditorGUILayout.LabelField("Renk & Yoğunluk", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ShadowColor, new GUIContent("Gölge Rengi"));
            EditorGUILayout.PropertyField(m_Intensity, new GUIContent("Gölge Opaklığı (0-1)"));

            EditorGUILayout.Space(8);

            // Boyutlar
            EditorGUILayout.LabelField("Gölge Kalınlığı", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_UniformSize, new GUIContent("Tüm Kenarlar Eşit"));

            if (m_UniformSize.boolValue)
            {
                EditorGUILayout.PropertyField(m_GlobalSize, new GUIContent("Genel Kalınlık (px)"));
            }
            else
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_TopSize, new GUIContent("Üst Kenar & Çentik"));
                EditorGUILayout.PropertyField(m_BottomSize, new GUIContent("Alt Kenar"));
                EditorGUILayout.PropertyField(m_LeftSize, new GUIContent("Sol Kenar"));
                EditorGUILayout.PropertyField(m_RightSize, new GUIContent("Sağ Kenar"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            // Kenar Aç / Kapa
            EditorGUILayout.LabelField("Kenarları Aç / Kapa", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            m_EnableTop.boolValue = GUILayout.Toggle(m_EnableTop.boolValue, "Üst & Çentik", "Button");
            m_EnableBottom.boolValue = GUILayout.Toggle(m_EnableBottom.boolValue, "Alt", "Button");
            m_EnableLeft.boolValue = GUILayout.Toggle(m_EnableLeft.boolValue, "Sol", "Button");
            m_EnableRight.boolValue = GUILayout.Toggle(m_EnableRight.boolValue, "Sağ", "Button");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Geçiş ve Yumuşaklık
            EditorGUILayout.LabelField("Yumuşaklık & Kalite", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_Falloff, new GUIContent("Dağılma Yumuşaklığı"));
            EditorGUILayout.PropertyField(m_QualitySteps, new GUIContent("Geçiş Halkaları (Steps)"));
            EditorGUILayout.PropertyField(m_CornerSegments, new GUIContent("Köşe Segmentleri"));

            EditorGUILayout.Space(8);

            // Çerçeve Boşlukları
            EditorGUILayout.LabelField("Çerçeve Boşlukları (Offset / Margin)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_OffsetTop, new GUIContent("Üst Boşluk"));
            EditorGUILayout.PropertyField(m_OffsetBottom, new GUIContent("Alt Boşluk"));
            EditorGUILayout.PropertyField(m_OffsetLeft, new GUIContent("Sol Boşluk"));
            EditorGUILayout.PropertyField(m_OffsetRight, new GUIContent("Sağ Boşluk"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
