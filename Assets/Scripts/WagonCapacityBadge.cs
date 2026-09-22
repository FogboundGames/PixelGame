using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Vagonun üzerinde kalan parça/küp sayısını gösteren dinamik rozet (Badge).
    /// - Havuz Modunda (Pool): İkinci görseldeki gibi parlak 3B tombul altın sarısı Tile (model gizli).
    /// - Aktif 3B Model Modunda (Slot / Ray Bandı): 3B robot modeli tamamen açık; rozet robotun yüzünü
    ///   kapatmayacak şekilde başının üstünde şık ve kompakt bir 3B mini kapsül (Badge_MiniPill) olarak süzülür.
    /// - Kalın beyaz LilitaOne yazı (#FFFFFF),
    /// - Belirgin derinlik gölgesi (Shadow) ve dış çizgi (Outline),
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
        private Image m_FakeShadow;
        private Image m_InnerPlate;
        private Image m_Background;
        private Text m_Text;
        private Shadow m_Shadow;
        private Outline m_Outline;
        private int m_CurrentCount = -1;
        private TruckCargo m_Cargo;

        private static Material s_AlwaysOnTopMaterial;

        [Header("🤖 3B Model & Mod Ayarları")]
        [Tooltip("Açıkken vagonun 3B gövde modelleri gizlenir ve sadece 3B tombul rozet (Tile) görünür. " +
                 "Kapalıyken 3B robot modeli aktiftir ve rozet başın üstünde şık mini kapsül olarak durur.")]
        [SerializeField] private bool m_HideModel = false;

        [Header("🌑 3B Zemin Temas Gölgesi (Fake Shadow)")]
        [Tooltip("Havuz modunda karonun altına 3B yumuşak temas gölgesi ekler. Obje taşındığında gölge onunla birlikte hareket eder.")]
        [SerializeField] private bool m_EnableFakeShadow = true;

        [Tooltip("Gölge görseli (Assets/UI/PoolSlot_Shadow.png)")]
        [SerializeField] private Sprite m_FakeShadowSprite;

        [Tooltip("Gölge rengi ve opaklığı")]
        [SerializeField] private Color m_FakeShadowColor = new Color(0.015f, 0.025f, 0.06f, 0.55f);

        [Tooltip("Gölgenin karoya göre X ve Y ofseti (piksel)")]
        [SerializeField] private Vector2 m_FakeShadowOffset = new Vector2(0f, -14f);

        [Tooltip("Gölgenin boyut çarpanı (varsayılan: 1.50x genişlik, 1.35x yükseklik)")]
        [SerializeField] private Vector2 m_FakeShadowScale = new Vector2(1.50f, 1.35f);

        [Header("🌟 Havuz Modu (Tile) Görselleri")]
        [Tooltip("Havuz modundaki dış 3B çerçeve görseli (Assets/UI/Count.png)")]
        [SerializeField] private Sprite m_BackgroundSprite;

        [Tooltip("Havuz modundaki iç kuyu (recess) görseli (Assets/UI/Count_InnerPlate.png)")]
        [SerializeField] private Sprite m_InnerPlateSprite;

        [Header("🚀 Aktif 3B Model Modu (Floating Head-Up Badge)")]
        [Tooltip("Aktif model modunda rozetin arka planında parlak 3B kapsül görünsün mü?")]
        [SerializeField] private bool m_ActiveShowMiniPill = true;

        [Tooltip("Aktif model modundaki şık 3B kapsül görseli (Assets/UI/Badge_JuicyPill.png)")]
        [SerializeField] private Sprite m_MiniPillSprite;

        [Tooltip("Aktif model modunda rozetin robot başının ne kadar üstünde duracağı (metre)")]
        [Range(0.02f, 0.40f)]
        [SerializeField] private float m_ActiveHeadElevation = 0.09f;

        [Tooltip("Aktif model modunda rozetin dünya genişliği (metre, varsayılan: 0.65)")]
        [Range(0.30f, 1.20f)]
        [SerializeField] private float m_ActiveWorldWidth = 0.65f;

        [Header("📐 İnce Ayarlar")]
        [Tooltip("Arka plan kutusu görünsün mü?")]
        [SerializeField] private bool m_ShowBackgroundBox = true;

        [Tooltip("Modelin merkezine eklenecek kamera uzayı ince ayar ofseti (X: sağ/sol, Y: yukarı/aşağı, Z: derinlik)")]
        [SerializeField] private Vector3 m_CenterOffset = new Vector3(0f, 0f, 0.05f);

        [Tooltip("Yazı fontu. Boş bırakılırsa varsayılan (LilitaOne) kullanılır.")]
        [SerializeField] private Font m_CustomFont;

        [Tooltip("Vagonun merkezinden manuel yerleşim ofseti (Havuz modu için)")]
        [SerializeField] private Vector3 m_ManualOffset = Vector3.zero;

        [Tooltip("Rozetin havuz modundaki dünya ölçek çarpanı (büyük, parlak ve tombul görünüm)")]
        [SerializeField] private float m_PoolTargetWorldScale = 0.0070f;

        [Tooltip("Rozetin ekranda kameraya göre hafif eğik durması için Z ekseni dönüşü (derece, sadece havuz modu)")]
        [Range(-45f, 45f)]
        [SerializeField] private float m_ManualTiltDegrees = 0f;

        public int CurrentCount => m_CurrentCount;
        public bool IsPoolMode => m_HideModel;

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

        /// <summary>
        /// Sahnede veya DontDestroyOnLoad altında kalmış tüm sahipsiz/bağlantısız rozet nesnelerini temizler.
        /// </summary>
        public static void PurgeOrphanBadges()
        {
            if (Application.isPlaying) return;

            GameObject container = GameObject.Find("[WagonBadgesContainer]");
            if (container != null)
            {
                DestroyImmediate(container);
            }

            Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allCanvases.Length; i++)
            {
                Canvas c = allCanvases[i];
                if (c == null) continue;
                if (c.name == "CapacityBadgeCanvas")
                {
                    if (c.transform.parent == null || c.GetComponentInParent<WagonCapacityBadge>() == null)
                    {
                        DestroyImmediate(c.gameObject);
                    }
                }
            }
        }

        private bool CheckIfInPool()
        {
            if (transform.parent == null) return false;
            return GetComponentInParent<TruckPool>() != null;
        }

        private void Awake()
        {
            EnsureBadgeUI();
            if (transform.parent == null)
            {
                if (m_Canvas != null) m_Canvas.gameObject.SetActive(false);
            }
            else
            {
                bool inPool = CheckIfInPool();
                SetPoolMode(inPool);
            }
        }

        private void OnEnable()
        {
            EnsureBadgeUI();
            if (transform.parent == null)
            {
                if (m_Canvas != null) m_Canvas.gameObject.SetActive(false);
            }
            else
            {
                bool inPool = CheckIfInPool();
                SetPoolMode(inPool);
                ApplyStyle();
                UpdatePlacement();
            }
        }

        private void OnDisable()
        {
            if (m_Canvas != null && m_Canvas.gameObject != null)
            {
                m_Canvas.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (m_Canvas != null && m_Canvas.gameObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(m_Canvas.gameObject);
                }
                else
                {
                    DestroyImmediate(m_Canvas.gameObject);
                }
            }
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
            UnityEditor.EditorApplication.delayCall -= DelayedOnValidate;
            UnityEditor.EditorApplication.delayCall += DelayedOnValidate;
#endif
        }

