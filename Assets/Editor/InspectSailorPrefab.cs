using System.IO;
using UnityEngine;
using UnityEditor;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class InspectSailorPrefab
    {
        static InspectSailorPrefab()
        {
            EditorApplication.update -= CheckFlag;
            EditorApplication.update += CheckFlag;
        }

        private static void CheckFlag()
        {
            if (File.Exists("scratch/inspect_sailor.flag"))
            {
                try { File.Delete("scratch/inspect_sailor.flag"); } catch {}
                Run();
            }
        }

        [MenuItem("Tools/PixelGame/Inspect Sailor Prefab")]
        public static void Run()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Sailor_Runner.prefab");
            if (prefab == null)
            {
                File.WriteAllText("scratch/sailor_inspection.txt", "Sailor_Runner.prefab not found!");
                return;
            }

            using (var sw = new StreamWriter("scratch/sailor_inspection.txt"))
            {
                sw.WriteLine($"=== Inspecting Sailor Prefab: {prefab.name} ===");
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                sw.WriteLine($"Total Renderers found: {renderers.Length}");

                foreach (var r in renderers)
                {
                    sw.WriteLine($"Renderer: '{r.gameObject.name}', Type: {r.GetType().Name}, Path: {GetPath(r.transform)}");
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        Material m = mats[i];
                        string matName = m != null ? m.name : "NULL";
                        Color col = Color.magenta;
                        if (m != null)
                        {
                            if (m.HasProperty("_BaseColor")) col = m.GetColor("_BaseColor");
                            else if (m.HasProperty("_Color")) col = m.GetColor("_Color");
                        }
                        sw.WriteLine($"   Mat [{i}]: {matName}, Color: {col}");
                    }
                }
            }
            Debug.Log("Inspection completed and written to scratch/sailor_inspection.txt");
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
