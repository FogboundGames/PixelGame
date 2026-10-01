using UnityEditor;
using UnityEngine;
using PixelGame;

namespace PixelGame.Editor
{
    [CustomEditor(typeof(LevelManager))]
    public class LevelManagerEditor : UnityEditor.Editor
    {
        private const string ProgressPrefKey = "PixelGame_CurrentLevelIndex";

        public override void OnInspectorGUI()
        {
            LevelManager lm = (LevelManager)target;
            serializedObject.Update();

            // 1. Bölüm Seçici ve Hızlı Geçiş Arayüzü
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.2f, 0.9f, 1f) }
            };
            EditorGUILayout.LabelField("🎮 Hızlı Bölüm Değiştirici", headerStyle);
            EditorGUILayout.Space(4);

            var levels = lm.Levels;
            int count = levels != null ? levels.Count : 0;

            if (count > 0)
            {
                string[] levelNames = new string[count];
                for (int i = 0; i < count; i++)
                {
                    string name = levels[i] != null ? levels[i].LevelName : $"Bölüm {i + 1}";
                    levelNames[i] = $"{i + 1}. {name}";
                }

                int currentIndex = Mathf.Clamp(lm.CurrentLevelIndex, 0, count - 1);
                int selectedIndex = EditorGUILayout.Popup("Seçili Bölüm", currentIndex, levelNames);

                if (selectedIndex != currentIndex)
                {
                    Undo.RecordObject(lm, "Change Current Level Index");
                    lm.LoadLevel(selectedIndex);
                    EditorUtility.SetDirty(lm);
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();

                GUI.enabled = currentIndex > 0;
                if (GUILayout.Button("◀ Önceki Bölüm", GUILayout.Height(28)))
                {
                    Undo.RecordObject(lm, "Previous Level");
                    lm.PreviousLevel();
                    EditorUtility.SetDirty(lm);
                }

                GUI.enabled = true;
                if (GUILayout.Button("🔄 Seçiliyi Sahneye Yükle", GUILayout.Height(28)))
                {
                    Undo.RecordObject(lm, "Load Selected Level");
                    lm.LoadSelectedLevel();
                    EditorUtility.SetDirty(lm);
                }

                GUI.enabled = currentIndex < count - 1;
                if (GUILayout.Button("Sonraki Bölüm ▶", GUILayout.Height(28)))
                {
                    Undo.RecordObject(lm, "Next Level");
                    lm.NextLevel();
                    EditorUtility.SetDirty(lm);
                }

                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Level listesi boş veya LevelSequence atanmamış!", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();

            // 2. Kayıtlı İlerleme (PlayerPrefs) Durumu
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            bool hasSavedProgress = PlayerPrefs.HasKey(ProgressPrefKey);
            int savedIndex = hasSavedProgress ? PlayerPrefs.GetInt(ProgressPrefKey) : -1;
            string savedName = (hasSavedProgress && levels != null && savedIndex >= 0 && savedIndex < levels.Count && levels[savedIndex] != null)
                ? $"{savedIndex + 1}. {levels[savedIndex].LevelName}"
                : (hasSavedProgress ? $"Bölüm {savedIndex + 1}" : "Yok (Kayıt Bulunmuyor)");

            EditorGUILayout.LabelField("💾 Kayıtlı İlerleme Durumu", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Cihazdaki Kayıt (PlayerPrefs): {savedName}");

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("🗑️ Kayıtlı İlerlemeyi Sıfırla (Bölüm 1'e Dön)", GUILayout.Height(24)))
            {
                lm.ResetProgress();
                EditorUtility.DisplayDialog("İlerleme Sıfırlandı", "Kayıtlı bölüm hafızası silindi. Artık 1. bölümden başlayacak.", "Tamam");
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("⚙️ Detaylı Bileşen Ayarları", EditorStyles.boldLabel);

            // Standart Inspector alanları
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("PixelGame/🎮 Bölüm Yönetimi/🗑️ Kayıtlı İlerlemeyi Sıfırla (Reset PlayerPrefs)")]
        public static void ResetPlayerPrefsProgress()
        {
            PlayerPrefs.DeleteKey(ProgressPrefKey);
            PlayerPrefs.Save();
            var lm = Object.FindFirstObjectByType<LevelManager>();
            if (lm != null)
            {
                lm.ResetProgress();
            }
            Debug.Log("<color=#00FFAA><b>[LevelManager]</b></color> Kayıtlı bölüm sıfırlandı.");
        }
    }
}
