using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Vagonun üzerinde kalan parça/küp sayısını gösteren dinamik rozet (Badge).
    /// Kullanıcının referans görselindeki gibi:
    /// - Kalın beyaz yazı (#FFFFFF),
    /// - Her yönden belirgin koyu/siyah dış çizgi (Outline) ve hafif derinlik gölgesi (Shadow),
    /// - Arka planda fazlalık kutu olmadan doğrudan vagonun üzerinde temiz ve doğal duruş,
    /// - Her zaman kameraya dik bakan Billboard modu,
    /// - Küp yüklendiğinde tatlı bir büyüme-küçülme (DOPunchScale) geri bildirimi.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Wagon Capacity Badge")]
    public class WagonCapacityBadge : MonoBehaviour
    {
        private Canvas m_Canvas;
        private RectTransform m_BadgeRect;
        private Image m_Background;
        private Text m_Text;
        private Outline m_Outline1;
        private Outline m_Outline2;
        private Shadow m_Shadow;
        private int m_CurrentCount = -1;

        [Header("📐 Görsel Stil")]
        [Tooltip("Arka plan kutusu görünsün mü? (Referans görselde doğrudan vagon üstündedir, varsayılan: false)")]
        [SerializeField] private bool m_ShowBackgroundBox = false;

        [Tooltip("Otomatik olarak vagonun 3D sınırlarını (bounds) bulup en üstüne ortala")]
        [SerializeField] private bool m_AutoCenterOnMesh = true;

        [Tooltip("Vagonun merkezinden ne kadar yukarıda duracağı (manuel ofset)")]
        [SerializeField] private Vector3 m_ManualOffset = new Vector3(0f, 0.52f, 0f);

        [Tooltip("Rozetin ölçeği (manuel fallback)")]
        [SerializeField] private float m_ManualScale = 0.008f;

        public int CurrentCount => m_CurrentCount;

        private void Awake()
        {
            EnsureBadgeUI();
        }

        private void OnEnable()
        {
            EnsureBadgeUI();
            UpdatePlacement();
        }

        private void LateUpdate()
        {
            UpdatePlacement();
        }

        /// <summary>
        /// Rozeti vagonun en tepe yüzeyine yerleştirir ve kameraya tam dik bakmasını sağlar (Billboard).
        /// </summary>
        private void UpdatePlacement()
        {
            if (m_Canvas == null) return;

            Camera cam = Camera.main;
            if (cam != null)
            {
                m_Canvas.transform.rotation = cam.transform.rotation;
            }

            if (!m_AutoCenterOnMesh)
            {
                m_Canvas.transform.localPosition = m_ManualOffset;
                m_Canvas.transform.localScale = Vector3.one * m_ManualScale;
                return;
            }

            // Vagonun render sınırlarını hesapla
            Renderer[] rends = GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds();
            bool found = false;

            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (r == null) continue;
                if (r.transform.IsChildOf(m_Canvas.transform)) continue;

                if (!found)
                {
                    b = r.bounds;
                    found = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            if (found && b.size.magnitude > 0.01f)
            {
                // Vagonun dünya uzayındaki tepe noktası + hafif yukarı ofset
                Vector3 worldTop = new Vector3(b.center.x, b.max.y + (b.size.y * 0.06f), b.center.z);
                m_Canvas.transform.position = worldTop;

                // Vagon genişliğinin ~%55'i kadar ölçekle (140 UI birimi referans)
                float wagonSize = Mathf.Max(b.size.x, b.size.z);
                float targetWorldSize = wagonSize * 0.55f;
                float targetScale = targetWorldSize / 140f;

                m_Canvas.transform.localScale = Vector3.one * Mathf.Max(0.001f, targetScale);
            }
            else
            {
                m_Canvas.transform.localPosition = m_ManualOffset;
                m_Canvas.transform.localScale = Vector3.one * m_ManualScale;
            }
        }

        /// <summary>
        /// Kalan küp sayısını günceller. Değiştiğinde punch animasyonu yapar.
        /// </summary>
        public void SetCount(int remainingCount, bool punchAnimation = true)
        {
            EnsureBadgeUI();
            if (m_Text == null) return;

            int prev = m_CurrentCount;
            m_CurrentCount = Mathf.Max(0, remainingCount);
            m_Text.text = m_CurrentCount.ToString();

            if (punchAnimation && prev != -1 && prev != m_CurrentCount && m_BadgeRect != null)
            {
                m_BadgeRect.DOKill();
                m_BadgeRect.localScale = Vector3.one;
                m_BadgeRect.DOPunchScale(new Vector3(0.32f, 0.32f, 0.32f), 0.20f, 6, 0.5f);
            }
        }

        public void EnsureBadgeUI()
        {
            if (m_Canvas != null && m_Text != null) return;

            Transform existing = transform.Find("CapacityBadgeCanvas");
            GameObject canvasObj;
            if (existing != null)
            {
                canvasObj = existing.gameObject;
            }
            else
            {
                canvasObj = new GameObject("CapacityBadgeCanvas");
                canvasObj.transform.SetParent(transform, false);
                canvasObj.transform.localPosition = m_ManualOffset;
            }

            m_Canvas = canvasObj.GetComponent<Canvas>();
            if (m_Canvas == null) m_Canvas = canvasObj.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.WorldSpace;
            m_Canvas.sortingOrder = 80; // Her şeyin önünde, net ve berrak görünsün

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(140f, 140f);
            canvasRect.localScale = Vector3.one * m_ManualScale;

            // Rozet Kökü
            Transform badgeTrans = canvasObj.transform.Find("BadgeRoot");
            GameObject badgeObj;
            if (badgeTrans != null)
            {
                badgeObj = badgeTrans.gameObject;
            }
            else
            {
                badgeObj = new GameObject("BadgeRoot");
                badgeObj.transform.SetParent(canvasObj.transform, false);
            }

            m_BadgeRect = badgeObj.GetComponent<RectTransform>();
            if (m_BadgeRect == null) m_BadgeRect = badgeObj.AddComponent<RectTransform>();
            m_BadgeRect.anchorMin = Vector2.zero;
            m_BadgeRect.anchorMax = Vector2.one;
            m_BadgeRect.sizeDelta = Vector2.zero;
            m_BadgeRect.anchoredPosition = Vector2.zero;

            // İsteğe bağlı arka plan (varsayılan saydam)
            m_Background = badgeObj.GetComponent<Image>();
            if (m_Background == null) m_Background = badgeObj.AddComponent<Image>();
            m_Background.raycastTarget = false;
            m_Background.color = m_ShowBackgroundBox ? new Color(0.1f, 0.12f, 0.18f, 0.85f) : Color.clear;

            // Metin nesnesi
            Transform textTrans = badgeObj.transform.Find("CountText");
            GameObject textObj;
            if (textTrans != null)
            {
                textObj = textTrans.gameObject;
            }
            else
            {
                textObj = new GameObject("CountText");
                textObj.transform.SetParent(badgeObj.transform, false);
            }

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            if (textRect == null) textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            m_Text = textObj.GetComponent<Text>();
            if (m_Text == null) m_Text = textObj.AddComponent<Text>();
            m_Text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            m_Text.fontSize = 80;
            m_Text.fontStyle = FontStyle.Bold;
            m_Text.alignment = TextAnchor.MiddleCenter;
            m_Text.color = Color.white;
            m_Text.raycastTarget = false;

            // Çift Outline: Referans görseldeki gibi her taraftan belirgin siyah çerçeve
            Outline[] outlines = textObj.GetComponents<Outline>();
            m_Outline1 = outlines.Length > 0 ? outlines[0] : textObj.AddComponent<Outline>();
            m_Outline1.effectColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            m_Outline1.effectDistance = new Vector2(3.5f, 3.5f);

            m_Outline2 = outlines.Length > 1 ? outlines[1] : textObj.AddComponent<Outline>();
            m_Outline2.effectColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            m_Outline2.effectDistance = new Vector2(-3.5f, -3.5f);

            // Alt gölge: 3B derinlik
            m_Shadow = textObj.GetComponent<Shadow>();
            if (m_Shadow == null) m_Shadow = textObj.AddComponent<Shadow>();
            m_Shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            m_Shadow.effectDistance = new Vector2(2f, -5f);
        }
    }
}
