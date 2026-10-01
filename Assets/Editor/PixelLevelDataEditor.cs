using UnityEditor;
using UnityEngine;
using PixelGame;

namespace PixelGame.Editor
{
    [CustomEditor(typeof(PixelLevelData))]
    public class PixelLevelDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            PixelLevelData level = (PixelLevelData)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.45f);
            if (GUILayout.Button($"▶️ Bu Seviyeyi Sahneye Yükle ('{level.LevelName}')", GUILayout.Height(36)))
            {
                PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
                if (gen == null)
                {
                    EditorUtility.DisplayDialog("Uyarı", "Sahnede [PixelArtGenerator] objesi bulunamadı. Lütfen önce Gemi sahnesini açın.", "Tamam");
                    return;
                }

                gen.LoadLevel(level);

                LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
                if (lm != null)
                {
                    lm.SyncActiveLevel(level);
                }

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                SceneView.RepaintAll();
                Debug.Log($"<color=#00FFAA><b>[PixelGame]</b></color> '{level.LevelName}' sahneye yüklendi ve aktif seviye olarak ayarlandı!");
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);

            DrawDefaultInspector();
        }
    }
}
