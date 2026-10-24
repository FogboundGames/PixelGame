#pragma warning disable 0414
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Gemi Sahnesi'nin (Gemi.unity) merkezi oyun yöneticisi (Gameplay Coordinator).
    /// Su slotlarını (ShipSlot), bekleme kuyruğunu (ShipQueuePool) ve küp patlama / kargo akışını koordine eder.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Ship Dispatcher")]
    public class ShipDispatcher : MonoBehaviour
    {
        private static ShipDispatcher s_Instance;
        public static ShipDispatcher Instance => s_Instance;

        [Header("⚓ Slotlar & Kuyruk")]
        [SerializeField] private List<ShipSlot> m_Slots = new List<ShipSlot>();

        /// <summary>Yanaşma slotları (salt okunur). Gemi rengi dağıtımında kullanılır.</summary>
        public IReadOnlyList<ShipSlot> Slots => m_Slots;

        /// <summary>Sahildeki açık (yanaşılabilir) aktif slot sayısı.</summary>
        public int OpenSlotCount => m_Slots != null ? m_Slots.Count : 0;

        /// <summary>Rıhtım iskelesindeki toplam bölme sayısı (arka planda çizili 5 bölme).</summary>
        public int TotalBaysCount
        {
            get
            {
                var marina = Object.FindFirstObjectByType<MarinaSlotLayout>();
                return marina != null ? marina.TotalBaysCount : MarinaSlotLayout.BayCountFor(OpenSlotCount);
            }
        }

        /// <summary>Sahildeki kilitli / kapalı yanaşma bölmesi sayısı.</summary>
        public int ClosedSlotCount => Mathf.Max(0, TotalBaysCount - OpenSlotCount);

        [SerializeField] private ShipQueuePool m_QueuePool;

        [Header("🎨 Piksel Sanatı Bağlantısı")]
        [SerializeField] private PixelArtGenerator m_Generator;

        [Header("🚀 Kargo Treni & Sahilden Gemiye Zıplama Ayarları")]
        [Tooltip("Küp treninin kayma hızı (dünya birimi / sn). Referans videoda ~13 küp/sn akıyor (iki kol toplamı).")]
        [Range(0.6f, 4f)]
        [SerializeField] private float m_RopeSpeed = 2.0f;
        [Tooltip("Rope Speed bu küp boyunda (ızgara adımı, dünya birimi) geçerlidir. Gerçek hız küp boyuyla ölçeklenir: " +
                 "16x16 gibi iri küplü bölümlerde küpler dünya biriminde daha hızlı gider, böylece her bölümde saniyede aynı " +
                 "sayıda küp boyu yol alırlar. 0.167 = 32x32 bölümdeki küp boyu.")]
        [SerializeField] private float m_RopeSpeedReferencePitch = 0.167f;

        /// <summary>
        /// Küp treninin gerçek hızı (dünya birimi / sn). Sabit dünya hızı iri küplü bölümlerde ağır çekim gibi
        /// görünüyordu (küp kendi boyuna göre yarı hızda ilerliyor, adımları seyrekleşiyordu).
        /// </summary>
        private float EffectiveRopeSpeed()
        {
            float speed = Mathf.Max(0.05f, m_RopeSpeed);
            float pitch = m_GridFrame.Pitch;
            if (m_RopeSpeedReferencePitch > 0.001f && pitch > 0.001f) speed *= pitch / m_RopeSpeedReferencePitch;
            return speed;
        }
        [Tooltip("Sahil kenarından gemiye zıplama süresi (sn).")]
        [SerializeField] private float m_HopDuration = 0.38f;
        [Tooltip("Sahil sonundan gemiye doğru zıplama yayının yüksekliği (parabolik zıplama tepe noktası).")]
        [SerializeField] private float m_HopArcHeight = 0.48f;
        [Tooltip("Kıyı noktasının dünya Y'si (ship == null durumunda yedek).")]
        [SerializeField] private float m_ShoreY = -1.97f;
        [Tooltip("Kıyı noktasının dünya Z'si (ship == null durumunda yedek).")]
        [SerializeField] private float m_ShoreZ = 0.07f;
        [Tooltip("Kumun suya değdiği kıyı çizgisi (dünya X,Y). Küpler bu çizgide, geminin en yakın noktasına kadar yürür ve oradan gemiye zıplar. Boşsa geminin hemen önü kullanılır.")]
        [SerializeField] private List<Vector2> m_Shoreline = new List<Vector2>();
        [Tooltip("Kıyı noktasının kum tarafına (geminin tersine) ne kadar içeride olacağı (dünya birimi).")]
        [SerializeField] private float m_ShorelineInset = 0.05f;

        // Düz ahşap iskele kıyı şeridi (World Y = -1.97f; kamera 20° eğime alınınca iskelenin ekrandaki yeri sabit kalsın diye -3.25'ten taşındı)
        private static readonly Vector2[] s_DefaultShoreline = new Vector2[]
        {
            new Vector2(-6.0f, -1.97f),
            new Vector2( 6.0f, -1.97f)
        };
        [Tooltip("Boşsa panodaki küpün kendisi yürür. Bir prefab atanırsa (ör. Mixamo koşucusu MainCube_Running) küp yerinde gizlenir, yerine bu prefab küpün renginde yürür.")]
        [SerializeField] private GameObject m_CargoStandInPrefab;

        [Header("🔄 Tersine Gemi Koşucuları (Reversed Ship Runner Flow)")]
        [Tooltip("Açık olduğunda küpler panodan gemiye değil; koşan karakterler gemiden sahile atlayıp OtCerceve etrafından piksel art yüklerini (konteynerleri) almaya gider ve gemiye geri döner.")]
        [SerializeField] private bool m_UseReversedShipRunners = true;
        [Tooltip("Koşucu olarak Resources/Sailor_Runner denizci prefabını kullan (kapalıysa eski küp koşucu).")]
        [SerializeField] private bool m_UseSailorRunner = true;
        private const string SailorRunnerResourcePath = "Sailor_Runner";
        private GameObject m_SailorRunnerPrefab;
        // Sahnedeki canlı koşucular: bölüm yeniden başlarken (retry) görevleri yarıda kalanlar da temizlenir
        private readonly List<GameObject> m_LiveRunners = new List<GameObject>();

        private void DestroyLiveRunners()
        {
            for (int i = m_LiveRunners.Count - 1; i >= 0; i--)
            {
                if (m_LiveRunners[i] != null)
                {
                    m_LiveRunners[i].SetActive(false);
                    Destroy(m_LiveRunners[i]);
                }
            }
            m_LiveRunners.Clear();
            m_RunnerAgents.Clear();
        }
        [Tooltip("Denizcinin önünde taşıdığı küpün boyu (model birimi; 1 = panodaki küp).")]
        [SerializeField, Range(0.2f, 1f)] private float m_SailorCargoSize = 0.6f;
        [Tooltip("Denizcinin panodaki küpe göre boy çarpanı. 1/Sailor Cargo Size (≈1.67) → elindeki küp panodaki küple aynı boyda.")]
        [SerializeField, Min(0.3f)] private float m_SailorSizeVsCube = 1.67f;
        [Tooltip("Denizcilerin gemiden art arda çıkış aralığı (sn).")]
        [SerializeField, Min(0.05f)] private float m_SailorLaunchInterval = 0.35f;
        [Header("📦 Yığın Taşıma (Denizci)")]
        [Tooltip("Bir denizcinin tek seferde tek tek gezip topladığı küp sayısı. 1 = her küpe ayrı denizci (eski davranış). " +
                 "Son denizci kalan kadarını taşır (ör. 12 → 5 + 5 + 2). Konteynerdeki sayı denizci dönünce bir kerede düşer.")]
        [SerializeField, Range(1, 12)] private int m_SailorStackSize = 5;
        [Tooltip("Görevi dolmamış denizcinin sıradaki küp için bekleme süresi (0 = beklemeden direkt elindekiyle döner, boşa çıkan kontenjana yeni gemici gönderilir).")]
        [SerializeField, Min(0f)] private float m_StackMaxWait = 0f;
        [Tooltip("Denizcinin yığına ekleyeceği sıradaki küp, bir önceki küpüne en fazla bu kadar hücre (yatay+dikey) uzakta " +
                 "olabilir. Daha uzaktaki küpler ona verilmez (panonun öbür ucuna gitmesin); onları sonraki denizciler alır.")]
        [SerializeField, Min(1)] private int m_StackMaxJumpCells = 4;
        [Tooltip("Konteynerin son denizcileri yakında küp bulamazsa uzaktaki küpe gitmeden önceki bekleme süresi (0 = beklemez).")]
        [SerializeField, Min(0f)] private float m_StackFarJumpDelay = 0f;
        [Tooltip("Başka denizcinin yürüdüğü küpe bu denizci en az bu kadar hücre daha yakınsa küp ona geçer (öbürü yeni küp arar).")]
        [SerializeField, Min(1)] private int m_StackStealMarginCells = 2;
        public int SailorStackSize { get => m_SailorStackSize; set => m_SailorStackSize = Mathf.Clamp(value, 1, 12); }
        [Tooltip("Denizcinin şapka alt şeridi için sabit ayırt edici renk (şapkanın üst kubbesi, ponponu ve tişörtü toplanacak küpün renginde olurken alt şerit bu renkte [varsayılan beyaz] kalarak denizci şapkası formunu belirginleştirir).")]
        [SerializeField] private Color m_SailorHatFixedColor = Color.white;
        public Color SailorHatFixedColor { get => m_SailorHatFixedColor; set => m_SailorHatFixedColor = value; }

        [Header("Denizci Renk Ayarları (Hypercasual)")]
        [Tooltip("Denizcinin sevimli hypercasual şeftali ten rengi.")]
        [SerializeField] private Color m_SailorSkinColor = new Color(1.0f, 0.82f, 0.70f, 1.0f);
        public Color SailorSkinColor { get => m_SailorSkinColor; set => m_SailorSkinColor = value; }

        [Tooltip("Denizcinin ten rengi için sıcak mercan gölgesi (gölgede çamurlaşma veya morarma olmaması için).")]
        [SerializeField] private Color m_SailorSkinShadowColor = new Color(0.94f, 0.68f, 0.56f, 1.0f);
        public Color SailorSkinShadowColor { get => m_SailorSkinShadowColor; set => m_SailorSkinShadowColor = value; }

        [Tooltip("Denizcinin tişörtü ve şapka alt şeridi için temiz, parlak beyaz.")]
        [SerializeField] private Color m_SailorWhiteColor = new Color(0.98f, 0.98f, 0.98f, 1.0f);
        public Color SailorWhiteColor { get => m_SailorWhiteColor; set => m_SailorWhiteColor = value; }

        [Tooltip("Denizcinin şortu için kontrast lacivert renk.")]
        [SerializeField] private Color m_SailorNavyColor = new Color(0.10f, 0.18f, 0.42f, 1.0f);
        public Color SailorNavyColor { get => m_SailorNavyColor; set => m_SailorNavyColor = value; }

        [Tooltip("Denizcinin ayakkabıları ve gözleri için koyu antrasit renk.")]
        [SerializeField] private Color m_SailorShoeColor = new Color(0.12f, 0.12f, 0.15f, 1.0f);
        public Color SailorShoeColor { get => m_SailorShoeColor; set => m_SailorShoeColor = value; }

        [Tooltip("Denizcinin yanaklarındaki sevimli pembe allık.")]
        [SerializeField] private Color m_SailorBlushColor = new Color(1.0f, 0.48f, 0.60f, 1.0f);
        public Color SailorBlushColor { get => m_SailorBlushColor; set => m_SailorBlushColor = value; }
        [Tooltip("Koşucuların yürüdüğü çerçevenin, bölüm başındaki resmin dış küp merkezlerinden uzaklığı (küp cinsinden).")]
        [SerializeField, Min(0.5f)] private float m_RunnerFrameMarginCubes = 1.2f;
        [Tooltip("Açıksa denizci çerçevesi sahnedeki OtCerceve ile genişletilir (eski davranış). Kapalıyken her levelde resmin kendi sınırları + pay kullanılır.")]
        [SerializeField] private bool m_RunnerFrameUseOtCerceve = false;
        [Tooltip("Görünen kumsalın üst kenarı (ekranın altından oran, 0..1). Çerçeve bunun üstüne, limana taşmaz.")]
        [SerializeField, Range(0.5f, 1f)] private float m_RunnerSandTopViewport = 0.78f;
        [Tooltip("Çerçevenin ekranın sol/sağ kenarından en az uzaklığı (ekran genişliğine oran).")]
        [SerializeField, Range(0f, 0.2f)] private float m_RunnerSandSideViewport = 0.04f;
        [Tooltip("Denizcinin küpü alırken küp merkezinden ne kadar önde durduğu (küp cinsinden).")]
        [SerializeField, Range(0f, 1.5f)] private float m_PickupStandoffCubes = 0.75f;
        [Tooltip("Resmin içindeki (çerçeve dışındaki) her adımın çerçevede yürümeye göre maliyet çarpanı. 1: en kısa yol, " +
                 "küplere yanaşır. Büyüdükçe denizci çerçeveyi dolaşıp içeri küpe en yakın yerden girer.")]
        [SerializeField, Range(1f, 5f)] private float m_RunnerInnerStepCost = 2.5f;
        [Tooltip("Açık: denizci küpü alırken, alma animasyonunun ikinci yarısında dönüş yoluna doğru yerinde döner.\n" +
                 "Kapalı: alırken küpe dönük kalır, yönünü yürümeye başlarken çevirir.")]
        [SerializeField] private bool m_SailorTurnWhilePickingUp = false;

        [Tooltip("Gemiden sahile atlayıp yükleri taşıyan animasyonlu koşucu prefabı (boş bırakılırsa MainCube_Running_Tabletop kullanılır).")]
        [SerializeField] private GameObject m_RunnerPrefab;

        [Tooltip("Eğik kameralı kumsal sahnesi için koşucu (bacaklar zemine basar). Doluysa Runner Prefab yerine bu kullanılır.")]
        [SerializeField] private GameObject m_TabletopRunnerPrefab;
        private const string TabletopRunnerPrefabPath = "Assets/Prefabs/MainCube_Running_Tabletop.prefab";

        [Tooltip("Koşucunun panodaki bir küpe göre boyutu (1 = küple aynı).")]
        [SerializeField, Min(0.1f)] private float m_RunnerSizeVsCube = 1.45f;

        [Tooltip("Sırttaki yük ile koşucunun üstü arasındaki boşluk (küp boyunun oranı).")]
        [SerializeField] private float m_CarriedCargoGap = 0.02f;
        [Tooltip("Sırttaki yükün kameradan uzağa (koşucunun arkasına) kayması (küp boyunun oranı).")]
        [SerializeField] private float m_CarriedCargoBackShift = 0.15f;

        [Tooltip("Sahnede etrafından koşulacak çerçeve (OtCerceve). Boş bırakılırsa sahnede otomatik bulunur.")]
        [SerializeField] private RectTransform m_OtCerceve;

        [Tooltip("Koşucuların gemiden kalkış aralığı (saniye).")]
        [SerializeField] private float m_RunnerStaggerDelay = 0.12f;

        [Tooltip("Slottaki gemiden koşucuların teker teker çıkma aralığı (saniye).")]
        [SerializeField, Min(0.01f)] private float m_RunnerLaunchInterval = 0.1f;
        [Tooltip("Koşucuların yürüme hızı (dünya birimi/sn). Eskiden 3.2'ye sabitti.")]
        [SerializeField, Min(0.5f)] private float m_RunnerSpeed = 2.2f;
        private int m_RunnerSerial;

        [Header("🏃 Denizci Hareket Hissi")]
        [Tooltip("Denizcinin duruştan tam hıza çıkma süresi (sn). 0 = anında tam hız.")]
        [SerializeField, Range(0f, 1f)] private float m_RunnerAccelTime = 0.2f;
        [Tooltip("Küpe varmadan kaç küp mesafeden itibaren yavaşlamaya başlasın.")]
        [SerializeField, Range(0f, 3f)] private float m_RunnerArriveSlowCubes = 1.0f;
        [Tooltip("Küpün önüne varırken düştüğü en düşük hız (tam hıza oran).")]
        [SerializeField, Range(0.1f, 1f)] private float m_RunnerArriveMinSpeed = 0.35f;
        [Tooltip("Her denizciye verilen rastgele hız farkı (±oran). Hepsi aynı ritimde yürümesin.")]
        [SerializeField, Range(0f, 0.3f)] private float m_RunnerSpeedJitter = 0.08f;
        [Tooltip("Denizci kıyıya ve güverteye konarken yaptığı ezilme miktarı (0 = yok).")]
        [SerializeField, Range(0f, 0.4f)] private float m_RunnerLandingSquash = 0.16f;
        [Tooltip("Yük binince geminin suya batma derinliği: tek yükte en az, art arda yüklerde en çok.")]
        [SerializeField] private Vector2 m_ShipBoardDipRange = new Vector2(0.08f, 0.2f);
        [Tooltip("Gemi dolduğu yükte batma derinliği.")]
        [SerializeField, Range(0f, 0.5f)] private float m_ShipFullDip = 0.26f;
        private readonly Dictionary<ShipController, (float time, int streak)> m_BoardStreaks = new Dictionary<ShipController, (float, int)>();

        [Header("↔️ Denizciler Arası Mesafe (Hafif İtme)")]
        [Tooltip("Bu mesafeden (küp cinsinden) daha yakın denizciler birbirini yana iter. 0 = kapalı.")]
        [SerializeField, Range(0f, 2f)] private float m_RunnerSeparationRadiusCubes = 0.9f;
        [Tooltip("Resmin dışında yana en fazla açılma (küp cinsinden).")]
        [SerializeField, Range(0f, 1.5f)] private float m_RunnerSeparationMaxCubes = 0.6f;
        [Tooltip("Resmin içindeki dar boşluklarda yana en fazla açılma (küp cinsinden); küplerin içine itilmesinler.")]
        [SerializeField, Range(0f, 1f)] private float m_RunnerSeparationInsideCubes = 0.2f;
        [Tooltip("Yana açılmanın ne kadar çabuk olduğu (büyüdükçe daha sert).")]
        [SerializeField, Range(1f, 30f)] private float m_RunnerSeparationResponse = 8f;
        [Tooltip("Aynı yöne yürürken öndekiyle korunan takip mesafesi (küp cinsinden). Yaklaşınca arkadaki yavaşlar. 0 = kapalı.")]
        [SerializeField, Range(0f, 2f)] private float m_RunnerFollowGapCubes = 1.4f;
        [Tooltip("Bu mesafenin (küp cinsinden) altında arkadaki tamamen durur.")]
        [SerializeField, Range(0f, 1f)] private float m_RunnerFollowStopCubes = 0.8f;

        /// <summary>Yolda yürüyen bir denizcinin itme durumu (yolun yanına ne kadar kaydığı).</summary>
        private class RunnerAgent
        {
            public Transform T;
            public int Id;
            public float Offset;
            public Vector2 Perp = Vector2.right;
            public Vector2 Dir = Vector2.up;
            public bool Active;
            public bool Moving;
        }

        /// <summary>
        /// Takip: aynı yöne yürürken hemen önünde (aynı şeritte) yürüyen biri varsa yavaşla; öndekinin içine girme.
        /// Karşıdan gelenler ve duranlar burada sayılmaz (onlar için yana açılma var).
        /// </summary>
        private float RunnerFollowFactor(RunnerAgent me, float pitch)
        {
            if (me == null || me.T == null || m_RunnerFollowGapCubes <= 0.001f) return 1f;
            float gap = pitch * m_RunnerFollowGapCubes;
            float stop = Mathf.Min(gap * 0.95f, pitch * m_RunnerFollowStopCubes);
            Vector2 myPos = new Vector2(me.T.position.x, me.T.position.y);
            float factor = 1f;
            for (int i = 0; i < m_RunnerAgents.Count; i++)
            {
                RunnerAgent a = m_RunnerAgents[i];
                if (a == null || a == me || a.T == null || !a.Active || !a.Moving) continue;
                if (Vector2.Dot(me.Dir, a.Dir) < 0.5f) continue;          // aynı yöne gitmiyor
                Vector2 rel = new Vector2(a.T.position.x, a.T.position.y) - myPos;
                float ahead = Vector2.Dot(rel, me.Dir);
                if (ahead <= 0f || ahead >= gap) continue;                  // önde değil ya da yeterince uzak
                if (Mathf.Abs(Vector2.Dot(rel, me.Perp)) > pitch * 0.6f) continue; // aynı şeritte değil
                // Açılı birleşen yollarda ikisi de diğerini önde sanabilir: yalnızca biri (büyük Id) yol verir
                if (Vector2.Dot(-rel, a.Dir) > 0f && me.Id < a.Id) continue;
                // Durma mesafesinin içine girmişler (yollar birleşince üst üste düşebiliyorlar): kimin önde olduğu
                // belirsiz, hepsi birbirini önde sanıp en düşük hızda birlikte sürünüyordu. Sabit sırayla çöz:
                // küçük Id geçer, büyük Id yol verir; grup tek sıra hâlinde açılır.
                if (ahead < stop && me.Id < a.Id) continue;
                factor = Mathf.Min(factor, Mathf.Clamp01((ahead - stop) / Mathf.Max(1e-4f, gap - stop)));
            }
            // Hiç kimse tamamen durmasın (kilitlenme olmasın), en az ağır ağır ilerlesin
            return Mathf.Max(factor, 0.08f);
        }
        private readonly List<RunnerAgent> m_RunnerAgents = new List<RunnerAgent>();

        /// <summary>
        /// Yol noktasını, yakındaki diğer denizcilerden yana (yola dik) iterek kaydırır. Yalnızca yana itilir:
        /// kimse yavaşlamaz, varış sırası değişmez. <paramref name="fade"/> küpe yakın / kıyıda 0'a iner.
        /// </summary>
        private Vector3 ApplyRunnerSeparation(RunnerAgent me, Vector3 basePos, Vector3 tangent, float fade, float pitch, float dt)
        {
            if (me == null || m_RunnerSeparationRadiusCubes <= 0.001f) return basePos;
            Vector2 perp = new Vector2(-tangent.y, tangent.x);
            if (perp.sqrMagnitude < 1e-6f) perp = me.Perp; else perp.Normalize();
            me.Perp = perp;
            me.Dir = new Vector2(perp.y, -perp.x);

            float radius = pitch * m_RunnerSeparationRadiusCubes;
            Vector2 myPos = new Vector2(basePos.x, basePos.y) + perp * me.Offset;
            float push = 0f;
            for (int i = m_RunnerAgents.Count - 1; i >= 0; i--)
            {
                RunnerAgent a = m_RunnerAgents[i];
                if (a == null || a.T == null) { m_RunnerAgents.RemoveAt(i); continue; }
                if (a == me || !a.Active) continue;
                // Aynı yöne yürüyenler yana itilmez (onları takip mesafesi ayırır); karşıdan gelen ya da duran itilir
                if (a.Moving && m_RunnerFollowGapCubes > 0.001f && Vector2.Dot(me.Dir, a.Dir) >= 0.5f) continue;
                Vector2 d = myPos - new Vector2(a.T.position.x, a.T.position.y);
                float dist = d.magnitude;
                if (dist >= radius) continue;
                float side = Vector2.Dot(d, perp);
                float sgn;
                if (Mathf.Abs(side) > radius * 0.05f) sgn = Mathf.Sign(side);
                else
                {
                    // Tam hizadalar: ortak bir yön seç (küçük Id'nin yan yönü), biri bir yana diğeri öbür yana
                    Vector2 shared = me.Id < a.Id ? perp : a.Perp;
                    Vector2 worldDir = me.Id < a.Id ? shared : -shared;
                    sgn = Vector2.Dot(worldDir, perp) >= 0f ? 1f : -1f;
                }
                push += sgn * (1f - dist / radius);
            }

            bool inside = basePos.x > m_BoardMinX - pitch * 0.5f && basePos.x < m_BoardMaxX + pitch * 0.5f &&
                          basePos.y > m_BoardBottomY - pitch * 0.5f && basePos.y < m_BoardTopY + pitch * 0.5f;
            float limit = pitch * (inside ? m_RunnerSeparationInsideCubes : m_RunnerSeparationMaxCubes);
            float target = Mathf.Clamp(push * pitch * m_RunnerSeparationMaxCubes, -limit, limit) * Mathf.Clamp01(fade);
            me.Offset = Mathf.Lerp(me.Offset, target, 1f - Mathf.Exp(-m_RunnerSeparationResponse * dt));
            return basePos + new Vector3(perp.x, perp.y, 0f) * me.Offset;
        }

        /// <summary>Hızlanma (yola çıkış) ve varışta yavaşlama çarpanı.</summary>
        private float RunnerSpeedFactor(float elapsed, float remaining, float slowDist, float minFactor)
        {
            float accel = m_RunnerAccelTime > 0.001f ? Mathf.SmoothStep(0.25f, 1f, Mathf.Clamp01(elapsed / m_RunnerAccelTime)) : 1f;
            float arrive = slowDist > 0.001f ? Mathf.Lerp(minFactor, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(remaining / slowDist))) : 1f;
            return Mathf.Min(accel, arrive);
        }

        /// <summary>Konma anında kısa ezilme: boy (kameraya doğru, -Z) kısalır, en genişler, sonra yaylanıp geri döner.</summary>
        private IEnumerator RunnerLandingSquashRoutine(Transform t, float amount, float duration)
        {
            if (t == null || amount <= 0.001f) yield break;
            Vector3 baseScale = t.localScale;
            float e = 0f;
            while (e < duration && t != null)
            {
                e += Time.deltaTime;
                float u = Mathf.Clamp01(e / duration);
                // 0→1→0 çan eğrisi, sonunda hafif ters yaylanma
                float k = Mathf.Sin(u * Mathf.PI) * (1f - u * 0.35f) - Mathf.Sin(u * Mathf.PI * 2f) * 0.12f * u;
                float a = amount * k;
                t.localScale = new Vector3(baseScale.x * (1f + a * 0.5f), baseScale.y * (1f + a * 0.5f), baseScale.z * (1f - a));
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        /// <summary>Art arda binen yük sayısına göre (0.5 sn pencere) gemi batma derinliği.</summary>
        private float NextShipBoardDip(ShipController ship)
        {
            if (ship == null) return m_ShipBoardDipRange.x;
            if (ship.IsFull) { m_BoardStreaks.Remove(ship); return m_ShipFullDip; }
            int streak = 1;
            if (m_BoardStreaks.TryGetValue(ship, out var prev) && Time.time - prev.time < 0.5f) streak = prev.streak + 1;
            m_BoardStreaks[ship] = (Time.time, streak);
            return Mathf.Lerp(m_ShipBoardDipRange.x, m_ShipBoardDipRange.y, Mathf.Clamp01((streak - 1) / 5f));
        }

        [Tooltip("Kargo konteynerini taşırken koşucunun kafasındaki yerel ofset.")]
        [SerializeField] private Vector3 m_CarriedCargoOffset = new Vector3(0f, 0.45f, 0f);

        [Header("✨ Küp Karakter Hareketi (Hypercasual Polished Movement)")]
        [Tooltip("Küp karakterlerin gemi ve arabalara giderkenki akıcı, organik hareket ayarları.")]
        [SerializeField] private CubeMovementSettings m_CubeMovementSettings;
        public CubeMovementSettings MovementSettings
        {
            get => m_CubeMovementSettings != null ? m_CubeMovementSettings : CubeMovementSettings.Default;
            set => m_CubeMovementSettings = value;
        }

        [Header("🪙 Altın Ödülleri")]
        [Tooltip("Bir gemi tam dolunca kazanılan altın.")]
        [SerializeField] private int m_CoinsPerFullShip = 1;

        /// <summary>Gemi tam dolduğunda çağrılır.</summary>
        public void OnShipFilled(ShipController ship)
        {
            // Coin sistemi tamamen kaldırıldı.
        }
        [Tooltip("Resim tamamlanınca kazanılan altın (zor seviyede iki katı).")]
        [SerializeField] private int m_LevelCompleteCoins = 20;

        private bool m_LevelEndPending = false;
        private int m_ShipsAwaitingDeparture = 0;
        private int m_ActiveCargoFlightCount = 0;

        [Header("⚡ Otomatik Yerleştirme & 2X Turbo")]
        [SerializeField] private bool m_EnableAutoPlaceAndTurbo = true;
        private bool m_IsAutoPlacing = false;
        private bool m_IsTurboActive = false;
        private float m_AutoPlaceCheckTimer = 0f;
        private int m_PlayerSentShipCount = 0;

        public void NotifyShipSent() => m_PlayerSentShipCount++;

        [Header("❌ Seviye Başarısızlık (Deadlock)")]
        [SerializeField] private bool m_EnableFailOnDeadlock = true;
        [SerializeField] private float m_DeadlockGraceDuration = 1.0f;
        private bool m_IsLevelFailed = false;
        private float m_DeadlockTimer = 0f;
        private float m_DeadlockCheckIntervalTimer = 0f;
        private bool m_CachedDeadlockCondition = false;

        public bool IsAutoPlacing => m_IsAutoPlacing;
        public bool IsTurboActive => m_IsTurboActive;
        public bool IsLevelFailed => m_IsLevelFailed;

        // Sol ve sağ kolun kıyıdaki giriş noktaları arası yarım mesafe. İki şerit arası (2x) küp boyundan
        // (~0.26) geniş olmalı; önceki 0.11 (0.22 aralık) yüzünden kollar kıyıda iç içe geçiyordu.
        private const float ShoreSideOffset = 0.17f;

        // Trende iki küp arası en kısa yol mesafesi (ızgara adımına oranla). 1'in biraz üstü:
        // virajda kiriş yaydan kısa kaldığı için küpler köşede bile birbirine girmez.
        private const float RopeSpacingFactor = 1.0f;
        // Mutlak alt sınır: virajda bile bunun altına inilmez (iç içe geçme olmaz)
        private const float RopeMinSpacingFactor = 0.90f;
        // Hız değişimlerinin ivmesi (taban hızın katı / sn): küçük = daha yumuşak hızlanıp yavaşlama
        private const float RopeAcceleration = 2.2f;
        // Önündekinden kopmuş (arada boşluk kalmış) küpün yetişmek için çıkabileceği en yüksek hız çarpanı
        private const float RopeCatchUpMaxMultiplier = 1.25f;
        // Kullanıcı isteği: "gemiler slotlara yerleşince küpler çok hızlı animasyona giriyor, smooth olsun"
        // Tren durgun başlar, bu süre boyunca yumuşak eğriyle (ease-in) tam hıza çıkar.
        private const float RopeStartRampDuration = 0.7f;
        // Küplerin panodan dış yürüme şeridine hep birlikte çıkış süresi ve zıplama yüksekliği (dünya birimi)
        private const float RopePopOutDuration = 0.35f;
        private const float RopePopOutHeight = 0.14f;

        /// <summary>Gemi başına kıyıya son varış zamanları — yeni tren eskisinin kuyruğuna binmesin.</summary>
        private class ShipRopeGate
        {
            public float LastArrivalLeft;
            public float LastArrivalRight;
        }

        private readonly Dictionary<ShipController, ShipRopeGate> m_RopeGates = new Dictionary<ShipController, ShipRopeGate>();

        // Izgara → dünya dönüşümü (seviye başına bir kez kurulur)
        private CubeGridFrame m_GridFrame;
        private bool m_GridFrameValid = false;

        // Pano / ana görsel sınırları: oyun boyunca küpler patlasa dahi asla içe küçülmez;
        // küplerin her zaman ana görselin dışındaki güvenli flank koridorundan inmesini garanti eder.
        private bool m_BoardBoundsInitialized = false;
        private float m_BoardMinX = -2.35f;
        private float m_BoardMaxX = 2.45f;
        private float m_BoardBottomY = 1.80f;
        private float m_BoardTopY = 5.85f;

        public void EnsureBoardBounds(bool forceRefresh = false)
        {
            if (m_BoardBoundsInitialized && !forceRefresh) return;

            if (m_Generator == null) m_Generator = PixelArtGenerator.Instance;
            PixelCube[] allCubes = null;
            if (m_Generator != null && m_Generator.CubesContainer != null)
            {
                allCubes = m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>(true);
            }
            if (allCubes == null || allCubes.Length == 0)
            {
                allCubes = Object.FindObjectsByType<PixelCube>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            if (allCubes != null && allCubes.Length > 0)
            {
                float mnX = float.MaxValue, mxX = float.MinValue;
                float mnY = float.MaxValue, mxY = float.MinValue;
                foreach (var c in allCubes)
                {
                    if (c == null) continue;
                    var p = c.transform.position;
                    mnX = Mathf.Min(mnX, p.x);
                    mxX = Mathf.Max(mxX, p.x);
                    mnY = Mathf.Min(mnY, p.y);
                    mxY = Mathf.Max(mxY, p.y);
                }
                if (mnX < float.MaxValue)
                {
                    m_BoardMinX = mnX;
                    m_BoardMaxX = mxX;
                    m_BoardBottomY = mnY;
                    m_BoardTopY = mxY;
                    m_BoardBoundsInitialized = true;
                }
            }
        }

        /// <summary>
        /// Seviye yeniden başlatılırken (Retry / Restart) tüm sahnede süren hareketleri,
        /// koşucuları, kargo teslimatlarını ve durum sayaçlarını anında, temiz şekilde durdurur.
        /// </summary>
        public void PrepareForLevelReload()
        {
            StopAllCoroutines();
            DestroyLiveRunners();
            m_RunnerSerial = 0;
            if (HypercasualFeedbackManager.Instance != null) HypercasualFeedbackManager.Instance.ResetShipSailSound();
            m_PlayerSentShipCount = 0;
            m_IsLevelFailed = false;
            m_LevelEndPending = false;
            m_ShipsAwaitingDeparture = 0;
            m_DeadlockTimer = 0f;
            m_DeadlockCheckIntervalTimer = 0f;
            m_CachedDeadlockCondition = false;
            SetTurboSpeed(false);
            m_IsAutoPlacing = false;
            if (CasualHudController.Instance != null)
            {
                CasualHudController.Instance.SetEndgameSpeedActive(false);
            }
            m_BoardBoundsInitialized = false;
            m_RopeGates.Clear();
            m_ActiveExtractingShips.Clear();
            m_ActiveCargoFlightCount = 0;
            s_ReservedCubes.Clear();
            m_StackClaims.Clear();
            m_GridFrameValid = false;

            ClearDockedShips();
            ClearShipsOutsideQueue();

            if (m_QueuePool != null)
            {
                m_QueuePool.StopAllCoroutines();
                m_QueuePool.ClearQueue();
            }
        }

        private void OnLevelLoaded(PixelLevelData data)
        {
            PrepareForLevelReload();
            EnsureBoardBounds(forceRefresh: true);
            EnsureReferences();
            if (data != null)
            {
                SetActivePalette(data);
            }
            else
            {
                s_PaletteColors.Clear();
                s_PaletteIndices.Clear();
                EnsurePaletteInitialized();
            }
            if (m_QueuePool != null)
            {
                m_QueuePool.InitializeQueue(force: true);
            }
        }

        private void Awake()
        {
            s_Instance = this;
            s_ReservedCubes.Clear();
            m_StackClaims.Clear();
            SanitizeSettings();
            EnsureReferences();
            EnsurePaletteInitialized();
            EnsureBoardBounds(forceRefresh: true);
        }

        private void OnValidate()
        {
            SanitizeSettings();
#if UNITY_EDITOR
            // Masa üstü (eğik kameralı) sahne için yapılmış koşucu: bacaklar kumsala doğru basar.
            if (m_TabletopRunnerPrefab == null)
                m_TabletopRunnerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(TabletopRunnerPrefabPath);
#endif
        }

        private void SanitizeSettings()
        {
            m_StackMaxWait = 0f;
            m_StackFarJumpDelay = 0f;
            if (m_HopArcHeight < 0.20f) m_HopArcHeight = 1.25f;
            if (m_HopDuration < 0.20f) m_HopDuration = 0.48f;
            // 2.4 fazla hızlı, 1.4 ağır çekim gibi bulundu; varsayılan 2.0. Inspector'dan ayarlanabilsin diye
            // yalnızca anlamsız değerler düzeltilir (üst sınırla zorla düşürmek ayarı etkisiz kılıyordu).
            if (m_RopeSpeed < 0.6f) m_RopeSpeed = 2.0f;

            // Eğer m_Shoreline eski koordinatları taşıyorsa, düz ahşap iskele çizgisine otomatik güncelle:
            float defaultShoreY = s_DefaultShoreline[0].y;
            if (m_Shoreline == null || m_Shoreline.Count < 2 || Mathf.Abs(m_Shoreline[m_Shoreline.Count / 2].y - defaultShoreY) > 0.25f)
            {
                m_Shoreline = new List<Vector2>(s_DefaultShoreline);
                m_ShorelineInset = 0.05f;
            }
        }

        private void OnEnable()
        {
            s_Instance = this;
            EnsureReferences();
            EnsurePaletteInitialized();
            EnsureBoardBounds();
            PixelArtGenerator.LevelLoaded -= OnLevelLoaded;
            PixelArtGenerator.LevelLoaded += OnLevelLoaded;
        }

        private void OnDisable()
        {
            PixelArtGenerator.LevelLoaded -= OnLevelLoaded;
            SetTurboSpeed(false);
            s_ReservedCubes.Clear();
            m_StackClaims.Clear();
            m_ActiveExtractingShips.Clear();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            // 1. Deadlock & Seviye Başarısızlık Kontrolü (tüm slotlar dolup hamle kalmadığında)
            UpdateDeadlockCheck();

            // 2. Otomatik Yerleştirme & 2X Turbo Kontrolü
            if (!m_IsAutoPlacing && !m_LevelEndPending && !m_IsLevelFailed && m_EnableAutoPlaceAndTurbo && m_PlayerSentShipCount > 0)
            {
                m_AutoPlaceCheckTimer += Time.unscaledDeltaTime;
                float interval = (m_QueuePool != null && m_QueuePool.HasNoMoreFutureShips()) ? 0.35f : 1.0f;
                if (m_AutoPlaceCheckTimer >= interval)
                {
                    m_AutoPlaceCheckTimer = 0f;
                    CheckAutoPlaceRemainingShips();
                }
            }
        }

        private void Start()
        {
            EnsureReferences();
            EnsurePaletteInitialized();
            ClearDockedShips();
            if (m_QueuePool != null)
            {
                m_QueuePool.InitializeQueue();
            }
            StartCoroutine(InitialDockedShipsCheckRoutine());
        }

        private IEnumerator InitialDockedShipsCheckRoutine()
        {
            yield return new WaitForSeconds(0.2f);
            TriggerWaitingShipsCheck();
        }

        /// <summary>
        /// Konteyner (UI) düzeninde MarinaSlotLayout yok; sahnede sabit sayıda UI slotu duruyor. Bölümün istediği
        /// kadarı açılır, fazlası kapatılır ve açık olanlar aynı aralıkla satırın ortasına dizilir.
        /// Yoksa her bölüm sahnedeki tüm slotlarla (4) oynanıyordu.
        /// </summary>
        private void ApplyUiSlotCount(int count)
        {
            var uiSlots = new List<ShipSlot>();
            foreach (var s in Object.FindObjectsByType<ShipSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (s != null && s.transform is RectTransform) uiSlots.Add(s);
            }
            if (uiSlots.Count == 0) return;
            uiSlots.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

            count = Mathf.Clamp(count, 1, uiSlots.Count);
            float spacing = uiSlots.Count > 1
                ? Mathf.Abs(((RectTransform)uiSlots[1].transform).anchoredPosition.x - ((RectTransform)uiSlots[0].transform).anchoredPosition.x)
                : 0f;

            for (int i = 0; i < uiSlots.Count; i++)
            {
                EnsureUiSlotBackLayer(uiSlots[i]);
                var rt = (RectTransform)uiSlots[i].transform;
                Vector2 p = rt.anchoredPosition;
                p.x = (i - (count - 1) * 0.5f) * spacing;
                rt.anchoredPosition = p;

                bool active = i < count;
                if (!active && uiSlots[i].DockedShip != null) uiSlots[i].ReleaseShip();
                if (uiSlots[i].gameObject.activeSelf != active) uiSlots[i].gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// Slot karesi, konteyner konturlarının (kök Canvas - 1) arkasında çizilsin diye kendi Canvas'ında (kök - 2)
        /// bir çocuk görsele taşınır; yoksa yerleşen konteynerin beyaz konturu slotun altında kalıyordu. Yerleşen
        /// konteyner slotun çocuğu olduğundan slot kökünde Canvas olamaz: kök Image saydam kalır (boyut/tıklama).
        /// </summary>
        private static void EnsureUiSlotBackLayer(ShipSlot slot)
        {
            var img = slot != null ? slot.GetComponent<UnityEngine.UI.Image>() : null;
            if (img == null || slot.transform.Find("SlotVisual") != null) return;
            Canvas parentCanvas = slot.GetComponentInParent<Canvas>();
            if (parentCanvas == null) return;
            Canvas root = parentCanvas.rootCanvas;

            var go = new GameObject("SlotVisual", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(slot.transform, false);
            go.transform.SetAsFirstSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var vis = go.GetComponent<UnityEngine.UI.Image>();
            vis.sprite = img.sprite;
            vis.type = img.type;
            vis.preserveAspect = img.preserveAspect;
            vis.color = img.color;
            vis.raycastTarget = false;

            var c = go.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingLayerID = root.sortingLayerID;
            c.sortingOrder = root.sortingOrder - 2;

            Color k = img.color;
            k.a = 0f;
            img.color = k;
        }

        public void EnsureReferences()
        {
            if (m_Generator == null)
            {
                m_Generator = PixelArtGenerator.Instance;
            }

            PixelLevelData level = m_Generator != null ? m_Generator.ActiveLevelData : null;
            int targetSlots = (level != null && level.SlotCount > 0) ? level.SlotCount : 5;

            var marina = UnityEngine.Object.FindFirstObjectByType<MarinaSlotLayout>();
            if (marina != null)
            {
                marina.SetSlotCount(targetSlots);
            }
            else if (Application.isPlaying)
            {
                ApplyUiSlotCount(targetSlots);
            }

            // Sadece aktif olan slotları sıralı olarak al
            m_Slots.Clear();
            var allSlots = ShipSlot.ActiveSlots;
            for (int i = 0; i < allSlots.Count; i++)
            {
                var s = allSlots[i];
                if (s != null && s.gameObject.activeInHierarchy)
                {
                    m_Slots.Add(s);
                }
            }
            m_Slots.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

            if (m_QueuePool == null)
            {
                m_QueuePool = UnityEngine.Object.FindFirstObjectByType<ShipQueuePool>();
            }
        }

        /// <summary>
        /// Tüm yanaşma slotlarındaki gemileri temizler. Seviye başlangıcında slotların boş olmasını garanti eder.
        /// </summary>
        /// <summary>
        /// Bölüm yeniden yüklenirken slota giden ya da kalkıp ayrılan (yanaşmamış) gemileri de siler.
        /// Bunların hareket tween'leri DOTween.KillAll ile ölünce yarı yolda donup slotta garip görüntü bırakıyordu.
        /// Kuyruktaki gemilere dokunulmaz (kuyruk kendi yeniden kurulur).
        /// </summary>
        private void ClearShipsOutsideQueue()
        {
            if (!Application.isPlaying) return;
            var waiting = m_QueuePool != null ? m_QueuePool.WaitingShips : null;
            var ships = new List<ShipController>(ShipController.ActiveShips);
            for (int i = 0; i < ships.Count; i++)
            {
                var ship = ships[i];
                if (ship == null) continue;
                if (waiting != null && waiting.Contains(ship)) continue;
                // Kuyruk havuzunun altındaki (henüz sahneye girmemiş) gemiler de kuyruğa aittir
                if (m_QueuePool != null && ship.transform.IsChildOf(m_QueuePool.transform)) continue;
                ship.StopAllCoroutines();
                ship.transform.DOKill(false);
                ship.gameObject.SetActive(false);
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(ship.gameObject);
                else Destroy(ship.gameObject);
#else
                Destroy(ship.gameObject);
#endif
            }
        }

        public void ClearDockedShips()
        {
            if (m_Slots == null) return;
            for (int i = 0; i < m_Slots.Count; i++)
            {
                var s = m_Slots[i];
                if (s != null && s.DockedShip != null)
                {
                    var ship = s.DockedShip;
                    s.ReleaseShip();
                    if (ship != null)
                    {
                        ship.StopAllCoroutines();
                        ship.transform.DOKill(false);
                        ship.gameObject.SetActive(false);
#if UNITY_EDITOR
                        if (!Application.isPlaying) DestroyImmediate(ship.gameObject);
                        else Destroy(ship.gameObject);
#else
                        Destroy(ship.gameObject);
#endif
                    }
                }
            }
        }

        // Aktif bölümün paleti: her renk varyantı (orijinal, hedef, ayarlı hedef, gemi rengi) → palet indeksi.
        // Renk eşleşmesi bu indeks üzerinden yapılır; sabit tolerans koyu yeşili laciverde, açık maviyi
        // koyu maviye bağlıyordu (ve eşleşme geçişsiz olduğu için gemiler yanlış küpleri topluyordu).
        private static readonly List<Color> s_PaletteColors = new List<Color>();
        private static readonly List<int> s_PaletteIndices = new List<int>();
        private const float PaletteSnapMaxSqrDistance = 0.035f;
        private static int s_YellowPaletteEntryCount;

        private static bool IsYellowTone(Color c) => c.r > 0.75f && c.g > 0.50f && c.b < 0.35f;

        public static PixelLevelData ActivePaletteLevel { get; private set; }

        public void EnsurePaletteInitialized()
        {
            if (m_Generator == null) m_Generator = PixelArtGenerator.Instance;
            PixelLevelData level = m_Generator != null ? m_Generator.ActiveLevelData : null;
            if (level == null && LevelManager.Instance != null) level = LevelManager.Instance.CurrentLevel;

            if (level != null && level.ColorPalette != null && level.ColorPalette.Count > 0)
            {
                if (ActivePaletteLevel != level || s_PaletteColors.Count == 0)
                {
                    SetActivePalette(level);
                }
                return;
            }

            if (s_PaletteColors.Count == 0)
            {
                var allCubes = UnityEngine.Object.FindObjectsByType<PixelCube>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (allCubes != null && allCubes.Length > 0)
                {
                    EnsurePaletteFromCubes(allCubes);
                }
            }
        }

        public static void EnsurePaletteFromCubes(IEnumerable<PixelCube> cubes)
        {
            if (s_PaletteColors.Count > 0) return;
            var unique = new List<Color>();
            foreach (var c in cubes)
            {
                if (c == null) continue;
                Color col = c.CurrentColor;
                bool exists = false;
                for (int i = 0; i < unique.Count; i++)
                {
                    float dr = unique[i].r - col.r, dg = unique[i].g - col.g, db = unique[i].b - col.b;
                    if (dr * dr + dg * dg + db * db < 0.005f) { exists = true; break; }
                }
                if (!exists) unique.Add(col);
            }
            s_PaletteColors.Clear();
            s_PaletteIndices.Clear();
            for (int i = 0; i < unique.Count; i++)
            {
                s_PaletteColors.Add(unique[i]);
                s_PaletteIndices.Add(i);
            }
        }

        /// <summary>
        /// ColorsMatch'in kullanacağı paleti ayarlar. null verilirse eski toleranslı karşılaştırmaya döner.
        /// </summary>
        public static void SetActivePalette(PixelLevelData level)
        {
            ActivePaletteLevel = level;
            s_PaletteColors.Clear();
            s_PaletteIndices.Clear();
            s_YellowPaletteEntryCount = 0;
            if (level == null || level.ColorPalette == null) return;

            foreach (var entry in level.ColorPalette)
            {
                if (entry == null) continue;
                Color adjusted = PixelCube.AdjustColor(entry.targetColor, level.ColorBrightness, level.ColorSaturation, level.ColorContrast);
                if (IsYellowTone(entry.targetColor) || IsYellowTone(adjusted)) s_YellowPaletteEntryCount++;
            }

            for (int i = 0; i < level.ColorPalette.Count; i++)
            {
                var entry = level.ColorPalette[i];
                if (entry == null) continue;
                Color adjusted = PixelCube.AdjustColor(entry.targetColor, level.ColorBrightness, level.ColorSaturation, level.ColorContrast);
                AddPaletteVariant(entry.originalColor, i);
                AddPaletteVariant(entry.targetColor, i);
                AddPaletteVariant(adjusted, i);
                AddPaletteVariant(NormalizeShipColor(entry.targetColor), i);
                AddPaletteVariant(NormalizeShipColor(adjusted), i);
            }

            if (level.UseCustomWagonSequence && level.WagonSequence != null)
            {
                for (int w = 0; w < level.WagonSequence.Count; w++)
                {
                    var wagon = level.WagonSequence[w];
                    if (wagon == null) continue;
                    int palIdx = wagon.paletteIndex;
                    if (palIdx >= 0 && palIdx < level.ColorPalette.Count)
                    {
                        AddPaletteVariant(wagon.wagonColor, palIdx);
                    }
                }
            }
        }

        private static void AddPaletteVariant(Color c, int index)
        {
            s_PaletteColors.Add(c);
            s_PaletteIndices.Add(index);
        }

        /// <summary>Rengi en yakın palet girdisine oturtur; palet yoksa veya renk hiçbirine yakın değilse -1.</summary>
        public static int GetPaletteIndex(Color c)
        {
            int bestIndex = -1;
            float bestDist = PaletteSnapMaxSqrDistance;
            for (int i = 0; i < s_PaletteColors.Count; i++)
            {
                Color p = s_PaletteColors[i];
                float dr = p.r - c.r, dg = p.g - c.g, db = p.b - c.b;
                float d = dr * dr + dg * dg + db * db;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIndex = s_PaletteIndices[i];
                }
            }
            return bestIndex;
        }

        /// <summary>
        /// İki rengin aynı oyun rengi olup olmadığını söyler. Aktif palet varsa iki renk de en yakın palet
        /// girdisine oturtulup indeksleri karşılaştırılır; yoksa hassas RGB karşılaştırmasına düşer.
        /// Asla yeşili koyu griye ya da açık maviyi beyaza eşlemez.
        /// </summary>
        public static bool ColorsMatch(Color a, Color b)
        {
            if (s_PaletteColors.Count == 0 && Instance != null)
            {
                Instance.EnsurePaletteInitialized();
            }

            if (s_PaletteColors.Count > 0)
            {
                int ia = GetPaletteIndex(a);
                int ib = GetPaletteIndex(b);
                if (ia >= 0 && ib >= 0) return ia == ib;
            }

            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            if ((dr * dr + dg * dg + db * db) < 0.008f) return true;

            // Sarı / Amber tonları için özel tolerans (küp ve gemi her koşulda %100 eşleşir):
            if (IsYellowTone(a) && IsYellowTone(b)) return true;

            return false;
        }

        private static readonly HashSet<PixelCube> s_ReservedCubes = new HashSet<PixelCube>();
        private readonly HashSet<ShipController> m_ActiveExtractingShips = new HashSet<ShipController>();

        /// <summary>
        /// Izgara dışındaki tüm boş alanlardan (dış hava) BFS başlatarak dış havayı bulur.
        /// </summary>
        public static HashSet<(int, int)> CalculateOutsideAir(Dictionary<(int, int), PixelCube> gridMap, int minX, int maxX, int minY, int maxY)
        {
            HashSet<(int, int)> outsideAir = new HashSet<(int, int)>();
            if (gridMap == null || gridMap.Count == 0) return outsideAir;

            int boundMinX = minX - 3;
            int boundMaxX = maxX + 3;
            int boundMinY = minY - 2;
            int boundMaxY = maxY + 3;

            Queue<(int, int)> airQueue = new Queue<(int, int)>();

            for (int x = boundMinX; x <= boundMaxX; x++)
            {
                airQueue.Enqueue((x, boundMinY));
                outsideAir.Add((x, boundMinY));
                airQueue.Enqueue((x, boundMaxY));
                outsideAir.Add((x, boundMaxY));
            }
            for (int y = boundMinY + 1; y < boundMaxY; y++)
            {
                airQueue.Enqueue((boundMinX, y));
                outsideAir.Add((boundMinX, y));
                airQueue.Enqueue((boundMaxX, y));
                outsideAir.Add((boundMaxX, y));
            }

            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };

            while (airQueue.Count > 0)
            {
                var (cx, cy) = airQueue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + dx[i];
                    int ny = cy + dy[i];

                    if (nx >= boundMinX && nx <= boundMaxX && ny >= boundMinY && ny <= boundMaxY)
                    {
                        if (!outsideAir.Contains((nx, ny)))
                        {
                            if (!gridMap.ContainsKey((nx, ny)))
                            {
                                outsideAir.Add((nx, ny));
                                airQueue.Enqueue((nx, ny));
                            }
                        }
                    }
                }
            }

            return outsideAir;
        }

        /// <summary>
        /// Verilen renkle eşleşen, henüz patlatılmamış/rezerve edilmemiş ve DIŞTA olan (en dış katmandaki) küpleri döner.
        /// CollectionOrder kuralıyla her zaman en dıştan en içe, en açık ve en yakın olanlar önceliklidir.
        /// </summary>
        public List<PixelCube> GetExposedMatchingCubes(Color shipColor)
        {
            return GetNextCollectibleBlocks(shipColor);
        }

        public bool HasExposedMatchingCube(Color shipColor)
        {
            var list = GetExposedMatchingCubes(shipColor);
            return list != null && list.Count > 0;
        }

        public bool HasMatchingCube(Color shipColor)
        {
            EnsurePaletteInitialized();
            var allCubes = PixelCube.ActiveCubes;
            if (allCubes == null) return false;
            for (int i = 0; i < allCubes.Count; i++)
            {
                var c = allCubes[i];
                if (c == null || c.IsPopped || s_ReservedCubes.Contains(c) || c.IsMystery) continue;
                if (ColorsMatch(c.CurrentColor, shipColor) || ColorsMatch(c.OriginalColor, shipColor))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gemi bir slota yanaştığında çağrılır.
        /// Sadece dıştaki küpleri toplar; dışta küp yoksa açılana kadar slotta bekler.
        /// </summary>
        public void OnShipDocked(ShipController ship)
        {
            if (ship == null || ship.IsDeparting) return;
            StartCoroutine(ExtractMatchingCubesToShipRoutine(ship));
            CheckAutoPlaceRemainingShips();
        }

        /// <summary>
        /// Dış havaya açılan veya komşusu temizlenen gizli / soru işaretli küpleri kontrol eder ve açar.
        /// </summary>
        public void CheckAndRevealMysteryCubes(bool triggerFollowUpCheck = false)
        {
            var allCubes = PixelCube.ActiveCubes;
            if (allCubes == null || allCubes.Count == 0) return;

            bool hasMystery = false;
            for (int i = 0; i < allCubes.Count; i++)
            {
                if (allCubes[i] != null && allCubes[i].IsMystery)
                {
                    hasMystery = true;
                    break;
                }
            }
            if (!hasMystery) return;

            if (!TryBuildLiveGrid(out var gridMap, out var outsideAir, out _)) return;

            PixelLevelData level = (m_Generator != null) ? m_Generator.ActiveLevelData : null;
            if (level == null && LevelManager.Instance != null) level = LevelManager.Instance.CurrentLevel;
            MysteryRevealCondition condition = (level != null) ? level.MysteryRevealCondition : MysteryRevealCondition.WhenExposed;

            bool anyRevealed = false;
            foreach (var kvp in gridMap)
            {
                PixelCube cube = kvp.Value;
                if (cube == null || !cube.IsMystery) continue;

                int x = cube.GridX;
                int y = cube.GridY;

                bool shouldReveal = false;
                if (condition == MysteryRevealCondition.WhenExposed)
                {
                    bool touchesAir = outsideAir != null && (
                        outsideAir.Contains((x - 1, y)) ||
                        outsideAir.Contains((x + 1, y)) ||
                        outsideAir.Contains((x, y - 1)) ||
                        outsideAir.Contains((x, y + 1))
                    );
                    if (touchesAir) shouldReveal = true;
                }
                else // WhenNeighborCleared
                {
                    bool neighborMissing = !gridMap.ContainsKey((x - 1, y)) ||
                                           !gridMap.ContainsKey((x + 1, y)) ||
                                           !gridMap.ContainsKey((x, y - 1)) ||
                                           !gridMap.ContainsKey((x, y + 1));
                    if (neighborMissing) shouldReveal = true;
                }

                if (shouldReveal)
                {
                    cube.RevealMystery(true);
                    anyRevealed = true;
                }
            }

            if (anyRevealed && triggerFollowUpCheck)
            {
                TriggerWaitingShipsCheck();
            }
        }

        /// <summary>
        /// Bir küp patladığında veya gemi yanaştığında, slotlarda bekleyen dolmamış diğer gemilerin
        /// önüne yeni açılan dış küp gelip gelmediğini kontrol eder ve toplamayı başlatır.
        /// </summary>
        public void TriggerWaitingShipsCheck()
        {
            CheckAndRevealMysteryCubes(triggerFollowUpCheck: false);

            if (m_Slots == null || m_Slots.Count == 0) return;

            foreach (var slot in m_Slots)
            {
                if (slot != null && !slot.IsEmpty && slot.DockedShip != null)
                {
                    ShipController ship = slot.DockedShip;
                    // Slot gemiye yola çıkarken ayrılır; tren ancak gemi slota oturunca başlasın
                    if (ship != null && ship.IsDocked && !ship.IsMoving && ship.CanAcceptMore && !m_ActiveExtractingShips.Contains(ship))
                    {
                        bool canLaunch = HasExposedMatchingCube(ship.ShipColor);
                        if (canLaunch)
                        {
                            StartCoroutine(ExtractMatchingCubesToShipRoutine(ship));
                        }
                    }
                }
            }
        }

        private IEnumerator ExtractMatchingCubesToShipRoutine(ShipController ship)
        {
            if (ship == null || ship.IsDeparting) yield break;
            if (!ship.IsDocked || ship.IsMoving) yield break;
            if (m_ActiveExtractingShips.Contains(ship)) yield break;

            m_ActiveExtractingShips.Add(ship);

            try
            {
                while (ship != null && !ship.IsDeparting)
                {
                    // 1. Gemi yeni yük kabul edebildiği ve dışta uygun küp olduğu sürece gemici fırlat
                    while (ship != null && ship.CanAcceptMore && !ship.IsDeparting)
                    {
                        if (!TryLaunchRope(ship, out float boardClearTime, m_UseReversedShipRunners ? 1 : int.MaxValue))
                        {
                            break; // Şu an dışta açık küp yok
                        }

                        while (ship != null && Time.time < boardClearTime) yield return null;
                        TriggerWaitingShipsCheck();
                    }

                    // 2. Seviyede bu renkten hiç küp kalmadıysa:
                    int totalRemaining = GetRemainingCountForColor(ship.ShipColor);
                    if (totalRemaining == 0)
                    {
                        yield return new WaitForSeconds(0.2f);
                        while (ship != null && (ship.HasPendingCargo || ship.ActiveWorkersOut > 0)) yield return null;
                        if (ship != null && !ship.IsDeparting)
                        {
                            ship.DepartAndFreeSlot();
                        }
                        break;
                    }

                    // 3. Kapasite durumu:
                    if (!ship.CanAcceptMore)
                    {
                        // Gemiye ait tüm kapasite şu an yoldaki gemicilere ayrılmış durumda.
                        // Yoldaki işçilerin dönmesini bekle; ANCAK bir işçi yoldayken alamadığı kotayı bırakırsa
                        // (boşa çıkınca), ship.CanAcceptMore tekrar true olur ve döngü HEMEN yeni gemici gönderir!
                        while (ship != null && !ship.CanAcceptMore && (ship.HasPendingCargo || ship.ActiveWorkersOut > 0))
                        {
                            yield return null;
                        }

                        // Eğer bir işçi kotayı bıraktı ve yer açıldıysa hemen yeni gemici gönder!
                        if (ship != null && ship.CanAcceptMore)
                        {
                            continue;
                        }

                        // Tüm işçiler döndü ve gemi tamamen dolduysa kalkış yap:
                        if (ship != null && ship.IsFull && ship.ActiveWorkersOut <= 0 && !ship.IsDeparting)
                        {
                            yield return new WaitForSeconds(0.2f);
                            if (ship != null && !ship.IsDeparting)
                            {
                                ship.DepartAndFreeSlot();
                            }
                            break;
                        }
                    }
                    else
                    {
                        // ship.CanAcceptMore == true ama TryLaunchRope false döndü (şu an açık küp yok).
                        // Yoldaki gemiciler küp aldıkça arkadaki küpler açılabilir veya başka gemiler küp açabilir:
                        if (ship.HasPendingCargo || m_ActiveCargoFlightCount > 0)
                        {
                            bool newCubeExposed = false;
                            while (ship != null && ship.CanAcceptMore && (ship.HasPendingCargo || m_ActiveCargoFlightCount > 0))
                            {
                                if (HasExposedMatchingCube(ship.ShipColor))
                                {
                                    newCubeExposed = true;
                                    break;
                                }
                                yield return new WaitForSeconds(0.05f);
                            }

                            if (newCubeExposed)
                            {
                                continue; // Yeni küp açıldı; hemen sıradaki gemiciyi gönder!
                            }
                        }

                        // Açılacak yeni küp yok ve yolda işçi de kalmadı:
                        while (ship != null && (ship.HasPendingCargo || ship.ActiveWorkersOut > 0)) yield return null;
                        if (ship != null && ship.IsFull && ship.ActiveWorkersOut <= 0 && !ship.IsDeparting)
                        {
                            yield return new WaitForSeconds(0.2f);
                            if (ship != null && !ship.IsDeparting)
                            {
                                ship.DepartAndFreeSlot();
                            }
                        }
                        break;
                    }
                }
            }
            finally
            {
                // Gemi bu arada yok edilmiş olsa bile referansı listeden çıkar
                m_ActiveExtractingShips.Remove(ship);
            }

            // Çekim bittikten sonra da genel kontrol yap
            TriggerWaitingShipsCheck();
        }

        /// <summary>
        /// Gemi için dış kenardaki eşleşen küplerden iki kollu bir tren kurar ve yola çıkarır.
        /// Dışta uygun küp yoksa false döner. <paramref name="boardClearTime"/>, trenin kuyruğunun
        /// panodan çıkacağı andır.
        /// </summary>
        private bool TryLaunchRope(ShipController ship, out float boardClearTime, int maxPerLaunch = int.MaxValue)
        {
            boardClearTime = Time.time;
            if (ship == null || !ship.CanAcceptMore) return false;
            if (!TryBuildLiveGrid(out var gridMap, out var outsideAir, out int minY)) return false;
            if (!EnsureGridFrame()) return false;

            s_ReservedCubes.RemoveWhere(c => c == null || c.IsPopped || !c.gameObject.activeSelf);

            // 1. Bu geminin rengiyle eşleşen ve henüz rezerve edilmemiş TÜM canlı küpleri topla
            var matchingCubes = new List<PixelCube>();
            int minX = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var kvp in gridMap)
            {
                PixelCube cube = kvp.Value;
                int gx = kvp.Key.Item1;
                int gy = kvp.Key.Item2;
                if (gx < minX) minX = gx;
                if (gx > maxX) maxX = gx;
                if (gy > maxY) maxY = gy;

                if (s_ReservedCubes.Contains(cube)) continue;
                if (cube.IsMystery) continue;
                if (!ColorsMatch(cube.CurrentColor, ship.ShipColor) && !ColorsMatch(cube.OriginalColor, ship.ShipColor)) continue;

                matchingCubes.Add(cube);
            }

            if (matchingCubes.Count == 0) return false;

            // Seçim kuralı (GetExposedMatchingCubes ile aynı): yalnızca şu an gerçekten açık küpler,
            // en alt sıra önce, aynı sırada soldan sağa.
            int needed = Mathf.Max(0, ship.Capacity - (ship.CurrentCargo + ship.PendingCargo));
            int limit = Mathf.Min(needed, Mathf.Max(1, maxPerLaunch));
            if (limit <= 0) return false;

            List<PixelCube> candidates;
            if (m_UseReversedShipRunners)
            {
                // Önce bekleyen denizciler (en eski çıkandan başlayarak) yeni açılan küpleri alır; yeni denizcinin ilk
                // küpü kalanlar arasından toplama sırasına göre (en alt, en dış) seçilir — mesafeye göre değil
                AllocateStackClaims();
                candidates = GetNextCollectibleBlocks(ship.ShipColor);
            }
            else
            {
                candidates = GetExposedMatchingCubes(ship.ShipColor);
            }
            if (candidates.Count > limit) candidates.RemoveRange(limit, candidates.Count - limit);

            if (candidates.Count == 0) return false;

            EnsureBoardBounds();
            Vector3 shoreCenter = GetShorePoint(ship);

            int reserved = 0;
            while (reserved < candidates.Count && ship.TryReserveCargo()) reserved++;
            if (reserved == 0) return false;

            var selectedCubes = new List<PixelCube>(reserved);
            for (int i = 0; i < reserved; i++)
            {
                var c = candidates[i];
                selectedCubes.Add(c);
                s_ReservedCubes.Add(c);
            }

            if (m_UseReversedShipRunners)
            {
                bool sailorRunners = m_UseSailorRunner && GetRunnerPrefab() != null && GetRunnerPrefab().GetComponent<SailorRunnerVisual>() != null;

                // Yığın taşıma: denizci ilk küpü alır, kalanını yolda tek tek toplar. Kotası kadar yer şimdiden
                // ayrılır ki sonraki denizciler aynı yükü paylaşmasın (30 → 6 denizci × 5). Bulamadığını dönüşte geri verir.
                int batchQuota = reserved;
                StackClaim claim = null;
                if (sailorRunners && reserved == 1 && m_SailorStackSize > 1)
                {
                    while (batchQuota < m_SailorStackSize && ship.TryReserveCargo()) batchQuota++;
                    ship.AddUnassignedQuota(batchQuota - reserved);
                    if (batchQuota > reserved)
                    {
                        // Görevin kalanı: sıradaki küpler denizci ilk küpü aldıktan sonra, bulunduğu yere en yakın açık küpten tek tek ayrılır
                        claim = new StackClaim { Serial = ++m_StackClaimSerial, Ship = ship, Color = ship.ShipColor, Unassigned = batchQuota - reserved,
                                                 Last = (selectedCubes[0].GridX, selectedCubes[0].GridY) };
                        m_StackClaims.Add(claim);
                        AllocateStackClaims();
                    }
                }

                StartCoroutine(RunReversedShipRunnersToCollectCargo(selectedCubes, ship, shoreCenter, batchQuota, claim));

                if (m_Generator != null) m_Generator.RegenerateContourShadowFromLiveCubeState();

                m_ActiveCargoFlightCount += batchQuota;
                // Bir sonraki koşucu, bu grubun dönmesini beklemeden sabit aralıkla çıkar.
                float launchInterval = sailorRunners ? Mathf.Max(m_RunnerLaunchInterval, m_SailorLaunchInterval) : m_RunnerLaunchInterval;
                boardClearTime = Time.time + launchInterval * reserved;
                return true;
            }

            float centerX = (m_BoardMinX + m_BoardMaxX) * 0.5f;
            float baseSpeed = Mathf.Max(3.2f, EffectiveRopeSpeed());
            float maxDuration = 0f;

            var paths = new List<ShoreLanePath>(reserved);
            var boardExitDists = new List<float>(reserved);
            var entersLeftList = new List<bool>(reserved);

            for (int i = 0; i < selectedCubes.Count; i++)
            {
                PixelCube c = selectedCubes[i];
                bool entersLeft = c.transform.position.x <= centerX;
                Vector3 boatEntrance = shoreCenter + new Vector3(entersLeft ? -0.06f : 0.06f, 0f, 0f);

                ShoreLanePath cubePath = BuildCubeObstacleAvoidancePath(
                    c, outsideAir, gridMap, minX, maxX, minY, maxY, boatEntrance, entersLeft, out float exitDist);

                paths.Add(cubePath);
                boardExitDists.Add(exitDist);
                entersLeftList.Add(entersLeft);

                float dur = (exitDist / baseSpeed) + 0.35f;
                if (dur > maxDuration) maxDuration = dur;
            }

            StartCoroutine(RunIndependentCubesToShip(selectedCubes, paths, boardExitDists, entersLeftList, ship, shoreCenter));

            if (m_Generator != null) m_Generator.RegenerateContourShadowFromLiveCubeState();

            m_ActiveCargoFlightCount += reserved;
            boardClearTime = Time.time + maxDuration;
            return true;
        }

        private static bool IsExposed(PixelCube c, HashSet<(int, int)> outsideAir)
        {
            int x = c.GridX, y = c.GridY;
            return outsideAir.Contains((x - 1, y)) || outsideAir.Contains((x + 1, y)) ||
                   outsideAir.Contains((x, y - 1)) || outsideAir.Contains((x, y + 1));
        }


        /// <summary>
        /// Küp kolu için zemin ve engel sınırına tam uyan, güvenli kontur koridoru oluşturur:
        /// Küp hücreleri -> Dış hava çıkışı -> Obstacle Boundary (Clearance) -> Sahil yaklaşımı -> Gemi girişi.
        /// Asla pixel-art'ın içinden geçmez, köşeleri kesmez.
        /// </summary>
        private UpperBeachGround m_BeachGround;

        /// <summary>Kumsal zemin düzleminin dünya z'si; zemin yoksa NaN.</summary>
        public float BeachGroundZ()
        {
            if (m_BeachGround == null) m_BeachGround = UpperBeachGround.Instance;
            return m_BeachGround != null ? m_BeachGround.GroundWorldZ : float.NaN;
        }

        /// <summary>
        /// Küpün arka yüzü kumsal düzlemine değecek şekilde merkezinin olması gereken z.
        /// Hesap şüpheliyse (zemin yok / panodan bir küp boyundan fazla sapma) pano z'si kullanılır.
        /// </summary>
        private float GroundContactZ(PixelCube cube, float boardZ, float pitch)
        {
            if (cube == null) return boardZ;
            float groundPlaneZ = BeachGroundZ();
            float bodyMaxZ = cube.BodyMaxWorldZ();
            if (!float.IsFinite(groundPlaneZ) || !float.IsFinite(bodyMaxZ)) return boardZ;

            float contactZ = cube.transform.position.z + (groundPlaneZ - bodyMaxZ);
            return Mathf.Abs(contactZ - boardZ) <= pitch ? contactZ : boardZ;
        }

        /// <summary>
        /// Küpün hücresinden başlayarak, panodaki katı (solid) piksel küplerine basmadan,
        /// sadece BOŞ HÜCRELER (outsideAir ve boş alanlar) üzerinden panonun alt sınırına
        /// (minY - 1) inen en kısa ve hedef gemiye yönelen yolu bulur.
        /// Asla katı piksellerin üzerinden veya köşelerinden geçmez (sınırları %100 korur).
        /// </summary>
        private List<(int, int)> FindEmptySpaceGridPath(
            PixelCube startCube,
            HashSet<(int, int)> outsideAir,
            Dictionary<(int, int), PixelCube> gridMap,
            int minX, int maxX, int minY, int maxY,
            Vector3 boatEntrance)
        {
            var path = new List<(int, int)>();
            if (startCube == null) return path;

            (int, int) startCell = (startCube.GridX, startCube.GridY);

            // Eğer küp zaten panonun altındaysa hemen bitir
            if (startCell.Item2 <= minY - 1)
            {
                path.Add(startCell);
                return path;
            }

            int boundMinX = minX - 4;
            int boundMaxX = maxX + 4;
            int boundMinY = minY - 2;
            int boundMaxY = maxY + 4;

            bool IsWalkable(int gx, int gy)
            {
                if (gx < boundMinX || gx > boundMaxX || gy < boundMinY || gy > boundMaxY)
                    return false;

                if (gx == startCell.Item1 && gy == startCell.Item2)
                    return true;

                // İçinde aktif, katı ve henüz ayrılmamış bir piksel küpü varsa ENGELDİR (yürünemez)
                if (gridMap != null && gridMap.TryGetValue((gx, gy), out PixelCube occ))
                {
                    if (occ != null && !occ.IsPopped && occ.gameObject.activeSelf && !occ.IsLeaving && occ != startCube)
                    {
                        return false;
                    }
                }
                return true;
            }

            Vector2 boatTarget2D = new Vector2(boatEntrance.x, boatEntrance.y);

            float Heuristic(int gx, int gy)
            {
                Vector3 w = m_GridFrame.ToWorld(gx, gy);
                // Hedef gemi girişine olan 2B mesafe
                return Vector2.Distance(new Vector2(w.x, w.y), boatTarget2D);
            }

            // A* Arama Yapısı
            var openList = new List<((int x, int y) cell, float fScore)>(64);
            var gScores = new Dictionary<(int, int), float>(128);
            var cameFrom = new Dictionary<(int, int), (int, int)>(128);

            gScores[startCell] = 0f;
            openList.Add((startCell, Heuristic(startCell.Item1, startCell.Item2)));

            (int, int)? goal = null;

            // 8 yönlü hareket vektörleri: 4 ortogonal + 4 diyagonal
            (int dx, int dy, float cost)[] moves = new (int, int, float)[]
            {
                (0, -1, 1.0f),  // Aşağı (kumsala doğru)
                (-1, 0, 1.0f),  // Sol
                (1, 0, 1.0f),   // Sağ
                (-1, -1, 1.414f), // Sol-Aşağı diyagonal
                (1, -1, 1.414f),  // Sağ-Aşağı diyagonal
                (0, 1, 1.0f),   // Yukarı
                (-1, 1, 1.414f),  // Sol-Yukarı diyagonal
                (1, 1, 1.414f)   // Sağ-Yukarı diyagonal
            };

            int maxIterations = 600;
            while (openList.Count > 0 && --maxIterations > 0)
            {
                // En düşük fScore'a sahip düğümü seç
                int bestIdx = 0;
                float bestF = openList[0].fScore;
                for (int i = 1; i < openList.Count; i++)
                {
                    if (openList[i].fScore < bestF)
                    {
                        bestF = openList[i].fScore;
                        bestIdx = i;
                    }
                }

                var current = openList[bestIdx].cell;
                openList.RemoveAt(bestIdx);

                // Hedef: Panonun en alt sırasının altına ulaştıysak güvenli kumsal koridoruna vardık demektir
                if (current.y <= minY - 1)
                {
                    goal = current;
                    break;
                }

                float curG = gScores[current];

                for (int m = 0; m < moves.Length; m++)
                {
                    int nx = current.x + moves[m].dx;
                    int ny = current.y + moves[m].dy;
                    var next = (nx, ny);

                    if (!IsWalkable(nx, ny)) continue;

                    // DİYAGONAL KÖŞE KESME KORUMASI (No Corner Cutting):
                    // Diyagonal geçerken her iki komşu ortogonal hücre de yürünebilir (boş) OLMALIDIR!
                    // Böylece katı piksellerin köşelerinden sıyrılamaz, sadece açık boş alanlarda diyagonal yürür.
                    if (moves[m].dx != 0 && moves[m].dy != 0)
                    {
                        if (!IsWalkable(current.x, ny) || !IsWalkable(nx, current.y))
                            continue;
                    }

                    float tentativeG = curG + moves[m].cost;
                    if (!gScores.TryGetValue(next, out float existingG) || tentativeG < existingG)
                    {
                        gScores[next] = tentativeG;
                        cameFrom[next] = current;
                        float f = tentativeG + Heuristic(nx, ny);
                        openList.Add((next, f));
                    }
                }
            }

            if (!goal.HasValue)
            {
                return path;
            }

            var step = goal.Value;
            while (step != startCell)
            {
                path.Add(step);
                step = cameFrom[step];
            }
            path.Add(startCell);
            path.Reverse();

            return path;
        }

        /// <summary>
        /// Her küp için kendi anlık dünya pozisyonundan gemi girişine giden bağımsız,
        /// sadece BOŞ ALANLAR üzerinden (pikselartın üstünden ASLA geçmeden) pürüzsüz ve akıcı yürüyüş yolu oluşturur.
        /// </summary>
        private ShoreLanePath BuildCubeObstacleAvoidancePath(
            PixelCube cube,
            HashSet<(int, int)> outsideAir,
            Dictionary<(int, int), PixelCube> gridMap,
            int minX, int maxX, int minY, int maxY,
            Vector3 boatEntrance,
            bool isLeft,
            out float boardExitDist)
        {
            boardExitDist = 0f;
            if (cube == null) return ShoreLanePath.BuildDirect(Vector3.zero, boatEntrance);

            Vector3 startPos = cube.transform.position;
            float boardZ = startPos.z;
            float pitch = Mathf.Max(0.12f, m_GridFrame.Pitch);
            CubeMovementSettings s = MovementSettings;
            float clearance = Mathf.Max(pitch * 1.4f, s.ObstacleClearance);
            float groundZ = GroundContactZ(cube, boardZ, pitch);

            var rawWpts = new List<Vector3>(24);
            rawWpts.Add(startPos);

            // 1. AŞAMA (PANO İÇİNDEN ÇIKIŞ): Küpün hücresinden sadece boş alanlar üzerinden pano altına iniş
            var gridPath = FindEmptySpaceGridPath(cube, outsideAir, gridMap, minX, maxX, minY, maxY, boatEntrance);

            if (gridPath != null && gridPath.Count > 1)
            {
                // Kolonel (aynı doğrultuda devam eden) gereksiz adımları ayıkla
                var simplified = new List<(int, int)>(gridPath.Count);
                simplified.Add(gridPath[0]);
                for (int i = 1; i < gridPath.Count - 1; i++)
                {
                    int dx1 = gridPath[i].Item1 - simplified[simplified.Count - 1].Item1;
                    int dy1 = gridPath[i].Item2 - simplified[simplified.Count - 1].Item2;
                    int dx2 = gridPath[i + 1].Item1 - gridPath[i].Item1;
                    int dy2 = gridPath[i + 1].Item2 - gridPath[i].Item2;

                    // Aynı yönde düz bir hat boyunca devam ediyorsa aradaki noktayı atla
                    if (dx1 * dy2 == dy1 * dx2 && (dx1 * dx2 >= 0) && (dy1 * dy2 >= 0))
                        continue;

                    simplified.Add(gridPath[i]);
                }
                simplified.Add(gridPath[gridPath.Count - 1]);

                for (int i = 1; i < simplified.Count; i++)
                {
                    var c = simplified[i];
                    Vector3 p = m_GridFrame.ToWorld(c.Item1, c.Item2);
                    p.z = boardZ;

                    // Pano dışındaki dış hava boşluğunda yürürken köşelere sürtünmemesi için
                    // dışa doğru hafif konfor payı ver
                    if (c.Item1 < minX) p.x -= pitch * 0.25f;
                    else if (c.Item1 > maxX) p.x += pitch * 0.25f;
                    if (c.Item2 > maxY) p.y += pitch * 0.25f;

                    rawWpts.Add(p);
                }
            }
            else
            {
                // Dış kontura doğru yönlen
                bool goLeft = boatEntrance.x < startPos.x;
                float flankX = goLeft ? (m_BoardMinX - pitch * 1.2f) : (m_BoardMaxX + pitch * 1.2f);
                float bottomY = m_BoardBottomY - pitch * 0.6f;
                rawWpts.Add(new Vector3(flankX, startPos.y, boardZ));
                rawWpts.Add(new Vector3(flankX, bottomY, boardZ));
            }

            Vector3 boardExit = rawWpts[rawWpts.Count - 1];

            // 2. AŞAMA (KUMSAL KORİDORU): Pano altındaki açık kumsalda gemiye yürüyüş
            float lowestCubeY = float.MaxValue;
            if (gridMap != null)
            {
                foreach (var kvp in gridMap)
                {
                    PixelCube c = kvp.Value;
                    if (c != null && !c.IsPopped && c.gameObject.activeSelf)
                    {
                        if (c.transform.position.y < lowestCubeY) lowestCubeY = c.transform.position.y;
                    }
                }
            }
            if (lowestCubeY > 1e4f) lowestCubeY = m_BoardBottomY;

            float exitX = boardExit.x;
            float targetX = boatEntrance.x;
            float targetY = boatEntrance.y;

            // Kumsal koridor Y seviyesi: Her zaman TÜM piksel küplerinin ve pano sınırının kesinlikle altında olmalı!
            float safeCorridorY = Mathf.Min(boardExit.y - pitch * 0.45f, lowestCubeY - clearance);
            safeCorridorY = Mathf.Min(safeCorridorY, m_BoardBottomY - clearance);
            safeCorridorY = Mathf.Max(safeCorridorY, targetY + pitch * 0.50f);
            safeCorridorY = Mathf.Min(safeCorridorY, lowestCubeY - pitch * 0.50f);

            // A) Zemine iniş geçiş noktası: Z düzlemi boardZ'den groundZ'ye geçer
            Vector3 pDrop = new Vector3(exitX, safeCorridorY, groundZ);
            rawWpts.Add(pDrop);

            // B) Kumsal boyunca gemiye doğru pürüzsüz ve akıcı kavis (Smooth Diagonal Flow):
            // Robotik 90° dik köşeli düz yürüyüş yerine, kumsalın tamamen boş ve açık alanını
            // kullanarak gemi girişine doğru tatlı bir kavisle süzülür.
            float deltaX = targetX - exitX;
            if (Mathf.Abs(deltaX) > pitch * 0.25f)
            {
                Vector3 pMid1 = new Vector3(
                    exitX + deltaX * 0.28f,
                    Mathf.Lerp(safeCorridorY, targetY, 0.16f),
                    groundZ);
                Vector3 pMid2 = new Vector3(
                    exitX + deltaX * 0.65f,
                    Mathf.Lerp(safeCorridorY, targetY, 0.48f),
                    groundZ);
                rawWpts.Add(pMid1);
                rawWpts.Add(pMid2);
            }

            // C) Gemi girişine yaklaşma
            float approachY = Mathf.Lerp(safeCorridorY, targetY, 0.80f);
            float approachX = targetX + (isLeft ? -pitch * 0.08f : pitch * 0.08f);
            Vector3 pApproach = new Vector3(approachX, approachY, groundZ);
            rawWpts.Add(pApproach);

            // D) Gemi iskelesi giriş eşiği
            Vector3 pFinal = new Vector3(targetX, targetY, groundZ);
            rawWpts.Add(pFinal);

            // Birbirine çok yakın ardışık noktaları temizle
            var cleanWpts = new List<Vector3>(rawWpts.Count);
            for (int i = 0; i < rawWpts.Count; i++)
            {
                if (cleanWpts.Count == 0 || (rawWpts[i] - cleanWpts[cleanWpts.Count - 1]).sqrMagnitude > 1e-5f)
                {
                    cleanWpts.Add(rawWpts[i]);
                }
            }
            if (cleanWpts.Count < 2)
            {
                cleanWpts.Add(pFinal);
            }

            // Köşeleri yumuşak ve doğal Bezier kavisle (Fillet) yuvarlatılmış akıcı yol oluştur:
            float cornerRadius = Mathf.Clamp(pitch * 1.10f, 0.16f, 0.32f);
            ShoreLanePath path = ShoreLanePath.BuildFilleted(cleanWpts, cornerRadius, 0.025f);
            boardExitDist = path.ClosestDistance(boardExit);
            return path;
        }

        /// <summary>
        /// Seçilen tüm küpleri kendi bağımsız yollarında, birbirini beklemeden ve konvoy/tren oluşturmadan
        /// doğrudan gemi iskelesine yürütür.
        /// </summary>
        private IEnumerator RunIndependentCubesToShip(
            List<PixelCube> cubes,
            List<ShoreLanePath> paths,
            List<float> boardExitDists,
            List<bool> entersLeftList,
            ShipController ship,
            Vector3 shoreTargetAtLaunch)
        {
            if (cubes == null || cubes.Count == 0) yield break;

            int count = cubes.Count;
            CubeMovementSettings s = MovementSettings;
            float pitch = Mathf.Max(0.12f, m_GridFrame.Pitch);
            float cruise = Mathf.Max(3.2f, EffectiveRopeSpeed());
            float liftHeight = pitch * s.LiftHeight;
            float arriveRadius = pitch * 0.35f;

            float rateScale = cruise / Mathf.Max(0.1f, s.MoveSpeed);
            float accelRate = s.Acceleration * rateScale;
            float decelRate = s.Deceleration * rateScale;
            float accelDuration = Mathf.Max(0.05f, s.MoveSpeed / Mathf.Max(0.1f, s.Acceleration));
            float arrivalDist = Mathf.Max(pitch, s.ArrivalDistance);

            var cargoObjects = new GameObject[count];
            var motions = new CubeMovementController[count];
            var speeds = new float[count];
            var cubeCruises = new float[count];
            var accelT = new float[count];
            var dists = new float[count];
            var arrived = new bool[count];
            float groundPlaneZ = BeachGroundZ();

            Camera cam = ShipController.MainCamera;
            Vector3 popUp = cam != null ? -cam.transform.forward : Vector3.back;
            Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;

            for (int k = 0; k < count; k++)
            {
                PixelCube cube = cubes[k];
                s_ReservedCubes.Add(cube);
                if (cube == null) continue;

                // 🔊 Küpler yerlerinden gemiye doğru ayrılırken hafif ve tatmin edici kalkış sesi (ASMR unstick pop)
                if (HypercasualFeedbackManager.Instance != null)
                {
                    HypercasualFeedbackManager.Instance.PlayCubeLiftoffFeedback(cube.transform.position, k);
                }

                // Doğal varyasyon: her küp kendi hafif hız varyasyonuna sahip (+-%4)
                float speedVar = UnityEngine.Random.Range(-s.SpeedVariation * 0.5f, s.SpeedVariation * 0.5f);
                cubeCruises[k] = cruise * (1f + speedVar);

                ICargoRunner selfRunner = cube.GetComponent<ICargoRunner>();
                if (m_CargoStandInPrefab != null)
                {
                    cargoObjects[k] = CreateStandInCargo(cube, k);
                    cube.SetPoppedVisualState(true, regenerateContourShadow: false);
                }
                else if (selfRunner != null)
                {
                    cube.BeginLeaving(regenerateContourShadow: false);
                    cargoObjects[k] = cube.gameObject;
                }
                else
                {
                    cargoObjects[k] = CreateRopeCargo(cube);
                    cube.SetPoppedVisualState(true, regenerateContourShadow: false);
                }

                GameObject cargo = cargoObjects[k];
                CubeMovementController motion = cargo.GetComponent<CubeMovementController>();
                if (motion == null) motion = cargo.AddComponent<CubeMovementController>();
                motion.CubeColor = cube.CurrentColor;

                Vector3 initialDir = (paths != null && k < paths.Count && paths[k] != null) 
                    ? paths[k].TangentAtDistance(0f) 
                    : Vector3.down;
                Vector3 toBoat = shoreTargetAtLaunch - cube.transform.position;
                toBoat.z = 0f;
                if (toBoat.sqrMagnitude > 1e-4f)
                {
                    // İlk kalkış yönlenmesinde hem yolun başlangıç yönünü hem de hedef gemiyi dikkate al
                    initialDir = (initialDir * 0.60f + toBoat.normalized * 0.40f).normalized;
                }
                motion.BeginRopeMotion(s, k, cubeCruises[k], liftHeight, initialDir);
                motion.SetGroundPlaneZ(groundPlaneZ);
                motions[k] = motion;

                dists[k] = 0f;
                speeds[k] = 0f;
                accelT[k] = 0f;
                arrived[k] = false;
            }

            int onPath = count;

            while (onPath > 0)
            {
                float dt = Time.deltaTime;
                if (dt <= 0f) { yield return null; continue; }

                Vector3 shoreNow = ship != null ? GetShorePoint(ship) : shoreTargetAtLaunch;
                Vector3 shoreShift = shoreNow - shoreTargetAtLaunch;

                for (int k = 0; k < count; k++)
                {
                    if (arrived[k]) continue;

                    GameObject cargo = cargoObjects[k];
                    if (cargo == null)
                    {
                        arrived[k] = true;
                        onPath--;
                        continue;
                    }

                    ShoreLanePath path = (paths != null && k < paths.Count) ? paths[k] : null;
                    if (path == null)
                    {
                        arrived[k] = true;
                        onPath--;
                        continue;
                    }

                    // Her küp tamamen bağımsız olarak hedefine ilerler
                    float remaining = Mathf.Max(0f, path.Length - dists[k]);
                    float speedFactor = HeadSpeedFactor(s, ref accelT[k], accelDuration, remaining, arrivalDist, dt);
                    float desiredSpeed = cubeCruises[k] * speedFactor;

                    speeds[k] = Mathf.MoveTowards(speeds[k], desiredSpeed, (desiredSpeed > speeds[k] ? accelRate : decelRate) * dt);
                    dists[k] += speeds[k] * dt;

                    float exitDist = (boardExitDists != null && k < boardExitDists.Count) ? boardExitDists[k] : 0f;
                    float w = path.Length > 1e-4f
                        ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dists[k] - exitDist) / Mathf.Max(0.5f, path.Length - exitDist)))
                        : 1f;

                    Vector3 basePos = path.PointAtDistance(dists[k]) + shoreShift * w;
                    motions[k].ApplyRopeFrame(basePos, 0f, popUp, camUp, dt);

                    if (dists[k] >= path.Length - arriveRadius)
                    {
                        arrived[k] = true;
                        onPath--;
                        bool entersLeft = (entersLeftList != null && k < entersLeftList.Count) ? entersLeftList[k] : false;
                        StartCoroutine(HopCargoToShip(cargo, ship, entersLeft, cubes[k], motions[k]));
                    }
                }

                yield return null;
            }
        }

        /// <summary>
        /// Baş küpün hız oranı: kalkışta AccelerationCurve ile hızlanır, hedefe Arrival Distance
        /// kadar kala DecelerationCurve ile ArrivalSlowdown oranına yavaşlar (asla sıfıra inmez;
        /// son yerleşmeyi binme animasyonu yapar).
        /// </summary>
        private static float HeadSpeedFactor(CubeMovementSettings s, ref float accelT, float accelDuration,
                                             float remaining, float arrivalDist, float dt)
        {
            accelT = Mathf.Min(1f, accelT + dt / accelDuration);
            float accelFactor = s.AccelerationCurve != null && s.AccelerationCurve.length > 0
                ? Mathf.Clamp01(s.AccelerationCurve.Evaluate(accelT))
                : Mathf.SmoothStep(0.15f, 1f, accelT);

            float decelFactor = 1f;
            if (remaining < arrivalDist)
            {
                float r = Mathf.Clamp01(remaining / arrivalDist);
                float c = s.DecelerationCurve != null && s.DecelerationCurve.length > 0
                    ? Mathf.Clamp01(s.DecelerationCurve.Evaluate(r))
                    : Mathf.Pow(r, s.ArrivalEase);
                decelFactor = Mathf.Lerp(s.ArrivalSlowdown, 1f, c);
            }
            return Mathf.Min(accelFactor, decelFactor);
        }

        private static MaterialPropertyBlock s_CargoColorBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// Küpün yerine yürüyen "dublör" prefab: küpün yeri, açısı, boyutu ve rengiyle doğar.
        /// Animator'ı varsa yan yana küpler ters fazda koşar.
        /// </summary>
        private GameObject CreateStandInCargo(PixelCube cube, int indexInRope)
        {
            GameObject cargo = Instantiate(m_CargoStandInPrefab, cube.transform.position, cube.transform.rotation);
            cargo.name = "RopeCargoCube";
            cargo.transform.localScale = cube.transform.lossyScale;

            if (s_CargoColorBlock == null) s_CargoColorBlock = new MaterialPropertyBlock();
            s_CargoColorBlock.Clear();
            s_CargoColorBlock.SetColor(BaseColorId, cube.CurrentColor);
            s_CargoColorBlock.SetColor(ColorId, cube.CurrentColor);
            s_CargoColorBlock.SetColor(EmissionColorId, Color.black);
            foreach (Renderer r in cargo.GetComponentsInChildren<Renderer>(true))
            {
                r.SetPropertyBlock(s_CargoColorBlock);
            }

            Animator animator = cargo.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Play(0, 0, (indexInRope % 2) * 0.5f);
            }

            return cargo;
        }

        /// <summary>
        /// Bacaksız bir küp prefab'ı kullanılıyorsa küpün yerine yürüyen yedek görsel
        /// (küp yerinde gizlenir, bu kopya kayarak gider).
        /// </summary>
        private static GameObject CreateRopeCargo(PixelCube cube)
        {
            GameObject cargo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cargo.name = "RopeCargoCube";
            cargo.transform.position = cube.transform.position;
            cargo.transform.rotation = cube.transform.rotation;
            cargo.transform.localScale = cube.transform.lossyScale;

            Collider c = cargo.GetComponent<Collider>();
            if (c != null) Destroy(c);

            MeshRenderer mr = cargo.GetComponent<MeshRenderer>();
            MeshRenderer srcMr = cube.GetComponent<MeshRenderer>();
            if (mr != null && srcMr != null)
            {
                mr.sharedMaterial = srcMr.sharedMaterial;
                MaterialPropertyBlock b = new MaterialPropertyBlock();
                srcMr.GetPropertyBlock(b);
                mr.SetPropertyBlock(b);
            }

            return cargo;
        }

        /// <summary>
        /// Gemi girişine gelen küp, referanstaki arabaya giren küpler gibi gemiye kayarak biner:
        /// zıplamadan, girişteki hızını ve yönünü koruyan alçak bir eğriyle güverteye yanaşır,
        /// yavaşlar, gövdesini düzeltir, minik bir yaylanmayla oturur ve güverteye batarak kaybolur.
        /// Move Into Ship → Align → Slight Ease → Settle.
        /// </summary>
        private IEnumerator HopCargoToShip(GameObject cargo, ShipController ship, bool fromLeft, PixelCube sourceCube, CubeMovementController controller = null)
        {
            if (cargo == null) yield break;

            CubeMovementSettings s = MovementSettings;
            ICargoRunner runner = cargo.GetComponent<ICargoRunner>();
            WaddleRunner waddle = cargo.GetComponent<WaddleRunner>();
            WalkingCargoVisual visual = cargo.GetComponent<WalkingCargoVisual>();

            // Araç güverte iniş hedefi:
            Vector3 deckOffset = new Vector3(fromLeft ? -0.06f : 0.06f, 0.16f, 0.02f);

            Camera mainCam = ShipController.MainCamera;
            Vector3 arcUp = mainCam != null ? mainCam.transform.up : Vector3.up;

            Vector3 start = cargo.transform.position;
            Vector3 baseScale = controller != null ? controller.BaseScale : cargo.transform.localScale;
            Vector3 entryDir = controller != null ? controller.MoveDirection : Vector3.down;
            float worldSize = Mathf.Max(0.01f, cargo.transform.lossyScale.y);
            float duration = Mathf.Max(0.08f, s.BoardingDuration + UnityEngine.Random.Range(0f, s.ArrivalVariation));

            // 1. Move Into Ship → Align → Slight Ease
            float elapsed = 0f;
            Vector3 prevPos = start;
            while (elapsed < duration && cargo != null)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease-out: girişteki hızla devam eder, güverteye yaklaşırken yumuşakça yavaşlar
                float e = 1f - (1f - t) * (1f - t);

                // Hedef pozisyon (araç hareket ediyorsa dinamik takip):
                Vector3 target = ship != null ? ship.transform.position + deckOffset : start;
                float span = (target - start).magnitude;
                // Kontrol noktası girişteki hareket yönünde: ani yön değişimi yok
                Vector3 ctrl = start + entryDir * (span * 0.45f) + arcUp * s.BoardingArcHeight;
                Vector3 p = QuadraticBezier(start, ctrl, target, e);
                cargo.transform.position = p;

                // Kıyıdan ayrılırken bacaklar toplanır
                if (t > 0.35f)
                {
                    if (waddle != null) waddle.IsAirborne = true;
                    if (visual != null) visual.SetAirborne(dt);
                }

                // Önce eğriyi takip et, son kısımda düz duruşa hizalan; viraj yatması söner
                Vector3 move = p - prevPos;
                prevPos = p;
                Vector3 face = t < 0.6f ? move : Vector3.down;
                if (controller != null) controller.SteerToward(face, 0f, dt);
                else if (runner != null) runner.TurnToward(face, dt);

                // Son kısımda oturmaya hazırlık: çok hafif basılma
                float squ = t > 0.7f ? Mathf.Sin((t - 0.7f) / 0.3f * Mathf.PI * 0.5f) * s.SquashAmount * 0.5f : 0f;
                cargo.transform.localScale = SquashScale(baseScale, squ);

                yield return null;
            }

            // Küp güverteye değdiği an sayılır: sayı, su/gemi tepkisiyle birlikte hemen düşer.
            // (Eskiden yaylanma + batma animasyonu da bittikten sonra sayılıyordu; sayı ~0.5 sn+ geç düşüyordu.)
            if (ship != null)
            {
                ship.AddCargo(1);
                ship.TriggerWaterDipImpact(0.12f, 0.35f);
                ShipController.SpawnWaterRipple(ship.transform.position + new Vector3(0f, -0.05f, 0.05f), 0.28f, 0.95f, 0.45f);
                HypercasualWaterController.TriggerWaterRipple(ship.transform.position, 0.70f, 0.25f);

                // ✨ Sparkle, 🔊 Tatmin Edici Melodik Chime, 📳 Hafif Mobil Titreşim
                Color cubeColor = sourceCube != null ? sourceCube.TrueColor : Color.white;
                bool isShipFull = ship.IsFull;
                HypercasualFeedbackManager.Instance.PlayCubeBoardFeedback(cargo.transform.position, cubeColor, ship.CurrentCargo, isShipFull);
            }

            // 2. Settle: sönümlü minik yaylanma, sonra güverteye ease-in ile batarak kaybolma (snap yok)
            float settleDur = Mathf.Max(0.06f, s.SettleDuration);
            float sinkDur = settleDur;
            float st = 0f;
            while (st < settleDur + sinkDur && cargo != null)
            {
                float dt = Time.deltaTime;
                st += dt;
                Vector3 deck = ship != null ? ship.transform.position + deckOffset : cargo.transform.position;

                if (st < settleDur)
                {
                    float u = st / settleDur;
                    float bounce = Mathf.Sin(u * Mathf.PI) * s.SettleBounce * (1f - u);
                    // Basılı başlar (önceki fazla sürekli), sekip normale döner
                    float squash = Mathf.Cos(u * Mathf.PI * 2f) * s.SquashAmount * 0.5f * (1f - u);
                    cargo.transform.position = deck + arcUp * bounce;
                    cargo.transform.localScale = SquashScale(baseScale, squash);
                }
                else
                {
                    float u = Mathf.Clamp01((st - settleDur) / sinkDur);
                    float ease = u * u;
                    cargo.transform.position = deck - arcUp * (worldSize * 0.35f * ease);
                    // Ölçek asla 0'a inmez: sıfır ölçekte çocuk objelere dünya konumu atanınca
                    // Unity NaN üretir ('IsFinite(distanceAlongView)' hatası).
                    cargo.transform.localScale = baseScale * Mathf.Max(0.15f, 1f - ease);
                }

                if (controller != null) controller.SteerToward(Vector3.down, 0f, dt);
                yield return null;
            }

            // Havuza dönen küp sonraki seviyede bozuk ölçekle doğmasın
            if (cargo != null) cargo.transform.localScale = baseScale;

            if (cargo != null)
            {
                if (sourceCube != null && cargo == sourceCube.gameObject)
                {
                    if (waddle != null) waddle.IsAirborne = false;
                    sourceCube.FinishLeaving();
                }
                else
                {
                    Destroy(cargo);
                }
            }
            if (sourceCube != null) s_ReservedCubes.Remove(sourceCube);

            m_ActiveCargoFlightCount = Mathf.Max(0, m_ActiveCargoFlightCount - 1);

            CheckWinCondition();
            TriggerWaitingShipsCheck();
        }

        private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        /// <summary>Hacmi koruyan minimal squash: pozitif değer Y'de basar, X/Z'de genişletir.</summary>
        private static Vector3 SquashScale(Vector3 baseScale, float squash)
        {
            return new Vector3(baseScale.x * (1f + squash * 0.5f), baseScale.y * (1f - squash), baseScale.z * (1f + squash * 0.5f));
        }

        private Vector3 ExitWorldPoint(PixelCube head, List<(int, int)> exitCells)
        {
            if (exitCells == null || exitCells.Count == 0) return head.transform.position;
            var last = exitCells[exitCells.Count - 1];
            return m_GridFrame.ToWorld(last.Item1, last.Item2);
        }

        /// <summary>
        /// Panodan gemi slotuna zemin üzerinde pürüzsüz yürüyüş rotası (Catmull-Rom kontrol noktaları).
        /// Referans videodaki gibi küpler pano altından masa/kumsal zeminine adım atar,
        /// zemin koridorundan akarak hedef geminin girişine kadar kesintisiz yürür.
        /// </summary>
        public static List<Vector3> BuildApproach(Vector3 exitPoint, Vector3 shoreCenter, Vector3 laneOffset)
        {
            Vector3 targetEntrance = shoreCenter + laneOffset;
            float totalDy = exitPoint.y - targetEntrance.y;
            if (totalDy <= 0.35f)
            {
                return new List<Vector3> { targetEntrance };
            }

            // 1. Pano Alt Sınırından Zemine İniş:
            // Küp panonun alt kenarından dışarı çıkar çıkmaz masa/kumsal zemin düzlemine basar
            float dropY = exitPoint.y - 0.22f;
            float dropZ = Mathf.Lerp(exitPoint.z, targetEntrance.z, 0.10f);
            Vector3 pDrop = new Vector3(exitPoint.x + laneOffset.x * 0.35f, dropY, dropZ);

            // 2. Zemin Koridoru (Orta Zemin):
            // Referans videodaki gibi iki kol zemin üzerinde tatlı bir kavisle gemi X hizasına doğru akar
            float midY = Mathf.Lerp(dropY, targetEntrance.y + 0.30f, 0.46f);
            float midX = Mathf.Lerp(exitPoint.x, targetEntrance.x, 0.55f) + laneOffset.x * 0.85f;
            float midZ = Mathf.Lerp(exitPoint.z, targetEntrance.z, 0.50f);
            Vector3 pMid = new Vector3(midX, midY, midZ);

            // 3. Giriş Öncesi Hizalanma:
            // Geminin pruva çizgisi hizasında düzelerek doğrudan slot/güverte ağzına yönelir
            float appY = targetEntrance.y + 0.22f;
            float appX = Mathf.Lerp(exitPoint.x, targetEntrance.x, 0.90f) + laneOffset.x * 0.55f;
            float appZ = Mathf.Lerp(exitPoint.z, targetEntrance.z, 0.85f);
            Vector3 pApproach = new Vector3(appX, appY, appZ);

            // 4. Son Varış (Gemi Ön Eşiği):
            Vector3 pFinal = targetEntrance;

            return new List<Vector3> { pDrop, pMid, pApproach, pFinal };
        }

        /// <summary>Geminin kıyıdaki/slotundaki sahil kenarı noktası (küplerin sahildeki yürüyüşünü tamamlayıp gemiye zıpladığı yer).</summary>
        [Tooltip("UI konteyner düzeninde denizcinin indiği nokta: pano çerçevesinin iç alt kenarından kaç küp yukarıda.")]
        [SerializeField, Range(0f, 3f)] private float m_UiShoreInsetCubes = 0.6f;

        /// <summary>
        /// UI konteyner (HUD Canvas) düzeni: Konteynerin slotundaki 3D zemin izdüşümü (bulunduğu yer).
        /// Denizciler hedefe giderken doğrudan buradan yürüyerek başlar, dönüşte de çerçeve kenarından
        /// buraya zıplayarak binerler.
        /// </summary>
        private bool TryGetUiContainerGroundPos(ShipController ship, out Vector3 groundPos)
        {
            groundPos = default;
            if (ship == null) return false;

            Camera cam = ShipController.MainCamera != null ? ShipController.MainCamera : Camera.main;
            if (cam == null) return false;

            float groundZ = BeachGroundZ();
            if (float.IsNaN(groundZ)) groundZ = m_ShoreZ;

            // Eğer konteyner slota bağlıysa veya slota kayma/yerleşme halindeyse, hedef slotun pozisyonunu baz al.
            // Bu sayede kayma animasyonu sırasında da varış noktası güncel hedef slotta sabit kalır.
            Transform targetAnchor = (ship.CurrentSlot != null) ? ship.CurrentSlot.transform : ship.transform;
            Vector3 baseWorldPos = targetAnchor.position;

            Vector3 targetPos = baseWorldPos;
            RectTransform rt = ship.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Konteynerin üst kapağı/ön kenarı hizasında zemine basması için hafif yukarı (+Y) alalım
                Vector3 topLocal = new Vector3(0f, rt.rect.height * 0.35f, 0f);
                Vector3 topWorld = rt.TransformPoint(topLocal);
                Vector3 offset = topWorld - ship.transform.position;
                targetPos = baseWorldPos + offset;
            }

            Canvas canvas = rt != null ? rt.GetComponentInParent<Canvas>(true) : null;
            Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null) ? canvas.worldCamera : cam;
            bool overlay = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay;
            Vector3 screen = overlay ? targetPos : uiCam.WorldToScreenPoint(targetPos);

            Ray ray = cam.ScreenPointToRay(screen);
            if (!new Plane(Vector3.back, new Vector3(0f, 0f, groundZ)).Raycast(ray, out float dist)) return false;

            Vector3 hit = ray.GetPoint(dist);
            groundPos = new Vector3(hit.x, hit.y, groundZ);
            return true;
        }

        public Vector3 GetShorePoint(ShipController ship)
        {
            if (ship != null && ship.IsUiContainer && TryGetUiContainerGroundPos(ship, out Vector3 uiGround))
            {
                return uiGround;
            }

            if (ship != null)
            {
                Vector3 shipPos = (ship.CurrentSlot != null) ? ship.CurrentSlot.transform.position : ship.transform.position;
                IList<Vector2> shoreline = (m_Shoreline != null && m_Shoreline.Count >= 2 && Mathf.Abs(m_Shoreline[m_Shoreline.Count / 2].y - s_DefaultShoreline[0].y) <= 0.25f)
                    ? (IList<Vector2>)m_Shoreline
                    : s_DefaultShoreline;

                // Kıyı çizgisinde gemiye en yakın nokta; biraz kum tarafına çekilir ki küpler suya basmasın
                Vector2 ship2 = new Vector2(shipPos.x, shipPos.y);
                Vector2 best = shoreline[0];
                float bestSq = float.MaxValue;
                for (int i = 0; i < shoreline.Count - 1; i++)
                {
                    Vector2 a = shoreline[i], b = shoreline[i + 1];
                    Vector2 ab = b - a;
                    float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(ship2 - a, ab) / ab.sqrMagnitude) : 0f;
                    Vector2 q = a + ab * t;
                    float sq = (q - ship2).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; best = q; }
                }
                Vector2 inward = best - ship2;
                float inset = (m_ShorelineInset > 0.001f && m_ShorelineInset <= 0.15f) ? m_ShorelineInset : 0.05f;
                if (inward.sqrMagnitude > 1e-6f) best += inward.normalized * inset;
                return new Vector3(best.x, best.y, 0.22f);
            }

            EnsureBoardBounds();
            float centerX = (m_BoardMinX + m_BoardMaxX) * 0.5f;
            return new Vector3(centerX, m_ShoreY + 0.95f, 0.22f);
        }

        /// <summary>Panodaki canlı (patlamamış) küplerin ızgarası ve dış hava hücreleri.</summary>
        /// <param name="ignoreReserved">true: başka bir koşucunun almaya gittiği (rezerve) küpler gitmiş sayılır.
        /// Art arda gönderilen aynı renk gemiler en alt sıra rezerve edilince kenarlara değil bir üst sıraya geçsin diye.</param>
        private bool TryBuildLiveGrid(out Dictionary<(int, int), PixelCube> gridMap, out HashSet<(int, int)> outsideAir, out int minY, bool ignoreReserved = false)
        {
            gridMap = new Dictionary<(int, int), PixelCube>();
            outsideAir = null;
            minY = 0;

            if (m_Generator == null) m_Generator = PixelArtGenerator.Instance;
            if (m_Generator == null || m_Generator.CubesContainer == null) return false;

            var allCubes = PixelCube.ActiveCubes;
            int minX = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            minY = int.MaxValue;
            foreach (var c in allCubes)
            {
                if (c == null || c.IsPopped || !c.gameObject.activeSelf) continue;
                if (ignoreReserved && s_ReservedCubes.Contains(c)) continue;
                gridMap[(c.GridX, c.GridY)] = c;
                if (c.GridX < minX) minX = c.GridX;
                if (c.GridX > maxX) maxX = c.GridX;
                if (c.GridY < minY) minY = c.GridY;
                if (c.GridY > maxY) maxY = c.GridY;
            }
            if (gridMap.Count == 0) return false;

            outsideAir = CalculateOutsideAir(gridMap, minX, maxX, minY, maxY);
            return true;
        }

        private bool EnsureGridFrame()
        {
            if (m_GridFrameValid) return true;
            if (m_Generator == null) m_Generator = PixelArtGenerator.Instance;
            if (m_Generator == null || m_Generator.CubesContainer == null) return false;

            // Patlamış küpler yerinde durur, ama yürüyerek ayrılanlar yerini terk etmiştir:
            // ızgarayı yerinde duranlardan çıkar.
            var inPlace = new List<PixelCube>();
            foreach (var c in m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>(true))
            {
                if (c != null && !c.IsLeaving) inPlace.Add(c);
            }
            m_GridFrameValid = CubeGridFrame.TryBuild(inPlace.ToArray(), out m_GridFrame);
            if (!m_GridFrameValid)
            {
                Debug.LogWarning("[ShipDispatcher] Küp ızgarası çıkarılamadı; kargo treni kurulamıyor.");
            }
            return m_GridFrameValid;
        }

        /// <summary>
        /// Boş olan en küçük indeksli (en soldaki: 1-2-3-4-5) slotu döner.
        /// Böylece her zaman 1 boşsa 1'e, 2 boşsa 2'ye dolar; arada boşluk kalmaz.
        /// </summary>
        public ShipSlot FindEmptySlot()
        {
            RefreshSlotListIfStale();
            if (m_Slots == null || m_Slots.Count == 0) return null;

            for (int i = 0; i < m_Slots.Count; i++)
            {
                var slot = m_Slots[i];
                if (slot != null && slot.IsEmpty)
                {
                    return slot;
                }
            }
            return null;
        }

        /// <summary>
        /// Kullanıcı talebi: Slotlar boşaldığında gemiler 1. slota doğru kaymaz;
        /// her gemi kendi yanaştığı slotta sabit kalır.
        /// </summary>
        public void CompactSlots(float delay = 0.12f)
        {
            CheckAutoPlaceRemainingShips();
        }

        private IEnumerator CompactSlotsRoutine(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            CheckAutoPlaceRemainingShips();
        }

        private void DetachShipFromQueueParent(ShipController s)
        {
            if (s == null) return;
            if (s.IsUiContainer)
            {
                // UI Konteyner: Canvas içinde tut, asla null'a (dünya uzayına) çıkarma!
                Transform root = s.transform.parent != null ? s.transform.parent.parent : null;
                if (root == null)
                {
                    Canvas c = s.GetComponentInParent<Canvas>();
                    if (c != null) root = c.transform;
                }
                if (root != null)
                {
                    s.transform.SetParent(root, true);
                    s.transform.SetAsLastSibling();
                }
            }
            else
            {
                s.transform.SetParent(null, true);
            }
        }

        /// <summary>
        /// Kuyruktan tıklanan gemiyi boş bir slota göndermeyi dener.
        /// Eğer gemi başka bir gemiye bağlıysa, her ikisi birden en ön sırada olmalı ve
        /// sahilde en az 2 boş slot bulunmalıdır; ikisi birlikte hareket eder.
        /// </summary>
        public bool TrySendShipFromQueue(ShipController ship)
        {
            if (m_IsAutoPlacing || m_IsLevelFailed) return false;
            if (ship == null || ship.IsDocked || ship.IsMoving || ship.IsDeparting || ship.IsQueueAnimating) return false;

            // 1. 🔗 Bağlı Gemi Kontrolü
            if (ship.IsLinked)
            {
                ShipController partner = ship.LinkedPartner;

                // Partner de en ön sırada mı?
                if (partner == null || !ship.CanDispatchLinked() || partner.IsQueueAnimating)
                {
                    ship.PlayWobble();
                    if (partner != null) partner.PlayWobble();
                    if (ship.Tether != null) ship.Tether.Rattle();
                    return false;
                }

                // 2 slotluk boş yer var mı? (Kural: 2 slot kaplayacaklar, 2 slotluk yer yoksa tıklanmaz)
                List<ShipSlot> emptySlots = GetEmptySlots();
                if (emptySlots == null || emptySlots.Count < 2)
                {
                    ship.PlayDenialFeedback();
                    if (partner != null) partner.PlayDenialFeedback();
                    if (ship.Tether != null) ship.Tether.Rattle();
                    TriggerFullSlotsAlertFeedback();
                    return false;
                }

                // İki bitişik slot tahsis et veya ortada kalan tekli gemiyi kaydırarak yan yana iki slot oluştur
                if (!TryGetOrMakeAdjacentSlotsForLinkedPair(null, out ShipSlot slotA, out ShipSlot slotB))
                {
                    ship.PlayDenialFeedback();
                    if (partner != null) partner.PlayDenialFeedback();
                    if (ship.Tether != null) ship.Tether.Rattle();
                    TriggerFullSlotsAlertFeedback();
                    return false;
                }

                // Gemileri önce kuyruk ebeveyninden ayır ki arkadaki gemiler öne kayarken ebeveyn çakışması olmasın
                DetachShipFromQueueParent(ship);
                DetachShipFromQueueParent(partner);

                // Her iki gemiyi de kuyruk sisteminden çıkar (alt alta olsalar dahi çift sıra kaydırmayı kusursuz işletir)
                if (m_QueuePool != null)
                {
                    m_QueuePool.OnLinkedShipsDispatched(ship, partner);
                }

                m_PlayerSentShipCount++;

                // ⚓ Halatlı gemilerin sol-sağ yön tutarlılığı:
                // Gemiler yan yana duruyorsa, oyuncu sağdaki veya soldaki gemiye tıklasa dahi
                // her zaman soldaki gemi soldaki slota, sağdaki gemi sağdaki slota yanaşmalıdır.
                // Böylece sağdaki gemiye basıldığında gemiler birbirlerinin önünü kesmez,
                // halat ters dönmez ve sol-sağ oryantasyonu daima korunur.
                ShipSlot leftSlot = (slotA.transform.position.x <= slotB.transform.position.x) ? slotA : slotB;
                ShipSlot rightSlot = (slotA.transform.position.x <= slotB.transform.position.x) ? slotB : slotA;

                if (Mathf.Abs(ship.transform.position.x - partner.transform.position.x) > 0.15f)
                {
                    ShipController leftShip = (ship.transform.position.x <= partner.transform.position.x) ? ship : partner;
                    ShipController rightShip = (ship.transform.position.x <= partner.transform.position.x) ? partner : ship;

                    leftShip.SailToSlot(leftSlot);
                    rightShip.SailToSlot(rightSlot);
                }
                else
                {
                    // Alt alta (aynı sütun / tandem) bağlı gemilerde tıklanan gemi ilk slota, partneri ikinci slota gider
                    ship.SailToSlot(leftSlot);
                    partner.SailToSlot(rightSlot);
                }
                return true;
            }

            // 1b. Bağlı gemi ama partneri henüz kuyruğa gelmedi: tek başına gönderilemez
            if (ship.LinkId > 0)
            {
                ship.PlayWobble();
                if (ship.Tether != null) ship.Tether.Rattle();
                return false;
            }

            // 2. Normal Tekil Gemi Kontrolü
            // En ön sıra kontrolü
            if (m_QueuePool != null && !m_QueuePool.IsFrontRow(ship))
            {
                ship.PlayDenialFeedback();
                return false;
            }

            // Boş slot kontrolü (Slotlar tamamen doluysa!)
            ShipSlot emptySlot = FindEmptySlot();
            if (emptySlot == null)
            {
                ship.PlayDenialFeedback();
                TriggerFullSlotsAlertFeedback();
                return false;
            }

            // Gemiyi kuyruk ebeveyninden hemen ayır ki arkadaki gemi geldiğinde çakışmasın
            DetachShipFromQueueParent(ship);

            // Kuyruktan çıkar, arkadaki gemiyi öne kaydır ve açık denizden yenisini getir
            if (m_QueuePool != null)
            {
                m_QueuePool.OnFrontShipDispatched(ship);
            }

            m_PlayerSentShipCount++;
            ship.SailToSlot(emptySlot);
            return true;
        }

        /// <summary>
        /// Sahnedeki tüm aktif ve boş yanaşma slotlarını döner.
        /// </summary>
        /// <summary>
        /// Slot listesi, slot kökü pasifken (ör. yükleme ekranı sırasında bölüm yüklenince) toplanırsa boş kalıyor ve
        /// bir daha yenilenmiyordu: hiçbir gemi gönderilemiyordu. Liste boşsa ya da pasif slot içeriyorsa yeniden toplanır.
        /// </summary>
        private void RefreshSlotListIfStale()
        {
            bool stale = m_Slots == null || m_Slots.Count == 0;
            if (!stale)
            {
                for (int i = 0; i < m_Slots.Count; i++)
                {
                    if (m_Slots[i] == null || !m_Slots[i].gameObject.activeInHierarchy) { stale = true; break; }
                }
            }
            if (stale) EnsureReferences();
        }

        public List<ShipSlot> GetEmptySlots()
        {
            RefreshSlotListIfStale();
            List<ShipSlot> list = new List<ShipSlot>();
            if (m_Slots != null)
            {
                for (int i = 0; i < m_Slots.Count; i++)
                {
                    var s = m_Slots[i];
                    if (s != null && s.IsEmpty && s.gameObject.activeInHierarchy)
                    {
                        list.Add(s);
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// 2'li bağlı gemi için yanaşacak yan yana (bitişik) iki slot bulur.
        /// Eğer yan yana iki boş slot halihazırda varsa doğrudan onları döner.
        /// Eğer boş slotlar arada tekli bir gemi kaldığı için birbirinden ayrıysa (örn: Slot 0 boş, Slot 1 tekli, Slot 2 boş),
        /// ortada kalan tekli gemiyi boş slota doğru 1 slot kaydırır; böylece 2'li gemiler yan yana yerleşir ve
        /// halat/köprü ortadaki geminin üzerinden geçmez.
        /// </summary>
        public bool TryGetOrMakeAdjacentSlotsForLinkedPair(ShipSlot preferredSlot, out ShipSlot slotA, out ShipSlot slotB)
        {
            slotA = null;
            slotB = null;

            RefreshSlotListIfStale();
            if (m_Slots == null || m_Slots.Count < 2) return false;

            // Aktif slotları soldan sağa sıralı olarak filtrele
            List<ShipSlot> activeSlots = new List<ShipSlot>();
            for (int i = 0; i < m_Slots.Count; i++)
            {
                var s = m_Slots[i];
                if (s != null && s.gameObject.activeInHierarchy) activeSlots.Add(s);
            }
            if (activeSlots.Count < 2) return false;

            activeSlots.Sort((a, b) =>
            {
                if (a.SlotIndex != b.SlotIndex) return a.SlotIndex.CompareTo(b.SlotIndex);
                return a.transform.position.x.CompareTo(b.transform.position.x);
            });

            // Boş slot sayısı 2'den azsa bağlı gemi yerleşemez
            int emptyCount = 0;
            for (int i = 0; i < activeSlots.Count; i++)
            {
                if (activeSlots[i].IsEmpty) emptyCount++;
            }
            if (emptyCount < 2) return false;

            // 1. Halihazırda yan yana (bitişik) boş olan slot çifti var mı?
            List<(ShipSlot sA, ShipSlot sB)> directPairs = new List<(ShipSlot, ShipSlot)>();
            for (int i = 0; i < activeSlots.Count - 1; i++)
            {
                ShipSlot s1 = activeSlots[i];
                ShipSlot s2 = activeSlots[i + 1];
                if (s1 != null && s2 != null && s1.IsEmpty && s2.IsEmpty)
                {
                    directPairs.Add((s1, s2));
                }
            }

            if (directPairs.Count > 0)
            {
                var chosen = directPairs[0];
                if (preferredSlot != null)
                {
                    float bestDist = float.MaxValue;
                    for (int i = 0; i < directPairs.Count; i++)
                    {
                        var pair = directPairs[i];
                        if (pair.sA == preferredSlot || pair.sB == preferredSlot)
                        {
                            chosen = pair;
                            break;
                        }
                        float d1 = Vector3.Distance(preferredSlot.transform.position, pair.sA.transform.position);
                        float d2 = Vector3.Distance(preferredSlot.transform.position, pair.sB.transform.position);
                        float minD = Mathf.Min(d1, d2);
                        if (minD < bestDist)
                        {
                            bestDist = minD;
                            chosen = pair;
                        }
                    }
                }

                slotA = chosen.sA;
                slotB = chosen.sB;
                return true;
            }

            // 2. Yan yana boş slot yok; arada tekli gemi(ler) kalmış.
            // Tekli gemiyi 1 slot kaydırarak 2 bitişik boş slot oluşturabilir miyiz?
            if (TryFindShiftSolution(activeSlots, preferredSlot, out List<(ShipController ship, ShipSlot targetSlot)> shifts, out slotA, out slotB))
            {
                // Kaydırmaları hemen işlet
                if (shifts != null && shifts.Count > 0)
                {
                    for (int s = 0; s < shifts.Count; s++)
                    {
                        var shift = shifts[s];
                        if (shift.ship != null && shift.targetSlot != null)
                        {
                            // Tekli gemiyi yeni boş slota tatlı bir zıplama/kayma süresiyle (0.22s) geçir
                            shift.ship.SailToSlot(shift.targetSlot, 0.22f);
                        }
                    }
                }
                return true;
            }

            return false;
        }

        private bool TryFindShiftSolution(
            List<ShipSlot> activeSlots,
            ShipSlot preferredSlot,
            out List<(ShipController ship, ShipSlot targetSlot)> shifts,
            out ShipSlot slotA,
            out ShipSlot slotB)
        {
            shifts = new List<(ShipController, ShipSlot)>();
            slotA = null;
            slotB = null;

            int n = activeSlots.Count;
            ShipController[] currentOccupants = new ShipController[n];
            for (int i = 0; i < n; i++)
            {
                currentOccupants[i] = activeSlots[i].DockedShip;
            }

            // Aşama 1: TEK BİR TEKLİ GEMİYİ 1 SLOT KAYDIRARAK ÇÖZME (1-shift)
            int bestShiftShipIndex = -1;
            int bestShiftTargetIndex = -1;
            int bestResultEmptyIndexA = -1;
            int bestResultEmptyIndexB = -1;
            float bestScore = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                ShipController docked = currentOccupants[i];
                if (docked == null) continue;
                if (docked.IsLinked || docked.IsDeparting) continue; // Yalnızca tekli ve ayrılmayan gemiler kaydırılabilir

                // i'deki gemi sola (i - 1) kaysın
                if (i - 1 >= 0 && currentOccupants[i - 1] == null)
                {
                    if (i + 1 < n && currentOccupants[i + 1] == null)
                    {
                        // (i, i + 1) boş olur
                        float score = CalculateShiftScore(activeSlots, i, i - 1, i, i + 1, preferredSlot);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestShiftShipIndex = i;
                            bestShiftTargetIndex = i - 1;
                            bestResultEmptyIndexA = i;
                            bestResultEmptyIndexB = i + 1;
                        }
                    }
                }

                // i'deki gemi sağa (i + 1) kaysın
                if (i + 1 < n && currentOccupants[i + 1] == null)
                {
                    if (i - 1 >= 0 && currentOccupants[i - 1] == null)
                    {
                        // (i - 1, i) boş olur
                        float score = CalculateShiftScore(activeSlots, i, i + 1, i - 1, i, preferredSlot);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestShiftShipIndex = i;
                            bestShiftTargetIndex = i + 1;
                            bestResultEmptyIndexA = i - 1;
                            bestResultEmptyIndexB = i;
                        }
                    }
                }
            }

            if (bestShiftShipIndex >= 0)
            {
                shifts.Add((currentOccupants[bestShiftShipIndex], activeSlots[bestShiftTargetIndex]));
                slotA = activeSlots[bestResultEmptyIndexA];
                slotB = activeSlots[bestResultEmptyIndexB];
                return true;
            }

            // Aşama 1.5: 2'Lİ BAĞLI BİR GEMİ ÇİFTİNİ 1 SLOT KAYDIRARAK ÇÖZME (Linked Pair Shift)
            // Örn: [Boş 0] [BağlıA 1] [BağlıB 2] [Boş 3] durumunda;
            // Bağlı gemiler sola kayarsa [BağlıA 0] [BağlıB 1] olur ve [Boş 2] [Boş 3] yan yana açılarak 
            // yeni gelecek 2'li bağlı gemiye yer açılır.
            int bestLinkedShiftPairIndex = -1;
            int bestLinkedShiftDirection = 0; // -1: sola, +1: sağa
            int bestLinkedEmptyA = -1;
            int bestLinkedEmptyB = -1;
            float bestLinkedScore = float.MaxValue;

            for (int i = 0; i < n - 1; i++)
            {
                ShipController shipA = currentOccupants[i];
                ShipController shipB = currentOccupants[i + 1];

                if (shipA == null || shipB == null) continue;
                if (!shipA.IsLinked || !shipB.IsLinked) continue;
                if (shipA.IsDeparting || shipB.IsDeparting) continue;
                if (shipA.LinkedPartner != shipB && shipA.LinkId != shipB.LinkId) continue;

                // 1) Sola kayma (i'deki shipA i-1'e, i+1'deki shipB i'ye)
                if (i - 1 >= 0 && currentOccupants[i - 1] == null)
                {
                    // Sağında boş slot kalırsa (i + 1) ve (i + 2) yan yana boş slot çifti oluşturur!
                    if (i + 2 < n && currentOccupants[i + 2] == null)
                    {
                        float score = CalculateShiftScore(activeSlots, i, i - 1, i + 1, i + 2, preferredSlot);
                        score -= 5f; // Sola toparlanma önceliği (kullanıcı tercihi)
                        if (score < bestLinkedScore)
                        {
                            bestLinkedScore = score;
                            bestLinkedShiftPairIndex = i;
                            bestLinkedShiftDirection = -1;
                            bestLinkedEmptyA = i + 1;
                            bestLinkedEmptyB = i + 2;
                        }
                    }
                }

                // 2) Sağa kayma (i+1'deki shipB i+2'ye, i'deki shipA i+1'e)
                if (i + 2 < n && currentOccupants[i + 2] == null)
                {
                    // Solunda boş slot kalırsa (i - 1) ve (i) yan yana boş slot çifti oluşturur!
                    if (i - 1 >= 0 && currentOccupants[i - 1] == null)
                    {
                        float score = CalculateShiftScore(activeSlots, i + 1, i + 2, i - 1, i, preferredSlot);
                        if (score < bestLinkedScore)
                        {
                            bestLinkedScore = score;
                            bestLinkedShiftPairIndex = i;
                            bestLinkedShiftDirection = 1;
                            bestLinkedEmptyA = i - 1;
                            bestLinkedEmptyB = i;
                        }
                    }
                }
            }

            if (bestLinkedShiftPairIndex >= 0)
            {
                int p = bestLinkedShiftPairIndex;
                ShipController shipA = currentOccupants[p];
                ShipController shipB = currentOccupants[p + 1];

                if (bestLinkedShiftDirection < 0)
                {
                    // Sola kayış: önce soldaki shipA p-1'e geçer (p boşalır), sonra shipB p'ye geçer
                    shifts.Add((shipA, activeSlots[p - 1]));
                    shifts.Add((shipB, activeSlots[p]));
                }
                else
                {
                    // Sağa kayış: önce sağdaki shipB p+2'ye geçer (p+1 boşalır), sonra shipA p+1'e geçer
                    shifts.Add((shipB, activeSlots[p + 2]));
                    shifts.Add((shipA, activeSlots[p + 1]));
                }

                slotA = activeSlots[bestLinkedEmptyA];
                slotB = activeSlots[bestLinkedEmptyB];
                return true;
            }

            // Aşama 2: Gerekirse 2 TEKLİ GEMİYİ SIKIŞTIRARAK (2-shift) ÇÖZME
            int[] dirCandidates = new int[] { -1, 1 };
            for (int i = 0; i < n; i++)
            {
                ShipController ship1 = currentOccupants[i];
                if (ship1 == null || ship1.IsLinked || ship1.IsDeparting) continue;

                foreach (int d1 in dirCandidates)
                {
                    int t1 = i + d1;
                    if (t1 < 0 || t1 >= n || currentOccupants[t1] != null) continue;

                    currentOccupants[i] = null;
                    currentOccupants[t1] = ship1;

                    for (int j = 0; j < n; j++)
                    {
                        if (j == t1) continue;
                        ShipController ship2 = currentOccupants[j];
                        if (ship2 == null || ship2.IsLinked || ship2.IsDeparting) continue;

                        foreach (int d2 in dirCandidates)
                        {
                            int t2 = j + d2;
                            if (t2 < 0 || t2 >= n || currentOccupants[t2] != null) continue;

                            currentOccupants[j] = null;
                            currentOccupants[t2] = ship2;

                            for (int k = 0; k < n - 1; k++)
                            {
                                if (currentOccupants[k] == null && currentOccupants[k + 1] == null)
                                {
                                    shifts.Add((ship1, activeSlots[t1]));
                                    shifts.Add((ship2, activeSlots[t2]));
                                    slotA = activeSlots[k];
                                    slotB = activeSlots[k + 1];
                                    return true;
                                }
                            }

                            currentOccupants[t2] = null;
                            currentOccupants[j] = ship2;
                        }
                    }

                    currentOccupants[t1] = null;
                    currentOccupants[i] = ship1;
                }
            }

            return false;
        }

        private float CalculateShiftScore(List<ShipSlot> activeSlots, int fromIndex, int toIndex, int emptyA, int emptyB, ShipSlot preferredSlot)
        {
            float score = 0f;
            if (preferredSlot != null)
            {
                bool hasPreferred = (activeSlots[emptyA] == preferredSlot || activeSlots[emptyB] == preferredSlot);
                if (hasPreferred) score -= 100f;
                else
                {
                    float d1 = Vector3.Distance(preferredSlot.transform.position, activeSlots[emptyA].transform.position);
                    float d2 = Vector3.Distance(preferredSlot.transform.position, activeSlots[emptyB].transform.position);
                    score += Mathf.Min(d1, d2);
                }
            }
            else
            {
                score += emptyA * 0.5f;
                if (toIndex < fromIndex) score -= 0.2f;
            }
            return score;
        }

        /// <summary>
        /// Slotlar doluyken oyuncu sıradaki gemiye tıkladığında yanaşma slotlarındaki gemilere ve suya uyarı dalgası yayar.
        /// </summary>
        private void TriggerFullSlotsAlertFeedback()
        {
            if (m_Slots == null) return;
            for (int i = 0; i < m_Slots.Count; i++)
            {
                var slot = m_Slots[i];
                if (slot != null && !slot.IsEmpty)
                {
                    slot.TriggerWaterDipImpact(-0.04f, 0.28f);
                }
            }
        }

        private bool IsAnyShipMoving()
        {
            var allShips = ShipController.ActiveShips;
            for (int i = 0; i < allShips.Count; i++)
            {
                var s = allShips[i];
                if (s != null && (s.IsMoving || s.IsDragging)) return true;
            }
            return false;
        }

        /// <summary>
        /// Seviyenin kesin olarak "oyun sonu" aşamasında olup olmadığını denetler:
        /// 1. Gelecekte spawn olacak başka gemi yok.
        /// 2. Tahtada gizemli (açılmamış soru işaretli) küp kalmamış.
        /// 3. Bekleyen tüm gemiler tahtadaki kalan renklere uygundur ("uygun gemiler").
        /// 4. Tahtada kalan tüm küplerin renkleri, bekleyen gemiler (+ slotlardaki boş kapasite) ile tamamen karşılanabiliyor.
        /// </summary>
        private bool IsEndOfGameWithSuitableShips(List<ShipController> waitingShips, List<ShipSlot> emptySlots)
        {
            if (waitingShips == null || waitingShips.Count == 0) return false;
            if (emptySlots == null || emptySlots.Count < waitingShips.Count) return false;

            // Tahtada kalan (henüz toplanmamış/rezerve edilmemiş) küpleri renk bazında say
            var remainingByColor = new Dictionary<Color, int>();
            int totalRemaining = 0;
            var activeCubes = PixelCube.ActiveCubes;
            if (activeCubes != null)
            {
                for (int i = 0; i < activeCubes.Count; i++)
                {
                    PixelCube c = activeCubes[i];
                    if (c != null && !c.IsPopped && c.gameObject.activeSelf && !c.IsLeaving && !s_ReservedCubes.Contains(c))
                    {
                        // Eğer henüz açılmamış soru işaretli küp varsa oyun sonu olamaz
                        if (c.IsMystery) return false;

                        Color col = c.CurrentColor;
                        remainingByColor.TryGetValue(col, out int count);
                        remainingByColor[col] = count + 1;
                        totalRemaining++;
                    }
                }
            }

            // Eğer tahtada hiç küp kalmadıysa otomatik yerleştirmeye gerek yok
            if (totalRemaining == 0) return false;

            // Slotlarda halihazırda yanaşmış ve daha fazla küp alabilecek gemilerin kapasitesi
            var capacityByColor = new Dictionary<Color, int>();
            if (m_Slots != null)
            {
                for (int i = 0; i < m_Slots.Count; i++)
                {
                    var slot = m_Slots[i];
                    if (slot != null && !slot.IsEmpty && slot.DockedShip != null)
                    {
                        ShipController docked = slot.DockedShip;
                        if (!docked.IsDeparting && docked.CanAcceptMore)
                        {
                            capacityByColor.TryGetValue(docked.ShipColor, out int cap);
                            capacityByColor[docked.ShipColor] = cap + docked.RemainingCapacity;
                        }
                    }
                }
            }

            // Bekleyen gemilerin her biri tahtada kalan bir renkle eşleşmeli ("uygun gemiler")
            for (int i = 0; i < waitingShips.Count; i++)
            {
                ShipController ship = waitingShips[i];
                if (ship == null) return false;

                bool matchesAnyRemaining = false;
                foreach (var col in remainingByColor.Keys)
                {
                    if (ColorsMatch(ship.ShipColor, col))
                    {
                        matchesAnyRemaining = true;
                        break;
                    }
                }

                if (!matchesAnyRemaining)
                {
                    // Bu gemi tahtada kalan hiçbir renkle eşleşmiyor; uygun gemi değil!
                    return false;
                }

                capacityByColor.TryGetValue(ship.ShipColor, out int cap);
                capacityByColor[ship.ShipColor] = cap + ship.RemainingCapacity;
            }

            // Tahtada kalan TÜM renklerin toplam küp sayısı, bu gemilerin kapasiteleriyle tamamen çözülebiliyor mu?
            foreach (var kvp in remainingByColor)
            {
                Color cubeColor = kvp.Key;
                int neededCount = kvp.Value;

                int availableCap = 0;
                foreach (var capKvp in capacityByColor)
                {
                    if (ColorsMatch(capKvp.Key, cubeColor))
                    {
                        availableCap += capKvp.Value;
                    }
                }

                if (availableCap < neededCount)
                {
                    // Bu rengi bitirecek yeterli gemi kapasitesi henüz yok! Oyun sonu değil.
                    return false;
                }
            }

            // Tüm kontroller geçti: Bu uygun gemiler yerleştiğinde seviyedeki TÜM küpler toplanacak ve oyun bitecek!
            return true;
        }

        /// <summary>
        /// Kalan gemilerin boş slotlara sığıp sığmadığını ve seviyeyi tamamen bitirecek
        /// "oyun sonu" durumu olup olmadığını kontrol eder.
        /// Kural: SADECE oyun sonu durumunda (gelecek başka gemi kalmamış, bekleyen tüm gemiler
        /// boş slotlara sığıyor ve tahtada kalan tüm küpleri bitirecek uygun gemiler olduğunda)
        /// gemiler otomatik yerleştirilir ve 2X turbo hıza geçilir.
        /// Oyun ortasında veya küpler normal gemilere geçerken ASLA 2x hızlanma yapılmaz.
        /// </summary>
        public void CheckAutoPlaceRemainingShips()
        {
            if (!m_EnableAutoPlaceAndTurbo || m_IsAutoPlacing || m_LevelEndPending || m_IsLevelFailed || !Application.isPlaying) return;
            if (m_QueuePool == null || m_Slots == null || m_Slots.Count == 0) return;

            // Bölüm bazında kapatılabilir (ör. ilk giriş bölümleri)
            PixelLevelData activeLevel = m_Generator != null ? m_Generator.ActiveLevelData : null;
            if (activeLevel != null && activeLevel.DisableAutoPlaceTurbo) return;

            // Oyun başlangıcında oyuncu en az bir gemi göndermeden otomatik yerleştirme çalışmaz
            if (m_PlayerSentShipCount == 0) return;

            // Eğer şu anda hareket eden veya sürüklenen herhangi bir gemi varsa bekle
            if (IsAnyShipMoving()) return;

            // Not: slotta küp toplayan gemi / yolda koşucu olması 2x'i engellemez. Koşucular sürekli aktif olduğu
            // için bu koşul 2x'i pratikte "slotlar tamamen boşken"e kısıtlıyordu.

            // Kuyrukta açık denizde veya sırada henüz gelmemiş başka gemi var mı?
            if (!m_QueuePool.HasNoMoreFutureShips()) return;

            // Şu an kuyrukta hazır bekleyen gemiler
            List<ShipController> waitingShips = m_QueuePool.GetActiveWaitingShips();
            int waitingCount = waitingShips.Count;
            if (waitingCount == 0) return;

            // Boş slotları al
            List<ShipSlot> emptySlots = GetEmptySlots();
            int emptyCount = emptySlots.Count;

            // Bekleyen tüm gemiler kalan boş slotlara sığabilmeli
            if (waitingCount > emptyCount) return;

            // Sadece ve sadece GERÇEK OYUN SONU ve UYGUN GEMİLER durumunda 2X otomatik yerleştirme yap:
            if (!IsEndOfGameWithSuitableShips(waitingShips, emptySlots)) return;

            StartCoroutine(AutoPlaceRemainingShipsRoutine(waitingShips, emptySlots));
        }

        private IEnumerator AutoPlaceRemainingShipsRoutine(List<ShipController> shipsToPlace, List<ShipSlot> targetSlots)
        {
            m_IsAutoPlacing = true;
            Debug.Log($"<color=#00FFAA><b>[ShipDispatcher]</b></color> ⚡ Otomatik Yerleştirme Devrede! Kalan {shipsToPlace.Count} gemi {targetSlots.Count} boş slota yerleştiriliyor.");

            // ⚡ Kullanıcı Kuralı: 2X butonu açıkken oyun sonunda sıradaki tüm gemiler için yerleşecek yer varsa 4X hız,
            // 2X butonu kapalıysa 2X hızında hemen yerleşsin
            if (CasualHudController.Instance != null)
            {
                CasualHudController.Instance.SetEndgameSpeedActive(true);
            }

            // Gemileri sırayla, tatlı bir aralıkla (0.10s) boş slotlara yolla
            for (int i = 0; i < shipsToPlace.Count; i++)
            {
                if (i >= targetSlots.Count) break;

                ShipController ship = shipsToPlace[i];
                ShipSlot slot = targetSlots[i];

                if (ship != null && slot != null && slot.IsEmpty)
                {
                    if (m_QueuePool != null)
                    {
                        m_QueuePool.RemoveShipFromQueue(ship);
                    }

                    DetachShipFromQueueParent(ship);
                    ship.SailToSlot(slot, 0.35f);
                }

                yield return new WaitForSeconds(0.10f);
            }

            m_IsAutoPlacing = false;
        }

        /// <summary>
        /// ⚡ Eski otomatik 2X fonksiyonu.
        /// Kullanıcı isteği: Diğer durumlar için olan 2x kaldırıldı. Sadece üst bardaki 2X butonu oyun hızını kontrol eder.
        /// </summary>
        public void SetTurboSpeed(bool active)
        {
            m_IsTurboActive = false;
        }

        /// <summary>
        /// Dış havaya açık (hemen toplanabilir) küplerin renk listesini döner.
        /// </summary>
        public List<Color> GetExposedLevelColors()
        {
            List<Color> exposedColors = new List<Color>();
            if (m_Generator == null) m_Generator = PixelArtGenerator.Instance;
            if (m_Generator == null || m_Generator.CubesContainer == null) return exposedColors;

            var allCubes = PixelCube.ActiveCubes;
            if (allCubes == null || allCubes.Count == 0) return exposedColors;

            Dictionary<(int, int), PixelCube> gridMap = new Dictionary<(int, int), PixelCube>(allCubes.Count);
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int i = 0; i < allCubes.Count; i++)
            {
                PixelCube c = allCubes[i];
                if (c != null && !c.IsPopped && c.gameObject.activeSelf)
                {
                    gridMap[(c.GridX, c.GridY)] = c;
                    if (c.GridX < minX) minX = c.GridX;
                    if (c.GridX > maxX) maxX = c.GridX;
                    if (c.GridY < minY) minY = c.GridY;
                    if (c.GridY > maxY) maxY = c.GridY;
                }
            }

            if (gridMap.Count == 0) return exposedColors;

            HashSet<(int, int)> outsideAir = CalculateOutsideAir(gridMap, minX, maxX, minY, maxY);

            Dictionary<Color, int> countMap = new Dictionary<Color, int>();
            foreach (var kvp in gridMap)
            {
                PixelCube cube = kvp.Value;
                if (cube == null || s_ReservedCubes.Contains(cube)) continue;

                int x = cube.GridX;
                int y = cube.GridY;
                bool touchesAir = outsideAir.Contains((x - 1, y)) ||
                                  outsideAir.Contains((x + 1, y)) ||
                                  outsideAir.Contains((x, y - 1)) ||
                                  outsideAir.Contains((x, y + 1));

                if (touchesAir)
                {
                    Color c = cube.CurrentColor != Color.clear ? cube.CurrentColor : cube.OriginalColor;
                    bool matched = false;
                    foreach (var key in countMap.Keys)
                    {
                        if (ColorsMatch(key, c))
                        {
                            countMap[key]++;
                            matched = true;
                            break;
                        }
                    }
                    if (!matched)
                    {
                        countMap[c] = 1;
                    }
                }
            }

            return new List<Color>(countMap.Keys);
        }

        /// <summary>
        /// Seviyedeki aktif henüz patlatılmamış küplerin renklerinden birini döndürür.
        /// preferExposed true ise öncelikle dışta (hemen toplanabilir) olan renklere öncelik verir.
        /// preferExposed false ise seviyede toplanacak TÜM renklerden (ör. yeşil, siyah, kahverengi vb.) seçim yapar.
        /// </summary>
        public static Color NormalizeShipColor(Color c)
        {
            // Küplerle gemilerin rengi birebir aynı olsun — hiçbir ton bozulmadan korunur
            return c;
        }

        public Color GetRemainingLevelColor(bool preferExposed = true)
        {
            // 1. İsteniyorsa önce DIŞTA (hemen toplanabilir) olan renklere öncelik ver!
            if (preferExposed)
            {
                var exposed = GetExposedLevelColors();
                if (exposed != null && exposed.Count > 0)
                {
                    return NormalizeShipColor(exposed[Random.Range(0, exposed.Count)]);
                }
            }

            if (m_Generator == null) m_Generator = PixelArtGenerator.Instance;

            if (m_Generator != null && m_Generator.CubesContainer != null)
            {
                var cubes = m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>(false);
                if (cubes != null && cubes.Length > 0)
                {
                    Dictionary<Color, int> colorCounts = new Dictionary<Color, int>();

                    foreach (var cube in cubes)
                    {
                        if (cube != null && !cube.IsPopped && cube.gameObject.activeSelf && !s_ReservedCubes.Contains(cube))
                        {
                            Color c = cube.CurrentColor != Color.clear ? cube.CurrentColor : cube.OriginalColor;
                            bool matched = false;
                            foreach (var key in colorCounts.Keys)
                            {
                                if (ColorsMatch(key, c))
                                {
                                    colorCounts[key]++;
                                    matched = true;
                                    break;
                                }
                            }
                            if (!matched)
                            {
                                colorCounts[c] = 1;
                            }
                        }
                    }

                    if (colorCounts.Count > 0)
                    {
                        List<Color> keys = new List<Color>(colorCounts.Keys);
                        return NormalizeShipColor(keys[Random.Range(0, keys.Count)]);
                    }
                }
            }

            // Fallback: Aktif bölümün paletinden renk çek (Asla alakasız rastgele renk üretmez!)
            PixelLevelData level = (m_Generator != null) ? m_Generator.ActiveLevelData : null;
            if (level == null && LevelManager.Instance != null) level = LevelManager.Instance.CurrentLevel;
            if (level == null)
            {
                LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
                if (lm != null) level = lm.CurrentLevel;
            }

            if (level != null && level.ColorPalette != null && level.ColorPalette.Count > 0)
            {
                var entry = level.ColorPalette[Random.Range(0, level.ColorPalette.Count)];
                return NormalizeShipColor(entry.targetColor != Color.clear ? entry.targetColor : entry.originalColor);
            }

            return Color.clear;
        }

        public int GetRemainingCountForColor(Color targetColor)
        {
            var activeCubes = PixelCube.ActiveCubes;
            if (activeCubes != null && activeCubes.Count > 0)
            {
                int count = 0;
                for (int i = 0; i < activeCubes.Count; i++)
                {
                    var cube = activeCubes[i];
                    if (cube != null && !cube.IsPopped && cube.gameObject.activeSelf && !s_ReservedCubes.Contains(cube))
                    {
                        if (ColorsMatch(cube.CurrentColor, targetColor) || ColorsMatch(cube.OriginalColor, targetColor))
                        {
                            count++;
                        }
                    }
                }
                // Tahta kuruluyken sayım kesindir: bu renk bittiyse 0 döner. (Eskiden 0'da palet sayısına
                // düşülüyordu; renk bitince gemi "hâlâ küp var" sanıp slotta sonsuza kadar bekliyordu.)
                return count;
            }

            // Fallback (tahta henüz kurulmamışken): Seviye paletinden piksel sayısını bul
            PixelLevelData level = (m_Generator != null) ? m_Generator.ActiveLevelData : null;
            if (level == null && LevelManager.Instance != null) level = LevelManager.Instance.CurrentLevel;
            if (level != null && level.ColorPalette != null)
            {
                foreach (var p in level.ColorPalette)
                {
                    if (ColorsMatch(p.targetColor, targetColor) || ColorsMatch(p.originalColor, targetColor))
                    {
                        return p.pixelCount;
                    }
                }
            }

            return 0;
        }

        public int GetTotalRemainingCubes()
        {
            var activeCubes = PixelCube.ActiveCubes;
            if (activeCubes != null && activeCubes.Count > 0)
            {
                int count = 0;
                for (int i = 0; i < activeCubes.Count; i++)
                {
                    var cube = activeCubes[i];
                    if (cube != null && !cube.IsPopped && cube.gameObject.activeSelf && !s_ReservedCubes.Contains(cube))
                    {
                        count++;
                    }
                }
                return count;
            }
            return 0;
        }

        /// <summary>
        /// Tablodaki tüm küpler patlatıldıysa seviye tamamlanma sürecini başlatır. Seviye geçişi
        /// hemen olmaz: son gemi de sahneyi gerçekten terk edene kadar beklenir (bkz. BeginLevelEndSequence).
        /// </summary>
        public void CheckWinCondition()
        {
            if (m_LevelEndPending) return;

            var activeCubes = PixelCube.ActiveCubes;
            int unpoppedCount = 0;

            if (activeCubes != null)
            {
                for (int i = 0; i < activeCubes.Count; i++)
                {
                    var cube = activeCubes[i];
                    if (cube != null && !cube.IsPopped && cube.gameObject.activeSelf)
                    {
                        unpoppedCount++;
                    }
                }
            }

            // Hâlâ havada (panodan gemiye uçuş animasyonu sürmekte olan) küp varsa bekle —
            // yoksa son küp koparılır kopartılmaz, o küp henüz gemiye TAM ulaşmadan gemiler
            // sahneyi animasyon yarıda kesilerek terk ediyordu.
            if (unpoppedCount == 0 && m_ActiveCargoFlightCount <= 0)
            {
                Debug.Log("<color=#00FFAA><b>[ShipDispatcher]</b></color> 🎉 Tüm piksel resmi tamamlandı! Son gemilerin sahneyi terk etmesi bekleniyor...");
                if (CasualHudController.Instance != null)
                {
                    CasualHudController.Instance.SetEndgameSpeedActive(true);
                }
                BeginLevelEndSequence();
            }
            else
            {
                CheckAutoPlaceRemainingShips();
            }
        }

        #region ❌ Deadlock & Seviye Başarısızlık Kontrolü

        private void UpdateDeadlockCheck()
        {
            if (!m_EnableFailOnDeadlock || m_LevelEndPending || m_IsLevelFailed)
            {
                m_DeadlockTimer = 0f;
                m_DeadlockCheckIntervalTimer = 0f;
                m_CachedDeadlockCondition = false;
                return;
            }

            // Boş slot varken oyuncu bir gemi gönderebiliyorsa deadlock imkansızdır; maliyetli kontrolleri yapma.
            // (Boş slot olsa bile tek seçenek 2 slot isteyen bağlı çiftse oyun kilitlenebilir — aşağıda kontrol edilir.)
            int emptySlotCount = CountEmptySlots();
            if (emptySlotCount > 0 && CanPlayerSendAnyShip(emptySlotCount))
            {
                m_DeadlockTimer = 0f;
                m_DeadlockCheckIntervalTimer = 0f;
                m_CachedDeadlockCondition = false;
                return;
            }

            // ⚡ Performans / Akıcılık:
            // Eğer havada taşınan kargo varsa, gemilerden biri küp çekiyorsa veya gemilerden biri hareket halindeyse,
            // oyun akışı devam etmektedir ve deadlock kesinlikle oluşamaz.
            // Bu anlarda her frame ağır BFS flood-fill ve bellek tahsisatı yapan CheckDeadlockCondition'ı çalıştırma!
            if (m_ActiveCargoFlightCount > 0 || (m_ActiveExtractingShips != null && m_ActiveExtractingShips.Count > 0) || IsAnyShipMoving())
            {
                m_DeadlockTimer = 0f;
                m_DeadlockCheckIntervalTimer = 0f;
                m_CachedDeadlockCondition = false;
                return;
            }

            m_DeadlockCheckIntervalTimer += Time.unscaledDeltaTime;
            if (m_DeadlockCheckIntervalTimer >= 0.5f)
            {
                m_DeadlockCheckIntervalTimer = 0f;
                m_CachedDeadlockCondition = CheckDeadlockCondition();
            }

            if (m_CachedDeadlockCondition)
            {
                m_DeadlockTimer += Time.unscaledDeltaTime;
                if (m_DeadlockTimer >= m_DeadlockGraceDuration)
                {
                    TriggerLevelFail();
                }
            }
            else
            {
                m_DeadlockTimer = 0f;
            }
        }

        private bool HasAnyEmptySlot()
        {
            if (m_Slots == null || m_Slots.Count == 0) return true;
            for (int i = 0; i < m_Slots.Count; i++)
            {
                var slot = m_Slots[i];
                if (slot == null || !slot.gameObject.activeInHierarchy) continue;
                if (slot.IsEmpty || slot.DockedShip == null) return true;
            }
            return false;
        }

        private bool HasAnyDockedShipExposedCube(List<ShipSlot> slots)
        {
            if (m_Generator == null) m_Generator = PixelArtGenerator.Instance;
            if (m_Generator == null || m_Generator.CubesContainer == null) return false;

            var activeCubes = PixelCube.ActiveCubes;
            if (activeCubes == null || activeCubes.Count == 0) return false;

            // Docked gemilerin kabul edebileceği renkleri topla
            List<Color> candidateColors = new List<Color>(slots != null ? slots.Count : 4);
            if (slots != null)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    var s = slots[i];
                    if (s != null && s.DockedShip != null && !s.DockedShip.IsDeparting && s.DockedShip.CanAcceptMore)
                    {
                        Color col = s.DockedShip.ShipColor;
                        bool alreadyAdded = false;
                        for (int c = 0; c < candidateColors.Count; c++)
                        {
                            if (ColorsMatch(candidateColors[c], col))
                            {
                                alreadyAdded = true;
                                break;
                            }
                        }
                        if (!alreadyAdded)
                        {
                            candidateColors.Add(col);
                        }
                    }
                }
            }

            if (candidateColors.Count == 0) return false;

            Dictionary<(int, int), PixelCube> gridMap = new Dictionary<(int, int), PixelCube>(activeCubes.Count);
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int i = 0; i < activeCubes.Count; i++)
            {
                PixelCube c = activeCubes[i];
                if (c != null && !c.IsPopped && c.gameObject.activeSelf)
                {
                    gridMap[(c.GridX, c.GridY)] = c;
                    if (c.GridX < minX) minX = c.GridX;
                    if (c.GridX > maxX) maxX = c.GridX;
                    if (c.GridY < minY) minY = c.GridY;
                    if (c.GridY > maxY) maxY = c.GridY;
                }
            }

            if (gridMap.Count == 0) return false;

            HashSet<(int, int)> outsideAir = CalculateOutsideAir(gridMap, minX, maxX, minY, maxY);

            foreach (var kvp in gridMap)
            {
                PixelCube cube = kvp.Value;
                if (cube == null || s_ReservedCubes.Contains(cube)) continue;

                for (int c = 0; c < candidateColors.Count; c++)
                {
                    if (ColorsMatch(cube.CurrentColor, candidateColors[c]) || ColorsMatch(cube.OriginalColor, candidateColors[c]))
                    {
                        int x = cube.GridX;
                        int y = cube.GridY;
                        bool touchesAir = outsideAir.Contains((x - 1, y)) ||
                                          outsideAir.Contains((x + 1, y)) ||
                                          outsideAir.Contains((x, y - 1)) ||
                                          outsideAir.Contains((x, y + 1));
                        if (touchesAir)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tüm slotlar dolduğunda ve hamle yapılamadığında true döner.
        /// Kullanıcı isteği: "slotların hepsi dolduğunda ve hamle yapılamadığında da level fail olacak"
        /// </summary>
        public bool CheckDeadlockCondition()
        {
            if (m_LevelEndPending || m_IsLevelFailed) return false;

            // 1. Slot referansları kontrolü: Herhangi bir slot boşsa oyuncu kuyruktan gemi gönderebilir -> fail DEĞİL
            if (m_Slots == null || m_Slots.Count == 0) return false;

            int activeSlotCount = 0;
            int emptySlots = 0;
            for (int i = 0; i < m_Slots.Count; i++)
            {
                var slot = m_Slots[i];
                if (slot == null || !slot.gameObject.activeInHierarchy) continue;

                activeSlotCount++;
                if (slot.IsEmpty || slot.DockedShip == null) emptySlots++;
            }

            if (activeSlotCount == 0) return false;

            // Boş slota gönderilebilecek bir gemi varsa fail DEĞİL. Yoksa (ör. tek boş slot var ama ön sıradaki
            // tek seçenek 2 slot isteyen bağlı çift) slotlar doluymuş gibi devam edilir; eskiden boş slot görünce
            // hiç fail verilmiyor, oyun sonsuza kadar kilitli kalıyordu.
            if (emptySlots > 0 && CanPlayerSendAnyShip(emptySlots)) return false;

            // 2. Tabloda küp kalmadıysa kazanılmıştır, fail olamaz
            int totalRemaining = GetTotalRemainingCubes();
            if (totalRemaining <= 0) return false;

            // 3. Havada uçuşan kargo var mı veya küp çekme coroutine'i çalışıyor mu?
            if (m_ActiveCargoFlightCount > 0) return false;
            m_ActiveExtractingShips.RemoveWhere(s => s == null);
            if (m_ActiveExtractingShips.Count > 0) return false;

            // 4. Sahnedeki gemilerden herhangi biri hareket halinde mi, sürükleniyor mu veya ayrılıyor mu?
            var allShips = ShipController.ActiveShips;
            for (int i = 0; i < allShips.Count; i++)
            {
                var ship = allShips[i];
                if (ship == null) continue;

                if (ship.IsMoving || ship.IsDragging || ship.IsDeparting || ship.HasPendingCargo)
                {
                    return false;
                }
            }

            // 5. Slotta yanaşık duran gemileri analiz et:
            // Herhangi bir gemi:
            // a) Tam kapasiteye ulaşmışsa -> kalkış yapacak, slot boşalacak -> fail DEĞİL
            // b) Tabloda o renkten hiç küp kalmamışsa -> zorunlu kalkış yapacak, slot boşalacak -> fail DEĞİL
            for (int i = 0; i < m_Slots.Count; i++)
            {
                var slot = m_Slots[i];
                if (slot == null || !slot.gameObject.activeInHierarchy) continue;

                ShipController ship = slot.DockedShip;
                if (ship == null) continue; // boş slot (yukarıda gönderilebilir gemi olmadığı görüldü)

                // Gemi şu anda kalkış yapabilecek durumdaysa (bağlıysa partneriyle birlikte kalkacaksa)
                // slot boşalacak demektir -> fail DEĞİL
                if (ship.CanDepartNow)
                {
                    return false;
                }

                // Dolu ama partnerini bekleyen bağlı gemi tek başına kalkıp slot boşaltamaz.
                // Partnerin veya başka gemilerin küp çekip çekemeyeceği aşağıda kontrol edilecek:
                if (ship.HasActiveLinkedPartner)
                {
                    continue;
                }

                if (ship.IsFull || !ship.CanAcceptMore)
                {
                    return false;
                }

                if (GetRemainingCountForColor(ship.ShipColor) == 0)
                {
                    return false;
                }
            }

            // c) Dış hatta (exposed) eşleşen küpü olan herhangi bir gemi var mı?
            // Tek bir birleşik dış hat taraması ile kontrol edilir.
            if (HasAnyDockedShipExposedCube(m_Slots))
            {
                return false;
            }

            // Gönderilebilir gemi yok VE hiçbir gemi hamle yapamıyor, küp çekemiyor, hareket edemiyor!
            return true;
        }

        private int CountEmptySlots()
        {
            if (m_Slots == null) return 0;
            int n = 0;
            for (int i = 0; i < m_Slots.Count; i++)
            {
                var slot = m_Slots[i];
                if (slot == null || !slot.gameObject.activeInHierarchy) continue;
                if (slot.IsEmpty || slot.DockedShip == null) n++;
            }
            return n;
        }

        /// <summary>
        /// Oyuncu şu an kuyruktan bir gemi gönderebilir mi? TrySendShipFromQueue ile aynı kurallar:
        /// tekil gemi ön sırada olmalı ve 1 boş slot ister; bağlı çift ikisi de gönderilebilir olmalı ve 2 boş slot ister.
        /// </summary>
        private bool CanPlayerSendAnyShip(int emptySlots)
        {
            if (emptySlots <= 0 || m_QueuePool == null) return false;
            var waiting = m_QueuePool.GetActiveWaitingShips();
            for (int i = 0; i < waiting.Count; i++)
            {
                var ship = waiting[i];
                if (ship == null || ship.IsDocked || ship.IsMoving || ship.IsDeparting) continue;
                if (ship.IsLinked)
                {
                    if (ship.LinkedPartner != null && ship.CanDispatchLinked() && emptySlots >= 2) return true;
                }
                else if (ship.LinkId == 0 && m_QueuePool.IsFrontRow(ship)) // partneri gelmemiş bağlı gemi gönderilemez
                {
                    return true;
                }
            }
            return false;
        }

        public void TriggerLevelFail()
        {
            if (m_IsLevelFailed || m_LevelEndPending) return;

            m_IsLevelFailed = true;
            m_DeadlockTimer = 0f;

            Debug.Log("<color=#FF3333><b>[ShipDispatcher]</b></color> ❌ SEVİYE BAŞARISIZ! Tüm slotlar dolu ve hamle yapılamıyor.");

            SetTurboSpeed(false);

            if (CasualHudController.Instance != null)
            {
                CasualHudController.Instance.ShowLevelFailPopup();
            }
            else
            {
                LandFlowLoadingScreen screen = LandFlowLoadingScreen.Instance ?? LandFlowLoadingScreen.EnsureInstance();
                if (screen != null)
                {
                    screen.ShowFailLevelAndRestart();
                }
            }
        }

        #endregion

        /// <summary>
        /// Küpler bitince çağrılır: artık toplanacak kargo kalmadığı için kuyrukta bekleyen (henüz
        /// yanaşmamış) gemileri hemen kaldırır. Sahnede hâlâ aktif olan gemilerden doğal olarak zaten
        /// kalkışa geçmiş olan (son kargoyla dolan gemi) kendi başına gider; geri kalanlar HEPSİ AYNI
        /// ANDA değil, kısa aralıklarla (kademeli) kalkışa zorlanır. Her geminin gerçekten sahneyi
        /// terk etmesi (OnDeparted) beklenir; son gemi de ayrıldığında bir sonraki seviyeye geçilir ve
        /// gemi kuyruğu o seviye için yeniden doldurulur.
        /// </summary>
        private void BeginLevelEndSequence()
        {
            m_LevelEndPending = true;

            var allShips = ShipController.ActiveShips;
            HashSet<ShipController> queueShips = m_QueuePool != null
                ? new HashSet<ShipController>(m_QueuePool.WaitingShips)
                : new HashSet<ShipController>();

            // Henüz yanaşmamış, kuyrukta bekleyen gemilerin artık toplayacağı kargo yok — bunları bekletmeden kaldır.
            if (m_QueuePool != null)
            {
                m_QueuePool.ClearQueue();
            }

            List<ShipController> shipsToForceDepart = new List<ShipController>();

            m_ShipsAwaitingDeparture = 0;
            for (int i = 0; i < allShips.Count; i++)
            {
                var ship = allShips[i];
                if (ship == null || queueShips.Contains(ship)) continue;

                m_ShipsAwaitingDeparture++;
                ship.OnDeparted += HandleShipDepartedDuringLevelEnd;

                // Zaten kalkışta olan (ör. son kargoyla az önce dolup kendi kendine kalkan) gemiye
                // dokunma — sadece hâlâ yanaşık duran gemileri kademeli kalkışa zorla.
                if (!ship.IsDeparting)
                {
                    shipsToForceDepart.Add(ship);
                }
            }

            if (shipsToForceDepart.Count > 0)
            {
                StartCoroutine(StaggeredForceDeparture(shipsToForceDepart));
            }

            if (m_ShipsAwaitingDeparture == 0)
            {
                CompleteLevelTransition();
            }
        }

        /// <summary>
        /// Kalan gemileri hepsi aynı anda değil, aralarında küçük bir gecikmeyle tek tek kalkışa
        /// zorlar — böylece "hepsi bir anda sahneyi terk etti" hissi yerine doğal, art arda bir
        /// kalkış sırası oluşur.
        /// </summary>
        private IEnumerator StaggeredForceDeparture(List<ShipController> ships)
        {
            const float staggerDelay = 0.25f;
            foreach (var ship in ships)
            {
                if (ship != null && !ship.IsDeparting)
                {
                    ship.DepartAndFreeSlot(force: true);
                }
                yield return new WaitForSeconds(staggerDelay);
            }
        }

        private void HandleShipDepartedDuringLevelEnd(ShipController ship)
        {
            ship.OnDeparted -= HandleShipDepartedDuringLevelEnd;
            m_ShipsAwaitingDeparture = Mathf.Max(0, m_ShipsAwaitingDeparture - 1);

            if (m_ShipsAwaitingDeparture == 0)
            {
                CompleteLevelTransition();
            }
        }

        private void CompleteLevelTransition()
        {
            SetTurboSpeed(false);
            m_IsAutoPlacing = false;
            if (CasualHudController.Instance != null)
            {
                CasualHudController.Instance.SetEndgameSpeedActive(false);
            }
            Debug.Log("<color=#00FFAA><b>[ShipDispatcher]</b></color> 🚢 Son gemi de sahneyi terk etti — seviye tamamlandı.");

            if (CasualHudController.Instance != null)
            {
                CasualHudController.Instance.HideLevelCompletePopup();
            }

            AdvanceToNextLevel();
        }

        private void AdvanceToNextLevel(int reward = 0)
        {
            LandFlowLoadingScreen screen = LandFlowLoadingScreen.Instance ?? LandFlowLoadingScreen.EnsureInstance();
            if (screen != null)
            {
                screen.ShowLevelCompleteAndLoad(() =>
                {
                    m_LevelEndPending = false;
                    if (LevelManager.Instance != null)
                    {
                        LevelManager.Instance.NextLevel();
                    }

                    if (m_QueuePool != null)
                    {
                        m_QueuePool.InitializeQueue();
                    }
                }, null);
            }
            else
            {
                m_LevelEndPending = false;
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.NextLevel();
                }

                if (m_QueuePool != null)
                {
                    m_QueuePool.InitializeQueue();
                }
            }
        }

        #region Reversed Ship Runners (Tersine Gemi Koşucuları)

        /// <summary>Panodaki küpler için gövde materyali (denizci koşucu açıkken insansı model materyali yerine küp materyalini döndürür).</summary>
        public Material GetRunnerBodyMaterial()
        {
            if (m_UseSailorRunner)
            {
#if UNITY_EDITOR
                if (m_TabletopRunnerPrefab == null)
                    m_TabletopRunnerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(TabletopRunnerPrefabPath);
#endif
                if (m_TabletopRunnerPrefab != null)
                {
                    foreach (var r in m_TabletopRunnerPrefab.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r == null || r.sharedMaterial == null) continue;
                        if (r.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        return SharedColorMaterialCache.ResolveSource(r.sharedMaterial);
                    }
                }
            }

            GameObject prefab = GetRunnerPrefab();
            if (prefab == null) return null;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.sharedMaterial == null) continue;
                if (r.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                return SharedColorMaterialCache.ResolveSource(r.sharedMaterial);
            }
            return null;
        }

        private GameObject m_LoadedRunnerFallback;
        private GameObject GetRunnerPrefab()
        {
            // Denizci koşucu (Resources/Sailor_Runner): sahneye referans gerekmez, build'de de yüklenir
            if (m_UseSailorRunner)
            {
                if (m_SailorRunnerPrefab == null) m_SailorRunnerPrefab = Resources.Load<GameObject>(SailorRunnerResourcePath);
                if (m_SailorRunnerPrefab != null) return m_SailorRunnerPrefab;
            }

            // Dik koşucu (MainCube_Running) bu sahnede bacaklarını ekranın altına doğru, kumun üstüne
            // yatırıyordu; zemine basan masa üstü varyantını tercih et.
#if UNITY_EDITOR
            if (m_TabletopRunnerPrefab == null)
                m_TabletopRunnerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(TabletopRunnerPrefabPath);
#endif
            if (m_TabletopRunnerPrefab != null) return m_TabletopRunnerPrefab;
            if (m_RunnerPrefab != null) return m_RunnerPrefab;
            if (m_CargoStandInPrefab != null) return m_CargoStandInPrefab;
            if (m_LoadedRunnerFallback != null) return m_LoadedRunnerFallback;

#if UNITY_EDITOR
            m_LoadedRunnerFallback = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MainCube_Running_Tabletop.prefab");
            if (m_LoadedRunnerFallback == null)
            {
                m_LoadedRunnerFallback = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MainCube_Running.prefab");
            }
#endif

            if (m_LoadedRunnerFallback == null)
            {
                var runnerInScene = FindFirstObjectByType<RunnerLegUpright>(FindObjectsInactive.Include);
                if (runnerInScene != null) m_LoadedRunnerFallback = runnerInScene.gameObject;
            }

            return m_LoadedRunnerFallback;
        }

        private Mesh m_CachedContainerMesh;
        private Mesh GetContainerMesh()
        {
            if (m_CachedContainerMesh != null) return m_CachedContainerMesh;

#if UNITY_EDITOR
            var fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kenney/kenney_watercraft-pack/Models/FBX format/cargo-container-a.fbx");
            if (fbx != null)
            {
                var mf = fbx.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    m_CachedContainerMesh = mf.sharedMesh;
                    return m_CachedContainerMesh;
                }
            }
#endif

            var anyCube = FindFirstObjectByType<PixelCube>(FindObjectsInactive.Include);
            if (anyCube != null)
            {
                var mf = anyCube.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    m_CachedContainerMesh = mf.sharedMesh;
                    return m_CachedContainerMesh;
                }
            }

            return null;
        }

        private RectTransform GetOtCerceveRect()
        {
            if (m_OtCerceve != null) return m_OtCerceve;

            var rects = Resources.FindObjectsOfTypeAll<RectTransform>();
            for (int i = 0; i < rects.Length; i++)
            {
                var rt = rects[i];
                if (rt != null && rt.gameObject.name == "OtCerceve" && rt.gameObject.scene.isLoaded)
                {
                    m_OtCerceve = rt;
                    return m_OtCerceve;
                }
            }
            return null;
        }

        /// <summary>
        /// OtCerceve (UI) dikdörtgeninin kamera üzerinden kumsal düzlemine izdüşümü. Obje kapalı olsa da çalışır;
        /// görsel gösterilmez, sadece koşucuların yürüdüğü çerçeve olarak kullanılır.
        /// </summary>
        private void ClampRunnerFrameToSand(float groundZ, float pitch, ref float fL, ref float fR, ref float fB, ref float fT)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Plane ground = new Plane(Vector3.back, new Vector3(0f, 0f, groundZ));
            bool Hit(Vector2 vp, out Vector3 w)
            {
                Ray ray = cam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
                w = Vector3.zero;
                if (!ground.Raycast(ray, out float d)) return false;
                w = ray.GetPoint(d);
                return true;
            }
            float minGap = pitch * 0.6f;
            if (Hit(new Vector2(0.5f, m_RunnerSandTopViewport), out Vector3 top))
                fT = Mathf.Max(Mathf.Min(fT, top.y), m_BoardTopY + minGap);
            if (Hit(new Vector2(m_RunnerSandSideViewport, 0.5f), out Vector3 left))
                fL = Mathf.Min(Mathf.Max(fL, left.x), m_BoardMinX - minGap);
            if (Hit(new Vector2(1f - m_RunnerSandSideViewport, 0.5f), out Vector3 right))
                fR = Mathf.Max(Mathf.Min(fR, right.x), m_BoardMaxX + minGap);
            // Alt: kıyı çizgisinin (iskele) üstünde kal
            IList<Vector2> shoreline = (m_Shoreline != null && m_Shoreline.Count >= 2) ? (IList<Vector2>)m_Shoreline : s_DefaultShoreline;
            float shoreY = shoreline[shoreline.Count / 2].y;
            fB = Mathf.Min(Mathf.Max(fB, shoreY + pitch * 0.5f), m_BoardBottomY - minGap);
        }

        /// <summary>
        /// Sahnedeki [SceneFramePreview] (PixelArtGenerator podyum/çerçevesi) sınırlarının kumsal düzlemine izdüşümü.
        /// Denizciler bu podyumun dış ahşap bordürü (rim) boyunca yürür; ortasından çapraz geçmez.
        /// </summary>
        /// <summary>Çerçeve şeridinin ortasının, sprite yarı boyutuna oranı (BoardPodium shader ölçülerinden; yoksa 0.9).</summary>
        private float FrameRimCenterFraction()
        {
            GameObject go = m_Generator != null && m_Generator.FramePreviewTransform != null
                ? m_Generator.FramePreviewTransform.gameObject
                : GameObject.Find("[SceneFramePreview]");
            SpriteRenderer sr = go != null ? go.GetComponent<SpriteRenderer>() : null;
            Material m = sr != null ? sr.sharedMaterial : null;
            if (m != null && m.HasProperty("_OuterHalf") && m.HasProperty("_InnerHalf"))
            {
                float f = m.GetFloat("_OuterHalf") + m.GetFloat("_InnerHalf");
                if (f > 0.5f && f < 1f) return f;
            }
            return 0.9f;
        }

        public bool TryGetSceneFramePreviewGroundRect(float groundZ, out float left, out float right, out float bottom, out float top)
        {
            left = right = bottom = top = 0f;
            if (m_Generator == null)
            {
                m_Generator = PixelArtGenerator.Instance;
            }

            Bounds b = default;
            bool found = false;
            if (m_Generator != null && m_Generator.TryGetSceneFramePreviewBounds(out b))
            {
                found = true;
            }
            else
            {
                GameObject go = GameObject.Find("[SceneFramePreview]");
                if (go != null && go.activeInHierarchy)
                {
                    SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null && sr.sprite != null)
                    {
                        b = sr.bounds;
                        found = true;
                    }
                }
            }

            if (!found) return false;

            Camera cam = ShipController.MainCamera != null ? ShipController.MainCamera : Camera.main;
            if (cam == null)
            {
                left = b.min.x;
                right = b.max.x;
                bottom = b.min.y;
                top = b.max.y;
                return true;
            }

            // [SceneFramePreview] köşelerini kumsal düzlemine kamera ışınıyla izdüşür:
            Plane ground = new Plane(Vector3.back, new Vector3(0f, 0f, groundZ));
            Vector3[] corners = new Vector3[4]
            {
                new Vector3(b.min.x, b.min.y, b.center.z),
                new Vector3(b.max.x, b.min.y, b.center.z),
                new Vector3(b.max.x, b.max.y, b.center.z),
                new Vector3(b.min.x, b.max.y, b.center.z)
            };

            left = bottom = float.MaxValue;
            right = top = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 screen = cam.WorldToScreenPoint(corners[i]);
                Ray ray = cam.ScreenPointToRay(screen);
                if (!ground.Raycast(ray, out float d)) return false;
                Vector3 g = ray.GetPoint(d);
                left = Mathf.Min(left, g.x);
                right = Mathf.Max(right, g.x);
                bottom = Mathf.Min(bottom, g.y);
                top = Mathf.Max(top, g.y);
            }

            // Denizciler çerçevenin beyaz şeridinin tam ortasından yürür: şerit, sprite kenarına oranla dış kenar
            // (_OuterHalf) ile tuval sınırı (_InnerHalf) arasındadır; ortası (dış + iç) / 2 / 0.5 = dış + iç
            float cx = (left + right) * 0.5f;
            float cy = (bottom + top) * 0.5f;
            float rimCenter = FrameRimCenterFraction();
            float hw = (right - left) * 0.5f * rimCenter;
            float hh = (top - bottom) * 0.5f * rimCenter;
            left = cx - hw;
            right = cx + hw;
            bottom = cy - hh;
            top = cy + hh;

            return right > left && top > bottom && float.IsFinite(left) && float.IsFinite(top);
        }

        private bool TryGetOtCerceveGroundRect(float groundZ, out float left, out float right, out float bottom, out float top)
        {
            left = right = bottom = top = 0f;
            RectTransform rt = GetOtCerceveRect();
            Camera cam = ShipController.MainCamera != null ? ShipController.MainCamera : Camera.main;
            if (rt == null || cam == null) return false;

            // UI köşeleri Canvas'ın kamerasıyla ekrana, ekrandan da oyun kamerasıyla kumsala indirilir
            Canvas canvas = rt.GetComponentInParent<Canvas>(true);
            Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null) ? canvas.worldCamera : cam;
            bool overlay = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay;

            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var ground = new Plane(Vector3.back, new Vector3(0f, 0f, groundZ));
            left = bottom = float.MaxValue; right = top = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 screen = overlay ? corners[i] : uiCam.WorldToScreenPoint(corners[i]);
                Ray ray = cam.ScreenPointToRay(screen);
                if (!ground.Raycast(ray, out float d)) return false;
                Vector3 g = ray.GetPoint(d);
                left = Mathf.Min(left, g.x); right = Mathf.Max(right, g.x);
                bottom = Mathf.Min(bottom, g.y); top = Mathf.Max(top, g.y);
            }
            return right > left && top > bottom && float.IsFinite(left) && float.IsFinite(top);
        }

        public bool TryGetOtCerceveWorldBounds(out Vector3 min, out Vector3 max, out Vector3[] corners)
        {
            corners = new Vector3[4];
            EnsureBoardBounds();

            float groundZ = BeachGroundZ();
            if (float.IsNaN(groundZ)) groundZ = m_ShoreZ;

            // Kullanıcı isteği: OtCerceve değil [SceneFramePreview] kullanılıyor
            if (TryGetSceneFramePreviewGroundRect(groundZ, out float pL, out float pR, out float pB, out float pT))
            {
                min = new Vector3(pL, pB, groundZ);
                max = new Vector3(pR, pT, groundZ);
                corners[0] = new Vector3(pL, pB, groundZ);
                corners[1] = new Vector3(pL, pT, groundZ);
                corners[2] = new Vector3(pR, pT, groundZ);
                corners[3] = new Vector3(pR, pB, groundZ);
                return true;
            }

            // OtCerceve çerçevesinin dünya koordinatları (yedek fallback):
            // Küp panosunun dış sınırları etrafında temiz bir yürüme koridoru (3D kumsal düzlemi üzerinde).
            float margin = 0.32f;
            float minX = m_BoardMinX - margin;
            float maxX = m_BoardMaxX + margin;
            float minY = m_BoardBottomY - margin;
            float maxY = m_BoardTopY + margin;

            min = new Vector3(minX, minY, groundZ);
            max = new Vector3(maxX, maxY, groundZ);
            corners[0] = new Vector3(min.x, min.y, groundZ);
            corners[1] = new Vector3(min.x, max.y, groundZ);
            corners[2] = new Vector3(max.x, max.y, groundZ);
            corners[3] = new Vector3(max.x, min.y, groundZ);
            return true;
        }

        private float CalculateReversedRunnersDuration(List<PixelCube> cubes, ShipController ship, Vector3 shoreCenter)
        {
            float baseSpeed = m_RunnerSpeed;
            float maxDist = 0f;
            for (int i = 0; i < cubes.Count; i++)
            {
                if (cubes[i] == null) continue;
                float d = Vector3.Distance(shoreCenter, cubes[i].transform.position);
                if (d > maxDist) maxDist = d;
            }
            float runTime = (maxDist * 2.2f) / baseSpeed;
            float hopsTime = m_HopDuration * 2f + 0.4f;
            float staggerTotal = (cubes.Count - 1) * m_RunnerStaggerDelay;
            return runTime + hopsTime + staggerTotal + 0.6f;
        }

        /// <summary>
        /// Koşucu yolu: kıyıdan açık kumsal üzerinden en alttaki küp sırasının hemen altına yürür,
        /// oradan SADECE boş hücrelerden geçerek hedef küpe ulaşır. Giriş noktası, kıyıya uzaklık +
        /// içerideki yol uzunluğu en kısa olacak şekilde seçilir. Küplerin içinden geçmez, çerçeve
        /// boyunca dolaşıp tepeden inmez. Yol bulunamazsa eski çerçeve yoluna düşülür.
        /// </summary>
        private List<Vector3> BuildGridAwareRunnerWaypoints(
            Vector3 shoreStart,
            PixelCube targetCube,
            Vector3 otMin,
            Vector3 otMax,
            Vector3[] otCorners,
            float groundZ,
            Vector3? frameStart = null)
        {
            Vector3 cubeWorld = targetCube.transform.position;
            if (!EnsureGridFrame())
                return BuildOtCerceveWaypoints(frameStart ?? shoreStart, cubeWorld, otMin, otMax, otCorners, groundZ);

            // 1. deneme: panoda fiziksel olarak duran her küp engel (rezerve olsalar da); koşucu küplerin yanından yürür.
            // 2. deneme: hedef yalnızca rezerve küpler alınınca açılıyorsa (ör. duvarın 2. sırası) rezerve küpler yok sayılır.
            var path = FindRunnerGridPath(shoreStart, targetCube, false, groundZ, frameStart) ?? FindRunnerGridPath(shoreStart, targetCube, true, groundZ, frameStart);
            return path ?? BuildOtCerceveWaypoints(frameStart ?? shoreStart, cubeWorld, otMin, otMax, otCorners, groundZ);
        }

        /// <summary>
        /// Referans oyundaki karınca yolu: kıyıdan çerçevenin alt kenarına çıkar, çerçeve boyunca (resmin dışından)
        /// hedefin AÇIKLIĞININ hizasına kadar yürür, oradan sadece boş hücreleri takip ederek küpe ulaşır.
        /// Açıklık (boş hücrelerden resmin dışına çıkış), toplam yürüme mesafesi en kısa olacak şekilde seçilir.
        /// Çerçeve sabittir (OtCerceve / bölüm başındaki resmin sınırları + m_RunnerFrameMarginCubes); küpler alındıkça
        /// küçülmez, böylece boşalan alan da yürünebilir iç bölge sayılır.
        /// </summary>
        /// <summary>
        /// Koşucuların yürüdüğü sabit çerçeve koridoru (dünya XY): sahnedeki [SceneFramePreview] podyumunun (veya yedek
        /// olarak OtCerceve'nin) kumsala izdüşümü ile resmin sınırları + pay. Izgara adımını (pitch) döndürür.
        /// </summary>
        private float GetRunnerFrameRect(float groundZ, out float fL, out float fR, out float fB, out float fT)
        {
            EnsureBoardBounds();
            float pitch = Mathf.Max(0.05f, m_GridFrame.Pitch);
            float margin = pitch * m_RunnerFrameMarginCubes;
            fL = m_BoardMinX - margin; fR = m_BoardMaxX + margin; fB = m_BoardBottomY - margin; fT = m_BoardTopY + margin;

            // Kullanıcı isteği: "otcerceve deil [SceneFramePreview] i kullanıyoruz ona göre yap"
            // Koridor çerçeve şeridinin tam ortası; resim payı yalnızca küplere 0.6 küpten fazla yaklaşmasın diye
            if (TryGetSceneFramePreviewGroundRect(groundZ, out float pL, out float pR, out float pB, out float pT))
            {
                float gap = pitch * 0.6f;
                fL = Mathf.Min(pL, m_BoardMinX - gap);
                fR = Mathf.Max(pR, m_BoardMaxX + gap);
                fB = Mathf.Min(pB, m_BoardBottomY - gap);
                fT = Mathf.Max(pT, m_BoardTopY + gap);
            }
            else if (m_RunnerFrameUseOtCerceve && TryGetOtCerceveGroundRect(groundZ, out float oL, out float oR, out float oB, out float oT))
            {
                fL = Mathf.Min(oL, fL);
                fR = Mathf.Max(oR, fR);
                fB = Mathf.Min(oB, fB);
                fT = Mathf.Max(oT, fT);
            }
            // Görünen kumsalın dışına (limana, ekran kenarına, iskeleye) taşma; ama küplere de yarım küpten fazla yaklaşma
            ClampRunnerFrameToSand(groundZ, pitch, ref fL, ref fR, ref fB, ref fT);
            return pitch;
        }

        private List<Vector3> FindRunnerGridPath(Vector3 shoreStart, PixelCube targetCube, bool ignoreReserved, float groundZ, Vector3? frameStart = null)
        {
            Vector3 cubeWorld = targetCube.transform.position;
            if (!TryBuildLiveGrid(out var gridMap, out _, out _, ignoreReserved))
                return null;

            float pitch = GetRunnerFrameRect(groundZ, out float fL, out float fR, out float fB, out float fT);

            Vector3 ToGround((int, int) c)
            {
                Vector3 w = m_GridFrame.ToWorld(c.Item1, c.Item2);
                w.z = groundZ;
                return w;
            }

            // Kıyıdan çerçeve boyunca çıkış hücresinin hizasına giden rota (çıkış hücresi hariç)
            List<Vector3> FrameRoute(Vector3 exitWorld, int side)
            {
                // Denizci zaten çerçeve koridorundaysa (yığın toplarken) kıyıdan değil bulunduğu yerden, kısa yönden dolaş
                if (frameStart.HasValue) return PerimeterRoute(frameStart.Value, exitWorld, side, fL, fR, fB, fT, groundZ);
                var r = new List<Vector3>(6);
                Vector3 P(float x, float y) => new Vector3(x, y, groundZ);
                r.Add(P(shoreStart.x, shoreStart.y));
                r.Add(P(shoreStart.x, fB));
                float bx = Mathf.Clamp(shoreStart.x, fL, fR);
                if (Mathf.Abs(bx - shoreStart.x) > 0.01f)
                {
                    r.Add(P(bx, fB));
                }
                if (side == 0) { r.Add(P(Mathf.Clamp(exitWorld.x, fL, fR), fB)); }
                else if (side == 1) { r.Add(P(fL, fB)); r.Add(P(fL, Mathf.Clamp(exitWorld.y, fB, fT))); }
                else if (side == 2) { r.Add(P(fR, fB)); r.Add(P(fR, Mathf.Clamp(exitWorld.y, fB, fT))); }
                else
                {
                    // Üst kenar: soldan mı sağdan mı dolaşmak daha kısa?
                    float viaL = (bx - fL) + (fT - fB) + Mathf.Abs(exitWorld.x - fL);
                    float viaR = (fR - bx) + (fT - fB) + Mathf.Abs(fR - exitWorld.x);
                    float sx = viaL <= viaR ? fL : fR;
                    r.Add(P(sx, fB)); r.Add(P(sx, fT)); r.Add(P(Mathf.Clamp(exitWorld.x, fL, fR), fT));
                }
                return r;
            }

            float RouteLength(List<Vector3> r, Vector3 last)
            {
                float len = 0f;
                for (int i = 1; i < r.Count; i++) len += Vector3.Distance(r[i - 1], r[i]);
                return len + Vector3.Distance(r[r.Count - 1], last);
            }

            // Hücre sabit çerçevenin çizgisine yarım küpten yakınsa ya da dışındaysa çerçeve koridorundadır (çıkış).
            // Çerçeve küpler alındıkça küçülmez: resim boşaldıkça koşucu boşalan kumdan kestirme yürür.
            float half = pitch * 0.5f;
            int FrameSide((int, int) c)
            {
                Vector3 w = m_GridFrame.ToWorld(c.Item1, c.Item2);
                if (w.y < fB + half) return 0;
                if (w.x < fL + half) return 1;
                if (w.x > fR - half) return 2;
                if (w.y > fT - half) return 3;
                return -1;
            }

            // Hedeften dışarı doğru boş hücrelerde BFS; çerçeveye varan hücreler çıkış adayı
            var start = (targetCube.GridX, targetCube.GridY);
            var cameFrom = new Dictionary<(int, int), (int, int)>();
            var steps = new Dictionary<(int, int), int>();
            var queue = new Queue<(int, int)>();
            cameFrom[start] = start; steps[start] = 0;
            queue.Enqueue(start);
            (int, int)? best = null; int bestSide = 0;
            float bestCost = float.MaxValue;
            var dirs = new (int, int)[] { (0, -1), (-1, 0), (1, 0), (0, 1) };
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                int curSteps = steps[cur];
                int side = cur == start ? -1 : FrameSide(cur);
                if (side >= 0)
                {
                    Vector3 ew = ToGround(cur);
                    float cost = RouteLength(FrameRoute(ew, side), ew) + curSteps * pitch * m_RunnerInnerStepCost;
                    if (cost < bestCost) { bestCost = cost; best = cur; bestSide = side; }
                    continue;   // çıkış hücresinden öteye gerek yok (dışarısı açık koridor)
                }
                foreach (var d in dirs)
                {
                    var nxt = (cur.Item1 + d.Item1, cur.Item2 + d.Item2);
                    if (cameFrom.ContainsKey(nxt)) continue;
                    // Panoda duran küpler engeldir (ignoreReserved ise koşucusu yolda olanlar gitmiş sayılır)
                    if (gridMap.ContainsKey(nxt)) continue;
                    cameFrom[nxt] = cur;
                    steps[nxt] = curSteps + 1;
                    queue.Enqueue(nxt);
                }
            }

            if (!best.HasValue) return null;

            // Çıkıştan hedefe hücre listesi (düz giden ara noktalar atılır)
            var cells = new List<(int, int)>();
            for (var c = best.Value; ; c = cameFrom[c])
            {
                cells.Add(c);
                if (c == start) break;
            }
            var simplified = new List<(int, int)> { cells[0] };
            for (int i = 1; i < cells.Count - 1; i++)
            {
                var a = simplified[simplified.Count - 1]; var b = cells[i]; var n = cells[i + 1];
                bool straight = (b.Item1 - a.Item1) * (n.Item2 - b.Item2) == (b.Item2 - a.Item2) * (n.Item1 - b.Item1);
                if (!straight) simplified.Add(b);
            }
            simplified.Add(cells[cells.Count - 1]);

            Vector3 exitPoint = ToGround(simplified[0]);
            var waypoints = FrameRoute(exitPoint, bestSide);
            for (int i = 0; i < simplified.Count - 1; i++) waypoints.Add(ToGround(simplified[i]));

            // Küpün içine girmesin: geldiği yönden küpün önünde (merkezden m_PickupStandoffCubes küp geride) dursun
            Vector3 cubeOnGround = new Vector3(cubeWorld.x, cubeWorld.y, groundZ);
            Vector3 approachFrom = waypoints[waypoints.Count - 1];
            Vector3 toCube = cubeOnGround - approachFrom;
            float standoff = pitch * m_PickupStandoffCubes;
            if (toCube.magnitude > standoff + pitch * 0.05f)
                waypoints.Add(cubeOnGround - toCube.normalized * standoff);
            else if (toCube.sqrMagnitude < 1e-8f)
                waypoints.Add(cubeOnGround);
            return waypoints;
        }

        private List<Vector3> BuildOtCerceveWaypoints(
            Vector3 shoreStart,
            Vector3 cubeTarget,
            Vector3 otMin,
            Vector3 otMax,
            Vector3[] otCorners,
            float groundZ)
        {
            var waypoints = new List<Vector3>(8);
            waypoints.Add(new Vector3(shoreStart.x, shoreStart.y, groundZ));

            float centerX = (otMin.x + otMax.x) * 0.5f;
            float pathLeftX = otMin.x;
            float pathRightX = otMax.x;
            float pathBottomY = otMin.y;
            float pathTopY = otMax.y;

            // Hedef küpe en yakın çerçeve kenarını belirle
            float distBottom = Mathf.Abs(cubeTarget.y - pathBottomY);
            float distLeft = Mathf.Abs(cubeTarget.x - pathLeftX);
            float distRight = Mathf.Abs(cubeTarget.x - pathRightX);
            float distTop = Mathf.Abs(cubeTarget.y - pathTopY);

            float minDist = Mathf.Min(distBottom, Mathf.Min(distLeft, Mathf.Min(distRight, distTop)));

            if (shoreStart.y < pathBottomY)
            {
                waypoints.Add(new Vector3(shoreStart.x, pathBottomY, groundZ));
            }

            if (minDist == distBottom)
            {
                // Alttan doğrudan çerçeveye çıkış ve hedefe yöneliş
                waypoints.Add(new Vector3(cubeTarget.x, pathBottomY, groundZ));
                waypoints.Add(new Vector3(cubeTarget.x, cubeTarget.y, groundZ));
            }
            else if (minDist == distLeft)
            {
                // Sol çerçeve kenarı boyunca koşu
                waypoints.Add(new Vector3(pathLeftX, pathBottomY, groundZ));
                waypoints.Add(new Vector3(pathLeftX, cubeTarget.y, groundZ));
                waypoints.Add(new Vector3(cubeTarget.x, cubeTarget.y, groundZ));
            }
            else if (minDist == distRight)
            {
                // Sağ çerçeve kenarı boyunca koşu
                waypoints.Add(new Vector3(pathRightX, pathBottomY, groundZ));
                waypoints.Add(new Vector3(pathRightX, cubeTarget.y, groundZ));
                waypoints.Add(new Vector3(cubeTarget.x, cubeTarget.y, groundZ));
            }
            else
            {
                // Üst çerçeve kenarı boyunca koşu
                bool goLeft = cubeTarget.x <= centerX;
                float flankX = goLeft ? pathLeftX : pathRightX;
                waypoints.Add(new Vector3(flankX, pathBottomY, groundZ));
                waypoints.Add(new Vector3(flankX, pathTopY, groundZ));
                waypoints.Add(new Vector3(cubeTarget.x, pathTopY, groundZ));
                waypoints.Add(new Vector3(cubeTarget.x, cubeTarget.y, groundZ));
            }

            return waypoints;
        }

        private void ApplyColorToRunner(GameObject runner, Color color)
        {
            if (runner == null) return;

            // Koşucunun denizci olup olmadığını tespit et (SailorRunnerVisual bileşeni veya M_* denizci materyalleri)
            bool isSailor = runner.GetComponent<SailorRunnerVisual>() != null;
            if (!isSailor)
            {
                foreach (var r in runner.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    if (r.name.StartsWith("TeamColor", System.StringComparison.Ordinal) ||
                        r.name.IndexOf("Cap", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isSailor = true;
                        break;
                    }
                    var sm = r.sharedMaterials;
                    for (int m = 0; m < sm.Length; m++)
                    {
                        if (sm[m] != null && sm[m].name.StartsWith("M_", System.StringComparison.Ordinal))
                        {
                            isSailor = true;
                            break;
                        }
                    }
                    if (isSailor) break;
                }
            }

            if (isSailor)
            {
                // Denizci: Hypercasual stilinde tatlı, sıcak bir ten rengi, net ayırt edici kıyafet detayları ve
                // toplanan küple (%100 aynı renkte) şapka / ponpon / tişört çizgileri.
                foreach (var r in runner.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    if (r.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

                    if (r.name.IndexOf("Band", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Şapkanın alt şeridi: temiz beyaz denizci şeridi
                        SharedColorMaterialCache.Apply(r, m_SailorWhiteColor, Color.black);
                    }
                    else if (r.name.IndexOf("Cap", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Şapkanın üst kubbesi: toplanan küpün / ait olunan geminin rengi
                        SharedColorMaterialCache.Apply(r, color, Color.black);
                    }
                    else if (r.name.IndexOf("Pompom", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                             r.name.StartsWith("TeamColor", System.StringComparison.Ordinal))
                    {
                        // Şapka ponponu ve diğer takım rengi parçaları: toplanan küpün rengi
                        SharedColorMaterialCache.Apply(r, color, Color.black);
                    }
                    else
                    {
                        // Çoklu alt materyallere sahip parçalar (Gövde, kollar, bacaklar, yüz, kafa)
                        Material[] mats = r.sharedMaterials;
                        bool changed = false;
                        for (int m = 0; m < mats.Length; m++)
                        {
                            Material src = SharedColorMaterialCache.ResolveSource(mats[m]);
                            if (src == null) continue;
                            string sName = src.name;

                            if (sName.StartsWith("M_Skin", System.StringComparison.Ordinal))
                            {
                                // Yüz, boyun, el ve bacak ten rengi: sevimli ve sıcak hypercasual şeftali tonu
                                mats[m] = SharedColorMaterialCache.GetWithCustomShadow(src, m_SailorSkinColor, m_SailorSkinShadowColor, Color.black);
                                changed = true;
                            }
                            else if (sName.StartsWith("M_TeamColor", System.StringComparison.Ordinal))
                            {
                                // Takım rengi yuvaları: toplanan küpün rengi
                                mats[m] = SharedColorMaterialCache.Get(src, color, Color.black);
                                changed = true;
                            }
                            else if (sName.StartsWith("M_Navy", System.StringComparison.Ordinal))
                            {
                                // Tişört çizgileri ve yaka toplanacak küpün renginde (böylece üst beden küpü temsil eder);
                                // Bacaklardaki şort ise klasik lacivert kalır (kontrast ve ayırt edici detay).
                                if (r.name == "Torso" || r.name == "ArmL" || r.name == "ArmR")
                                {
                                    mats[m] = SharedColorMaterialCache.Get(src, color, Color.black);
                                }
                                else
                                {
                                    mats[m] = SharedColorMaterialCache.Get(src, m_SailorNavyColor, Color.black);
                                }
                                changed = true;
                            }
                            else if (sName.StartsWith("M_White", System.StringComparison.Ordinal))
                            {
                                // Canlı, temiz beyaz tişört ve detaylar
                                mats[m] = SharedColorMaterialCache.Get(src, m_SailorWhiteColor, Color.black);
                                changed = true;
                            }
                            else if (sName.StartsWith("M_Black", System.StringComparison.Ordinal))
                            {
                                // Ayakkabılar ve gözler için temiz koyu antrasit
                                mats[m] = SharedColorMaterialCache.Get(src, m_SailorShoeColor, Color.black);
                                changed = true;
                            }
                            else if (sName.StartsWith("M_Pink", System.StringComparison.Ordinal))
                            {
                                // Yanaklardaki sevimli pembe allık
                                mats[m] = SharedColorMaterialCache.Get(src, m_SailorBlushColor, Color.black);
                                changed = true;
                            }
                        }
                        if (changed) r.sharedMaterials = mats;
                    }
                }
            }
            else
            {
                // Standart küp koşucuları için tüm gövde toplanan küp renginde
                foreach (var r in runner.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    if (r.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    SharedColorMaterialCache.Apply(r, color, Color.black);
                }
            }

            // Karakterdeki ve şapkadaki tüm specular ve glint parlaklıklarını sıfırla (temiz mat görünüm)
            foreach (var r in runner.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    SharedColorMaterialCache.SanitizeMatteProperties(mats[i]);
                }
            }
        }

        /// <summary>
        /// Yükü koşucunun gerçek görünür sınırlarının üstüne (sırtına) oturtur; koşucunun child'ı
        /// olduğu için koşarken onunla birlikte döner ve hareket eder.
        /// </summary>
        private void PlaceCargoOnRunnerBack(Transform cargo, Mesh cargoMesh, Transform runner)
        {
            if (cargo == null || runner == null) return;

            bool hasBounds = false;
            Bounds body = new Bounds(runner.position, Vector3.zero);
            foreach (var r in runner.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || r.transform.IsChildOf(cargo)) continue;
                if (r.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (!hasBounds) { body = r.bounds; hasBounds = true; }
                else body.Encapsulate(r.bounds);
            }
            if (!hasBounds) return;

            Vector3 cargoScale = cargo.lossyScale;
            Bounds mb = cargoMesh != null ? cargoMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            Vector3 pivotToCenter = cargo.rotation * Vector3.Scale(mb.center, cargoScale);

            RunnerLegUpright leg = runner.GetComponent<RunnerLegUpright>();
            Vector3 centerWorld;
            if (leg != null && leg.IsTabletop)
            {
                // Masa üstü: zemin XY, sırt kameraya bakan taraf (-Z). Yükü gövdenin üstüne koy,
                // hafifçe geriye (+Y = kameradan uzak) kaydır.
                float cubeD = Mathf.Abs(mb.size.z * cargoScale.z);
                float cubeH = Mathf.Abs(mb.size.y * cargoScale.y);
                centerWorld = new Vector3(
                    body.center.x,
                    body.center.y + cubeH * m_CarriedCargoBackShift,
                    body.min.z - cubeD * (0.5f + m_CarriedCargoGap));
            }
            else
            {
                float cubeH = Mathf.Abs(mb.size.y * cargoScale.y);
                centerWorld = new Vector3(
                    body.center.x,
                    body.max.y + cubeH * (0.5f + m_CarriedCargoGap),
                    body.center.z + cubeH * m_CarriedCargoBackShift);
            }
            cargo.position = centerWorld - pivotToCenter;
        }

        /// <summary>Nesneye panodaki küpün mesh'ini ve renkli materyalini verir; kullanılan mesh'i döndürür.</summary>
        private Mesh ApplyCargoCubeLook(GameObject go, PixelCube sourceCube)
        {
            MeshFilter mf = go.AddComponent<MeshFilter>();
            Mesh sourceMesh = null;
            MeshFilter sourceMf = sourceCube != null ? sourceCube.GetComponent<MeshFilter>() : null;
            if (sourceMf != null && sourceMf.sharedMesh != null) sourceMesh = sourceMf.sharedMesh;
            if (sourceMesh == null) sourceMesh = GetContainerMesh();
            mf.sharedMesh = sourceMesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            // Materyalsiz MeshRenderer pembe (missing material) çiziliyordu: küpün kendi materyalini ver.
            MeshRenderer sourceMr = sourceCube != null ? sourceCube.GetComponent<MeshRenderer>() : null;
            if (sourceMr != null && sourceMr.sharedMaterial != null)
                mr.sharedMaterial = SharedColorMaterialCache.ResolveSource(sourceMr.sharedMaterial);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Color c = sourceCube != null ? sourceCube.CurrentColor : Color.white;
            SharedColorMaterialCache.Apply(mr, c, Color.black);
            return sourceMesh;
        }

        /// <summary>
        /// Denizcinin ellerine boş bir küp yığını bağlar (ilk küp ve sonrakiler AddCubeToCargoStack ile eklenir).
        /// Socket'i olmayan koşucularda null döner (yığın taşıma yalnızca denizcide).
        /// </summary>
        private CarriedCargoStack CreateCarriedCargoStack(PixelCube firstCube, SailorRunnerVisual sailor)
        {
            if (sailor == null || sailor.CargoSocket == null) return null;
            GameObject root = new GameObject("CarriedCargoStack");
            var stack = root.AddComponent<CarriedCargoStack>();
            MeshFilter sourceMf = firstCube != null ? firstCube.GetComponent<MeshFilter>() : null;
            Mesh mesh = sourceMf != null && sourceMf.sharedMesh != null ? sourceMf.sharedMesh : GetContainerMesh();
            float meshEdge = mesh != null ? Mathf.Max(1e-4f, mesh.bounds.size.x) : 1f;
            // Kök ölçeği tek küplük taşımayla aynı: yığının yerel uzayında 1 birim = bir küp kenarı
            sailor.AttachCargo(root.transform, m_SailorCargoSize, meshEdge);
            root.transform.localScale = Vector3.one * m_SailorCargoSize;
            return stack;
        }

        private void AddCubeToCargoStack(CarriedCargoStack stack, PixelCube sourceCube)
        {
            if (stack == null) return;
            GameObject cube = new GameObject("CarriedCargo");
            Mesh mesh = ApplyCargoCubeLook(cube, sourceCube);
            float meshEdge = mesh != null ? Mathf.Max(1e-4f, mesh.bounds.size.x) : 1f;
            // Yığın yerel uzayı küp kenarı birimindedir; mesh kendi kenarı kadar büyük olduğundan normalize et
            stack.Add(cube.transform, 1f / meshEdge);
        }

        private GameObject CreateCarriedCargoVisual(PixelCube sourceCube, Transform runnerParent)
        {
            GameObject carried = new GameObject("CarriedCargo");
            carried.transform.SetParent(runnerParent, false);
            carried.transform.localPosition = m_CarriedCargoOffset;
            carried.transform.localRotation = Quaternion.identity;
            // Koşucu zaten panodaki küp boyutunda; yük de dünyada aynı boyutta olsun
            // (küpün ölçeğini bir de koşucunun ölçeğiyle çarpınca yük nokta kadar kalıyordu).
            Vector3 refWorld = sourceCube != null ? sourceCube.transform.lossyScale : Vector3.one;
            Vector3 parentWorld = runnerParent != null ? runnerParent.lossyScale : Vector3.one;
            carried.transform.localScale = new Vector3(
                parentWorld.x > 1e-4f ? refWorld.x / parentWorld.x : 1f,
                parentWorld.y > 1e-4f ? refWorld.y / parentWorld.y : 1f,
                parentWorld.z > 1e-4f ? refWorld.z / parentWorld.z : 1f);
            carried.transform.rotation = sourceCube != null ? sourceCube.transform.rotation : carried.transform.rotation;

            Mesh sourceMesh = ApplyCargoCubeLook(carried, sourceCube);

            // Denizci: küp iki elinin arasında, CargoSocket'te (1 model birimi = panodaki küp)
            SailorRunnerVisual sailor = runnerParent != null ? runnerParent.GetComponent<SailorRunnerVisual>() : null;
            if (sailor != null && sailor.CargoSocket != null)
            {
                float meshEdge = sourceMesh != null ? Mathf.Max(1e-4f, sourceMesh.bounds.size.x) : 1f;
                sailor.AttachCargo(carried.transform, m_SailorCargoSize, meshEdge);
                return carried;
            }

            PlaceCargoOnRunnerBack(carried.transform, sourceMesh, runnerParent);

            // İstenmeyen sahte gölge nesnelerini temizle
            for (int i = carried.transform.childCount - 1; i >= 0; i--)
            {
                Transform ch = carried.transform.GetChild(i);
                if (ch != null && ch.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ch.gameObject.SetActive(false);
                    Destroy(ch.gameObject);
                }
            }

            return carried;
        }

        private IEnumerator RunReversedShipRunnersToCollectCargo(
            List<PixelCube> cubes,
            ShipController ship,
            Vector3 shoreTargetAtLaunch,
            int batchQuota = 1,
            StackClaim claim = null)
        {
            if (cubes == null || cubes.Count == 0 || ship == null) yield break;
            int count = cubes.Count;

            EnsureBoardBounds();
            TryGetOtCerceveWorldBounds(out Vector3 otMin, out Vector3 otMax, out Vector3[] otCorners);

            float groundPlaneZ = BeachGroundZ();
            if (float.IsNaN(groundPlaneZ)) groundPlaneZ = m_ShoreZ;

            for (int k = 0; k < count; k++)
            {
                PixelCube targetCube = cubes[k];
                if (ship == null || ship.IsDeparting || targetCube == null || targetCube.IsPopped)
                {
                    // Denizci çıkmıyor: bu küp için (yığın kotası dahil) ayrılan yerleri geri ver
                    int unusedQuota = count == 1 ? Mathf.Max(1, batchQuota) : 1;
                    if (ship != null)
                    {
                        if (count == 1) ReleaseStackClaim(claim);
                        for (int q = 0; q < unusedQuota; q++) ship.ReleaseCargoReservation();
                    }
                    m_ActiveCargoFlightCount = Mathf.Max(0, m_ActiveCargoFlightCount - unusedQuota);
                    if (targetCube != null) s_ReservedCubes.Remove(targetCube);
                    if (ship == null || ship.IsDeparting) break;
                    continue;
                }

                // Koşucular artık tek tek çıktığı için k hep 0 olurdu; sağ/sol sırası global sayaçla dönsün.
                // Yığın kotası yalnızca tek küplük fırlatmada anlamlı (koşucular tek tek çıkıyor)
                int quota = count == 1 ? Mathf.Max(1, batchQuota) : 1;
                StartCoroutine(SingleRunnerMissionRoutine(m_RunnerSerial++, count, targetCube, ship, shoreTargetAtLaunch, otMin, otMax, otCorners, groundPlaneZ, quota, count == 1 ? claim : null));

                if (k < count - 1)
                {
                    // Denizciler dip dibe çıkmasın: kendi (daha seyrek) aralıklarıyla
                    bool sailor = m_UseSailorRunner && GetRunnerPrefab() != null && GetRunnerPrefab().GetComponent<SailorRunnerVisual>() != null;
                    yield return new WaitForSeconds(sailor ? Mathf.Max(m_RunnerLaunchInterval, m_SailorLaunchInterval) : m_RunnerLaunchInterval);
                }
            }
        }

        /// <summary>Koşucunun kökünden zemine doğru (+z) en derin görünür noktasına uzaklık.</summary>
        private static float RunnerGroundDepth(GameObject runner)
        {
            if (runner == null) return 0f;
            bool has = false;
            float maxZ = 0f;
            foreach (var r in runner.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled) continue;
                if (r.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                float z = r.bounds.max.z;
                if (!has || z > maxZ) { maxZ = z; has = true; }
            }
            return has ? Mathf.Max(0f, maxZ - runner.transform.position.z) : 0f;
        }

        private static void ReleaseReservedCubes(PixelCube current, List<PixelCube> picked)
        {
            if (current != null) s_ReservedCubes.Remove(current);
            if (picked == null) return;
            for (int i = 0; i < picked.Count; i++)
                if (picked[i] != null) s_ReservedCubes.Remove(picked[i]);
        }

        /// <summary>
        /// Bu renkte sıradaki toplanabilir küpler, toplama sırasıyla (CollectionOrder: önce en alt sıra, aynı sırada en
        /// dış, eşitlikte sol). Yalnızca dışarıya erişimi olan, kimseye ayrılmamış, gizemli olmayan küpler döner;
        /// rezerve edilmiş küpler tahtada durduğu için erişimi ve sıra uçlarını belirlemeye devam eder. Boşsa beklenir.
        /// </summary>
        public List<PixelCube> GetNextCollectibleBlocks(Color color, (int, int)? refCell = null)
        {
            var result = new List<PixelCube>();
            var all = PixelCube.ActiveCubes;
            if (all == null || all.Count == 0) return result;

            s_ReservedCubes.RemoveWhere(c => c == null || c.IsPopped || !c.gameObject.activeSelf);
            var present = new Dictionary<(int, int), PixelCube>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                PixelCube c = all[i];
                if (c != null && !c.IsPopped && c.gameObject.activeSelf) present[(c.GridX, c.GridY)] = c;
            }

            var cells = CollectionOrder.GetNextCollectibleCells(present.Keys, cell =>
            {
                PixelCube c = present[cell];
                if (s_ReservedCubes.Contains(c) || c.IsMystery) return false;
                return ColorsMatch(c.CurrentColor, color) || ColorsMatch(c.OriginalColor, color);
            }, refCell);
            for (int i = 0; i < cells.Count; i++) result.Add(present[cells[i]]);
            return result;
        }

        /// <summary>
        /// Konteynerin tüm kapasitesi yoldaki denizcilere dağıtılmışsa (yeni denizci çıkmayacaksa) atlama sınırı
        /// uygulanmaz: denizcinin bıraktığı her boş yer, tek küp için fazladan bir denizci demekti (30 = 6 × 5'te
        /// biri 4'le dönünce 7. denizci 1 küp için çıkıyordu). Bu durumda denizci uzaktaki küpü de alıp görevini doldurur.
        /// </summary>
        private static bool JumpLimitRelaxed(StackClaim claim)
        {
            return claim != null && claim.Ship != null && !claim.Ship.CanAcceptMore;
        }

        /// <summary>Bu renkte, verilen hücreye atlama sınırı içinde kimseye ayrılmamış erişilebilir en dış küp var mı?</summary>
        private bool HasNearbyCollectible(Color color, (int, int) from)
        {
            var list = GetNextCollectibleBlocks(color, from);
            for (int i = 0; i < list.Count; i++)
            {
                PixelCube c = list[i];
                if (Mathf.Abs(c.GridX - from.Item1) + Mathf.Abs(c.GridY - from.Item2) <= m_StackMaxJumpCells) return true;
            }
            return false;
        }

        /// <summary>
        /// Denizcinin bulunduğu boş hücreden hedef küpün komşusu olan boş bir hücreye, yalnızca boş hücrelerden geçen
        /// kısa yol (4 yönlü BFS). Rezerve edilmiş küpler de tahtada durduğu için engeldir. Yol en fazla
        /// m_StackMaxJumpCells * 3 adımsa döner: [ara noktalar..., küpün önündeki duruş noktası].
        /// </summary>
        private bool TryLocalRunnerPath(Vector3 here, PixelCube target, float groundZ, out List<Vector3> path)
        {
            path = null;
            if (target == null || !EnsureGridFrame()) return false;
            if (!TryBuildLiveGrid(out var gridMap, out _, out _, false)) return false;

            var start = m_GridFrame.ToGrid(here);
            if (gridMap.ContainsKey(start)) return false;   // bir küpün üstünde duruyorsa güvenli değil
            var goalCube = (target.GridX, target.GridY);
            int maxSteps = Mathf.Max(2, m_StackMaxJumpCells * 3);
            var dirs = new (int, int)[] { (0, -1), (-1, 0), (1, 0), (0, 1) };

            var cameFrom = new Dictionary<(int, int), (int, int)> { [start] = start };
            var steps = new Dictionary<(int, int), int> { [start] = 0 };
            var queue = new Queue<(int, int)>();
            queue.Enqueue(start);
            (int, int)? goal = null;
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (Mathf.Abs(cur.Item1 - goalCube.Item1) + Mathf.Abs(cur.Item2 - goalCube.Item2) == 1) { goal = cur; break; }
                if (steps[cur] >= maxSteps) continue;
                foreach (var d in dirs)
                {
                    var n = (cur.Item1 + d.Item1, cur.Item2 + d.Item2);
                    if (cameFrom.ContainsKey(n) || gridMap.ContainsKey(n)) continue;
                    cameFrom[n] = cur;
                    steps[n] = steps[cur] + 1;
                    queue.Enqueue(n);
                }
            }
            if (!goal.HasValue) return false;

            var cells = new List<(int, int)>();
            for (var c = goal.Value; c != start; c = cameFrom[c]) cells.Add(c);
            cells.Reverse();

            Vector3 G((int, int) c) { Vector3 w = m_GridFrame.ToWorld(c.Item1, c.Item2); w.z = groundZ; return w; }
            path = new List<Vector3>(cells.Count + 1);
            for (int i = 0; i < cells.Count - 1; i++) path.Add(G(cells[i]));
            // Son hücre yerine küpün önündeki duruş noktası (küpe dönük, m_PickupStandoffCubes geride)
            Vector3 cubeOnGround = G(goalCube);
            Vector3 approach = cells.Count > 0 ? G(cells[cells.Count - 1]) : here;
            Vector3 toCube = cubeOnGround - approach;
            float standoff = m_GridFrame.Pitch * m_PickupStandoffCubes;
            path.Add(toCube.sqrMagnitude > 1e-8f ? cubeOnGround - toCube.normalized * standoff : approach);
            return true;
        }

        /// <summary>
        /// Yığın taşıyan bir denizcinin görevi: konteynerde ayırdığı ama henüz küpünü seçmediği yerler (Unassigned) ve
        /// kendisine sırayla ayrılmış, henüz alınmamış küpler (Targets, rezerve). Serial çıkış sırasıdır.
        /// </summary>
        private class StackClaim
        {
            public int Serial;
            public ShipController Ship;
            public Color Color;
            public int Unassigned;
            public (int, int) Last;   // en son ayrılan küpün hücresi: sonraki küp buna bitişik/en yakın seçilir
            public float WaitingSince = -1f;   // yakında küp yokken beklemeye başladığı an (uzağa gitmeden önce bekler)
            public bool Collecting;   // denizci ilk küpünü aldı: sıradaki küp ancak bundan sonra, bulunduğu yere göre ayrılır
            public PixelCube Current;   // şu an yürüdüğü (alacağı) küp; daha yakın bir denizci boşa çıkarsa ona devredilebilir
            public Transform Runner;
            public readonly Queue<PixelCube> Targets = new Queue<PixelCube>();
        }

        private readonly List<StackClaim> m_StackClaims = new List<StackClaim>();
        private int m_StackClaimSerial;

        /// <summary>
        /// Açık olan sıradaki küpleri, görevi dolmamış denizcilere dağıtır. Her denizciye aynı anda yalnızca bir küp
        /// ayrılır (o an yürüdüğü/alacağı küp); gerisi o küpü aldıktan sonra, o anki tahtaya göre seçilir. Böylece
        /// sonradan açılan ya da önüne düşen küp, baştan başka denizciye ayrılmış olduğu için atlanmaz.
        /// Seçim: aynı renkte hedefi olmayan denizciler ve açık küpler arasında ızgarada en yakın çift önce eşleşir
        /// (eşitlikte eski çıkan denizci, sonra toplama sırası). Denizcinin ilk küpü öncelik sırasıyla (en alt, en dış)
        /// seçilmiştir. Her küp tek denizciye ayrılır; sonuç belirlidir.
        /// </summary>
        private void AllocateStackClaims()
        {
            if (m_StackClaims.Count == 0) return;
            m_StackClaims.Sort((a, b) => a.Serial.CompareTo(b.Serial));

            var frontiers = new List<KeyValuePair<Color, List<PixelCube>>>();
            var groups = new List<List<StackClaim>>();
            for (int i = 0; i < m_StackClaims.Count; i++)
            {
                StackClaim claim = m_StackClaims[i];
                if (!NeedsStackTarget(claim)) continue;

                int fi = -1;
                for (int f = 0; f < frontiers.Count; f++)
                    if (ColorsMatch(frontiers[f].Key, claim.Color)) { fi = f; break; }
                if (fi < 0)
                {
                    frontiers.Add(new KeyValuePair<Color, List<PixelCube>>(claim.Color, GetNextCollectibleBlocks(claim.Color)));
                    groups.Add(new List<StackClaim>());
                    fi = frontiers.Count - 1;
                }
                groups[fi].Add(claim);
            }

            for (int g = 0; g < groups.Count; g++)
            {
                List<PixelCube> list = frontiers[g].Value;
                List<StackClaim> waiting = groups[g];

                // 1. Yakın eşleşmeler: atlama sınırı içindeki en kısa denizci–küp çifti önce. Başka bir denizcinin
                // yürüdüğü küp de aday: bu denizci ona belirgin şekilde daha yakınsa küp ona geçer, öbürü yeniden aranır
                while (waiting.Count > 0)
                {
                    int bestClaim = -1, bestCube = -1, bestDist = int.MaxValue;
                    StackClaim bestOwner = null;
                    for (int w = 0; w < waiting.Count; w++)
                    {
                        StackClaim claim = waiting[w];
                        for (int k = 0; k < list.Count; k++)
                        {
                            PixelCube c = list[k];
                            if (c == null || c.IsPopped || s_ReservedCubes.Contains(c)) continue;
                            int d = GridDist(c, claim.Last);
                            // waiting seri sırasında, liste öncelik sırasında: eşitlikte öndekiler kalır
                            if (d < bestDist) { bestDist = d; bestClaim = w; bestCube = k; bestOwner = null; }
                        }
                        for (int o = 0; o < m_StackClaims.Count; o++)
                        {
                            StackClaim owner = m_StackClaims[o];
                            if (owner == claim || owner == null || owner.Current == null || owner.Current.IsPopped) continue;
                            if (!ColorsMatch(owner.Color, claim.Color)) continue;
                            int d = GridDist(owner.Current, claim.Last);
                            if (d >= bestDist || d > m_StackMaxJumpCells) continue;
                            if (d + m_StackStealMarginCells > OwnerDistance(owner)) continue;
                            bestDist = d; bestClaim = w; bestCube = -1; bestOwner = owner;
                        }
                    }
                    if (bestClaim < 0 || bestDist > m_StackMaxJumpCells) break;
                    StackClaim taker = waiting[bestClaim];
                    waiting.RemoveAt(bestClaim);
                    if (bestOwner != null)
                    {
                        PixelCube stolen = bestOwner.Current;
                        RobStackTarget(bestOwner);
                        GiveStackTarget(taker, stolen);
                        if (NeedsStackTarget(bestOwner)) waiting.Add(bestOwner);   // soyulan denizci de aynı turda yeni küp arar
                    }
                    else
                    {
                        AssignStackTarget(taker, list, bestCube);
                    }
                }

                // 2. Uzak küp: yalnızca konteynerin son denizcisi, yakınında küp açılmasını bir süre bekledikten sonra
                // alır; tahtanın köşesine değil, bulunduğu yere en yakın boştaki küpe gider.
                // Yakınında henüz açılmamış aynı renk küp yoksa orada yeni küp açılamaz: beklemeden gider
                for (int w = 0; w < waiting.Count; w++)
                {
                    StackClaim claim = waiting[w];
                    if (!JumpLimitRelaxed(claim)) continue;
                    bool mayOpenNearby = HasHiddenNearby(claim.Color, claim.Last, list);
                    if (mayOpenNearby && (claim.WaitingSince < 0f || Time.time - claim.WaitingSince < m_StackFarJumpDelay)) continue;
                    int best = -1, bestDist = int.MaxValue;
                    for (int k = 0; k < list.Count; k++)
                    {
                        PixelCube c = list[k];
                        if (c == null || c.IsPopped || s_ReservedCubes.Contains(c)) continue;
                        int d = GridDist(c, claim.Last);
                        if (d < bestDist) { bestDist = d; best = k; }
                    }
                    if (best >= 0) AssignStackTarget(claim, list, best);
                }
            }
        }

        private static int GridDist(PixelCube c, (int, int) cell)
        {
            return Mathf.Abs(c.GridX - cell.Item1) + Mathf.Abs(c.GridY - cell.Item2);
        }

        /// <summary>Denizcinin bulunduğu yerden şu an yürüdüğü küpe kalan ızgara mesafesi.</summary>
        private int OwnerDistance(StackClaim owner)
        {
            if (owner.Runner == null || !EnsureGridFrame()) return 0;   // konumu bilinmiyorsa elinden alınmaz
            return GridDist(owner.Current, m_GridFrame.ToGrid(owner.Runner.position));
        }

        /// <summary>Denizcinin yürüdüğü küpü elinden alır: yeri yeniden hedefsiz sayılır, bulunduğu yerden yeni küp aranır.</summary>
        private void RobStackTarget(StackClaim owner)
        {
            if (owner.Runner != null && EnsureGridFrame()) owner.Last = m_GridFrame.ToGrid(owner.Runner.position);
            owner.Current = null;
            owner.Unassigned++;
            owner.Ship.AddUnassignedQuota(1);
        }

        /// <summary>Zaten rezerve olan küpü (başka denizciden devralınan) bu denizciye verir.</summary>
        private void GiveStackTarget(StackClaim claim, PixelCube cube)
        {
            claim.WaitingSince = -1f;
            claim.Targets.Enqueue(cube);
            claim.Last = (cube.GridX, cube.GridY);
            claim.Unassigned--;
            claim.Ship.ConsumeUnassignedQuota(1);
        }

        /// <summary>
        /// Verilen hücreye atlama sınırı içinde, bu renkte henüz alınamayan (açılmamış) ve kimseye ayrılmamış küp var mı?
        /// Yoksa denizcinin yakınında yeni küp açılamaz; beklemek boşunadır. freeList: o an alınabilir küpler.
        /// </summary>
        private bool HasHiddenNearby(Color color, (int, int) from, List<PixelCube> freeList)
        {
            var all = PixelCube.ActiveCubes;
            if (all == null) return false;
            var free = new HashSet<PixelCube>(freeList);
            for (int i = 0; i < all.Count; i++)
            {
                PixelCube c = all[i];
                if (c == null || c.IsPopped || !c.gameObject.activeSelf || s_ReservedCubes.Contains(c) || free.Contains(c)) continue;
                if (!ColorsMatch(c.CurrentColor, color) && !ColorsMatch(c.OriginalColor, color)) continue;
                if (Mathf.Abs(c.GridX - from.Item1) + Mathf.Abs(c.GridY - from.Item2) <= m_StackMaxJumpCells) return true;
            }
            return false;
        }

        /// <summary>Görevinde yer kalmış ve şu an yürüdüğü/alacağı ayrılmış küpü olmayan denizci.</summary>
        private static bool NeedsStackTarget(StackClaim claim)
        {
            return claim != null && claim.Collecting && claim.Unassigned > 0 && claim.Targets.Count == 0 && claim.Current == null
                   && claim.Ship != null && !claim.Ship.IsDeparting;
        }

        private void AssignStackTarget(StackClaim claim, List<PixelCube> list, int index)
        {
            PixelCube cube = list[index];
            list.RemoveAt(index);
            claim.WaitingSince = -1f;
            s_ReservedCubes.Add(cube);
            claim.Targets.Enqueue(cube);
            claim.Last = (cube.GridX, cube.GridY);
            claim.Unassigned--;
            claim.Ship.ConsumeUnassignedQuota(1);
        }

        /// <summary>Görevi biten/iptal olan denizcinin hedefsiz yerlerini ve alınmamış küp rezervasyonlarını bırakır.</summary>
        private void ReleaseStackClaim(StackClaim claim)
        {
            if (claim == null) return;
            if (claim.Ship != null) claim.Ship.ConsumeUnassignedQuota(claim.Unassigned);
            claim.Unassigned = 0;
            while (claim.Targets.Count > 0)
            {
                PixelCube cube = claim.Targets.Dequeue();
                if (cube != null) s_ReservedCubes.Remove(cube);
            }
            claim.Current = null;
            m_StackClaims.Remove(claim);
        }

        /// <summary>
        /// Çerçeve koridorundaki bir noktadan, çıkış hücresinin çerçeve kenarındaki izdüşümüne çerçeve boyunca giden
        /// rota; saat yönü ile tersinden hangisi kısaysa o yön seçilir, aradaki köşeler eklenir.
        /// Kenarlar FrameSide ile aynı numaralı: 0 alt, 1 sol, 2 sağ, 3 üst.
        /// </summary>
        /// <param name="preferDir">+1 saat yönünün tersi, -1 saat yönü, 0 tercih yok. Tercih edilen yön en kısasından
        /// en fazla <paramref name="tolerance"/> kat uzunsa o yön seçilir (ör. geldiği yoldan dönmek).</param>
        private static List<Vector3> PerimeterRoute(Vector3 from, Vector3 exitWorld, int exitSide,
            float fL, float fR, float fB, float fT, float groundZ, int preferDir = 0, float tolerance = 1.25f)
        {
            float W = Mathf.Max(1e-3f, fR - fL), H = Mathf.Max(1e-3f, fT - fB), L = 2f * (W + H);
            Vector3 P(float x, float y) => new Vector3(x, y, groundZ);

            Vector3 Project(Vector3 q, int side)
            {
                switch (side)
                {
                    case 0: return P(Mathf.Clamp(q.x, fL, fR), fB);
                    case 1: return P(fL, Mathf.Clamp(q.y, fB, fT));
                    case 2: return P(fR, Mathf.Clamp(q.y, fB, fT));
                    default: return P(Mathf.Clamp(q.x, fL, fR), fT);
                }
            }
            // Saat yönünün tersine çevre koordinatı (sol alt köşe = 0)
            float T(Vector3 q, int side)
            {
                switch (side)
                {
                    case 0: return q.x - fL;
                    case 2: return W + (q.y - fB);
                    case 3: return W + H + (fR - q.x);
                    default: return 2f * W + H + (fT - q.y);
                }
            }
            float Mod(float v) => ((v % L) + L) % L;

            // Başlangıcın en yakın kenarı
            float cx = Mathf.Clamp(from.x, fL, fR), cy = Mathf.Clamp(from.y, fB, fT);
            float[] dist = { Mathf.Abs(from.y - fB) + Mathf.Abs(from.x - cx), Mathf.Abs(from.x - fL) + Mathf.Abs(from.y - cy),
                             Mathf.Abs(from.x - fR) + Mathf.Abs(from.y - cy), Mathf.Abs(from.y - fT) + Mathf.Abs(from.x - cx) };
            int startSide = 0;
            for (int i = 1; i < 4; i++) if (dist[i] < dist[startSide]) startSide = i;

            Vector3 a = Project(from, startSide), b = Project(exitWorld, exitSide);
            float ta = T(a, startSide), tb = T(b, exitSide);
            float ccw = Mod(tb - ta);
            bool goCcw = ccw <= L * 0.5f;
            if (preferDir != 0)
            {
                float preferred = preferDir > 0 ? ccw : L - ccw;
                if (preferred <= Mathf.Min(ccw, L - ccw) * tolerance) goCcw = preferDir > 0;
            }
            float span = goCcw ? ccw : L - ccw;

            var corners = new List<(float d, Vector3 p)>(4);
            void Corner(float tc, Vector3 p)
            {
                float d = goCcw ? Mod(tc - ta) : Mod(ta - tc);
                if (d > 1e-4f && d < span - 1e-4f) corners.Add((d, p));
            }
            Corner(0f, P(fL, fB)); Corner(W, P(fR, fB)); Corner(W + H, P(fR, fT)); Corner(2f * W + H, P(fL, fT));
            corners.Sort((x, y) => x.d.CompareTo(y.d));

            var r = new List<Vector3>(6) { P(from.x, from.y), a };
            foreach (var c in corners) r.Add(c.p);
            r.Add(b);
            return r;
        }

        /// <summary>
        /// Kıyı → küp rotasının çerçeve üzerinde ilk ilerlediği yön: +1 saat yönünün tersi, -1 saat yönü, 0 belirsiz
        /// (ör. küp kıyının tam karşısında, çerçevede yan yürüyüş yok).
        /// </summary>
        private static int ShoreRouteFrameDirection(List<Vector3> route, float fL, float fR, float fB, float fT)
        {
            const float eps = 1e-3f;
            float W = Mathf.Max(1e-3f, fR - fL), H = Mathf.Max(1e-3f, fT - fB), L = 2f * (W + H);
            float? T(Vector3 q)
            {
                bool inX = q.x >= fL - eps && q.x <= fR + eps, inY = q.y >= fB - eps && q.y <= fT + eps;
                if (Mathf.Abs(q.y - fB) < eps && inX) return q.x - fL;
                if (Mathf.Abs(q.x - fR) < eps && inY) return W + (q.y - fB);
                if (Mathf.Abs(q.y - fT) < eps && inX) return W + H + (fR - q.x);
                if (Mathf.Abs(q.x - fL) < eps && inY) return 2f * W + H + (fT - q.y);
                return null;
            }
            float? first = null;
            foreach (var q in route)
            {
                float? t = T(q);
                if (!t.HasValue) { if (first.HasValue) break; continue; }   // çerçeveden ayrıldı (iç hücreler)
                if (!first.HasValue) { first = t; continue; }
                float d = ((t.Value - first.Value) % L + L) % L;
                if (d < 1e-3f || d > L - 1e-3f) continue;
                return d <= L * 0.5f ? 1 : -1;
            }
            return 0;
        }

        /// <summary>
        /// Denizcinin bulunduğu yerden, son küpe geldiği rotayı (kıyı → küp) tersten izleyerek çerçeve koridoruna
        /// çıkış: rotanın çerçeve çizgisi üzerindeki son noktasına kadar. Sonuç [here, ..., çerçeve noktası].
        /// </summary>
        private static List<Vector3> ExitToFrame(Vector3 here, List<Vector3> toPrev, float fL, float fR, float fB, float fT)
        {
            const float eps = 1e-3f;
            var r = new List<Vector3> { here };
            for (int i = toPrev.Count - 1; i >= 0; i--)
            {
                Vector3 q = toPrev[i];
                if ((q - here).sqrMagnitude > 1e-6f) r.Add(q);
                bool onFrame = (Mathf.Abs(q.y - fB) < eps || Mathf.Abs(q.y - fT) < eps) && q.x >= fL - eps && q.x <= fR + eps
                            || (Mathf.Abs(q.x - fL) < eps || Mathf.Abs(q.x - fR) < eps) && q.y >= fB - eps && q.y <= fT + eps;
                if (onFrame) break;
            }
            return r;
        }

        /// <summary>İki rotayı uç uca ekler; tekrarlanan ve aynı çizgi üzerindeki ara noktaları (geri dönüşleri) siler.</summary>
        private static List<Vector3> JoinRunnerLegs(List<Vector3> first, List<Vector3> second)
        {
            var route = new List<Vector3>(first.Count + second.Count);
            route.AddRange(first);
            route.AddRange(second);

            for (int i = route.Count - 1; i > 0; i--)
                if ((route[i] - route[i - 1]).sqrMagnitude < 1e-6f) route.RemoveAt(i);

            const float eps = 1e-3f;
            bool changed = true;
            while (changed && route.Count > 2)
            {
                changed = false;
                for (int i = 1; i < route.Count - 1; i++)
                {
                    Vector3 a = route[i - 1], b = route[i], c = route[i + 1];
                    bool sameRow = Mathf.Abs(a.y - b.y) < eps && Mathf.Abs(b.y - c.y) < eps;
                    bool sameCol = Mathf.Abs(a.x - b.x) < eps && Mathf.Abs(b.x - c.x) < eps;
                    if (sameRow || sameCol) { route.RemoveAt(i); changed = true; break; }
                }
            }
            return route;
        }

        private IEnumerator SingleRunnerMissionRoutine(
            int indexInQueue,
            int totalInGroup,
            PixelCube targetCube,
            ShipController ship,
            Vector3 shoreTargetAtLaunch,
            Vector3 otMin,
            Vector3 otMax,
            Vector3[] otCorners,
            float groundPlaneZ,
            int batchQuota = 1,
            StackClaim claim = null)
        {
            // quota: bu denizci için konteynerde ayrılmış yer sayısı (yığın taşımada ilk küp + yolda toplanacaklar)
            int quota = Mathf.Max(1, batchQuota);
            var pickedCubes = new List<PixelCube>(quota);
            if (ship == null) yield break;
            if (targetCube == null)
            {
                { ReleaseStackClaim(claim); for (int q = 0; q < quota; q++) ship.ReleaseCargoReservation(); }
                m_ActiveCargoFlightCount = Mathf.Max(0, m_ActiveCargoFlightCount - quota);
                yield break;
            }

            GameObject runnerPrefabToUse = GetRunnerPrefab();

            bool isUiContainer = ship != null && ship.IsUiContainer;
            bool entersLeft = (indexInQueue % 2 == 0);
            Vector3 deckOffset = new Vector3(entersLeft ? -0.06f : 0.06f, 0.16f, 0.02f);
            Vector3 shipDeckPos = ship.transform.position + deckOffset;
            Vector3 shorePoint = GetShorePoint(ship);
            shorePoint.z = groundPlaneZ;

            // UI konteynerler için hafif sağ/sol ayrımı (ardışık gemiciler tam üst üste binmesin)
            if (isUiContainer)
            {
                shorePoint.x += (entersLeft ? -0.08f : 0.08f);
            }

            // Jenerik Instantiate<GameObject> burada InvalidCastException atıyordu (koşucu hiç doğmuyor,
            // rezervasyon da düşmediği için gemi slotta takılı kalıyordu). Jenerik olmayan sürümle
            // klonla ve dönen nesneden GameObject'i güvenle çıkar.
            GameObject runner = null;
            if (runnerPrefabToUse != null)
            {
                Vector3 spawnPos = isUiContainer ? shorePoint : shipDeckPos;
                Object clone = Instantiate((Object)runnerPrefabToUse, spawnPos, Quaternion.identity);
                runner = clone as GameObject;
                if (runner == null && clone is Component cloneComp) runner = cloneComp.gameObject;
            }

            if (runner == null)
            {
                Debug.LogError($"[ShipDispatcher] Koşucu oluşturulamadı (prefab: {(runnerPrefabToUse != null ? runnerPrefabToUse.name : "null")}). Rezervasyon geri bırakılıyor.");
                ReleaseReservedCubes(targetCube, pickedCubes);
                { ReleaseStackClaim(claim); for (int q = 0; q < quota; q++) ship.ReleaseCargoReservation(); }
                m_ActiveCargoFlightCount = Mathf.Max(0, m_ActiveCargoFlightCount - quota);
                yield break;
            }
            runner.name = $"Runner_{indexInQueue}_{targetCube.name}";
            m_LiveRunners.Add(runner);

            // Mor renkli veya istenmeyen sahte gölge (CubeShadow, WalkFootstepShadow vb.) kalıntılarını tamamen yok et
            for (int i = runner.transform.childCount - 1; i >= 0; i--)
            {
                Transform ch = runner.transform.GetChild(i);
                if (ch != null && ch.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0 && !ch.name.StartsWith("Sailor_"))
                {
                    ch.gameObject.SetActive(false);
                    Destroy(ch.gameObject);
                }
            }

            // Boyutlar birebir aynı:
            Vector3 refScale = (targetCube != null && targetCube.transform.lossyScale.sqrMagnitude > 1e-4f)
                ? targetCube.transform.lossyScale
                : (runnerPrefabToUse != null ? runnerPrefabToUse.transform.localScale : Vector3.one);
            runner.transform.localScale = refScale * m_RunnerSizeVsCube;
            // Denizci: elindeki küp panodaki küple aynı boyda görünsün (karakter de ona orantılı büyür).
            // Prefabdaki ModelPivot 1/1.3 ölçekli; kök m_RunnerSizeVsCube ile çarpılınca 1 model birimi = 1 küp oluyor.
            if (runner.GetComponent<SailorRunnerVisual>() != null)
                runner.transform.localScale = refScale * (m_RunnerSizeVsCube * m_SailorSizeVsCube);

            // Dik ve düz dursun: panodaki küplerle aynı duruş, yürürken yana dönme/yatma yok.
            Quaternion upright = targetCube != null ? targetCube.transform.rotation : Quaternion.identity;
            RunnerLegUpright legUpright = runner.GetComponent<RunnerLegUpright>();
            if (legUpright != null) legUpright.LockUpright(upright);
            else runner.transform.rotation = upright;

            // Ayaklar kumun içine gömülmesin: koşucunun en derin noktası (ayak tabanı) zemin düzlemine
            // değecek şekilde yol z'sini kameraya doğru kaydır. Ayak izleri yine zemin düzleminde kalır.
            float runnerPathZ = groundPlaneZ - RunnerGroundDepth(runner);
            shorePoint.z = runnerPathZ;
            Vector3 containerGroundPos = shorePoint;
            if (isUiContainer && runner != null)
            {
                runner.transform.position = containerGroundPos;
            }

            // Koşucu karakter kesinlikle toplanan küpün / ait olduğu geminin renginde:
            Color runnerColor = targetCube != null ? targetCube.CurrentColor : ship.ShipColor;
            ApplyColorToRunner(runner, runnerColor);
            SailorRunnerVisual sailorSetup = runner.GetComponent<SailorRunnerVisual>();
            if (sailorSetup != null)
            {
                sailorSetup.SetupFakeShadow(groundPlaneZ);
            }

            PixelCube pc = runner.GetComponent<PixelCube>();
            if (pc != null)
            {
                Destroy(pc); // Koşucu sahada koşan karakterdir, tahtadaki küp listesine (ActiveCubes) girmemeli!
            }

            CubeMovementSettings s = MovementSettings;
            CubeMovementController motion = runner.GetComponent<CubeMovementController>();
            if (motion == null) motion = runner.AddComponent<CubeMovementController>();
            // Ayak izleri panodaki küpün rengiyle birebir aynı olsun
            motion.CubeColor = targetCube != null ? targetCube.CurrentColor : runnerColor;
            motion.SetGroundPlaneZ(groundPlaneZ);
            // Denizcinin gövde hareketini animasyon yapıyor: kod tarafındaki bob/esneme/yatma kapalı
            motion.PositionOnly = runner.GetComponent<SailorRunnerVisual>() != null;

            ICargoRunner cargoRunner = runner.GetComponent<ICargoRunner>();
            WaddleRunner waddle = runner.GetComponent<WaddleRunner>();
            WalkingCargoVisual visual = runner.GetComponent<WalkingCargoVisual>();
            SailorRunnerVisual sailorVis = runner.GetComponent<SailorRunnerVisual>();
            if (isUiContainer)
            {
                if (sailorVis != null)
                {
                    sailorVis.SetRingVisible(true);
                    sailorVis.Play(SailorRunnerVisual.StateRunEmpty);
                }
            }
            else
            {
                if (sailorVis != null) { sailorVis.Play(SailorRunnerVisual.StateHopDown, 0f); sailorVis.SetRingVisible(false); }
            }

            // Phase 1: Hop from ship deck onto shore
            // Kullanıcı isteği: Sayılar blok girince değil, içinden kişiler/işçiler çıkınca azalsın
            if (ship != null)
            {
                ship.OnWorkerExited();
            }

            Vector3 camUp = ShipController.MainCamera != null ? ShipController.MainCamera.transform.up : Vector3.up;

            if (isUiContainer)
            {
                // Kullanıcı isteği: Konteynerlar slotlara yerleştiğinde gemiciler direkt çerçeve içine doğru
                // zıplamasın; bulundukları yerden (konteynerdan) hedeflere yürüyerek gitsinler.
                runner.transform.position = containerGroundPos;
                if (waddle != null) waddle.IsAirborne = false;
                if (HypercasualFeedbackManager.Instance != null)
                {
                    HypercasualFeedbackManager.Instance.TriggerHapticLight();
                }
            }
            else
            {
                float hopDuration = Mathf.Max(0.18f, m_HopDuration);
                Vector3 hopCtrl = (shipDeckPos + shorePoint) * 0.5f + camUp * m_HopArcHeight;

                ShipController.SpawnWaterRipple(shipDeckPos, 0.20f, 0.6f, 0.35f);

                float tHop = 0f;
                Vector3 hopPrevPos = shipDeckPos;
                while (tHop < hopDuration && runner != null)
                {
                    tHop += Time.deltaTime;
                    float u = Mathf.Clamp01(tHop / hopDuration);
                    float ease = Mathf.SmoothStep(0f, 1f, u);
                    Vector3 pos = QuadraticBezier(shipDeckPos, hopCtrl, shorePoint, ease);
                    runner.transform.position = pos;

                    if (u > 0.2f && u < 0.85f)
                    {
                        if (waddle != null) waddle.IsAirborne = true;
                        if (visual != null) visual.SetAirborne(Time.deltaTime);
                    }

                    Vector3 moveDir = pos - hopPrevPos;
                    hopPrevPos = pos;
                    if (moveDir.sqrMagnitude > 1e-5f)
                    {
                        motion.SteerToward(moveDir.normalized, 0f, Time.deltaTime);
                    }
                    yield return null;
                }

                if (runner == null) yield break;
                runner.transform.position = shorePoint;
                if (waddle != null) waddle.IsAirborne = false;
                if (sailorVis != null)
                {
                    sailorVis.SetRingVisible(true);
                    StartCoroutine(RunnerLandingSquashRoutine(runner.transform, m_RunnerLandingSquash, 0.22f));
                }

                if (HypercasualFeedbackManager.Instance != null)
                {
                    HypercasualFeedbackManager.Instance.TriggerHapticLight();
                }
            }

            // Phase 2: Run along OtCerceve to target container
            Vector3 cubeWorldPos = targetCube.transform.position;
            List<Vector3> forwardWaypoints = BuildGridAwareRunnerWaypoints(shorePoint, targetCube, otMin, otMax, otCorners, runnerPathZ);
            // Yığın toplayan denizci dönüşte geldiği yoldan dönsün diye gidişte çerçevede hangi yöne yürüdüğü
            GetRunnerFrameRect(runnerPathZ, out float ofL, out float ofR, out float ofB, out float ofT);
            int outboundFrameDir = ShoreRouteFrameDirection(forwardWaypoints, ofL, ofR, ofB, ofT);
            float runnerCornerRadius = m_GridFrameValid ? Mathf.Min(0.22f, m_GridFrame.Pitch * 0.45f) : 0.22f;
            ShoreLanePath forwardPath = ShoreLanePath.BuildFilleted(forwardWaypoints, runnerCornerRadius, 8);

            float dist = 0f;
            // Her denizci hafif farklı hızda: hepsi aynı ritimde yürümesin
            float cruiseSpeed = m_RunnerSpeed * (1f + UnityEngine.Random.Range(-m_RunnerSpeedJitter, m_RunnerSpeedJitter));
            float pathLen = forwardPath.Length;
            float runPitch = m_GridFrameValid ? Mathf.Max(0.05f, m_GridFrame.Pitch) : 0.3f;
            float runElapsed = 0f;
            EnsureBoardBounds();
            var agent = new RunnerAgent { T = runner.transform, Id = runner.GetInstanceID(), Active = true };
            m_RunnerAgents.Add(agent);

            motion.BeginRopeMotion(s, indexInQueue, cruiseSpeed, 0f, forwardPath.TangentAtDistance(0f));
            if (sailorVis != null) sailorVis.Play(SailorRunnerVisual.StateRunEmpty);

            while (dist < pathLen && runner != null && targetCube != null)
            {
                float dt = Time.deltaTime;
                if (dt <= 0f) { yield return null; continue; }

                runElapsed += dt;
                agent.Moving = true;
                // Yola çıkarken hızlan, küpün önüne varırken yavaşla (son yerleşme kısa ve kontrollü);
                // aynı yöne giden birinin hemen arkasındaysa onun içine girmemek için yavaşla
                dist += cruiseSpeed * RunnerSpeedFactor(runElapsed, pathLen - dist, runPitch * m_RunnerArriveSlowCubes, m_RunnerArriveMinSpeed)
                        * RunnerFollowFactor(agent, runPitch) * dt;
                float clampedDist = Mathf.Min(dist, pathLen);
                Vector3 p = forwardPath.PointAtDistance(clampedDist);
                // Yakındaki denizcilerden yana açıl; kıyıdan çıkarken ve küpe varırken itme kapanır
                float sepFade = Mathf.Clamp01(clampedDist / (0.25f * runPitch)) * Mathf.Clamp01((pathLen - clampedDist - 0.3f * runPitch) / (1.2f * runPitch));
                p = ApplyRunnerSeparation(agent, p, forwardPath.TangentAtDistance(clampedDist), sepFade, runPitch, dt);
                motion.ApplyRopeFrame(p, 0f, Vector3.zero, camUp, dt);

                yield return null;
            }

            if (runner == null || targetCube == null)
            {
                ReleaseReservedCubes(targetCube, pickedCubes);
                if (ship != null)
                {
                    { ReleaseStackClaim(claim); for (int q = 0; q < quota; q++) ship.ReleaseCargoReservation(); }
                    ship.OnWorkerMissionAborted();
                }
                m_ActiveCargoFlightCount = Mathf.Max(0, m_ActiveCargoFlightCount - quota);
                // Hedef küp kayboldu (ör. bölüm yeniden başladı): koşucu sahnede asılı kalmasın
                if (runner != null) { m_LiveRunners.Remove(runner); Destroy(runner); }
                yield break;
            }

            // Phase 3: Pick up cargo container
            agent.Moving = false;   // küpü alırken duruyor: arkadakiler beklemesin, yanından geçsin
            // Orta bölgedeki / dikey sütun küplerinde denizci küpün önünde tam dik dursun ve yerinde hafif dönme/titreme yapmasın
            EnsureBoardBounds();
            float boardCenterX = (m_BoardMinX + m_BoardMaxX) * 0.5f;
            float boardHalfSpan = Mathf.Max(0.5f, (m_BoardMaxX - m_BoardMinX) * 0.5f);
            float xDistFromCenter = Mathf.Abs(targetCube.transform.position.x - boardCenterX);
            bool isMiddleArea = xDistFromCenter <= boardHalfSpan * 0.75f;
            Vector3 toCubeVec = targetCube.transform.position - runner.transform.position;
            toCubeVec.z = 0f;
            bool isVerticalApproach = Mathf.Abs(toCubeVec.y) >= Mathf.Abs(toCubeVec.x) * 0.85f;
            bool isMiddlePickup = isMiddleArea && isVerticalApproach;

            if (sailorVis != null && isMiddlePickup)
            {
                Vector3 middleFaceDir = toCubeVec.y >= 0f ? Vector3.up : Vector3.down;
                sailorVis.SnapFacing(middleFaceDir);
            }

            if (HypercasualFeedbackManager.Instance != null)
            {
                // Gecikmesiz: index yalnızca perde çeşitlemesi (sıra numarası oyun boyu büyüdüğü için gecikme saniyelerce birikiyordu)
                HypercasualFeedbackManager.Instance.PlayCubeLiftoffFeedback(targetCube.transform.position, indexInQueue, immediate: true);
            }

            // Yığın taşımada küp ellerdeki yığının ilk katı olur; tek küplükte eski görsel
            CarriedCargoStack cargoStack = quota > 1 ? CreateCarriedCargoStack(targetCube, sailorVis) : null;
            if (cargoStack != null) AddCubeToCargoStack(cargoStack, targetCube);
            else CreateCarriedCargoVisual(targetCube, runner.transform);
            pickedCubes.Add(targetCube);
            if (sailorVis != null) sailorVis.PlayThen(SailorRunnerVisual.StatePickup, SailorRunnerVisual.StateRunCarry);
            targetCube.SetPoppedVisualState(true, regenerateContourShadow: true);
            TriggerWaitingShipsCheck();

            if (runner != null && sailorVis == null)
            {
                runner.transform.DOPunchScale(new Vector3(-0.1f, 0.15f, -0.1f) * runner.transform.localScale.x, 0.15f, 2, 0.5f);
            }

            // Phase 3b: Yığın taşıma — kota dolana kadar sıradaki açık küpe yürü ve onu da yığına ekle.
            // Yalnızca o anda gerçekten açık olan küpler alınır; yoksa kısa bir süre bekler, sonra elindekilerle döner.
            if (claim != null) { claim.Collecting = true; claim.Runner = runner.transform; }
            bool targetHandedOver = false;
            while (cargoStack != null && runner != null && pickedCubes.Count < quota && ship != null && !ship.IsDeparting)
            {
                // Önceki küpü alma animasyonu bitsin (yolda küpü devredip yeniden yönlenirken beklemez)
                if (sailorVis != null && !targetHandedOver)
                {
                    float pickWait = sailorVis.GetClipLength(SailorRunnerVisual.StatePickup) * 0.8f;
                    float tw = 0f;
                    while (tw < pickWait && runner != null) { tw += Time.deltaTime; yield return null; }
                }
                targetHandedOver = false;
                if (runner == null) break;

                // Sıradaki hedef: bu denizciye toplama sırasıyla ayrılmış küp (en alt, en dış).
                PixelCube nextCube = null;
                if (claim != null && runner != null && ship != null && !ship.IsDeparting)
                {
                    AllocateStackClaims();
                    while (nextCube == null && claim.Targets.Count > 0)
                    {
                        PixelCube t = claim.Targets.Dequeue();
                        if (t != null && !t.IsPopped && t.gameObject.activeSelf) { nextCube = t; claim.Current = t; break; }
                        // Hedef kayboldu (ör. bölüm sıfırlandı): yer yeniden hedefsiz sayılır
                        if (t != null) s_ReservedCubes.Remove(t);
                        claim.Unassigned++;
                        ship.AddUnassignedQuota(1);
                    }
                }
                // Kullanıcı kuralı: Panoda bekleme yapılmasın! Sıradaki küp o an hemen açık değilse
                // beklemeden elindekiyle DİREKT DÖNSÜN; boşa çıkan kontenjan için gemiden yeni gemici gönderilsin.
                if (nextCube == null || runner == null) break;

                // Sıradaki küp yakınsa boş hücrelerden doğrudan yürü (çerçeveye çıkıp geri girmesin); değilse
                // bulunduğu yerden çerçeveye çık, çerçeve boyunca kısa yönden sıradaki küpün açıklığına yürü
                Vector3 here = runner.transform.position;
                here.z = runnerPathZ;
                List<Vector3> backOut, toNext;
                if (TryLocalRunnerPath(here, nextCube, runnerPathZ, out List<Vector3> local))
                {
                    backOut = new List<Vector3> { here };
                    toNext = local;
                }
                else
                {
                    GetRunnerFrameRect(runnerPathZ, out float rfL, out float rfR, out float rfB, out float rfT);
                    backOut = ExitToFrame(here, forwardWaypoints, rfL, rfR, rfB, rfT);
                    toNext = BuildGridAwareRunnerWaypoints(shorePoint, nextCube, otMin, otMax, otCorners, runnerPathZ, backOut[backOut.Count - 1]);
                }

                // Dönüş için kıyı → küp rotası da tutulur (son küpten kıyıya tersten yürünür)
                List<Vector3> nextWaypoints = BuildGridAwareRunnerWaypoints(shorePoint, nextCube, otMin, otMax, otCorners, runnerPathZ);
                ShoreLanePath leg = ShoreLanePath.BuildFilleted(JoinRunnerLegs(backOut, toNext), runnerCornerRadius, 8);

                float legDist = 0f;
                float legLen = leg.Length;
                float legElapsed = 0f;
                motion.BeginRopeMotion(s, indexInQueue, cruiseSpeed, 0f, leg.TangentAtDistance(0f));
                if (sailorVis != null)
                {
                    // Orta küpte kilitlenen yön (SnapFacing) yürürken kalırsa denizci yan yan yürüyor: yürüme yönüne dönsün
                    sailorVis.ClearFacingOverride();
                    sailorVis.Play(SailorRunnerVisual.StateRunCarry);
                }
                while (legDist < legLen && runner != null && nextCube != null && !nextCube.IsPopped && claim.Current == nextCube)
                {
                    float dt = Time.deltaTime;
                    if (dt <= 0f) { yield return null; continue; }
                    legElapsed += dt;
                    agent.Moving = true;
                    legDist += cruiseSpeed * RunnerSpeedFactor(legElapsed, legLen - legDist, runPitch * m_RunnerArriveSlowCubes, m_RunnerArriveMinSpeed)
                               * RunnerFollowFactor(agent, runPitch) * dt;
                    float cd = Mathf.Min(legDist, legLen);
                    Vector3 p = leg.PointAtDistance(cd);
                    float sepFade = Mathf.Clamp01(cd / (0.25f * runPitch)) * Mathf.Clamp01((legLen - cd - 0.3f * runPitch) / (1.2f * runPitch));
                    p = ApplyRunnerSeparation(agent, p, leg.TangentAtDistance(cd), sepFade, runPitch, dt);
                    motion.ApplyRopeFrame(p, 0f, Vector3.zero, camUp, dt);
                    yield return null;
                }
                if (runner == null) break;
                // Küp yolda daha yakın bir denizciye devredildi: yer zaten yeniden hedefsiz sayıldı, bulunduğu yerden yeni küp aranır
                if (claim.Current != nextCube) { targetHandedOver = true; continue; }
                if (nextCube == null || nextCube.IsPopped)
                {
                    claim.Current = null;
                    if (nextCube != null) s_ReservedCubes.Remove(nextCube);
                    claim.Unassigned++;
                    ship.AddUnassignedQuota(1);
                    continue;
                }

                agent.Moving = false;
                if (HypercasualFeedbackManager.Instance != null)
                    HypercasualFeedbackManager.Instance.PlayCubeLiftoffFeedback(nextCube.transform.position, indexInQueue, immediate: true);
                claim.Current = null;
                AddCubeToCargoStack(cargoStack, nextCube);
                if (sailorVis != null) sailorVis.PlayThen(SailorRunnerVisual.StatePickup, SailorRunnerVisual.StateRunCarry);
                nextCube.SetPoppedVisualState(true, regenerateContourShadow: true);
                pickedCubes.Add(nextCube);
                targetCube = nextCube;
                forwardWaypoints = nextWaypoints;   // dönüş yolu son alınan küpten kıyıya
                TriggerWaitingShipsCheck();
            }
            // Doldurulamayan kotayı konteynere geri ver: sayı ve kalkış hesabı yalnızca gerçekten taşınan küplerle yürür
            if (pickedCubes.Count < quota && ship != null)
            {
                int unused = quota - pickedCubes.Count;
                ReleaseStackClaim(claim);
                for (int q = 0; q < unused; q++) ship.ReleaseCargoReservation();
                m_ActiveCargoFlightCount = Mathf.Max(0, m_ActiveCargoFlightCount - unused);
                quota = pickedCubes.Count;
                // Boşa çıkan kontenjanı hemen değerlendir: bekleyen gemileri ve sıradaki gemicileri HEMEN tetikle!
                TriggerWaitingShipsCheck();
            }
            ReleaseStackClaim(claim);   // görev doldu ya da bırakıldı: kayıt kapanır (tam doluysa bırakılacak bir şey yoktur)

            // Phase 4: Run back along OtCerceve to shore
            // UI konteynerde yol konteynerin önündeki noktada (containerGroundPos) biter: denizci zıplamadan yürüyerek girer.
            // Konteyner saha içinde yana kaymışsa (ör. linked konteyner yerleşimi için sağa/sola açılmışsa)
            // koşucunun dönüş rotası geminin GÜNCEL slot/kıyı noktasına göre kurulur.
            Vector3 currentShorePoint = GetShorePoint(ship);
            currentShorePoint.z = runnerPathZ;
            if (isUiContainer)
            {
                currentShorePoint.x += (entersLeft ? -0.08f : 0.08f);
            }

            bool containerShifted = Mathf.Abs(currentShorePoint.x - shorePoint.x) > 0.04f;

            List<Vector3> returnWaypoints;
            if ((pickedCubes.Count > 1 || containerShifted) && runner != null)
            {
                // Birden çok küp toplayan veya konteyneri yana kaymış olan denizci:
                // Bulunduğu yerden çerçeveye çık, çerçeve boyunca geminin GÜNCEL kıyı/slot hizasına yürü, oradan konteynere in.
                Vector3 here = runner.transform.position;
                here.z = runnerPathZ;
                GetRunnerFrameRect(runnerPathZ, out float bfL, out float bfR, out float bfB, out float bfT);
                List<Vector3> backOut = ExitToFrame(here, forwardWaypoints, bfL, bfR, bfB, bfT);
                Vector3 shoreOnGround = new Vector3(currentShorePoint.x, currentShorePoint.y, runnerPathZ);
                List<Vector3> round = PerimeterRoute(backOut[backOut.Count - 1], shoreOnGround, 0, bfL, bfR, bfB, bfT, runnerPathZ, -outboundFrameDir);
                round.Add(shoreOnGround);
                returnWaypoints = JoinRunnerLegs(backOut, round);
            }
            else
            {
                returnWaypoints = new List<Vector3>(forwardWaypoints);
                returnWaypoints.Reverse();
            }
            ShoreLanePath returnPath = ShoreLanePath.BuildFilleted(returnWaypoints, runnerCornerRadius, 8);

            // Denizci küpü alırken yerinde durur: orta küplerde dik kalır; diğer küplerde ayara bağlı
            // dönüş yoluna doğru yumuşakça döner, sonra yürür (anında 180° dönüp küpü savurmasın)
            if (sailorVis != null && runner != null)
            {
                float pickupTime = sailorVis.GetClipLength(SailorRunnerVisual.StatePickup);
                float maxWait = m_SailorTurnWhilePickingUp ? pickupTime : Mathf.Min(0.12f, pickupTime * 0.3f);
                Vector3 backDir = returnPath.TangentAtDistance(0f);
                float tPick = 0f;
                while (tPick < maxWait && runner != null)
                {
                    tPick += Time.deltaTime;
                    if (!isMiddlePickup && m_SailorTurnWhilePickingUp && tPick > pickupTime * 0.45f)
                    {
                        sailorVis.SetFacingOverride(backDir);
                    }
                    yield return null;
                }
                if (sailorVis != null) sailorVis.ClearFacingOverride();
            }
            if (runner == null)
            {
                ReleaseReservedCubes(targetCube, pickedCubes);
                if (ship != null)
                {
                    { ReleaseStackClaim(claim); for (int q = 0; q < quota; q++) ship.ReleaseCargoReservation(); }
                    ship.OnWorkerMissionAborted();
                }
                m_ActiveCargoFlightCount = Mathf.Max(0, m_ActiveCargoFlightCount - quota);
                yield break;
            }

            dist = 0f;
            pathLen = returnPath.Length;
            runElapsed = 0f;
            motion.BeginRopeMotion(s, indexInQueue, cruiseSpeed, 0f, returnPath.TangentAtDistance(0f));
            if (sailorVis != null) sailorVis.Play(SailorRunnerVisual.StateRunCarry);

            while (dist < pathLen && runner != null)
            {
                float dt = Time.deltaTime;
                if (dt <= 0f) { yield return null; continue; }

                runElapsed += dt;
                agent.Moving = true;
                // Yükle hızlan; kıyıda zıplamadan önce hafif yavaşla (tamamen durmadan); öndekinin içine girme
                dist += cruiseSpeed * RunnerSpeedFactor(runElapsed, pathLen - dist, runPitch * m_RunnerArriveSlowCubes * 0.6f, 0.65f)
                        * RunnerFollowFactor(agent, runPitch) * dt;
                float clampedDist = Mathf.Min(dist, pathLen);
                Vector3 p = returnPath.PointAtDistance(clampedDist);
                float sepFade = Mathf.Clamp01(clampedDist / (1.2f * runPitch)) * Mathf.Clamp01((pathLen - clampedDist) / (0.25f * runPitch));
                p = ApplyRunnerSeparation(agent, p, returnPath.TangentAtDistance(clampedDist), sepFade, runPitch, dt);
                motion.ApplyRopeFrame(p, 0f, Vector3.zero, camUp, dt);

                yield return null;
            }

            if (runner == null)
            {
                ReleaseReservedCubes(targetCube, pickedCubes);
                if (ship != null)
                {
                    { ReleaseStackClaim(claim); for (int q = 0; q < quota; q++) ship.ReleaseCargoReservation(); }
                    ship.OnWorkerMissionAborted();
                }
                m_ActiveCargoFlightCount = Mathf.Max(0, m_ActiveCargoFlightCount - quota);
                yield break;
            }

            // Phase 5: Hop from shore onto ship deck (UI konteynerde zıplama yok: yürüyüş yolu zaten konteynerde bitti)
            agent.Active = false;
            m_RunnerAgents.Remove(agent);
            Vector3 shoreEnd = runner.transform.position;

            // UI Konteyner için güvenlik: Eğer koşucu dönüş yolundayken konteyner son anda kaymışsa,
            // koşucu konteynerin güncel önüne kadar pürüzsüzce adımlayarak gitsin (eski slotta kalıp yanlış konteynere binmesin).
            if (isUiContainer && runner != null && ship != null)
            {
                Vector3 finalShorePoint = GetShorePoint(ship);
                finalShorePoint.z = runnerPathZ;
                finalShorePoint.x += (entersLeft ? -0.08f : 0.08f);

                float lateralGap = Mathf.Abs(runner.transform.position.x - finalShorePoint.x);
                if (lateralGap > 0.05f)
                {
                    Vector3 walkStart = runner.transform.position;
                    Vector3 walkEnd = new Vector3(finalShorePoint.x, finalShorePoint.y, runnerPathZ);
                    float walkSpeed = Mathf.Max(2.4f, cruiseSpeed);
                    float walkDuration = Mathf.Clamp(lateralGap / walkSpeed, 0.10f, 0.40f);
                    float tWalk = 0f;
                    Vector3 walkDir = (walkEnd - walkStart).normalized;
                    if (walkDir.sqrMagnitude > 1e-4f) motion.SteerToward(walkDir, 0f, 0.02f);
                    if (sailorVis != null) sailorVis.Play(SailorRunnerVisual.StateRunCarry);

                    while (tWalk < walkDuration && runner != null)
                    {
                        float dt = Time.deltaTime;
                        tWalk += dt;
                        float u = Mathf.Clamp01(tWalk / walkDuration);
                        runner.transform.position = Vector3.Lerp(walkStart, walkEnd, Mathf.SmoothStep(0f, 1f, u));
                        yield return null;
                    }
                    if (runner != null) runner.transform.position = walkEnd;
                    shoreEnd = runner != null ? runner.transform.position : walkEnd;
                }
            }

            float returnHopDuration = isUiContainer ? 0f : Mathf.Max(0.2f, m_HopDuration);
            if (sailorVis != null)
            {
                if (!isUiContainer) sailorVis.Play(SailorRunnerVisual.StateHopBoard, 0.05f);
                sailorVis.SetRingVisible(false);
            }
            float tReturnHop = 0f;
            Vector3 prevPos = shoreEnd;

            Vector3 targetDeckNow = ship != null ? ((ship.CurrentSlot != null) ? ship.CurrentSlot.transform.position : ship.transform.position) + deckOffset : shoreEnd;
            float hopArc = m_HopArcHeight;

            while (tReturnHop < returnHopDuration && runner != null)
            {
                float dt = Time.deltaTime;
                tReturnHop += dt;
                float u = Mathf.Clamp01(tReturnHop / returnHopDuration);
                float ease = 1f - (1f - u) * (1f - u);

                Vector3 hopReturnCtrl = (shoreEnd + targetDeckNow) * 0.5f + camUp * hopArc;
                Vector3 p = QuadraticBezier(shoreEnd, hopReturnCtrl, targetDeckNow, ease);
                runner.transform.position = p;

                if (u > 0.35f)
                {
                    if (waddle != null) waddle.IsAirborne = true;
                    if (visual != null) visual.SetAirborne(dt);
                }

                Vector3 moveDir = p - prevPos;
                prevPos = p;
                if (moveDir.sqrMagnitude > 1e-5f)
                {
                    motion.SteerToward(moveDir.normalized, 0f, dt);
                }

                yield return null;
            }

            // Boarding completion & load ship
            if (ship != null)
            {
                ship.AddCargo(Mathf.Max(1, pickedCubes.Count));
                // Tek yükte hafif, art arda yüklerde ve gemi dolunca daha güçlü batma
                float dip = NextShipBoardDip(ship);
                ship.TriggerWaterDipImpact(dip, Mathf.Lerp(0.32f, 0.5f, Mathf.InverseLerp(m_ShipBoardDipRange.x, m_ShipFullDip, dip)));
                ShipController.SpawnWaterRipple(ship.transform.position + new Vector3(0f, -0.05f, 0.05f), 0.28f, 0.95f, 0.45f);
                HypercasualWaterController.TriggerWaterRipple(ship.transform.position, 0.70f, 0.25f);

                HypercasualFeedbackManager.Instance.PlayCubeBoardFeedback(
                    runner != null ? runner.transform.position : ship.transform.position,
                    runnerColor,
                    ship.CurrentCargo,
                    ship.IsFull);
            }

            if (runner != null)
            {
                // Güverteye konarken kısa ezilme, sonra içeri iner
                if (sailorVis != null && !isUiContainer && m_RunnerLandingSquash > 0.001f)
                    yield return RunnerLandingSquashRoutine(runner.transform, m_RunnerLandingSquash, 0.12f);
            }
            if (runner != null)
            {
                float sinkT = 0f;
                Vector3 finalScale = runner.transform.localScale;
                while (sinkT < 0.15f && runner != null)
                {
                    sinkT += Time.deltaTime;
                    float su = Mathf.Clamp01(sinkT / 0.15f);
                    runner.transform.localScale = Vector3.Lerp(finalScale, finalScale * 0.1f, su);
                    yield return null;
                }

                m_LiveRunners.Remove(runner);
                Destroy(runner);
            }

            // İşçi güverteden içeri tamamen bindi (yok oldu):
            if (ship != null)
            {
                ship.OnWorkerBoarded(Mathf.Max(1, pickedCubes.Count));
            }

            ReleaseReservedCubes(targetCube, pickedCubes);
            m_ActiveCargoFlightCount = Mathf.Max(0, m_ActiveCargoFlightCount - quota);
            CheckWinCondition();
        }

        #endregion
    }
}
