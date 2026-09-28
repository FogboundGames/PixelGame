#pragma warning disable 0414
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
        /// <summary>
        /// A/B test anahtarı: true ise kargo gelince eski DOTween Punch Scale efekti çalışır,
        /// false ise yeni su-salınımı tabanlı "sallanma" efekti. Level Designer'daki butonla
        /// Play Mode'da bile anında değiştirilebilir (tüm gemiler için ortak).
        /// Düz bir statik alan OLARAK TUTULMUYOR: Play Mode'a her girişte Unity script'leri
        /// "domain reload" ile sıfırlar ve düz statikler kod içindeki başlangıç değerine
        /// (false) geri dönerdi — EditorPrefs bu geçişlerden etkilenmeyen kalıcı bir depo.
        /// </summary>
        private const string LegacyCargoPunchPrefKey = "PixelGame_UseLegacyCargoPunch";
        private static bool s_UseLegacyCargoPunch;
        private static bool s_LegacyCargoPunchLoaded;

        public static bool UseLegacyCargoPunch
        {
            get
            {
#if UNITY_EDITOR
                if (!s_LegacyCargoPunchLoaded)
                {
                    s_UseLegacyCargoPunch = EditorPrefs.GetBool(LegacyCargoPunchPrefKey, false);
                    s_LegacyCargoPunchLoaded = true;
                }
#endif
                return s_UseLegacyCargoPunch;
            }
            set
            {
                s_UseLegacyCargoPunch = value;
#if UNITY_EDITOR
                EditorPrefs.SetBool(LegacyCargoPunchPrefKey, value);
                s_LegacyCargoPunchLoaded = true;
#endif
            }
        }

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

        [Header("📦 Kargo Alınca Heyecanlı Sallanma (Cargo Wobble)")]
        [Tooltip("Kargo her geldiğinde 1'e sıçrar, sonra zamanla yumuşakça 0'a söner. Var olan su " +
                 "salınımının genliğini geçici olarak büyütür — DOTween tween'i olmadığı için (sadece " +
                 "sabit bir taban değere göre her karede yeniden hesaplanır) asla birikip 'kayma' yapmaz.")]
        [SerializeField] private float m_CargoWobbleDecaySpeed = 0.35f;
        [SerializeField] private float m_CargoWobbleRollMultiplier = 18.0f;
        [SerializeField] private float m_CargoWobblePitchMultiplier = 12.0f;
        private float m_CargoWobbleBoost = 0f;

        [Header("🏷️ Kapasite Rozeti (World Space UI)")]
        [SerializeField] private GameObject m_BadgeCanvasObj;
        [SerializeField] private TextMeshProUGUI m_BadgeText;
        [SerializeField] private Text m_BadgeUIText;
        [SerializeField] private Image m_BadgeImage;

        // Sabit temel ölçek (Her zaman uniform 0.26f - 2. görseldeki gibi doygun ve büyük)
        public const float DefaultShipScale = 0.26f;

        [Header("🌑 Yerel Gölge Yakalayıcı (Per-Ship Shadow Catcher)")]
        [Tooltip("Sahnedeki tek/büyük Ground_ShadowCatcher düzlemi, gemiyle aynı Z derinliğinde " +
                 "olmadığı için gemi gölgesini geminin kendisinden UZAK bir noktada gösteriyordu " +
                 "(ışık açılı geldiği için Z farkı = X/Y kayması). Çözüm: her gemiye, gemiyle TAM " +
                 "AYNI derinlikte duran kendi küçük yakalayıcısını vermek — böylece gölge her zaman " +
                 "geminin tam altında kalır.")]
        [SerializeField] private bool m_EnableLocalShadowCatcher = false;
        [SerializeField] private Vector2 m_ShadowCatcherWorldSize = new Vector2(0.55f, 0.95f);
        [Tooltip("Yakalayıcının gemiden, IŞIĞIN KENDİ YÖNÜ boyunca ne kadar öteye kayacağı. " +
                 "Sıfır olursa yakalayıcı geminin gövdesiyle aynı derinlikte kalır ve Unity'nin " +
                 "gölge yanlılığı (shadow bias) bunu 'kendi kendine gölge' sayıp gölgeyi hiç " +
                 "göstermez. Board'daki çalışan küp gölgesiyle aynı büyüklükte bir ayrım kullanıyoruz.")]
        [SerializeField] private float m_ShadowCatcherOffsetDistance = 0.42f;
        private GameObject m_ShadowCatcherObj;
        private static Material s_ShipShadowCatcherMaterial;
        private static Mesh s_ShadowCatcherQuadMesh;
        private static Light s_CachedMainLight;

        private static Light GetMainDirectionalLight()
        {
            if (s_CachedMainLight == null)
            {
                s_CachedMainLight = UnityEngine.Object.FindFirstObjectByType<Light>();
            }
            return s_CachedMainLight;
        }

        // Dahili referanslar
        private MeshRenderer[] m_Renderers;
        private MaterialPropertyBlock m_PropBlock;
        private float m_BobRandomOffset;
        private Vector3 m_BaseLocalPosition;
        private Quaternion m_BaseLocalRotation;
        private Vector3 m_BaseScale = Vector3.one * DefaultShipScale;
        private static Material s_AlwaysOnTopMaterial;

        public Color ShipColor => m_ShipColor;
        public int Capacity => m_Capacity;
        public int CurrentCargo => m_CurrentCargo;
        public int RemainingCapacity => Mathf.Max(0, m_Capacity - m_CurrentCargo);
        public bool IsDocked => m_IsDocked;
        public bool IsFull => m_CurrentCargo >= m_Capacity;
        public bool IsDeparting => m_IsDeparting;

        // ---------------- Rezerve (yolda olan) kargo ----------------
        // Küp panodan koparıldığı anda gemiye yazılmıyor; uçuş ~1.5 sn sürüyor ve
        // AddCargo ancak varışta çağrılıyor. Bu sayaç olmadan kapasite kontrolü
        // sadece VARMIŞ kargoyu görüyordu: küpler 0.12 sn arayla fırlatıldığı için
        // 10 kapasiteli bir gemi için panodan ~22 küp çıkıyordu.
        private int m_PendingCargo = 0;

        /// <summary>Yola çıkmış ama henüz gemiye varmamış kargo sayısı.</summary>
        public int PendingCargo => m_PendingCargo;
        public bool HasPendingCargo => m_PendingCargo > 0;

        /// <summary>Yoldakiler DAHİL gemi hâlâ kargo alabilir mi?</summary>
        public bool CanAcceptMore => !m_IsDeparting && (m_CurrentCargo + m_PendingCargo) < m_Capacity;

        /// <summary>
        /// Uçuş BAŞLAMADAN önce çağrılır: yer varsa bir kargo yeri ayırıp true döner.
        /// false dönerse küp panodan koparılmamalıdır.
        /// </summary>
        public bool TryReserveCargo()
        {
            if (!CanAcceptMore) return false;
            m_PendingCargo++;
            return true;
        }

        /// <summary>Uçuş yarıda kesilirse ayrılan yeri geri verir.</summary>
        public void ReleaseCargoReservation()
        {
            m_PendingCargo = Mathf.Max(0, m_PendingCargo - 1);
        }
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

        private static Material GetShipShadowCatcherMaterial()
        {
            if (s_ShipShadowCatcherMaterial == null)
            {
                Shader shader = Shader.Find("Custom/URP_ShadowCatcher");
                if (shader != null)
                {
                    s_ShipShadowCatcherMaterial = new Material(shader);
                    s_ShipShadowCatcherMaterial.name = "Ship_Local_ShadowCatcher_Mat";
                    // Gemi_ShadowCatcher_Mat ile aynı renk/opaklık (bkz. Assets/Materials/Gemi_ShadowCatcher_Mat.mat)
                    s_ShipShadowCatcherMaterial.SetColor("_ShadowColor", new Color(0.04f, 0.08f, 0.16f, 0.45f));
                }
            }
            return s_ShipShadowCatcherMaterial;
        }

        private static Mesh GetShadowCatcherQuadMesh()
        {
            if (s_ShadowCatcherQuadMesh == null)
            {
                s_ShadowCatcherQuadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            }
            return s_ShadowCatcherQuadMesh;
        }

        /// <summary>
        /// Kullanıcı isteği: Gemi gölge yakalayıcıları (shadow catchers) kaldırıldı.
        /// </summary>
        private void CreateOrFindShadowCatcher()
        {
            Transform existing = transform.Find("Ship_Shadow_Catcher");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject, true);
            }
            if (m_ShadowCatcherObj != null)
            {
                if (Application.isPlaying) Destroy(m_ShadowCatcherObj);
                else DestroyImmediate(m_ShadowCatcherObj, true);
                m_ShadowCatcherObj = null;
            }
        }

        private void UpdateShadowCatcherPlacement()
        {
            // Kullanıcı isteği: Gemi gölge yakalayıcıları kaldırıldı.
        }

        private void Awake()
        {
            m_PropBlock = new MaterialPropertyBlock();
            m_BobRandomOffset = UnityEngine.Random.Range(0f, 100f);
            // Dünya uzayı (lossy) ölçeği yakala — böylece gemi daha sonra farklı bir ebeveyne
            // (ör. slot) geçtiğinde, o ebeveynin kendi (simetrik olmayabilen) ölçeğinden bağımsız
            // olarak hep aynı GÖRSEL boyutta kalır.
            if (transform.lossyScale != Vector3.zero) m_BaseScale = transform.lossyScale;

            EnsureVisualComponents();
            CreateOrFindBadge();
            CreateOrFindShadowCatcher();
        }

        private void OnEnable()
        {
            EnsureVisualComponents();
            CreateOrFindBadge();
            CreateOrFindShadowCatcher();
            UpdateBadgeText();
        }

        private void OnValidate()
        {
            if (m_BadgeUIText != null)
            {
                UpdateBadgeText();
            }
        }

        private void Start()
        {
            m_BaseLocalPosition = transform.localPosition;
            m_BaseLocalRotation = transform.localRotation;
            if (transform.lossyScale != Vector3.zero) m_BaseScale = transform.lossyScale;

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

        /// <summary>
        /// Geminin sabit bir GÖRSEL (dünya uzayı) boyutta kalmasını sağlayacak local scale'i
        /// hesaplar. m_BaseScale artık dünya ölçeği olarak tutuluyor; ama Transform.localScale'e
        /// doğrudan atanamaz çünkü o an bulunduğu ebeveynin (kuyruk noktası, slot, vb.) kendi ölçeği
        /// eklenip binmiş olur — üstelik slotlar simetrik ölçekli bile değil (X/Y/Z farklı). Bu yüzden
        /// önce mevcut ebeveynin ölçeğini bölerek doğru local scale'e çeviriyoruz.
        /// </summary>
        private Vector3 GetLocalScaleForBaseWorldScale()
        {
            if (transform.parent == null) return m_BaseScale;

            Vector3 parentLossy = transform.parent.lossyScale;
            return new Vector3(
                m_BaseScale.x / Mathf.Max(0.0001f, parentLossy.x),
                m_BaseScale.y / Mathf.Max(0.0001f, parentLossy.y),
                m_BaseScale.z / Mathf.Max(0.0001f, parentLossy.z)
            );
        }

        private void LateUpdate()
        {
            UpdateBadgePlacement();
            UpdateShadowCatcherPlacement();
        }

        private void UpdateBadgePlacement()
        {
            if (m_BadgeCanvasObj == null)
            {
                Transform foundTr = transform.Find("Ship_Capacity_Canvas");
                if (foundTr != null) m_BadgeCanvasObj = foundTr.gameObject;
            }
            if (m_BadgeCanvasObj == null) return;

            // Kullanıcı isteği: "textteki sayı dolduğunda text yok olsun"
            // Kapasite dolduğunda veya gemi kalkışta iken rozet ve metin KESİNLİKLE gizlenir.
            if (m_IsDeparting || IsFull || RemainingCapacity <= 0)
            {
                if (m_BadgeCanvasObj.activeSelf) m_BadgeCanvasObj.SetActive(false);
                return;
            }

            Transform canvasTr = m_BadgeCanvasObj.transform;
            if (canvasTr.parent != transform)
            {
                canvasTr.SetParent(transform, true);
            }

            // 1. Kullanıcı isteği: "gemilerdeki textler ortalanacak"
            // Gemi gövdesinin/kabininin tam ortası: X = 0f, Y = 2.22f, Z = -0.20f
            Vector3 roofLocalPos = new Vector3(0f, 2.22f, -0.20f);
            Vector3 targetWorldPos = transform.TransformPoint(roofLocalPos);
            if ((canvasTr.position - targetWorldPos).sqrMagnitude > 0.00001f)
            {
                canvasTr.position = targetWorldPos;
            }

            // 2. Kullanıcı isteği: "slotlara yerleştiğinde de textler sabit konumda olacak değişim göstermesinler (ilk görselde yer alıyor)"
            // Slotların X/Y/Z rotasyonu veya su dalgalanması ne olursa olsun,
            // yazı HER ZAMAN kameraya dik, düzgün ve net bakar; asla yana yatmaz, bozulmaz.
            Camera cam = Camera.main;
            Quaternion targetWorldRot = cam != null ? cam.transform.rotation : Quaternion.identity;
            if (canvasTr.rotation != targetWorldRot)
            {
                canvasTr.rotation = targetWorldRot;
            }

            // 3. Kullanıcı isteği: "2.görseldeki gibi görünsün textler daha kaliteli hale getir"
            // 2. Görsel referansındaki gibi aracın genişliğinin ~%72-75'ini kaplayan BÜYÜK HERO TEXT.
            // Ebeveyn slot veya kuyruk ölçeği ne olursa olsun sabit bir dünya boyutunda tut:
            Vector3 boatLossy = transform.lossyScale;
            float avgLossy = (Mathf.Abs(boatLossy.x) + Mathf.Abs(boatLossy.y) + Mathf.Abs(boatLossy.z)) / 3f;
            if (avgLossy < 0.0001f) avgLossy = 0.35f;

            // Hedef dünya metin genişliği: ~0.70 dünya birimi (geminin 0.94 dünya genişliğinin %74.5'i)
            float targetLocalScaleFactor = 0.0088f / avgLossy;
            Vector3 targetLocalScale = Vector3.one * targetLocalScaleFactor;
            if ((canvasTr.localScale - targetLocalScale).sqrMagnitude > 0.000001f)
            {
                canvasTr.localScale = targetLocalScale;
            }

            if (m_BadgeText == null)
            {
                m_BadgeText = m_BadgeCanvasObj.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (m_BadgeText != null)
            {
                RectTransform rt = m_BadgeText.rectTransform;
                if (rt.anchoredPosition != Vector2.zero) rt.anchoredPosition = Vector2.zero;
                if (Mathf.Abs(rt.localPosition.z - (-11.5f)) > 0.001f) rt.localPosition = new Vector3(0f, 0f, -11.5f);
                if (rt.sizeDelta != new Vector2(180f, 120f)) rt.sizeDelta = new Vector2(180f, 120f);
                if (rt.localScale != Vector3.one) rt.localScale = Vector3.one;

                if (m_BadgeText.fontSize != 72f) m_BadgeText.fontSize = 72f;
                if (m_BadgeText.color != Color.white) m_BadgeText.color = Color.white;
                if (m_BadgeText.outlineWidth != 0.22f) m_BadgeText.outlineWidth = 0.22f;
            }

            if (!m_BadgeCanvasObj.activeSelf && !m_IsDeparting && !IsFull && RemainingCapacity > 0)
            {
                m_BadgeCanvasObj.SetActive(true);
            }
        }

        private void ApplyWaterBobbing()
        {
            // Kargo geldikçe tazelenen "heyecan" boost'u zamanla 0'a söner (her karede sabit bir
            // taban değerden yeniden hesaplandığı için DOTween tween'leri gibi asla birikip kayma yapmaz).
            if (m_CargoWobbleBoost > 0f)
            {
                m_CargoWobbleBoost = Mathf.Max(0f, m_CargoWobbleBoost - Time.deltaTime * m_CargoWobbleDecaySpeed);
            }

            float time = Time.time * m_BobFrequency + m_BobRandomOffset;
            float dy = Mathf.Sin(time) * m_BobHeight;
            float rollAmp = m_RollAngle * (1f + m_CargoWobbleBoost * m_CargoWobbleRollMultiplier);
            float pitchAmp = m_PitchAngle * (1f + m_CargoWobbleBoost * m_CargoWobblePitchMultiplier);
            float dRoll = Mathf.Sin(time * 0.85f) * rollAmp;
            float dPitch = Mathf.Cos(time * 0.75f) * pitchAmp;

            transform.localPosition = m_BaseLocalPosition + new Vector3(0f, dy, 0f);
            transform.localRotation = m_BaseLocalRotation * Quaternion.Euler(dPitch, 0f, dRoll);
        }

        /// <summary>
        /// Kargo alındığında çağrılır: geminin idle su salınımını kısa süreliğine daha canlı/enerjik
        /// (daha büyük genlikte) yapar, sonra doğal olarak sönümlenip normal sakin salınıma döner.
        /// </summary>
        private void TriggerCargoWobble()
        {
            m_CargoWobbleBoost = 1f;
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
            m_PendingCargo = 0;
            m_ColorName = string.IsNullOrEmpty(colorName) ? ColorUtility.ToHtmlStringRGB(color) : colorName;

            ClearCargoBarrels();
            EnsureVisualComponents();
            CreateOrFindBadge();
            CreateOrFindShadowCatcher();
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

        private static readonly Dictionary<Color32, Texture2D> s_CachedBoatTextures = new Dictionary<Color32, Texture2D>();
        private static readonly Dictionary<Color32, Material> s_CachedBoatMaterials = new Dictionary<Color32, Material>();

        public static bool IsYellowSpectrum(Color c)
        {
            // Amber/turuncu-sarı (F9A825) ve açık sarılar dahil tüm sarı tonlarını saf canlı sarıya eşle
            return c.r > 0.70f && c.g > 0.45f && c.b < 0.35f;
        }

        public static readonly Color PureSunnyYellow = new Color(1.0f, 0.88f, 0.05f, 1f);

        public static Texture2D GetOrCreateBoatTexture(Color color)
        {
            if (IsYellowSpectrum(color))
            {
                color = PureSunnyYellow;
            }

            Color32 key = (Color32)color;
            key.a = 255;
            if (s_CachedBoatTextures.TryGetValue(key, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            int w = 64;
            int h = 64;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.name = $"BoatColormap_{ColorUtility.ToHtmlStringRGB(color)}";

            Color32[] pixels = new Color32[w * h];
            // Kullanıcı isteği: Geminin üst kısmı, çatısı ve her zerresi küpün rengiyle BİREBİR AYNI
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = key;
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            s_CachedBoatTextures[key] = tex;
            return tex;
        }

        public static Material GetOrCreateBoatMaterial(Color color)
        {
            bool isYellow = IsYellowSpectrum(color);
            if (isYellow)
            {
                color = PureSunnyYellow;
            }

            Color32 key = (Color32)color;
            key.a = 255;
            if (s_CachedBoatMaterials.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Material baseMat = null;
#if UNITY_EDITOR
            baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ship_Watercraft_Mat.mat");
#endif
            Material mat = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Toony Colors Pro 2/PixelGame/Cartoon") ?? Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = $"Ship_BoatMat_{ColorUtility.ToHtmlStringRGB(color)}";

            Texture2D tex = GetOrCreateBoatTexture(color);
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);

            if (isYellow)
            {
                mat.SetColor("_HColor", new Color(1.0f, 0.98f, 0.65f, 1f));
                mat.SetColor("_SColor", new Color(0.92f, 0.78f, 0.10f, 1f)); // Sıcak altın sarısı gölge, ASLA turuncu/kahve değil!
            }
            else
            {
                mat.SetColor("_HColor", Color.Lerp(Color.white, color, 0.25f));
                mat.SetColor("_SColor", color * 0.70f);
            }
            mat.SetColor("_RimColor", new Color(1f, 1f, 1f, 0.35f));
            mat.SetColor("_PlasticHighlightColor", Color.white);

            s_CachedBoatMaterials[key] = mat;
            return mat;
        }

        /// <summary>
        /// Gemiyi kenney boat-house-a stiline uygun olarak boyar:
        /// Geminin her zerresi ve çatısı toplayacağı küpün rengini alır.
        /// </summary>
        public void ApplyColorToShip(Color color)
        {
            if (IsYellowSpectrum(color))
            {
                color = PureSunnyYellow;
            }
            m_ShipColor = color;
            if (m_Renderers == null || m_Renderers.Length == 0)
            {
                m_Renderers = GetComponentsInChildren<MeshRenderer>(true);
            }

            Material boatMat = GetOrCreateBoatMaterial(color);

            foreach (var mr in m_Renderers)
            {
                if (mr == null) continue;
                // Dinamik varil renderers'ını ana gövde boyamasından ayrı tut
                if (m_CargoDeckRoot != null && mr.transform.IsChildOf(m_CargoDeckRoot)) continue;

                if (Application.isPlaying)
                {
                    mr.material = boatMat;
                }
                else
                {
                    mr.sharedMaterial = boatMat;
                }
            }

            // Mevcut oluşturulmuş variller varsa renklerini de güncelle
            UpdateAllBarrelsColor();
        }

        /// <summary>
        /// Gemiye kargo (küp parçacığı) ekler ve güvertede geminin renginde bir 3D varil oluşturur.
        /// </summary>
        public void AddCargo(int amount = 1)
        {
            // Rezervasyon her hâlükârda tüketilir — gemi kalkıyor olsa bile sayaç sızmasın,
            // yoksa gemi bir daha hiç "dolabilir" duruma gelemez.
            m_PendingCargo = Mathf.Max(0, m_PendingCargo - amount);

            if (m_IsDeparting) return;

            m_CurrentCargo = Mathf.Min(m_Capacity, m_CurrentCargo + amount);
            UpdateBadgeText();

            // Referans videodaki gibi küp geldikçe canlı, enerjik yaylanma / squash tepkisi
            PlayCargoReceiveJuice();

            if (IsFull && !m_IsDeparting)
            {
                OnCargoFilled?.Invoke(this);
                DepartAndFreeSlot();
            }
        }

        public void PlayCargoReceiveJuice()
        {
            transform.DOKill(true);
            transform.DOPunchScale(new Vector3(0.065f, -0.065f, 0.065f) * m_BaseScale.x, 0.12f, 2, 0.45f)
                .OnComplete(() => transform.localScale = GetLocalScaleForBaseWorldScale());
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
            // 1. Text ve rozet hemen kaybolur
            if (m_BadgeCanvasObj != null) m_BadgeCanvasObj.SetActive(false);
            if (m_BadgeUIText != null) m_BadgeUIText.gameObject.SetActive(false);
            if (m_BadgeImage != null) m_BadgeImage.gameObject.SetActive(false);

            // Slottan ayrılırken slotu hemen boşa çıkar ki oyuncu hemen yeni gemi yerleştirebilsin (videodaki gibi)
            if (m_CurrentSlot != null)
            {
                m_CurrentSlot.ReleaseShip();
                m_CurrentSlot = null;
            }

            // Slottan dünya koordinatlarına çık
            transform.SetParent(null, true);
            transform.DOKill(true);

            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            // Slottan ayrılış: Geminin pruva (burun) yönünde slottan ileriye doğru zarifçe süzülür
            Vector3 undockOffset = transform.forward * 0.85f;
            const float departExtraLift = 0.15f;
            Vector3 undockPos = startPos + undockOffset + new Vector3(0f, departExtraLift, 0f);

            // Su yüzeyi düzleminde sola bakan hedef rotasyon (Lokal Y ekseninde -90° dönüş):
            Quaternion leftTargetRot = Quaternion.Euler(-68f, 0f, 0f) * Quaternion.Euler(0f, -90f, 0f);

            // Ekranın sol kenarından tamamen çıkacak hedef koordinat (X = -8.8f):
            Vector3 departTarget = new Vector3(-8.8f, undockPos.y, undockPos.z);

            // 4 Noktalı Pürüzsüz Bezier Su Rotası (Slottan öne çıkıp sola doğru tatlı bir yay çizer)
            Vector3 p0 = startPos;
            Vector3 p1 = undockPos;
            Vector3 p2 = new Vector3(Mathf.Lerp(p1.x, departTarget.x, 0.38f), undockPos.y, undockPos.z);
            Vector3 p3 = departTarget;

            float duration = 1.15f;
            float elapsed = 0f;
            float lastSmokeTime = 0f;

            // Kalkışta motor çalıştırma puf dumanı ve küçük su dalgası
            SpawnSmokePuff(startPos - transform.forward * 0.25f, 0.16f, 0.40f, 0.42f);
            SpawnWaterRipple(startPos - transform.forward * 0.25f, 0.24f, 0.85f, 0.45f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float moveT = (t < 0.25f) ? (2.0f * t * t) : (t - 0.125f) / 0.875f;
                moveT = Mathf.Clamp01(moveT);

                // Konum güncellemesi
                Vector3 currentPos = EvaluateCubicBezier(p0, p1, p2, p3, moveT);
                transform.position = currentPos;

                // Dönüş yönü: Slottan çıkarken dümen kırma (t: 0.10 -> 0.48 arasında sola dönüş)
                float turnT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.08f) / 0.40f));
                float bankRoll = Mathf.Sin(turnT * Mathf.PI) * 7.0f;
                transform.rotation = Quaternion.Slerp(startRot, leftTargetRot, turnT) * Quaternion.Euler(0f, 0f, bankRoll);

                // Referans videodaki gibi arkasında beyaz puf duman bulutları ve köpük izi
                if (Time.time - lastSmokeTime > 0.040f)
                {
                    lastSmokeTime = Time.time;
                    Vector3 exhaustPos = currentPos - transform.forward * 0.35f + new Vector3(0f, -0.05f, 0.02f);
                    SpawnSmokePuff(exhaustPos, 0.12f, 0.36f, 0.38f);
                    SpawnWaterRipple(exhaustPos, 0.18f, 0.65f, 0.38f);
                }

                yield return null;
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
            Vector3 p1 = startWorldPos + new Vector3(0f, 0.65f, -0.08f);
            Vector3 p2 = targetWorld + new Vector3(0f, -0.65f, 0.08f);
            Vector3 p3 = targetWorld;

            float duration = 0.48f; // Referans videodaki gibi seri, tatmin edici ve atik geçiş süresi
            float elapsed = 0f;
            float lastSmokeTime = 0f;
            float lateralDelta = targetWorld.x - startWorldPos.x;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 currentWorldPos = EvaluateCubicBezier(p0, p1, p2, p3, easeT);
                transform.position = currentWorldPos;

                // Dünya boyutunu yelken boyunca %100 sabit tut
                if (transform.parent == null)
                {
                    transform.localScale = startWorldScale;
                }
                else
                {
                    transform.localScale = GetLocalScaleForBaseWorldScale();
                }

                // Dönüş yönüne göre hafif yatma (Banking Roll)
                float bankRoll = Mathf.Sin(easeT * Mathf.PI) * (-Mathf.Sign(lateralDelta) * Mathf.Clamp(Mathf.Abs(lateralDelta) * 5.0f, 2f, 6.5f));
                float alignWeight = Mathf.Clamp01((easeT - 0.65f) / 0.35f);
                float currentRoll = Mathf.Lerp(bankRoll, 0f, alignWeight);

                transform.rotation = Quaternion.Slerp(startRot, targetSlotWorldRot, easeT) * Quaternion.Euler(0f, 0f, currentRoll);

                // Slota ilerlerken motor dumanı ve su izi
                if (Time.time - lastSmokeTime > 0.045f)
                {
                    lastSmokeTime = Time.time;
                    Vector3 exhaustPos = currentWorldPos - transform.forward * 0.32f + new Vector3(0f, -0.05f, 0.02f);
                    SpawnSmokePuff(exhaustPos, 0.10f, 0.28f, 0.32f);
                    SpawnWaterRipple(exhaustPos, 0.15f, 0.52f, 0.35f);
                }

                yield return null;
            }

            transform.SetParent(targetSlot.transform, true);
            transform.localPosition = targetLocalPos;
            transform.localRotation = Quaternion.identity;
            transform.localScale = GetLocalScaleForBaseWorldScale();

            m_BaseLocalPosition = targetLocalPos;
            m_BaseLocalRotation = Quaternion.identity;

            // Slota yanaşma puf dalgası ve hafif yaylanma
            SpawnWaterRipple(transform.position, 0.28f, 0.95f, 0.45f);
            transform.DOPunchScale(new Vector3(0.06f, -0.06f, 0.06f) * m_BaseScale.x, 0.20f, 2, 0.45f)
                .OnComplete(() => transform.localScale = GetLocalScaleForBaseWorldScale());

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
            // Arka plandaki su dokusunun canlı dalgalanmasını da tetikle
            HypercasualWaterController.TriggerWaterRipple(worldPos, 0.55f, Mathf.Clamp(maxScale * 0.22f, 0.08f, 0.28f));

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
                Shader rippleShader = Shader.Find("PixelGame/WaterRippleRing");
                if (rippleShader == null) rippleShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (rippleShader == null) rippleShader = Shader.Find("Unlit/Transparent");

                Material mat = new Material(rippleShader);
                Color foamColor = new Color(0.85f, 0.96f, 1f, 0.85f);
                mat.SetColor("_BaseColor", foamColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", foamColor);
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                ripple.transform.DOScale(Vector3.one * maxScale, duration).SetEase(Ease.OutQuad);
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

        private static Material s_SmokePuffSharedMaterial;

        /// <summary>
        /// Referans videodaki gibi geminin arkasında beliren yumuşak beyaz puf duman bulutu (Cartoon Smoke Puff) oluşturur.
        /// </summary>
        public static void SpawnSmokePuff(Vector3 worldPos, float startScale = 0.10f, float maxScale = 0.32f, float duration = 0.38f)
        {
            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Quad);
            puff.name = "SmokePuff_FX";
            puff.transform.position = worldPos;

            Camera cam = Camera.main;
            if (cam != null) puff.transform.rotation = cam.transform.rotation;
            else puff.transform.rotation = Quaternion.Euler(-68f, 0f, 0f);

            puff.transform.localScale = Vector3.one * startScale;

            Collider col = puff.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            MeshRenderer mr = puff.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                if (s_SmokePuffSharedMaterial == null)
                {
                    Shader smokeShader = Shader.Find("PixelGame/CartoonSmokePuff");
                    if (smokeShader == null) smokeShader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (smokeShader == null) smokeShader = Shader.Find("Unlit/Transparent");
                    s_SmokePuffSharedMaterial = new Material(smokeShader);
                    s_SmokePuffSharedMaterial.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.90f));
                }

                Material instMat = new Material(s_SmokePuffSharedMaterial);
                mr.sharedMaterial = instMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                Vector3 drift = Vector3.up * 0.12f + new Vector3(UnityEngine.Random.Range(-0.04f, 0.04f), UnityEngine.Random.Range(0f, 0.04f), UnityEngine.Random.Range(-0.04f, 0.04f));
                puff.transform.DOMove(worldPos + drift, duration).SetEase(Ease.OutQuad);
                puff.transform.DOScale(Vector3.one * maxScale, duration).SetEase(Ease.OutQuad);
                instMat.DOFade(0f, "_BaseColor", duration).SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        if (Application.isPlaying)
                        {
                            Destroy(instMat);
                            Destroy(puff);
                        }
                        else
                        {
                            DestroyImmediate(instMat);
                            DestroyImmediate(puff);
                        }
                    });
            }
            else
            {
                if (Application.isPlaying) Destroy(puff, duration);
                else DestroyImmediate(puff);
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
        /// Geminin kabin çatısı üzerine (2. fotodaki gibi) çerçevesiz, kalın siyah konturlu NET BEYAZ rakam metni oluşturur.
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
            }

            // Standart Canvas transform değerleri (Gemi gövde çatısı tam merkezi: X=0, Y=2.22, Z=-0.20)
            canvasObj.transform.localPosition = new Vector3(0f, 2.22f, -0.20f);
            canvasObj.transform.localRotation = Quaternion.identity;
            canvasObj.transform.localScale = Vector3.one * 0.025f;

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.sizeDelta = new Vector2(180f, 120f);
                canvasRect.pivot = new Vector2(0.5f, 0.5f);
                canvasRect.anchoredPosition = Vector2.zero;
            }

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 3000;

            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (cam != null) canvas.worldCamera = cam;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;
            scaler.referencePixelsPerUnit = 100;

            m_BadgeCanvasObj = canvasObj;

            // Dairesel çerçeve halkasını kaldır (2. fotodaki gibi çerçevesiz düz yazı)
            Transform bgTr = canvasObj.transform.Find("Badge_CircleRing");
            if (bgTr != null)
            {
                if (Application.isPlaying) Destroy(bgTr.gameObject);
                else DestroyImmediate(bgTr.gameObject, true);
            }
            m_BadgeImage = null;

            // Metin Nesnesi (Badge_Text)
            Transform textTr = canvasObj.transform.Find("Badge_Text");
            GameObject textObj;
            if (textTr != null)
            {
                textObj = textTr.gameObject;
            }
            else
            {
                textObj = new GameObject("Badge_Text", typeof(RectTransform), typeof(CanvasRenderer));
                textObj.transform.SetParent(canvasObj.transform, false);
            }

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.localPosition = new Vector3(0f, 0f, -11.5f);
            textRect.localRotation = Quaternion.identity;
            textRect.localScale = Vector3.one;
            textRect.sizeDelta = new Vector2(180f, 120f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.pivot = new Vector2(0.5f, 0.5f);

            // Eski pikselli UI.Outline ve Shadow bileşenlerini temizle
            var oldOutlines = textObj.GetComponents<Outline>();
            for (int i = 0; i < oldOutlines.Length; i++)
            {
                if (Application.isPlaying) Destroy(oldOutlines[i]);
                else DestroyImmediate(oldOutlines[i]);
            }
            var oldShadows = textObj.GetComponents<Shadow>();
            for (int i = 0; i < oldShadows.Length; i++)
            {
                if (Application.isPlaying) Destroy(oldShadows[i]);
                else DestroyImmediate(oldShadows[i]);
            }

            // 3. Kullanıcı isteği: "2.görseldeki gibi görünsün textler daha kaliteli hale getir"
            // TextMeshPro SDF ile vektör kalitesinde, pürüzsüz yuvarlak siyah konturlu NET BEYAZ rakam:
            TMP_FontAsset tmpFont = null;
#if UNITY_EDITOR
            tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
#endif
            if (tmpFont == null) tmpFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LilitaOne-Regular SDF");
            if (tmpFont == null) tmpFont = Resources.Load<TMP_FontAsset>("Fonts/LilitaOne-Regular SDF");
            if (tmpFont == null) tmpFont = Resources.Load<TMP_FontAsset>("LilitaOne-Regular SDF");

            TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = textObj.AddComponent<TextMeshProUGUI>();

            if (tmpFont != null) tmp.font = tmpFont;
            tmp.fontSize = 72f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white; // 2. görseldeki gibi NET BEYAZ
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            // 2. Görseldeki gibi pürüzsüz, yuvarlak ve kaliteli siyah kontur (SDF fragment shader ile hesaplanır)
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = new Color32(18, 18, 22, 255);
            if (tmp.fontMaterial != null)
            {
                tmp.fontMaterial.EnableKeyword("OUTLINE_ON");
                tmp.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
                tmp.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(18, 18, 22, 255));
            }

            m_BadgeText = tmp;

            // Eski standart Text varsa devre dışı bırak
            Text oldUiText = textObj.GetComponent<Text>();
            if (oldUiText != null)
            {
                oldUiText.enabled = false;
            }
            m_BadgeUIText = null;

            UpdateBadgePlacement();
            UpdateBadgeText();
        }

        private void UpdateBadgeText()
        {
            int remaining = RemainingCapacity;

            // Kullanıcı isteği: "textteki sayı dolduğunda text yok olsun"
            // Kapasite dolduğunda (kalan <= 0 veya IsFull) text HEMEN yok olur!
            if (remaining <= 0 || IsFull || m_IsDeparting)
            {
                if (m_BadgeCanvasObj != null && m_BadgeCanvasObj.activeSelf)
                    m_BadgeCanvasObj.SetActive(false);
                if (m_BadgeText != null && m_BadgeText.gameObject.activeSelf)
                    m_BadgeText.gameObject.SetActive(false);
                if (m_BadgeUIText != null && m_BadgeUIText.gameObject.activeSelf)
                    m_BadgeUIText.gameObject.SetActive(false);
                return;
            }

            string countStr = remaining.ToString();

            if (m_BadgeCanvasObj != null && !m_BadgeCanvasObj.activeSelf)
                m_BadgeCanvasObj.SetActive(true);

            if (m_BadgeText != null)
            {
                if (!m_BadgeText.gameObject.activeSelf) m_BadgeText.gameObject.SetActive(true);
                m_BadgeText.text = countStr;
            }
            if (m_BadgeUIText != null && m_BadgeUIText.enabled)
            {
                if (!m_BadgeUIText.gameObject.activeSelf) m_BadgeUIText.gameObject.SetActive(true);
                m_BadgeUIText.text = countStr;
            }
        }
    }
}

