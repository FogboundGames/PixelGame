using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelGame
{
    /// <summary>
    /// Firestore üzerinden en güncel oyun sürümünü ve duyuru iletisini kontrol eder.
    /// Yeni bir build/sürüm çıktığında oyuncuya şık bir oyun içi bildirim (Pop-up) gösterip
    /// tek tıkla Google Play Store / App Store mağaza sayfasına yönlendirir.
    /// </summary>
    [ExecuteAlways]
    public class InAppUpdateManager : MonoBehaviour
    {
        private const string VersionDocUrl = "https://firestore.googleapis.com/v1/projects/landflow-385da/databases/(default)/documents/players/_app_version";
        private const string DismissedDatePrefKey = "LandFlow_LastUpdateDismissedDate";
        private const string DismissedVersionPrefKey = "LandFlow_LastUpdateDismissedVersion";

        private static InAppUpdateManager s_Instance;
        public static InAppUpdateManager Instance => s_Instance;

        [Header("Ayarlar")]
        [Tooltip("Oyun açılışında otomatik güncelleme kontrolü yapılsın mı?")]
        [SerializeField] private bool m_CheckOnStart = true;

        [Tooltip("Kontrol için ağ zaman aşımı süresi (saniye)")]
        [SerializeField] private int m_TimeoutSeconds = 6;

        [Tooltip("Test amaçlı her zaman pop-up açılsın mı?")]
        [SerializeField] private bool m_DebugForceShow = false;

        private GameObject m_PopupRoot;
        private RectTransform m_CardRect;
        private TextMeshProUGUI m_TitleText;
        private TextMeshProUGUI m_MessageText;
        private TextMeshProUGUI m_VersionBadgeText;
        private Button m_UpdateButton;
        private Button m_LaterButton;
        private GameObject m_LaterButtonObj;
        private Button m_CloseButton;
        private GameObject m_CloseButtonObj;
        private Coroutine m_CardPopInCoroutine;
        private TMP_FontAsset m_Font;

        public class UpdateInfo
        {
            public string LatestVersion = "";
            public string MinRequiredVersion = "1.0.0";
            public string Title = "Yeni Güncelleme Geldi!";
            public string Message = "Yepyeni seviyeler ve eğlenceli geliştirmeler seni bekliyor!";
            public string StoreUrl = "";
            public bool IsForce = false;
        }

        private UpdateInfo m_CurrentInfo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (s_Instance != null) return;
            GameObject go = new GameObject("[InAppUpdateManager]");
            s_Instance = go.AddComponent<InAppUpdateManager>();
            if (Application.isPlaying) DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }
            s_Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (Application.isPlaying && m_CheckOnStart)
            {
                // Sahne elemanları ve ağ hazır olunca kontrol et
                StartCoroutine(DelayedCheckRoutine());
            }
        }

        private void Update()
        {
            // Pop-up açık değilse işlem yapma
            if (m_PopupRoot == null || !m_PopupRoot.activeSelf) return;

            // Zorunlu güncelleme ise kapatma kontrolleri devre dışıdır
            if (m_CurrentInfo != null && m_CurrentInfo.IsForce) return;

            // Tıklama / Dokunma algılama (Hem Play Mode hem Edit Mode desteği)
            bool clicked = false;
            Vector2 screenPos = Vector2.zero;

            if (Input.GetMouseButtonDown(0))
            {
                clicked = true;
                screenPos = Input.mousePosition;
            }
            else if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                clicked = true;
                screenPos = Input.GetTouch(0).position;
            }

            if (clicked)
            {
                // 1. Sağ Üst '✕' Butonu Tıklaması
                if (m_CloseButtonObj != null && m_CloseButtonObj.activeSelf)
                {
                    RectTransform closeRt = (RectTransform)m_CloseButtonObj.transform;
                    if (RectTransformUtility.RectangleContainsScreenPoint(closeRt, screenPos, null))
                    {
                        DismissPopup();
                        return;
                    }
                }

                // 2. 'Daha Sonra' Butonu Tıklaması
                if (m_LaterButtonObj != null && m_LaterButtonObj.activeSelf)
                {
                    RectTransform laterRt = (RectTransform)m_LaterButtonObj.transform;
                    if (RectTransformUtility.RectangleContainsScreenPoint(laterRt, screenPos, null))
                    {
                        DismissPopup();
                        return;
                    }
                }

                // 3. Kartın Dışına (Arka Plan Karartmasına) Tıklama
                if (m_CardRect != null)
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(m_CardRect, screenPos, null))
                    {
                        DismissPopup();
                        return;
                    }
                }
            }
        }

        private IEnumerator DelayedCheckRoutine()
        {
            yield return new WaitForSeconds(1.2f);
            CheckForUpdates();
        }

        /// <summary>
        /// Güncellemeyi kontrol eder ve gerekirse Pop-up gösterir.
        /// </summary>
        public void CheckForUpdates(Action<bool, UpdateInfo> onComplete = null)
        {
            StartCoroutine(FetchVersionConfigRoutine(onComplete));
        }

        private IEnumerator FetchVersionConfigRoutine(Action<bool, UpdateInfo> onComplete)
        {
            string url = VersionDocUrl + "?t=" + DateTime.UtcNow.Ticks;
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = m_TimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[InAppUpdateManager] Versiyon kontrolü yapılamadı (çevrimdışı ya da ağ hatası): {req.error}");
                    onComplete?.Invoke(false, null);
                    yield break;
                }

                string json = req.downloadHandler.text;
                UpdateInfo info = ParseFirestoreVersionDoc(json);

                if (info == null || string.IsNullOrEmpty(info.LatestVersion))
                {
                    onComplete?.Invoke(false, null);
                    yield break;
                }

                m_CurrentInfo = info;
                string currentVersion = Application.version;
                bool isNewer = IsVersionNewer(info.LatestVersion, currentVersion);
                bool isForce = info.IsForce || IsVersionNewer(info.MinRequiredVersion, currentVersion);
                info.IsForce = isForce;

                if (m_DebugForceShow)
                {
                    isNewer = true;
                }

                if (isNewer)
                {
                    // Kullanıcı zorunlu olmayan bir güncellemeyi bugün zaten kapattıysa günde bir kereden fazla boğmayalım
                    string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                    string dismissedDate = PlayerPrefs.GetString(DismissedDatePrefKey, "");
                    string dismissedVer = PlayerPrefs.GetString(DismissedVersionPrefKey, "");

                    if (!isForce && dismissedDate == today && dismissedVer == info.LatestVersion && !m_DebugForceShow)
                    {
                        Debug.Log("[InAppUpdateManager] Güncelleme mevcut ama kullanıcı bugün zaten 'Daha Sonra' dedi.");
                        onComplete?.Invoke(false, info);
                        yield break;
                    }

                    ShowUpdatePopup(info);
                    onComplete?.Invoke(true, info);
                }
                else
                {
                    Debug.Log($"[InAppUpdateManager] Oyun sürümü güncel ({currentVersion}).");
                    onComplete?.Invoke(false, info);
                }
            }
        }

        private UpdateInfo ParseFirestoreVersionDoc(string json)
        {
            try
            {
                var info = new UpdateInfo();

                // 1. Version
                info.LatestVersion = ExtractFieldString(json, "version");

                // 2. Device (min version ve force bayrağı burada saklanabilir: min:1.0.0|force:1)
                string deviceStr = ExtractFieldString(json, "device");
                if (!string.IsNullOrEmpty(deviceStr))
                {
                    if (deviceStr.Contains("min:"))
                    {
                        var m = Regex.Match(deviceStr, @"min:([0-9\.]+)");
                        if (m.Success) info.MinRequiredVersion = m.Groups[1].Value;
                    }
                    if (deviceStr.Contains("force:1") || deviceStr.Contains("force:true"))
                    {
                        info.IsForce = true;
                    }
                }

                // 3. Title ve Message (events haritası altında stringValue olarak tutulur)
                string title = ExtractMapFieldString(json, "title");
                if (!string.IsNullOrEmpty(title)) info.Title = title;

                string message = ExtractMapFieldString(json, "message");
                if (!string.IsNullOrEmpty(message)) info.Message = message;

                string storeUrl = ExtractMapFieldString(json, "store_url");
                if (!string.IsNullOrEmpty(storeUrl)) info.StoreUrl = storeUrl;

                return info;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InAppUpdateManager] JSON parse hatası: {ex.Message}");
                return null;
            }
        }

        private string ExtractFieldString(string json, string fieldName)
        {
            string pattern = "\"" + fieldName + "\"\\s*:\\s*\\{\\s*\"stringValue\"\\s*:\\s*\"([^\"]*)\"";
            var m = Regex.Match(json, pattern);
            return m.Success ? m.Groups[1].Value : "";
        }

        private string ExtractMapFieldString(string json, string fieldName)
        {
            string pattern = "\"" + fieldName + "\"\\s*:\\s*\\{\\s*\"stringValue\"\\s*:\\s*\"([^\"]*)\"";
            var m = Regex.Match(json, pattern);
            return m.Success ? m.Groups[1].Value : "";
        }

        /// <summary>
        /// İki semantik sürümü karşılaştırır (ör. "1.8" > "1.7" => true).
        /// </summary>
        public static bool IsVersionNewer(string remoteVersion, string localVersion)
        {
            if (string.IsNullOrEmpty(remoteVersion) || string.IsNullOrEmpty(localVersion)) return false;

            string cleanRemote = remoteVersion.Trim().TrimStart('v', 'V');
            string cleanLocal = localVersion.Trim().TrimStart('v', 'V');

            string[] rParts = cleanRemote.Split('.');
            string[] lParts = cleanLocal.Split('.');
            int maxLen = Mathf.Max(rParts.Length, lParts.Length);

            for (int i = 0; i < maxLen; i++)
            {
                int rVal = (i < rParts.Length && int.TryParse(rParts[i], out int rv)) ? rv : 0;
                int lVal = (i < lParts.Length && int.TryParse(lParts[i], out int lv)) ? lv : 0;

                if (rVal > lVal) return true;
                if (rVal < lVal) return false;
            }
            return false;
        }

        /// <summary>
        /// Güncelleme bildirim pop-up'ını ekranda gösterir.
        /// </summary>
        public void ShowUpdatePopup(UpdateInfo info)
        {
            m_CurrentInfo = info ?? new UpdateInfo();
            EnsurePopupUi();

            // SDF fontlarda desteklenmeyen emoji ve karakterleri temizle
            string cleanTitle = m_CurrentInfo.Title ?? "YENİ GÜNCELLEME!";
            cleanTitle = Regex.Replace(cleanTitle, @"\p{Cs}|\p{So}", "").Trim();

            if (m_TitleText != null) m_TitleText.text = cleanTitle;
            if (m_MessageText != null) m_MessageText.text = m_CurrentInfo.Message;
            if (m_VersionBadgeText != null)
            {
                m_VersionBadgeText.text = $"v{Application.version}  ->  <color=#10B981><b>v{m_CurrentInfo.LatestVersion}</b></color>";
            }

            // Zorunlu güncelleme kontrolü:
            // Zorunlu ise (IsForce == true) ne 'X' butonu ne de 'Daha Sonra' butonu görünür!
            bool canDismiss = !m_CurrentInfo.IsForce;

            if (m_LaterButtonObj != null)
            {
                m_LaterButtonObj.SetActive(canDismiss);
            }

            if (m_CloseButtonObj != null)
            {
                m_CloseButtonObj.SetActive(canDismiss);
            }

            m_PopupRoot.SetActive(true);

            // Açılış animasyonu (Pop / Punch - yalnızca Play Mode'da)
            if (m_CardRect != null)
            {
                if (Application.isPlaying)
                {
                    if (m_CardPopInCoroutine != null)
                    {
                        StopCoroutine(m_CardPopInCoroutine);
                        m_CardPopInCoroutine = null;
                    }
                    m_CardRect.localScale = Vector3.one * 0.75f;
                    m_CardPopInCoroutine = StartCoroutine(CardPopInRoutine(m_CardRect));
                }
                else
                {
                    m_CardRect.localScale = Vector3.one;
                }
            }
        }

        private IEnumerator CardPopInRoutine(RectTransform rt)
        {
            if (rt == null) yield break;
            float duration = 0.22f;
            float elapsed = 0f;
            Vector3 start = Vector3.one * 0.75f;
            Vector3 overshoot = Vector3.one * 1.05f;
            Vector3 target = Vector3.one;

            while (elapsed < duration)
            {
                if (rt == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (t < 0.7f)
                {
                    float subT = t / 0.7f;
                    rt.localScale = Vector3.LerpUnclamped(start, overshoot, Mathf.SmoothStep(0f, 1f, subT));
                }
                else
                {
                    float subT = (t - 0.7f) / 0.3f;
                    rt.localScale = Vector3.Lerp(overshoot, target, subT);
                }
                yield return null;
            }
            if (rt != null) rt.localScale = target;
            m_CardPopInCoroutine = null;
        }

        /// <summary>
        /// Pop-up'ı kapatır ve oyun içi ekranı tamamen serbest bırakır.
        /// Eğer güncelleme zorunluysa kapatmaya izin vermez.
        /// </summary>
        public void DismissPopup()
        {
            if (m_CurrentInfo != null && m_CurrentInfo.IsForce)
            {
                // Zorunlu güncelleme ise kapatılamaz
                Debug.Log("[InAppUpdateManager] Bu güncelleme zorunludur, kapatılamaz.");
                return;
            }

            if (m_CardPopInCoroutine != null)
            {
                StopCoroutine(m_CardPopInCoroutine);
                m_CardPopInCoroutine = null;
            }

            if (m_CurrentInfo != null && !m_CurrentInfo.IsForce)
            {
                PlayerPrefs.SetString(DismissedDatePrefKey, DateTime.UtcNow.ToString("yyyy-MM-dd"));
                PlayerPrefs.SetString(DismissedVersionPrefKey, m_CurrentInfo.LatestVersion);
                PlayerPrefs.Save();
            }

            if (m_PopupRoot != null)
            {
                m_PopupRoot.SetActive(false);
            }

            Debug.Log("[InAppUpdateManager] Güncelleme penceresi kapatıldı, oyun kontrolleri aktif.");
        }

        public void OpenStore()
        {
            string url = GetStoreUrl(m_CurrentInfo?.StoreUrl);
            Debug.Log($"[InAppUpdateManager] Mağaza açılıyor: {url}");

            try
            {
                Application.OpenURL(url);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InAppUpdateManager] Mağaza linki açılamadı: {ex.Message}");
                string fallbackUrl = "https://play.google.com/store/apps/details?id=" + Application.identifier;
                Application.OpenURL(fallbackUrl);
            }
        }

        public static string GetStoreUrl(string customUrl = "")
        {
            if (!string.IsNullOrEmpty(customUrl)) return customUrl;

            string pkg = Application.identifier;
            if (string.IsNullOrEmpty(pkg)) pkg = "com.fogboundgames.pixelflow";

#if UNITY_ANDROID && !UNITY_EDITOR
            return "market://details?id=" + pkg;
#elif UNITY_IOS && !UNITY_EDITOR
            return "https://apps.apple.com/app/id" + pkg;
#else
            return "https://play.google.com/store/apps/details?id=" + pkg;
#endif
        }

        private static Sprite LoadVioletSprite(string assetPath, string resourceName)
        {
            // 1. Önce Resources klasörünü dene (Çalışma zamanı / Build desteği)
            if (!string.IsNullOrEmpty(resourceName))
            {
                Sprite rSp = Resources.Load<Sprite>(resourceName);
                if (rSp != null) return rSp;
            }

#if UNITY_EDITOR
            // 2. AssetDatabase doğrudan Sprite yüklemeyi dene
            Sprite directSp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (directSp != null) return directSp;

            // 3. AssetDatabase alt varlıkları ara (Multiple Sprite Mode için)
            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (subAssets != null)
            {
                for (int i = 0; i < subAssets.Length; i++)
                {
                    if (subAssets[i] is Sprite sp) return sp;
                }
            }
#endif

            return null;
        }

        private void EnsurePopupUi()
        {
            if (m_CardPopInCoroutine != null)
            {
                StopCoroutine(m_CardPopInCoroutine);
                m_CardPopInCoroutine = null;
            }

            // Sahnedeki tüm eski InAppUpdate_Popup ve InAppUpdate_Canvas nesnelerini kökten temizle
            GameObject[] allGos = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allGos.Length; i++)
            {
                if (allGos[i] != null && (allGos[i].name == "InAppUpdate_Popup" || allGos[i].name == "InAppUpdate_Canvas"))
                {
                    if (Application.isPlaying) Destroy(allGos[i]);
                    else DestroyImmediate(allGos[i]);
                }
            }
            m_PopupRoot = null;

            // Bağımsız, en üst katmanda Overlay Canvas kur (Hiçbir 3D nesne veya HUD katmanı tıklamayı engelleyemez)
            GameObject canvasGo = new GameObject("InAppUpdate_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas targetCanvas = canvasGo.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 32767; // Unity'deki en yüksek katman (Her şeyin üstünde)

            GraphicRaycaster gr = canvasGo.GetComponent<GraphicRaycaster>();
            gr.blockingObjects = GraphicRaycaster.BlockingObjects.None;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            if (Application.isPlaying) DontDestroyOnLoad(canvasGo);

#if UNITY_EDITOR
            m_Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/LilitaOne-Regular SDF.asset");
            TMP_FontAsset fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (m_Font != null && fallbackFont != null)
            {
                if (m_Font.fallbackFontAssetTable == null) m_Font.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!m_Font.fallbackFontAssetTable.Contains(fallbackFont)) m_Font.fallbackFontAssetTable.Add(fallbackFont);
            }
