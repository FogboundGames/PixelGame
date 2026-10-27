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
        [SerializeField] private float m_WalkSpeed = 1.65f;
        [SerializeField] private float m_SpeedMultiplier = 1.0f;
        [Tooltip("Gemicilerin küp alma anları arasındaki mikrosaniye gecikmesi (Melodik ASMR akoru için)")]
        [SerializeField] private float m_SailorStagger = 0.08f;
        [SerializeField] private float m_GroundZ = 0.42f;

        [Header("📦 Konteyner & Teslimat (Container Delivery)")]
        [SerializeField] private Transform m_ContainerTransform;
        [SerializeField] private TMPro.TextMeshProUGUI m_ContainerBadgeText;
        [SerializeField] private UnityEngine.UI.Image m_ContainerImage;
        [SerializeField] private int m_ContainerCapacity = 16;
        [SerializeField] private float m_DeliveryDelayPerCube = 0.12f;
        [SerializeField] private Vector3 m_ContainerDropOffset = new Vector3(0f, -0.40f, 0f);

        [Header("🎬 Otomatik Oynatma")]
        [SerializeField] private bool m_AutoStartOnPlay = true;
        [SerializeField] private bool m_AutoLoop = true;
        [SerializeField] private float m_StartDelay = 0.8f;
        [SerializeField] private float m_EndDelay = 3.5f;

        [Header("🎥 Kamera")]
        [SerializeField] private CreativeThirdPersonCamera m_Camera;
        [SerializeField] private Transform m_CameraCenterAnchor;

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

        public List<CreativeSailorData> Sailors => m_Sailors;
        public CreativeThirdPersonCamera Camera { get => m_Camera; set => m_Camera = value; }
        public Transform CameraCenterAnchor { get => m_CameraCenterAnchor; set => m_CameraCenterAnchor = value; }
        public Transform ContainerTransform { get => m_ContainerTransform; set => m_ContainerTransform = value; }
        public TMPro.TextMeshProUGUI ContainerBadgeText { get => m_ContainerBadgeText; set => m_ContainerBadgeText = value; }
        public UnityEngine.UI.Image ContainerImage { get => m_ContainerImage; set => m_ContainerImage = value; }
        public int ContainerCapacity { get => m_ContainerCapacity; set => m_ContainerCapacity = value; }
        public Vector3 ContainerDropOffset { get => m_ContainerDropOffset; set => m_ContainerDropOffset = value; }

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
            UpdateContainerBadge(0);

            InitSailors();
            CacheInitialCubeData();
            UpdateCameraCenterAnchor(true);
        }

        public void UpdateContainerBadge(int count)
        {
            if (m_ContainerBadgeText != null)
            {
                m_ContainerBadgeText.text = $"{count} / {m_ContainerCapacity}";
                if (count >= m_ContainerCapacity && m_ContainerCapacity > 0)
                {
                    m_ContainerBadgeText.color = new Color(0.2f, 1f, 0.4f, 1f);
                }
                else
                {
                    m_ContainerBadgeText.color = Color.white;
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

            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var s in m_Sailors)
            {
                if (s.SailorObject != null)
                {
                    sum += s.SailorObject.transform.position;
                    count++;
                }
            }

            if (count > 0)
            {
                Vector3 center = sum / count;
                m_CameraCenterAnchor.position = center;
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

            // Kamera başlangıç perspektifine yumuşakça döner
            if (m_Camera != null)
            {
                m_Camera.AnimateZoom(new Vector3(0f, -2.10f, -3.20f), 48f, 0.6f);
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

            yield return new WaitForSeconds(m_EndDelay);

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

                float pickupWait = sailor.Visual.GetClipLength(SailorRunnerVisual.StatePickup) * 0.45f;
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

                float remainWait = sailor.Visual.GetClipLength(SailorRunnerVisual.StatePickup) * 0.35f;
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
                Vector3 dropTargetPos = m_ContainerTransform.position + m_ContainerDropOffset;
                dropTargetPos.z = m_GroundZ;

                // 🎥 Ezgi'nin isteği: Konteynera yaklaşırken kamera pürüzsüzce geriye ve geniş açıya çekilir
                if (m_Camera != null)
                {
                    m_Camera.AnimateZoom(new Vector3(0.0f, -2.75f, -4.60f), 53f, 1.35f);
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

                sailor.Visual.SnapFacing(Vector3.up);
                sailor.Visual.Play(SailorRunnerVisual.StateIdle);

                yield return StartCoroutine(DeliverCubesToContainerRoutine(sailor));
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

        private IEnumerator DeliverCubesToContainerRoutine(CreativeSailorData sailor)
        {
            if (sailor.CargoStack == null || m_ContainerTransform == null) yield break;

            int deliveredCount = 0;
            // Konteynerin orijinal scale'ini garantiye al (önceki tween kalıntılarını sıfırla)
            m_ContainerTransform.DOKill();
            if (m_ContainerOriginalScale == Vector3.one || m_ContainerOriginalScale.x > 0.1f)
            {
                m_ContainerOriginalScale = new Vector3(0.0078f, 0.0078f, 0.0078f);
            }
            m_ContainerTransform.localScale = m_ContainerOriginalScale;
            Vector3 containerTargetPos = m_ContainerTransform.position + new Vector3(0f, 0f, -0.2f);

            while (sailor.CargoStack.Count > 0)
            {
                while (m_IsPaused) yield return null;

                Transform topCube = sailor.CargoStack.PopTop();
                if (topCube != null)
                {
                    topCube.SetParent(null, true);
                    deliveredCount++;

                    int cubeIndex = deliveredCount;
                    // Konteynerin içine kavisle zıplayarak giriş (DOJump)
                    topCube.DOJump(containerTargetPos, 0.45f, 1, 0.24f).SetEase(Ease.InQuad);
                    topCube.DOScale(Vector3.zero, 0.24f).SetEase(Ease.InBack).OnComplete(() =>
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

                            m_ContainerTransform.DOScale(squashed, 0.04f).SetEase(Ease.OutQuad).OnComplete(() =>
                            {
                                if (m_ContainerTransform != null)
                                {
                                    m_ContainerTransform.DOScale(m_ContainerOriginalScale, 0.07f).SetEase(Ease.OutBack);
                                }
                            });
                        }
                    });

                    UpdateContainerBadge(deliveredCount);
                }

                float timer = 0f;
                while (timer < m_DeliveryDelayPerCube)
                {
                    while (m_IsPaused) yield return null;
                    timer += Time.deltaTime;
                    yield return null;
                }
            }

            // 🎉 16/16 Tamamlandı: Konteyner tatlı bir kutlama sıçraması yapar + Sailor zafer pozu verir
            if (m_ContainerTransform != null)
            {
                m_ContainerTransform.DOKill();
                m_ContainerTransform.localScale = m_ContainerOriginalScale;
                Vector3 popScale = m_ContainerOriginalScale * 1.15f;
                m_ContainerTransform.DOScale(popScale, 0.14f).SetEase(Ease.OutBack).OnComplete(() =>
                {
                    if (m_ContainerTransform != null)
                    {
                        m_ContainerTransform.DOScale(m_ContainerOriginalScale, 0.20f).SetEase(Ease.InOutQuad);
                    }
                });
            }

            // 🎉 16/16 Tamamlandı: Sailor yüzünü kameraya dönüp kutlama yapar
            sailor.Visual.SnapFacing(Vector3.down);
            sailor.Visual.Play(SailorRunnerVisual.StateIdle);
            sailor.SailorObject.transform.DOPunchScale(new Vector3(0.12f, 0.12f, 0.12f), 0.5f, 3, 0.5f);

            yield return new WaitForSeconds(0.4f);
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

            GUILayout.BeginArea(new Rect(15, 15, 430, 205), style);
            GUILayout.Label("🎬 <b>ASMR KREATİF ÇEKİMİ (THİRD PERSON + SU DAMLASI + KONTEYNER)</b>", GUI.skin.label);
            GUILayout.Space(4);
            GUILayout.Label($"• <b>[1, 2, 3]</b> Kamera Modu: {(m_Camera != null ? m_Camera.Mode.ToString() : "N/A")}");
            GUILayout.Label("• <b>[R]</b> Sahneyi Baştan Başlat (Replay)");
            GUILayout.Label($"• <b>[Space]</b> {(m_IsPaused ? "▶️ Devam Et" : "⏸️ Duraklat")}");
            GUILayout.Label($"• <b>[ [ / ] ]</b> Hız Çarpanı: {m_SpeedMultiplier:F2}x (Temel: {m_WalkSpeed:F2})");
            GUILayout.Label("• <b>[H]</b> Bu Bilgi Kutusunu Gizle / Göster (Temiz Kayıt)");
            GUILayout.Label("• <b>Akış:</b> Su Damlası Toplama ➔ Konteynere Teslimat ➔ Kutlama");
            GUILayout.EndArea();
        }
    }
}
