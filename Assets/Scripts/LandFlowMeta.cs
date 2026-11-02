using System.Collections;
using System.Collections.Generic;
using Facebook.Unity;
using Facebook.Unity.Settings;
using GameAnalyticsSDK;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Meta (Facebook) SDK başlatıcısı: uygulama olayları ve reklam atfı için. Ayar dosyasında App ID veya Client Token
    /// yoksa (Facebook → Edit Settings) hiçbir şey göndermez; böylece değerler girilmeden de oyun sorunsuz çalışır.
    /// Oyuncu veri izni vermeden başlatılmaz (LandFlowPrivacy). iOS'ta ardından ATT izni sorulur, SDK cevaptan sonra başlatılır.
    /// Ayar dosyasında otomatik olay ve reklam kimliği kapalıdır; yalnızca izin varsa burada açılır.
    /// Reklam tarafı için Level 5, 10 ve 20 bitince, oyuncu başına birer kez "level achieved" olayı gönderir.
    /// LevelComplete, LevelFail, LevelRestart ve LevelQuit özel olaylarını da level kimliği ve geçen saniyeyle gönderir.
    /// </summary>
    public sealed class LandFlowMeta : MonoBehaviour, IGameAnalyticsATTListener
    {
        private const string MilestonePrefPrefix = "LandFlowMeta_LevelAchieved_";
        private static readonly int[] LevelMilestones = { 5, 10, 20 };

        private static bool s_Initialized;
        // Editörde ve masaüstünde SDK hiç başlatılmaz; FB API'sine yalnızca bu bayrak açıkken dokunulur
        private static bool s_SdkReady;

        // ATT cevabı ana thread dışında gelebilir; bayraklar Start içinde ana thread'de okunur
        private volatile bool m_AttAnswered;
        private volatile bool m_AttAuthorized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_Initialized) return;
            s_Initialized = true;

#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)
            // Meta SDK'yı yalnızca mobil cihazda kullanıyoruz. Windows/Mac build'de SDK'nın masaüstü (FBG) native
            // katmanı oyunu açılışta çökertebiliyor. Editörde de SDK Windows katmanını arıyor; bu eklentiler kapalı
            // olduğu için FB.Init her karede NullReferenceException üretiyordu. Bu ortamlarda hiç başlatma.
#else

            if (!FacebookSettings.IsValidAppId || string.IsNullOrEmpty(FacebookSettings.ClientToken))
            {
                Debug.LogWarning("[LandFlowMeta] Meta App ID veya Client Token yok (Facebook → Edit Settings); SDK başlatılmayacak.");
                return;
            }

            var go = new GameObject("LandFlowMeta");
            DontDestroyOnLoad(go);
            go.AddComponent<LandFlowMeta>();
#endif
        }

        private void Awake() => LandFlowPrivacy.ConsentChanged += ApplyConsent;
        private void OnDestroy() => LandFlowPrivacy.ConsentChanged -= ApplyConsent;

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => LandFlowPrivacy.Allowed);
#if UNITY_IOS && !UNITY_EDITOR
            // Uygulama aktif değilken yapılan ATT isteği pencere göstermeden düşer
            yield return new WaitUntil(() => Application.isFocused);
            GameAnalytics.RequestTrackingAuthorization(this);
            yield return new WaitUntil(() => m_AttAnswered);
#endif
            // ATT beklenirken izin geri alınmış olabilir
            yield return new WaitUntil(() => LandFlowPrivacy.Allowed);
            FB.Init(HandleInitialized);
        }

        private void HandleInitialized()
        {
            if (!FB.IsInitialized)
            {
                Debug.LogError("[LandFlowMeta] Meta SDK başlatılamadı.");
                return;
            }

            s_SdkReady = true;
            ApplyConsent(LandFlowPrivacy.Allowed);
        }

        /// <summary>Özel level olayı: saniye olayın değeri (valueToSum), level kimliği parametresi olur.</summary>
        public static void LogLevelEvent(string eventName, string levelId, int seconds)
        {
            if (!s_SdkReady || !LandFlowPrivacy.Allowed) return;

            FB.LogAppEvent(eventName, seconds, new Dictionary<string, object>
            {
                { AppEventParameterName.Level, levelId },
            });
        }

        /// <summary>
        /// Level bitince çağrılır. O an gönderilemeyen dönüm noktası (SDK hazır değil, izin yok) sonraki bitirmede gider.
        /// </summary>
        public static void LevelCompleted()
        {
            if (!s_SdkReady || !LandFlowPrivacy.Allowed || LevelManager.Instance == null) return;

            int levelNumber = LevelManager.Instance.CurrentLevelIndex + 1;
            foreach (int milestone in LevelMilestones)
            {
                string key = MilestonePrefPrefix + milestone;
                if (levelNumber < milestone || PlayerPrefs.HasKey(key)) continue;

                FB.LogAppEvent(AppEventName.AchievedLevel, null, new Dictionary<string, object>
                {
                    { AppEventParameterName.Level, milestone.ToString() },
                });
                PlayerPrefs.SetInt(key, 1);
                PlayerPrefs.Save();
            }
        }

        private void ApplyConsent(bool allowed)
        {
            if (!FB.IsInitialized) return;

            FB.Mobile.SetAutoLogAppEventsEnabled(allowed);
            FB.Mobile.SetAdvertiserIDCollectionEnabled(allowed);
#if UNITY_IOS && !UNITY_EDITOR
            FB.Mobile.SetAdvertiserTrackingEnabled(allowed && m_AttAuthorized);
#endif
            if (allowed) FB.ActivateApp();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && FB.IsInitialized && LandFlowPrivacy.Allowed) FB.ActivateApp();
        }

        public void GameAnalyticsATTListenerNotDetermined() => m_AttAnswered = true;
        public void GameAnalyticsATTListenerRestricted() => m_AttAnswered = true;
        public void GameAnalyticsATTListenerDenied() => m_AttAnswered = true;

        public void GameAnalyticsATTListenerAuthorized()
        {
            m_AttAuthorized = true;
            m_AttAnswered = true;
        }
    }
}
