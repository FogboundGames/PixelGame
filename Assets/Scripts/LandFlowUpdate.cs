using System;
using System.Collections.Generic;
using UnityEngine;
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
using Firebase.Extensions;
using Firebase.RemoteConfig;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelGame
{
    /// <summary>
    /// Yeni sürüm bildirimi. Ayarlar Firebase Remote Config'ten gelir (konsoldan düzenlenir, sürüm geçmişi tutulur):
    ///   update_latest_version  "1.9"  → bu sürümden eskiler isteğe bağlı güncelleme görür
    ///   update_min_version     "1.7"  → bundan eskiler oyuna devam edemez (zorunlu)
    ///   update_title / update_message → pop-up metni (İngilizce)
    ///   update_ios_app_id      App Store'daki sayısal uygulama numarası
    /// Android'de ayrıca Play'e sorulur: Play bu cihaza güncelleme sunmuyorsa (mağazada henüz yayında değil) pop-up çıkmaz.
    /// İsteğe bağlı pop-up level ortasında açılmaz; yeni level başlarken gösterilir ve "Later" ile 3 gün susar.
    /// </summary>
    public static class LandFlowUpdate
    {
        public enum State
        {
            UpToDate,
            Optional,
            Required,
        }

        private const string LatestKey = "update_latest_version";
        private const string MinKey = "update_min_version";
        private const string TitleKey = "update_title";
        private const string MessageKey = "update_message";
        private const string IosAppIdKey = "update_ios_app_id";

        private const string SnoozePrefKey = "LandFlow_UpdateSnooze";
        private const int SnoozeDays = 3;
        private const float LevelStartWindow = 8f;
        private const ulong FetchIntervalMs = 30UL * 60UL * 1000UL;

        private static readonly Dictionary<string, object> s_Defaults = new Dictionary<string, object>
        {
            { LatestKey, "" },
            { MinKey, "" },
            { TitleKey, "New Update!" },
            { MessageKey, "New levels and improvements are waiting for you!" },
            { IosAppIdKey, "" },
        };

        private static State s_State;
        private static string s_Latest = "";
        private static string s_Title = "";
        private static string s_Message = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            PixelArtGenerator.LevelLoaded += HandleLevelLoaded;
            var go = new GameObject("[LandFlowUpdate]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<ResumeListener>();
            LandFlowFirebase.WhenReady(InitRemoteConfig);
#endif
        }

        /// <summary>Sürümleri sayı sayı karşılaştırır ("1.10" > "1.9"); boş ya da okunamayan hedef sürüm hiçbir şey tetiklemez.</summary>
        public static State Decide(string current, string latest, string min)
        {
            if (IsNewer(min, current)) return State.Required;
            if (IsNewer(latest, current)) return State.Optional;
            return State.UpToDate;
        }

        public static bool IsNewer(string candidate, string current)
        {
            if (!TryParse(candidate, out int[] a) || !TryParse(current, out int[] b)) return false;
            for (int i = 0; i < Mathf.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0;
                int y = i < b.Length ? b[i] : 0;
                if (x != y) return x > y;
            }
            return false;
        }

        private static bool TryParse(string version, out int[] parts)
        {
            parts = null;
            if (string.IsNullOrWhiteSpace(version)) return false;
            string[] pieces = version.Trim().TrimStart('v', 'V').Split('.');
            parts = new int[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                // "2-beta" gibi eklerde baştaki sayıyı al
                int digits = 0;
                while (digits < pieces[i].Length && char.IsDigit(pieces[i][digits])) digits++;
                if (digits == 0 || !int.TryParse(pieces[i].Substring(0, digits), out parts[i])) return false;
            }
            return true;
        }

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        private static string s_IosAppId = "";
        private static bool s_Checking;
        private static bool s_ConfigReady;
        private static bool s_WantOptional;
        private static float s_LastLevelLoadTime = -100f;

        private static void InitRemoteConfig()
        {
            FirebaseRemoteConfig config = FirebaseRemoteConfig.DefaultInstance;
            var settings = new ConfigSettings { MinimumFetchIntervalInMilliseconds = FetchIntervalMs, FetchTimeoutInMilliseconds = 10000 };
            config.SetConfigSettingsAsync(settings).ContinueWithOnMainThread(settingsTask =>
            {
                config.SetDefaultsAsync(s_Defaults).ContinueWithOnMainThread(defaultsTask =>
                {
                    s_ConfigReady = true;
                    Check();
                });
            });
        }

        /// <summary>Remote Config'i günceller (aralık dolmadıysa önbellekten okur; çevrimdışıyken son değerler kullanılır) ve durumu yeniden değerlendirir.</summary>
        private static void Check()
        {
            if (!s_ConfigReady || s_Checking) return;
            s_Checking = true;
            FirebaseRemoteConfig config = FirebaseRemoteConfig.DefaultInstance;
            config.FetchAndActivateAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted) Debug.Log($"[LandFlowUpdate] Remote Config alınamadı, son değerler kullanılıyor: {task.Exception?.GetBaseException().Message}");

                s_Latest = config.GetValue(LatestKey).StringValue;
                s_Title = config.GetValue(TitleKey).StringValue;
                s_Message = config.GetValue(MessageKey).StringValue;
                s_IosAppId = config.GetValue(IosAppIdKey).StringValue;
                State state = Decide(Application.version, s_Latest, config.GetValue(MinKey).StringValue);
                Debug.Log($"[LandFlowUpdate] cihaz={Application.version} son={s_Latest} durum={state}");

                if (state == State.UpToDate)
                {
                    Apply(state);
                    return;
                }

                LandFlowPlayUpdate.Query(availability =>
                {
                    // Play bu cihaza güncelleme sunmuyorsa mağazada henüz yok demektir; Play'e ulaşılamazsa Remote Config'e güvenilir
                    if (availability == LandFlowPlayUpdate.Availability.NotAvailable) state = State.UpToDate;
                    Apply(state);
                });
            });
        }

        private static void Apply(State state)
        {
            s_Checking = false;
            s_State = state;
            s_WantOptional = false;
            if (state == State.UpToDate) LandFlowUpdatePopup.Hide();
            else if (state == State.Required) Show();
            else TryShowOptional();
        }

        private static void HandleLevelLoaded(PixelLevelData level)
        {
            s_LastLevelLoadTime = Time.unscaledTime;
            if (s_State == State.Optional) TryShowOptional();
        }

        private static bool IsLoading => LandFlowLoadingScreen.Instance != null && LandFlowLoadingScreen.Instance.IsBusy;

        /// <summary>Level yükleniyorsa ya da yeni başladıysa gösterir (yükleme ekranı kapanınca); değilse bir sonraki level başlangıcını bekler.</summary>
        private static void TryShowOptional()
        {
            if (IsLoading || Time.unscaledTime - s_LastLevelLoadTime <= LevelStartWindow) s_WantOptional = true;
        }

        private sealed class ResumeListener : MonoBehaviour
        {
            private void OnApplicationPause(bool paused)
            {
                if (!paused) Check();
            }

            private void Update()
            {
                if (!s_WantOptional || IsLoading) return;
                s_WantOptional = false;
                if (s_State == State.Optional && !LandFlowUpdatePopup.IsShowing && !IsSnoozed()) Show();
            }
        }
