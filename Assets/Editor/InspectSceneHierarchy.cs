using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Text;
using System.IO;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class InspectSceneHierarchy
    {
        static InspectSceneHierarchy()
        {
            EditorApplication.delayCall += RunDump;
        }

        private static void RunDump()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Dump();
        }

        [MenuItem("PixelGame/🔍 Dump Scene Hierarchy to File")]
        public static void Dump()
        {
            var scene = EditorSceneManager.GetActiveScene();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[Scene Hierarchy: {scene.name} at {scene.path}]");
            foreach (var root in scene.GetRootGameObjects())
            {
                PrintHierarchy(root.transform, 0, sb);
            }
            File.WriteAllText("Assets/Editor/scene_hierarchy_dump.txt", sb.ToString());
            Debug.Log("<color=#00FFAA>[Scene Dump]</color> Hierarchy saved to Assets/Editor/scene_hierarchy_dump.txt");
        }

        private static void PrintHierarchy(Transform tr, int depth, StringBuilder sb)
        {
            sb.Append(new string(' ', depth * 2));
            sb.Append($"- {tr.name} (active: {tr.gameObject.activeSelf}, pos: {tr.localPosition}, rot: {tr.localEulerAngles}, scale: {tr.localScale})");
            var comps = tr.GetComponents<Component>();
            sb.Append(" [");
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] != null && !(comps[i] is Transform))
                {
                    sb.Append(comps[i].GetType().Name);
                    if (i < comps.Length - 1) sb.Append(", ");
                }
            }
            sb.AppendLine("]");

            for (int i = 0; i < tr.childCount; i++)
            {
                PrintHierarchy(tr.GetChild(i), depth + 1, sb);
            }
        }
    }
}
