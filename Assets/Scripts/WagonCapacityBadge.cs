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
    [ExecuteAlways]
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
        private TruckCargo m_Cargo;
        private float m_CurrentFillRatio = 0f;

        private static Material s_AlwaysOnTopMaterial;

        [Header("📐 Görsel Stil & Boyut")]
        [Tooltip("Arka plan kutusu görünsün mü? (Varsayılan: false, doğrudan vagonun üzerinde)")]
        [SerializeField] private bool m_ShowBackgroundBox = false;

        [Tooltip("Otomatik olarak vagonun 3D sınırlarını (bounds) bulup modelin tam ortasına yerleştir")]
        [SerializeField] private bool m_AutoCenterOnMesh = true;

        [Tooltip("Metnin vagon modeline göre büyüklük oranı (varsayılan: 0.85)")]
        [Range(0.4f, 1.6f)]
        [SerializeField] private float m_SizeRatio = 0.85f;

        [Tooltip("Kamera bakış açısına göre vagon boşken ortalanması için yukarı kaldırma oranı (varsayılan: 0.18)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float m_VerticalLiftRatio = 0.18f;

        [Header("📦 Doluluk Dinamik Yükselmesi (Pile Float)")]
        [Tooltip("Kasa doldukça metnin parçaların üstünde kalması için dinamik yükselme oranı (varsayılan: 0.28)")]
        [Range(0f, 0.6f)]
        [SerializeField] private float m_FillRiseRatio = 0.28f;

        [Tooltip("Modelin merkezine eklenecek kamera uzayı ince ayar ofseti (X: sağ/sol, Y: yukarı/aşağı, Z: derinlik)")]
        [SerializeField] private Vector3 m_CenterOffset = Vector3.zero;

        [Tooltip("Yazı boyutu (Canvas birimi, varsayılan: 105)")]
        [Range(40, 160)]
        [SerializeField] private int m_FontSize = 105;

        [Header("🔧 Manuel Mod (AutoCenter kapalıysa)")]
        [Tooltip("Vagonun merkezinden manuel yerleşim ofseti")]
        [SerializeField] private Vector3 m_ManualOffset = new Vector3(0f, 0.15f, 0f);

        [Tooltip("Rozetin manuel ölçeği")]
        [SerializeField] private float m_ManualScale = 0.012f;

        public int CurrentCount => m_CurrentCount;

        /// <summary>
        /// 3B parçaların ve vagon gövdesinin metni asla kapatamaması için ZTest Always UI materyali.
        /// </summary>
        public static Material GetAlwaysOnTopMaterial()
        {
            if (s_AlwaysOnTopMaterial == null)
            {
                Shader shader = Shader.Find("UI/Default");
                Material baseMat = shader != null ? new Material(shader) : new Material(Canvas.GetDefaultCanvasMaterial());
                baseMat.name = "UI_AlwaysOnTop_Mat";
                baseMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                s_AlwaysOnTopMaterial = baseMat;
            }
            return s_AlwaysOnTopMaterial;
        }

        private void Awake()
        {
            EnsureBadgeUI();
        }

        private void OnEnable()
        {
            EnsureBadgeUI();
            ApplyStyle();
            UpdatePlacement();
        }

        private void OnValidate()
        {
            ApplyStyle();
        }

        private void LateUpdate()
        {
            UpdatePlacement();
        }

        /// <summary>
        /// Rozeti vagon modelinin tam merkezine yerleştirir ve kameraya tam dik bakmasını sağlar (Billboard).
        /// Kasa doldukça metin yığının üzerinde dinamik olarak yükselir ve parçaların altında kalmaz.
        /// </summary>
        public void UpdatePlacement()
        {
            if (m_Canvas == null) return;

            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
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

            bool isBottle = name.IndexOf("Bottle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (transform.parent != null && transform.parent.name.IndexOf("Bottle", System.StringComparison.OrdinalIgnoreCase) >= 0);

            if (isBottle)
            {
                m_Canvas.transform.localPosition = new Vector3(-0.04f, 0.45f, 0.45f);
                m_Canvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                m_Canvas.transform.localScale = Vector3.one * 0.0022f;
                return;
            }

            // Vagonun render sınırlarını hesapla (düşen parçacıklar ve canvas hariç)
            Renderer[] rends = GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds();
            bool found = false;
            Renderer bodyRenderer = null;

            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r.transform.IsChildOf(m_Canvas.transform)) continue;
                if (r.name.StartsWith("CargoPiece") || r.name.StartsWith("Voxel")) continue;

                if (r.name.StartsWith("MineCart_Body") || r.name.StartsWith("Truck_Cargo") || r.name.IndexOf("Bottle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    bodyRenderer = r;
                }

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
                float wagonSize = Mathf.Max(b.size.x, b.size.z);

                // Modelin tam merkezi (gövde rendereri varsa gövdenin merkezi, yoksa tüm vagon sınırlarının merkezi)
                Vector3 center = bodyRenderer != null ? bodyRenderer.bounds.center : b.center;

                // Doluluk oranını TruckCargo'dan alıp pürüzsüzce takip et
                if (m_Cargo == null) m_Cargo = GetComponent<TruckCargo>();
                float targetFillRatio = m_Cargo != null ? m_Cargo.FillRatio : 0f;
                m_CurrentFillRatio = Mathf.MoveTowards(m_CurrentFillRatio, targetFillRatio, Time.deltaTime * 3.5f);

                // 1. Temel yukarı kaldırma (boşken haznenin tam ortası)
                float baseLift = Mathf.Max(b.size.y, wagonSize * 0.65f) * m_VerticalLiftRatio;

                // 2. Doluluk yükselmesi (parçalar doldukça metin yığının üzerinde yükselir)
                float fillLift = Mathf.Max(b.size.y, wagonSize * 0.65f) * m_FillRiseRatio * m_CurrentFillRatio;

                Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;
                center += camUp * (baseLift + fillLift);

                // 3. Kamera bakış açısına göre kullanıcı ince ayar ofseti
                if (cam != null)
                {
                    center += cam.transform.rotation * m_CenterOffset;
                }
                else
                {
                    center += m_CenterOffset;
                }

                m_Canvas.transform.position = center;

                // Vagon genişliğine göre ölçekle
                float targetWorldSize = wagonSize * m_SizeRatio;
                float targetScale = targetWorldSize / 140f;

                m_Canvas.transform.localScale = Vector3.one * Mathf.Max(0.001f, targetScale);
            }
            else
            {
                m_Canvas.transform.localPosition = m_ManualOffset;
                m_Canvas.transform.localScale = Vector3.one * m_ManualScale;
            }
        }

        public void ApplyStyle()
        {
            if (m_Text != null)
            {
                m_Text.fontSize = m_FontSize;
                m_Text.resizeTextForBestFit = true;
                m_Text.resizeTextMinSize = 30;
                m_Text.resizeTextMaxSize = m_FontSize;
                m_Text.horizontalOverflow = HorizontalWrapMode.Overflow;
                m_Text.verticalOverflow = VerticalWrapMode.Overflow;

                // Asla parçaların arkasında kalmaması için Always-On-Top materyali ata
                Material alwaysOnTop = GetAlwaysOnTopMaterial();
                if (alwaysOnTop != null && m_Text.material != alwaysOnTop)
                {
                    m_Text.material = alwaysOnTop;
                }
            }

            if (m_Background != null)
            {
                m_Background.color = m_ShowBackgroundBox ? new Color(0.1f, 0.12f, 0.18f, 0.85f) : Color.clear;
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

            if (m_CurrentCount == 0 && prev > 0)
            {
                PlayCompletionAnimation();
                return;
            }

            m_Text.text = m_CurrentCount.ToString();
            m_Text.color = Color.white;

            if (m_Cargo == null) m_Cargo = GetComponent<TruckCargo>();
            if (m_Cargo != null && m_Cargo.RemainingCapacity == m_Cargo.Capacity)
            {
                // Vagon sıfırlandıysa doluluk yükselmesini de anında tabana al
                m_CurrentFillRatio = 0f;
            }

            if (punchAnimation && prev != -1 && prev != m_CurrentCount && m_BadgeRect != null)
            {
                m_BadgeRect.DOKill();
                m_BadgeRect.localScale = Vector3.one;
                m_BadgeRect.DOPunchScale(new Vector3(0.32f, 0.32f, 0.32f), 0.20f, 6, 0.5f);
            }
        }

        /// <summary>
        /// Vagon kapasitesi tamamen dolduğunda (parçalar tamamlandığında)
        /// oynatılacak minik, tatlı kutlama/tamamlama animasyonu.
        /// </summary>
        public void PlayCompletionAnimation()
        {
            EnsureBadgeUI();
            if (m_Text == null || m_BadgeRect == null) return;

            m_BadgeRect.DOKill();
            m_Text.DOKill();

            m_Text.text = "✓";
            m_Text.color = new Color(0.25f, 0.95f, 0.45f, 1f); // Parlak tatlı yeşil

            m_BadgeRect.localScale = Vector3.one;
            m_BadgeRect.DOPunchScale(new Vector3(0.55f, 0.55f, 0.55f), 0.45f, 6, 0.55f);
        }

        public void EnsureBadgeUI()
        {
            if (m_Canvas != null && m_Text != null)
            {
                ApplyStyle();
                return;
            }

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
            m_Canvas.sortingOrder = 100; // Her şeyin önünde, net ve berrak görünsün

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
            m_Text.fontSize = m_FontSize;
            m_Text.fontStyle = FontStyle.Bold;
            m_Text.alignment = TextAnchor.MiddleCenter;
            m_Text.color = Color.white;
            m_Text.raycastTarget = false;
            m_Text.horizontalOverflow = HorizontalWrapMode.Overflow;
            m_Text.verticalOverflow = VerticalWrapMode.Overflow;
            m_Text.resizeTextForBestFit = true;
            m_Text.resizeTextMinSize = 30;
            m_Text.resizeTextMaxSize = m_FontSize;

            // 3B nesneler ve yığılan parçalar metni asla örtmesin
            Material alwaysOnTop = GetAlwaysOnTopMaterial();
            if (alwaysOnTop != null)
            {
                m_Text.material = alwaysOnTop;
            }

            // 8 Yönlü Kesintisiz Kalın Outline:
            // Beyaz, sarı, siyah ya da herhangi bir renkteki parçanın üstünde %100 net kontrast sağlar
            Outline[] outlines = textObj.GetComponents<Outline>();
            m_Outline1 = outlines.Length > 0 ? outlines[0] : textObj.AddComponent<Outline>();
            m_Outline1.effectColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            m_Outline1.effectDistance = new Vector2(4.5f, 4.5f);

            m_Outline2 = outlines.Length > 1 ? outlines[1] : textObj.AddComponent<Outline>();
            m_Outline2.effectColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            m_Outline2.effectDistance = new Vector2(-4.5f, 4.5f);

            Outline outline3 = outlines.Length > 2 ? outlines[2] : textObj.AddComponent<Outline>();
            outline3.effectColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            outline3.effectDistance = new Vector2(4.5f, 0f);

            Outline outline4 = outlines.Length > 3 ? outlines[3] : textObj.AddComponent<Outline>();
            outline4.effectColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            outline4.effectDistance = new Vector2(0f, 4.5f);

            // Alt gölge: 3B derinlik
            m_Shadow = textObj.GetComponent<Shadow>();
            if (m_Shadow == null) m_Shadow = textObj.AddComponent<Shadow>();
            m_Shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            m_Shadow.effectDistance = new Vector2(2.5f, -5.5f);
        }
    }
}
