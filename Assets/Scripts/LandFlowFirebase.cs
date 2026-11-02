using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Firebase (Google Analytics) için ince sarmalayıcı: panel level verisini buradan API ile okur.
    /// Veri toplama manifest ve Info.plist'te kapalı başlar; yalnızca oyuncu izin verince burada açılır (LandFlowPrivacy).
    /// Level olaylarını level_id ve seconds parametreleriyle gönderir. Firebase hazır olana kadar gelen olaylar sırada bekler.
    /// </summary>
    public static class LandFlowFirebase
    {
        private const string LevelIdParameter = "level_id";
        private const string SecondsParameter = "seconds";

        private readonly struct PendingEvent
        {
            public readonly string Name;
            public readonly string LevelId;
            public readonly int Seconds;

            public PendingEvent(string name, string levelId, int seconds)
            {
                Name = name;
                LevelId = levelId;
                Seconds = seconds;
            }
        }

        private static readonly Queue<PendingEvent> s_Pending = new Queue<PendingEvent>();
        private static bool s_Initializing;
        private static bool s_Ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            // Firebase'i yalnızca mobil cihazda kullanıyoruz; editör ve masaüstü build'de hiç başlatılmaz.
            LandFlowPrivacy.ConsentChanged += ApplyConsent;
            if (LandFlowPrivacy.Allowed) ApplyConsent(true);
#endif
        }

        private static void ApplyConsent(bool allowed)
        {
            if (s_Ready)
            {
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(allowed);
                return;
            }
            if (!allowed || s_Initializing) return;

            s_Initializing = true;
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                s_Initializing = false;
                if (task.Exception != null || task.Result != DependencyStatus.Available)
                {
                    Debug.LogError($"[LandFlowFirebase] Firebase başlatılamadı: {(task.Exception != null ? task.Exception.Message : task.Result.ToString())}");
                    s_Pending.Clear();
                    return;
                }

                s_Ready = true;
                bool consent = LandFlowPrivacy.Allowed;
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(consent);
                while (s_Pending.Count > 0)
                {
                    PendingEvent pending = s_Pending.Dequeue();
                    if (consent) Send(pending.Name, pending.LevelId, pending.Seconds);
                }
            });
        }

        /// <summary>Çağıran, oyuncunun izin verdiğini denetlemiş olmalı (LandFlowAnalytics).</summary>
        public static void LogLevelEvent(string eventName, string levelId, int seconds)
        {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            LandFlowFirestore.RecordLevelEvent(eventName, levelId, seconds);
            if (s_Ready)
            {
                Send(eventName, levelId, seconds);
                return;
            }

            // İzin olayı bu sınıfa LandFlowAnalytics'ten sonra ulaşmış olabilir; ilk level_start kaybolmasın
            if (!s_Initializing) ApplyConsent(true);
            s_Pending.Enqueue(new PendingEvent(eventName, levelId, seconds));
#endif
        }

        private static void Send(string eventName, string levelId, int seconds)
        {
            FirebaseAnalytics.LogEvent(eventName, new[]
            {
                new Parameter(LevelIdParameter, levelId),
                new Parameter(SecondsParameter, seconds),
            });
        }
    }
}
