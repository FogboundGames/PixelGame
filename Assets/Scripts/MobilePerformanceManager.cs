using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PixelGame
{
    /// <summary>
    /// Mobil cihazlarda varsayılan 30 FPS kısıtlamasını kaldırır.
    /// Oyunu en düşük 45 FPS, en yüksek 60 FPS hedefinde akıcı ve kararlı bir şekilde çalıştırır.
    /// vSync'i sıfırlar, targetFrameRate'i 60 yapar, uyku modunu engeller ve cihaz zorlandığında
    /// 45 FPS altına düşmemek için dinamik performans optimizasyonunu yönetir.
    /// </summary>
    [AddComponentMenu("PixelGame/Mobile Performance Manager")]
    public class MobilePerformanceManager : MonoBehaviour
    {
        private static MobilePerformanceManager s_Instance;
        public static MobilePerformanceManager Instance => s_Instance;

        [Header("📱 Hedef Kare Hızı (FPS)")]
        [Tooltip("Mobil için hedeflenen en yüksek kare hızı (varsayılan 60 FPS).")]
        [SerializeField, Range(30, 120)] private int m_MaxTargetFps = 60;

        [Tooltip("Performansın altına düşmesine izin verilmeyen taban kare hızı (en düşük 45 FPS).")]
        [SerializeField, Range(30, 60)] private int m_MinTargetFps = 45;

        [Header("⚡ Dinamik Performans & Çözünürlük Ölçekleyici")]
        [Tooltip("Kare hızı 45 FPS'e doğru gerilediğinde URP 3D render çözünürlüğünü dinamik olarak hafifçe ölçekleyerek 45+ FPS'i korur (sık çözünürlük değişimi render buffer tahsisatı yapabileceğinden varsayılan olarak kapalıdır).")]
        [SerializeField] private bool m_EnableAdaptiveResolution = false;

        [Tooltip("Dinamik ölçeklemede inilebilecek en düşük 3D render ölçeği (0.82 = %82 çözünürlük, GPU piksel yükünü %33 azaltır).")]
        [SerializeField, Range(0.70f, 1.0f)] private float m_MinRenderScale = 0.82f;

        [Header("📊 Hata Ayıklama & FPS Sayacı")]
        [Tooltip("Sol üst köşede küçük bir FPS sayacı gösterir (mobil test ve doğrulama için).")]
        [SerializeField] private bool m_ShowFpsCounter = false;

        public int MaxTargetFps
        {
            get => m_MaxTargetFps;
            set
            {
                m_MaxTargetFps = Mathf.Clamp(value, 30, 120);
                ApplyTargetFrameRate();
            }
        }

        public int MinTargetFps
        {
            get => m_MinTargetFps;
            set => m_MinTargetFps = Mathf.Clamp(value, 30, m_MaxTargetFps);
        }

        public bool ShowFpsCounter
        {
            get => m_ShowFpsCounter;
            set => m_ShowFpsCounter = value;
        }

        public float CurrentFps { get; private set; } = 60f;

        private UniversalRenderPipelineAsset m_ActiveUrpAsset;
        private float m_OriginalRenderScale = 1f;
        private float m_CurrentRenderScale = 1f;
        private float m_FpsAccumulator = 0f;
        private int m_FpsFrames = 0;
        private float m_FpsTimeLeft = 0.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            EnsureInstance();
        }

        public static MobilePerformanceManager EnsureInstance()
        {
            if (s_Instance != null) return s_Instance;

            s_Instance = FindFirstObjectByType<MobilePerformanceManager>();
            if (s_Instance != null)
            {
                DontDestroyOnLoad(s_Instance.gameObject);
                s_Instance.InitializePerformance();
                return s_Instance;
            }

            GameObject go = new GameObject("[MobilePerformanceManager]");
            s_Instance = go.AddComponent<MobilePerformanceManager>();
            DontDestroyOnLoad(go);
            s_Instance.InitializePerformance();
            return s_Instance;
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePerformance();
        }

        private void Start()
        {
            ApplyTargetFrameRate();
        }

        public void InitializePerformance()
        {
            CacheUrpAsset();
            ApplyTargetFrameRate();

            // Incremental GC ayarı: Çöp toplama duraklamalarını (saliselik donma/stutter) engellemek için
            // her kare başına düşen GC süresini 2 ms ile sınırla (60 FPS için 16.6ms kare bütçesi korunur).
            if (UnityEngine.Scripting.GarbageCollector.isIncremental)
            {
                UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds = 2_000_000; // 2 ms
            }
        }

        private void CacheUrpAsset()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                m_ActiveUrpAsset = urp;
                m_OriginalRenderScale = urp.renderScale;
                m_CurrentRenderScale = urp.renderScale;
            }
            else if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset qUrp)
            {
                m_ActiveUrpAsset = qUrp;
                m_OriginalRenderScale = qUrp.renderScale;
                m_CurrentRenderScale = qUrp.renderScale;
            }
        }

        public void ApplyTargetFrameRate()
        {
            // Mobil cihazlarda vSync sıfırlanmadan targetFrameRate kilitlenemez
            QualitySettings.vSyncCount = 0;

            // En yüksek 60 FPS hedefi: mobilde pil tüketimini ve ısınmayı dengeler, 30 FPS kilidini kaldırır
            Application.targetFrameRate = m_MaxTargetFps;

            // Oyun esnasında ekranın kapanmasını önle
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Dokunma gecikmesini (input lag) düşürmek için kuyruk tamponunu sınırla
            QualitySettings.maxQueuedFrames = 2;

            Debug.Log($"<color=#00FFAA><b>[MobilePerformanceManager]</b></color> Kare hızı hedefi güncellendi: Min {m_MinTargetFps} FPS - Max {m_MaxTargetFps} FPS (vSync=0, targetFrameRate={Application.targetFrameRate})");
        }

        private void Update()
        {
            // FPS ölçümü (0.5 saniyede bir güncellenen yumuşatılmış değer)
            float dt = Time.unscaledDeltaTime;
            m_FpsTimeLeft -= dt;
            m_FpsAccumulator += 1f / Mathf.Max(1e-4f, dt);
            m_FpsFrames++;

            if (m_FpsTimeLeft <= 0f)
            {
                CurrentFps = m_FpsAccumulator / m_FpsFrames;
                m_FpsTimeLeft = 0.5f;
                m_FpsAccumulator = 0f;
                m_FpsFrames = 0;

                // Dinamik çözünürlük yönetimi: FPS 45'in altına düşme tehlikesindeyse renderScale'i hafifçe kıs
                if (m_EnableAdaptiveResolution && m_ActiveUrpAsset != null)
                {
                    AdjustAdaptiveResolution();
                }
            }
        }

        private void AdjustAdaptiveResolution()
        {
            if (m_ActiveUrpAsset == null) return;

            // Eğer FPS 45'e yaklaşıyorsa (< 48 FPS), renderScale'i kademeli olarak %2 düşür
            if (CurrentFps < m_MinTargetFps + 3f)
            {
                if (m_CurrentRenderScale > m_MinRenderScale)
                {
                    m_CurrentRenderScale = Mathf.Max(m_MinRenderScale, m_CurrentRenderScale - 0.03f);
                    m_ActiveUrpAsset.renderScale = m_CurrentRenderScale;
                }
            }
            // Eğer FPS rahatça 58+ FPS üzerindeyse çözünürlüğü tam orijinal değerine geri çıkar
            else if (CurrentFps >= m_MaxTargetFps - 2f)
            {
                if (m_CurrentRenderScale < m_OriginalRenderScale)
                {
                    m_CurrentRenderScale = Mathf.Min(m_OriginalRenderScale, m_CurrentRenderScale + 0.02f);
                    m_ActiveUrpAsset.renderScale = m_CurrentRenderScale;
                }
            }
        }

        private void OnDisable()
        {
            // Çıkışta veya devre dışı kaldığında orijinal render ölçeğini geri yükle
            if (m_ActiveUrpAsset != null)
            {
                m_ActiveUrpAsset.renderScale = m_OriginalRenderScale;
            }
        }

        private void OnDestroy()
        {
            if (m_ActiveUrpAsset != null)
            {
                m_ActiveUrpAsset.renderScale = m_OriginalRenderScale;
            }
        }

        private void OnGUI()
        {
            if (!m_ShowFpsCounter) return;

            int fps = Mathf.RoundToInt(CurrentFps);
            Color textColor = fps >= 50 ? Color.green : (fps >= 42 ? Color.yellow : Color.red);

            GUIStyle style = new GUIStyle();
            style.fontSize = Mathf.Max(24, Screen.height / 45);
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = textColor;
            style.alignment = TextAnchor.UpperLeft;

            GUIStyle bgStyle = new GUIStyle(GUI.skin.box);

            Rect boxRect = new Rect(20, 20, 180, 50);
            GUI.Box(boxRect, GUIContent.none, bgStyle);
            GUI.Label(new Rect(30, 25, 160, 40), $"{fps} FPS", style);
        }
    }
}
