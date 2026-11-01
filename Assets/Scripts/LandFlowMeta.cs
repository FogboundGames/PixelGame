using System.Collections;
using Facebook.Unity;
using Facebook.Unity.Settings;
using GameAnalyticsSDK;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Meta (Facebook) SDK başlatıcısı: uygulama olayları ve reklam atfı için. Ayar dosyasında App ID veya Client Token
    /// yoksa (Facebook → Edit Settings) hiçbir şey göndermez; böylece değerler girilmeden de oyun sorunsuz çalışır.
    /// iOS'ta önce ATT izni sorulur, SDK cevaptan sonra başlatılır; böylece izinden önce olay gönderilmez.
    /// </summary>
    public sealed class LandFlowMeta : MonoBehaviour, IGameAnalyticsATTListener
    {
        private static bool s_Initialized;

        // ATT cevabı ana thread dışında gelebilir; bayraklar Start içinde ana thread'de okunur
        private volatile bool m_AttAnswered;
        private volatile bool m_AttAuthorized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_Initialized) return;
            s_Initialized = true;

            if (!FacebookSettings.IsValidAppId || string.IsNullOrEmpty(FacebookSettings.ClientToken))
            {
                Debug.LogWarning("[LandFlowMeta] Meta App ID veya Client Token yok (Facebook → Edit Settings); SDK başlatılmayacak.");
                return;
            }

            var go = new GameObject("LandFlowMeta");
            DontDestroyOnLoad(go);
            go.AddComponent<LandFlowMeta>();
        }

        private IEnumerator Start()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // Uygulama aktif değilken yapılan ATT isteği pencere göstermeden düşer
            yield return new WaitUntil(() => Application.isFocused);
            GameAnalytics.RequestTrackingAuthorization(this);
            yield return new WaitUntil(() => m_AttAnswered);
#else
            yield return null;
#endif
            FB.Init(HandleInitialized);
        }

        private void HandleInitialized()
        {
            if (!FB.IsInitialized)
            {
                Debug.LogError("[LandFlowMeta] Meta SDK başlatılamadı.");
                return;
            }

#if UNITY_IOS && !UNITY_EDITOR
            FB.Mobile.SetAdvertiserTrackingEnabled(m_AttAuthorized);
#endif
            FB.ActivateApp();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && FB.IsInitialized) FB.ActivateApp();
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
