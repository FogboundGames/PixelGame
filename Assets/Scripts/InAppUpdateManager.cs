using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        // Pop-up şu an gösterilmesi gereken durumda mı? (sahne yeniden yüklenince durumu korumak için)
        private bool m_IsPopupVisible;
        // Aynı anda birden fazla ağ isteği atılmasını engeller.
        private bool m_IsChecking;
        // Uygulama arka plandan dönünce tekrar kontrol için son kontrol zamanı (gerçek zaman).
        private float m_LastCheckRealtime = -1000f;
        private const float ResumeRecheckCooldown = 5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (s_Instance != null) return;
            var existing = UnityEngine.Object.FindFirstObjectByType<InAppUpdateManager>();
            if (existing != null)
            {
                s_Instance = existing;
                return;
            }
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

            BindExistingPopupIfPresent();
            HideScenePopupIfNotShowing();
        }

        private void OnEnable()
        {
            BindExistingPopupIfPresent();
            if (Application.isPlaying) SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            if (Application.isPlaying) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// Sahne yeniden yüklendiğinde (seviye restart vb.) sahnedeki pop-up yeniden oluşur.
        /// Önceki durumu (gösteriliyor / gizli) yeni pop-up'a uygula.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (s_Instance != this) return;
            m_PopupRoot = null;
            m_CloseButtonObj = null;
            BindExistingPopupIfPresent();

            if (m_IsPopupVisible && m_CurrentInfo != null) ShowUpdatePopup(m_CurrentInfo);
            else HideScenePopupIfNotShowing();
        }

        /// <summary>
        /// Sahneye gömülü pop-up varsayılan olarak aktif kaydedilmiş olabilir; güncelleme yoksa görünmemeli.
        /// </summary>
        private void HideScenePopupIfNotShowing()
        {
            if (!Application.isPlaying || m_IsPopupVisible) return;
            if (m_PopupRoot != null && m_PopupRoot.activeSelf) m_PopupRoot.SetActive(false);
        }

        /// <summary>
        /// Android/iOS'ta oyunu "kapatıp açmak" çoğu zaman uygulamayı yeniden başlatmaz, sadece arka plandan
        /// geri getirir; bu durumda Start() tekrar çalışmaz. Bu yüzden her ön plana dönüşte yeniden kontrol edilir.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused) RecheckOnResume();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) RecheckOnResume();
        }

        private void RecheckOnResume()
        {
            if (!Application.isPlaying || !m_CheckOnStart || s_Instance != this) return;
            if (Time.realtimeSinceStartup - m_LastCheckRealtime < ResumeRecheckCooldown) return;
            CheckForUpdates();
        }

        private void Start()
        {
            BindExistingPopupIfPresent();

            if (Application.isPlaying && m_CheckOnStart)
            {
                StartCoroutine(DelayedCheckRoutine());
            }
        }

        private void Update()
        {
            if (m_PopupRoot == null || !m_PopupRoot.activeSelf) return;

            // Zorunlu güncelleme ise kapatmaya izin verilmez
            if (m_CurrentInfo != null && m_CurrentInfo.IsForce) return;

            // Güvenli giriş algılama (New Input System ve Legacy Input uyumlu)
            if (GetPointerDown(out Vector2 screenPos))
            {
                // 1. Sağ Üst Çarpı Butonu Tıklaması
                if (m_CloseButtonObj != null && m_CloseButtonObj.activeSelf)
                {
                    RectTransform closeRt = (RectTransform)m_CloseButtonObj.transform;
                    if (RectTransformUtility.RectangleContainsScreenPoint(closeRt, screenPos, null))
                    {
                        DismissPopup();
                        return;
                    }
                }

                // 2. Kartın dışına (arka plan karartmasına) tıklayınca kapatma
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

        private bool GetPointerDown(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                screenPos = Pointer.current.position.ReadValue();
                return true;
            }
#endif
            return false;
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
            if (m_IsChecking) return;
            StartCoroutine(FetchVersionConfigRoutine(onComplete));
        }

        private IEnumerator FetchVersionConfigRoutine(Action<bool, UpdateInfo> onComplete)
        {
            m_IsChecking = true;
            m_LastCheckRealtime = Time.realtimeSinceStartup;
            try
            {
                yield return FetchVersionConfigCore(onComplete);
            }
            finally
            {
                m_IsChecking = false;
                m_LastCheckRealtime = Time.realtimeSinceStartup;
            }
        }

        private IEnumerator FetchVersionConfigCore(Action<bool, UpdateInfo> onComplete)
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
                    string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                    string dismissedDate = PlayerPrefs.GetString(DismissedDatePrefKey, "");
                    string dismissedVer = PlayerPrefs.GetString(DismissedVersionPrefKey, "");

                    if (!isForce && dismissedDate == today && dismissedVer == info.LatestVersion && !m_DebugForceShow)
                    {
                        Debug.Log("[InAppUpdateManager] Güncelleme mevcut ama kullanıcı bugün zaten kapattı.");
                        onComplete?.Invoke(false, info);
                        yield break;
                    }

                    ShowUpdatePopup(info);
                    onComplete?.Invoke(true, info);
                }
                else
                {
                    Debug.Log($"[InAppUpdateManager] Oyun sürümü güncel ({currentVersion}).");
                    // Panelden sürüm geri alındıysa açık kalan pop-up'ı kapat.
                    if (m_IsPopupVisible)
                    {
                        m_IsPopupVisible = false;
                        if (m_PopupRoot != null) m_PopupRoot.SetActive(false);
                    }
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

                // 2. Device (min version ve force bayrağı: min:1.0.0|force:1)
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

                // 3. Title ve Message
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
            // Zorunlu ise (IsForce == true) 'X' butonu görünmez!
            bool canDismiss = !m_CurrentInfo.IsForce;

            if (m_CloseButtonObj != null)
            {
                m_CloseButtonObj.SetActive(canDismiss);
            }

            m_PopupRoot.SetActive(true);
            m_IsPopupVisible = true;

            // Açılış animasyonu
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
        /// Pop-up'ı kapatır ve oyun içi kontrolleri tamamen aktif eder.
        /// </summary>
        public void DismissPopup()
        {
            if (m_CurrentInfo != null && m_CurrentInfo.IsForce)
            {
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

            m_IsPopupVisible = false;
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
            if (!string.IsNullOrEmpty(resourceName))
            {
                Sprite rSp = Resources.Load<Sprite>(resourceName);
                if (rSp != null) return rSp;
            }

#if UNITY_EDITOR
            Sprite directSp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (directSp != null) return directSp;

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

        private void BindExistingPopupIfPresent()
        {
            if (m_PopupRoot != null && m_CloseButtonObj != null) return;

            GameObject popup = GameObject.Find("InAppUpdate_Popup");
            if (popup == null)
            {
                var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < allGos.Length; i++)
                {
                    if (allGos[i] != null && allGos[i].name == "InAppUpdate_Popup" && allGos[i].scene.isLoaded)
                    {
                        popup = allGos[i];
                        break;
                    }
                }
            }

            if (popup != null)
            {
                m_PopupRoot = popup;
                ConfigureExistingPopup(popup);
            }
        }

        private void ConfigureExistingPopup(GameObject popup)
        {
            m_PopupRoot = popup;

            // Arka plan overlay butonuna tıklayınca kapatma
            Button overlayBtn = popup.GetComponent<Button>();
            if (overlayBtn != null)
            {
                overlayBtn.onClick.RemoveAllListeners();
                overlayBtn.onClick.AddListener(() =>
                {
                    if (m_CurrentInfo != null && !m_CurrentInfo.IsForce)
                    {
                        DismissPopup();
                    }
                });
            }

            Transform cardTr = popup.transform.Find("Update_Card");
            if (cardTr == null) return;
            m_CardRect = (RectTransform)cardTr;

            // 'Daha Sonra' butonunu tamamen kaldır
            Transform laterTr = cardTr.Find("Btn_Later");
            if (laterTr != null)
            {
                if (Application.isPlaying) Destroy(laterTr.gameObject);
                else DestroyImmediate(laterTr.gameObject);
            }

            // 'ŞİMDİ GÜNCELLE' butonunu aşağıya doğru ortala ve bağla
            Transform updateTr = cardTr.Find("Btn_Update");
            if (updateTr != null)
            {
                RectTransform updateRt = (RectTransform)updateTr;
                updateRt.anchoredPosition = new Vector2(0f, 65f); // Daha Sonra kalktığı için dengeli konum
                m_UpdateButton = updateTr.GetComponent<Button>();
                if (m_UpdateButton != null)
                {
                    m_UpdateButton.onClick.RemoveAllListeners();
                    m_UpdateButton.onClick.AddListener(OpenStore);
                }
            }

            // Kullanıcının eklediği / özelleştirdiği Çarpı ('✕') Butonu
            Transform closeTr = cardTr.Find("Btn_Close");
            if (closeTr == null) closeTr = popup.transform.Find("Btn_Close");

            if (closeTr != null)
            {
                m_CloseButtonObj = closeTr.gameObject;
                m_CloseButton = closeTr.GetComponent<Button>();
                if (m_CloseButton == null) m_CloseButton = closeTr.gameObject.AddComponent<Button>();

                Image closeImg = closeTr.GetComponent<Image>();
                if (closeImg != null)
                {
                    closeImg.raycastTarget = true;
                    m_CloseButton.targetGraphic = closeImg;
                }

                // Tıklayınca DismissPopup fonksiyonunu çağır
                m_CloseButton.onClick.RemoveAllListeners();
                m_CloseButton.onClick.AddListener(DismissPopup);

                // En üst katmana getir (hiçbir nesne arkasında kalmasın)
                closeTr.SetAsLastSibling();
            }

            // Metin referanslarını bağla
            Transform titleTr = cardTr.Find("Title_Text");
            if (titleTr != null) m_TitleText = titleTr.GetComponent<TextMeshProUGUI>();

            Transform pillTr = cardTr.Find("Version_Pill/Version_Text");
            if (pillTr != null) m_VersionBadgeText = pillTr.GetComponent<TextMeshProUGUI>();

            Transform msgTr = cardTr.Find("Message_Text");
            if (msgTr != null) m_MessageText = msgTr.GetComponent<TextMeshProUGUI>();
        }

        private void EnsurePopupUi()
        {
            if (m_CardPopInCoroutine != null)
            {
                StopCoroutine(m_CardPopInCoroutine);
                m_CardPopInCoroutine = null;
            }

            // 1. Sahnede zaten var olan bir InAppUpdate_Popup varsa onu doğrudan kullan ve özelleştir
            GameObject existingPopup = GameObject.Find("InAppUpdate_Popup");
            if (existingPopup == null)
            {
                var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < allGos.Length; i++)
                {
                    if (allGos[i] != null && allGos[i].name == "InAppUpdate_Popup" && allGos[i].scene.isLoaded)
                    {
                        existingPopup = allGos[i];
                        break;
                    }
                }
            }

            if (existingPopup != null)
            {
                ConfigureExistingPopup(existingPopup);
                return;
            }

            // 2. Sahnede yoksa sıfırdan oluştur
            GameObject canvasGo = new GameObject("InAppUpdate_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas targetCanvas = canvasGo.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 32767;

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

            // Karartma Overlay
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

            // Card Panel
            GameObject cardGo = new GameObject("Update_Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            cardGo.transform.SetParent(m_PopupRoot.transform, false);

            m_CardRect = (RectTransform)cardGo.transform;
            m_CardRect.anchorMin = new Vector2(0.5f, 0.5f);
            m_CardRect.anchorMax = new Vector2(0.5f, 0.5f);
            m_CardRect.pivot = new Vector2(0.5f, 0.5f);
            m_CardRect.anchoredPosition = new Vector2(0f, 15f);
            m_CardRect.sizeDelta = new Vector2(660f, 480f);

            Image cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(0.10f, 0.15f, 0.24f, 0.98f);
            cardImg.raycastTarget = true;

            Outline cardOutline = cardGo.GetComponent<Outline>();
            cardOutline.effectColor = new Color(0.25f, 0.45f, 0.80f, 0.85f);
            cardOutline.effectDistance = new Vector2(3f, -3f);

            // Başlık
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
            m_TitleText.color = new Color(1f, 0.84f, 0.20f);
            m_TitleText.outlineColor = Color.black;
            m_TitleText.outlineWidth = 0.28f;
            m_TitleText.text = "YENİ GÜNCELLEME!";

            // Versiyon Hapı
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

            // Mesaj Metni
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

            // GÜNCELLE Butonu (Daha Sonra yazısı olmadan tek, büyük ve ortalanmış)
            GameObject btnUpdateGo = new GameObject("Btn_Update", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            btnUpdateGo.transform.SetParent(cardGo.transform, false);
            RectTransform btnUpdateRt = (RectTransform)btnUpdateGo.transform;
            btnUpdateRt.anchorMin = new Vector2(0.5f, 0f);
            btnUpdateRt.anchorMax = new Vector2(0.5f, 0f);
            btnUpdateRt.pivot = new Vector2(0.5f, 0.5f);
            btnUpdateRt.anchoredPosition = new Vector2(0f, 65f);
            btnUpdateRt.sizeDelta = new Vector2(520f, 88f);

            Image btnUpdateImg = btnUpdateGo.GetComponent<Image>();
            btnUpdateImg.color = new Color(0.08f, 0.74f, 0.38f);

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

            // ❌ Sağ Üst Kapatma Çarpı Butonu (Violet Theme UI Close İkonlu)
            m_CloseButtonObj = new GameObject("Btn_Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            m_CloseButtonObj.transform.SetParent(cardGo.transform, false);
            RectTransform btnCloseRt = (RectTransform)m_CloseButtonObj.transform;
            btnCloseRt.anchorMin = new Vector2(1f, 1f);
            btnCloseRt.anchorMax = new Vector2(1f, 1f);
            btnCloseRt.pivot = new Vector2(0.5f, 0.5f);
            btnCloseRt.anchoredPosition = new Vector2(-26f, -26f);
            btnCloseRt.sizeDelta = new Vector2(58f, 58f);

            Image btnCloseImg = m_CloseButtonObj.GetComponent<Image>();
            Sprite closeSprite = LoadVioletSprite("Assets/Violet Theme Ui/Colored Icons/Close.png", "ui_violet_close");
            if (closeSprite != null)
            {
                btnCloseImg.sprite = closeSprite;
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
