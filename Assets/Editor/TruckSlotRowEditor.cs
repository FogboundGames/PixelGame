using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    [CustomEditor(typeof(TruckSlotRow))]
    public class TruckSlotRowEditor : UnityEditor.Editor
    {
        private SerializedProperty m_Slots;
        private SerializedProperty m_Style;
        private SerializedProperty m_ManualShadowMode;

        private SerializedProperty m_EnableShadow;
        private SerializedProperty m_ShadowSprite;
        private SerializedProperty m_ShadowColor;
        private SerializedProperty m_ShadowOffset;
        private SerializedProperty m_ShadowScale;
        private SerializedProperty m_ShadowZ;

        private SerializedProperty m_EnableRowGroundShadow;
        private SerializedProperty m_RowGroundShadowSprite;
        private SerializedProperty m_RowGroundShadowColor;
        private SerializedProperty m_RowGroundShadowOffset;
        private SerializedProperty m_RowGroundShadowPadding;
        private SerializedProperty m_RowGroundShadowZ;

        private bool m_ShowIndividualSlots = true;
        private bool m_ShowGlobalSettings = false;

        private void OnEnable()
        {
            m_Slots = serializedObject.FindProperty("m_Slots");
            m_Style = serializedObject.FindProperty("m_Style");
            m_ManualShadowMode = serializedObject.FindProperty("m_ManualShadowMode");

            if (m_Style != null)
            {
                m_EnableShadow = m_Style.FindPropertyRelative("enableShadow");
                m_ShadowSprite = m_Style.FindPropertyRelative("shadowSprite");
                m_ShadowColor = m_Style.FindPropertyRelative("shadowColor");
                m_ShadowOffset = m_Style.FindPropertyRelative("shadowOffset");
                m_ShadowScale = m_Style.FindPropertyRelative("shadowScale");
                m_ShadowZ = m_Style.FindPropertyRelative("shadowZ");

                m_EnableRowGroundShadow = m_Style.FindPropertyRelative("enableRowGroundShadow");
                m_RowGroundShadowSprite = m_Style.FindPropertyRelative("rowGroundShadowSprite");
                m_RowGroundShadowColor = m_Style.FindPropertyRelative("rowGroundShadowColor");
                m_RowGroundShadowOffset = m_Style.FindPropertyRelative("rowGroundShadowOffset");
                m_RowGroundShadowPadding = m_Style.FindPropertyRelative("rowGroundShadowPadding");
                m_RowGroundShadowZ = m_Style.FindPropertyRelative("rowGroundShadowZ");
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            TruckSlotRow rowTarget = (TruckSlotRow)target;

            EditorGUILayout.Space(6);

            // 1. Elle Ayarlama Modu Başlığı & Açıklaması
            bool isManual = m_ManualShadowMode != null && m_ManualShadowMode.boolValue;

            GUI.backgroundColor = isManual ? new Color(0.3f, 0.9f, 0.5f, 1f) : new Color(0.9f, 0.7f, 0.3f, 1f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("✋ Elle Ayarlama Modu (Manual Mode)", EditorStyles.boldLabel);
            if (m_ManualShadowMode != null)
            {
                m_ManualShadowMode.boolValue = EditorGUILayout.Toggle(m_ManualShadowMode.boolValue, GUILayout.Width(30));
            }
            EditorGUILayout.EndHorizontal();

            if (isManual)
            {
                EditorGUILayout.HelpBox("AÇIK: Sahnede gölge nesnelerini (SlotShadow_1..5, RowGroundShadow) serbestçe tutup sürükleyebilir, boyutlandırabilir ve renklendirebilirsiniz. Kod yaptığınız değişiklikleri ASLA ezmez veya sıfırlamaz!", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("KAPALI: Tüm gölgeler aşağıdaki otomatik stil kurallarına göre dinamik hesaplanır.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 2. Hızlı Sahne Seçim Butonları
            EditorGUILayout.LabelField("🎯 Sahnede Doğrudan Seçim & Gizmo ile Taşıma", EditorStyles.boldLabel);
            if (GUILayout.Button("🎯 Tüm Slot Gölgelerini Sahnede Seç", GUILayout.Height(28)))
            {
                List<GameObject> all = rowTarget.GetAllShadowObjects();
                if (all != null && all.Count > 0)
                {
                    Selection.objects = all.ToArray();
                    SceneView.FrameLastActiveSceneView();
                }
            }

            EditorGUILayout.Space(10);

            // 3. Bireysel Slot Gölgeleri (Canlı Kontrol)
            m_ShowIndividualSlots = EditorGUILayout.Foldout(m_ShowIndividualSlots, "🎛️ Bireysel Slot Gölgeleri (Tek Tek Elle Ayarla)", true, EditorStyles.foldoutHeader);
            if (m_ShowIndividualSlots)
            {
                EditorGUI.indentLevel++;
                int slotCount = rowTarget.SlotCount;

                if (slotCount == 0)
                {
                    EditorGUILayout.HelpBox("Sahnede henüz slot bulunamadı. Lütfen slotları oluşturun.", MessageType.Warning);
                }

                for (int i = 0; i < slotCount; i++)
                {
                    GameObject sObj = rowTarget.GetSlotShadowObject(i);
                    if (sObj == null) continue;

                    RectTransform sRect = sObj.GetComponent<RectTransform>();
                    Image sImg = sObj.GetComponent<Image>();

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"🅿️ Slot {i + 1} Gölgesi", EditorStyles.boldLabel);
                    if (GUILayout.Button("🎯 Sahnede Seç", GUILayout.Width(110)))
                    {
                        Selection.activeGameObject = sObj;
                        SceneView.FrameLastActiveSceneView();
                    }
                    EditorGUILayout.EndHorizontal();

                    if (sRect != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector3 pos = sRect.anchoredPosition3D;
                        Vector3 newPos = EditorGUILayout.Vector3Field("Pozisyon (X, Y, Z)", pos);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(sRect, $"Move SlotShadow_{i + 1}");
                            sRect.anchoredPosition3D = newPos;
                            EditorUtility.SetDirty(sRect);
                        }

                        EditorGUI.BeginChangeCheck();
                        Vector2 size = sRect.sizeDelta;
                        Vector2 newSize = EditorGUILayout.Vector2Field("Boyut (Genişlik, Yükseklik)", size);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(sRect, $"Resize SlotShadow_{i + 1}");
                            sRect.sizeDelta = newSize;
                            EditorUtility.SetDirty(sRect);
                        }
                    }

                    if (sImg != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        Color col = EditorGUILayout.ColorField("Gölge Rengi & Opaklık", sImg.color);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(sImg, $"Recolor SlotShadow_{i + 1}");
                            sImg.color = col;
                            EditorUtility.SetDirty(sImg);
                        }
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // 4. Genel Şablon & Toplu Stiller (Global Styles & Presets)
            m_ShowGlobalSettings = EditorGUILayout.Foldout(m_ShowGlobalSettings, "⚙️ Toplu Şablon ve Genel Stil Ayarları (Global)", true, EditorStyles.foldoutHeader);
            if (m_ShowGlobalSettings)
            {
                EditorGUI.indentLevel++;

                // Presets
                EditorGUILayout.LabelField("Hızlı Hazır Ayarlar (Presets)", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Doğal (Natural)"))
                {
                    ApplyPreset(rowTarget,
                        new Color(0.02f, 0.03f, 0.06f, 0.48f), new Vector2(0f, -14f), new Vector2(1.06f, 1.06f), 4f);
                }
                if (GUILayout.Button("Belirgin (Deep)"))
                {
                    ApplyPreset(rowTarget,
                        new Color(0.01f, 0.02f, 0.04f, 0.65f), new Vector2(0f, -18f), new Vector2(1.09f, 1.09f), 5f);
                }
                if (GUILayout.Button("Hafif (Subtle)"))
                {
                    ApplyPreset(rowTarget,
                        new Color(0.03f, 0.04f, 0.08f, 0.32f), new Vector2(0f, -10f), new Vector2(1.04f, 1.04f), 3f);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(8);

                // Bireysel Slot Gölgesi Global
                EditorGUILayout.LabelField("Global Slot Gölgesi Stili", EditorStyles.boldLabel);
                if (m_EnableShadow != null)
                {
                    EditorGUILayout.PropertyField(m_EnableShadow, new GUIContent("Slot Gölgesi Aktif"));
                    if (m_EnableShadow.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        if (m_ShadowSprite != null) EditorGUILayout.PropertyField(m_ShadowSprite, new GUIContent("Gölge Sprite"));
                        if (m_ShadowColor != null) EditorGUILayout.PropertyField(m_ShadowColor, new GUIContent("Gölge Rengi"));
                        if (m_ShadowOffset != null) EditorGUILayout.PropertyField(m_ShadowOffset, new GUIContent("Gölge Ofseti"));
                        if (m_ShadowScale != null) EditorGUILayout.PropertyField(m_ShadowScale, new GUIContent("Gölge Boyut Oranı"));
                        if (m_ShadowZ != null) EditorGUILayout.PropertyField(m_ShadowZ, new GUIContent("Derinlik (Z)"));
                        EditorGUI.indentLevel--;
                    }
                }

                EditorGUILayout.Space(8);



                EditorGUILayout.Space(6);
                if (GUILayout.Button("🔄 Bu Stili Tüm Gölgelere Eşitle / Sıfırla", GUILayout.Height(28)))
                {
                    if (EditorUtility.DisplayDialog("Gölgeleri Stile Eşitle", "Tüm gölgeler yukarıdaki stil değerleriyle yeniden hizalanacak. Devam edilsin mi?", "Evet, Eşitle", "İptal"))
                    {
                        rowTarget.ForceApplyStyleToShadows();
                        EditorUtility.SetDirty(rowTarget);
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // 5. Araç Butonları
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎨 Dokuları Yeniden Üret", GUILayout.Height(30)))
            {
                SlotShadowTextureGenerator.GenerateAllShadowTextures();
                rowTarget.UpdateShadows();
                EditorUtility.SetDirty(rowTarget);
            }
            EditorGUILayout.EndHorizontal();

            if (serializedObject.ApplyModifiedProperties())
            {
                if (!isManual)
                {
                    rowTarget.UpdateShadows();
                }
                EditorUtility.SetDirty(rowTarget);
            }
        }

        private void ApplyPreset(TruckSlotRow targetRow,
            Color shadowCol, Vector2 shadowOff, Vector2 shadowSc, float shadowZ)
        {
            if (m_EnableShadow != null) m_EnableShadow.boolValue = true;
            if (m_ShadowColor != null) m_ShadowColor.colorValue = shadowCol;
            if (m_ShadowOffset != null) m_ShadowOffset.vector2Value = shadowOff;
            if (m_ShadowScale != null) m_ShadowScale.vector2Value = shadowSc;
            if (m_ShadowZ != null) m_ShadowZ.floatValue = shadowZ;

            if (m_EnableRowGroundShadow != null) m_EnableRowGroundShadow.boolValue = false;

            serializedObject.ApplyModifiedProperties();
            targetRow.ForceApplyStyleToShadows();
            EditorUtility.SetDirty(targetRow);
        }
    }
}
