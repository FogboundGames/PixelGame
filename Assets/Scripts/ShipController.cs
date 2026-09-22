using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;

namespace PixelGame
{
    /// <summary>
    /// Su üzerindeki tek bir kargo gemisini (ship-cargo-a) yönetir.
    /// Renk, kapasite rozeti, su salınımı (bobbing), slota yanaşma ve kargo dolunca
    /// açık denize yelken açma (sail-away) animasyonlarını içerir.
    /// </summary>
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
        [SerializeField] private float m_BobHeight = 0.04f;
        [SerializeField] private float m_RollAngle = 2.2f;
        [SerializeField] private float m_PitchAngle = 1.4f;

        [Header("🏷️ Kapasite Rozeti (3D / UI Badge)")]
        [SerializeField] private GameObject m_BadgeRoot;
        [SerializeField] private TextMeshPro m_BadgeText;
        [SerializeField] private SpriteRenderer m_BadgeBackground;

        // Dahili referanslar
        private MeshRenderer[] m_Renderers;
        private MaterialPropertyBlock m_PropBlock;
        private float m_BobRandomOffset;
        private Vector3 m_BaseLocalPosition;
        private Quaternion m_BaseLocalRotation;
        private Vector3 m_BaseLocalScale;

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

        private void Awake()
        {
            m_PropBlock = new MaterialPropertyBlock();
            m_BobRandomOffset = UnityEngine.Random.Range(0f, 100f);
            m_BaseLocalScale = transform.localScale;

            EnsureVisualComponents();
            CreateOrFindBadge();
        }

        private void Start()
        {
            m_BaseLocalPosition = transform.localPosition;
            m_BaseLocalRotation = transform.localRotation;
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

            ApplyColorToShip(color);
            UpdateBadgeText();
        }

