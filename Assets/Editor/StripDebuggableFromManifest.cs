#if UNITY_EDITOR
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class StripDebuggableFromManifest : IPreprocessBuildWithReport
{
    public int callbackOrder => 100;

    public void OnPreprocessBuild(BuildReport report)
    {
        string manifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        if (File.Exists(manifestPath))
        {
            string content = File.ReadAllText(manifestPath);
            if (content.Contains("android:debuggable=\"true\""))
            {
                content = content.Replace("android:debuggable=\"true\"", "");
                File.WriteAllText(manifestPath, content);
                Debug.Log("[Build] AndroidManifest.xml içindeki 'android:debuggable=\"true\"' Google Play için otomatik temizlendi.");
            }
        }
    }
}
#endif
