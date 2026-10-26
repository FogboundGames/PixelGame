using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class SetupAsmrAudioMode
    {

        [MenuItem("Tools/PixelGame/💾 ASMR Ayarlarını Sahneye Kaydet", priority = 0)]
        public static void SaveAsmrSettingsToScene()
        {
            var manager = Object.FindFirstObjectByType<HypercasualFeedbackManager>();
            if (manager != null)
            {
                Undo.RecordObject(manager, "Update ASMR Audio Settings");
                manager.AsmrCreativeOnlyPickupSound = true;
                manager.AsmrUsePureProceduralSound = false; // Eski orijinal ses hakim
                manager.AsmrPickupVolume = 0.35f;
                manager.LiftoffSoundDebounceTime = 0.032f;
                EditorUtility.SetDirty(manager);
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                EditorSceneManager.SaveScene(manager.gameObject.scene);
                Debug.Log($"<color=#14D2EB>🎙️ [ASMR Ses Modu]</color> Orijinal ses ayarları sahneye kaydedildi: {manager.gameObject.scene.path}");
            }
        }



        [MenuItem("Tools/PixelGame/🎙️ ASMR Kreatif Ses Modu (Sadece Küp Toplama Sesini Aç/Kapat)", priority = 1)]
        public static void ToggleAsmrCreativeSoundMode()
        {
            var manager = Object.FindFirstObjectByType<HypercasualFeedbackManager>();
            if (manager == null)
            {
                EditorUtility.DisplayDialog("Hata", "Sahnede HypercasualFeedbackManager bulunamadı!", "Tamam");
                return;
            }

            Undo.RecordObject(manager, "Toggle ASMR Creative Audio Mode");
            manager.AsmrCreativeOnlyPickupSound = !manager.AsmrCreativeOnlyPickupSound;
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            string state = manager.AsmrCreativeOnlyPickupSound ? "✅ AÇIK (SADECE Küp Toplama Sesi)" : "❌ KAPALI (Tüm Sesler Normal)";
            string details = manager.AsmrCreativeOnlyPickupSound
                ? "🎙️ ASMR Kreatif Çekim Modu AKTİF EDİLDİ!\n\n" +
                  "• SADECE denizcinin küp toplama (pickup/liftoff) çıtırtı sesi çalacak.\n" +
                  "• Gemi seyir/motor sesi susturuldu.\n" +
                  "• Gemi yanaşma (dock) ve kalkış düdüğü susturuldu.\n" +
                  "• Konteyner darbe sesi susturuldu.\n" +
                  "• UI buton klikleri ve jingle'lar susturuldu.\n" +
                  "• Partikül çıtırtıları susturuldu.\n\n" +
                  "Kreatif çekiminiz için kusursuz ve tertemiz ASMR ses ortamı hazır!"
                : "🔊 ASMR Modu Kapatıldı. Oyundaki tüm sesler (motor, düdük, iskele, UI vb.) tekrar aktif hale geldi.";

            EditorUtility.DisplayDialog($"ASMR Kreatif Ses Modu: {state}", details, "Harika!");
            Debug.Log($"<color=#14D2EB>🎙️ [ASMR Ses Modu]</color> Durum: <b>{state}</b>");
        }

        [MenuItem("Tools/PixelGame/🔔 ASMR Gemiye Yüklenme Tınısını Aç/Kapat", priority = 2)]
        public static void ToggleAsmrIncludeBoardingSound()
        {
            var manager = Object.FindFirstObjectByType<HypercasualFeedbackManager>();
            if (manager == null)
            {
                EditorUtility.DisplayDialog("Hata", "Sahnede HypercasualFeedbackManager bulunamadı!", "Tamam");
                return;
            }

            Undo.RecordObject(manager, "Toggle ASMR Boarding Sound");
            manager.AsmrIncludeBoardingSound = !manager.AsmrIncludeBoardingSound;
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            string state = manager.AsmrIncludeBoardingSound ? "AÇIK (Küp Toplama + Gemiye Biniş Tınısı)" : "KAPALI (SADECE Küp Toplama)";
            EditorUtility.DisplayDialog("ASMR Gemiye Yüklenme Tınısı", $"Gemiye biniş melodik tınısı: {state}", "Tamam");
            Debug.Log($"<color=#14D2EB>🔔 [ASMR Ses Modu]</color> Gemiye Yüklenme Tınısı: <b>{state}</b>");
        }
    }
}
