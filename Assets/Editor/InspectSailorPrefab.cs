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
                AssetDatabase.Refresh();
            }
        }
    }
}
