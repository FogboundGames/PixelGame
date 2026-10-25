using GameAnalyticsSDK;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// GameAnalytics için ince sarmalayıcı. Ayar dosyası (Resources/GameAnalytics/Settings) yoksa hiçbir şey göndermez;
    /// böylece anahtarlar girilmeden de oyun sorunsuz çalışır. Level başlangıç/bitiş/kayıp olaylarını
    /// "Level_015:Hayalet" biçiminde gönderir (sıra değişse de level adı ayırt edilebilsin diye).
    /// </summary>
    public static class LandFlowAnalytics
    {
        private static bool s_Enabled;
        private static bool s_Initialized;
        private static PixelLevelData s_LastStartedLevel;
        private static float s_LastStartTime = -10f;
        private static float s_LevelStartTime;

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

            if (Object.FindFirstObjectByType<GameAnalytics>() == null)
            {
                new GameObject("GameAnalytics").AddComponent<GameAnalytics>();
            }

            GameAnalytics.Initialize();
            s_Enabled = true;
            PixelArtGenerator.LevelLoaded += HandleLevelLoaded;
        }

        private static string ProgressionId(PixelLevelData level, out string levelName)
        {
            int number = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelIndex + 1 : (level != null ? level.LevelIndex : 0);
            levelName = level != null && !string.IsNullOrEmpty(level.LevelName) ? level.LevelName.Replace(":", "") : "Unknown";
            return $"Level_{number:000}";
        }

        private static PixelLevelData CurrentLevel => LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;

        private static void HandleLevelLoaded(PixelLevelData level)
        {
            if (!s_Enabled || !Application.isPlaying) return;
            // LevelLoaded aynı yükleme için birden fazla tetiklenebiliyor
            if (level == s_LastStartedLevel && Time.unscaledTime - s_LastStartTime < 1f) return;
            s_LastStartedLevel = level;
            s_LastStartTime = Time.unscaledTime;
            s_LevelStartTime = Time.unscaledTime;

            string id = ProgressionId(level, out string name);
            GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, id, name);
        }

        /// <summary>Küpler bittiğinde (kazanma anı).</summary>
        public static void LevelComplete()
        {
            if (!s_Enabled) return;
            string id = ProgressionId(CurrentLevel, out string name);
            GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, id, name, Mathf.RoundToInt(Time.unscaledTime - s_LevelStartTime));
        }

        /// <summary>Slotlar dolup hamle kalmadığında.</summary>
        public static void LevelFail()
        {
            if (!s_Enabled) return;
            string id = ProgressionId(CurrentLevel, out string name);
            GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, id, name, Mathf.RoundToInt(Time.unscaledTime - s_LevelStartTime));
        }

        /// <summary>Serbest tasarım olayı, ör. "Speed:2X" veya "Level:Restart".</summary>
        public static void Design(string eventId, float value = 0f)
        {
            if (!s_Enabled) return;
            GameAnalytics.NewDesignEvent(eventId, value);
        }
    }
}
