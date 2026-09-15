using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Vagonların içinde oturan, vagon raya girince zıplayarak çıkan
    /// ve rengine uyan küpe koşup onu kıran madenci karakter bileşeni.
    ///
    /// Durum Makinesi: SeatedInWagon -> Jumping -> Running -> Mining -> Done
    /// Obje havuzu (Object Pooling) ve hedef küp rezervasyonu içerir.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Miner")]
    public class Miner : MonoBehaviour
    {
        public enum State
        {
            Idle,
            SeatedInWagon,
            Jumping,
            Running,
            Mining,
            Escaping,
            Done
        }

        private static readonly Stack<Miner> s_Pool = new Stack<Miner>();
        private static readonly List<Miner> s_ActiveMiners = new List<Miner>();
        private static readonly HashSet<PixelCube> s_ReservedCubes = new HashSet<PixelCube>();
        private static Transform s_PoolRoot;

        [Header("🦘 Zıplama Ayarları")]
        [Tooltip("Vagondan ve çerçeve üstünden atlama süresi (saniye)")]
        [SerializeField] private float m_JumpDuration = 0.65f;

        [Tooltip("Vagondan ve çerçeve üstünden atlama yayının tepe yüksekliği")]
        [SerializeField] private float m_JumpPower = 0.60f;

        [Header("🏃 Hareket & Ritim Ayarları")]
        [Tooltip("Madencinin yatay ve dikey koşu hızı (yavaşlatılmış ve dengelenmiş)")]
        [SerializeField] private float m_RunSpeed = 0.85f;

        [Tooltip("Koşma sırasındaki dikey zıplama frekansı")]
        [SerializeField] private float m_BounceFrequency = 10f;

        [Tooltip("Koşma sırasındaki dikey zıplama yüksekliği")]
        [SerializeField] private float m_BounceHeight = 0.05f;

        [Header("⛏️ Madencilik / Vurma Ayarları")]
        [Tooltip("Blok önünde vurma animasyonunun temas anına kadarki süresi (saniye)")]
        [SerializeField] private float m_PunchImpactDelay = 0.55f;

        [Tooltip("Küp patladıktan sonra vuruşu tamamlayıp kaçmaya başlama öncesi bekleme süresi (saniye)")]
        [SerializeField] private float m_PostPunchDelay = 0.35f;

        [Tooltip("Animasyon klipleri oyun süresinden uzun olduğunda hızlandırılır ki hareket " +
                 "ortasından kesilmeyip baştan sona görünsün. Bu, izin verilen en yüksek " +
                 "hızlandırma çarpanıdır. 1 = hiç hızlandırma yapma.")]
        [Range(1f, 6f)]
        [SerializeField] private float m_MaxClipSpeedUp = 3.5f;

        [Tooltip("Koşu klibinde ayağın, model ölçeği 1 iken zemini süpürme hızı (birim/sn). " +
                 "Klipten ölçülür: adım uzunluğu / adım süresi. Miner_Run için 0.703 / 0.317 = 2.22. " +
                 "Animasyon oynatma hızı buna göre ölçeklenir, böylece ayaklar zeminde kaymaz. " +
                 "Koşu klibini değiştirirsen bu değeri de yeni klibe göre güncelle.")]
        [Min(0.01f)]
        [SerializeField] private float m_RunStrideSpeed = 2.22f;

        private State m_State = State.Idle;
        private PixelCube m_TargetCube;
        private Color m_MinerColor = Color.white;
        private float m_ColorThreshold = 0.04f;

        private static readonly int[] s_IdleHashes =
        {
            Animator.StringToHash("Idle"),
            Animator.StringToHash("MechaMiner_Idle"),
            Animator.StringToHash("Armature|Idle")
        };

        private static readonly int[] s_JumpHashes =
        {
            Animator.StringToHash("Jump"),
            Animator.StringToHash("MechaMiner_Jump"),
            Animator.StringToHash("Armature|Jump")
        };

        private static readonly int[] s_RunHashes =
        {
            Animator.StringToHash("Running"),
            Animator.StringToHash("MechaMiner_Run"),
            Animator.StringToHash("Armature|Running")
        };

        private static readonly int[] s_AttackHashes =
        {
            Animator.StringToHash("ZombiePunching"),
            Animator.StringToHash("Attack"),
            Animator.StringToHash("Armature|ZombiePunching"),
            Animator.StringToHash("MechaMiner_Attack")
        };

        private static readonly int s_IsRunningHash = Animator.StringToHash("IsRunning");
        private static readonly int s_JumpTriggerHash = Animator.StringToHash("Jump");
        private static readonly int s_AttackTriggerHash = Animator.StringToHash("Attack");
        private static readonly int s_RunSpeedMulHash = Animator.StringToHash("RunSpeedMul");
        private static readonly int s_ClipSpeedHash = Animator.StringToHash("ClipSpeed");

        private Animator m_Animator;
        private MeshRenderer m_Renderer;
        private Material m_Material;
        private Sequence m_JumpSequence;

        private Vector3 m_BasePosition;
        private Vector3 m_MiningStandPosition;
        private Vector3 m_TargetRunningScale = Vector3.one;
        private List<Vector3> m_PathWaypoints;
        private int m_CurrentWaypointIndex;
        private float m_RunTimer;

        /// <summary>
        /// Zıplama ritminin kişisel hız çarpanı.
        /// Madenciler senkron zıplamasın diye kullanılır. Eskiden bunun için zamanlayıcı
        /// rastgele bir değerden başlatılıyordu; o zaman koşuya geçilen ilk karede
        /// zıplama ofseti sıfırdan değil rastgele bir noktadan başlıyor ve madenci
        /// derinlik ekseninde anlık sıçrıyordu. Hızı çeşitlemek aynı dağınıklığı
        /// sıçrama olmadan sağlar.
        /// </summary>
        private float m_BounceSpeedMultiplier = 1f;

        /// <summary>
        /// Yol noktasına "varıldı" sayılacak mesafe. Izgara hücre boyutundan türetilir.
        /// Sabit bir değer (0.15) kullanılırsa hücre boyutundan büyük kalıp madencinin
        /// hedefe varmadan yön değiştirmesine, keskin köşelerde geri sıçramasına yol açar.
        /// </summary>
        private float m_ReachDistance = -1f;

        /// <summary>Izgara hücre boyutuna göre hesaplanan varış mesafesi (bir kez hesaplanır).</summary>
        private float ReachDistance
        {
            get
            {
                if (m_ReachDistance <= 0f)
                {
                    BoardLayout layout = CalculateBoardLayout(null);
                    m_ReachDistance = Mathf.Max(0.02f, layout.cellSize * 0.6f);
                }

                return m_ReachDistance;
            }
        }

        /// <summary>
        /// Bir yol noktasına "varıldı" sayılacak mesafe.
        ///
        /// Izgara hücresine bağlı sabit bir değer yeterli değil: yol noktaları köşelerde
        /// birbirine hücre boyutundan çok daha yakın düşebiliyor (ölçümde 0.099 birim
        /// aralıklı noktalar görüldü, eşik ise 0.15'ti). Eşik aralıktan büyük olduğunda
        /// aynı karede birden fazla nokta atlanıyor, madenci köşeyi kesip yön sıçratıyor.
        ///
        /// Bu yüzden eşik, madencinin o karede kat ettiği mesafeye bağlanır: bir adımdan
        /// biraz fazlasına yaklaşınca varılmış sayılır. Böylece hiçbir nokta atlanmaz ve
        /// hız ne olursa olsun titreme oluşmaz.
        /// </summary>
        private float GetReachDistance(float step)
        {
            return Mathf.Max(0.02f, Mathf.Min(step * 1.5f, ReachDistance));
        }

        private static readonly Dictionary<string, float> s_ClipLengths = new Dictionary<string, float>();

        /// <summary>
        /// Adı verilen animasyon klibinin süresini döndürür (ilk okumada önbelleğe alınır).
        /// </summary>
        private static float GetClipLength(Animator animator, string clipNamePart)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return 0f;

            string key = animator.runtimeAnimatorController.name + "/" + clipNamePart;
            if (s_ClipLengths.TryGetValue(key, out float cached)) return cached;

            float found = 0f;
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null &&
                    clips[i].name.IndexOf(clipNamePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = clips[i].length;
                    break;
                }
            }

            s_ClipLengths[key] = found;
            return found;
        }

        /// <summary>
        /// Bir animasyon klibini verilen oyun süresine sığdıracak animatör hızı.
        ///
        /// Klipler oyun pencerelerinden çok daha uzun: ölçümde zıplama klibi 2.17 sn
        /// ama zıplama yayı 0.70 sn, vurma klibi 3.83 sn ama vuruş penceresi 0.90 sn
        /// sürüyordu. Bu yüzden her hareket ortasından kesiliyor, madenci zıplama
        /// pozunda yerde kayıyor ve yumruk hiç tamamlanmıyordu. Hız klip uzunluğuna
        /// göre ölçeklenince hareket baştan sona görünür.
        /// </summary>
        private float GetClipFitSpeed(string clipNamePart, float targetDuration)
        {
            float length = GetClipLength(m_Animator, clipNamePart);
            if (length <= 0.01f || targetDuration <= 0.01f) return 1f;

            return Mathf.Clamp(length / targetDuration, 0.5f, Mathf.Max(1f, m_MaxClipSpeedUp));
        }

        /// <summary>
        /// Koşma klibinin oynatma çarpanı (RunSpeedMul parametresine yazılır).
        ///
        /// Eskiden sabit bir katsayıydı (m_RunSpeed * 1.25) ve ölçekle hiç ilgisi yoktu.
        /// Ölçümde madenciler dünya ölçeği 0.175'te koşuyordu; klibin adımı bu ölçekte
        /// saniyede 0.39 birim süpürürken yer hızı 0.85 birimdi. Yani madenciler
        /// bacaklarının taşıdığından 2.2 kat hızlı gidiyor, ayakları buz üstünde
        /// kayıyormuş gibi görünüyordu.
        ///
        /// Artık oynatma hızı yer hızından türetiliyor: hızı veya madenci ölçeğini
        /// değiştirsen de ayaklar kaymaz.
        /// </summary>
        private float RunAnimationSpeed
        {
            get
            {
                float footSpeed = m_RunStrideSpeed * m_RunWorldScale;
                if (footSpeed <= 0.0001f) return 1f;

                return Mathf.Clamp(EffectiveRunSpeed / footSpeed, 0.5f, 3.0f);
            }
        }

        /// <summary>
        /// Koşu animasyon hızının hesabında kullanılan dünya ölçeği.
        /// Koşuya girişte BİR KEZ yakalanır, her karede okunmaz: madenci vagondan
        /// zıplarken DOScale ile küçülüyor, havuza dönerken de ölçeği düşüyor
        /// (ölçümde 0.00096'ya kadar). Ölçeği her karede okumak, bu anlarda
        /// animasyon hızını üst sınıra çarptırıp yeni bir sıçrama yaratıyordu.
        /// </summary>
        private void CaptureRunScale()
        {
            float scale = Mathf.Abs(transform.lossyScale.x);
            if (scale > 0.0001f) m_RunWorldScale = scale;
        }

        /// <summary>
        /// Bir float animatör parametresini yazar (parametre yoksa sessizce atlar).
        ///
        /// Klip hızları neden Animator.speed ile değil parametreyle sürülüyor:
        /// Animator.speed TÜM katmanı çarpar. Yumruk/zıplama için onu 3-4 katına
        /// çıkarmak, o anda sönmekte olan koşu klibini de aynı oranda hızlandırıyor
        /// ve geçiş sırasında 2-4 karelik gözle görülür bir "fırıldak" sıçraması
        /// yaratıyordu. Durum bazlı hız parametresi bu karışmayı tamamen kaldırır.
        /// </summary>
        private void SetAnimatorFloatParameter(int paramHash, float value)
        {
            if (m_Animator == null || !m_Animator.isActiveAndEnabled) return;

            foreach (AnimatorControllerParameter p in m_Animator.parameters)
            {
                if (p.nameHash == paramHash && p.type == AnimatorControllerParameterType.Float)
                {
                    m_Animator.SetFloat(paramHash, value);
                    return;
                }
            }
        }
        private Coroutine m_StateCoroutine;
        private bool m_HasJumpedOutside;
        private bool m_IsExitingLeft;
        private float m_IndividualSpeedMultiplier = 1.0f;
        private bool m_HasMinedOneCube = false;
        private float m_RunWorldScale = 1f;

        public State CurrentState => m_State;
        public PixelCube TargetCube => m_TargetCube;

        /// <summary>
        /// Madencinin gerçek yer hızı. Animatör hızı da aynı kişisel çarpanla
        /// ölçeklendiği için, bu çarpanı yalnızca animasyona uygulamak ayakların
        /// zeminde kaymasına (ve hareketin tekliyormuş gibi görünmesine) yol açıyordu.
        /// </summary>
        private float EffectiveRunSpeed => m_RunSpeed * m_IndividualSpeedMultiplier;

        private void TriggerAnimatorParameter(int triggerHash, int[] fallbackHashes, float transitionDuration = 0.12f, float normalizedTimeOffset = -1f)
        {
            if (m_Animator == null || !m_Animator.isActiveAndEnabled) return;

            bool found = false;
            foreach (AnimatorControllerParameter p in m_Animator.parameters)
            {
                if (p.nameHash == triggerHash)
                {
                    m_Animator.SetTrigger(triggerHash);
                    found = true;
                    break;
                }
            }

            if (!found && fallbackHashes != null)
            {
                CrossFadeAnimation(fallbackHashes, transitionDuration, normalizedTimeOffset);
            }
        }

        private void SetAnimatorBoolParameter(int boolHash, bool value, int[] fallbackHashes, float transitionDuration = 0.12f)
        {
            if (m_Animator == null || !m_Animator.isActiveAndEnabled) return;

            bool found = false;
            foreach (AnimatorControllerParameter p in m_Animator.parameters)
            {
                if (p.nameHash == boolHash)
                {
                    m_Animator.SetBool(boolHash, value);
                    found = true;
                    break;
                }
            }

            if (!found && value && fallbackHashes != null)
            {
                CrossFadeAnimation(fallbackHashes, transitionDuration);
            }
        }

        private void CrossFadeAnimation(int[] hashes, float transitionDuration = 0.12f, float normalizedTimeOffset = -1f)
        {
            if (m_Animator == null || !m_Animator.isActiveAndEnabled) return;

            // Zaten bu animasyondaysa veya bu animasyona geçiş halindeyse tekrar tetikleyip takılma yaratma
            if (IsCurrentOrNextStateAny(m_Animator, hashes)) return;

            for (int i = 0; i < hashes.Length; i++)
            {
                if (m_Animator.HasState(0, hashes[i]))
                {
                    if (normalizedTimeOffset >= 0f)
                    {
                        m_Animator.CrossFadeInFixedTime(hashes[i], transitionDuration, 0, normalizedTimeOffset);
                    }
                    else
                    {
                        m_Animator.CrossFadeInFixedTime(hashes[i], transitionDuration, 0);
                    }
                    return;
                }
            }
        }

        private static bool IsCurrentOrNextStateAny(Animator animator, int[] hashes)
        {
            if (animator == null) return false;
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            for (int i = 0; i < hashes.Length; i++)
            {
                if (current.shortNameHash == hashes[i] || current.fullPathHash == hashes[i])
                {
                    return true;
                }
            }
            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                for (int i = 0; i < hashes.Length; i++)
                {
                    if (next.shortNameHash == hashes[i] || next.fullPathHash == hashes[i])
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void MaintainLoop(Animator animator, int[] hashes)
        {
            // Unity'nin Animator Controller ve FBX import ayarları (loopTime = 1) animasyonu
            // dahili C++ motoruyla kesintisiz ve pürüzsüz döngüye sokar.
            // Kod ile manuel CrossFade veya resete zorlamak "2 ileri 1 geri" sıçramalarına neden olur.
            return;
        }

        #region 🚀 Başlatma & Havuzlama (Spawning & Pooling)

        /// <summary>
        /// Vagonun kasası içinde oturan görsel bir madenci oluşturur.
        /// Vagon raya girdiğinde vagondan dışarı zıplayacaktır.
        /// </summary>
        public static Miner CreateSeatedMiner(
            Transform bodyTransform,
            Vector3 localSeatPos,
            float localScale,
            Color cargoColor,
            GameObject minerPrefab = null,
            float runSpeed = 0.85f)
        {
            Miner miner = Rent(minerPrefab);
            miner.m_MinerColor = cargoColor;
            miner.m_RunSpeed = runSpeed;
            miner.m_State = State.SeatedInWagon;

            // Vagon kasasının çocuğu olarak dik oturt
            miner.transform.SetParent(bodyTransform, false);
            miner.transform.localPosition = localSeatPos;
            miner.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            miner.transform.localScale = Vector3.one * Mathf.Max(0.02f, localScale);

            // Renk uygulaması
            miner.ApplyColor(cargoColor);
            miner.gameObject.SetActive(true);

            if (miner.m_Animator != null && miner.m_Animator.isActiveAndEnabled)
            {
                miner.m_Animator.enabled = true;
                miner.m_Animator.speed = 1f * miner.m_IndividualSpeedMultiplier;
                miner.SetAnimatorBoolParameter(s_IsRunningHash, false, null);
                float randomIdlePhase = UnityEngine.Random.value;
                miner.m_Animator.Play("Armature|Idle", 0, randomIdlePhase);
            }

            s_ActiveMiners.Add(miner);
            return miner;
        }

        /// <summary>
        /// Vagonda oturan madenciyi dışarı fırlatır ve küpe koşmaya başlatır.
        /// </summary>
        public bool JumpOutFromWagon(Color cargoColor, float colorThreshold, float runSpeed = 0.85f)
        {
            if (m_State != State.SeatedInWagon) return false;

            m_MinerColor = cargoColor;
            m_ColorThreshold = colorThreshold;
            m_RunSpeed = runSpeed;

            Vector3 spawnWorldPos = transform.position;

            // Hedef küp ara
            PixelCube target = FindAndReserveClosestCube(spawnWorldPos, cargoColor, colorThreshold);
            if (target == null)
            {
                // Kırılacak uygun küp kalmadıysa false dön (oturan madenci vagonda kalmaya devam eder)
                return false;
            }

            m_TargetCube = target;

            // Vagondan bağımsız dünya uzayına al (dünya pozisyonu ve ölçeği korunur)
            transform.SetParent(EnsurePoolRoot(), true);
            m_BasePosition = transform.position;

            // Zıplarken yere inene kadar pürüzsüzce küçüleceği hedef koşu ölçeği (%80)
            m_TargetRunningScale = transform.localScale * 0.80f;

            // Zıplama dizisini başlat
            StartJumpState(m_BasePosition);
            return true;
        }

        private static Miner Rent(GameObject prefab)
        {
            while (s_Pool.Count > 0)
            {
                Miner pooled = s_Pool.Pop();
                if (pooled != null && pooled.gameObject != null)
                {
                    pooled.m_IndividualSpeedMultiplier = UnityEngine.Random.Range(0.93f, 1.07f);
                    // Bölüm değişmiş olabilir: varış mesafesi yeni ızgaraya göre yeniden hesaplansın
                    pooled.m_ReachDistance = -1f;
                    return pooled;
                }
            }
            Miner newMiner = Create(prefab);
            newMiner.m_IndividualSpeedMultiplier = UnityEngine.Random.Range(0.93f, 1.07f);
            return newMiner;
        }

        private static Miner Create(GameObject prefab)
        {
            GameObject obj;
            if (prefab != null)
            {
                obj = Instantiate(prefab);
                obj.name = "Miner";
            }
            else
            {
                // Geçici kapsül primitive
                obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                obj.name = "Miner_Primitive";

                Collider col = obj.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            obj.transform.SetParent(EnsurePoolRoot(), false);
            Miner miner = obj.GetComponent<Miner>();
            if (miner == null) miner = obj.AddComponent<Miner>();

            miner.m_Animator = obj.GetComponent<Animator>();
            miner.m_Renderer = obj.GetComponentInChildren<MeshRenderer>();

            return miner;
        }

        private static Transform EnsurePoolRoot()
        {
            if (s_PoolRoot != null) return s_PoolRoot;

            GameObject root = new GameObject("[Miners]");
            s_PoolRoot = root.transform;

            if (Application.isPlaying) DontDestroyOnLoad(root);

            return s_PoolRoot;
        }

        public void Release()
        {
            CleanupState();
            UnreserveTarget();
            m_HasJumpedOutside = false;
            m_IsExitingLeft = false;
            m_HasMinedOneCube = false;

            s_ActiveMiners.Remove(this);

            if (m_Animator != null && m_Animator.isActiveAndEnabled && gameObject.activeInHierarchy)
            {
                m_Animator.speed = 1f;
                SetAnimatorBoolParameter(s_IsRunningHash, false, null);
                m_Animator.Play("Armature|Idle", 0, 0f);
            }

            if (gameObject != null)
            {
                gameObject.SetActive(false);
                transform.SetParent(EnsurePoolRoot(), false);
                s_Pool.Push(this);
            }
        }

        /// <summary>
        /// Sahnedeki tüm aktif ve oturan madencileri temizler ve havuza iade eder.
        /// </summary>
        public static void ClearAllActiveMiners()
        {
            InvalidateLayoutCache();

            Miner[] activeArray = s_ActiveMiners.ToArray();
            for (int i = 0; i < activeArray.Length; i++)
            {
                if (activeArray[i] != null)
                {
                    activeArray[i].Release();
                }
            }

            s_ActiveMiners.Clear();
            s_ReservedCubes.Clear();
        }

        #endregion

        #region 🎯 Küp Rezervasyonu & Arama

        #region 🧭 Navigasyon Önbelleği

        private static PixelCube[] s_CubeCache;
        private static int s_CubeCacheFrame = -1;

        private static Dictionary<(int, int), PixelCube> s_GridMapCache;
        private static int s_GridMapCacheFrame = -1;

        /// <summary>
        /// Sahnedeki küpleri kare başına yalnızca bir kez toplar; tüm madenciler paylaşır.
        ///
        /// Önceden her madenci her karede kendi başına sahneyi tarayıp küpleri topluyordu.
        /// 20 madenci ve 576 küplü bir tabloda bu, kare başına 20 sahne taraması ve
        /// 20 dizi tahsisi demekti; hareketin takılmasının ana sebebi buydu.
        /// </summary>
        public static PixelCube[] GetCubesCached()
        {
            if (s_CubeCacheFrame == Time.frameCount && s_CubeCache != null)
            {
                return s_CubeCache;
            }

            PixelArtGenerator gen = UnityEngine.Object.FindFirstObjectByType<PixelArtGenerator>();

            s_CubeCache = gen != null && gen.CubesContainer != null
                ? gen.CubesContainer.GetComponentsInChildren<PixelCube>()
                : null;

            s_CubeCacheFrame = Time.frameCount;
            return s_CubeCache;
        }

        /// <summary>
        /// Izgara haritasını kare başına bir kez kurar; tüm madenciler paylaşır.
        /// Küpler kırıldıkça harita değişir, bu yüzden önbellek kare bazlıdır.
        /// </summary>
        public static Dictionary<(int, int), PixelCube> GetGridMapCached()
        {
            if (s_GridMapCacheFrame == Time.frameCount && s_GridMapCache != null)
            {
                return s_GridMapCache;
            }

            s_GridMapCache = BuildCubeGridMap(GetCubesCached());
            s_GridMapCacheFrame = Time.frameCount;
            return s_GridMapCache;
        }

        /// <summary>Bölüm değiştiğinde önbelleği geçersiz kılar.</summary>
        public static void InvalidateNavigationCache()
        {
            s_CubeCache = null;
            s_CubeCacheFrame = -1;
            s_GridMapCache = null;
            s_GridMapCacheFrame = -1;
        }

        #endregion

        public static Dictionary<(int, int), PixelCube> BuildCubeGridMap(PixelCube[] allCubes)
        {
            var map = new Dictionary<(int, int), PixelCube>();
            if (allCubes == null) return map;

            for (int i = 0; i < allCubes.Length; i++)
            {
                PixelCube cube = allCubes[i];
                if (cube != null && !cube.IsPopped)
                {
                    map[(cube.GridX, cube.GridY)] = cube;
                }
            }
            return map;
        }

        /// <summary>
        /// Dıştan içe (outside-in) kuralı: Küpün 4 dik komşusundan (Sol, Sağ, Aşağı, Yukarı)
        /// en az biri eksik/boş veya patlatılmışsa bu küp dışarıya açıktır ve kırılabilir.
        /// </summary>
        public static bool IsCubeExposed(PixelCube cube, Dictionary<(int, int), PixelCube> gridMap)
        {
            if (cube == null || cube.IsPopped) return false;
            if (gridMap == null || gridMap.Count == 0) return true;

            int x = cube.GridX;
            int y = cube.GridY;

            if (!gridMap.TryGetValue((x - 1, y), out PixelCube left) || left == null || left.IsPopped) return true;
            if (!gridMap.TryGetValue((x + 1, y), out PixelCube right) || right == null || right.IsPopped) return true;
            if (!gridMap.TryGetValue((x, y - 1), out PixelCube down) || down == null || down.IsPopped) return true;
            if (!gridMap.TryGetValue((x, y + 1), out PixelCube up) || up == null || up.IsPopped) return true;

            return false;
        }

        public static bool HasAccessibleMatchingCube(Color cargoColor, float threshold)
        {
            PixelCube[] allCubes = GetCubesCached();
            if (allCubes == null || allCubes.Length == 0) return false;

            var gridMap = GetGridMapCached();

            for (int i = 0; i < allCubes.Length; i++)
            {
                PixelCube cube = allCubes[i];
                if (cube == null || cube.IsPopped || s_ReservedCubes.Contains(cube)) continue;

                if (IsCubeExposed(cube, gridMap) && IsCubeMatchingCargo(cube, cargoColor, threshold))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Panoda (açık veya içte) henüz kırılmamış ve rezerve edilmemiş eşleşen küp var mı?
        /// </summary>
        public static bool HasMatchingUnpoppedCube(Color cargoColor, float threshold)
        {
            PixelCube[] allCubes = GetCubesCached();
            if (allCubes == null || allCubes.Length == 0) return false;

            for (int i = 0; i < allCubes.Length; i++)
            {
                PixelCube cube = allCubes[i];
                if (cube == null || cube.IsPopped || s_ReservedCubes.Contains(cube)) continue;

                if (IsCubeMatchingCargo(cube, cargoColor, threshold))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Sahada şu anda bu renkteki küplere doğru koşan veya kırmaya hazırlanan madenci var mı?
        /// Bu madenciler küpleri vurduğunda yeni iç küpler açığa çıkacaktır.
        /// </summary>
        public static bool HasActiveMinersTargetingColor(Color cargoColor, float threshold)
        {
            for (int i = 0; i < s_ActiveMiners.Count; i++)
            {
                Miner miner = s_ActiveMiners[i];
                if (miner == null || !miner.gameObject.activeSelf) continue;

                if (miner.m_State == State.Jumping || miner.m_State == State.Running || miner.m_State == State.Mining)
                {
                    if (TruckCargo.ColorDistance(miner.m_MinerColor, cargoColor) <= threshold)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public struct BoardLayout
        {
            public int cols;
            public int rows;
            public float cellSize;
            public Vector3 gridOrigin;
            public float lowestCubeBottom;
            public float highestCubeTop;
            public float leftmostCubeLeft;
            public float rightmostCubeRight;
            public float bottomCorridorY;
            public float topCorridorY;
            public float leftCorridorX;
            public float rightCorridorX;
            public Vector3 frameCenter;
            public float frameBottomY;
            public float fullWidth;
            public float fullHeight;
        }

        private static BoardLayout s_CachedLayout;
        private static bool s_LayoutCached = false;

        public static void InvalidateLayoutCache()
        {
            // Bölüm değişiminde küp listesi ve ızgara haritası da geçersizdir
            InvalidateNavigationCache();

            s_LayoutCached = false;
        }

        public static BoardLayout CalculateBoardLayout(PixelCube[] allCubes)
        {
            if (s_LayoutCached) return s_CachedLayout;

            BoardLayout layout = new BoardLayout();

            // Önce PixelArtGenerator ve aktif kamera üzerinden tam teorik sınırları hesapla
            PixelArtGenerator gen = UnityEngine.Object.FindFirstObjectByType<PixelArtGenerator>();
            Camera mainCam = Camera.main;

            if (gen != null && mainCam != null && gen.CalculateTargetWorldBounds(mainCam, out Vector3 worldCenter, out float worldWidth, out float worldHeight))
            {
                layout.frameCenter = worldCenter;
                layout.frameCenter.z = gen.TargetZ;
                layout.fullWidth = worldWidth;
                layout.fullHeight = worldHeight;
                layout.frameBottomY = layout.frameCenter.y - layout.fullHeight * 0.5f;

                gen.GetEffectiveGridSize(gen.GetActiveTexture(), out int cols, out int rows);
                if (cols <= 0 || rows <= 0) { cols = 24; rows = 24; }
                layout.cols = cols;
                layout.rows = rows;

                layout.cellSize = Mathf.Min(worldWidth / cols, worldHeight / rows);
                float totalWidth = cols * layout.cellSize;
                float totalHeight = rows * layout.cellSize;

                layout.gridOrigin = new Vector3(
                    worldCenter.x - totalWidth * 0.5f + layout.cellSize * 0.5f,
                    worldCenter.y - totalHeight * 0.5f + layout.cellSize * 0.5f,
                    gen.TargetZ
                );

                layout.leftmostCubeLeft = layout.gridOrigin.x - layout.cellSize * 0.5f;
                layout.rightmostCubeRight = layout.gridOrigin.x + (layout.cols - 0.5f) * layout.cellSize;
                layout.lowestCubeBottom = layout.gridOrigin.y - layout.cellSize * 0.5f;
                layout.highestCubeTop = layout.gridOrigin.y + (layout.rows - 0.5f) * layout.cellSize;

                float frameLeftX = layout.frameCenter.x - layout.fullWidth * 0.5f;
                float frameRightX = layout.frameCenter.x + layout.fullWidth * 0.5f;
                float frameTopY = layout.frameCenter.y + layout.fullHeight * 0.5f;

                // Koridor noktaları ızgaranın doğal devamı olmalıdır: gx=-1 hücresi
                // gx=0'ın tam bir hücre solunda, gx=cols ise son sütunun bir hücre sağında.
                //
                // Önceden koridor çerçeve kenarından içeri doğru (frameLeftX + margin)
                // hesaplanıyordu. Küpler çerçeveyi tamamen doldurduğu için sol koridor
                // en sol küp sütununun SAĞINA düşüyordu; yol bulma "gx=-1" adımını sola
                // sanıyor ama dünyada sağa gidiyordu. Kenar bölgelerinde madencilerin
                // ileri geri savrulmasının sebebi buydu.
                layout.leftCorridorX = layout.gridOrigin.x - layout.cellSize;
                layout.rightCorridorX = layout.gridOrigin.x + layout.cols * layout.cellSize;
                layout.bottomCorridorY = layout.gridOrigin.y - layout.cellSize;
                layout.topCorridorY = layout.gridOrigin.y + layout.rows * layout.cellSize;

                s_CachedLayout = layout;
                s_LayoutCached = true;
                return layout;
            }

            // Yedek: küpler üzerinden hesaplama
            int minGx = int.MaxValue, maxGx = int.MinValue;
            int minGy = int.MaxValue, maxGy = int.MinValue;
            PixelCube refCube = null;

            if (allCubes != null)
            {
                for (int i = 0; i < allCubes.Length; i++)
                {
                    PixelCube c = allCubes[i];
                    if (c == null) continue;
                    if (refCube == null) refCube = c;

                    if (c.GridX < minGx) minGx = c.GridX;
                    if (c.GridX > maxGx) maxGx = c.GridX;
                    if (c.GridY < minGy) minGy = c.GridY;
                    if (c.GridY > maxGy) maxGy = c.GridY;
                }
            }

            Bounds b = GetBoardBounds(allCubes);
            layout.frameCenter = b.center;
            layout.fullWidth = b.size.x * 1.18f;
            layout.fullHeight = b.size.y * 1.18f;
            layout.frameBottomY = layout.frameCenter.y - layout.fullHeight * 0.5f;

            if (refCube != null && maxGx >= 0 && maxGy >= 0)
            {
                layout.cols = maxGx + 1;
                layout.rows = maxGy + 1;
                layout.cellSize = Mathf.Max(0.2f, refCube.transform.localScale.x);
                layout.gridOrigin = refCube.transform.position - new Vector3(refCube.GridX * layout.cellSize, refCube.GridY * layout.cellSize, 0f);

                layout.leftmostCubeLeft = layout.gridOrigin.x - layout.cellSize * 0.5f;
                layout.rightmostCubeRight = layout.gridOrigin.x + (layout.cols - 0.5f) * layout.cellSize;
                layout.lowestCubeBottom = layout.gridOrigin.y - layout.cellSize * 0.5f;
                layout.highestCubeTop = layout.gridOrigin.y + (layout.rows - 0.5f) * layout.cellSize;
            }
            else
            {
                layout.cols = 24;
                layout.rows = 24;
                layout.cellSize = Mathf.Max(0.2f, b.size.x / 24f);
                layout.gridOrigin = b.min + Vector3.one * (layout.cellSize * 0.5f);
                layout.leftmostCubeLeft = b.min.x;
                layout.rightmostCubeRight = b.max.x;
                layout.lowestCubeBottom = b.min.y;
                layout.highestCubeTop = b.max.y;
            }

            // Koridorlar ızgaranın doğal devamıdır (ana yoldaki hesapla aynı kural):
            // gx=-1 ve gy=-1 hücreleri ızgaranın bir hücre dışında yer alır.
            layout.leftCorridorX = layout.gridOrigin.x - layout.cellSize;
            layout.rightCorridorX = layout.gridOrigin.x + layout.cols * layout.cellSize;
            layout.bottomCorridorY = layout.gridOrigin.y - layout.cellSize;
            layout.topCorridorY = layout.gridOrigin.y + layout.rows * layout.cellSize;

            s_CachedLayout = layout;
            s_LayoutCached = true;
            return layout;
        }

        /// <summary>
        /// Kırma noktası: Bloğun kesinlikle dış tarafında (Alt, Sol, Sağ veya Üst)
        /// madencinin duracağı noktayı hesaplar. Bloğun üstüne veya altına girmesini engeller.
        /// Karakterin bloğun içine yapışmasını önleyecek, vuruş menziline uygun doğal duruş mesafesi (0.92x).
        /// </summary>
        public static Vector3 GetMiningStandPosition(PixelCube cube, Dictionary<(int, int), PixelCube> gridMap)
        {
            if (cube == null) return Vector3.zero;

            Vector3 cubePos = cube.transform.position;
            float cubeScale = Mathf.Max(0.35f, cube.transform.localScale.x);
            float offset = cubeScale * 0.92f;

            int x = cube.GridX;
            int y = cube.GridY;

            // Öncelikli dışa açık kenarlar: Alt (-Y), Sol (-X), Sağ (+X), Üst (+Y)
            if (gridMap == null || !gridMap.TryGetValue((x, y - 1), out PixelCube down) || down == null || down.IsPopped)
            {
                return cubePos + new Vector3(0f, -offset, -0.30f);
            }
            if (!gridMap.TryGetValue((x - 1, y), out PixelCube left) || left == null || left.IsPopped)
            {
                return cubePos + new Vector3(-offset, 0f, -0.30f);
            }
            if (!gridMap.TryGetValue((x + 1, y), out PixelCube right) || right == null || right.IsPopped)
            {
                return cubePos + new Vector3(offset, 0f, -0.30f);
            }
            if (!gridMap.TryGetValue((x, y + 1), out PixelCube up) || up == null || up.IsPopped)
            {
                return cubePos + new Vector3(0f, offset, -0.30f);
            }

            return cubePos + new Vector3(0f, -offset, -0.30f);
        }

        public static Bounds GetBoardBounds(PixelCube[] allCubes)
        {
            if (allCubes == null || allCubes.Length == 0) return new Bounds(Vector3.zero, new Vector3(5f, 5f, 1f));

            Bounds b = new Bounds(allCubes[0].transform.position, Vector3.zero);
            for (int i = 0; i < allCubes.Length; i++)
            {
                if (allCubes[i] != null && !allCubes[i].IsPopped)
                {
                    b.Encapsulate(allCubes[i].transform.position);
                }
            }
            return b;
        }

        /// <summary>
        /// Mavi çerçevenin (MainPlane / Frame.png) dünya koordinatlarındaki sınırlarını ve
        /// 4 kapı noktasını (2 alt kırmızı giriş, 2 yan yeşil çıkış) hesaplar.
        /// BoardLayout ile senkronize çalışır ve küpler patlatıldıkça ASLA kaymaz/bozulmaz.
        /// </summary>
        public static bool GetFrameGatePositions(
            PixelCube[] allCubes,
            out Vector3 leftBottomEntrance,
            out Vector3 rightBottomEntrance,
            out Vector3 leftSideExit,
            out Vector3 rightSideExit,
            out Vector3 frameCenter,
            out float fullWidth,
            out float fullHeight)
        {
            BoardLayout layout = CalculateBoardLayout(allCubes);
            frameCenter = layout.frameCenter;
            fullWidth = layout.fullWidth;
            fullHeight = layout.fullHeight;
            float frameBottomY = layout.frameBottomY;
            float frameLeftX = layout.frameCenter.x - layout.fullWidth * 0.5f;
            float frameRightX = layout.frameCenter.x + layout.fullWidth * 0.5f;

            // Frame.png analizine göre alt kapı merkezleri:
            // Sol kırmızı giriş açıklığı: genişliğin ~%31'i (merkezin %19 solu)
            // Sağ kırmızı giriş açıklığı: genişliğin ~%69'u (merkezin %19 sağı)
            leftBottomEntrance = new Vector3(layout.frameCenter.x - layout.fullWidth * 0.1906f, frameBottomY, -0.30f);
            rightBottomEntrance = new Vector3(layout.frameCenter.x + layout.fullWidth * 0.1898f, frameBottomY, -0.30f);

            // Yan yeşil çıkış açıklıkları: Sol ve sağ yan duvarların tam dikey ortası (%50)
            leftSideExit = new Vector3(frameLeftX, layout.frameCenter.y, -0.30f);
            rightSideExit = new Vector3(frameRightX, layout.frameCenter.y, -0.30f);

            return true;
        }

        /// <summary>
        /// Geriye dönük uyumluluk için yan yeşil çıkış kapılarını döndürür.
        /// </summary>
        public static void GetFrameGapPositions(PixelCube[] allCubes, out Vector3 leftGap, out Vector3 rightGap)
        {
            GetFrameGatePositions(allCubes, out _, out _, out leftGap, out rightGap, out _, out _, out _);
        }

        /// <summary>
        /// Madencinin iki yan yeşil çıkış kapısından hangisine daha yakın olduğunu belirler ve seçer.
        /// </summary>
        public static Vector3 SelectClosestFrameGap(Vector3 pos, PixelCube[] allCubes)
        {
            GetFrameGatePositions(allCubes, out _, out _, out Vector3 leftExit, out Vector3 rightExit, out _, out _, out _);
            float distLeft = (leftExit - pos).sqrMagnitude;
            float distRight = (rightExit - pos).sqrMagnitude;
            return distLeft <= distRight ? leftExit : rightExit;
        }

        /// <summary>
        /// <summary>
        /// Madencinin iniş yaptığı iç alt koridordan hedef küpün stand noktasına
        /// boş hücreler ve açık koridorlar üzerinden giden BFS yolunu oluşturur.
        /// </summary>
        public static List<Vector3> GeneratePathWaypoints(Vector3 startPos, Vector3 targetStandPos, PixelCube[] allCubes)
        {
            List<Vector3> path = new List<Vector3>();
            BoardLayout layout = CalculateBoardLayout(allCubes);

            // Madenci çerçevenin altındaysa (dışarıdaysa), alt koridora geçiş noktası ekle.
            //
            // Geçiş noktası HEDEFİN değil MADENCİNİN x hizasında olmalı: hedefin hizasına
            // sabitlendiğinde madenci koridora inerken hedefe doğru bir adım atıyor, ama
            // BFS iç alanı dolu bulup yan koridordan dolaşmaya karar verdiğinde hemen
            // ardından ters yöne dönüyordu. Bu, koşunun başında gözle görülür bir
            // "gidip geri gelme" hareketi yaratıyordu. Yönü artık tamamen BFS seçiyor.
            if (startPos.y < layout.frameBottomY)
            {
                float entryX = Mathf.Clamp(startPos.x, layout.leftmostCubeLeft, layout.rightmostCubeRight);
                Vector3 entryPoint = new Vector3(entryX, layout.bottomCorridorY, -0.30f);
                path.Add(entryPoint);
                List<Vector3> insidePath = FindEmptyCellPath(entryPoint, targetStandPos, allCubes);
                path.AddRange(insidePath);
                EnsurePathEndsAtTarget(path, targetStandPos);
                return path;
            }

            // Madenci zaten çerçevenin içindeyse (ortaya atlayarak inmişse) doğrudan boş koridorlardan hedefe git
            List<Vector3> directPath = FindEmptyCellPath(startPos, targetStandPos, allCubes);
            path.AddRange(directPath);
            EnsurePathEndsAtTarget(path, targetStandPos);
            return path;
        }

        /// <summary>
        /// Rotanın son noktasını tam kazma pozisyonuna oturtur.
        ///
        /// Yol bulma ızgara hücre merkezlerinden geçtiği için son nokta, küpün önündeki
        /// gerçek duruş noktasından sapıyordu (ölçümde 0.07 birim fark görüldü). Madenci
        /// o noktaya varıp Mining durumuna geçince pozisyon farkı kadar ışınlanıyordu.
        /// </summary>
        private static void EnsurePathEndsAtTarget(List<Vector3> path, Vector3 targetStandPos)
        {
            if (path.Count == 0)
            {
                path.Add(targetStandPos);
                return;
            }

            if ((path[path.Count - 1] - targetStandPos).sqrMagnitude > 0.0001f)
            {
                path.Add(targetStandPos);
            }
        }

        /// <summary>
        /// Madenci bloğu kırdıktan sonra çerçevenin sol veya sağındaki en yakın mavi boru kenarına
        /// (bulunduğu anlık Y yüksekliğinde) boş hücre koridorları üzerinden yürüyerek ulaşır.
        /// </summary>
        public static List<Vector3> GenerateEscapeWaypoints(Vector3 currentStandPos, PixelCube[] allCubes)
        {
            List<Vector3> path = new List<Vector3>();
            BoardLayout layout = CalculateBoardLayout(allCubes);

            // Madencinin merkeze göre sol veya sağ tarafta olmasına göre en yakın çıkış tarafı seçilir
            bool isLeft = currentStandPos.x <= layout.frameCenter.x;

            // Madenci hizasındaki Y yüksekliği korunur (en yakın mavi boru kenarı)
            float targetY = Mathf.Clamp(currentStandPos.y, layout.bottomCorridorY, layout.topCorridorY);
            Vector3 chosenEdgePoint = isLeft
                ? new Vector3(layout.leftCorridorX, targetY, -0.30f)
                : new Vector3(layout.rightCorridorX, targetY, -0.30f);

            // Çerçeve içindeki boş koridorlardan seçilen yan boru kenarına ulaş
            List<Vector3> insidePath = FindEmptyCellPath(currentStandPos, chosenEdgePoint, allCubes);
            path.AddRange(insidePath);

            // Kenar zıplama noktası son yol noktası olarak eklenir
            if (path.Count == 0 || (path[path.Count - 1] - chosenEdgePoint).sqrMagnitude > 0.01f)
            {
                path.Add(chosenEdgePoint);
            }

            return path;
        }

        /// <summary>
        /// Izgara üzerindeki boş (kırılmış/küp olmayan) hücrelerden ve çerçevenin iç koridorundan
        /// geçen BFS yol bulma algoritması. Mavi boruların dışına taşmayı ve DOLU BLOKLARIN ÜSTÜNE/ALTINA basmayı engeller.
        /// </summary>
        public static List<Vector3> FindEmptyCellPath(Vector3 startWorldPos, Vector3 targetWorldPos, PixelCube[] allCubes)
        {
            List<Vector3> waypoints = new List<Vector3>();
            if (allCubes == null || allCubes.Length == 0)
            {
                waypoints.Add(new Vector3(targetWorldPos.x, targetWorldPos.y, -0.30f));
                return waypoints;
            }

            BoardLayout layout = CalculateBoardLayout(allCubes);
            var gridMap = GetGridMapCached();

            (int x, int y) WorldToGrid(Vector3 wPos)
            {
                int gx;
                if (wPos.x <= layout.leftmostCubeLeft) gx = -1;
                else if (wPos.x >= layout.rightmostCubeRight) gx = layout.cols;
                else gx = Mathf.Clamp(Mathf.RoundToInt((wPos.x - layout.gridOrigin.x) / layout.cellSize), 0, layout.cols - 1);

                int gy;
                if (wPos.y <= layout.lowestCubeBottom) gy = -1;
                else if (wPos.y >= layout.highestCubeTop) gy = layout.rows;
                else gy = Mathf.Clamp(Mathf.RoundToInt((wPos.y - layout.gridOrigin.y) / layout.cellSize), 0, layout.rows - 1);

                return (gx, gy);
            }

            Vector3 GridToWorld(int gx, int gy)
            {
                float wx;
                if (gx <= -1) wx = layout.leftCorridorX;
                else if (gx >= layout.cols) wx = layout.rightCorridorX;
                else wx = layout.gridOrigin.x + gx * layout.cellSize;

                float wy;
                if (gy <= -1) wy = layout.bottomCorridorY;
                else if (gy >= layout.rows) wy = layout.topCorridorY;
                else wy = layout.gridOrigin.y + gy * layout.cellSize;

                return new Vector3(wx, wy, -0.30f);
            }

            bool IsPassable(int x, int y)
            {
                // Çerçeve sınırları: Çerçevenin iç koridorları x in [-1, cols] ve y in [-1, rows]
                // Bunun dışına çıkmak (mavi borulardan geçmek) kesinlikle yasaktır!
                if (x < -1 || x > layout.cols || y < -1 || y > layout.rows) return false;

                // İç koridordaysa (çerçeve ile küpler arasındaki açık alan): Her zaman geçilebilir
                if (x == -1 || x == layout.cols || y == -1 || y == layout.rows) return true;

                // Izgara içindeki küp alanı: Sadece patlatılmış veya küp bulunmayan boş yerler geçilebilir
                // DOLU BLOKLARIN ÜSTÜNE VEYA ALTINA ASLA BASILAMAZ!
                if (gridMap.TryGetValue((x, y), out PixelCube cube) && cube != null && !cube.IsPopped)
                {
                    return false; // Dolu kırılmamış küp var
                }
                return true; // Kırılmış/boş alan
            }

            // İki ızgara düğümü arasındaki düz çizgi tamamen geçilebilir hücrelerden
            // geçiyor mu? Örnekleme noktasının dokunduğu DÖRT hücre de kontrol edilir,
            // böylece iki dolu küpün köşesi arasından sızmak imkânsız olur.
            bool HasClearLine(int ax, int ay, int bx, int by)
            {
                float dx = bx - ax;
                float dy = by - ay;
                int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 4f);
                if (steps <= 0) return true;

                for (int s = 0; s <= steps; s++)
                {
                    float t = (float)s / steps;
                    float fx = ax + dx * t;
                    float fy = ay + dy * t;

                    int x0 = Mathf.FloorToInt(fx);
                    int x1 = Mathf.CeilToInt(fx);
                    int y0 = Mathf.FloorToInt(fy);
                    int y1 = Mathf.CeilToInt(fy);

                    if (!IsPassable(x0, y0) || !IsPassable(x1, y0) ||
                        !IsPassable(x0, y1) || !IsPassable(x1, y1))
                    {
                        return false;
                    }
                }

                return true;
            }

            (int startGx, int startGy) = WorldToGrid(startWorldPos);
            (int targetGx, int targetGy) = WorldToGrid(targetWorldPos);

            Queue<(int x, int y)> queue = new Queue<(int x, int y)>();
            HashSet<(int x, int y)> visited = new HashSet<(int x, int y)>();
            Dictionary<(int x, int y), (int x, int y)> parentMap = new Dictionary<(int x, int y), (int x, int y)>();

            (int x, int y) startNode = (startGx, startGy);
            (int x, int y) targetNode = (targetGx, targetGy);

            queue.Enqueue(startNode);
            visited.Add(startNode);

            bool found = false;
            (int x, int y)[] dirs = new (int x, int y)[]
            {
                (0, -1), (0, 1), (-1, 0), (1, 0)
            };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == targetNode)
                {
                    found = true;
                    break;
                }

                foreach (var d in dirs)
                {
                    (int nx, int ny) next = (current.x + d.x, current.y + d.y);
                    if (!visited.Contains(next) && IsPassable(next.nx, next.ny))
                    {
                        visited.Add(next);
                        parentMap[next] = current;
                        queue.Enqueue(next);
                    }
                }
            }

            if (found)
            {
                List<(int x, int y)> gridPath = new List<(int x, int y)>();
                (int x, int y) curr = targetNode;
                while (curr != startNode)
                {
                    gridPath.Add(curr);
                    curr = parentMap[curr];
                }
                gridPath.Reverse();

                // Tam düğüm dizisi (başlangıç dahil) üzerinde görüş hattı sadeleştirmesi
                // yapılır: bir düğümden düz çizgiyle görülebilen EN UZAK düğüme atlanır.
                //
                // Eskiden yalnızca "yön değişen düğüm" eklenirdi; ancak eklenen düğüm
                // köşenin kendisi değil ONDAN SONRAKİ düğüm oluyordu. Bu yüzden rota
                // köşeleri çapraz kesiyor, madenci dolu küplerin köşesine sürterek
                // ilerliyor ve her köşede gözle görülür şekilde takılıp yön değiştiriyordu.
                List<(int x, int y)> nodes = new List<(int x, int y)>(gridPath.Count + 1);
                nodes.Add(startNode);
                nodes.AddRange(gridPath);

                int cursor = 0;
                while (cursor < nodes.Count - 1)
                {
                    int next = cursor + 1;

                    for (int far = nodes.Count - 1; far > cursor + 1; far--)
                    {
                        if (HasClearLine(nodes[cursor].x, nodes[cursor].y, nodes[far].x, nodes[far].y))
                        {
                            next = far;
                            break;
                        }
                    }

                    waypoints.Add(GridToWorld(nodes[next].x, nodes[next].y));
                    cursor = next;
                }
            }
            else
            {
                // Doğrudan rota bulunamazsa ASLA dolu küplerin üzerinden geçme!
                // Açık dış koridorlardan dolaş:
                waypoints.Add(GridToWorld(startGx, -1)); // Alt koridor
                waypoints.Add(GridToWorld(targetGx, -1)); // Alt koridorda hedef X hizası
                if (targetGy > -1)
                {
                    // Hedef kenara göre sol veya sağ koridordan yukarı çık
                    int sideX = targetGx <= layout.cols / 2 ? -1 : layout.cols;
                    waypoints.Add(GridToWorld(sideX, -1));
                    waypoints.Add(GridToWorld(sideX, targetGy));
                }
            }

            // Hedef son noktayı ekle (kesinlikle blok üstünde olmayan güvenli Z)
            Vector3 finalPoint = new Vector3(targetWorldPos.x, targetWorldPos.y, -0.30f);

            if (waypoints.Count == 0)
            {
                waypoints.Add(finalPoint);
            }
            else
            {
                Vector3 last = waypoints[waypoints.Count - 1];
                float gap = (last - finalPoint).magnitude;

                if (gap > 0.01f)
                {
                    // Son yol noktası hedefin ızgaraya yuvarlanmış hâliyse, ONU DEĞİŞTİR.
                    // Eski hâlde arkasına ekleniyordu: madenci hücre merkezine kadar
                    // koşup hemen ardından kazma noktasına geri adım atıyordu. Bu geri
                    // adım tam kırma animasyonuna girerken oluyor ve gözle görülür bir
                    // takılma/geri sıçrama olarak fark ediliyordu.
                    if (gap < layout.cellSize * 0.95f)
                    {
                        waypoints[waypoints.Count - 1] = finalPoint;
                    }
                    else
                    {
                        waypoints.Add(finalPoint);
                    }
                }
            }

            return waypoints;
        }



        public static PixelCube FindAndReserveClosestCube(Vector3 fromPosition, Color cargoColor, float threshold)
        {
            PixelCube[] allCubes = GetCubesCached();
            if (allCubes == null || allCubes.Length == 0) return null;

            var gridMap = GetGridMapCached();
            PixelCube bestCube = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < allCubes.Length; i++)
            {
                PixelCube cube = allCubes[i];
                if (cube == null || cube.IsPopped || s_ReservedCubes.Contains(cube)) continue;

                // Dıştan içe kuralı: Sadece erişilebilir/dışarıya açık küpler hedeflenebilir
                if (IsCubeExposed(cube, gridMap) && IsCubeMatchingCargo(cube, cargoColor, threshold))
                {
                    // 1) En alt dikey sıra önceliği: Küçük Y yüksekliği (GridY) en yüksek avantajı alır
                    float yScore = cube.GridY * 50f;

                    // 2) Diğer aktif rezerve edilen küplere yakınlık cezası (Madencileri taban sırasına yayma)
                    float separationPenalty = 0f;
                    foreach (PixelCube reserved in s_ReservedCubes)
                    {
                        if (reserved != null && !reserved.IsPopped)
                        {
                            float dist = Vector2.Distance(cube.transform.position, reserved.transform.position);
                            if (dist < 3.5f)
                            {
                                separationPenalty += (3.5f - dist) * 15f;
                            }
                        }
                    }

                    // 3) Madencinin anlık başlangıç noktasına mesafe
                    float distScore = (cube.transform.position - fromPosition).sqrMagnitude;

                    float totalScore = yScore + separationPenalty + distScore;

                    if (totalScore < bestScore)
                    {
                        bestScore = totalScore;
                        bestCube = cube;
                    }
                }
            }

            if (bestCube != null)
            {
                s_ReservedCubes.Add(bestCube);
            }

            return bestCube;
        }

        public static bool IsCubeMatchingCargo(PixelCube cube, Color cargoColor, float threshold)
        {
            if (cube == null) return false;

            // 1. Ekranda görünen ayarlanmış renk eşleşmesi (CurrentColor)
            if (TruckCargo.ColorDistance(cube.CurrentColor, cargoColor) <= threshold) return true;

            // 2. Küpün ham orijinal renk eşleşmesi (OriginalColor)
            if (TruckCargo.ColorDistance(cube.OriginalColor, cargoColor) <= threshold) return true;

            // 3. Bölüm paletine dönüştürülmüş renk eşleşmesi (ClassifyToPalette)
            TruckDispatcher dispatcher = TruckDispatcher.Instance;
            if (dispatcher != null)
            {
                Color classifiedCurrent = dispatcher.ClassifyToPalette(cube.CurrentColor);
                if (TruckCargo.ColorDistance(classifiedCurrent, cargoColor) <= threshold) return true;

                Color classifiedOriginal = dispatcher.ClassifyToPalette(cube.OriginalColor);
                if (TruckCargo.ColorDistance(classifiedOriginal, cargoColor) <= threshold) return true;
            }

            return false;
        }

        private void UnreserveTarget()
        {
            if (m_TargetCube != null)
            {
                s_ReservedCubes.Remove(m_TargetCube);
                m_TargetCube = null;
            }
        }

        #endregion

        #region 🔄 Durum Makinesi (State Machine)

        private void StartJumpState(Vector3 startPos)
        {
            m_State = State.Jumping;
            m_HasJumpedOutside = false;
            m_IsExitingLeft = false;
            CleanupState();

            if (m_Animator != null && m_Animator.isActiveAndEnabled)
            {
                m_Animator.enabled = true;
                m_Animator.speed = m_IndividualSpeedMultiplier;
                SetAnimatorFloatParameter(s_ClipSpeedHash, GetClipFitSpeed("Jump", m_JumpDuration));
                SetAnimatorBoolParameter(s_IsRunningHash, false, null);
                TriggerAnimatorParameter(s_JumpTriggerHash, s_JumpHashes, 0.08f, UnityEngine.Random.Range(0f, 0.25f));
            }

            BoardLayout layout = CalculateBoardLayout(null);

            // Çerçevenin hemen öte tarafına (iç alt tabana) zıplayıp karşıya iner:
            float targetX = Mathf.Clamp(startPos.x + UnityEngine.Random.Range(-0.15f, 0.15f), layout.leftmostCubeLeft, layout.rightmostCubeRight);
            float landingY = layout.frameBottomY + 0.35f;
            Vector3 jumpTarget = new Vector3(targetX, landingY, -0.30f);

            m_JumpSequence = DOTween.Sequence();
            m_JumpSequence.Join(transform.DOJump(jumpTarget, m_JumpPower, 1, m_JumpDuration).SetEase(Ease.OutQuad));
            m_JumpSequence.Join(transform.DOScale(m_TargetRunningScale, m_JumpDuration).SetEase(Ease.OutQuad));

            // Zıplarken yüzünü iniş yönüne çevir
            Vector3 jumpDir = (jumpTarget - startPos);
            jumpDir.z = 0f;
            if (jumpDir.sqrMagnitude > 0.001f)
            {
                float targetYaw = Mathf.Atan2(jumpDir.x, jumpDir.y) * Mathf.Rad2Deg;
                m_JumpSequence.Join(transform.DORotate(new Vector3(0f, targetYaw, 0f), 0.25f));
            }

            m_JumpSequence.OnComplete(() =>
            {
                m_BasePosition = new Vector3(transform.position.x, transform.position.y, -0.30f);
                StartRunningState();
            });
        }

        private void StartRunningState()
        {
            m_State = State.Running;
            CaptureRunScale();
            // Zamanlayıcı sıfırdan başlar: ilk karede zıplama ofseti de sıfır olur,
            // böylece koşuya geçişte anlık sıçrama olmaz. Senkronsuzluk artık
            // zamanlayıcıyı kaydırarak değil, zıplama hızını çeşitleyerek sağlanıyor.
            m_RunTimer = 0f;
            m_BounceSpeedMultiplier = UnityEngine.Random.Range(0.82f, 1.22f);
            m_CurrentWaypointIndex = 0;

            PixelCube[] allCubes = GetCubesCached();
            var gridMap = GetGridMapCached();

            if (m_TargetCube != null)
            {
                m_MiningStandPosition = GetMiningStandPosition(m_TargetCube, gridMap);
                m_PathWaypoints = GeneratePathWaypoints(m_BasePosition, m_MiningStandPosition, allCubes);
            }
            else
            {
                m_PathWaypoints = new List<Vector3> { m_BasePosition };
            }

            if (m_Animator != null && m_Animator.isActiveAndEnabled)
            {
                m_Animator.enabled = true;
                m_Animator.speed = m_IndividualSpeedMultiplier;
                SetAnimatorFloatParameter(s_RunSpeedMulHash, RunAnimationSpeed);
                m_Animator.ResetTrigger(s_JumpTriggerHash);
                m_Animator.ResetTrigger(s_AttackTriggerHash);
                SetAnimatorBoolParameter(s_IsRunningHash, true, s_RunHashes, 0.12f);
            }
        }

        private void Update()
        {
            if (m_State == State.Running)
            {
                UpdateRunning();
            }
            else if (m_State == State.Escaping)
            {
                UpdateEscaping();
            }
        }

        private void UpdateRunning()
        {
            PixelCube[] cubes = GetCubesCached();

            // Hedef küp geçerliliğini koruyor mu?
            if (m_TargetCube == null || m_TargetCube.IsPopped)
            {
                UnreserveTarget();

                // Eğer madenci zaten 1 küp kırdıysa veya başka hedef yoksa KESİNLİKLE yeni hedef arama, doğrudan çıkışa git
                if (m_HasMinedOneCube)
                {
                    StartEscapingState();
                    return;
                }

                m_TargetCube = FindAndReserveClosestCube(transform.position, m_MinerColor, m_ColorThreshold);

                if (m_TargetCube == null)
                {
                    // Yeni hedef bulunamadıysa hemen kaybolmak yerine dışarı kaç
                    StartEscapingState();
                    return;
                }

                // Yeni hedef için yol noktalarını yeniden hesapla (bulunduğu anlık konumdan hesaplanır)
                var map = GetGridMapCached();
                m_MiningStandPosition = GetMiningStandPosition(m_TargetCube, map);
                m_BasePosition = transform.position;
                m_PathWaypoints = GeneratePathWaypoints(transform.position, m_MiningStandPosition, cubes);
                m_CurrentWaypointIndex = 0;
            }

            // Koşma animasyonunun kesintisiz ve pürüzsüz akması için sadece hız güncellenir
            if (m_Animator != null)
            {
                m_Animator.speed = m_IndividualSpeedMultiplier;
                SetAnimatorFloatParameter(s_RunSpeedMulHash, RunAnimationSpeed);
            }

            if (m_PathWaypoints == null || m_CurrentWaypointIndex >= m_PathWaypoints.Count)
            {
                StartMiningState();
                return;
            }

            bool isLastWaypoint = (m_CurrentWaypointIndex == m_PathWaypoints.Count - 1);
            Vector3 targetWaypoint = m_PathWaypoints[m_CurrentWaypointIndex];
            Vector3 dir = (targetWaypoint - m_BasePosition);
            float distance = dir.magnitude;

            // Ara noktalarda koridor köşelerini yumuşak dönmek için 0.15f toleransı kullanılır.
            // Son noktada (küp önü kırma pozisyonunda) ise madenci tam hedefe kadar yavaşlayarak yanaşır.
            if (!isLastWaypoint && distance < GetReachDistance(EffectiveRunSpeed * Time.deltaTime))
            {
                m_CurrentWaypointIndex++;
                if (m_CurrentWaypointIndex >= m_PathWaypoints.Count)
                {
                    StartMiningState();
                    return;
                }
                isLastWaypoint = (m_CurrentWaypointIndex == m_PathWaypoints.Count - 1);
                targetWaypoint = m_PathWaypoints[m_CurrentWaypointIndex];
                dir = (targetWaypoint - m_BasePosition);
                distance = dir.magnitude;
            }

            Vector3 normDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;

            // Hedefe yaklaşırken yumuşak yavaşlama (Ease-Out Deceleration) ve sekme sönümleme
            float speedMultiplier = 1.0f;
            float bounceDamping = 1.0f;

            if (isLastWaypoint)
            {
                const float k_DecelDist = 0.50f;
                if (distance < k_DecelDist)
                {
                    float t = Mathf.Clamp01(distance / k_DecelDist);
                    // Pürüzsüz hız eğrisi: minimum %25 hızla durma noktasına nazikçe yanaşır
                    speedMultiplier = Mathf.Lerp(0.25f, 1.0f, Mathf.SmoothStep(0f, 1f, t));
                    bounceDamping = Mathf.SmoothStep(0f, 1f, t);
                }
            }

            float currentSpeed = EffectiveRunSpeed * speedMultiplier;
            float step = currentSpeed * Time.deltaTime;

            // Son noktaya varış kontrolü: Kademeli yavaşlama bittiğinde hedefe tam oturur
            if (isLastWaypoint && distance <= Mathf.Max(0.02f, step))
            {
                m_BasePosition = targetWaypoint;
                transform.position = targetWaypoint;
                StartMiningState();
                return;
            }

            m_BasePosition += normDir * Mathf.Min(step, distance);

            // Zıplama ritmi (hedefe varırken pürüzsüzce sönümlenir, zemine yumuşakça basar)
            m_RunTimer += Time.deltaTime;
            float bounceOffset = Mathf.Abs(Mathf.Sin(m_RunTimer * m_BounceFrequency * m_BounceSpeedMultiplier)) * m_BounceHeight * bounceDamping;

            // Zıplama EKRAN YUKARISINA (+Y) uygulanır, kameraya doğru (-Z) DEĞİL.
            //
            // Kamera (0, 1, -10) konumunda, rotasyonsuz, tam +Z'ye bakıyor. Zıplama
            // -Z'de olduğunda madenci kameraya yaklaşıp uzaklaşıyor; perspektif de
            // sahneyi o nokta etrafında büyütüp küçültüyordu. Ölçümde dünya hızı
            // ±%0.5 sabitken ekrandaki hız 17.6 ↔ 21.9 piksel arasında, saniyede
            // ~3 kez salınıyordu (%11 hız değişimi) ve bu "ilerleyip takılıp yine
            // ilerleme" olarak görünüyordu. Etki ekran merkezinden uzaklaştıkça
            // büyüdüğü için tahtanın sol/sağ kenarlarında en belirgindi.
            //
            // Buna karşılık zıplamanın görünür faydası yoktu: ekran Y'si yalnızca
            // 1.5 piksel oynuyordu. +Y'de ise zıplama gerçekten görünür ve derinlik
            // hiç değişmediği için yatay hareket kusursuz düzgün akar.
            transform.position = m_BasePosition + Vector3.up * bounceOffset;

            // Karakter yönü: Koşarken koridor yönüne döner, son yaklaşmada hedef küpe doğru yumuşakça hizalanır
            if (isLastWaypoint && m_TargetCube != null && distance < 0.50f)
            {
                Vector3 faceDir = (m_TargetCube.transform.position - m_MiningStandPosition);
                if (faceDir.x != 0f || faceDir.y != 0f)
                {
                    float faceYaw = Mathf.Atan2(faceDir.x, faceDir.y) * Mathf.Rad2Deg;
                    Quaternion faceRot = Quaternion.Euler(0f, faceYaw, 0f);

                    if (dir.sqrMagnitude > 0.005f)
                    {
                        float moveYaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                        Quaternion moveRot = Quaternion.Euler(0f, moveYaw, 0f);
                        float blendT = 1f - Mathf.Clamp01(distance / 0.50f);
                        Quaternion blendedTarget = Quaternion.Slerp(moveRot, faceRot, blendT * 0.85f);
                        transform.rotation = Quaternion.Slerp(transform.rotation, blendedTarget, 12.0f * Time.deltaTime);
                    }
                    else
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, faceRot, 12.0f * Time.deltaTime);
                    }
                }
            }
            else if (dir.sqrMagnitude > 0.005f)
            {
                float targetYaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                Quaternion targetRot = Quaternion.Euler(0f, targetYaw, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 14.0f * Time.deltaTime);
            }
        }

        private void StartMiningState()
        {
            m_State = State.Mining;
            m_BasePosition = m_MiningStandPosition;
            transform.position = m_MiningStandPosition;

            if (m_TargetCube != null)
            {
                // Kırılacak bloğa yumuşak ve doğal bir kavisle dön (0.28s, Ease.OutQuad)
                Vector3 faceDir = (m_TargetCube.transform.position - m_MiningStandPosition);
                if (faceDir.x != 0f || faceDir.y != 0f)
                {
                    float targetYaw = Mathf.Atan2(faceDir.x, faceDir.y) * Mathf.Rad2Deg;
                    transform.DORotate(new Vector3(0f, targetYaw, 0f), 0.28f).SetEase(Ease.OutQuad);
                }
            }

            // Blok önünde koşma durur, vurma/kazma animasyonu pürüzsüz crossfade (0.25s) ile başlar (wind-up atlanmaz)
            if (m_Animator != null && m_Animator.isActiveAndEnabled)
            {
                m_Animator.enabled = true;
                m_Animator.speed = m_IndividualSpeedMultiplier;
                SetAnimatorFloatParameter(s_ClipSpeedHash, GetClipFitSpeed("Punch", m_PunchImpactDelay + m_PostPunchDelay));
                SetAnimatorBoolParameter(s_IsRunningHash, false, null);
                TriggerAnimatorParameter(s_AttackTriggerHash, s_AttackHashes, 0.25f, 0f);
            }

            m_StateCoroutine = StartCoroutine(MiningRoutine());
        }

        private IEnumerator MiningRoutine()
        {
            // 1) Blok önünde durur, vurma animasyonu başlar ve darbe hedefe iner
            yield return new WaitForSeconds(m_PunchImpactDelay);

            // 2) Darbe temas anında hedef küp patlar (ve bu madenci 1 küp kırma hakkını doldurur)
            if (m_TargetCube != null && !m_TargetCube.IsPopped)
            {
                m_TargetCube.BurstAndDestroy();
                m_HasMinedOneCube = true;
            }

            UnreserveTarget();
            
            // 3) Vuruş hareketini tamamlayana kadar kısa bir an duraklar
            yield return new WaitForSeconds(m_PostPunchDelay);

            // 4) Vuruş tamamlanınca tekrar koşma durumuna geçerek panodan dışarı kaçar
            StartEscapingState();
        }

        private void StartEscapingState()
        {
            m_State = State.Escaping;
            CaptureRunScale();
            m_HasJumpedOutside = false;
            // Zamanlayıcı sıfırdan başlar: ilk karede zıplama ofseti de sıfır olur,
            // böylece koşuya geçişte anlık sıçrama olmaz. Senkronsuzluk artık
            // zamanlayıcıyı kaydırarak değil, zıplama hızını çeşitleyerek sağlanıyor.
            m_RunTimer = 0f;
            m_BounceSpeedMultiplier = UnityEngine.Random.Range(0.82f, 1.22f);
            m_CurrentWaypointIndex = 0;
            m_BasePosition = transform.position;

            BoardLayout layout = CalculateBoardLayout(null);
            m_IsExitingLeft = transform.position.x <= layout.frameCenter.x;

            float targetY = Mathf.Clamp(transform.position.y, layout.bottomCorridorY, layout.topCorridorY);
            Vector3 chosenEdgePoint = m_IsExitingLeft
                ? new Vector3(layout.leftCorridorX, targetY, -0.30f)
                : new Vector3(layout.rightCorridorX, targetY, -0.30f);

            // Madenci KESİNLİKLE en yakın mavi boru kenarının hemen dibindeyse anında atlamayı başlat, aksi halde navigasyon rotası oluştur
            if ((transform.position - chosenEdgePoint).sqrMagnitude <= 0.05f)
            {
                StartEscapeJump();
                return;
            }

            PixelCube[] allCubes = GetCubesCached();

            m_PathWaypoints = GenerateEscapeWaypoints(transform.position, allCubes);

            if (m_Animator != null && m_Animator.isActiveAndEnabled)
            {
                m_Animator.enabled = true;
                m_Animator.speed = m_IndividualSpeedMultiplier;
                SetAnimatorFloatParameter(s_RunSpeedMulHash, RunAnimationSpeed);
                m_Animator.ResetTrigger(s_JumpTriggerHash);
                m_Animator.ResetTrigger(s_AttackTriggerHash);
                // Durum makinesine bırakılır. Ek bir CrossFade, bool ile başlayan
                // geçişin üstüne ikinci bir zorlamalı geçiş bindirip takılma yaratıyordu.
                SetAnimatorBoolParameter(s_IsRunningHash, true, s_RunHashes, 0.20f);
            }
        }

        private void UpdateEscaping()
        {
            BoardLayout layout = CalculateBoardLayout(null);
            float frameLeftX = layout.frameCenter.x - layout.fullWidth * 0.5f;
            float frameRightX = layout.frameCenter.x + layout.fullWidth * 0.5f;

            // 1) Eğer çerçevenin dışına zaten zıpladıysa (m_HasJumpedOutside == true)
            if (m_HasJumpedOutside)
            {
                bool isPastOffscreen = m_IsExitingLeft
                    ? (m_BasePosition.x < frameLeftX - 3.0f)
                    : (m_BasePosition.x > frameRightX + 3.0f);

                if (isPastOffscreen || m_PathWaypoints == null || m_CurrentWaypointIndex >= m_PathWaypoints.Count)
                {
                    m_State = State.Done;
                    Release();
                    return;
                }
            }

            // Koşma animasyonunun kesintisiz ve pürüzsüz akması için sadece hız güncellenir
            if (m_Animator != null)
            {
                m_Animator.speed = m_IndividualSpeedMultiplier;
                SetAnimatorFloatParameter(s_RunSpeedMulHash, RunAnimationSpeed);
            }

            Vector3 targetWaypoint = m_PathWaypoints != null && m_CurrentWaypointIndex < m_PathWaypoints.Count
                ? m_PathWaypoints[m_CurrentWaypointIndex]
                : (m_IsExitingLeft
                    ? new Vector3(layout.leftCorridorX, Mathf.Clamp(m_BasePosition.y, layout.bottomCorridorY, layout.topCorridorY), -0.30f)
                    : new Vector3(layout.rightCorridorX, Mathf.Clamp(m_BasePosition.y, layout.bottomCorridorY, layout.topCorridorY), -0.30f));

            Vector3 dir = (targetWaypoint - m_BasePosition);
            float distance = dir.magnitude;

            bool isLastWaypoint = m_PathWaypoints != null &&
                                  m_CurrentWaypointIndex == m_PathWaypoints.Count - 1;

            float step = EffectiveRunSpeed * Time.deltaTime;

            // Son yol noktası kenardaki zıplama noktasıdır; oraya tam oturmalı,
            // yoksa kaçış zıplaması yanlış yerden başlar
            if (isLastWaypoint && distance <= Mathf.Max(0.02f, step))
            {
                m_BasePosition = targetWaypoint;

                if (m_HasJumpedOutside)
                {
                    m_State = State.Done;
                    Release();
                }
                else
                {
                    StartEscapeJump();
                }
                return;
            }

            // Ara noktalarda köşeyi yumuşak dönmek için hücre boyutuna bağlı tolerans.
            // Sabit bir tolerans hücreden büyük kalırsa madenci hedefe varmadan
            // yön değiştirir ve keskin köşelerde bir kare geri sıçrar.
            if (!isLastWaypoint && distance < GetReachDistance(step))
            {
                m_CurrentWaypointIndex++;
                if (m_PathWaypoints == null || m_CurrentWaypointIndex >= m_PathWaypoints.Count)
                {
                    if (m_HasJumpedOutside)
                    {
                        m_State = State.Done;
                        Release();
                    }
                    else
                    {
                        StartEscapeJump();
                    }
                    return;
                }
                targetWaypoint = m_PathWaypoints[m_CurrentWaypointIndex];
                dir = (targetWaypoint - m_BasePosition);
                distance = dir.magnitude;
            }

            Vector3 normDir = dir.normalized;
            m_BasePosition += normDir * Mathf.Min(step, distance);

            m_RunTimer += Time.deltaTime;
            float bounceOffset = Mathf.Abs(Mathf.Sin(m_RunTimer * m_BounceFrequency * m_BounceSpeedMultiplier)) * m_BounceHeight;

            // Zıplama EKRAN YUKARISINA (+Y) uygulanır, kameraya doğru (-Z) DEĞİL.
            //
            // Kamera (0, 1, -10) konumunda, rotasyonsuz, tam +Z'ye bakıyor. Zıplama
            // -Z'de olduğunda madenci kameraya yaklaşıp uzaklaşıyor; perspektif de
            // sahneyi o nokta etrafında büyütüp küçültüyordu. Ölçümde dünya hızı
            // ±%0.5 sabitken ekrandaki hız 17.6 ↔ 21.9 piksel arasında, saniyede
            // ~3 kez salınıyordu (%11 hız değişimi) ve bu "ilerleyip takılıp yine
            // ilerleme" olarak görünüyordu. Etki ekran merkezinden uzaklaştıkça
            // büyüdüğü için tahtanın sol/sağ kenarlarında en belirgindi.
            //
            // Buna karşılık zıplamanın görünür faydası yoktu: ekran Y'si yalnızca
            // 1.5 piksel oynuyordu. +Y'de ise zıplama gerçekten görünür ve derinlik
            // hiç değişmediği için yatay hareket kusursuz düzgün akar.
            transform.position = m_BasePosition + Vector3.up * bounceOffset;

            // Karakterin şu anki koridor yönüne pürüzsüz dönmesi (aniden sert takılma yapmaz)
            if (dir.sqrMagnitude > 0.005f)
            {
                float targetYaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                Quaternion targetRot = Quaternion.Euler(0f, targetYaw, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 14.0f * Time.deltaTime);
            }
        }

        /// <summary>
        /// Madenci sol veya sağ çerçeve kenarına ulaştığında çerçevenin üstünden dışarı (sola veya sağa) zıplayarak karşıya iner.
        /// Borunun içine girmeden daha geriden zıplar ve borunun üzerinden aşarak dış zemine konar.
        /// </summary>
        private void StartEscapeJump()
        {
            m_State = State.Jumping; // State.Jumping yaparak UpdateEscaping döngüsünü durduruyoruz
            CleanupState();

            if (m_Animator != null && m_Animator.isActiveAndEnabled)
            {
                m_Animator.enabled = true;
                m_Animator.speed = m_IndividualSpeedMultiplier;
                SetAnimatorFloatParameter(s_ClipSpeedHash, GetClipFitSpeed("Jump", Mathf.Max(0.70f, m_JumpDuration)));
                SetAnimatorBoolParameter(s_IsRunningHash, false, null);
                TriggerAnimatorParameter(s_JumpTriggerHash, s_JumpHashes, 0.08f, UnityEngine.Random.Range(0f, 0.25f));
            }

            BoardLayout layout = CalculateBoardLayout(null);
            Vector3 currentPos = transform.position;

            float frameLeftX = layout.frameCenter.x - layout.fullWidth * 0.5f;
            float frameRightX = layout.frameCenter.x + layout.fullWidth * 0.5f;

            // Merkezin solundaysa sola, sağındaysa sağa atlayarak kapıdan çıkar
            bool isLeft = m_IsExitingLeft || (currentPos.x <= layout.frameCenter.x);
            m_IsExitingLeft = isLeft;

            // Dışarı iniş noktası: Madencinin bulunduğu anlık Y yüksekliğinde mavi borunun dışı
            float outsideLandingX = isLeft ? (frameLeftX - 1.20f) : (frameRightX + 1.20f);
            float targetLandingY = Mathf.Clamp(currentPos.y, layout.bottomCorridorY, layout.topCorridorY);
            Vector3 outsideLanding = new Vector3(outsideLandingX, targetLandingY, -0.30f);

            // Zıplarken sola (-90) veya sağa (+90) yüzünü yumuşakça dön
            float targetYaw = isLeft ? -90f : 90f;
            transform.DORotate(new Vector3(0f, targetYaw, 0f), 0.15f).SetEase(Ease.OutQuad);

            // Geriden atlayıp borunun üzerinden yüksek ve temiz bir yayla geçsin:
            float jumpDuration = Mathf.Max(0.70f, m_JumpDuration);
            float jumpPower = Mathf.Max(0.70f, m_JumpPower);

            m_JumpSequence = DOTween.Sequence();
            m_JumpSequence.Join(transform.DOJump(outsideLanding, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad));

            m_JumpSequence.OnComplete(() =>
            {
                m_HasJumpedOutside = true;
                // Dışarı karşıya indi! Şimdi koşarak ekranın soluna veya sağına doğru uzaklaşsın
                StartRunAwayState(outsideLanding, isLeft);
            });
        }

        private void StartRunAwayState(Vector3 startRunPos, bool isLeft)
        {
            m_State = State.Escaping;
            CaptureRunScale();
            m_HasJumpedOutside = true;
            m_IsExitingLeft = isLeft;
            m_RunTimer = 0f;
            m_CurrentWaypointIndex = 0;
            m_BasePosition = startRunPos;

            // Dışarıda sola veya sağa doğru ekran dışına koşma hedefi
            float offscreenX = isLeft ? (startRunPos.x - 4.5f) : (startRunPos.x + 4.5f);
            Vector3 offscreenTarget = new Vector3(offscreenX, startRunPos.y, -0.30f);
            m_PathWaypoints = new List<Vector3> { offscreenTarget };

            if (m_Animator != null && m_Animator.isActiveAndEnabled)
            {
                m_Animator.enabled = true;
                m_Animator.speed = m_IndividualSpeedMultiplier;
                SetAnimatorFloatParameter(s_RunSpeedMulHash, RunAnimationSpeed);
                SetAnimatorBoolParameter(s_IsRunningHash, true, s_RunHashes, 0.12f);
            }
        }

        #endregion

        #region 🎨 Görsel & Materyal

        private void ApplyColor(Color color)
        {
            TruckPaint paint = GetComponent<TruckPaint>();
            if (paint == null) paint = GetComponentInChildren<TruckPaint>();

            if (paint != null)
            {
                PixelArtGenerator gen = UnityEngine.Object.FindFirstObjectByType<PixelArtGenerator>();
                PixelLevelData level = gen != null ? gen.ActiveLevelData : null;
                if (level != null && level.ColorTheme != null)
                {
                    paint.ApplyTheme(level.ColorTheme, color);
                }
                else
                {
                    paint.SetBodyColor(color);
                    paint.SetMinerColors(color, color);
                    paint.Apply();
                }
                return;
            }

            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                if (m_Material == null)
                {
                    m_Material = CartoonShader.CreateMaterial(color, "Miner_Mat");
                    rend.sharedMaterial = m_Material;
                }
                else
                {
                    CartoonShader.ApplyColor(m_Material, color);
                }
            }
        }

        private void CleanupState()
        {
            if (m_JumpSequence != null)
            {
                m_JumpSequence.Kill();
                m_JumpSequence = null;
            }
            if (m_StateCoroutine != null)
            {
                StopCoroutine(m_StateCoroutine);
                m_StateCoroutine = null;
            }
            transform.DOKill();
        }

        private void OnDisable()
        {
            CleanupState();
        }

        private void OnDestroy()
        {
            CleanupState();
            UnreserveTarget();
            s_ActiveMiners.Remove(this);
        }

        #endregion
    }
}
