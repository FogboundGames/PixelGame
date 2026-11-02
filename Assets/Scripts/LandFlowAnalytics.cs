using System.Text;
using GameAnalyticsSDK;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// GameAnalytics için ince sarmalayıcı. Ayar dosyası (Resources/GameAnalytics/Settings) yoksa hiçbir şey göndermez;
    /// böylece anahtarlar girilmeden de oyun sorunsuz çalışır. Oyuncu veri izni vermeden başlatılmaz (LandFlowPrivacy).
    /// Level başlangıç/bitiş/kayıp olaylarını "Level_015:Hayalet" biçiminde gönderir (sıra değişse de level adı ayırt edilebilsin diye).
    /// Yarıda kesilen denemeler için "Level:Restart:Level_015" ve "Level:Quit:Level_015" tasarım olaylarını, değer olarak geçen saniyeyle gönderir.
    /// Quit, level sürerken uygulama arka plana geçince deneme başına bir kez gider; oyuncu sonra dönüp devam etmiş olabilir.
    /// </summary>
    public static class LandFlowAnalytics
    {
        private const string TurkishLetters = "çÇğĞıİöÖşŞüÜéÉ";
        private const string AsciiLetters = "cCgGiIoOsSuUeE";
        private const string AllowedSymbols = " -_.,()!?";

        private static bool s_Enabled;
        private static bool s_Initialized;
        private static bool s_Started;
        private static PixelLevelData s_LastStartedLevel;
        private static float s_LastStartTime = -10f;
        private static bool s_StartPending;
        private static bool s_LevelActive;
        private static bool s_QuitSent;
        private static float s_LevelElapsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_Initialized) return;
            s_Initialized = true;

            if (Resources.Load("GameAnalytics/Settings") == null)
            {
                Debug.LogWarning("[LandFlowAnalytics] GameAnalytics ayar dosyası yok (Window → GameAnalytics → Select Settings); olaylar gönderilmeyecek.");
                return;
            }

            // İzinden önce de dinlenir: yeni oyuncu izni ilk level yüklendikten sonra verir, o levelin Start'ı kaybolmasın
            PixelArtGenerator.LevelLoaded += HandleLevelLoaded;
            LandFlowPrivacy.ConsentChanged += ApplyConsent;
            if (LandFlowPrivacy.Allowed) ApplyConsent(true);
        }

        private static void ApplyConsent(bool allowed)
        {
            s_Enabled = allowed;
            if (s_Started)
            {
                GameAnalytics.SetEnabledEventSubmission(allowed);
                if (allowed) SendPendingStart();
                return;
            }
            if (!allowed) return;

            if (Object.FindFirstObjectByType<GameAnalytics>() == null)
            {
                new GameObject("GameAnalytics").AddComponent<GameAnalytics>();
            }

            GameAnalytics.Initialize();
            s_Started = true;

            var listener = new GameObject("LandFlowAnalytics");
            Object.DontDestroyOnLoad(listener);
            listener.AddComponent<LevelTimer>();

            SendPendingStart();
        }

        private static string ProgressionId(PixelLevelData level, out string levelName)
        {
            int number = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelIndex + 1 : (level != null ? level.LevelIndex : 0);
            levelName = level != null ? AllowedLevelName(level.LevelName) : "Unknown";
            return $"Level_{number:000}";
        }

        /// <summary>GameAnalytics Türkçe harf içeren progression olayını tümüyle reddeder ("Balık" → "Balik").</summary>
        private static string AllowedLevelName(string levelName)
        {
            if (string.IsNullOrEmpty(levelName)) return "Unknown";

            var allowed = new StringBuilder(levelName.Length);
            foreach (char c in levelName)
            {
                int turkish = TurkishLetters.IndexOf(c);
                if (turkish >= 0) allowed.Append(AsciiLetters[turkish]);
                else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || AllowedSymbols.IndexOf(c) >= 0) allowed.Append(c);
            }
            return allowed.Length > 0 ? allowed.ToString() : "Unknown";
        }

        private static PixelLevelData CurrentLevel => LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;

        private static void HandleLevelLoaded(PixelLevelData level)
        {
            if (!Application.isPlaying) return;
            // LevelLoaded aynı yükleme için birden fazla tetiklenebiliyor
            if (level == s_LastStartedLevel && Time.unscaledTime - s_LastStartTime < 1f) return;
            s_LastStartedLevel = level;
            s_LastStartTime = Time.unscaledTime;
            s_LevelElapsed = 0f;
            s_LevelActive = true;
            s_QuitSent = false;

            if (s_Enabled) SendStart(level);
            else s_StartPending = true;
        }

        private static void SendStart(PixelLevelData level)
        {
            string id = ProgressionId(level, out string name);
            GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, id, name);
            LandFlowFirebase.LogLevelEvent("level_start", id, 0);
            LandFlowFirestore.RecordLevelName(id, level != null ? level.LevelName : "");
        }

        /// <summary>Level izin ekranı açıkken yüklendiyse Start şimdi gider; süre de oyuncu oynamaya başladığı andan sayılır.</summary>
        private static void SendPendingStart()
        {
            if (!s_StartPending) return;
            s_StartPending = false;
            s_LevelElapsed = 0f;
            SendStart(s_LastStartedLevel);
        }

        /// <summary>Küpler bittiğinde (kazanma anı).</summary>
        public static void LevelComplete()
        {
            s_LevelActive = false;
            if (!s_Enabled) return;
            string id = ProgressionId(CurrentLevel, out string name);
            GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, id, name, Mathf.RoundToInt(s_LevelElapsed));
            LandFlowMeta.LogLevelEvent("LevelComplete", id, Mathf.RoundToInt(s_LevelElapsed));
            LandFlowFirebase.LogLevelEvent("level_complete", id, Mathf.RoundToInt(s_LevelElapsed));
        }

        /// <summary>Slotlar dolup hamle kalmadığında.</summary>
        public static void LevelFail()
        {
            s_LevelActive = false;
            if (!s_Enabled) return;
            string id = ProgressionId(CurrentLevel, out string name);
            GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, id, name, Mathf.RoundToInt(s_LevelElapsed));
            LandFlowMeta.LogLevelEvent("LevelFail", id, Mathf.RoundToInt(s_LevelElapsed));
            LandFlowFirebase.LogLevelEvent("level_fail", id, Mathf.RoundToInt(s_LevelElapsed));
        }

        /// <summary>Oyuncu leveli bitirmeden yeniden başlattığında.</summary>
        public static void LevelRestart()
        {
            if (!s_LevelActive) return;
            s_LevelActive = false;
            // Yeniden yükleme hızlı bitse de yeni deneme sayılsın
            s_LastStartedLevel = null;
            SendAttemptEvent("Restart");
        }

        private static void HandleAppLeft()
        {
            if (!s_LevelActive || s_QuitSent) return;
            s_QuitSent = true;
            SendAttemptEvent("Quit");
        }

        private static void SendAttemptEvent(string action)
        {
            if (!s_Enabled) return;
            string id = ProgressionId(CurrentLevel, out _);
            GameAnalytics.NewDesignEvent($"Level:{action}:{id}", Mathf.RoundToInt(s_LevelElapsed));
            LandFlowMeta.LogLevelEvent($"Level{action}", id, Mathf.RoundToInt(s_LevelElapsed));
            LandFlowFirebase.LogLevelEvent($"level_{action.ToLowerInvariant()}", id, Mathf.RoundToInt(s_LevelElapsed));
        }

        /// <summary>Serbest tasarım olayı, ör. "Speed:2X" veya "Level:Restart".</summary>
        public static void Design(string eventId, float value = 0f)
        {
            if (!s_Enabled) return;
            GameAnalytics.NewDesignEvent(eventId, value);
            LandFlowFirestore.RecordEvent(eventId);
        }

        /// <summary>Deneme süresini kare kare toplar; arka planda geçen süre sayılmaz. Uygulamanın arka plana geçişini de bildirir.</summary>
        private sealed class LevelTimer : MonoBehaviour
        {
            private void Update()
            {
                if (s_LevelActive) s_LevelElapsed += Mathf.Min(Time.unscaledDeltaTime, 1f);
            }

            private void OnApplicationPause(bool paused)
            {
                if (paused) HandleAppLeft();
            }

            private void OnApplicationQuit() => HandleAppLeft();
        }
    }
}
