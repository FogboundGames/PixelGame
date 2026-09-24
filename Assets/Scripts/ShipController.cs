using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelGame
{
    /// <summary>
    /// Su üzerindeki tek bir kargo gemisini (ship-cargo-a) yönetir.
    /// Renk, World Space Canvas kapasite rozeti, su salınımı (bobbing),
    /// slota gerçekçi Bezier su rotasıyla yanaşma ve açık denize yelken açma animasyonlarını içerir.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Ship Controller")]
    public class ShipController : MonoBehaviour, IPointerClickHandler
    {
        [Header("🎨 Renk & Kimlik")]
        [SerializeField] private Color m_ShipColor = Color.red;
        [SerializeField] private string m_ColorName = "Red";

        [Header("📦 Kargo & Kapasite")]
        [SerializeField] private int m_Capacity = 16;
        [SerializeField] private int m_CurrentCargo = 0;

        [Header("⚓ Durum")]
        [SerializeField] private bool m_IsDocked = false;
        [SerializeField] private bool m_IsDeparting = false;
        [SerializeField] private bool m_IsMoving = false;
        [SerializeField] private ShipSlot m_CurrentSlot;

        [Header("🌊 Su Salınımı (Idle Water Bobbing)")]
        [SerializeField] private bool m_EnableWaterBobbing = true;
        [SerializeField] private float m_BobFrequency = 2.4f;
        [SerializeField] private float m_BobHeight = 0.035f;
        [SerializeField] private float m_RollAngle = 2.0f;
        [SerializeField] private float m_PitchAngle = 1.2f;

        [Header("🏷️ Kapasite Rozeti (World Space UI)")]
        [SerializeField] private GameObject m_BadgeCanvasObj;
        [SerializeField] private TextMeshProUGUI m_BadgeText;
        [SerializeField] private Text m_BadgeUIText;
        [SerializeField] private Image m_BadgeImage;

        // Sabit temel ölçek (Her zaman uniform 0.126)
        public const float DefaultShipScale = 0.126f;

        // Dahili referanslar
        private MeshRenderer[] m_Renderers;
        private MaterialPropertyBlock m_PropBlock;
        private float m_BobRandomOffset;
        private Vector3 m_BaseLocalPosition;
        private Quaternion m_BaseLocalRotation;
        private static Material s_AlwaysOnTopMaterial;

        public Color ShipColor => m_ShipColor;
        public int Capacity => m_Capacity;
        public int CurrentCargo => m_CurrentCargo;
        public int RemainingCapacity => Mathf.Max(0, m_Capacity - m_CurrentCargo);
        public bool IsDocked => m_IsDocked;
        public bool IsFull => m_CurrentCargo >= m_Capacity;
        public bool IsDeparting => m_IsDeparting;
        public bool IsMoving => m_IsMoving;
        public ShipSlot CurrentSlot => m_CurrentSlot;

        public event Action<ShipController> OnCargoFilled;
        public event Action<ShipController> OnDeparted;

        private const string FontPath = "Assets/Fonts/LilitaOne-Regular SDF.asset";
        private const string TTFFontPath = "Assets/Fonts/LilitaOne-Regular.ttf";
        private const string BadgeSpritePath = "Assets/UI/Badge_MiniPill_Tintable.png";

        public static Material GetAlwaysOnTopMaterial()
        {
            if (s_AlwaysOnTopMaterial == null)
            {
                Shader shader = Shader.Find("UI/Default");
                Material baseMat = shader != null ? new Material(shader) : new Material(Canvas.GetDefaultCanvasMaterial());
                baseMat.name = "Ship_UI_AlwaysOnTop_Mat";
                baseMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                s_AlwaysOnTopMaterial = baseMat;
            }
            return s_AlwaysOnTopMaterial;
        }

        private void Awake()
        {
            m_PropBlock = new MaterialPropertyBlock();
            m_BobRandomOffset = UnityEngine.Random.Range(0f, 100f);
            transform.localScale = Vector3.one * DefaultShipScale;

            EnsureVisualComponents();
            CreateOrFindBadge();
        }

        private void OnEnable()
        {
            EnsureVisualComponents();
            CreateOrFindBadge();
            UpdateBadgeText();
        }

        private void OnValidate()
        {
            InvalidateRingSprite();
            CreateOrFindBadge();
            UpdateBadgeText();
        }

        private void Start()
        {
            m_BaseLocalPosition = transform.localPosition;
            m_BaseLocalRotation = transform.localRotation;
            transform.localScale = Vector3.one * DefaultShipScale;

            UpdateBadgeText();
            ApplyColorToShip(m_ShipColor);
        }

        private void Update()
        {
            if (m_EnableWaterBobbing && !m_IsMoving && !m_IsDeparting && Application.isPlaying)
            {
                ApplyWaterBobbing();
            }
        }

        private void LateUpdate()
        {
            UpdateBadgePlacement();
        }

        private void UpdateBadgePlacement()
        {
            if (m_BadgeCanvasObj == null) return;

            // Kullanıcı isteği: "textteki sayı dolduğunda text yok olsun"
            // Kapasite dolduğunda veya gemi kalkışta iken rozet ve metin KESİNLİKLE gizlenir.
            if (m_IsDeparting || IsFull || RemainingCapacity <= 0)
            {
                if (m_BadgeCanvasObj.activeSelf) m_BadgeCanvasObj.SetActive(false);
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();

            if (cam != null)
            {
                m_BadgeCanvasObj.transform.rotation = cam.transform.rotation;
                // Kameraya doğru belirgin şekilde öne çıkar (0.85f Z-ofset ile 3D gövdenin önünde parlar):
                Vector3 headTopPos = transform.position + cam.transform.up * 0.52f - cam.transform.forward * 0.85f;
                m_BadgeCanvasObj.transform.position = headTopPos;
            }

            float lossy = transform.lossyScale.x;
            if (Mathf.Abs(lossy) < 0.0001f) lossy = 1f;
            // Metnin ve dairenin kristal netlikte görünmesi için büyük, ferah ölçek
            m_BadgeCanvasObj.transform.localScale = Vector3.one * (0.011f / lossy);

            if (!m_BadgeCanvasObj.activeSelf && !m_IsDeparting && !IsFull && RemainingCapacity > 0)
            {
                m_BadgeCanvasObj.SetActive(true);
            }
        }

        private void ApplyWaterBobbing()
        {
            float time = Time.time * m_BobFrequency + m_BobRandomOffset;
            float dy = Mathf.Sin(time) * m_BobHeight;
            float dRoll = Mathf.Sin(time * 0.85f) * m_RollAngle;
            float dPitch = Mathf.Cos(time * 0.75f) * m_PitchAngle;

            transform.localPosition = m_BaseLocalPosition + new Vector3(0f, dy, 0f);
            transform.localRotation = m_BaseLocalRotation * Quaternion.Euler(dPitch, 0f, dRoll);
        }

        [Header("📦 Dinamik Varil Güvertesi (Cargo Deck)")]
        [SerializeField] private Transform m_CargoDeckRoot;
        private readonly List<GameObject> m_SpawnedBarrels = new List<GameObject>();
        private static Mesh s_BarrelMesh;
        private Material m_BarrelSharedMaterial;

        /// <summary>
        /// Gemiyi belirli bir renk ve kapasite ile yapılandırır.
        /// Başlangıçta güverte tamamen boştur, küpler geldikçe aynı renkte variller oluşur.
        /// </summary>
        public void Configure(Color color, int capacity, string colorName = "")
        {
            m_ShipColor = color;
            m_Capacity = Mathf.Max(1, capacity);
            m_CurrentCargo = 0;
            m_ColorName = string.IsNullOrEmpty(colorName) ? ColorUtility.ToHtmlStringRGB(color) : colorName;

            ClearCargoBarrels();
            EnsureVisualComponents();
            CreateOrFindBadge();
            ApplyColorToShip(color);
            UpdateBadgeText();
        }

        private static Texture2D s_WhiteTex;
        public static Texture2D GetWhiteTexture()
        {
            if (s_WhiteTex != null) return s_WhiteTex;
            s_WhiteTex = new Texture2D(1, 1);
            s_WhiteTex.SetPixel(0, 0, Color.white);
            s_WhiteTex.Apply();
            return s_WhiteTex;
        }

        /// <summary>
        /// Geminin HER BİR ZERRESİNİ (%100 tüm parçalarını) küplerle tam aynı renge boyar.
        /// FBX'in çok renkli dokusunu (colormap) devre dışı bırakır.
        /// </summary>
        public void ApplyColorToShip(Color color)
        {
            m_ShipColor = color;
            if (m_Renderers == null || m_Renderers.Length == 0)
            {
                m_Renderers = GetComponentsInChildren<MeshRenderer>(true);
            }

            if (m_PropBlock == null) m_PropBlock = new MaterialPropertyBlock();

            Texture2D whiteTex = GetWhiteTexture();

            foreach (var mr in m_Renderers)
            {
                if (mr == null) continue;
                // Dinamik varil renderers'ını ana gövde boyamasından ayrı tut
                if (m_CargoDeckRoot != null && mr.transform.IsChildOf(m_CargoDeckRoot)) continue;

                mr.GetPropertyBlock(m_PropBlock);

                // Çok renkli kaplamayı iptal et ve HER ZERRESİNİ birebir küpün rengi yap!
                m_PropBlock.SetColor("_BaseColor", color);
                m_PropBlock.SetColor("_Color", color);
                m_PropBlock.SetTexture("_BaseMap", whiteTex);
                m_PropBlock.SetTexture("_MainTex", whiteTex);
                m_PropBlock.SetColor("_HColor", Color.Lerp(color, Color.white, 0.28f));
                m_PropBlock.SetColor("_SColor", Color.Lerp(color, Color.black, 0.35f));
                m_PropBlock.SetColor("_RimColor", Color.Lerp(color, Color.white, 0.45f));
                m_PropBlock.SetColor("_PlasticHighlightColor", Color.white);
                mr.SetPropertyBlock(m_PropBlock);
            }

            // Mevcut oluşturulmuş variller varsa renklerini de güncelle
            UpdateAllBarrelsColor();
        }

        /// <summary>
        /// Gemiye kargo (küp parçacığı) ekler ve güvertede geminin renginde bir 3D varil oluşturur.
        /// </summary>
        public void AddCargo(int amount = 1)
        {
            if (m_IsDeparting) return;

            int prevCargo = m_CurrentCargo;
            m_CurrentCargo = Mathf.Min(m_Capacity, m_CurrentCargo + amount);
            UpdateBadgeText();

            // Yeni binen her küp için geminin renginde 1 adet 3D varil oluştur
            for (int i = prevCargo; i < m_CurrentCargo; i++)
            {
                SpawnCargoBarrel(i);
            }

            // Kargo alma tatlı zıplama efekti
            transform.DOKill(true);
            transform.DOPunchScale(new Vector3(0.08f, -0.08f, 0.08f) * DefaultShipScale, 0.18f, 4, 0.5f)
                .OnComplete(() => transform.localScale = Vector3.one * DefaultShipScale);

            if (IsFull && !m_IsDeparting)
            {
                OnCargoFilled?.Invoke(this);
                DepartAndFreeSlot();
            }
        }

        /// <summary>
        /// Kargo dolduğunda gemi slottan çıkar, sol tarafa doğru kavisli deniz rotasıyla hızlanarak yol alır ve yok olur.
        /// Kullanıcının isteği: Text ve dairesel rozet gemi dolduğu an tamamen yok olur; gemi sol tarafa giderek kaybolur.
        /// </summary>
        public void DepartAndFreeSlot()
        {
            if (m_IsDeparting) return;
            m_IsDeparting = true;
            m_EnableWaterBobbing = false;

            // Rozeti / texti HEMEN yok et (Kullanıcı: "textteki sayı dolduğunda text yok olsun")
            if (m_BadgeCanvasObj != null)
            {
                m_BadgeCanvasObj.SetActive(false);
            }
            if (m_BadgeUIText != null)
            {
                m_BadgeUIText.gameObject.SetActive(false);
            }
            if (m_BadgeImage != null)
            {
                m_BadgeImage.gameObject.SetActive(false);
            }

            StartCoroutine(DepartToLeftRoutine());
        }

        [ContextMenu("🚢 Test Depart To Left (Test Kalkış)")]
        public void TestDepartToLeft()
        {
            DepartAndFreeSlot();
        }

        private IEnumerator DepartToLeftRoutine()
        {
            // 1. Text ve rozet hemen kaybolur (Kullanıcı: "textteki sayı dolduğunda text yok olsun")
            if (m_BadgeCanvasObj != null) m_BadgeCanvasObj.SetActive(false);
            if (m_BadgeUIText != null) m_BadgeUIText.gameObject.SetActive(false);
            if (m_BadgeImage != null) m_BadgeImage.gameObject.SetActive(false);

            // Slottan dünya koordinatlarına çık
            transform.SetParent(null, true);
            transform.DOKill(true);

            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            // Slottan ayrılış: Geminin pruva (burun) yönünde slottan ileriye doğru zarifçe süzülür
            Vector3 undockOffset = transform.forward * 0.90f;
            Vector3 undockPos = startPos + undockOffset;

            // Su yüzeyi düzleminde sola bakan hedef rotasyon (Lokal Y ekseninde -90° dönüş):
            Quaternion leftTargetRot = Quaternion.Euler(-68f, 0f, 0f) * Quaternion.Euler(0f, -90f, 0f);

            // Ekranın sol kenarından tamamen çıkacak hedef koordinat (X = -8.8f):
            Vector3 departTarget = new Vector3(-8.8f, undockPos.y, undockPos.z);

            // 4 Noktalı Pürüzsüz Bezier Su Rotası (Slottan öne çıkıp sola doğru tatlı bir yay çizer)
            Vector3 p0 = startPos;
            Vector3 p1 = undockPos;
            Vector3 p2 = new Vector3(Mathf.Lerp(p1.x, departTarget.x, 0.38f), undockPos.y, undockPos.z);
            Vector3 p3 = departTarget;

            float travelDist = Mathf.Abs(startPos.x - departTarget.x);
            float duration = Mathf.Clamp(travelDist * 0.15f + 0.35f, 1.25f, 2.10f);
            float elapsed = 0f;
            float lastRippleTime = 0f;
            bool slotFreed = false;

            // Kalkışta motor çalıştırma küçük su dalgası (Departure Splash)
            SpawnWaterRipple(startPos - transform.forward * 0.30f + new Vector3(0f, -0.06f, 0.02f), 0.28f, 1.05f, 0.50f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Slottan çıkınca (t >= 0.25f) slotu serbest bırak ki arkadan gelen gemi yanaşabilsin
                if (!slotFreed && t >= 0.25f)
                {
                    slotFreed = true;
                    if (m_CurrentSlot != null)
                    {
                        m_CurrentSlot.ReleaseShip();
                        m_CurrentSlot = null;
                    }
                }

                // Gerçekçi gemi ivmelenmesi (ilk %25'te tatlı hızlanma, sonra sabit seyir sürati)
                float moveT = (t < 0.25f) ? (2.0f * t * t) : (t - 0.125f) / 0.875f;
                moveT = Mathf.Clamp01(moveT);

                // Konum güncellemesi
                Vector3 currentPos = EvaluateCubicBezier(p0, p1, p2, p3, moveT);
                transform.position = currentPos;

                // Dönüş yönü: Slottan çıkarken dümen kırma (t: 0.10 -> 0.52 arasında sola dönüş)
                float turnT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.10f) / 0.42f));
                // Dönüş esnasında sola tatlı gemi yatması (Banking Roll)
                float bankRoll = Mathf.Sin(turnT * Mathf.PI) * 7.5f;
                transform.rotation = Quaternion.Slerp(startRot, leftTargetRot, turnT) * Quaternion.Euler(0f, 0f, bankRoll);

                // Arkada köpüklü su izi (Water Wake Ripples)
                if (Time.time - lastRippleTime > 0.065f)
                {
                    lastRippleTime = Time.time;
                    Vector3 wakePos = currentPos - transform.forward * 0.38f + new Vector3(0f, -0.06f, 0.02f);
                    SpawnWaterRipple(wakePos, 0.20f, 0.80f, 0.40f);
                }

                yield return null;
            }

            if (!slotFreed && m_CurrentSlot != null)
            {
                m_CurrentSlot.ReleaseShip();
                m_CurrentSlot = null;
            }

            OnDeparted?.Invoke(this);
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        /// <summary>
        /// Gemiyi bekleme sırasından hedef slota doğru gerçek bir gemi gibi kavisli Bezier su rotasıyla yüzdürür.
        /// </summary>
        public void SailToSlot(ShipSlot targetSlot, Action onComplete = null)
        {
            if (targetSlot == null) return;
            StartCoroutine(SailToSlotRoutine(targetSlot, onComplete));
        }

        private IEnumerator SailToSlotRoutine(ShipSlot targetSlot, Action onComplete)
        {
            m_IsMoving = true;
            m_EnableWaterBobbing = false;
            transform.DOKill(true);

            // Slota bağla
            targetSlot.DockShip(this);
            m_CurrentSlot = targetSlot;

            Vector3 startWorldPos = transform.position;
            Vector3 startWorldScale = transform.lossyScale;
            Quaternion startRot = transform.rotation;

            Vector3 targetLocalPos = new Vector3(0f, 0.08f, 0.02f);
            Quaternion targetSlotWorldRot = targetSlot.transform.rotation;
            Vector3 targetWorld = targetSlot.transform.TransformPoint(targetLocalPos);

            // 4 Noktalı Pürüzsüz Bezier Su Rotası
            Vector3 p0 = startWorldPos;
            Vector3 p1 = startWorldPos + new Vector3(0f, 0.75f, -0.1f);
            Vector3 p2 = targetWorld + new Vector3(0f, -0.75f, 0.1f);
            Vector3 p3 = targetWorld;

            float duration = 1.05f;
            float elapsed = 0f;
            float lastRippleTime = 0f;
            float lateralDelta = targetWorld.x - startWorldPos.x;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 currentWorldPos = EvaluateCubicBezier(p0, p1, p2, p3, easeT);
                transform.position = currentWorldPos;

                // Dünya boyutunu yelken boyunca %100 sabit tut (asla büyüme/küçülme yapmaz)
                if (transform.parent == null)
                {
                    transform.localScale = startWorldScale;
                }
                else
                {
                    transform.localScale = Vector3.one * DefaultShipScale;
                }

                // Dönüş yönüne göre hafif yatma (Banking Roll)
                float bankRoll = Mathf.Sin(easeT * Mathf.PI) * (-Mathf.Sign(lateralDelta) * Mathf.Clamp(Mathf.Abs(lateralDelta) * 5.0f, 2f, 7.5f));
                float alignWeight = Mathf.Clamp01((easeT - 0.65f) / 0.35f);
                float currentRoll = Mathf.Lerp(bankRoll, 0f, alignWeight);

                transform.rotation = Quaternion.Slerp(startRot, targetSlotWorldRot, easeT) * Quaternion.Euler(0f, 0f, currentRoll);

                if (Time.time - lastRippleTime > 0.075f)
                {
                    lastRippleTime = Time.time;
                    Vector3 wakePos = currentWorldPos + new Vector3(0f, -0.10f, 0.05f);
                    SpawnWaterRipple(wakePos, 0.18f, 0.75f, 0.45f);
                }

                yield return null;
            }

            transform.SetParent(targetSlot.transform, true);
            transform.localPosition = targetLocalPos;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * DefaultShipScale;

            m_BaseLocalPosition = targetLocalPos;
            m_BaseLocalRotation = Quaternion.identity;

            SpawnWaterRipple(transform.position, 0.35f, 1.15f, 0.6f);
            transform.DOPunchScale(new Vector3(0.08f, -0.08f, 0.08f) * DefaultShipScale, 0.28f, 3, 0.4f)
                .OnComplete(() => transform.localScale = Vector3.one * DefaultShipScale);

            m_IsMoving = false;
            m_IsDocked = true;
            m_EnableWaterBobbing = true;

            if (ShipDispatcher.Instance != null)
            {
                ShipDispatcher.Instance.OnShipDocked(this);
            }

            onComplete?.Invoke();
        }

        private static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 p = uuu * p0;
            p += 3f * uu * t * p1;
            p += 3f * u * tt * p2;
            p += ttt * p3;
            return p;
        }

        /// <summary>
        /// Su üzerinde genişleyip sönen beyaz köpük dalgası (Water Ripple Ring) oluşturur.
        /// </summary>
        public static void SpawnWaterRipple(Vector3 worldPos, float startScale = 0.2f, float maxScale = 0.75f, float duration = 0.45f)
        {
            GameObject ripple = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ripple.name = "WaterRipple_FX";
            ripple.transform.position = new Vector3(worldPos.x, worldPos.y, worldPos.z + 0.02f);
            ripple.transform.rotation = Quaternion.Euler(-68f, 0f, 0f);
            ripple.transform.localScale = Vector3.one * startScale;

            Collider col = ripple.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            MeshRenderer mr = ripple.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlitShader == null) unlitShader = Shader.Find("Unlit/Transparent");

                Material mat = new Material(unlitShader);
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.65f));
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                ripple.transform.DOScale(Vector3.one * maxScale, duration).SetEase(Ease.OutCubic);
                mat.DOFade(0f, "_BaseColor", duration).SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        if (Application.isPlaying)
                        {
                            Destroy(mat);
                            Destroy(ripple);
                        }
                        else
                        {
                            DestroyImmediate(mat);
                            DestroyImmediate(ripple);
                        }
                    });
            }
            else
            {
                if (Application.isPlaying) Destroy(ripple, duration);
                else DestroyImmediate(ripple);
            }
        }

        /// <summary>
        /// Gemi, ShipController dışından (ör. ShipQueuePool'un arkadan gelme / öne kayma
        /// kuyruk animasyonları) DOTween ile taşınırken çağrılmalıdır. Su sallanması (bobbing)
        /// her karede pozisyonu eski "dinlenme" tabanına geri çektiği için, bu susturulmazsa
        /// dışarıdan yapılan pozisyon animasyonu her karede ezilip gemi hiç ilerlemiyormuş gibi
        /// görünür. Animasyon bitince `false` ile çağırıp geminin O ANKİ konumunu/rotasyonunu
        /// yeni dinlenme tabanı olarak kaydet — aksi halde bir sonraki bobbing karesi gemiyi
        /// eski (animasyon öncesi) konuma geri çeker.
        /// </summary>
        public void SetQueueAnimating(bool animating)
        {
            m_IsMoving = animating;
            if (!animating)
            {
                m_BaseLocalPosition = transform.localPosition;
                m_BaseLocalRotation = transform.localRotation;
            }
        }

        /// <summary>
        /// Slotlar doluysa veya geçersiz tıklamada gemi iki yana sallanır (Wobble).
        /// </summary>
        public void PlayWobble()
        {
            if (m_IsMoving || m_IsDeparting) return;
            transform.DOKill(true);
            transform.DOShakeRotation(0.35f, new Vector3(0f, 0f, 15f), 12, 90f, true)
                .OnComplete(() => transform.localRotation = m_BaseLocalRotation);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (m_IsDocked || m_IsMoving || m_IsDeparting) return;

            if (ShipDispatcher.Instance != null)
            {
                ShipDispatcher.Instance.TrySendShipFromQueue(this);
            }
        }

        private void EnsureVisualComponents()
        {
            // FBX içindeki statik çok renkli kargo bloklarını (cargo-b, cargo-c) tamamen gizle (güverte boş başlar)
            foreach (Transform child in transform)
            {
                string cName = child.name.ToLowerInvariant();
                if (cName.Contains("cargo-b") || cName.Contains("cargo-c") || cName.Contains("cargo_b") || cName.Contains("cargo_c"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            m_Renderers = GetComponentsInChildren<MeshRenderer>(true);

            BoxCollider col = GetComponent<BoxCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider>();
            }
            col.size = new Vector3(2.2f, 2.5f, 4.2f);
            col.center = new Vector3(0f, 1.0f, 0f);
            col.isTrigger = true;

            EnsureCargoDeck();
        }

        private void EnsureCargoDeck()
        {
            if (m_CargoDeckRoot != null) return;

            Transform deck = transform.Find("[CargoDeck]");
            if (deck != null)
            {
                m_CargoDeckRoot = deck;
            }
            else
            {
                GameObject deckObj = new GameObject("[CargoDeck]");
                deckObj.transform.SetParent(transform, false);
                deckObj.transform.localPosition = Vector3.zero;
                deckObj.transform.localRotation = Quaternion.identity;
                deckObj.transform.localScale = Vector3.one;
                m_CargoDeckRoot = deckObj.transform;
            }
        }

        /// <summary>
        /// Güvertedeki tüm varilleri temizler.
        /// </summary>
        public void ClearCargoBarrels()
        {
            EnsureCargoDeck();
            if (m_CargoDeckRoot != null)
            {
                for (int i = m_CargoDeckRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = m_CargoDeckRoot.GetChild(i);
                    if (child != null)
                    {
                        if (Application.isPlaying) Destroy(child.gameObject);
                        else DestroyImmediate(child.gameObject);
                    }
                }
            }
            m_SpawnedBarrels.Clear();
        }

        /// <summary>
        /// Güvertedeki belirli bir indeksli varilin yerel (local) koordinatını hesaplar.
        /// 2 sütun x 4 satır taban düzenindedir; 8'den fazla ise 2. kat olarak üstlerine istiflenir.
        /// </summary>
        public Vector3 GetDeckLocalPositionForBarrel(int index)
        {
            int layer = (index >= 8 && m_Capacity > 8) ? 1 : 0;
            int indexInLayer = (layer == 1) ? (index - 8) : index;

            int col = indexInLayer % 2;
            int row = indexInLayer / 2;

            float posX = (col == 0) ? -0.36f : 0.36f;
            float posY = (layer == 0) ? 0.52f : 1.02f;
            float posZ = -0.95f - (row * 0.72f);

            return new Vector3(posX, posY, posZ);
        }

        private Material GetOrCreateBarrelMaterial()
        {
            if (m_BarrelSharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Toony Colors Pro 2/Hybrid Shader");
                if (shader == null) shader = Shader.Find("Standard");
                m_BarrelSharedMaterial = new Material(shader);
                m_BarrelSharedMaterial.name = "Ship_CargoBarrel_Mat";
            }

            m_BarrelSharedMaterial.SetColor("_BaseColor", m_ShipColor);
            m_BarrelSharedMaterial.SetColor("_Color", m_ShipColor);

            if (m_BarrelSharedMaterial.HasProperty("_HColor"))
            {
                m_BarrelSharedMaterial.SetColor("_HColor", Color.Lerp(m_ShipColor, Color.white, 0.40f));
            }
            if (m_BarrelSharedMaterial.HasProperty("_SColor"))
            {
                m_BarrelSharedMaterial.SetColor("_SColor", Color.Lerp(m_ShipColor, new Color(0.15f, 0.18f, 0.28f, 1f), 0.45f));
            }
            if (m_BarrelSharedMaterial.HasProperty("_EmissionColor"))
            {
                m_BarrelSharedMaterial.EnableKeyword("_EMISSION");
                m_BarrelSharedMaterial.SetColor("_EmissionColor", m_ShipColor * 0.22f);
            }
            if (m_BarrelSharedMaterial.HasProperty("_Smoothness"))
            {
                m_BarrelSharedMaterial.SetFloat("_Smoothness", 0.60f);
            }

            return m_BarrelSharedMaterial;
        }

        private void UpdateAllBarrelsColor()
        {
            if (m_BarrelSharedMaterial != null)
            {
                GetOrCreateBarrelMaterial();
            }
            foreach (var b in m_SpawnedBarrels)
            {
                if (b != null)
                {
                    var mr = b.GetComponent<MeshRenderer>();
                    if (mr != null) mr.sharedMaterial = GetOrCreateBarrelMaterial();
                }
            }
        }

        /// <summary>
        /// Güverteye geminin renginde yeni bir 3D kargo varili ekler ve zıplatarak gösterir.
        /// </summary>
        private void SpawnCargoBarrel(int index)
        {
            EnsureCargoDeck();
            if (m_CargoDeckRoot == null) return;

            Vector3 localPos = GetDeckLocalPositionForBarrel(index);
            Vector3 targetScale = new Vector3(0.50f, 0.38f, 0.50f);

#if UNITY_EDITOR
            if (s_BarrelMesh == null)
            {
                GameObject barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FreeLowpolyScifiObjects/Prefabs/Barrels/barrel.prefab");
                if (barrelPrefab != null)
                {
                    MeshFilter mf = barrelPrefab.GetComponent<MeshFilter>();
                    if (mf != null) s_BarrelMesh = mf.sharedMesh;
                }
            }
#endif

            GameObject barrelObj;
            if (s_BarrelMesh != null)
            {
                barrelObj = new GameObject($"Barrel_{index}");
                MeshFilter mf = barrelObj.AddComponent<MeshFilter>();
                mf.sharedMesh = s_BarrelMesh;
                MeshRenderer mr = barrelObj.AddComponent<MeshRenderer>();
                mr.sharedMaterial = GetOrCreateBarrelMaterial();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
                targetScale = Vector3.one * 0.40f;
            }
            else
            {
                barrelObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrelObj.name = $"Barrel_{index}";
                Collider col = barrelObj.GetComponent<Collider>();
                if (col != null) Destroy(col);
                MeshRenderer mr = barrelObj.GetComponent<MeshRenderer>();
                mr.sharedMaterial = GetOrCreateBarrelMaterial();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
            }

            barrelObj.transform.SetParent(m_CargoDeckRoot, false);
            barrelObj.transform.localPosition = localPos;
            barrelObj.transform.localRotation = Quaternion.identity;
            barrelObj.transform.localScale = Vector3.zero;

            m_SpawnedBarrels.Add(barrelObj);

            // Tatlı yaylanarak beliren Juice Pop animasyonu
            barrelObj.transform.DOScale(targetScale, 0.22f).SetEase(Ease.OutBack);
        }

        private static Sprite s_CircleRingSprite;

        /// <summary>
        /// İçi %100 şeffaf, etrafı SAF BEYAZ yuvarlak dairesel çerçeveden (White Ring Frame) oluşan 2D Sprite üretir.
        /// </summary>
        public static Sprite GetCircleRingSprite()
        {
            if (s_CircleRingSprite != null) return s_CircleRingSprite;

            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float center = size * 0.5f;
            float radius = size * 0.46f;
            float borderWidth = size * 0.09f; // Dış beyaz çember halkası genişliği

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= radius + 1f && dist >= radius - borderWidth - 1f)
                    {
                        float outerFade = Mathf.Clamp01(radius + 1f - dist);
                        float innerFade = Mathf.Clamp01(dist - (radius - borderWidth - 1f));
                        float alpha = Mathf.Min(outerFade, innerFade);
                        // SAF BEYAZ yuvarlak çerçeve halkası
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        // İÇİ %100 ŞEFFAF (Dahili dolgu YOK, arkaplansız!)
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            s_CircleRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return s_CircleRingSprite;
        }

        public static void InvalidateRingSprite()
        {
            s_CircleRingSprite = null;
        }

        /// <summary>
        /// Geminin üzerine arkaplansız, dairesel BEYAZ çerçeveli yuvarlak rozet + %100 SAF BEYAZ ortalanmış rakam oluşturur.
        /// Kullanıcının isteği: Text ve dairesel çerçeve dahil TÜM detaylar %100 SAF BEYAZDIR, siyah kontur veya gölge içermez.
        /// Standart TrueType font (LilitaOne-Regular) ile %100 temiz, keskin rakam görünümü sağlar.
        /// </summary>
        private void CreateOrFindBadge()
        {
            Transform existingCanvas = transform.Find("Ship_Capacity_Canvas");
            GameObject canvasObj;

            if (existingCanvas != null && existingCanvas.GetComponent<RectTransform>() == null)
            {
                if (Application.isPlaying) Destroy(existingCanvas.gameObject);
                else DestroyImmediate(existingCanvas.gameObject);
                existingCanvas = null;
            }

            if (existingCanvas != null)
            {
                canvasObj = existingCanvas.gameObject;
            }
            else
            {
                canvasObj = new GameObject("Ship_Capacity_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvasObj.transform.SetParent(transform, false);
                canvasObj.transform.localPosition = new Vector3(0f, 2.7f, 0.25f);
                canvasObj.transform.localRotation = Quaternion.identity;
                canvasObj.transform.localScale = Vector3.one * 0.04f;

                Canvas canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 3000;

                Camera cam = Camera.main;
                if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
                if (cam != null) canvas.worldCamera = cam;

                CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 10;
            }

            m_BadgeCanvasObj = canvasObj;

            // Eski glitched TextMeshPro bileşenlerini TAMAMEN yok et
            TextMeshProUGUI[] tmps = canvasObj.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < tmps.Length; i++)
            {
                if (tmps[i] != null)
                {
                    if (Application.isPlaying) Destroy(tmps[i]);
                    else DestroyImmediate(tmps[i]);
                }
            }
            m_BadgeText = null;

            // Siyah kontur ve gölge bileşenlerini yok et (Kullanıcı: "hep beyaz olsun her detayı çerçevesi dahil")
            Outline[] outlines = canvasObj.GetComponentsInChildren<Outline>(true);
            for (int i = 0; i < outlines.Length; i++)
            {
                if (outlines[i] != null)
                {
                    if (Application.isPlaying) Destroy(outlines[i]);
                    else DestroyImmediate(outlines[i]);
                }
            }

            Shadow[] shadows = canvasObj.GetComponentsInChildren<Shadow>(true);
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null)
                {
                    if (Application.isPlaying) Destroy(shadows[i]);
                    else DestroyImmediate(shadows[i]);
                }
            }

            Transform bgTr = canvasObj.transform.Find("Badge_CircleRing");
            GameObject bgObj;
            if (bgTr != null && bgTr.GetComponent<RectTransform>() == null)
            {
                if (Application.isPlaying) Destroy(bgTr.gameObject);
                else DestroyImmediate(bgTr.gameObject);
                bgTr = null;
            }

            if (bgTr != null)
            {
                bgObj = bgTr.gameObject;
            }
            else
            {
                bgObj = new GameObject("Badge_CircleRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bgObj.transform.SetParent(canvasObj.transform, false);
            }

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(100f, 100f);
            bgRect.anchoredPosition = Vector2.zero;

            m_BadgeImage = bgObj.GetComponent<Image>();
            m_BadgeImage.enabled = true;
            m_BadgeImage.sprite = GetCircleRingSprite();
            m_BadgeImage.color = Color.white; // SAF BEYAZ ÇERÇEVE
            m_BadgeImage.raycastTarget = false;

            // 3. UI Text Nesnesi (Badge_Text)
            Transform textTr = bgObj.transform.Find("Badge_Text");
            GameObject textObj;
            if (textTr != null && textTr.GetComponent<RectTransform>() == null)
            {
                if (Application.isPlaying) Destroy(textTr.gameObject);
                else DestroyImmediate(textTr.gameObject);
                textTr = null;
            }

            if (textTr != null)
            {
                textObj = textTr.gameObject;
            }
            else
            {
                textObj = new GameObject("Badge_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textObj.transform.SetParent(bgObj.transform, false);
            }

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            m_BadgeUIText = textObj.GetComponent<Text>();
            m_BadgeUIText.gameObject.SetActive(true);

            Font fontToUse = null;
#if UNITY_EDITOR
            fontToUse = AssetDatabase.LoadAssetAtPath<Font>(TTFFontPath);
#endif
            if (fontToUse == null) fontToUse = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            m_BadgeUIText.font = fontToUse;
            m_BadgeUIText.fontSize = 54;
            m_BadgeUIText.fontStyle = FontStyle.Normal;
            m_BadgeUIText.alignment = TextAnchor.MiddleCenter;
            m_BadgeUIText.color = Color.white; // %100 SAF BEYAZ YAZI
            m_BadgeUIText.horizontalOverflow = HorizontalWrapMode.Overflow;
            m_BadgeUIText.verticalOverflow = VerticalWrapMode.Overflow;
            m_BadgeUIText.raycastTarget = false;

            UpdateBadgePlacement();
            UpdateBadgeText();
        }

        private void UpdateBadgeText()
        {
            int remaining = RemainingCapacity;

            // Kullanıcı isteği: "textteki sayı dolduğunda text yok olsun"
            // Kapasite dolduğunda (kalan <= 0 veya IsFull) text ve dairesel çerçeve HEMEN yok olur!
            if (remaining <= 0 || IsFull || m_IsDeparting)
            {
                if (m_BadgeCanvasObj != null && m_BadgeCanvasObj.activeSelf)
                    m_BadgeCanvasObj.SetActive(false);
                if (m_BadgeUIText != null && m_BadgeUIText.gameObject.activeSelf)
                    m_BadgeUIText.gameObject.SetActive(false);
                if (m_BadgeImage != null && m_BadgeImage.gameObject.activeSelf)
                    m_BadgeImage.gameObject.SetActive(false);
                return;
            }

            string countStr = remaining.ToString();

            if (m_BadgeCanvasObj != null && !m_BadgeCanvasObj.activeSelf)
                m_BadgeCanvasObj.SetActive(true);
            if (m_BadgeUIText != null)
            {
                if (!m_BadgeUIText.gameObject.activeSelf) m_BadgeUIText.gameObject.SetActive(true);
                m_BadgeUIText.text = countStr;
            }
            if (m_BadgeImage != null && !m_BadgeImage.gameObject.activeSelf)
                m_BadgeImage.gameObject.SetActive(true);
        }
    }
}
// Trigger reload: 2026-09-23 20:27