#else
            m_Font = Resources.Load<TMP_FontAsset>("Fonts/LilitaOne-Regular SDF");
            TMP_FontAsset fallbackFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
#endif

            // 1. Fullscreen Dark Overlay (Karartma & Arka Plana Tıklayınca Kapanma)
            m_PopupRoot = new GameObject("InAppUpdate_Popup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            m_PopupRoot.transform.SetParent(targetCanvas.transform, false);

            RectTransform rootRt = (RectTransform)m_PopupRoot.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Image overlayImg = m_PopupRoot.GetComponent<Image>();
            overlayImg.color = new Color(0.03f, 0.07f, 0.12f, 0.85f);
            overlayImg.raycastTarget = true;

            Button overlayBtn = m_PopupRoot.GetComponent<Button>();
            overlayBtn.transition = Selectable.Transition.None;
            overlayBtn.onClick.AddListener(() =>
            {
                if (m_CurrentInfo != null && !m_CurrentInfo.IsForce)
                {
                    DismissPopup();
                }
            });

            // 2. Card Panel (Modern, ferah ve orantılı tasarım)
            GameObject cardGo = new GameObject("Update_Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            cardGo.transform.SetParent(m_PopupRoot.transform, false);

            m_CardRect = (RectTransform)cardGo.transform;
            m_CardRect.anchorMin = new Vector2(0.5f, 0.5f);
            m_CardRect.anchorMax = new Vector2(0.5f, 0.5f);
            m_CardRect.pivot = new Vector2(0.5f, 0.5f);
            m_CardRect.anchoredPosition = new Vector2(0f, 15f);
            m_CardRect.sizeDelta = new Vector2(660f, 500f);

            Image cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(0.10f, 0.15f, 0.24f, 0.98f); // Koyu lacivert/slate zemin
            cardImg.raycastTarget = true;

            Outline cardOutline = cardGo.GetComponent<Outline>();
            cardOutline.effectColor = new Color(0.25f, 0.45f, 0.80f, 0.85f);
            cardOutline.effectDistance = new Vector2(3f, -3f);

            // 3. Başlık (İkonsuz, Büyük, Belirgin ve Vurgulu)
            GameObject titleGo = new GameObject("Title_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(cardGo.transform, false);
            RectTransform titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, -62f);
            titleRt.sizeDelta = new Vector2(520f, 62f);

            m_TitleText = titleGo.GetComponent<TextMeshProUGUI>();
            if (m_Font != null) m_TitleText.font = m_Font;
            m_TitleText.fontSize = 44f;
            m_TitleText.fontStyle = FontStyles.Bold;
            m_TitleText.alignment = TextAlignmentOptions.Center;
            m_TitleText.color = new Color(1f, 0.84f, 0.20f); // Altın sarısı
            m_TitleText.outlineColor = Color.black;
            m_TitleText.outlineWidth = 0.28f;
            m_TitleText.text = "YENİ GÜNCELLEME!";

            // 4. Versiyon Hapı (v1.7 -> v1.8)
            GameObject verPillGo = new GameObject("Version_Pill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            verPillGo.transform.SetParent(cardGo.transform, false);
            RectTransform verPillRt = (RectTransform)verPillGo.transform;
            verPillRt.anchorMin = new Vector2(0.5f, 1f);
            verPillRt.anchorMax = new Vector2(0.5f, 1f);
            verPillRt.pivot = new Vector2(0.5f, 0.5f);
            verPillRt.anchoredPosition = new Vector2(0f, -122f);
            verPillRt.sizeDelta = new Vector2(340f, 42f);

            Image verPillImg = verPillGo.GetComponent<Image>();
            verPillImg.color = new Color(0.06f, 0.09f, 0.16f, 0.95f);

            Outline verPillOutline = verPillGo.GetComponent<Outline>();
            verPillOutline.effectColor = new Color(0.20f, 0.35f, 0.60f, 0.40f);
            verPillOutline.effectDistance = new Vector2(1f, -1f);

            GameObject verTextGo = new GameObject("Version_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            verTextGo.transform.SetParent(verPillGo.transform, false);
            RectTransform verTextRt = (RectTransform)verTextGo.transform;
            verTextRt.anchorMin = Vector2.zero;
            verTextRt.anchorMax = Vector2.one;
            verTextRt.offsetMin = Vector2.zero;
            verTextRt.offsetMax = Vector2.zero;

            m_VersionBadgeText = verTextGo.GetComponent<TextMeshProUGUI>();
            if (m_Font != null) m_VersionBadgeText.font = m_Font;
            m_VersionBadgeText.fontSize = 22f;
            m_VersionBadgeText.fontStyle = FontStyles.Bold;
            m_VersionBadgeText.alignment = TextAlignmentOptions.Center;
            m_VersionBadgeText.color = new Color(0.75f, 0.85f, 0.98f);
            m_VersionBadgeText.text = "v1.7 -> v1.8";

            // 5. Mesaj Açıklama Metni (Büyük, Ferah ve Okunaklı)
            GameObject msgGo = new GameObject("Message_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            msgGo.transform.SetParent(cardGo.transform, false);
            RectTransform msgRt = (RectTransform)msgGo.transform;
            msgRt.anchorMin = new Vector2(0.5f, 1f);
            msgRt.anchorMax = new Vector2(0.5f, 1f);
            msgRt.pivot = new Vector2(0.5f, 0.5f);
            msgRt.anchoredPosition = new Vector2(0f, -205f);
            msgRt.sizeDelta = new Vector2(580f, 105f);

            m_MessageText = msgGo.GetComponent<TextMeshProUGUI>();
            if (fallbackFont != null) m_MessageText.font = fallbackFont;
            else if (m_Font != null) m_MessageText.font = m_Font;
            m_MessageText.fontSize = 26f;
            m_MessageText.lineSpacing = 12f;
            m_MessageText.alignment = TextAlignmentOptions.Center;
            m_MessageText.color = new Color(0.90f, 0.94f, 1f);
            m_MessageText.text = "30 yeni piksel bölüm, taze çizimler ve görsel geliştirmeler eklendi!";

            // 6. GÜNCELLE Butonu (Geniş, Büyük, Canlı Yeşil)
            GameObject btnUpdateGo = new GameObject("Btn_Update", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            btnUpdateGo.transform.SetParent(cardGo.transform, false);
            RectTransform btnUpdateRt = (RectTransform)btnUpdateGo.transform;
            btnUpdateRt.anchorMin = new Vector2(0.5f, 0f);
            btnUpdateRt.anchorMax = new Vector2(0.5f, 0f);
            btnUpdateRt.pivot = new Vector2(0.5f, 0.5f);
            btnUpdateRt.anchoredPosition = new Vector2(0f, 105f);
            btnUpdateRt.sizeDelta = new Vector2(520f, 84f);

            Image btnUpdateImg = btnUpdateGo.GetComponent<Image>();
            btnUpdateImg.color = new Color(0.08f, 0.74f, 0.38f); // Canlı Zümrüt Yeşili (#15B760)

            Outline btnOutline = btnUpdateGo.GetComponent<Outline>();
            btnOutline.effectColor = new Color(0.05f, 0.45f, 0.22f);
            btnOutline.effectDistance = new Vector2(0f, -5f);

            m_UpdateButton = btnUpdateGo.GetComponent<Button>();
            m_UpdateButton.targetGraphic = btnUpdateImg;
            m_UpdateButton.onClick.AddListener(OpenStore);

            GameObject btnUpdateTextGo = new GameObject("Btn_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            btnUpdateTextGo.transform.SetParent(btnUpdateGo.transform, false);
            RectTransform btnUpdateTextRt = (RectTransform)btnUpdateTextGo.transform;
            btnUpdateTextRt.anchorMin = Vector2.zero;
            btnUpdateTextRt.anchorMax = Vector2.one;
            btnUpdateTextRt.offsetMin = Vector2.zero;
            btnUpdateTextRt.offsetMax = Vector2.zero;

            TextMeshProUGUI btnUpdateTmp = btnUpdateTextGo.GetComponent<TextMeshProUGUI>();
            if (m_Font != null) btnUpdateTmp.font = m_Font;
            btnUpdateTmp.fontSize = 36f;
            btnUpdateTmp.fontStyle = FontStyles.Bold;
            btnUpdateTmp.alignment = TextAlignmentOptions.Center;
            btnUpdateTmp.color = Color.white;
            btnUpdateTmp.outlineColor = Color.black;
            btnUpdateTmp.outlineWidth = 0.24f;
            btnUpdateTmp.text = "ŞİMDİ GÜNCELLE";

            // 7. DAHA SONRA Butonu (Tıklanabilir Şık Pill Buton)
            m_LaterButtonObj = new GameObject("Btn_Later", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            m_LaterButtonObj.transform.SetParent(cardGo.transform, false);
            RectTransform btnLaterRt = (RectTransform)m_LaterButtonObj.transform;
            btnLaterRt.anchorMin = new Vector2(0.5f, 0f);
            btnLaterRt.anchorMax = new Vector2(0.5f, 0f);
            btnLaterRt.pivot = new Vector2(0.5f, 0.5f);
            btnLaterRt.anchoredPosition = new Vector2(0f, 38f);
            btnLaterRt.sizeDelta = new Vector2(260f, 42f);

            Image btnLaterImg = m_LaterButtonObj.GetComponent<Image>();
            btnLaterImg.color = new Color(0.14f, 0.20f, 0.32f, 0.75f); // Tıklanabilir zemin
            btnLaterImg.raycastTarget = true;

            Outline laterOutline = m_LaterButtonObj.GetComponent<Outline>();
            laterOutline.effectColor = new Color(0.25f, 0.35f, 0.55f, 0.50f);
            laterOutline.effectDistance = new Vector2(1f, -1f);

            m_LaterButton = m_LaterButtonObj.GetComponent<Button>();
            m_LaterButton.targetGraphic = btnLaterImg;
            m_LaterButton.onClick.AddListener(DismissPopup);

            GameObject btnLaterTextGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            btnLaterTextGo.transform.SetParent(m_LaterButtonObj.transform, false);
            RectTransform btnLaterTextRt = (RectTransform)btnLaterTextGo.transform;
            btnLaterTextRt.anchorMin = Vector2.zero;
            btnLaterTextRt.anchorMax = Vector2.one;
            btnLaterTextRt.offsetMin = Vector2.zero;
            btnLaterTextRt.offsetMax = Vector2.zero;

            TextMeshProUGUI btnLaterTmp = btnLaterTextGo.GetComponent<TextMeshProUGUI>();
            if (fallbackFont != null) btnLaterTmp.font = fallbackFont;
            else if (m_Font != null) btnLaterTmp.font = m_Font;
            btnLaterTmp.fontSize = 22f;
            btnLaterTmp.alignment = TextAlignmentOptions.Center;
            btnLaterTmp.color = new Color(0.70f, 0.80f, 0.95f, 0.95f);
            btnLaterTmp.raycastTarget = false;
            btnLaterTmp.text = "Daha Sonra";

            // 8. ❌ Sağ Üst Kapatma Çarpı Butonu (Violet Theme UI Orijinal Sprite Entegreli)
            m_CloseButtonObj = new GameObject("Btn_Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            m_CloseButtonObj.transform.SetParent(cardGo.transform, false);
            RectTransform btnCloseRt = (RectTransform)m_CloseButtonObj.transform;
            btnCloseRt.anchorMin = new Vector2(1f, 1f);
            btnCloseRt.anchorMax = new Vector2(1f, 1f);
            btnCloseRt.pivot = new Vector2(0.5f, 0.5f);
            btnCloseRt.anchoredPosition = new Vector2(-26f, -26f);
            btnCloseRt.sizeDelta = new Vector2(58f, 58f);

            Image btnCloseImg = m_CloseButtonObj.GetComponent<Image>();
            Sprite violetRedBtnSprite = LoadVioletSprite("Assets/Violet Theme Ui/Buttons/Misc/Small Square Button Red.png", "ui_violet_red_button");
            if (violetRedBtnSprite != null)
            {
                btnCloseImg.sprite = violetRedBtnSprite;
                btnCloseImg.color = Color.white;
            }
            else
            {
                btnCloseImg.color = new Color(0.88f, 0.22f, 0.22f, 1.0f);
            }
            btnCloseImg.raycastTarget = true;

            m_CloseButton = m_CloseButtonObj.GetComponent<Button>();
            m_CloseButton.targetGraphic = btnCloseImg;
            m_CloseButton.onClick.AddListener(DismissPopup);

            // Violet White Close İkonu
            GameObject closeIconGo = new GameObject("Violet_Close_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            closeIconGo.transform.SetParent(m_CloseButtonObj.transform, false);
            RectTransform closeIconRt = (RectTransform)closeIconGo.transform;
            closeIconRt.anchorMin = new Vector2(0.5f, 0.5f);
            closeIconRt.anchorMax = new Vector2(0.5f, 0.5f);
            closeIconRt.pivot = new Vector2(0.5f, 0.5f);
            closeIconRt.anchoredPosition = new Vector2(0f, 2f); // Buton kabartmasına göre hafif yukarı merkezli
            closeIconRt.sizeDelta = new Vector2(34f, 34f);

            Image closeIconImg = closeIconGo.GetComponent<Image>();
            Sprite violetCloseIconSprite = LoadVioletSprite("Assets/Violet Theme Ui/White Icons/White Close.png", "ui_violet_close");
            if (violetCloseIconSprite != null)
            {
                closeIconImg.sprite = violetCloseIconSprite;
                closeIconImg.color = Color.white;
            }
            else
            {
                // Alternatif Colored Icons Close sprite'ı dene
                Sprite coloredClose = LoadVioletSprite("Assets/Violet Theme Ui/Colored Icons/Close.png", "");
                if (coloredClose != null) closeIconImg.sprite = coloredClose;
            }
            closeIconImg.preserveAspect = true;
            closeIconImg.raycastTarget = false; // Tıklamayı ana butonun alması için false

            // Çarpı butonunu en üst sıraya yerleştir
            m_CloseButtonObj.transform.SetAsLastSibling();

            m_PopupRoot.SetActive(false);
        }

#if UNITY_EDITOR
        [MenuItem("Tools/PixelGame/📱 Güncelleme Pop-up'ı (Normal - Kapatılabilir)", priority = 20)]
        public static void TestShowUpdatePopupNormal()
        {
            if (s_Instance == null)
            {
                var existing = UnityEngine.Object.FindFirstObjectByType<InAppUpdateManager>();
                if (existing != null) s_Instance = existing;
                else AutoBootstrap();
            }

            var sampleInfo = new UpdateInfo
            {
                LatestVersion = "1.8",
                MinRequiredVersion = "1.0",
                Title = "YENİ GÜNCELLEME!",
                Message = "30 yeni piksel bölüm, taze çizimler ve görsel geliştirmeler eklendi!",
                IsForce = false
            };

            s_Instance.ShowUpdatePopup(sampleInfo);
        }

        [MenuItem("Tools/PixelGame/🔒 Güncelleme Pop-up'ı (Zorunlu - Kapatılamaz)", priority = 21)]
        public static void TestShowUpdatePopupForced()
        {
            if (s_Instance == null)
            {
                var existing = UnityEngine.Object.FindFirstObjectByType<InAppUpdateManager>();
                if (existing != null) s_Instance = existing;
                else AutoBootstrap();
            }

            var sampleInfo = new UpdateInfo
            {
                LatestVersion = "2.0",
                MinRequiredVersion = "2.0",
                Title = "KRİTİK GÜNCELLEME!",
                Message = "Oyuna devam edebilmek için lütfen yeni sürümü indirin.",
                IsForce = true
            };

            s_Instance.ShowUpdatePopup(sampleInfo);
        }

        [MenuItem("Tools/PixelGame/❌ Güncelleme Pop-up'ını Kapat", priority = 22)]
        public static void TestDismissPopupManual()
        {
            if (s_Instance != null)
            {
                s_Instance.DismissPopup();
            }
            else
            {
                var popup = GameObject.Find("InAppUpdate_Popup");
                if (popup != null) popup.SetActive(false);
                var canvas = GameObject.Find("InAppUpdate_Canvas");
                if (canvas != null) canvas.SetActive(false);
            }
        }
#endif
    }
}
