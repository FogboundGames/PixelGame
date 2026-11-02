using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PixelGame
{
    /// <summary>
    /// dashboard/panel.html'in doğrudan okuduğu oyuncu özeti: players/{playerId} altında gün ve level sayaçları.
    /// playerId cihazda üretilen rastgele kimliktir. Yalnızca veri izni varsa ve mobil cihazda yazar.
    /// Firestore SDK'sı yerine REST kullanılır: SDK build'e yerel kütüphane ekliyor, bize giriş ya da canlı okuma gerekmiyor.
    /// Ücretsiz yazma kotası için olay başına değil, deneme bitince ya da uygulama arka plana geçince tek yazma yapar.
    /// Yazmalar önce cihaza kaydedilir, gönderilince silinir; böylece internet yokken ya da uygulama kapanırken kaybolmaz.
    /// </summary>
    public static class LandFlowFirestore
    {
        private const string Project = "landflow-385da";
        private const string CommitUrl = "https://firestore.googleapis.com/v1/projects/" + Project + "/databases/(default)/documents:commit";
        private const string DocumentPrefix = "projects/" + Project + "/databases/(default)/documents/players/";
        private const string PlayerIdPrefKey = "LandFlow_PlayerId";
        private const string FirstSeenPrefKey = "LandFlow_FirstSeen";
        private const string OutboxPrefKey = "LandFlow_FirestoreOutbox";
        private const string StartEvent = "level_start";
        private const string EventPrefix = "level_";
        private const double SessionGapSeconds = 300;
        private const int MaxOutbox = 100;
        private const int RequestTimeoutSeconds = 15;

        private static readonly Dictionary<string, Dictionary<string, long>> s_PendingLevels = new Dictionary<string, Dictionary<string, long>>();
        private static readonly List<string> s_Outbox = new List<string>();
        private static long s_PendingSessions;
        private static float s_PendingSeconds;
        private static bool s_Started;
        private static bool s_Sending;
        private static DateTime s_LeftAt;
        private static Tracker s_Tracker;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            LandFlowPrivacy.ConsentChanged += allowed => { if (allowed) Begin(); };
            if (LandFlowPrivacy.Allowed) Begin();
#endif
        }

        private static void Begin()
        {
            if (s_Started) return;
            s_Started = true;

            string saved = PlayerPrefs.GetString(OutboxPrefKey, "");
            if (saved.Length > 0) s_Outbox.AddRange(saved.Split('\n'));

            var host = new GameObject("LandFlowFirestore");
            UnityEngine.Object.DontDestroyOnLoad(host);
            s_Tracker = host.AddComponent<Tracker>();

            s_PendingSessions++;
            Flush();
        }

        /// <summary>eventName ör. "level_complete": "complete" sayacını ve "completeSec" toplamını artırır.</summary>
        public static void RecordLevelEvent(string eventName, string levelId, int seconds)
        {
            if (!s_PendingLevels.TryGetValue(levelId, out Dictionary<string, long> counters))
            {
                counters = new Dictionary<string, long>();
                s_PendingLevels[levelId] = counters;
            }

            string action = eventName.Substring(EventPrefix.Length);
            counters.TryGetValue(action, out long count);
            counters[action] = count + 1;

            if (eventName == StartEvent) return;

            string secondsKey = action + "Sec";
            counters.TryGetValue(secondsKey, out long total);
            counters[secondsKey] = total + seconds;
            Flush();
        }

        private static void Flush()
        {
            if (!s_Started || !LandFlowPrivacy.Allowed) return;

            long seconds = (long)s_PendingSeconds;
            if (s_PendingLevels.Count > 0 || s_PendingSessions > 0 || seconds > 0)
            {
                string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                s_Outbox.Add(BuildCommit(today, seconds));
                if (s_Outbox.Count > MaxOutbox) s_Outbox.RemoveAt(0);
                SaveOutbox();

                s_PendingLevels.Clear();
                s_PendingSessions = 0;
                s_PendingSeconds -= seconds;
            }

            if (!s_Sending && s_Outbox.Count > 0) s_Tracker.StartCoroutine(SendOutbox());
        }

        private static string BuildCommit(string today, long seconds)
        {
            var transforms = new List<string>();
            AddIncrement(transforms, $"days.`{today}`.sessions", s_PendingSessions);
            AddIncrement(transforms, $"days.`{today}`.seconds", seconds);
            foreach (KeyValuePair<string, Dictionary<string, long>> level in s_PendingLevels)
            {
                foreach (KeyValuePair<string, long> counter in level.Value)
                {
                    AddIncrement(transforms, $"levels.`{level.Key}`.{counter.Key}", counter.Value);
                }
            }

            string platform = Application.platform == RuntimePlatform.IPhonePlayer ? "ios" : "android";
            return "{\"writes\":[{\"update\":{\"name\":\"" + DocumentPrefix + PlayerId() + "\",\"fields\":{"
                + StringField("firstSeen", FirstSeen(today)) + ","
                + StringField("lastSeen", today) + ","
                + StringField("platform", platform) + ","
                + StringField("version", Application.version)
                + "}},\"updateMask\":{\"fieldPaths\":[\"firstSeen\",\"lastSeen\",\"platform\",\"version\"]},\"updateTransforms\":["
                + string.Join(",", transforms) + "]}]}";
        }

        private static void AddIncrement(List<string> transforms, string fieldPath, long amount)
        {
            if (amount != 0) transforms.Add("{\"fieldPath\":\"" + fieldPath + "\",\"increment\":{\"integerValue\":\"" + amount + "\"}}");
        }

        private static string StringField(string name, string value)
        {
            return "\"" + name + "\":{\"stringValue\":\"" + value.Replace("\\", "").Replace("\"", "") + "\"}";
        }

        /// <summary>Sırayla gönderir; hata olursa kalanlar cihazda bekler, sonraki yazmada ya da açılışta yeniden denenir.</summary>
        private static IEnumerator SendOutbox()
        {
            s_Sending = true;
            while (s_Outbox.Count > 0)
            {
                using (var request = new UnityWebRequest(CommitUrl, UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(s_Outbox[0]));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = RequestTimeoutSeconds;
                    yield return request.SendWebRequest();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"[LandFlowFirestore] Yazılamadı: {request.error} {request.downloadHandler.text}");
                        // Sunucu isteği reddettiyse yeniden denemek işe yaramaz; bozuk kayıt sırayı tıkamasın
                        if (request.result == UnityWebRequest.Result.ProtocolError && request.responseCode >= 400 && request.responseCode < 500 && request.responseCode != 429)
                        {
                            s_Outbox.RemoveAt(0);
                            SaveOutbox();
                        }
                        break;
                    }
                }

                s_Outbox.RemoveAt(0);
                SaveOutbox();
            }
            s_Sending = false;
        }

        private static void SaveOutbox()
        {
            PlayerPrefs.SetString(OutboxPrefKey, string.Join("\n", s_Outbox));
            PlayerPrefs.Save();
        }

        private static string PlayerId()
        {
            string id = PlayerPrefs.GetString(PlayerIdPrefKey, "");
            if (id.Length > 0) return id;

            id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PlayerIdPrefKey, id);
            PlayerPrefs.Save();
            return id;
        }

        private static string FirstSeen(string today)
        {
            string firstSeen = PlayerPrefs.GetString(FirstSeenPrefKey, "");
            if (firstSeen.Length > 0) return firstSeen;

            PlayerPrefs.SetString(FirstSeenPrefKey, today);
            PlayerPrefs.Save();
            return today;
        }

        /// <summary>Oynama süresini toplar (arka plan sayılmaz), arka plana geçişte yazar, uzun aradan dönüşü yeni oturum sayar.</summary>
        private sealed class Tracker : MonoBehaviour
        {
            private void Update() => s_PendingSeconds += Mathf.Min(Time.unscaledDeltaTime, 1f);

            private void OnApplicationPause(bool paused)
            {
                if (paused)
                {
                    s_LeftAt = DateTime.UtcNow;
                    Flush();
                }
                else
                {
                    if ((DateTime.UtcNow - s_LeftAt).TotalSeconds >= SessionGapSeconds) s_PendingSessions++;
                    // Arka plana geçerken başlayan gönderim yarıda kalmış olabilir
                    s_Sending = false;
                    StopAllCoroutines();
                    Flush();
                }
            }

            private void OnApplicationQuit() => Flush();
        }
    }
}
