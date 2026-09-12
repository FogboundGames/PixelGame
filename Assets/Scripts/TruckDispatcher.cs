using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Kamyon döngüsünün merkezi.
    ///
    /// Akış: slotlar boş başlar → oyuncu havuzdan bir kamyon seçip boş slota gönderir →
    /// tablodan o kamyonun rengindeki küpleri patlatıp kasasını doldurur →
    /// kasa dolunca kamyon kapağını kapatıp kalkar, slot boşalır →
    /// havuzda boşalan yere kuyruktan yeni kamyon gelir.
    ///
    /// Kamyon kuyruğu bölümün renk paletinden üretilir: her renkten, o renkteki küpleri
    /// taşımaya yetecek kadar kamyon çıkar. Böylece bölüm her zaman çözülebilir kalır.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Truck Dispatcher")]
    public class TruckDispatcher : MonoBehaviour
    {
        private static TruckDispatcher s_Instance;
        public static TruckDispatcher Instance => s_Instance;

        [Header("🔗 Bağlantılar")]
        [SerializeField] private TruckSlotRow m_Slots;
        [SerializeField] private TruckPool m_Pool;
        [SerializeField] private GameObject m_TruckPrefab;
        [SerializeField] private PixelArtGenerator m_Generator;

        [Header("🎯 Kurallar")]
        [Tooltip("Açıkken küp ancak rengine uyan bir kamyon slotta varsa patlar. " +
                 "Kapalıyken her küp patlar (eski serbest davranış).")]
        [SerializeField] private bool m_RequireMatchingTruck = true;

        [Tooltip("Kamyon rengi ile palet rengi arasındaki tolerans.\n" +
                 "Küpün rengi önce paletteki en yakın renge sınıflandırıldığı için bu değerin " +
                 "büyük olmasına gerek yoktur; kamyon renkleri de paletten geldiği için " +
                 "normalde birebir eşleşirler.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float m_ColorThreshold = 0.02f;

        [Tooltip("Bölüm verisinde kamyon ayarı yoksa kullanılacak yedek kapasite. " +
                 "Normalde kapasite bölümden gelir (Level Designer > Kamyon Düzeni).")]
        [Min(1)]
        [SerializeField] private int m_FallbackTruckCapacity = 16;

        [Header("🚚 Geçiş")]
        [Tooltip("Kamyonun havuzdan slota (veya havuz içinde ileri) gitme süresi")]
        [Min(0.05f)]
        [SerializeField] private float m_MoveDuration = 0.4f;

        [Tooltip("Geçiş sırasında kamyonun kameraya doğru yaptığı kavis. " +
                 "0 = düz kayar, negatif değer öne doğru hafifçe kalkar.")]
        [SerializeField] private float m_MoveArc = -120f;

        [Header("🚚 Kalkış")]
        [Tooltip("Vagonun ray boyunca gitme hızı (şerit birimi / saniye). " +
                 "Süre sabit değildir: vagon bu hızla portalı geçene kadar gider.")]
        [Min(1f)]
        [SerializeField] private float m_DepartSpeed = 900f;

        [Tooltip("Vagonun tam hıza ulaşma süresi. Hareketin bir anda başlamış gibi değil, " +
                 "ağır bir vagonun yavaşça yol almaya başlaması gibi görünmesini sağlar.")]
        [Min(0f)]
        [SerializeField] private float m_DepartAccelTime = 0.9f;

        [Tooltip("Portalı geçtikten sonra ne kadar daha gitsin. Vagonun portalın " +
                 "arkasında tamamen kaybolmasını garanti eder.")]
        [Min(0f)]
        [SerializeField] private float m_DepartExtraDistance = 600f;

        [Tooltip("Kasa dolduktan sonra kalkışa kadar beklenen süre")]
        [Min(0f)]
        [SerializeField] private float m_DepartDelay = 0.35f;

        /// <summary>
        /// Kuyruktaki bir kamyon siparişi: hangi renk, kaç küp.
        /// Kapasite sabit değildir; bir rengin son kamyonu kalan küp kadar yük alır,
        /// böylece asla dolmayan (ve oyunu kilitleyen) yarım kamyon oluşmaz.
        /// </summary>
        private struct TruckOrder
        {
            public Color Color;
            public int Capacity;
        }

        /// <summary>Sıradaki kamyonlar.</summary>
        private readonly Queue<TruckOrder> m_Queue = new Queue<TruckOrder>();

        public bool RequireMatchingTruck { get => m_RequireMatchingTruck; set => m_RequireMatchingTruck = value; }
        public int QueuedTruckCount => m_Queue.Count;

        private void Awake()
        {
            s_Instance = this;
        }

        private void OnEnable()
        {
            PixelArtGenerator.LevelLoaded -= OnLevelLoaded;
            PixelArtGenerator.LevelLoaded += OnLevelLoaded;
        }

        private void OnDisable()
        {
            PixelArtGenerator.LevelLoaded -= OnLevelLoaded;
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        private void Start()
        {
            if (!Application.isPlaying) return;

            // LevelManager bölümü de Start() içinde yüklüyor ve sıra garantili değil.
            // Bu yüzden bir kare bekleyip paleti okuruz; bölüm o ana kadar yüklenmiş olur.
            StartCoroutine(RebuildNextFrame());
        }

        private IEnumerator RebuildNextFrame()
        {
            yield return null;
            Rebuild();
        }

        private void OnLevelLoaded(PixelLevelData level)
        {
            if (!Application.isPlaying) return;

            // Bölüm değişti: kamyonlar yeni bölümün paletine göre yeniden kurulmalı
            Rebuild();
        }

        /// <summary>
        /// Kamyon döngüsünü aktif bölümün paletine göre sıfırdan kurar.
        /// Birden çok kez çağrılabilir; her seferinde temiz bir başlangıç yapar.
        /// </summary>
        public void Rebuild()
        {
            StopAllCoroutines();
            m_Moving.Clear();

            ClearSlots();
            ClearPool();
            RebuildStrips();
            BuildQueue();
            RefillPool();
        }

        /// <summary>
        /// Slot ve havuz şeritlerini bölüm verisindeki sayılara göre yeniden kurar.
        /// Bölüm kaç slot ve kaç sıra havuz istiyorsa şeritler ona göre üretilir.
        /// </summary>
        private void RebuildStrips()
        {
            PixelLevelData level = GetLevel();
            if (level == null) return;

            // Bölüme özel park yeri görseli varsa onu kullan; yoksa kurulumdan geleni bırak
            if (level.SlotSprite != null)
            {
                if (m_Slots != null) m_Slots.Style.sprite = level.SlotSprite;
                if (m_Pool != null) m_Pool.Style.sprite = level.SlotSprite;
            }

            // Sahnedeki mevcut slotlar varsa ve sayıları uyuyorsa (veya sahne slotları ayarlandıysa),
            // slotları ve gölgeleri silip yok etmek yerine durumlarını ve gölgelerini güncelle
            if (m_Slots != null)
            {
                if (m_Slots.SlotCount > 0 && (m_Slots.SlotCount == level.SlotCount || level.SlotCount <= 0))
                {
                    m_Slots.UpdateShadows();
                }
                else
                {
                    m_Slots.RebuildPlaces(level.SlotCount > 0 ? level.SlotCount : 5, 1);
                }
            }

            if (m_Pool != null) m_Pool.RebuildPlaces(level.PoolColumns, level.PoolRows);
        }

        /// <summary>Aktif bölüm verisi.</summary>
        private PixelLevelData GetLevel()
        {
            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            return m_Generator != null ? m_Generator.ActiveLevelData : null;
        }

        /// <summary>Kamyon kapasitesi: bölümden gelir, yoksa yedek değer kullanılır.</summary>
        private int GetTruckCapacity()
        {
            PixelLevelData level = GetLevel();
            return level != null ? level.TruckCapacity : m_FallbackTruckCapacity;
        }

        #region 🎨 Kuyruk Kurulumu

        /// <summary>
        /// Bölümün renk paletinden kamyon kuyruğunu üretir.
        /// Her renk için o renkteki küpleri taşımaya yetecek sayıda kamyon eklenir.
        /// </summary>
        public void BuildQueue()
        {
            m_Queue.Clear();

            List<PaletteColorOverride> palette = GetPalette();
            if (palette == null || palette.Count == 0)
            {
                Debug.LogWarning("[TruckDispatcher] Bölüm paleti bulunamadı; kamyon kuyruğu kurulamadı.");
                return;
            }

            var trucks = new List<TruckOrder>();
            int capacity = GetTruckCapacity();

            // Paletteki HER renk için kamyon çıkmalı; atlanan bir renk,
            // hiç patlatılamayan ve bölümü bitirilemez kılan küpler demektir
            for (int i = 0; i < palette.Count; i++)
            {
                PaletteColorOverride entry = palette[i];
                if (entry == null || entry.pixelCount <= 0) continue;

                // Rengi taşımaya yetecek kadar kamyon; sonuncusu kalan kadar yük alır
                int remaining = entry.pixelCount;

                while (remaining > 0)
                {
                    int load = Mathf.Min(capacity, remaining);
                    trucks.Add(new TruckOrder { Color = entry.targetColor, Capacity = load });
                    remaining -= load;
                }
            }

            Shuffle(trucks);

            foreach (TruckOrder order in trucks)
            {
                m_Queue.Enqueue(order);
            }
        }

        private List<PaletteColorOverride> GetPalette()
        {
            PixelLevelData level = GetLevel();
            return level != null ? level.ColorPalette : null;
        }

        private static void Shuffle(List<TruckOrder> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        #endregion

        #region 🅿️ Havuz ve Slotlar

        private void ClearSlots()
        {
            if (m_Slots == null) return;

            foreach (TruckSlot slot in m_Slots.Slots)
            {
                if (slot == null || slot.IsEmpty) continue;

                Transform truck = slot.ReleaseTruck();
                if (truck != null) Destroy(truck.gameObject);
            }
        }

        private void ClearPool()
        {
            if (m_Pool == null) return;

            foreach (TruckSlot place in m_Pool.Places)
            {
                if (place == null || place.IsEmpty) continue;

                Transform truck = place.ReleaseTruck();
                if (truck != null) Destroy(truck.gameObject);
            }
        }

        /// <summary>Havuzdaki boş yerlere kuyruktan kamyon getirir.</summary>
        public void RefillPool()
        {
            if (m_Pool == null) return;

            foreach (TruckSlot place in m_Pool.Places)
            {
                if (place == null || !place.IsEmpty) continue;
                if (m_Queue.Count == 0) break;

                TruckOrder order = m_Queue.Dequeue();
                SpawnTruckInto(place, order);
            }
        }

        private void SpawnTruckInto(TruckSlot place, TruckOrder order)
        {
            if (m_TruckPrefab == null || place == null) return;

            GameObject truck = Instantiate(m_TruckPrefab);
            truck.name = $"Truck_{ColorUtility.ToHtmlStringRGB(order.Color)}_{order.Capacity}";

            place.AssignTruck(truck.transform, order.Color);

            TruckCargo cargo = truck.GetComponent<TruckCargo>();
            if (cargo == null) cargo = truck.AddComponent<TruckCargo>();

            cargo.ResetCargo(order.Color, order.Capacity);

            // Vagon park ederken sabit durmalı. MineCartMover prefabda "başlayınca hareket et"
            // ile geldiği için kapatıyoruz; hareketi yalnızca kalkış yönetir.
            MineCartMover mover = truck.GetComponent<MineCartMover>();
            if (mover != null) mover.StopMoving();

            // Yuvarlanma animasyonu da beklerken donmuş kalsın
            Animator animator = truck.GetComponent<Animator>();
            if (animator != null) animator.speed = 0f;
        }

        /// <summary>
        /// Havuzdaki bir kamyonu ilk boş slota gönderir.
        /// Boş slot yoksa hiçbir şey yapmaz.
        /// </summary>
        public bool SendToSlot(TruckSlot place)
        {
            if (place == null || place.IsEmpty || m_Slots == null) return false;

            TruckSlot target = FindEmptySlot();
            if (target == null) return false;

            Color color = place.TruckColor;
            Transform truck = place.ReleaseTruck();

            MoveTruckInto(target, truck, color);

            TruckCargo cargo = truck != null ? truck.GetComponent<TruckCargo>() : null;
            if (cargo != null)
            {
                cargo.Filled -= OnCargoFilled;
                cargo.Filled += OnCargoFilled;
            }

            CompactPool();
            RefillPool();

            return true;
        }

        /// <summary>
        /// Kamyonu hedef park yerine yerleştirir ve oraya kayarak gitmesini sağlar.
        /// Hedefe hemen bağlanır (slot dolu sayılır), yalnızca görsel geçiş animasyonludur.
        /// </summary>
        private void MoveTruckInto(TruckSlot target, Transform truck, Color color)
        {
            if (target == null) return;

            if (truck == null)
            {
                target.AssignTruck(null, color);
                return;
            }

            // Geçişten önceki dünya konumu
            Vector3 startWorld = truck.position;

            // Hedefin çocuğu yap ve oturacağı yeri hesapla
            target.AssignTruck(truck, color);

            Vector3 endLocal = truck.localPosition;
            Vector3 startLocal = truck.parent != null
                ? truck.parent.InverseTransformPoint(startWorld)
                : endLocal;

            // Hızlı tıklamalarda aynı kamyon için ikinci bir geçiş başlarsa
            // ikisi birbiriyle yarışıp titremeye yol açar; öncekini durdur
            if (m_Moving.TryGetValue(truck, out Coroutine running) && running != null)
            {
                StopCoroutine(running);
            }

            m_Moving[truck] = StartCoroutine(MoveRoutine(truck, startLocal, endLocal));
        }

        /// <summary>Hâlen yer değiştirmekte olan kamyonlar.</summary>
        private readonly Dictionary<Transform, Coroutine> m_Moving =
            new Dictionary<Transform, Coroutine>();

        private IEnumerator MoveRoutine(Transform truck, Vector3 from, Vector3 to)
        {
            float elapsed = 0f;

            while (elapsed < m_MoveDuration && truck != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / m_MoveDuration);

                // Yavaşlayarak yerleşsin
                float eased = 1f - (1f - t) * (1f - t);

                Vector3 position = Vector3.Lerp(from, to, eased);
                // Yolun ortasında kameraya doğru hafifçe kalk
                position.z += Mathf.Sin(t * Mathf.PI) * m_MoveArc;

                truck.localPosition = position;
                yield return null;
            }

            if (truck != null)
            {
                truck.localPosition = to;
                m_Moving.Remove(truck);
            }
        }

        /// <summary>
        /// Havuzdaki kamyonları öne kaydırarak boşlukları kapatır; kayma animasyonludur.
        /// </summary>
        private void CompactPool()
        {
            if (m_Pool == null) return;

            var places = m_Pool.Places;
            int write = 0;

            for (int read = 0; read < places.Count; read++)
            {
                TruckSlot source = places[read];
                if (source == null || source.IsEmpty) continue;

                if (read != write)
                {
                    TruckSlot target = places[write];
                    if (target != null)
                    {
                        Color color = source.TruckColor;
                        MoveTruckInto(target, source.ReleaseTruck(), color);
                    }
                }

                write++;
            }
        }

        /// <summary>
        /// Boş bir ray yeri bulur; aramaya ekranın ortasından başlar ve dışa doğru açılır.
        /// Böylece vagon ilk olarak rayın ortasına gelir, oradan portala doğru yola çıkar.
        /// </summary>
        private TruckSlot FindEmptySlot()
        {
            if (m_Slots == null) return null;

            var slots = m_Slots.Slots;
            int count = slots.Count;
            if (count == 0) return null;

            int center = count / 2;

            // Ortadan dışa: merkez, merkez-1, merkez+1, merkez-2, merkez+2 ...
            for (int offset = 0; offset <= count; offset++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    int index = center + offset * side;
                    if (index < 0 || index >= count) continue;

                    TruckSlot slot = slots[index];
                    if (slot != null && slot.IsEmpty) return slot;

                    if (offset == 0) break; // merkez tek kez denenir
                }
            }

            return null;
        }

        #endregion

        #region 💥 Küp Patlatma

        /// <summary>
        /// Bu renkteki bir küp şu an patlatılabilir mi?
        /// Kural açıkken slotta rengine uyan, dolmamış bir kamyon gerekir.
        /// </summary>
        public bool CanPop(Color cubeColor)
        {
            if (!m_RequireMatchingTruck) return true;

            return FindSlotFor(ClassifyToPalette(cubeColor)) != null;
        }

        /// <summary>
        /// Patlatılan küpü rengine uyan kamyona yükler.
        /// </summary>
        public void NotifyCubePopped(Color cubeColor)
        {
            Color paletteColor = ClassifyToPalette(cubeColor);

            TruckSlot slot = FindSlotFor(paletteColor);
            if (slot == null) return;

            TruckCargo cargo = slot.Cargo;
            if (cargo == null) return;

            cargo.TryLoad(paletteColor, m_ColorThreshold);
        }

        /// <summary>
        /// Küpün rengini bölüm paletindeki EN YAKIN renge eşler.
        ///
        /// Küplerin rengi paletteki temsilci renkten bir miktar sapar (palet benzer tonları
        /// gruplayarak çıkarılır). Sabit bir toleransla karşılaştırmak, sapması toleransı aşan
        /// küplerin hiçbir kamyona uymaması ve hiç patlamaması demekti.
        /// En yakına sınıflandırma bunu kökten çözer: her küp mutlaka bir palet rengine düşer.
        /// </summary>
        private Color ClassifyToPalette(Color cubeColor)
        {
            List<PaletteColorOverride> palette = GetPalette();
            if (palette == null || palette.Count == 0) return cubeColor;

            Color best = cubeColor;
            float bestDistance = float.MaxValue;

            foreach (PaletteColorOverride entry in palette)
            {
                if (entry == null || entry.pixelCount <= 0) continue;

                float distance = TruckCargo.ColorDistance(cubeColor, entry.targetColor);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = entry.targetColor;
                }
            }

            return best;
        }

        /// <summary>Bu rengi kabul edebilecek, dolmamış kamyonu taşıyan slotu bulur.</summary>
        private TruckSlot FindSlotFor(Color cubeColor)
        {
            if (m_Slots == null) return null;

            TruckSlot best = null;
            float bestDistance = float.MaxValue;

            foreach (TruckSlot slot in m_Slots.Slots)
            {
                if (slot == null || slot.IsEmpty) continue;

                TruckCargo cargo = slot.Cargo;
                if (cargo == null || cargo.IsFull) continue;

                float distance = TruckCargo.ColorDistance(cubeColor, cargo.CargoColor);
                if (distance > m_ColorThreshold) continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = slot;
                }
            }

            return best;
        }

        #endregion

        #region 🚚 Kalkış

        private void OnCargoFilled(TruckCargo cargo)
        {
            cargo.Filled -= OnCargoFilled;

            TruckSlot slot = FindSlotOf(cargo.transform);
            if (slot == null) return;

            StartCoroutine(DepartRoutine(slot, cargo.transform));
        }

        private TruckSlot FindSlotOf(Transform truck)
        {
            if (m_Slots == null) return null;

            foreach (TruckSlot slot in m_Slots.Slots)
            {
                if (slot != null && slot.Truck == truck) return slot;
            }

            return null;
        }

        /// <summary>
        /// Vagonu ray üzerinden kendi hızıyla uzaklaştırır ve portalı geçince yok eder.
        /// Süre sabit değildir; mesafe ve hız belirler, böylece hareket doğal görünür.
        /// </summary>
        private IEnumerator DepartRoutine(TruckSlot slot, Transform truck)
        {
            yield return new WaitForSeconds(m_DepartDelay);

            slot.ReleaseTruck();

            if (truck == null) yield break;

            MineCartMover mover = truck.GetComponent<MineCartMover>();
            Animator animator = truck.GetComponent<Animator>();

            // Vagon kendi park yerinden portala kadar gider; soldaki yerler daha uzun yol alır
            RectTransform slotRect = slot.SlotRect;
            RectTransform rowRect = slotRect != null ? slotRect.parent as RectTransform : null;

            float railHalf = rowRect != null ? rowRect.rect.width * 0.5f : 1000f;
            float slotX = slotRect != null ? slotRect.anchoredPosition.x : 0f;

            float distance = (railHalf - slotX) + m_DepartExtraDistance;
            float travelled = 0f;
            float elapsed = 0f;

            // Şerit ölçeği: tekerlek dönüşü dünya birimiyle hesaplanır
            float worldScale = truck.parent != null ? truck.parent.lossyScale.x : 1f;

            while (travelled < distance && truck != null)
            {
                elapsed += Time.deltaTime;

                // Yavaşça yol almaya başla: ani kayma yerine ağırlık hissi
                float accel = m_DepartAccelTime > 0f
                    ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / m_DepartAccelTime))
                    : 1f;

                float step = m_DepartSpeed * accel * Time.deltaTime;

                travelled += step;
                truck.localPosition += Vector3.right * step;

                // Yuvarlanma animasyonu hıza bağlı oynar; animasyon yoksa tekerlekleri
                // mover döndürür (ikisi birlikte çalışırsa birbirini ezer)
                if (animator != null)
                {
                    animator.speed = accel;
                }
                else if (mover != null)
                {
                    mover.SpinWheelsByDistance(step * worldScale);
                }

                yield return null;
            }

            if (truck != null) Destroy(truck.gameObject);
        }

        #endregion
    }
}
