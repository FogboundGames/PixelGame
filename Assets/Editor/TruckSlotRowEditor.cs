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

        private SerializedProperty m_EnablePortalShadow;
        private SerializedProperty m_PortalShadowSprite;
        private SerializedProperty m_PortalShadowColor;
        private SerializedProperty m_PortalShadowOffset;
        private SerializedProperty m_PortalShadowScale;
        private SerializedProperty m_PortalShadowZ;

        private bool m_ShowPortals = true;
        private bool m_ShowRowGround = true;
        private bool m_ShowIndividualSlots = false;
        private bool m_ShowGlobalSettings = true;

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

                m_EnablePortalShadow = m_Style.FindPropertyRelative("enablePortalShadow");
                m_PortalShadowSprite = m_Style.FindPropertyRelative("portalShadowSprite");
                m_PortalShadowColor = m_Style.FindPropertyRelative("portalShadowColor");
                m_PortalShadowOffset = m_Style.FindPropertyRelative("portalShadowOffset");
                m_PortalShadowScale = m_Style.FindPropertyRelative("portalShadowScale");
                m_PortalShadowZ = m_Style.FindPropertyRelative("portalShadowZ");
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            TruckSlotRow rowTarget = (TruckSlotRow)target;

            EditorGUILayout.Space(6);

            // 1. Ana Slot Gölgesi Aksiyon Butonu
            GUI.backgroundColor = new Color(0.2f, 0.8f, 1f, 1f);
            if (GUILayout.Button("🌑 Slot Fake Shadow'larını Kur / Yeniden Hesapla", GUILayout.Height(36)))
            {
                if (m_EnableShadow != null) m_EnableShadow.boolValue = true;
                if (m_ShadowSprite != null && m_ShadowSprite.objectReferenceValue == null)
                {
                    m_ShadowSprite.objectReferenceValue = SlotShadowTextureGenerator.GetOrGenerateSlotShadowSprite();
                }
                serializedObject.ApplyModifiedProperties();
                rowTarget.ForceApplyStyleToShadows();
                EditorUtility.SetDirty(rowTarget);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);

            // 2. Elle Ayarlama Modu Başlığı & Açıklaması
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
                EditorGUILayout.HelpBox("AÇIK: Sahnede gölge nesnelerini (SlotShadow_1..5, RowGroundShadow, Portallar) serbestçe tutup sürükleyebilir, boyutlandırabilir ve renklendirebilirsiniz. Kod yaptığınız değişiklikleri ASLA ezmez!", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("KAPALI: Tüm gölgeler aşağıdaki otomatik stil kurallarına göre dinamik hesaplanır.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 3. Hızlı Sahne Seçim Butonları
            EditorGUILayout.LabelField("🎯 Sahnede Doğrudan Seçim & Gizmo ile Taşıma", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎯 Tüm Slot Gölgelerini Seç (1..5)", GUILayout.Height(28)))
            {
                var slotShadows = new List<GameObject>();
                for (int i = 0; i < rowTarget.SlotCount; i++)
                {
                    GameObject s = rowTarget.GetSlotShadowObject(i);
                    if (s != null) slotShadows.Add(s);
                }
                if (slotShadows.Count > 0)
                {
                    Selection.objects = slotShadows.ToArray();
                    SceneView.FrameLastActiveSceneView();
                }
            }
            if (GUILayout.Button("🎯 Tüm Gölgeler (Portallar + Ray + Slotlar)", GUILayout.Height(28)))
            {
                List<GameObject> all = rowTarget.GetAllShadowObjects();
                if (all != null && all.Count > 0)
                {
                    Selection.objects = all.ToArray();
                    SceneView.FrameLastActiveSceneView();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < Mathf.Min(5, rowTarget.SlotCount); i++)
            {
                int slotIdx = i;
                if (GUILayout.Button($"Slot {slotIdx + 1}", GUILayout.Height(22)))
                {
                    GameObject s = rowTarget.GetSlotShadowObject(slotIdx);
                    if (s != null) { Selection.activeGameObject = s; SceneView.FrameLastActiveSceneView(); }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 3. Maden Portalı Gölgeleri (Portal Shadows)
            m_ShowPortals = EditorGUILayout.Foldout(m_ShowPortals, "⛏️ Maden Portalı Gölgeleri (Portal Shadows)", true, EditorStyles.foldoutHeader);
            if (m_ShowPortals)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (m_EnablePortalShadow != null)
                {
                    EditorGUILayout.PropertyField(m_EnablePortalShadow, new GUIContent("Portal Gölgeleri Aktif"));
                    if (m_EnablePortalShadow.boolValue)
                    {
                        if (m_PortalShadowSprite != null) EditorGUILayout.PropertyField(m_PortalShadowSprite, new GUIContent("Portal Gölge Sprite"));
                        if (m_PortalShadowColor != null) EditorGUILayout.PropertyField(m_PortalShadowColor, new GUIContent("Gölge Rengi & Opaklık"));
                        if (m_PortalShadowOffset != null) EditorGUILayout.PropertyField(m_PortalShadowOffset, new GUIContent("Gölge Ofseti (X, Y)"));
                        if (m_PortalShadowScale != null) EditorGUILayout.PropertyField(m_PortalShadowScale, new GUIContent("Boyut Oranı (X, Y)"));
                        if (m_PortalShadowZ != null) EditorGUILayout.PropertyField(m_PortalShadowZ, new GUIContent("Z Derinliği"));
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);

            // 4. Ray Şeridi Zemin Gölgesi (Row Ground Shadow)
            m_ShowRowGround = EditorGUILayout.Foldout(m_ShowRowGround, "🛤️ Ray Şeridi Zemin Gölgesi (Row Ground Shadow)", true, EditorStyles.foldoutHeader);
            if (m_ShowRowGround)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (m_EnableRowGroundShadow != null)
                {
                    EditorGUILayout.PropertyField(m_EnableRowGroundShadow, new GUIContent("Ray Zemin Gölgesi Aktif"));
                    if (m_EnableRowGroundShadow.boolValue)
                    {
                        if (m_RowGroundShadowSprite != null) EditorGUILayout.PropertyField(m_RowGroundShadowSprite, new GUIContent("Şerit Gölge Sprite"));
                        if (m_RowGroundShadowColor != null) EditorGUILayout.PropertyField(m_RowGroundShadowColor, new GUIContent("Gölge Rengi & Opaklık"));
                        if (m_RowGroundShadowOffset != null) EditorGUILayout.PropertyField(m_RowGroundShadowOffset, new GUIContent("Gölge Ofseti (X, Y)"));
                        if (m_RowGroundShadowPadding != null) EditorGUILayout.PropertyField(m_RowGroundShadowPadding, new GUIContent("Genişlik & Yükseklik Payı"));
                        if (m_RowGroundShadowZ != null) EditorGUILayout.PropertyField(m_RowGroundShadowZ, new GUIContent("Z Derinliği"));
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);

            // 5. Bireysel Slot Gölgeleri (Canlı Kontrol)
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

            EditorGUILayout.Space(6);

            // 6. Genel Şablon & Hazır Ayarlar (Global Presets)
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
                        slotCol: new Color(0.04f, 0.06f, 0.14f, 0.58f), slotOff: new Vector2(0f, -14f), slotSc: new Vector2(1.04f, 1.04f), slotZ: 4f,
                        rowCol: new Color(0.02f, 0.03f, 0.05f, 0.42f), rowOff: new Vector2(0f, -16f), rowPad: new Vector2(320f, 60f), rowZ: 8f,
                        portalCol: new Color(0.02f, 0.03f, 0.06f, 0.52f), portalOff: new Vector2(0f, -14f), portalSc: new Vector2(1.12f, 1.12f), portalZ: 4f);
                }
                if (GUILayout.Button("Belirgin (Deep)"))
                {
                    ApplyPreset(rowTarget,
                        slotCol: new Color(0.02f, 0.03f, 0.08f, 0.78f), slotOff: new Vector2(0f, -18f), slotSc: new Vector2(1.08f, 1.08f), slotZ: 4f,
                        rowCol: new Color(0.01f, 0.02f, 0.04f, 0.58f), rowOff: new Vector2(0f, -18f), rowPad: new Vector2(360f, 75f), rowZ: 8f,
                        portalCol: new Color(0.01f, 0.02f, 0.04f, 0.68f), portalOff: new Vector2(0f, -16f), portalSc: new Vector2(1.16f, 1.16f), portalZ: 5f);
                }
                if (GUILayout.Button("Yumuşak (Soft)"))
                {
                    ApplyPreset(rowTarget,
                        slotCol: new Color(0.05f, 0.08f, 0.16f, 0.38f), slotOff: new Vector2(0f, -10f), slotSc: new Vector2(1.02f, 1.02f), slotZ: 4f,
                        rowCol: new Color(0.03f, 0.04f, 0.08f, 0.28f), rowOff: new Vector2(0f, -12f), rowPad: new Vector2(280f, 50f), rowZ: 8f,
                        portalCol: new Color(0.03f, 0.04f, 0.08f, 0.35f), portalOff: new Vector2(0f, -10f), portalSc: new Vector2(1.08f, 1.08f), portalZ: 3f);
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

                EditorGUILayout.Space(6);
                if (GUILayout.Button("🔄 Bu Stili Tüm Gölgelere Eşitle / Otomatik Uygula", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Gölgeleri Stile Eşitle", "Tüm gölgeler (Ray Zemin, Maden Portalları ve Slotlar) yukarıdaki stil değerleriyle yeniden hizalanacak. Devam edilsin mi?", "Evet, Eşitle", "İptal"))
                    {
                        rowTarget.ForceApplyStyleToShadows();
                        EditorUtility.SetDirty(rowTarget);
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // 7. Araç Butonları
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎨 Tüm Dokuları Yeniden Üret (Portal + Ray + Slot)", GUILayout.Height(32)))
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
            Color slotCol, Vector2 slotOff, Vector2 slotSc, float slotZ,
            Color rowCol, Vector2 rowOff, Vector2 rowPad, float rowZ,
            Color portalCol, Vector2 portalOff, Vector2 portalSc, float portalZ)
        {
            if (m_EnableShadow != null) m_EnableShadow.boolValue = true;
            if (m_ShadowColor != null) m_ShadowColor.colorValue = slotCol;
            if (m_ShadowOffset != null) m_ShadowOffset.vector2Value = slotOff;
            if (m_ShadowScale != null) m_ShadowScale.vector2Value = slotSc;
            if (m_ShadowZ != null) m_ShadowZ.floatValue = slotZ;

            if (m_EnableRowGroundShadow != null) m_EnableRowGroundShadow.boolValue = true;
            if (m_RowGroundShadowColor != null) m_RowGroundShadowColor.colorValue = rowCol;
            if (m_RowGroundShadowOffset != null) m_RowGroundShadowOffset.vector2Value = rowOff;
            if (m_RowGroundShadowPadding != null) m_RowGroundShadowPadding.vector2Value = rowPad;
            if (m_RowGroundShadowZ != null) m_RowGroundShadowZ.floatValue = rowZ;

            if (m_EnablePortalShadow != null) m_EnablePortalShadow.boolValue = true;
            if (m_PortalShadowColor != null) m_PortalShadowColor.colorValue = portalCol;
            if (m_PortalShadowOffset != null) m_PortalShadowOffset.vector2Value = portalOff;
            if (m_PortalShadowScale != null) m_PortalShadowScale.vector2Value = portalSc;
            if (m_PortalShadowZ != null) m_PortalShadowZ.floatValue = portalZ;

            serializedObject.ApplyModifiedProperties();
            targetRow.ForceApplyStyleToShadows();
            EditorUtility.SetDirty(targetRow);
        }
    }
}
