using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// GitHub Actions iOS build girişi (.github/workflows/ios-testflight.yml).
    /// Linux'ta yalnızca Xcode projesini çıkarır; derleme, imza ve TestFlight yüklemesi macOS runner'da yapılır.
    /// Bundle ID / Team ID / profil / sürüm komut satırından gelir, ProjectSettings'e kalıcı yazılmaz (CI kopyasında değişir).
    /// </summary>
    public static class IOSBuild
    {
        public static void Build()
        {
            string outputPath = Arg("-customBuildPath") ?? "build/iOS/LandFlow";
            string bundleId = Arg("-iosBundleId");
            string teamId = Arg("-iosTeamId");
            string version = Arg("-iosVersion");
            string buildNumber = Arg("-iosBuildNumber");
            string profileId = Arg("-iosProfileId");

            if (string.IsNullOrEmpty(bundleId) || string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(profileId))
            {
                Debug.LogError("[IOSBuild] -iosBundleId, -iosTeamId ve -iosProfileId zorunlu.");
                EditorApplication.Exit(1);
                return;
            }

            // Manuel imza: profil yalnız Unity-iPhone hedefine yazılır (UnityFramework profilsiz kalmalı).
            // Otomatik imza CI'da her koşuda yeni sertifika üretip Apple'ın sertifika sınırını doldurur.
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, bundleId);
            PlayerSettings.iOS.appleDeveloperTeamID = teamId;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.iOS.iOSManualProvisioningProfileID = profileId;
            PlayerSettings.iOS.iOSManualProvisioningProfileType = ProvisioningProfileType.Distribution;
            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;
            if (!string.IsNullOrEmpty(buildNumber)) PlayerSettings.iOS.buildNumber = buildNumber;

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Debug.Log($"[IOSBuild] {bundleId} {PlayerSettings.bundleVersion} ({PlayerSettings.iOS.buildNumber}) → {outputPath}, sahneler: {string.Join(", ", scenes)}");

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            });

            Debug.Log($"[IOSBuild] Sonuç: {report.summary.result}, hata: {report.summary.totalErrors}");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