        /// <summary>
        /// Geminin materyallerine Toony Colors Pro rengi uygular.
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
                m_PropBlock.SetColor("_HColor", Color.Lerp(color, Color.white, 0.45f));
                m_PropBlock.SetColor("_SColor", Color.Lerp(color, new Color(0.1f, 0.15f, 0.28f, 1f), 0.55f));
                m_PropBlock.SetColor("_RimColor", new Color(0.4f, 0.85f, 1.0f, 0.7f));
                m_PropBlock.SetColor("_PlasticHighlightColor", Color.white);
                mr.SetPropertyBlock(m_PropBlock);
            }

            if (m_BadgeBackground != null)
            {
                m_BadgeBackground.color = color;
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
            transform.DOPunchScale(new Vector3(0.08f, -0.08f, 0.08f) * m_BaseLocalScale.x, 0.18f, 4, 0.5f)
                .OnComplete(() => transform.localScale = m_BaseLocalScale);

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
            // 1. Kutlama zıplaması & rozet gizleme
            if (m_BadgeRoot != null)
            {
                m_BadgeRoot.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack);
            }

            // Hafif havaya kalkıp suya iniş
            Vector3 currentPos = transform.position;
            yield return transform.DOMoveY(currentPos.y + 0.35f, 0.22f).SetEase(Ease.OutQuad).WaitForCompletion();
            yield return transform.DOMoveY(currentPos.y, 0.20f).SetEase(Ease.OutBounce).WaitForCompletion();

            // 2. Açık denize doğru (aşağıya/ekran dışına) hızlanarak süzülme
            Vector3 departTarget = transform.position + new Vector3(0f, -6.5f, 0.5f);
            transform.DORotate(new Vector3(12f, 0f, 0f), 0.6f, RotateMode.WorldAxisAdd);

            yield return transform.DOMove(departTarget, 1.4f).SetEase(Ease.InQuad).WaitForCompletion();

            if (m_CurrentSlot != null)
            {
                m_CurrentSlot.ReleaseShip();
                m_CurrentSlot = null;
            }

            OnDeparted?.Invoke(this);
            Destroy(gameObject);
        }

        /// <summary>
        /// Gemiyi bekleme sırasından hedef slota doğru animasyonla yüzdürür.
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

            Vector3 startWorld = transform.position;
            Quaternion startRot = transform.rotation;

            // Slota bağla
            targetSlot.DockShip(this);
            m_CurrentSlot = targetSlot;

            Vector3 targetLocalPos = new Vector3(0f, 0.08f, 0.02f);
            Quaternion targetLocalRot = Quaternion.Euler(0f, 0f, 0f);

            transform.SetParent(targetSlot.transform, true);

            float duration = 0.65f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                // Kavisli su hareketi
                Vector3 currentLocal = Vector3.Lerp(transform.parent.InverseTransformPoint(startWorld), targetLocalPos, ease);
                float arcY = Mathf.Sin(t * Mathf.PI) * 0.25f;
                currentLocal.y += arcY;

                transform.localPosition = currentLocal;
                transform.localRotation = Quaternion.Slerp(startRot, targetSlot.transform.rotation * targetLocalRot, ease);

                yield return null;
            }

            transform.localPosition = targetLocalPos;
            transform.localRotation = targetLocalRot;
            m_BaseLocalPosition = targetLocalPos;
            m_BaseLocalRotation = targetLocalRot;

            // Suya iniş sıçraması
            transform.DOPunchScale(new Vector3(0.06f, -0.06f, 0.06f) * m_BaseLocalScale.x, 0.2f, 3, 0.4f)
                .OnComplete(() => transform.localScale = m_BaseLocalScale);

            m_IsMoving = false;
            m_IsDocked = true;
            m_EnableWaterBobbing = true;

            onComplete?.Invoke();
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

            // Bekleme sırasındayken tıklandığında dispatcher üzerinden slota gitmeyi dene
            if (ShipDispatcher.Instance != null)
            {
                ShipDispatcher.Instance.TrySendShipFromQueue(this);
            }
        }

        private void EnsureVisualComponents()
        {
            m_Renderers = GetComponentsInChildren<MeshRenderer>(true);

            // Tıklama için 3D BoxCollider
            BoxCollider col = GetComponent<BoxCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider>();
            }
            col.size = new Vector3(2.2f, 2.5f, 4.2f);
            col.center = new Vector3(0f, 1.0f, 0f);
            col.isTrigger = true;
        }

        private void CreateOrFindBadge()
        {
            Transform existingBadge = transform.Find("Ship_Capacity_Badge");
            if (existingBadge != null)
            {
                m_BadgeRoot = existingBadge.gameObject;
                m_BadgeText = existingBadge.GetComponentInChildren<TextMeshPro>(true);
                m_BadgeBackground = existingBadge.GetComponentInChildren<SpriteRenderer>(true);
                return;
            }

            GameObject badgeObj = new GameObject("Ship_Capacity_Badge");
            badgeObj.transform.SetParent(transform, false);
            // Pruvanın/kabinin üzerinde net görünen konum:
            badgeObj.transform.localPosition = new Vector3(0f, 2.3f, 0.4f);
            badgeObj.transform.localRotation = Quaternion.Euler(68f, 0f, 0f); // Kameraya tam dik baksın
            badgeObj.transform.localScale = Vector3.one * 0.85f;

            m_BadgeRoot = badgeObj;

            // Rozet Arka Planı (SpriteRenderer)
            GameObject bgObj = new GameObject("Badge_BG");
            bgObj.transform.SetParent(badgeObj.transform, false);
            bgObj.transform.localPosition = Vector3.zero;
            bgObj.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            m_BadgeBackground = bgObj.AddComponent<SpriteRenderer>();
            m_BadgeBackground.sortingOrder = 50;

            // TextMeshPro Sayı
            GameObject textObj = new GameObject("Badge_Text");
            textObj.transform.SetParent(badgeObj.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            textObj.transform.localScale = Vector3.one * 0.75f;

            m_BadgeText = textObj.AddComponent<TextMeshPro>();
            m_BadgeText.alignment = TextAlignmentOptions.Center;
            m_BadgeText.fontSize = 7.5f;
            m_BadgeText.fontStyle = FontStyles.Bold;
            m_BadgeText.color = Color.white;
            m_BadgeText.sortingOrder = 55;
            m_BadgeText.text = m_Capacity.ToString();
        }

        private void UpdateBadgeText()
        {
            if (m_BadgeText != null)
            {
                int remaining = RemainingCapacity;
                m_BadgeText.text = remaining.ToString();
            }
        }
    }
}