#endif

        private static void Show()
        {
            LandFlowUpdatePopup.Show(s_Title, s_Message, s_Latest, s_State == State.Required, OpenUpdate, Snooze);
        }

        private static bool IsSnoozed()
        {
            string[] parts = PlayerPrefs.GetString(SnoozePrefKey, "").Split('|');
            return parts.Length == 2 && parts[0] == s_Latest && long.TryParse(parts[1], out long until) && DateTime.UtcNow.Ticks < until;
        }

        private static void Snooze()
        {
            PlayerPrefs.SetString(SnoozePrefKey, s_Latest + "|" + DateTime.UtcNow.AddDays(SnoozeDays).Ticks);
            PlayerPrefs.Save();
        }

        private static void OpenUpdate()
        {
            LandFlowPlayUpdate.StartImmediate(started =>
            {
                if (!started) Application.OpenURL(StoreUrl());
            });
        }

        private static string StoreUrl()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return string.IsNullOrEmpty(s_IosAppId) ? "https://apps.apple.com/" : "itms-apps://apps.apple.com/app/id" + s_IosAppId;
#elif UNITY_ANDROID && !UNITY_EDITOR
            return "market://details?id=" + Application.identifier;
#else
            return "https://play.google.com/store/apps/details?id=" + Application.identifier;
#endif
        }

#if UNITY_EDITOR
        [MenuItem("Tools/PixelGame/Update Popup/Optional (Play Mode)", priority = 20)]
        private static void PreviewOptional() => Preview(State.Optional);

        [MenuItem("Tools/PixelGame/Update Popup/Required (Play Mode)", priority = 21)]
        private static void PreviewRequired() => Preview(State.Required);

        [MenuItem("Tools/PixelGame/Update Popup/Optional (Play Mode)", true)]
        [MenuItem("Tools/PixelGame/Update Popup/Required (Play Mode)", true)]
        private static bool CanPreview() => Application.isPlaying;

        private static void Preview(State state)
        {
            s_State = state;
            s_Latest = "9.9";
            s_Title = (string)s_Defaults[TitleKey];
            s_Message = (string)s_Defaults[MessageKey];
            Show();
        }
#endif
    }
}
