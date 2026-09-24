using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class InspectSceneTexts
    {
        static InspectSceneTexts()
        {
            // Otomatik tetikleme kapatıldı: scene_texts.txt editör açılışında yazılmasın.
            // Gerekirse Tools menüsünden elle çalıştırılır.
            // EditorApplication.delayCall += Inspect;
        }

        // [MenuItem("Tools/PixelGame/Inspect All Scene Texts")]
        public static void Inspect()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== ALL TEXT OBJECTS IN SCENE ===");

            foreach (Text t in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sb.AppendLine($"[UI.Text] GameObject: '{t.gameObject.name}', Path: '{GetPath(t.transform)}', Text: '{t.text}', Active: {t.gameObject.activeInHierarchy}, Pos: {t.transform.position}");
            }

            foreach (TMPro.TextMeshProUGUI tmp in Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sb.AppendLine($"[TMP_UGUI] GameObject: '{tmp.gameObject.name}', Path: '{GetPath(tmp.transform)}', Text: '{tmp.text}', Active: {tmp.gameObject.activeInHierarchy}, Pos: {tmp.transform.position}");
            }

            foreach (TextMesh tm in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sb.AppendLine($"[3D TextMesh] GameObject: '{tm.gameObject.name}', Path: '{GetPath(tm.transform)}', Text: '{tm.text}', Active: {tm.gameObject.activeInHierarchy}, Pos: {tm.transform.position}");
            }

            Debug.Log(sb.ToString());
            System.IO.File.WriteAllText("Assets/Editor/scene_texts.txt", sb.ToString());
        }

        private static string GetPath(Transform t)
        {
            if (t == null) return "";
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
