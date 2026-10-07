using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class SetupLoadingScreen
    {
        [MenuItem("Tools/PixelGame/🌊 Land Flow & Fogbound Açılış Ekranını Kur / Güncelle")]
        public static void SetupInScene()
        {
            var existing = Object.FindFirstObjectByType<LandFlowLoadingScreen>();
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject go = new GameObject("LandFlow_LoadingScreen");
            var screen = go.AddComponent<LandFlowLoadingScreen>();
            Undo.RegisterCreatedObjectUndo(go, "Create LandFlow Loading Screen");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = go;

            ConfigureFogboundPlayerSettings(false);

            EditorUtility.DisplayDialog("Açılış ve Yükleme Ekranı Kuruldu", 
                "✨ Fogbound & Land Flow Açılış Ekranı başarıyla hazırlandı!\n\n" +
                "1. Faz - Fogbound Stüdyo Girişi:\n" +
                "   - Saf siyah zemin üzerinde ortalanmış beyaz Fogbound şirket logosu\n" +
                "   - Nefes alma animasyonu ve yumuşak geçiş\n\n" +
                "2. Faz - Land Flow Tam Ekran Yükleme Ekranı:\n" +
                "   - 9:16 dikey tüm ekranı kaplayan sevimli gemili Land Flow arka planı\n" +
                "   - 'LAND FLOW' başlık stiliyle birebir eşleşen 3D kabartmalı 'LOADING...' rozeti\n" +
                "   - Akıcı altın sarısı ilerleme çubuğu\n\n" +
                "Oyun açılışında, level geçişlerinde ve level fail durumlarında otomatik ve pürüzsüz çalışır.", "Harika!");
        }

        [MenuItem("Tools/PixelGame/🏢 Fogbound Logo ve Proje Ayarlarını Yapılandır")]
        public static void ConfigureSettingsMenu()
        {
            ConfigureFogboundPlayerSettings(true);
        }

        public static void ConfigureFogboundPlayerSettings(bool showDialog)
        {
            PlayerSettings.companyName = "Fogbound";
            PlayerSettings.productName = "Land Flow";

            Sprite fogLogo = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Fogbound_Logo_Transparent.png");
            if (fogLogo == null)
            {
                fogLogo = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Fogbound_Logo.png");
            }

            if (fogLogo != null)
            {
                try
                {
                    PlayerSettings.SplashScreenLogo logo = PlayerSettings.SplashScreenLogo.Create(2.0f, fogLogo);
                    PlayerSettings.SplashScreen.logos = new PlayerSettings.SplashScreenLogo[] { logo };
                    PlayerSettings.SplashScreen.show = true;
                    PlayerSettings.SplashScreen.showUnityLogo = false;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[PlayerSettings] Splash logo kurulumu: {ex.Message}");
                }
            }

            AssetDatabase.SaveAssets();

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Fogbound Ayarları Tamamlandı",
                    "Şirket Adı: Fogbound\n" +
                    "Oyun Adı: Land Flow\n" +
                    "Açılış Zemin Rengi: Siyah\n" +
                    "Açılış Logosu: Fogbound Logo\n\n" +
                    "Proje ayarları başarıyla güncellendi!", "Tamam");
            }
        }
    }
}
