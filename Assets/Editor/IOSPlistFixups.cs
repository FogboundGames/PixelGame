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
    /// - NSUserTrackingUsageDescription oyunun kendi metniyle yazılır: Meta SDK için ATT izni isteniyor (LandFlowMeta);
    ///   GameAnalytics'in varsayılan metninin yerine geçer. App Store Connect'te "tracking" beyanı buna uygun olmalı.
    /// - SKAdNetworkItems'a Meta'nın kimlikleri eklenir (ATT reddedilse de reklam atfı için).
    /// - FIREBASE_ANALYTICS_COLLECTION_ENABLED = NO: Firebase oyuncu izin verene kadar veri toplamaz (LandFlowFirebase).
    /// - ITSAppUsesNonExemptEncryption = NO: yalnızca standart HTTPS kullanılıyor; her yüklemede
    ///   App Store Connect'te şifreleme sorusu çıkmasın.
    /// </summary>
    public static class IOSPlistFixups
    {
        private const string TrackingUsageDescription = "This identifier is used to measure and improve the ads that bring new players to Land Flow.";
        private static readonly string[] s_MetaSkAdNetworkIds = { "v9wttpbfk9.skadnetwork", "n38lu8286q.skadnetwork" };

        [PostProcessBuild(9999)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            if (!File.Exists(plistPath)) return;

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            PlistElementDict root = plist.root;

            root.SetString("NSUserTrackingUsageDescription", TrackingUsageDescription);
            AddSkAdNetworkIds(root);

            root.SetBoolean("FIREBASE_ANALYTICS_COLLECTION_ENABLED", false);
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

            plist.WriteToFile(plistPath);
        }

        private static void AddSkAdNetworkIds(PlistElementDict root)
        {
            PlistElementArray items = root.values.TryGetValue("SKAdNetworkItems", out PlistElement existing)
                ? existing.AsArray()
                : root.CreateArray("SKAdNetworkItems");

            foreach (string id in s_MetaSkAdNetworkIds)
            {
                bool present = items.values.Exists(e => e.AsDict()?["SKAdNetworkIdentifier"]?.AsString() == id);
                if (!present) items.AddDict().SetString("SKAdNetworkIdentifier", id);
            }
        }
    }
}
#endif
