#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// iOS build sonrası Info.plist düzeltmeleri (GameAnalytics'in post-process'inden SONRA çalışır):
    /// - NSUserTrackingUsageDescription kaldırılır: oyun takip/ATT izni istemiyor; GameAnalytics bu anahtarı
    ///   varsayılan olarak eklediği için App Store "tracking" beyanıyla çelişip gönderimi engelliyordu.
    /// - ITSAppUsesNonExemptEncryption = NO: yalnızca standart HTTPS kullanılıyor; her yüklemede
    ///   App Store Connect'te şifreleme sorusu çıkmasın.
    /// </summary>
    public static class IOSPlistFixups
    {
        [PostProcessBuild(9999)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            if (!File.Exists(plistPath)) return;

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            PlistElementDict root = plist.root;

            if (root.values.ContainsKey("NSUserTrackingUsageDescription"))
            {
                root.values.Remove("NSUserTrackingUsageDescription");
                Debug.Log("[IOSPlistFixups] NSUserTrackingUsageDescription kaldırıldı (oyun takip izni istemiyor).");
            }

            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

            plist.WriteToFile(plistPath);
        }
    }
}
#endif