#if UNITY_EDITOR
        private void DelayedOnValidate()
        {
            if (this == null) return;
            if (m_Canvas == null)
            {
                EnsureBadgeUI();
            }
            else
            {
                ApplyStyle();
                UpdatePlacement();
            }
        }
#endif

        private void LateUpdate()
        {
            UpdatePlacement();
        }

        /// <summary>
        /// Havuz modunda mı (tile görünümü) yoksa aktif oyun/slot/ray modunda mı (3B robot modeli görünümü)?
        /// Kullanıcının "+ ona tıkladıktan sonra modelim aktif olsun slota falan da model şeklinde yerleşsin ama bant kısmında hareket ederken modelim aktif olsun istiyorum"
        /// talebini yönetir.
        /// </summary>
        public void SetPoolMode(bool inPool)
        {
            m_HideModel = inPool;

            if (m_HideModel)
            {
                HideWagonModels();
            }
            else
            {
                ShowWagonModels();
            }

            ApplyStyle();
            UpdatePlacement();
        }

        /// <summary>
        /// Vagon üzerindeki 3B gövde modellerini gizler (Havuzda bekleme modu).
        /// </summary>
        public void HideWagonModels()
        {
            if (!m_HideModel) return;

            Transform modelChild = transform.Find("Model");
            if (modelChild != null && !modelChild.gameObject.activeSelf)
            {
                modelChild.gameObject.SetActive(true);
            }

            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (m_Canvas != null && r.transform.IsChildOf(m_Canvas.transform)) continue;
                if (r.enabled) r.enabled = false;
            }
        }

        /// <summary>
        /// Vagon üzerindeki 3B gövde modellerini aktif eder (Slota veya ray bandına yerleşme modu).
        /// </summary>
        public void ShowWagonModels()
        {
            Transform modelChild = transform.Find("Model");
            if (modelChild != null && !modelChild.gameObject.activeSelf)
            {
                modelChild.gameObject.SetActive(true);
            }

            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (m_Canvas != null && r.transform.IsChildOf(m_Canvas.transform)) continue;
                r.enabled = true;
            }
        }

        /// <summary>
        /// Rozeti konumlandırır ve kameraya tam dik bakmasını sağlar (Billboard).
        /// Ebeveyn nesnenin çocuğu olarak kalır, böylece vagon silindiğinde veya gizlendiğinde rozet asla başıboş kalmaz.
        /// </summary>
        public void UpdatePlacement()
        {
            if (m_Canvas == null) return;

            if (!gameObject.activeInHierarchy || !enabled)
            {
                if (m_Canvas.gameObject.activeSelf) m_Canvas.gameObject.SetActive(false);
                return;
            }

            // Sahipsiz (parent == null) ve rayda hareket etmeyen vagonların rozetini gösterme
            if (transform.parent == null)
            {
                bool isTrackWagon = false;
                if (TruckDispatcher.Instance != null && TruckDispatcher.Instance.MovingWagons != null)
                {
                    for (int i = 0; i < TruckDispatcher.Instance.MovingWagons.Count; i++)
                    {
                        var mw = TruckDispatcher.Instance.MovingWagons[i];
                        if (mw != null && mw.Transform == transform)
                        {
                            isTrackWagon = true;
                            break;
                        }
                    }
                }

                if (!isTrackWagon)
                {
                    if (m_Canvas.gameObject.activeSelf) m_Canvas.gameObject.SetActive(false);
                    return;
                }
            }

            // Canvas'ın her zaman vagonun kendi çocuğu olduğundan emin ol
            if (m_Canvas.transform.parent != transform)
            {
                m_Canvas.transform.SetParent(transform, true);
            }

            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();

            float parentLossy = transform.lossyScale.x;
            if (Mathf.Abs(parentLossy) < 0.0001f) parentLossy = 1f;

            if (m_HideModel)
            {
                // ==========================================
                // 1. HAVUZ MODU (Glossy 3D Tile)
                // ==========================================
                HideWagonModels();

                Vector3 basePos = transform.position;
                if (cam != null)
                {
                    basePos += cam.transform.right * m_ManualOffset.x;
                    basePos += cam.transform.up * m_ManualOffset.y;
                    basePos += cam.transform.forward * m_ManualOffset.z;
                    m_Canvas.transform.position = basePos;
                    m_Canvas.transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, m_ManualTiltDegrees);
                }
                else
                {
                    m_Canvas.transform.position = basePos + m_ManualOffset;
                    m_Canvas.transform.localRotation = Quaternion.Euler(0f, 0f, m_ManualTiltDegrees);
                }

                m_Canvas.transform.localScale = Vector3.one * (m_PoolTargetWorldScale / parentLossy);
            }
            else
            {
                // ==========================================
                // 2. AKTİF 3B MODEL MODU (Robot Başının Üstünde Yüzen Şık 3B Kapsül)
                // ==========================================
                ShowWagonModels();

                if (cam != null)
                {
                    m_Canvas.transform.rotation = cam.transform.rotation;
                }

                Renderer[] rends = GetComponentsInChildren<Renderer>();
                Bounds b = new Bounds();
                bool found = false;

                for (int i = 0; i < rends.Length; i++)
                {
                    Renderer r = rends[i];
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    if (m_Canvas != null && r.transform.IsChildOf(m_Canvas.transform)) continue;
                    if (r.name.StartsWith("CargoPiece") || r.name.StartsWith("Voxel")) continue;

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

                Vector3 headTopPos;
                if (found && b.size.magnitude > 0.01f)
                {
                    Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;
                    Vector3 camFwd = cam != null ? cam.transform.forward : Vector3.forward;

                    // Robot modelinin en üst tepesi + baş üstü yükseltmesi
                    headTopPos = b.center + camUp * (b.extents.y + m_ActiveHeadElevation) - camFwd * 0.04f;
                }
                else
                {
                    headTopPos = transform.position + (cam != null ? cam.transform.up * 0.85f : Vector3.up * 0.85f);
                }

                m_Canvas.transform.position = headTopPos;

                float targetActiveWorldScale = m_ActiveWorldWidth / 130f;
                m_Canvas.transform.localScale = Vector3.one * (targetActiveWorldScale / parentLossy);
            }

            if (!m_Canvas.gameObject.activeSelf)
            {
                m_Canvas.gameObject.SetActive(true);
            }
        }

        public void ApplyStyle()
        {
            Material alwaysOnTopMat = GetAlwaysOnTopMaterial();

            if (m_Canvas == null) EnsureBadgeUI();

            RectTransform canvasRt = m_Canvas != null ? m_Canvas.GetComponent<RectTransform>() : null;

            if (m_HideModel)
            {
                // ==========================================
                // HAVUZ MODU STİLİ: Kare 3B Tombul Tile (140x140)
                // 2. görseldeki gibi %100 parlak, doygun altın sarısı 3B karo
                // ==========================================
                if (canvasRt != null) canvasRt.sizeDelta = new Vector2(140f, 140f);
                if (m_BadgeRect != null) m_BadgeRect.sizeDelta = Vector2.zero;

                if (m_FakeShadow != null)
                {
                    m_FakeShadow.gameObject.SetActive(m_EnableFakeShadow);
                    m_FakeShadow.sprite = ResolveFakeShadowSprite();
                    m_FakeShadow.color = m_FakeShadowColor;
                    if (alwaysOnTopMat != null) m_FakeShadow.material = alwaysOnTopMat;

                    RectTransform shadowRt = m_FakeShadow.rectTransform;
                    shadowRt.anchorMin = new Vector2(0.5f, 0.5f);
                    shadowRt.anchorMax = new Vector2(0.5f, 0.5f);
                    shadowRt.pivot = new Vector2(0.5f, 0.5f);
                    shadowRt.sizeDelta = new Vector2(140f * m_FakeShadowScale.x, 140f * m_FakeShadowScale.y);
                    shadowRt.anchoredPosition = m_FakeShadowOffset;
                    shadowRt.localPosition = new Vector3(m_FakeShadowOffset.x, m_FakeShadowOffset.y, 0f);
                }

                Sprite bgSprite = ResolveBackgroundSprite();
                bool isFullPlate = bgSprite != null && bgSprite.name.IndexOf("FullPlate", System.StringComparison.OrdinalIgnoreCase) >= 0;

                if (m_InnerPlate != null)
                {
                    if (isFullPlate)
                    {
                        m_InnerPlate.gameObject.SetActive(false);
                    }
                    else
                    {
                        m_InnerPlate.gameObject.SetActive(true);
                        m_InnerPlate.sprite = ResolveInnerPlateSprite();
                        m_InnerPlate.type = Image.Type.Simple;
                        m_InnerPlate.preserveAspect = true;
                        m_InnerPlate.color = Color.white;
                        if (alwaysOnTopMat != null) m_InnerPlate.material = alwaysOnTopMat;

                        RectTransform innerRt = m_InnerPlate.rectTransform;
                        innerRt.anchorMin = Vector2.zero;
                        innerRt.anchorMax = Vector2.one;
                        innerRt.sizeDelta = Vector2.zero;
                    }
                }

                if (m_Background != null)
                {
                    m_Background.gameObject.SetActive(m_ShowBackgroundBox);
                    m_Background.sprite = bgSprite;
                    m_Background.type = Image.Type.Simple;
                    m_Background.preserveAspect = true;
                    m_Background.color = m_ShowBackgroundBox ? GetBackgroundColor() : Color.clear;
                    if (alwaysOnTopMat != null) m_Background.material = alwaysOnTopMat;

                    RectTransform bgRt = m_Background.rectTransform;
                    bgRt.anchorMin = Vector2.zero;
                    bgRt.anchorMax = Vector2.one;
                    bgRt.sizeDelta = Vector2.zero;
                }

                if (m_Text != null)
                {
                    RectTransform textRt = m_Text.rectTransform;
                    textRt.anchorMin = new Vector2(0.18f, 0.20f);
                    textRt.anchorMax = new Vector2(0.82f, 0.78f);
                    textRt.sizeDelta = Vector2.zero;

                    m_Text.fontSize = 62;
                    m_Text.color = Color.white;
                    if (alwaysOnTopMat != null) m_Text.material = alwaysOnTopMat;
                }

                if (m_Shadow != null)
                {
                    m_Shadow.effectColor = new Color(0.04f, 0.08f, 0.22f, 0.95f);
                    m_Shadow.effectDistance = new Vector2(0f, -4f);
                }

                if (m_Outline != null)
                {
                    m_Outline.effectColor = new Color(0.05f, 0.10f, 0.26f, 0.85f);
                    m_Outline.effectDistance = new Vector2(0f, -1.5f);
                }
            }
            else
            {
                // ==========================================
                // AKTİF 3B MODEL MODU STİLİ: Parlak 3B Kapsül (130x68)
                // ==========================================
                if (canvasRt != null) canvasRt.sizeDelta = new Vector2(130f, 68f);
                if (m_BadgeRect != null) m_BadgeRect.sizeDelta = Vector2.zero;

                if (m_FakeShadow != null)
                {
                    m_FakeShadow.gameObject.SetActive(false);
                }

                if (m_InnerPlate != null)
                {
                    m_InnerPlate.gameObject.SetActive(false);
                }

                if (m_Background != null)
                {
                    m_Background.gameObject.SetActive(m_ActiveShowMiniPill);
                    m_Background.sprite = ResolveMiniPillSprite();
                    m_Background.type = Image.Type.Simple;
                    m_Background.preserveAspect = true;
                    m_Background.color = Color.white;
                    if (alwaysOnTopMat != null) m_Background.material = alwaysOnTopMat;

                    RectTransform bgRt = m_Background.rectTransform;
                    bgRt.anchorMin = Vector2.zero;
                    bgRt.anchorMax = Vector2.one;
                    bgRt.sizeDelta = Vector2.zero;
                }

                if (m_Text != null)
                {
                    RectTransform textRt = m_Text.rectTransform;
                    textRt.anchorMin = new Vector2(0.05f, 0.05f);
                    textRt.anchorMax = new Vector2(0.95f, 0.95f);
                    textRt.sizeDelta = Vector2.zero;

                    m_Text.fontSize = 54;
                    m_Text.color = Color.white;
                    if (alwaysOnTopMat != null) m_Text.material = alwaysOnTopMat;
                }

                if (m_Shadow != null)
                {
                    m_Shadow.effectColor = new Color(0.01f, 0.02f, 0.08f, 0.98f);
                    m_Shadow.effectDistance = new Vector2(0f, -3.5f);
                }

                if (m_Outline != null)
                {
                    m_Outline.effectColor = new Color(0.02f, 0.04f, 0.12f, 0.90f);
                    m_Outline.effectDistance = new Vector2(0f, -1.8f);
                }
            }
        }

        [SerializeField] private Color m_OverrideColor = Color.clear;

        public void SetOverrideColor(Color color)
        {
            m_OverrideColor = color;
            ApplyStyle();
        }

        /// <summary>
        /// Rozet arka planının rengi: Sarı ve altın tonlarında %100 orijinal görsel parlaklığını (Color.white) korur.
        /// </summary>
        private Color GetBackgroundColor()
        {
            if (m_OverrideColor.a > 0.01f)
            {
                Color.RGBToHSV(m_OverrideColor, out float oh, out float os, out float ov);
                // Altın sarısı tonu ise doğrudan Color.white döndür (görselin orijinal parlaklığını korur)
                if (oh >= 0.08f && oh <= 0.22f) return Color.white;
                return m_OverrideColor;
            }

            if (m_Cargo == null) m_Cargo = GetComponent<TruckCargo>();
            Color baseColor = (m_Cargo != null) ? m_Cargo.CargoColor : new Color32(255, 218, 16, 255);

            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            if (h >= 0.08f && h <= 0.22f)
            {
                return Color.white; // Altın sarısı görsel için tam beyaz çarpan -> %100 orijinal canlılık
            }

            v = Mathf.Max(v, 1.0f);
            s = Mathf.Clamp(s * 1.15f, 0f, 1f);
            return Color.HSVToRGB(h, s, v);
        }

        private Sprite ResolveFakeShadowSprite()
        {
            if (m_FakeShadowSprite != null) return m_FakeShadowSprite;
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("PoolSlot_Shadow t:Sprite");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                m_FakeShadowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            if (m_FakeShadowSprite == null)
            {
                guids = UnityEditor.AssetDatabase.FindAssets("SlotShadow t:Sprite");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    m_FakeShadowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
#endif
            return m_FakeShadowSprite;
        }

        private Sprite ResolveInnerPlateSprite()
        {
            if (m_InnerPlateSprite != null) return m_InnerPlateSprite;
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("Count_InnerPlate t:Sprite");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                m_InnerPlateSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
#endif
            return m_InnerPlateSprite;
        }

        private Sprite ResolveBackgroundSprite()
        {
            if (m_BackgroundSprite != null) return m_BackgroundSprite;
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("Count_FullPlate t:Sprite");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                m_BackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            if (m_BackgroundSprite == null)
            {
                guids = UnityEditor.AssetDatabase.FindAssets("Count t:Sprite");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    m_BackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
            if (m_BackgroundSprite == null)
            {
                guids = UnityEditor.AssetDatabase.FindAssets("Count_Tintable t:Sprite");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    m_BackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
#endif
            return m_BackgroundSprite;
        }

        private Sprite ResolveMiniPillSprite()
        {
            if (m_MiniPillSprite != null) return m_MiniPillSprite;
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("Badge_JuicyPill t:Sprite");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                m_MiniPillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            if (m_MiniPillSprite == null)
            {
                guids = UnityEditor.AssetDatabase.FindAssets("Badge_MiniPill t:Sprite");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    m_MiniPillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
#endif
            return m_MiniPillSprite;
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
            m_Text.color = new Color(0.25f, 0.95f, 0.45f, 1f);

            m_BadgeRect.localScale = Vector3.one;
            m_BadgeRect.DOPunchScale(new Vector3(0.48f, 0.48f, 0.48f), 0.38f, 6, 0.55f);
        }

        public void EnsureBadgeUI()
        {
            if (m_Canvas != null && m_Text != null && m_InnerPlate != null && m_Background != null && m_FakeShadow != null)
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
            m_Canvas.sortingOrder = 100;

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            if (canvasRect == null) canvasRect = canvasObj.AddComponent<RectTransform>();
            canvasRect.sizeDelta = m_HideModel ? new Vector2(140f, 140f) : new Vector2(130f, 68f);

            Material alwaysOnTopMat = GetAlwaysOnTopMaterial();

            // 0. Katman: 3B Zemin Temas Gölgesi (Fake Shadow - Karonun doğrudan alt nesnesi)
            Transform shadowTrans = canvasObj.transform.Find("FakeShadow");
            GameObject shadowObj = shadowTrans != null ? shadowTrans.gameObject : new GameObject("FakeShadow");
            if (shadowTrans == null) shadowObj.transform.SetParent(canvasObj.transform, false);
            shadowObj.transform.SetSiblingIndex(0);

            RectTransform shadowRt = shadowObj.GetComponent<RectTransform>();
            if (shadowRt == null) shadowRt = shadowObj.AddComponent<RectTransform>();
            shadowRt.anchorMin = new Vector2(0.5f, 0.5f);
            shadowRt.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRt.pivot = new Vector2(0.5f, 0.5f);
            shadowRt.sizeDelta = new Vector2(140f * m_FakeShadowScale.x, 140f * m_FakeShadowScale.y);
            shadowRt.anchoredPosition = m_FakeShadowOffset;
            shadowRt.localPosition = new Vector3(m_FakeShadowOffset.x, m_FakeShadowOffset.y, 0f);

            m_FakeShadow = shadowObj.GetComponent<Image>();
            if (m_FakeShadow == null) m_FakeShadow = shadowObj.AddComponent<Image>();
            m_FakeShadow.raycastTarget = false;
            m_FakeShadow.sprite = ResolveFakeShadowSprite();
            m_FakeShadow.type = Image.Type.Simple;
            m_FakeShadow.preserveAspect = true;
            m_FakeShadow.color = m_FakeShadowColor;
            if (alwaysOnTopMat != null) m_FakeShadow.material = alwaysOnTopMat;

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
            m_BadgeRect.anchoredPosition3D = Vector3.zero;
            m_BadgeRect.localPosition = Vector3.zero;
            m_BadgeRect.localRotation = Quaternion.identity;

            // 1. Katman: İç Koyu Lacivert Plaka (InnerPlate)
            Transform innerTrans = badgeObj.transform.Find("InnerPlate");
            GameObject innerObj = innerTrans != null ? innerTrans.gameObject : new GameObject("InnerPlate");
            if (innerTrans == null) innerObj.transform.SetParent(badgeObj.transform, false);
            innerObj.transform.SetSiblingIndex(0);

            RectTransform innerRt = innerObj.GetComponent<RectTransform>();
            if (innerRt == null) innerRt = innerObj.AddComponent<RectTransform>();
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.sizeDelta = Vector2.zero;
            innerRt.anchoredPosition = Vector2.zero;

            m_InnerPlate = innerObj.GetComponent<Image>();
            if (m_InnerPlate == null) m_InnerPlate = innerObj.AddComponent<Image>();
            m_InnerPlate.raycastTarget = false;
            m_InnerPlate.sprite = ResolveInnerPlateSprite();
            m_InnerPlate.type = Image.Type.Simple;
            m_InnerPlate.preserveAspect = true;
            m_InnerPlate.color = Color.white;
            if (alwaysOnTopMat != null) m_InnerPlate.material = alwaysOnTopMat;

            // 2. Katman: Dış 3B Çerçeve / Mini Pill (Background)
            Transform frameTrans = badgeObj.transform.Find("Frame");
            GameObject frameObj = frameTrans != null ? frameTrans.gameObject : new GameObject("Frame");
            if (frameTrans == null) frameObj.transform.SetParent(badgeObj.transform, false);
            frameObj.transform.SetSiblingIndex(1);

            RectTransform frameRt = frameObj.GetComponent<RectTransform>();
            if (frameRt == null) frameRt = frameObj.AddComponent<RectTransform>();
            frameRt.anchorMin = Vector2.zero;
            frameRt.anchorMax = Vector2.one;
            frameRt.sizeDelta = Vector2.zero;
            frameRt.anchoredPosition = Vector2.zero;

            m_Background = frameObj.GetComponent<Image>();
            if (m_Background == null) m_Background = frameObj.AddComponent<Image>();
            m_Background.raycastTarget = false;
            m_Background.sprite = m_HideModel ? ResolveBackgroundSprite() : ResolveMiniPillSprite();
            m_Background.type = Image.Type.Simple;
            m_Background.preserveAspect = true;
            m_Background.color = m_HideModel ? (m_ShowBackgroundBox ? GetBackgroundColor() : Color.clear) : Color.white;
            if (alwaysOnTopMat != null) m_Background.material = alwaysOnTopMat;

            // 3. Katman: Metin Nesnesi (CountText)
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
            textObj.transform.SetAsLastSibling();

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            if (textRect == null) textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = m_HideModel ? new Vector2(0.18f, 0.20f) : new Vector2(0.05f, 0.05f);
            textRect.anchorMax = m_HideModel ? new Vector2(0.82f, 0.78f) : new Vector2(0.95f, 0.95f);
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            bool textWasNew = textObj.GetComponent<Text>() == null;
            m_Text = textObj.GetComponent<Text>();
            if (m_Text == null) m_Text = textObj.AddComponent<Text>();

            Font fontToUse = m_CustomFont;
            if (fontToUse == null && !textWasNew && m_Text.font != null) fontToUse = m_Text.font;
            if (fontToUse == null)
            {
#if UNITY_EDITOR
                fontToUse = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/LilitaOne-Regular.ttf");
#endif
            }
            if (fontToUse == null)
            {
                fontToUse = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            m_Text.font = fontToUse;
            m_Text.fontSize = m_HideModel ? 58 : 54;
            m_Text.fontStyle = FontStyle.Normal;
            m_Text.alignment = TextAnchor.MiddleCenter;
            m_Text.color = Color.white;
            m_Text.raycastTarget = false;
            m_Text.horizontalOverflow = HorizontalWrapMode.Overflow;
            m_Text.verticalOverflow = VerticalWrapMode.Overflow;
            m_Text.resizeTextForBestFit = false;

            if (alwaysOnTopMat != null)
            {
                m_Text.material = alwaysOnTopMat;
            }

            // Gölge
            m_Shadow = textObj.GetComponent<Shadow>();
            if (m_Shadow == null) m_Shadow = textObj.AddComponent<Shadow>();
            m_Shadow.effectColor = new Color(0.02f, 0.05f, 0.16f, 0.95f);
            m_Shadow.effectDistance = m_HideModel ? new Vector2(0f, -4.5f) : new Vector2(0f, -3.5f);

            // Dış çizgi
            m_Outline = textObj.GetComponent<Outline>();
            if (m_Outline == null) m_Outline = textObj.AddComponent<Outline>();
            m_Outline.effectColor = new Color(0.03f, 0.07f, 0.20f, 0.85f);
            m_Outline.effectDistance = m_HideModel ? new Vector2(0f, -1.5f) : new Vector2(0f, -1.8f);
        }
    }
}


