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
        private Coroutine m_StateCoroutine;
        private bool m_HasJumpedOutside;
        private bool m_IsExitingLeft;
        private float m_IndividualSpeedMultiplier = 1.0f;
        private bool m_HasMinedOneCube = false;

        public State CurrentState => m_State;
        public PixelCube TargetCube => m_TargetCube;

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
            PixelArtGenerator gen = UnityEngine.Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen == null || gen.CubesContainer == null) return false;

            PixelCube[] allCubes = gen.CubesContainer.GetComponentsInChildren<PixelCube>();
            if (allCubes == null || allCubes.Length == 0) return false;

            var gridMap = BuildCubeGridMap(allCubes);

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
            PixelArtGenerator gen = UnityEngine.Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen == null || gen.CubesContainer == null) return false;

            PixelCube[] allCubes = gen.CubesContainer.GetComponentsInChildren<PixelCube>();
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

                layout.bottomCorridorY = (layout.frameBottomY + layout.lowestCubeBottom) * 0.5f;
                layout.topCorridorY = (frameTopY + layout.highestCubeTop) * 0.5f;
                float takeoffMargin = Mathf.Max(0.55f, layout.cellSize * 2.2f);
                layout.leftCorridorX = frameLeftX + takeoffMargin;
                layout.rightCorridorX = frameRightX - takeoffMargin;

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

            // Çerçeve boruları ile bloklar arasındaki açık iç koridorların merkezleri:
            float fLeftX = layout.frameCenter.x - layout.fullWidth * 0.5f;
            float fRightX = layout.frameCenter.x + layout.fullWidth * 0.5f;
            float fTopY = layout.frameCenter.y + layout.fullHeight * 0.5f;

            layout.bottomCorridorY = (layout.frameBottomY + layout.lowestCubeBottom) * 0.5f;
            layout.topCorridorY = (fTopY + layout.highestCubeTop) * 0.5f;
            float fallbackTakeoffMargin = Mathf.Max(0.55f, layout.cellSize * 2.2f);
            layout.leftCorridorX = fLeftX + fallbackTakeoffMargin;
            layout.rightCorridorX = fRightX - fallbackTakeoffMargin;

            s_CachedLayout = layout;
            s_LayoutCached = true;
            return layout;
        }

        /// <summary>
        /// Kırma noktası: Bloğun kesinlikle dış tarafında (Alt, Sol, Sağ veya Üst)
        /// madencinin duracağı noktayı hesaplar. Bloğun üstüne veya altına girmesini engeller.
        /// Kullanıcının isteği doğrultusunda bloğun hemen dibine (0.65x) kadar yanaşır.
        /// </summary>
        public static Vector3 GetMiningStandPosition(PixelCube cube, Dictionary<(int, int), PixelCube> gridMap)
        {
            if (cube == null) return Vector3.zero;

            Vector3 cubePos = cube.transform.position;
            float cubeScale = Mathf.Max(0.35f, cube.transform.localScale.x);
            float offset = cubeScale * 0.65f;

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

            // Madenci çerçevenin altındaysa (dışarıdaysa), alt koridora geçiş noktası ekle
            if (startPos.y < layout.frameBottomY)
            {
                float entryX = Mathf.Clamp(targetStandPos.x, layout.leftmostCubeLeft, layout.rightmostCubeRight);
                Vector3 entryPoint = new Vector3(entryX, layout.bottomCorridorY, -0.30f);
                path.Add(entryPoint);
                List<Vector3> insidePath = FindEmptyCellPath(entryPoint, targetStandPos, allCubes);
                path.AddRange(insidePath);
                return path;
            }

            // Madenci zaten çerçevenin içindeyse (ortaya atlayarak inmişse) doğrudan boş koridorlardan hedefe git
            List<Vector3> directPath = FindEmptyCellPath(startPos, targetStandPos, allCubes);
            path.AddRange(directPath);
            return path;
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
            var gridMap = BuildCubeGridMap(allCubes);

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

                (int x, int y) prevDir = (0, 0);
                for (int i = 0; i < gridPath.Count; i++)
                {
                    var p = gridPath[i];
                    var prevNode = i == 0 ? startNode : gridPath[i - 1];
                    (int x, int y) curDir = (p.x - prevNode.x, p.y - prevNode.y);

                    if (curDir != prevDir || i == gridPath.Count - 1)
                    {
                        waypoints.Add(GridToWorld(p.x, p.y));
                        prevDir = curDir;
                    }
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
            if (waypoints.Count == 0 || (waypoints[waypoints.Count - 1] - finalPoint).sqrMagnitude > 0.01f)
            {
                waypoints.Add(finalPoint);
            }

            return waypoints;
        }



        public static PixelCube FindAndReserveClosestCube(Vector3 fromPosition, Color cargoColor, float threshold)
        {
            PixelArtGenerator gen = UnityEngine.Object.FindFirstObjectByType<PixelArtGenerator>();

            if (gen == null || gen.CubesContainer == null) return null;

            PixelCube[] allCubes = gen.CubesContainer.GetComponentsInChildren<PixelCube>();
            if (allCubes == null || allCubes.Length == 0) return null;

            var gridMap = BuildCubeGridMap(allCubes);
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
                m_Animator.speed = 1f * m_IndividualSpeedMultiplier;
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
            m_RunTimer = UnityEngine.Random.Range(0f, 10f);
            m_CurrentWaypointIndex = 0;

            PixelArtGenerator gen = UnityEngine.Object.FindFirstObjectByType<PixelArtGenerator>();
            PixelCube[] allCubes = gen != null && gen.CubesContainer != null ? gen.CubesContainer.GetComponentsInChildren<PixelCube>() : null;
            var gridMap = BuildCubeGridMap(allCubes);

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
                m_Animator.speed = Mathf.Clamp(m_RunSpeed * 1.25f * m_IndividualSpeedMultiplier, 0.70f, 1.45f);
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
            PixelArtGenerator genObj = UnityEngine.Object.FindFirstObjectByType<PixelArtGenerator>();
            PixelCube[] cubes = genObj != null && genObj.CubesContainer != null ? genObj.CubesContainer.GetComponentsInChildren<PixelCube>() : null;

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
                var map = BuildCubeGridMap(cubes);
                m_MiningStandPosition = GetMiningStandPosition(m_TargetCube, map);
                m_BasePosition = transform.position;
                m_PathWaypoints = GeneratePathWaypoints(transform.position, m_MiningStandPosition, cubes);
                m_CurrentWaypointIndex = 0;
            }

            // Koşma animasyonunun kesintisiz ve pürüzsüz akması için sadece hız güncellenir
            if (m_Animator != null)
            {
                m_Animator.speed = Mathf.Clamp(m_RunSpeed * 1.25f * m_IndividualSpeedMultiplier, 0.70f, 1.45f);
            }

            if (m_PathWaypoints == null || m_CurrentWaypointIndex >= m_PathWaypoints.Count)
            {
                StartMiningState();
                return;
            }

            Vector3 targetWaypoint = m_PathWaypoints[m_CurrentWaypointIndex];
            Vector3 dir = (targetWaypoint - m_BasePosition);
            float distance = dir.magnitude;

            if (distance < 0.15f)
            {
                m_CurrentWaypointIndex++;
                if (m_CurrentWaypointIndex >= m_PathWaypoints.Count)
                {
                    StartMiningState();
                    return;
                }
                targetWaypoint = m_PathWaypoints[m_CurrentWaypointIndex];
                dir = (targetWaypoint - m_BasePosition);
                distance = dir.magnitude;
            }

            Vector3 normDir = dir.normalized;
            float step = m_RunSpeed * Time.deltaTime;
            m_BasePosition += normDir * Mathf.Min(step, distance);

            // Zıplama ritmi (Sinüs dalgası)
            m_RunTimer += Time.deltaTime;
            float bounceOffset = Mathf.Abs(Mathf.Sin(m_RunTimer * m_BounceFrequency)) * m_BounceHeight;

            transform.position = m_BasePosition + Vector3.back * bounceOffset;

            // Karakterin şu anki koridor yönüne pürüzsüz dönmesi (aniden sert takılma yapmaz)
            if (dir.sqrMagnitude > 0.005f)
            {
                float targetYaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                Quaternion targetRot = Quaternion.Euler(0f, targetYaw, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 14.0f * Time.deltaTime);
            }
        }

        private void StartMiningState()
        {
            m_State = State.Mining;
            if (m_TargetCube != null)
            {
                m_BasePosition = m_MiningStandPosition;

                // Kırılacak bloğa yumuşakça dön
                Vector3 faceDir = (m_TargetCube.transform.position - m_MiningStandPosition);
                if (faceDir.x != 0f || faceDir.y != 0f)
                {
                    float targetYaw = Mathf.Atan2(faceDir.x, faceDir.y) * Mathf.Rad2Deg;
                    transform.DORotate(new Vector3(0f, targetYaw, 0f), 0.10f).SetEase(Ease.OutQuad);
                }
            }

            // Blok önünde koşma durur, vurma/kazma animasyonu pürüzsüz crossfade ile başlar
            if (m_Animator != null && m_Animator.isActiveAndEnabled)
            {
                m_Animator.enabled = true;
                m_Animator.speed = 1.0f * m_IndividualSpeedMultiplier;
                SetAnimatorBoolParameter(s_IsRunningHash, false, null);
                TriggerAnimatorParameter(s_AttackTriggerHash, s_AttackHashes, 0.12f, UnityEngine.Random.Range(0f, 0.15f));
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
            m_HasJumpedOutside = false;
            m_RunTimer = UnityEngine.Random.Range(0f, 10f);
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

            PixelArtGenerator gen = UnityEngine.Object.FindFirstObjectByType<PixelArtGenerator>();
            PixelCube[] allCubes = gen != null && gen.CubesContainer != null ? gen.CubesContainer.GetComponentsInChildren<PixelCube>() : null;

            m_PathWaypoints = GenerateEscapeWaypoints(transform.position, allCubes);

            if (m_Animator != null && m_Animator.isActiveAndEnabled)
            {
                m_Animator.enabled = true;
                m_Animator.speed = Mathf.Clamp(m_RunSpeed * 1.25f * m_IndividualSpeedMultiplier, 0.70f, 1.45f);
                m_Animator.ResetTrigger(s_JumpTriggerHash);
                m_Animator.ResetTrigger(s_AttackTriggerHash);
                SetAnimatorBoolParameter(s_IsRunningHash, true, s_RunHashes, 0.15f);
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
                m_Animator.speed = Mathf.Clamp(m_RunSpeed * 1.25f * m_IndividualSpeedMultiplier, 0.70f, 1.45f);
            }

            Vector3 targetWaypoint = m_PathWaypoints != null && m_CurrentWaypointIndex < m_PathWaypoints.Count
                ? m_PathWaypoints[m_CurrentWaypointIndex]
                : (m_IsExitingLeft
                    ? new Vector3(layout.leftCorridorX, Mathf.Clamp(m_BasePosition.y, layout.bottomCorridorY, layout.topCorridorY), -0.30f)
                    : new Vector3(layout.rightCorridorX, Mathf.Clamp(m_BasePosition.y, layout.bottomCorridorY, layout.topCorridorY), -0.30f));

            Vector3 dir = (targetWaypoint - m_BasePosition);
            float distance = dir.magnitude;

            if (distance < 0.15f)
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
            float step = m_RunSpeed * Time.deltaTime;
            m_BasePosition += normDir * Mathf.Min(step, distance);

            m_RunTimer += Time.deltaTime;
            float bounceOffset = Mathf.Abs(Mathf.Sin(m_RunTimer * m_BounceFrequency)) * m_BounceHeight;

            transform.position = m_BasePosition + Vector3.back * bounceOffset;

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
                m_Animator.speed = 1f * m_IndividualSpeedMultiplier;
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
                m_Animator.speed = Mathf.Clamp(m_RunSpeed * 1.25f * m_IndividualSpeedMultiplier, 0.70f, 1.45f);
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
                paint.SetBodyColor(color);
                paint.SetMinerColors(color, color);
                paint.Apply();
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
