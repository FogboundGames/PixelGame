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

            if (m_IsDeparting)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();

            if (cam != null)
            {
                m_BadgeCanvasObj.transform.rotation = cam.transform.rotation;
                // Kaptan köşkünün tam üstü (Kameraya 0.22f daha yakın, çatı üstünde ferah konum):
                Vector3 headTopPos = transform.position + cam.transform.up * 0.52f - cam.transform.forward * 0.22f;
                m_BadgeCanvasObj.transform.position = headTopPos;
            }

            float lossy = transform.lossyScale.x;
            if (Mathf.Abs(lossy) < 0.0001f) lossy = 1f;
            m_BadgeCanvasObj.transform.localScale = Vector3.one * (0.0055f / lossy);

            if (!m_BadgeCanvasObj.activeSelf && !m_IsDeparting)
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

        /// <summary>
        /// Gemiyi belirli bir renk ve kapasite ile yapılandırır.
        /// </summary>
        public void Configure(Color color, int capacity, string colorName = "")
        {
            m_ShipColor = color;
            m_Capacity = Mathf.Max(1, capacity);
            m_CurrentCargo = 0;
            m_ColorName = string.IsNullOrEmpty(colorName) ? ColorUtility.ToHtmlStringRGB(color) : colorName;

            CreateOrFindBadge();
            ApplyColorToShip(color);
            UpdateBadgeText();
        }

        /// <summary>
        /// Geminin materyallerine Toony Colors Pro cel-shading rengi uygular.
        /// </summary>
        public void ApplyColorToShip(Color color)
        {
            m_ShipColor = color;
            if (m_Renderers == null || m_Renderers.Length == 0)
            {
                m_Renderers = GetComponentsInChildren<MeshRenderer>(true);
            }

            if (m_PropBlock == null) m_PropBlock = new MaterialPropertyBlock();

            foreach (var mr in m_Renderers)
            {
                if (mr == null) continue;
                mr.GetPropertyBlock(m_PropBlock);
                m_PropBlock.SetColor("_BaseColor", Color.white);
                m_PropBlock.SetColor("_HColor", Color.Lerp(color, Color.white, 0.40f));
                m_PropBlock.SetColor("_SColor", Color.Lerp(color, new Color(0.12f, 0.18f, 0.32f, 1f), 0.50f));
                m_PropBlock.SetColor("_RimColor", new Color(0.4f, 0.85f, 1.0f, 0.75f));
                m_PropBlock.SetColor("_PlasticHighlightColor", Color.white);
                mr.SetPropertyBlock(m_PropBlock);
            }

            if (m_BadgeImage != null)
            {
                m_BadgeImage.color = Color.Lerp(color, Color.white, 0.20f);
            }
        }

        /// <summary>
        /// Gemiye kargo (küp parçacığı) ekler.
        /// </summary>
        public void AddCargo(int amount = 1)
        {
            if (m_IsDeparting) return;

            m_CurrentCargo = Mathf.Min(m_Capacity, m_CurrentCargo + amount);
            UpdateBadgeText();

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
        /// Kargo dolduğunda gemi kutlama yapar ve açık denize doğru uzaklaşır.
        /// </summary>
        public void DepartAndFreeSlot()
        {
            if (m_IsDeparting) return;
            m_IsDeparting = true;
            m_EnableWaterBobbing = false;

            StartCoroutine(DepartRoutine());
        }

        private IEnumerator DepartRoutine()
        {
            if (m_BadgeCanvasObj != null)
            {
                m_BadgeCanvasObj.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack);
            }

            Vector3 currentPos = transform.position;
            yield return transform.DOMoveY(currentPos.y + 0.35f, 0.22f).SetEase(Ease.OutQuad).WaitForCompletion();
            yield return transform.DOMoveY(currentPos.y, 0.20f).SetEase(Ease.OutBounce).WaitForCompletion();

            Vector3 departTarget = transform.position + new Vector3(0f, -6.5f, 0.5f);
            transform.DORotate(new Vector3(12f, 0f, 0f), 0.6f, RotateMode.WorldAxisAdd);

            float departDur = 1.4f;
            float elapsed = 0f;
            Vector3 departStart = transform.position;

            while (elapsed < departDur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / departDur;
                transform.position = Vector3.Lerp(departStart, departTarget, t * t);

                if (UnityEngine.Random.value < 0.35f)
                {
                    SpawnWaterRipple(transform.position + new Vector3(0f, -0.15f, 0.05f), 0.25f, 0.9f, 0.55f);
                }
                yield return null;
            }

            if (m_CurrentSlot != null)
            {
                m_CurrentSlot.ReleaseShip();
                m_CurrentSlot = null;
            }

            OnDeparted?.Invoke(this);
            Destroy(gameObject);
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
            if (col != null) Destroy(col);

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
                        Destroy(mat);
                        Destroy(ripple);
                    });
            }
            else
            {
                Destroy(ripple, duration);
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
            m_Renderers = GetComponentsInChildren<MeshRenderer>(true);

            BoxCollider col = GetComponent<BoxCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider>();
            }
            col.size = new Vector3(2.2f, 2.5f, 4.2f);
            col.center = new Vector3(0f, 1.0f, 0f);
            col.isTrigger = true;
        }

        /// <summary>
        /// World Space Canvas tabanlı, asla kaybolmayan, yüksek çözünürlüklü kapasite rozeti oluşturur.
        /// </summary>
        private void CreateOrFindBadge()
        {
            Transform existingCanvas = transform.Find("Ship_Capacity_Canvas");
            if (existingCanvas != null)
            {
                m_BadgeCanvasObj = existingCanvas.gameObject;
                m_BadgeText = existingCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
                m_BadgeUIText = existingCanvas.GetComponentInChildren<Text>(true);
                m_BadgeImage = existingCanvas.GetComponentInChildren<Image>(true);
            }
            else
            {
                Material alwaysOnTopMat = GetAlwaysOnTopMaterial();

                // 1. World Space Canvas
                GameObject canvasObj = new GameObject("Ship_Capacity_Canvas");
                canvasObj.transform.SetParent(transform, false);
                canvasObj.transform.localPosition = new Vector3(0f, 2.7f, 0.25f);
                canvasObj.transform.localRotation = Quaternion.identity;
                canvasObj.transform.localScale = Vector3.one * 0.04f;

                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 300; // En üstte net görünür

                Camera cam = Camera.main;
                if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
                if (cam != null) canvas.worldCamera = cam;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 10;

                m_BadgeCanvasObj = canvasObj;

                // 2. Rozet Arka Plan Görseli (Image)
                GameObject bgObj = new GameObject("Badge_BG");
                bgObj.transform.SetParent(canvasObj.transform, false);

                RectTransform bgRect = bgObj.AddComponent<RectTransform>();
                bgRect.sizeDelta = new Vector2(130f, 75f);
                bgRect.anchoredPosition = Vector2.zero;

                m_BadgeImage = bgObj.AddComponent<Image>();
                m_BadgeImage.raycastTarget = false;

#if UNITY_EDITOR
                Sprite pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Badge_JuicyPill.png");
                if (pillSprite == null) pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BadgeSpritePath);
                if (pillSprite == null) pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Count_FullPlate.png");
                if (pillSprite != null) m_BadgeImage.sprite = pillSprite;
#endif

                // 3. TextMeshProUGUI Sayı Metni
                GameObject textObj = new GameObject("Badge_Text");
                textObj.transform.SetParent(bgObj.transform, false);

                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0.05f, 0.05f);
                textRect.anchorMax = new Vector2(0.95f, 0.95f);
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                m_BadgeText = textObj.AddComponent<TextMeshProUGUI>();
                m_BadgeText.alignment = TextAlignmentOptions.Center;
                m_BadgeText.enableWordWrapping = false;
                m_BadgeText.overflowMode = TextOverflowModes.Overflow;
                m_BadgeText.enableAutoSizing = true;
                m_BadgeText.fontSizeMin = 24f;
                m_BadgeText.fontSizeMax = 50f;
                m_BadgeText.fontSize = 46f;
                m_BadgeText.fontStyle = FontStyles.Bold;
                m_BadgeText.color = Color.white;
                m_BadgeText.outlineColor = new Color32(15, 15, 25, 255);
                m_BadgeText.outlineWidth = 0.28f;
                m_BadgeText.raycastTarget = false;

#if UNITY_EDITOR
                TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                if (fontAsset != null)
                {
                    m_BadgeText.font = fontAsset;
                }
#endif
            }

            UpdateBadgePlacement();
            UpdateBadgeText();
        }

        private void UpdateBadgeText()
        {
            int remaining = RemainingCapacity;
            string countStr = remaining.ToString();

            if (m_BadgeText != null)
            {
                m_BadgeText.text = countStr;
            }
            if (m_BadgeUIText != null)
            {
                m_BadgeUIText.text = countStr;
            }
        }
    }
}
