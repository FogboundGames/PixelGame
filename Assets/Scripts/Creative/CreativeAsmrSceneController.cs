using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelGame.Creative
{
    [System.Serializable]
    public class CreativeSailorData
    {
        public string Name = "Sailor";
        public GameObject SailorObject;
        public Color TeamColor = Color.white;
        public List<PixelCube> AssignedCubes = new List<PixelCube>();
        public Vector3 DropOffset = new Vector3(0f, -0.55f, 0f);
        public Vector3 FinalFacingDirection = Vector3.up;

        [HideInInspector] public SailorRunnerVisual Visual;
        [HideInInspector] public CarriedCargoStack CargoStack;
        [HideInInspector] public Vector3 InitialPosition;
        [HideInInspector] public Coroutine ActiveRoutine;
        [HideInInspector] public int CollectedCount = 0;
    }

    /// <summary>
    /// ASMR Kreatif Çekimi için özel sahne yöneticisi.
    /// Gemiciler 3. fotoğrafta belirtilen sol başlangıç noktasından başlayarak
    /// soldan sağa (+X yönünde) koşar ve kendi renklerindeki küpleri (Pembe, Sarı, Mavi)
    /// eş zamanlı olarak toplayarak ellerinde dengeli 10 katlı kuleler oluştururlar.
    /// </summary>
    public class CreativeAsmrSceneController : MonoBehaviour
    {
        [Header("🚢 Denizciler (Çoklu Gemici Desteği)")]
        [SerializeField] private List<CreativeSailorData> m_Sailors = new List<CreativeSailorData>();
        [Tooltip("ASMR için optimize edilmiş sakin ve akıcı yürüme hızı")]
        [SerializeField] private float m_WalkSpeed = 1.08f;
        [SerializeField] private float m_SpeedMultiplier = 1.0f;
        [Tooltip("Gemicilerin küp alma anları arasındaki mikrosaniye gecikmesi (Melodik ASMR akoru için)")]
        [SerializeField] private float m_SailorStagger = 0.12f;
        [SerializeField] private float m_GroundZ = 0.42f;

        [Header("📦 Konteyner & Teslimat (Container Delivery)")]
        [SerializeField] private Transform m_ContainerTransform;
        [SerializeField] private TMPro.TextMeshProUGUI m_ContainerBadgeText;
        [SerializeField] private Vector2 m_BadgeAnchoredPosition = new Vector2(0f, 21f);
        [SerializeField] private UnityEngine.UI.Image m_ContainerImage;
        [SerializeField] private int m_ContainerCapacity = 16;
        [SerializeField] private float m_DeliveryDelayPerCube = 0.26f;
        [SerializeField] private Vector3 m_ContainerDropOffset = new Vector3(0f, -0.40f, 0f);

        [Header("🎬 Otomatik Oynatma")]
        [SerializeField] private bool m_AutoStartOnPlay = true;
        [SerializeField] private bool m_AutoLoop = true;
        [SerializeField] private float m_StartDelay = 0.04f;
        [SerializeField] private float m_EndDelay = 2.4f;

        [Header("🎥 Kamera")]
        [SerializeField] private CreativeThirdPersonCamera m_Camera;
        [SerializeField] private Transform m_CameraCenterAnchor;
        [Tooltip("Kameranın doğrudan takip edeceği gemici indeksi (1 = Orta Hero Gemici)")]
        [SerializeField] private int m_CameraFollowSailorIndex = 1;

        // Küplerin orijinal durumları (tekrar başlatmak için)
        private struct CubeInitData
        {
            public PixelCube Cube;
            public Vector3 OriginalPosition;
            public Color OriginalColor;
            public Vector3 OriginalScale;
        }
        private readonly List<CubeInitData> m_InitialData = new List<CubeInitData>();
        private Coroutine m_MasterRoutine;
        private bool m_IsPaused = false;
        private bool m_ShowHelp = true;
        private Vector3 m_ContainerOriginalScale = Vector3.one;
        private int m_TotalDeliveredCount = 0;
        private int m_DeliveredToContainerCount = 0;

        public List<CreativeSailorData> Sailors => m_Sailors;
        public int CameraFollowSailorIndex { get => m_CameraFollowSailorIndex; set => m_CameraFollowSailorIndex = value; }
        public CreativeThirdPersonCamera Camera { get => m_Camera; set => m_Camera = value; }
        public Transform CameraCenterAnchor { get => m_CameraCenterAnchor; set => m_CameraCenterAnchor = value; }
        public Transform ContainerTransform { get => m_ContainerTransform; set => m_ContainerTransform = value; }
        public TMPro.TextMeshProUGUI ContainerBadgeText { get => m_ContainerBadgeText; set => m_ContainerBadgeText = value; }
        public Vector2 BadgeAnchoredPosition { get => m_BadgeAnchoredPosition; set => m_BadgeAnchoredPosition = value; }
        public UnityEngine.UI.Image ContainerImage { get => m_ContainerImage; set => m_ContainerImage = value; }
        public int ContainerCapacity { get => m_ContainerCapacity; set => m_ContainerCapacity = value; }
        public Vector3 ContainerDropOffset { get => m_ContainerDropOffset; set => m_ContainerDropOffset = value; }

        [Header("🎊 Kutlama & Konfeti Efekti")]
        [SerializeField] private ParticleSystem m_ConfettiPS;
        [SerializeField] private GameObject m_PerfectStamp;
        private TMPro.TextMeshProUGUI m_StampText;
        private bool m_HasCelebrated = false;
        private static Texture2D s_ConfettiTexture;

        public ParticleSystem ConfettiPS { get => m_ConfettiPS; set => m_ConfettiPS = value; }
        public GameObject PerfectStamp { get => m_PerfectStamp; set => m_PerfectStamp = value; }

        private void Awake()
        {
            if (m_CameraCenterAnchor == null)
            {
                var anchorGo = new GameObject("CameraCenterAnchor");
                m_CameraCenterAnchor = anchorGo.transform;
            }

            if (m_Camera == null)
            {
                m_Camera = Object.FindFirstObjectByType<CreativeThirdPersonCamera>();
            }

            if (m_Camera != null)
            {
                m_Camera.Target = m_CameraCenterAnchor;
            }

            if (m_ContainerTransform != null)
            {
                m_ContainerOriginalScale = m_ContainerTransform.localScale;
            }

            if (m_ContainerBadgeText != null)
            {
                m_ContainerBadgeText.rectTransform.anchoredPosition = m_BadgeAnchoredPosition;
                m_ContainerBadgeText.fontStyle = TMPro.FontStyles.Bold;
                m_ContainerBadgeText.outlineWidth = 0.28f;
                m_ContainerBadgeText.outlineColor = Color.black;
            }
            UpdateContainerBadge(0);

            InitSailors();
            CacheInitialCubeData();
            EnsureConfettiSystem();
            UpdateCameraCenterAnchor(true);
        }

        private void OnValidate()
        {
            if (m_ContainerBadgeText != null)
            {
                m_ContainerBadgeText.rectTransform.anchoredPosition = m_BadgeAnchoredPosition;
            }
        }

        public void UpdateContainerBadge(int deliveredCount)
        {
            if (m_ContainerBadgeText != null)
            {
                int remaining = Mathf.Max(0, m_ContainerCapacity - deliveredCount);
                m_ContainerBadgeText.text = remaining.ToString();

                if (remaining == 0)
                {
                    m_ContainerBadgeText.color = new Color(0.25f, 1f, 0.45f, 1f); // Hafif yeşil tatlı zafer tonu
                }
                else
                {
                    m_ContainerBadgeText.color = Color.white;
                }

                if (deliveredCount > 0)
                {
                    // 💓 Sayaç Kalp Atışı (Pulse) - Sadece sayı rakamı büyüyüp küçülür
                    m_ContainerBadgeText.transform.DOKill();
                    m_ContainerBadgeText.transform.localScale = Vector3.one * 1.35f;
                    m_ContainerBadgeText.transform.DOScale(Vector3.one, 0.14f).SetEase(Ease.OutBack);
                }
                else
                {
                    m_ContainerBadgeText.transform.localScale = Vector3.one;
                }
            }
        }

        private void Start()
        {
            if (HypercasualFeedbackManager.Instance != null)
            {
                HypercasualFeedbackManager.Instance.AsmrCreativeOnlyPickupSound = true;
                HypercasualFeedbackManager.Instance.AsmrUsePureProceduralSound = false;
                HypercasualFeedbackManager.Instance.AsmrPickupVolume = 0.35f;
            }

            if (m_AutoStartOnPlay)
            {
                StartCreativeSequence();
            }
        }

        private void Update()
        {
            UpdateCameraCenterAnchor(false);

            var kb = Keyboard.current;
            if (kb == null) return;

            // R: Baştan başlat
            if (kb.rKey.wasPressedThisFrame)
            {
                RestartSequence();
            }

            // Boşluk: Duraklat / Devam et
            if (kb.spaceKey.wasPressedThisFrame)
            {
                m_IsPaused = !m_IsPaused;
            }

            // H: Bilgi kutusunu gizle / göster
            if (kb.hKey.wasPressedThisFrame)
            {
                m_ShowHelp = !m_ShowHelp;
            }

            // [ ve ] : Hız ayarı
            if (kb.leftBracketKey.wasPressedThisFrame)
            {
                m_SpeedMultiplier = Mathf.Max(0.5f, m_SpeedMultiplier - 0.25f);
            }
            if (kb.rightBracketKey.wasPressedThisFrame)
            {
                m_SpeedMultiplier = Mathf.Min(3.0f, m_SpeedMultiplier + 0.25f);
            }

            // T: Ses Modunu Değiştir (Orijinal Kenney impactSoft vs ASMR Melodik Puf)
            if (kb.tKey.wasPressedThisFrame)
            {
                if (HypercasualFeedbackManager.Instance != null)
                {
                    HypercasualFeedbackManager.Instance.AsmrUsePureProceduralSound = !HypercasualFeedbackManager.Instance.AsmrUsePureProceduralSound;
                }
            }
        }

        public void InitSailors()
        {
            foreach (var sailor in m_Sailors)
            {
                if (sailor.SailorObject == null) continue;

                sailor.Visual = sailor.SailorObject.GetComponent<SailorRunnerVisual>();
                sailor.InitialPosition = sailor.SailorObject.transform.position;
                sailor.CollectedCount = 0;

                if (sailor.Visual != null)
                {
                    sailor.Visual.TiltTowardsCamera = 0f;
                    sailor.Visual.SnapFacing(Vector3.up);
                    sailor.Visual.Play(SailorRunnerVisual.StateIdle);
                }

                ApplySailorVisualColor(sailor.SailorObject, sailor.TeamColor, m_GroundZ);
            }
        }

        private void UpdateCameraCenterAnchor(bool snap)
        {
            if (m_CameraCenterAnchor == null || m_Sailors.Count == 0) return;

            Transform targetTransform = null;
            if (m_CameraFollowSailorIndex >= 0 && m_CameraFollowSailorIndex < m_Sailors.Count && m_Sailors[m_CameraFollowSailorIndex].SailorObject != null)
            {
                targetTransform = m_Sailors[m_CameraFollowSailorIndex].SailorObject.transform;
            }
            else
            {
                foreach (var s in m_Sailors)
                {
                    if (s.SailorObject != null) { targetTransform = s.SailorObject.transform; break; }
                }
            }

            if (targetTransform != null)
            {
                m_CameraCenterAnchor.position = targetTransform.position;
                if (snap && m_Camera != null)
                {
                    m_Camera.SnapToTarget();
                }
            }
        }

        public void CacheInitialCubeData()
        {
            m_InitialData.Clear();
            foreach (var s in m_Sailors)
            {
                foreach (var cube in s.AssignedCubes)
                {
                    if (cube != null)
                    {
                        m_InitialData.Add(new CubeInitData
                        {
                            Cube = cube,
                            OriginalPosition = cube.transform.position,
                            OriginalColor = cube.CurrentColor,
                            OriginalScale = cube.transform.localScale
                        });
                    }
                }
            }
        }

        public void RestartSequence()
        {
            if (m_MasterRoutine != null)
            {
                StopCoroutine(m_MasterRoutine);
                m_MasterRoutine = null;
            }

            foreach (var s in m_Sailors)
            {
                if (s.ActiveRoutine != null)
                {
                    StopCoroutine(s.ActiveRoutine);
                    s.ActiveRoutine = null;
                }

                if (s.CargoStack != null)
                {
                    Destroy(s.CargoStack.gameObject);
                    s.CargoStack = null;
                }

                if (s.SailorObject != null)
                {
                    s.SailorObject.transform.position = s.InitialPosition;
                    if (s.Visual != null)
                    {
                        s.Visual.TiltTowardsCamera = 0f;
                        s.Visual.SnapFacing(Vector3.up);
                        s.Visual.Play(SailorRunnerVisual.StateIdle);
                    }
                }
                s.CollectedCount = 0;
            }

            // Küpleri geri yükle
            foreach (var data in m_InitialData)
            {
                if (data.Cube != null)
                {
                    data.Cube.gameObject.SetActive(true);
                    data.Cube.transform.position = data.OriginalPosition;
                    data.Cube.transform.localScale = data.OriginalScale;
                    data.Cube.ApplyColor(data.OriginalColor, 0f);
                }
            }

            if (m_ContainerTransform != null)
            {
                m_ContainerTransform.DOKill();
                m_ContainerTransform.localScale = m_ContainerOriginalScale;
            }
            m_TotalDeliveredCount = 0;
            m_DeliveredToContainerCount = 0;
            m_HasCelebrated = false;
            if (m_ConfettiPS != null) m_ConfettiPS.Clear();
            if (m_PerfectStamp != null) m_PerfectStamp.SetActive(false);
            UpdateContainerBadge(0);

            UpdateCameraCenterAnchor(true);
            StartCreativeSequence();
        }

        public void StartCreativeSequence()
        {
            if (m_MasterRoutine != null) StopCoroutine(m_MasterRoutine);
            m_MasterRoutine = StartCoroutine(MasterSequenceRoutine());
        }

        private IEnumerator MasterSequenceRoutine()
        {
            foreach (var s in m_Sailors)
            {
                if (s.Visual != null)
                {
                    s.Visual.TiltTowardsCamera = 0f;
                    s.Visual.SnapFacing(Vector3.up);
                    s.Visual.Play(SailorRunnerVisual.StateIdle);
                }
            }

            // 🎥 Kamera başlangıçta denizcilere doğrudan yakın ve alçak açıdan başlasın (ASMR yakınlığı - ilk saniyede anında hareket kancası)
            if (m_Camera != null)
            {
                m_Camera.SnapToTarget();
                m_Camera.AnimateZoom(new Vector3(0f, -1.80f, -2.55f), 42f, 0.01f);
            }

            yield return new WaitForSeconds(m_StartDelay);

            int runningCount = m_Sailors.Count;
            for (int i = 0; i < m_Sailors.Count; i++)
            {
                var sailor = m_Sailors[i];
                sailor.ActiveRoutine = StartCoroutine(SailorCollectionRoutine(sailor, i, () => runningCount--));
            }

            while (runningCount > 0)
            {
                yield return null;
            }

            // 🎊 Her ihtimale karşı tüm denizciler bitirdiğinde kutlama tetiklenmemişse:
            if (!m_HasCelebrated)
            {
                TriggerCelebrationSequence();
            }

            // 🌟 Kutlama patlamasının ilk 1.5 saniyesini canlı yaşat (PERFECT! damgası ve konfetiler)
            yield return new WaitForSeconds(1.5f);

            // 🧹 Kapanış kartında arka planın tertemiz görünmesi için PERFECT! yazısını pürüzsüzce küçültüp gizle
            if (m_PerfectStamp != null)
            {
                m_PerfectStamp.transform.DOKill();
                m_PerfectStamp.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack).OnComplete(() =>
                {
                    if (m_PerfectStamp != null) m_PerfectStamp.SetActive(false);
                });
            }

            // Konfetilerin tamamen dağılıp geride sadece dopdolu gururlu konteyner ve zafer pozu veren tatlı gemicilerin kalmasını bekle (0.75s)
            yield return new WaitForSeconds(0.75f);

            if (CreativeSceneVideoRecorder.Instance != null && CreativeSceneVideoRecorder.Instance.IsRecording)
            {
                CreativeSceneVideoRecorder.Instance.StopRecording();
                yield break;
            }

            if (m_AutoLoop)
            {
                RestartSequence();
            }
        }

        private IEnumerator SailorCollectionRoutine(CreativeSailorData sailor, int sailorIndex, System.Action onComplete)
        {
            if (sailor.SailorObject == null || sailor.Visual == null || sailor.AssignedCubes.Count == 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            // Melodik ASMR akoru için mikro-gecikme
            if (sailorIndex > 0 && m_SailorStagger > 0f)
            {
                yield return new WaitForSeconds(sailorIndex * m_SailorStagger);
            }

            // Yığın nesnesini oluştur ve bağla (Orijinal Gemi.unity sahnesiyle 1:1 aynı küp orantısı: 0.54f)
            GameObject stackGo = new GameObject($"CargoStack_{sailor.Name}");
            sailor.CargoStack = stackGo.AddComponent<CarriedCargoStack>();
            sailor.Visual.AttachCargo(stackGo.transform, 0.54f, 1f);
            // 10 küplük yüksek kulelerin yana bükülüp birbirine çarpmaması, dik ve zarif durması için ayarlar:
            sailor.CargoStack.ConfigureStack(spacing: 0.94f, snakeAmp: 0.035f, waveAmp: 0.06f, maxSway: 0.10f, randomYaw: 2.0f);

            sailor.Visual.Play(SailorRunnerVisual.StateRunEmpty);

            // Küpleri soldan sağa doğru sırayla topla
            for (int i = 0; i < sailor.AssignedCubes.Count; i++)
            {
                PixelCube targetCube = sailor.AssignedCubes[i];
                if (targetCube == null) continue;

                Vector3 targetPos = targetCube.transform.position;
                targetPos.z = m_GroundZ;

                // Küpün tam içine/merkezine basmak yerine, yaklaşma açısında küpün tam önünde dur
                Vector3 startPos = sailor.SailorObject.transform.position;
                startPos.z = m_GroundZ;
                Vector3 toCube = targetPos - startPos;
                toCube.z = 0f;
                float distToCube = toCube.magnitude;

                // Küpün kenarının önünde duruş mesafesi (~0.21m, küp kenarından 10cm önü)
                float standoff = Mathf.Min(0.21f, distToCube * 0.75f);
                Vector3 approachDir = distToCube > 1e-4f ? (toCube / distToCube) : Vector3.up;
                Vector3 stopPos = targetPos - approachDir * standoff;
                stopPos.z = m_GroundZ;

                // Hedef küpün önüne doğru yürü
                while (true)
                {
                    while (m_IsPaused) yield return null;

                    Vector3 currentPos = sailor.SailorObject.transform.position;
                    Vector3 toTarget = stopPos - currentPos;
                    toTarget.z = 0f;
                    float dist = toTarget.magnitude;

                    if (dist <= 0.03f) break;

                    float step = m_WalkSpeed * m_SpeedMultiplier * Time.deltaTime;
                    Vector3 moveDir = toTarget / dist;
                    sailor.Visual.SnapFacing(moveDir);
                    sailor.SailorObject.transform.position += moveDir * Mathf.Min(step, dist);

                    yield return null;
                }

                // Küpe tam dön ve alma animasyonu
                Vector3 pickupDir = (targetPos - sailor.SailorObject.transform.position);
                pickupDir.z = 0f;
                if (pickupDir.sqrMagnitude > 1e-4f)
                {
                    sailor.Visual.SnapFacing(pickupDir.normalized);
                }

                sailor.Visual.PlayThen(SailorRunnerVisual.StatePickup, SailorRunnerVisual.StateRunCarry);

                float pickupWait = Mathf.Max(0.24f, sailor.Visual.GetClipLength(SailorRunnerVisual.StatePickup) * 0.48f);
                float timer = 0f;
                while (timer < pickupWait)
                {
                    while (m_IsPaused) yield return null;
                    timer += Time.deltaTime;
                    yield return null;
                }

                // 🔊 ASMR KÜP TOPLAMA SESİ
                if (HypercasualFeedbackManager.Instance != null)
                {
                    HypercasualFeedbackManager.Instance.PlayCubeLiftoffFeedback(
                        targetCube.transform.position,
                        sailor.CollectedCount,
                        immediate: true
                    );
                }

                // Küpü yığına ekle
                AddCubeVisualToStack(targetCube, sailor.CollectedCount, sailor.CargoStack, sailor.TeamColor);
                targetCube.gameObject.SetActive(false);
                sailor.CollectedCount++;

                float remainWait = Mathf.Max(0.20f, sailor.Visual.GetClipLength(SailorRunnerVisual.StatePickup) * 0.40f);
                timer = 0f;
                while (timer < remainWait)
                {
                    while (m_IsPaused) yield return null;
                    timer += Time.deltaTime;
                    yield return null;
                }

                sailor.Visual.ClearFacingOverride();
                sailor.Visual.Play(SailorRunnerVisual.StateRunCarry);
            }

            // Eğer sahneye bir konteyner bağlanmışsa, oraya yürüyüp küpleri boşaltsın
            if (m_ContainerTransform != null && sailor.CargoStack != null && sailor.CargoStack.Count > 0)
            {
                Vector3 dropTargetPos = m_ContainerTransform.position + sailor.DropOffset;
                dropTargetPos.z = m_GroundZ;

                // 🎥 Konteynera yaklaşırken kamera yukarıdan tatlı bir takip açısına geçer
                if (m_Camera != null)
                {
                    m_Camera.AnimateZoom(new Vector3(0.0f, -2.60f, -4.20f), 50f, 1.8f);
                }

                sailor.Visual.Play(SailorRunnerVisual.StateRunCarry);
                while (true)
                {
                    while (m_IsPaused) yield return null;

                    Vector3 cur = sailor.SailorObject.transform.position;
                    Vector3 toDrop = dropTargetPos - cur;
                    toDrop.z = 0f;
                    float d = toDrop.magnitude;
                    if (d <= 0.04f) break;

                    float step = m_WalkSpeed * m_SpeedMultiplier * Time.deltaTime;
                    Vector3 moveDir = toDrop / d;
                    sailor.Visual.SnapFacing(moveDir);
                    sailor.SailorObject.transform.position += moveDir * Mathf.Min(step, d);
                    yield return null;
                }

                sailor.Visual.SnapFacing(sailor.FinalFacingDirection);
                sailor.Visual.Play(SailorRunnerVisual.StateIdle);

                yield return StartCoroutine(DeliverCubesToContainerRoutine(sailor, sailorIndex));
            }
            else
            {
                // Zafer duruşu: Bitişte yüzünü kameraya dönüp kutlama yap
                sailor.Visual.SnapFacing(Vector3.down);
                sailor.Visual.Play(SailorRunnerVisual.StateIdle);
                sailor.SailorObject.transform.DOPunchScale(new Vector3(0.08f, 0.08f, 0.08f), 0.4f, 2, 0.5f);
            }

            onComplete?.Invoke();
        }

        private IEnumerator DeliverCubesToContainerRoutine(CreativeSailorData sailor, int sailorIndex)
        {
            if (sailor.CargoStack == null || m_ContainerTransform == null) yield break;

            // Ritmik ASMR aralığı: sol, orta ve sağ gemiciler küpleri hafif aralıklı atsın
            if (sailorIndex > 0)
            {
                yield return new WaitForSeconds(sailorIndex * 0.06f);
            }

            // Konteynerin orijinal scale'ini garantiye al (önceki tween kalıntılarını sıfırla)
            m_ContainerTransform.DOKill();
            if (m_ContainerOriginalScale == Vector3.one || m_ContainerOriginalScale.x > 0.1f)
            {
                m_ContainerOriginalScale = new Vector3(0.0078f, 0.0078f, 0.0078f);
            }
            m_ContainerTransform.localScale = m_ContainerOriginalScale;
            Vector3 containerTargetPos = m_ContainerTransform.position + new Vector3(0f, 0.08f, -0.15f);

            while (sailor.CargoStack.Count > 0)
            {
                while (m_IsPaused) yield return null;

                Transform topCube = sailor.CargoStack.PopTop();
                if (topCube != null)
                {
                    topCube.SetParent(null, true);
                    m_TotalDeliveredCount++;

                    int cubeIndex = m_TotalDeliveredCount;

                    // 🏃 Gemici atış reaksiyonu (Hafif geri tepme - smooth karakter animasyonu)
                    if (sailor.SailorObject != null)
                    {
                        sailor.SailorObject.transform.DOPunchPosition(-sailor.FinalFacingDirection * 0.035f, 0.20f, 1, 0.4f);
                    }

                    // 🧊 Akıcı, pürüzsüz parabolik yay atışı (DOJump + OutQuad)
                    topCube.DOJump(containerTargetPos, 0.55f, 1, 0.42f).SetEase(Ease.OutQuad);

                    // 🌀 Havada hafif 3D dönüş (ASMR fiziksel süzülme hissiyatı)
                    Vector3 randomSpin = new Vector3(
                        Random.Range(-20f, 20f),
                        Random.Range(-20f, 20f),
                        Random.Range(-35f, 35f)
                    );
                    topCube.DORotate(randomSpin, 0.42f, RotateMode.LocalAxisAdd).SetEase(Ease.OutSine);

                    // ✨ Havada boyutunu korur, tam konteyner deliğine inerken pürüzsüzce küçülür
                    topCube.DOScale(Vector3.zero, 0.22f).SetDelay(0.20f).SetEase(Ease.InSine).OnComplete(() =>
                    {
                        if (topCube != null) Destroy(topCube.gameObject);

                        // 🔊 ASMR Konteynere Temas Sesi (Küp tam konteynere girdiği an)
                        if (HypercasualFeedbackManager.Instance != null)
                        {
                            HypercasualFeedbackManager.Instance.PlayCubeLiftoffFeedback(
                                containerTargetPos,
                                cubeIndex,
                                immediate: true
                            );
                        }

                        // 📳 Konteyner Mikro Yaylanma (Oransal Squash & Stretch: asla bozulmaz, tok ve tatlı sekme)
                        if (m_ContainerTransform != null)
                        {
                            m_ContainerTransform.DOKill();
                            m_ContainerTransform.localScale = m_ContainerOriginalScale;

                            // Her küp vuruşunda dikeyde %6 basılma, enine %4 yaylanma
                            Vector3 squashed = new Vector3(
                                m_ContainerOriginalScale.x * 1.05f,
                                m_ContainerOriginalScale.y * 0.94f,
                                m_ContainerOriginalScale.z
                            );

                            m_ContainerTransform.DOScale(squashed, 0.07f).SetEase(Ease.OutQuad).OnComplete(() =>
                            {
                                if (m_ContainerTransform != null)
                                {
                                    m_ContainerTransform.DOScale(m_ContainerOriginalScale, 0.14f).SetEase(Ease.OutBack);
                                }
                            });
                        }

                        // 🏷️ Küp tam konteynere girdiği an sayı rozeti eksilir ve mikro yaylanır
                        m_DeliveredToContainerCount++;
                        UpdateContainerBadge(m_DeliveredToContainerCount);

                        // 🎯 Küpler dolarken sırayla motive edici kelimeler patlasın (GOOD! -> GREAT! -> AMAZING!)
                        CheckMilestoneFeedback(m_DeliveredToContainerCount);

                        // 🎉 Son küp ulaştıysa konfeti ve kutlama sekansını anında ateşle!
                        if (m_DeliveredToContainerCount >= m_ContainerCapacity && !m_HasCelebrated)
                        {
                            TriggerCelebrationSequence();
                        }
                    });
                }

                float timer = 0f;
                while (timer < m_DeliveryDelayPerCube)
                {
                    while (m_IsPaused) yield return null;
                    timer += Time.deltaTime;
                    yield return null;
                }
            }

            // Bireysel teslimat bittiğinde denizci kutlama için kameraya bakar
            if (m_DeliveredToContainerCount >= m_ContainerCapacity)
            {
                TriggerCelebrationSequence();
            }
            else
            {
                sailor.Visual.SnapFacing(Vector3.down);
                sailor.Visual.Play(SailorRunnerVisual.StateIdle);
            }

            yield return new WaitForSeconds(0.2f);
        }

        private void AddCubeVisualToStack(PixelCube sourceCube, int index, CarriedCargoStack stack, Color teamColor)
        {
            if (stack == null || sourceCube == null) return;

            GameObject carried = new GameObject($"CarriedCube_{index}");
            MeshFilter mf = carried.AddComponent<MeshFilter>();

            MeshFilter sourceMf = sourceCube.GetComponent<MeshFilter>();
            mf.sharedMesh = sourceMf != null ? sourceMf.sharedMesh : GetDefaultCubeMesh();

            MeshRenderer mr = carried.AddComponent<MeshRenderer>();
            MeshRenderer sourceMr = sourceCube.GetComponent<MeshRenderer>();
            if (sourceMr != null && sourceMr.sharedMaterial != null)
            {
                mr.sharedMaterial = sourceMr.sharedMaterial;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            float meshEdge = mf.sharedMesh != null ? Mathf.Max(1e-4f, mf.sharedMesh.bounds.size.x) : 1f;
            stack.Add(carried.transform, 1f / meshEdge);
        }

        public static void ApplySailorVisualColor(GameObject sailor, Color color, float groundZ)
        {
            if (sailor == null) return;

            var vis = sailor.GetComponent<SailorRunnerVisual>();
            if (vis != null)
            {
                vis.TiltTowardsCamera = 0f;
                vis.SetupTeamRing(color, groundZ);
                vis.SetRingVisible(true);
                vis.SetupFakeShadow(groundZ);
                vis.SetShadowVisible(true);
            }

            foreach (var r in sailor.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is SpriteRenderer) continue;
                if (r.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

                if (r.name.IndexOf("Cap", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.name.IndexOf("Pompom", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.name.StartsWith("TeamColor", System.StringComparison.Ordinal))
                {
                    SharedColorMaterialCache.Apply(r, color, Color.black);
                }
                else
                {
                    Material[] mats = r.sharedMaterials;
                    bool changed = false;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        Material src = SharedColorMaterialCache.ResolveSource(mats[m]);
                        if (src == null) continue;
                        string sName = src.name;

                        if (sName.StartsWith("M_TeamColor", System.StringComparison.Ordinal))
                        {
                            mats[m] = SharedColorMaterialCache.Get(src, color, Color.black);
                            changed = true;
                        }
                        else if (sName.StartsWith("M_Navy", System.StringComparison.Ordinal))
                        {
                            if (r.name == "Torso" || r.name == "ArmL" || r.name == "ArmR")
                            {
                                mats[m] = SharedColorMaterialCache.Get(src, color, Color.black);
                                changed = true;
                            }
                        }
                    }
                    if (changed) r.sharedMaterials = mats;
                }
            }
        }

        private static Mesh s_DefaultCubeMesh;
        private static Mesh GetDefaultCubeMesh()
        {
            if (s_DefaultCubeMesh == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                s_DefaultCubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(temp);
            }
            return s_DefaultCubeMesh;
        }

        #region 🎊 Konfeti & Zafer Kutlaması

        private static readonly Color[] ConfettiPalette = new Color[]
        {
            new Color(1.00f, 0.84f, 0.00f, 1f), // #FFD600 Parlak Altın Sarısı
            new Color(1.00f, 0.16f, 0.43f, 1f), // #FF2A6D Canlı Mercan Pembe
            new Color(0.00f, 0.90f, 1.00f, 1f), // #00E5FF Parlak Turkuaz / Cyan
            new Color(0.00f, 0.55f, 1.00f, 1f), // #008CFF Elektrik Mavisi
            new Color(0.00f, 0.90f, 0.46f, 1f), // #00E676 Canlı Zümrüt Yeşili
            new Color(0.55f, 0.25f, 1.00f, 1f), // #8C40FF Parlak Mor
            new Color(1.00f, 0.45f, 0.00f, 1f), // #FF7300 Neon Turuncu
            new Color(1.00f, 1.00f, 1.00f, 1f)  // #FFFFFF Parıltılı Beyaz
        };

        public void TriggerCelebrationSequence()
        {
            if (m_HasCelebrated) return;
            m_HasCelebrated = true;

            // 🎥 Kutlamada kamera hafif zoom-out yapar ve tatlı bir sevinç titreşimi verir
            if (m_Camera != null)
            {
                m_Camera.AnimateZoom(new Vector3(0.0f, -3.10f, -5.20f), 54f, 0.6f);
                m_Camera.transform.DOShakePosition(0.35f, 0.08f, 25, 90f);
            }

            // 🌟 "PERFECT!" Final Başarı Damgası (Altın sarısı, konfetilerin ortasında patlar)
            ShowMilestoneStamp("PERFECT!", new Color(1.00f, 0.88f, 0.10f, 1f), -14f, 1.35f, isFinalCelebration: true);

            // 1. 🎉 Konteyner neşeli kutlama sıçraması (Pop Jump)
            if (m_ContainerTransform != null)
            {
                m_ContainerTransform.DOKill();
                m_ContainerTransform.localScale = m_ContainerOriginalScale;
                Vector3 popScale = m_ContainerOriginalScale * 1.20f;
                m_ContainerTransform.DOScale(popScale, 0.16f).SetEase(Ease.OutBack).OnComplete(() =>
                {
                    if (m_ContainerTransform != null)
                    {
                        m_ContainerTransform.DOScale(m_ContainerOriginalScale, 0.22f).SetEase(Ease.InOutQuad);
                    }
                });
            }

            if (m_ContainerBadgeText != null)
            {
                m_ContainerBadgeText.transform.DOKill();
                m_ContainerBadgeText.transform.localScale = Vector3.one;
                m_ContainerBadgeText.transform.DOPunchScale(Vector3.one * 0.35f, 0.45f, 2, 0.5f);
            }

            // 2. 🎊 RENKLİ KONFETİ PATLAMASI! (Çift yönlü toplar + Konteyner volkanı)
            PlayConfettiBurst();

            // 3. 🕺 Tüm 3 denizci de aynı anda kameraya dönüp zafer kutlaması yapar
            foreach (var s in m_Sailors)
            {
                if (s.Visual != null)
                {
                    s.Visual.SnapFacing(Vector3.down);
                    s.Visual.Play(SailorRunnerVisual.StateIdle);
                }
                if (s.SailorObject != null)
                {
                    s.SailorObject.transform.DOPunchScale(new Vector3(0.14f, 0.14f, 0.14f), 0.6f, 3, 0.5f);
                }
            }
        }

        private void CheckMilestoneFeedback(int deliveredCount)
        {
            if (deliveredCount == 4)
            {
                // 1. Aşama: "GOOD!" - Canlı Zümrüt / Nane Yeşili
                ShowMilestoneStamp("GOOD!", new Color(0.10f, 0.95f, 0.50f, 1f), -7f, 1.02f);
            }
            else if (deliveredCount == 8)
            {
                // 2. Aşama: "GREAT!" - Elektrik Cam Göbeği / Canlı Turkuaz
                ShowMilestoneStamp("GREAT!", new Color(0.00f, 0.85f, 1.00f, 1f), 6f, 1.08f);
            }
            else if (deliveredCount == 12)
            {
                // 3. Aşama: "SUPER!" - Neon Pembe / Fuşya (Ekrana mükemmel oturan 6 harfli tatlı kelime)
                ShowMilestoneStamp("SUPER!", new Color(1.00f, 0.22f, 0.58f, 1f), -6f, 1.05f);
            }
        }

        public void ShowMilestoneStamp(string text, Color color, float tiltAngle, float targetScale, bool isFinalCelebration = false)
        {
            if (m_PerfectStamp == null) return;

            if (m_StampText == null)
            {
                m_StampText = m_PerfectStamp.GetComponent<TMPro.TextMeshProUGUI>();
            }

            if (m_StampText != null)
            {
                m_StampText.text = text;
                m_StampText.color = color;
            }

            m_PerfectStamp.SetActive(true);
            m_PerfectStamp.transform.DOKill();

            if (isFinalCelebration)
            {
                // 🌟 "PERFECT!" Final Büyük Patlama & Sevinç Sekmesi
                m_PerfectStamp.transform.localScale = Vector3.zero;
                m_PerfectStamp.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
                m_PerfectStamp.transform.DOScale(Vector3.one * targetScale, 0.38f).SetEase(Ease.OutBack);
                m_PerfectStamp.transform.DORotate(new Vector3(0f, 0f, 6f), 0.38f).SetEase(Ease.OutBack).OnComplete(() =>
                {
                    if (m_PerfectStamp != null)
                    {
                        m_PerfectStamp.transform.DORotate(new Vector3(0f, 0f, -3f), 0.28f).SetEase(Ease.InOutSine);
                    }
                });
            }
            else
            {
                // ✨ "GOOD!", "GREAT!", "AMAZING!" Ara Kelimeleri (Canlı Elastik Pop & Hafif Yaylanma)
                m_PerfectStamp.transform.localScale = Vector3.one * (targetScale * 0.35f);
                m_PerfectStamp.transform.localRotation = Quaternion.Euler(0f, 0f, tiltAngle * 1.4f);

                m_PerfectStamp.transform.DOScale(Vector3.one * targetScale, 0.22f).SetEase(Ease.OutBack).OnComplete(() =>
                {
                    if (m_PerfectStamp != null && !m_HasCelebrated)
                    {
                        m_PerfectStamp.transform.DOScale(Vector3.one * (targetScale * 1.05f), 0.32f)
                            .SetEase(Ease.InOutSine)
                            .SetLoops(-1, LoopType.Yoyo);
                    }
                });
                m_PerfectStamp.transform.DORotate(new Vector3(0f, 0f, tiltAngle), 0.22f).SetEase(Ease.OutBack);
            }
        }

        public void PlayConfettiBurst()
        {
            EnsureConfettiSystem();
            if (m_ConfettiPS == null) return;

            StartCoroutine(ConfettiBurstSequence());
        }

        private IEnumerator ConfettiBurstSequence()
        {
            Vector3 containerPos = m_ContainerTransform != null ? m_ContainerTransform.position : Vector3.zero;

            // 1. DALGA: Sol ve Sağ Konfeti Topları (Çapraz yukarı fırlar)
            EmitCannonBurst(containerPos + new Vector3(-0.95f, -0.20f, -0.15f), new Vector2(1.8f, 5.2f), count: 50);
            EmitCannonBurst(containerPos + new Vector3(+0.95f, -0.20f, -0.15f), new Vector2(-1.8f, 5.2f), count: 50);

            // 🔊 ASMR Konfeti Patlama Sesi
            if (HypercasualFeedbackManager.Instance != null)
            {
                HypercasualFeedbackManager.Instance.PlayBalloonPopFeedback(containerPos, ConfettiPalette[0]);
            }

            yield return new WaitForSeconds(0.20f);

            // 2. DALGA: Konteyner Merkezinden Fışkıran Volkanik Konfeti Yağmuru
            EmitCannonBurst(containerPos + new Vector3(0.0f, 0.10f, -0.15f), new Vector2(0f, 6.0f), count: 60, spreadX: 2.6f);

            if (HypercasualFeedbackManager.Instance != null)
            {
                HypercasualFeedbackManager.Instance.PlayBalloonPopFeedback(containerPos, ConfettiPalette[1]);
            }
        }

        private void EmitCannonBurst(Vector3 origin, Vector2 baseVelocity, int count, float spreadX = 1.6f)
        {
            if (m_ConfettiPS == null) return;

            for (int i = 0; i < count; i++)
            {
                ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
                ep.position = origin + new Vector3(
                    Random.Range(-0.12f, 0.12f),
                    Random.Range(-0.06f, 0.06f),
                    Random.Range(-0.05f, 0.05f)
                );

                float vx = baseVelocity.x + Random.Range(-spreadX, spreadX);
                float vy = baseVelocity.y * Random.Range(0.75f, 1.25f);
                float vz = Random.Range(-0.8f, 0.2f);
                ep.velocity = new Vector3(vx, vy, vz);

                Color col = ConfettiPalette[Random.Range(0, ConfettiPalette.Length)];
                ep.startColor = col;

                float s = Random.Range(0.09f, 0.15f);
                ep.startSize = s;

                ep.rotation3D = new Vector3(
                    Random.Range(0f, 360f),
                    Random.Range(0f, 360f),
                    Random.Range(0f, 360f)
                );
                ep.angularVelocity3D = new Vector3(
                    Random.Range(-300f, 300f),
                    Random.Range(-300f, 300f),
                    Random.Range(-300f, 300f)
                );

                ep.startLifetime = Random.Range(2.2f, 3.2f);

                m_ConfettiPS.Emit(ep, 1);
            }
        }

        public void EnsureConfettiSystem()
        {
            if (m_ConfettiPS != null) return;

            Transform existing = transform.Find("[Creative_Confetti_FX]");
            if (existing != null)
            {
                m_ConfettiPS = existing.GetComponent<ParticleSystem>();
                if (m_ConfettiPS != null) return;
            }

            GameObject psGo = new GameObject("[Creative_Confetti_FX]");
            psGo.transform.SetParent(transform, false);
            psGo.transform.position = m_ContainerTransform != null ? m_ContainerTransform.position : Vector3.zero;

            m_ConfettiPS = psGo.AddComponent<ParticleSystem>();
            SetupConfettiParticleSystem(m_ConfettiPS);
        }

        public void SetupConfettiParticleSystem(ParticleSystem ps)
        {
            if (ps == null) return;

            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            Material confettiMat = new Material(sh);
            confettiMat.name = "Creative_Confetti_Mat";
            Texture2D tex = GetConfettiTexture();
            if (confettiMat.HasProperty("_BaseMap")) confettiMat.SetTexture("_BaseMap", tex);
            if (confettiMat.HasProperty("_MainTex")) confettiMat.SetTexture("_MainTex", tex);
            confettiMat.mainTexture = tex;

            var psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr == null) psr = ps.gameObject.AddComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.alignment = ParticleSystemRenderSpace.View;
            psr.sharedMaterial = confettiMat;
            psr.sortingOrder = 50; // Konteyner (5) ve zeminin (-100) önünde

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.maxParticles = 600;
            main.startLifetime = 2.8f;
            main.gravityModifier = 1.35f;

            var emission = ps.emission;
            emission.enabled = false;

            var limitVel = ps.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.dampen = 0.22f;
            limitVel.drag = 1.25f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-180f * Mathf.Deg2Rad, 180f * Mathf.Deg2Rad);
            rot.y = new ParticleSystem.MinMaxCurve(-240f * Mathf.Deg2Rad, 240f * Mathf.Deg2Rad);
            rot.z = new ParticleSystem.MinMaxCurve(-120f * Mathf.Deg2Rad, 120f * Mathf.Deg2Rad);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0f),
                    new GradientAlphaKey(1.0f, 0.80f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }

        private static Texture2D GetConfettiTexture()
        {
            if (s_ConfettiTexture != null) return s_ConfettiTexture;
            int size = 32;
            s_ConfettiTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - 15.5f) - 13.5f);
                    float dy = Mathf.Max(0, Mathf.Abs(y - 15.5f) - 13.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - d);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            s_ConfettiTexture.SetPixels(pixels);
            s_ConfettiTexture.Apply();
            return s_ConfettiTexture;
        }

        #endregion

        private void OnGUI()
        {
            if (!m_ShowHelp) return;

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;

            GUILayout.BeginArea(new Rect(15, 15, 450, 225), style);
            GUILayout.Label("🎬 <b>ASMR KREATİF ÇEKİMİ (THİRD PERSON + SU DAMLASI + KONTEYNER)</b>", GUI.skin.label);
            GUILayout.Space(4);
            GUILayout.Label($"• <b>[1, 2, 3]</b> Kamera Modu: {(m_Camera != null ? m_Camera.Mode.ToString() : "N/A")}");
            GUILayout.Label("• <b>[R]</b> Sahneyi Baştan Başlat (Replay)");
            GUILayout.Label($"• <b>[Space]</b> {(m_IsPaused ? "▶️ Devam Et" : "⏸️ Duraklat")}");
            GUILayout.Label($"• <b>[ [ / ] ]</b> Hız Çarpanı: {m_SpeedMultiplier:F2}x (Temel: {m_WalkSpeed:F2})");
            string soundModeName = (HypercasualFeedbackManager.Instance != null && HypercasualFeedbackManager.Instance.AsmrUsePureProceduralSound)
                ? "<color=#00E5FF>ASMR Melodik Puf (Procedural)</color>"
                : "<color=#FFD600>Orijinal Kenney impactSoft (Klasik)</color>";
            GUILayout.Label($"• <b>[T]</b> Toplama Sesi: {soundModeName}");
            GUILayout.Label("• <b>[H]</b> Bu Bilgi Kutusunu Gizle / Göster (Temiz Kayıt)");
            GUILayout.Label("• <b>Akış:</b> Su Damlası Toplama ➔ Konteynere Teslimat ➔ Kutlama");
            GUILayout.EndArea();
        }
    }
}
