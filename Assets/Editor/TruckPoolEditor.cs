using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// TruckPool için zengin, görsel ve kullanımı son derece kolay Custom Editor.
    /// Oyun başlatılmadan da havuz karolarını (2. görseldeki 3B tombul sarı rozetleri)
    /// sahnede canlı görmeyi ve anlık olarak düzenlemeyi sağlar.
    /// </summary>
    [CustomEditor(typeof(TruckPool))]
    public class TruckPoolEditor : UnityEditor.Editor
    {
        private TruckPool m_Pool;

        private void OnEnable()
        {
            m_Pool = (TruckPool)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(6);
            DrawHeaderBanner();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("👁️ Canlı Editör Önizleme Ayarları", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            SerializedProperty previewProp = serializedObject.FindProperty("m_PreviewInEditor");
            EditorGUILayout.PropertyField(previewProp, new GUIContent("Canlı Önizleme Aktif", "Oyun kapalıyken de sahnede havuz karolarını gösterir."));

            if (previewProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_PreviewCapacity"), new GUIContent("Önizleme Sayısı", "Rozet üzerindeki sayı (örn: 16)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_PreviewColor"), new GUIContent("Önizleme Rengi", "Karo rengi (örn: Altın Sarısı)"));
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🔄 Canlı Önizlemeyi Tazele", GUILayout.Height(28)))
                {
                    m_Pool.RefreshEditorPreview();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("🎯 Aktif Bölümden Bilgileri Al", GUILayout.Height(28)))
                {
                    SyncWithActiveLevel();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("📊 Havuz Izgara Boyutları", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUI.BeginChangeCheck();
            SerializedProperty colProp = serializedObject.FindProperty("m_Columns");
            SerializedProperty rowProp = serializedObject.FindProperty("m_Rows");
            
            EditorGUILayout.PropertyField(colProp, new GUIContent("Sütun Sayısı (Columns)", "Yatayda kaç vagon olacağı"));
            EditorGUILayout.PropertyField(rowProp, new GUIContent("Satır Sayısı (Rows)", "Dikeyde kaç sıra olacağı"));

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                m_Pool.RebuildPlaces(colProp.intValue, rowProp.intValue);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("🏗️ Havuz Slotlarını Baştan Kur (Rebuild)", GUILayout.Height(24)))
            {
                m_Pool.RebuildPlaces(m_Pool.Columns, m_Pool.Rows);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🌑 Havuz Sahte Gölgeleri (Pool Fake Shadows)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            SerializedProperty enableShadowsProp = serializedObject.FindProperty("m_EnableShadows");
            EditorGUILayout.PropertyField(enableShadowsProp, new GUIContent("Slot Gölgeleri Aktif", "Her havuz karosunun altına 3B temas gölgesi ekler."));
            if (enableShadowsProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ShadowSprite"), new GUIContent("Gölge Görseli"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ShadowColor"), new GUIContent("Gölge Rengi & Opaklığı"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ShadowOffset"), new GUIContent("Gölge Ofseti (X, Y)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ShadowScale"), new GUIContent("Gölge Boyut Çarpanı"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ShadowZ"), new GUIContent("Gölge Z Derinliği"));
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(4);
                if (GUILayout.Button("🌑 Gölgeleri Güncelle", GUILayout.Height(24)))
                {
                    serializedObject.ApplyModifiedProperties();
                    m_Pool.UpdateShadows();
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🎨 Stil & Yerleşim Ayarları (Style)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            SerializedProperty styleProp = serializedObject.FindProperty("m_Style");
            EditorGUILayout.PropertyField(styleProp, true);
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying && m_Pool.PreviewInEditor)
            {
                m_Pool.RefreshEditorPreview();
            }
        }

        private void SyncWithActiveLevel()
        {
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            PixelLevelData lvl = gen != null ? gen.ActiveLevelData : null;
            if (lvl == null)
            {
                LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
                if (lm != null) lvl = lm.CurrentLevel;
            }

            if (lvl != null)
            {
                m_Pool.RebuildPlaces(Mathf.Max(1, lvl.PoolColumns), Mathf.Max(1, lvl.PoolRows));
                m_Pool.PreviewCapacity = lvl.TruckCapacity;
                if (lvl.ColorPalette != null && lvl.ColorPalette.Count > 0)
                {
                    m_Pool.PreviewColor = lvl.ColorPalette[0].targetColor;
                }
                EditorUtility.SetDirty(m_Pool);
                m_Pool.RefreshEditorPreview();
                SceneView.RepaintAll();
                Debug.Log($"<color=#00FFAA><b>[TruckPool]</b></color> '{lvl.LevelName}' bölümünden havuz ayarları ({lvl.PoolColumns}x{lvl.PoolRows}, Kapasite: {lvl.TruckCapacity}) başarıyla çekildi!");
            }
            else
            {
                Debug.LogWarning("[TruckPool] Sahnede aktif bir PixelLevelData bulunamadı.");
            }
        }

        private void DrawHeaderBanner()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 38f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.16f, 0.28f, 1f));

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.25f, 1f) }
            };

            GUI.Label(rect, "🅿️ TRUCK POOL — CANLI HAVUZ VE KARO DÜZENLEYİCİ", titleStyle);
        }
    }
}
