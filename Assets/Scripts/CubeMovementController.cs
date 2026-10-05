using System;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Küp karakterlerin gemi ve arabalara doğru ilerlerken referans videodaki gibi akıcı,
    /// organik, canlı ve hypercasual hissettiren karakter hareket katmanı (Movement Controller).
    /// </summary>
    [DisallowMultipleComponent]
    public class CubeMovementController : MonoBehaviour
    {
        public enum MovementState
        {
            Idle,
            Anticipation,
            Moving,
            Arriving,
            Settling,
            Boarding,
            Completed
        }

        [SerializeField] private CubeMovementSettings m_Settings;
        public CubeMovementSettings Settings
        {
            get => m_Settings != null ? m_Settings : CubeMovementSettings.Default;
            set => m_Settings = value;
        }

        // Durumlar
        private MovementState m_State = MovementState.Idle;
        public MovementState State => m_State;
        public bool IsFinished => m_State == MovementState.Completed;

        // Yol ve Hedef
        private ShoreLanePath m_Path;
        private Vector3 m_StartPos;
        private Vector3 m_Destination;
        private float m_CurrentDist;
        private float m_TotalPathLength;
        private Action m_OnArrivedCallback;

        // Hız ve İvme
        private float m_CurrentSpeed;
        private float m_TargetCruiseSpeed;
        private float m_SpeedMultiplier = 1f;

        // Zamanlayıcılar ve Fazlar
        private float m_StateTimer;
        private float m_BobPhase;
        private float m_StepDistanceTravelled;
        private int m_IndexInQueue;

        // Görsel ve Dönüş
        private Transform m_VisualTransform;
        private Vector3 m_BaseScale = Vector3.one;
        private Quaternion m_BaseRotation = Quaternion.identity;
        private float m_CurrentBankAngle;
        private float m_CurrentHeadingYaw;
        private Vector3 m_LastMoveDir = Vector3.down;
        private ICargoRunner m_CargoRunner;

        // Boarding (Araç/Gemi Güvertesine Atlayış)
        private Vector3 m_BoardingStartPos;
        private Vector3 m_BoardingTargetPos;
        private Quaternion m_BoardingStartRot;
        private float m_BoardingDuration;
        private Action m_OnBoardedCallback;
        private Transform m_TargetVehicle;

        private void Awake()
        {
            m_VisualTransform = transform;
            m_BaseScale = transform.localScale;
            m_BaseRotation = transform.rotation;
            m_CargoRunner = GetComponent<ICargoRunner>();
        }

        /// <summary>
        /// Küpün hareket motorunu ve varyasyonlarını sırasına göre başlatır.
        /// </summary>
        public void Initialize(CubeMovementSettings settings, int indexInQueue, Vector3 baseScale)
        {
            m_Settings = settings != null ? settings : CubeMovementSettings.Default;
            m_IndexInQueue = indexInQueue;
            m_BaseScale = baseScale;
            transform.localScale = baseScale;
            m_BaseRotation = transform.rotation;

            // Kontrollü multi-cube varyasyonları
            float speedVar = UnityEngine.Random.Range(-m_Settings.SpeedVariation, m_Settings.SpeedVariation);
            m_SpeedMultiplier = 1f + speedVar;
            m_TargetCruiseSpeed = m_Settings.MoveSpeed * m_SpeedMultiplier;
            m_BobPhase = (indexInQueue % 2) * Mathf.PI + UnityEngine.Random.Range(0f, m_Settings.BobVariation * Mathf.PI);

            m_CargoRunner = GetComponent<ICargoRunner>();
            if (m_CargoRunner != null)
            {
                m_CargoRunner.BeginWalk(indexInQueue);
            }
        }

        /// <summary>
        /// Küpü verilen yol hattı boyunca hedefe doğru yönlendirir.
        /// </summary>
        public void StartFollowPath(ShoreLanePath path, float initialDistance, Action onArrived)
        {
            m_Path = path;
            m_CurrentDist = Mathf.Max(0f, initialDistance);
            m_TotalPathLength = path != null ? path.Length : 0f;
            m_StartPos = transform.position;
            m_OnArrivedCallback = onArrived;

            m_CurrentSpeed = 0f;
            m_StateTimer = 0f;
            m_StepDistanceTravelled = 0f;
            m_CurrentBankAngle = 0f;

            // 1. Aşama: Anticipation (Hafif kalkış hazırlığı)
            m_State = MovementState.Anticipation;
        }

        /// <summary>
        /// Küpü doğrudan hedef noktaya (araba/gemi/slot) organik kavisli yol oluşturarak hareket ettirir.
        /// </summary>
        public void MoveToTarget(Vector3 target, Action onArrived = null)
        {
            ShoreLanePath path = BuildPath(transform.position, target);
            FollowPath(path, onArrived);
        }

        /// <summary>
        /// İki nokta arasında hypercasual pürüzsüz kavisli yol inşa eder.
        /// </summary>
        public ShoreLanePath BuildPath(Vector3 start, Vector3 target)
        {
            Vector3 delta = target - start;
            Vector3 mid = (start + target) * 0.5f;
            float curvature = Settings.PathCurvature * 0.35f;
            Vector3 lateral = Vector3.Cross(delta.normalized, Vector3.forward) * curvature;
            Vector3 control = mid + lateral;
            return ShoreLanePath.BuildThrough(new[] { start, control, target }, Settings.PathSmoothing > 0 ? Settings.PathSmoothing : 16);
        }

        /// <summary>
        /// Belirtilen yolu baştan itibaren takip etmeye başlar.
        /// </summary>
        public void FollowPath(ShoreLanePath path, Action onArrived = null)
        {
            StartFollowPath(path, 0f, onArrived);
        }

        /// <summary>
        /// Dışarıdan veya Update'ten her karede hareket katmanını günceller.
        /// </summary>
        public void Tick(float dt, Vector3 destinationDynamicShift)
        {
            if (dt <= 0f) return;

            switch (m_State)
            {
                case MovementState.Anticipation:
                    UpdateAnticipation(dt);
                    break;

                case MovementState.Moving:
                case MovementState.Arriving:
                    UpdatePathFollowing(dt, destinationDynamicShift);
                    break;

                case MovementState.Settling:
                    UpdateSettling(dt);
                    break;

                case MovementState.Boarding:
                    UpdateBoarding(dt);
                    break;
            }
        }

        // =========================================================================
        // 1. ANTICIPATION: Harekete başlamadan önce canlı, organik hazırlık
        // =========================================================================
        private void UpdateAnticipation(float dt)
        {
            m_StateTimer += dt;
            float dur = Mathf.Max(0.04f, Settings.AnticipationDuration);
            float t = Mathf.Clamp01(m_StateTimer / dur);

            // Yaylanma: Hafifçe aşağı basılma (compression) ve geriye toplanma
            float recoilFactor = Mathf.Sin(t * Mathf.PI);
            float squash = recoilFactor * Settings.SquashAmount;

            transform.localScale = new Vector3(
                m_BaseScale.x * (1f + squash * 0.4f),
                m_BaseScale.y * (1f - squash * 0.6f),
                m_BaseScale.z * (1f + squash * 0.4f)
            );

            // Başlangıç konumundan çok hafif geriye esneme
            Vector3 pathTangent = m_Path != null ? m_Path.TangentAtDistance(m_CurrentDist) : -transform.forward;
            Vector3 recoilOffset = -pathTangent.normalized * (recoilFactor * Settings.AnticipationRecoil);
            transform.position = m_StartPos + recoilOffset;

            if (m_StateTimer >= dur)
            {
                // Anticipation tamamlandı -> Hızlanarak harekete geç
                transform.localScale = m_BaseScale;
                m_State = MovementState.Moving;
                m_StateTimer = 0f;
            }
        }

        // =========================================================================
        // 2. PATH FOLLOWING: Hızlanma, viraj yatması (Bank Tilt), adım sekmesi (Bob)
        // =========================================================================
        private void UpdatePathFollowing(float dt, Vector3 destinationDynamicShift)
        {
            if (m_Path == null) return;

            float remainingDist = Mathf.Max(0f, m_TotalPathLength - m_CurrentDist);

            // Hız Profili (Acceleration & Deceleration):
            if (remainingDist <= Settings.ArrivalDistance)
            {
                // Yavaşlama bölgesi (Deceleration):
                m_State = MovementState.Arriving;
                float arrivalRatio = Mathf.Clamp01(remainingDist / Settings.ArrivalDistance);
                float easeRatio = Mathf.Pow(arrivalRatio, Settings.ArrivalEase);
                float desiredSpeed = Mathf.Lerp(m_TargetCruiseSpeed * Settings.ArrivalSlowdown, m_TargetCruiseSpeed, easeRatio);
                m_CurrentSpeed = Mathf.MoveTowards(m_CurrentSpeed, desiredSpeed, Settings.Deceleration * dt);
            }
            else
            {
                // Hızlanma ve seyir (Acceleration & Cruise):
                m_CurrentSpeed = Mathf.MoveTowards(m_CurrentSpeed, m_TargetCruiseSpeed, Settings.Acceleration * dt);
            }

            // Mesafe ilerlemesi:
            float moveDelta = m_CurrentSpeed * dt;
            m_CurrentDist += moveDelta;
            m_StepDistanceTravelled += moveDelta;

            // Yol üzerindeki temel pozisyon:
            Vector3 rawPos = m_Path.PointAtDistance(m_CurrentDist);

            // Dinamik hedef kayması (gemi slot kayması vb. için yumuşak ağırlık):
            float shiftWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(m_CurrentDist / m_TotalPathLength));
            Vector3 trackPos = rawPos + destinationDynamicShift * shiftWeight;

            // Teğet ve İleri Yön:
            Vector3 pathTangent = m_Path.TangentAtDistance(m_CurrentDist);
            if (pathTangent.sqrMagnitude > 1e-6f)
            {
                m_LastMoveDir = pathTangent.normalized;
            }

            // Dönüş Açısı ve Bank Tilt (Viraja Yatma):
            // İlerideki noktanın teğetiyle mevcut yön arasındaki açı farkından dönüş oranı hesaplanır
            Vector3 futureTangent = m_Path.TangentAtDistance(m_CurrentDist + 0.18f);
            float turnCrossY = Vector3.Cross(m_LastMoveDir, futureTangent.normalized).z;
            float targetTilt = Mathf.Clamp(turnCrossY * Settings.TiltAmount * 12f, -Settings.TiltAmount, Settings.TiltAmount);
            m_CurrentBankAngle = Mathf.Lerp(m_CurrentBankAngle, targetTilt, dt / Mathf.Max(0.01f, Settings.TiltSmoothness));

            // İkincil Hareket (Secondary Motion - Bobbing & Squash):
            Camera cam = ShipController.MainCamera;
            Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;

            float bobSin = Mathf.Sin(m_StepDistanceTravelled * Settings.BobSpeed + m_BobPhase);
            // Yavaşlarken sekme genliği de tatlıca söner
            float speedRatio = Mathf.Clamp01(m_CurrentSpeed / m_TargetCruiseSpeed);
            float currentBob = bobSin * Settings.BobAmount * speedRatio;

            // Hafif adım squash-stretch'i (karakter hissiyatı)
            float stepSquash = bobSin * (Settings.SquashAmount * 0.35f * speedRatio);
            transform.localScale = new Vector3(
                m_BaseScale.x * (1f + stepSquash * 0.3f),
                m_BaseScale.y * (1f - stepSquash * 0.5f),
                m_BaseScale.z * (1f + stepSquash * 0.3f)
            );

            // Pozisyonu uygula:
            transform.position = trackPos + camUp * currentBob;

            // Rotasyonu uygula (Yönelme + Viraj Yatma):
            if (m_CargoRunner != null)
            {
                m_CargoRunner.TurnToward(m_LastMoveDir, dt);
            }
            else
            {
                float targetYaw = CargoRunnerHeading.TargetYaw(m_LastMoveDir, 35f);
                m_CurrentHeadingYaw = Mathf.Lerp(m_CurrentHeadingYaw, targetYaw, dt / Mathf.Max(0.01f, Settings.RotationSmoothness));
                Quaternion headingRot = Quaternion.AngleAxis(m_CurrentHeadingYaw, Vector3.up) * m_BaseRotation;
                Quaternion bankRot = Quaternion.AngleAxis(m_CurrentBankAngle, Vector3.forward);
                transform.rotation = bankRot * headingRot;
            }

            // Yolun sonuna varış kontrolü:
            if (m_CurrentDist >= m_TotalPathLength)
            {
                m_State = MovementState.Settling;
                m_StateTimer = 0f;
                m_Destination = trackPos;
            }
        }

        // =========================================================================
        // 3. SETTLING: Hedefe vardığında sönümlü yaylanarak durma (Settle Bounce)
        // =========================================================================
        private void UpdateSettling(float dt)
        {
            m_StateTimer += dt;
            float dur = Mathf.Max(0.05f, Settings.SettleDuration);
            float t = Mathf.Clamp01(m_StateTimer / dur);

            // Sönümlü sinüs yaylanması (Damped spring bounce):
            float decay = 1f - t;
            float bounce = Mathf.Sin(t * Mathf.PI * 2f) * (Settings.SettleBounce * decay);

            Camera cam = ShipController.MainCamera;
            Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;
            transform.position = m_Destination + camUp * bounce;

            // Gövde yatmasını sıfıra döndür ve scale'i normale oturt
            m_CurrentBankAngle = Mathf.Lerp(m_CurrentBankAngle, 0f, t);
            transform.localScale = Vector3.Lerp(transform.localScale, m_BaseScale, t);

            if (m_StateTimer >= dur)
            {
                transform.position = m_Destination;
                transform.localScale = m_BaseScale;
                m_State = MovementState.Completed;
                m_OnArrivedCallback?.Invoke();
            }
        }

        // =========================================================================
        // 4. BOARDING: Gemi veya Araba Güvertesine Pürüzsüz Atlayış & Yerleşim
        // =========================================================================
        /// <summary>
        /// Küpün sahil/yol ucundan araç güvertesine organik, parabolik yaylanma ile atlamasını sağlar.
        /// </summary>
        public void StartBoarding(Transform targetVehicle, Vector3 deckTargetOffset, float duration, Action onBoarded)
        {
            m_TargetVehicle = targetVehicle;
            m_BoardingStartPos = transform.position;
            m_BoardingStartRot = transform.rotation;
            m_BoardingDuration = Mathf.Max(0.12f, duration);
            m_OnBoardedCallback = onBoarded;

            m_State = MovementState.Boarding;
            m_StateTimer = 0f;

            WaddleRunner waddle = GetComponent<WaddleRunner>();
            if (waddle != null) waddle.IsAirborne = true;
        }

        private void UpdateBoarding(float dt)
        {
            m_StateTimer += dt;
            float t = Mathf.Clamp01(m_StateTimer / m_BoardingDuration);

            // Hedef güverte noktası (araç hareket ediyorsa dinamik takip eder):
            Vector3 currentTarget = m_TargetVehicle != null
                ? m_TargetVehicle.position + new Vector3(0f, 0.16f, 0.02f)
                : m_BoardingStartPos;

            // Parabolik Yay & İlerleme:
            float hT = Mathf.SmoothStep(0f, 1f, t);
            Vector3 p = Vector3.Lerp(m_BoardingStartPos, currentTarget, hT);

            // Zirvede tepe yapan parabol yay:
            Camera cam = ShipController.MainCamera;
            Vector3 arcUp = cam != null ? cam.transform.up : Vector3.up;
            float arc = 4f * t * (1f - t);
            float hopHeight = 0.28f;
            p += arcUp * (arc * hopHeight);

            transform.position = p;

            // Havada uçuş eğimi (pitch) ve hedefe yönelme:
            float pitchAngle = Mathf.Sin(t * Mathf.PI) * 16f;
            transform.rotation = m_BoardingStartRot * Quaternion.Euler(pitchAngle, 0f, 0f);

            // Squash & Stretch:
            if (t < 0.25f)
            {
                // Havaya fırlama: Boyuna uzama (stretch)
                float stretch = Mathf.Sin(t / 0.25f * Mathf.PI * 0.5f) * Settings.SquashAmount;
                transform.localScale = new Vector3(m_BaseScale.x * (1f - stretch * 0.5f), m_BaseScale.y * (1f + stretch), m_BaseScale.z * (1f - stretch * 0.5f));
            }
            else if (t > 0.78f)
            {
                // Güverteye iniş: Basılma ve yaylanma (landing squash & settle)
                float landT = (t - 0.78f) / 0.22f;
                float squash = Mathf.Sin(landT * Mathf.PI) * (Settings.SquashAmount * 1.2f);
                transform.localScale = new Vector3(m_BaseScale.x * (1f + squash * 0.5f), m_BaseScale.y * (1f - squash), m_BaseScale.z * (1f + squash * 0.5f));
            }
            else
            {
                transform.localScale = m_BaseScale;
            }

            if (m_StateTimer >= m_BoardingDuration)
            {
                transform.localScale = m_BaseScale;
                m_State = MovementState.Completed;
                m_OnBoardedCallback?.Invoke();
            }
        }

        // =========================================================================
        // 5. İP (ROPE) MODU: Konumu ShipDispatcher'ın ip çözücüsü verir, bu katman
        //    üstüne karakter hissini ekler (anticipation, kalkış, yön yumuşatma,
        //    viraja yatma, hızlanma esnemesi, bob). Kendi Update'i yoktur; GC üretmez.
        // =========================================================================
        private WaddleRunner m_Waddle;
        private Vector3 m_RopeLastBase;
        private Vector3 m_RopeDir = Vector3.down;
        private Vector3 m_RopeRecoilDir = Vector3.down;
        private float m_RopeLastSpeed;
        private float m_RopeStretch;
        private float m_RopeLift;
        private float m_RopeLiftHeight;
        private float m_RopeCruise = 1f;
        private float m_RopeBobDist;
        private float m_BobMul = 1f;
        private float m_TiltMul = 1f;

        /// <summary>Küpün o anki yumuşatılmış hareket yönü (ekran düzleminde).</summary>
        public Vector3 MoveDirection => m_RopeDir;
        /// <summary>Viraja yatma açısı (derece).</summary>
        public float BankAngle => m_CurrentBankAngle;
        /// <summary>Hareket başladığı andaki ölçek (squash bitince dönülen ölçek).</summary>
        public Vector3 BaseScale => m_BaseScale;

        /// <summary>
        /// İp modunu başlatır. Küp, ip onu çekene kadar (gerilim gelene kadar) yerinde bekler;
        /// ilk çekildiği karede kısa bir anticipation yapıp hareketlenir.
        /// </summary>
        public void BeginRopeMotion(CubeMovementSettings settings, int indexInQueue, float cruiseSpeed, float liftHeight)
        {
            m_Settings = settings != null ? settings : CubeMovementSettings.Default;
            m_IndexInQueue = indexInQueue;
            m_BaseScale = transform.localScale;
            m_BaseRotation = transform.rotation;
            m_CargoRunner = GetComponent<ICargoRunner>();
            m_Waddle = GetComponent<WaddleRunner>();

            m_RopeLastBase = transform.position;
            m_RopeDir = Vector3.down;
            m_RopeRecoilDir = Vector3.down;
            m_RopeLastSpeed = 0f;
            m_RopeStretch = 0f;
            m_RopeLift = 0f;
            m_RopeLiftHeight = liftHeight;
            m_RopeCruise = Mathf.Max(0.1f, cruiseSpeed);
            m_RopeBobDist = 0f;
            m_CurrentBankAngle = 0f;
            m_CurrentHeadingYaw = 0f;
            m_StateTimer = 0f;

            CubeMovementSettings s = Settings;
            m_BobPhase = (indexInQueue % 2) * Mathf.PI + UnityEngine.Random.Range(0f, s.BobVariation * Mathf.PI);
            m_BobMul = 1f + UnityEngine.Random.Range(-s.BobVariation, s.BobVariation) * 0.3f;
            m_TiltMul = 1f + UnityEngine.Random.Range(-0.15f, 0.15f);

            if (m_CargoRunner != null) m_CargoRunner.BeginWalk(indexInQueue);
            m_State = MovementState.Idle;
        }

        /// <summary>
        /// Bir karelik ip konumunu uygular. <paramref name="basePos"/> küpün yol/ip üzerindeki
        /// çıplak konumudur; <paramref name="landFade"/> 1 iken küp tam kalkık, 0 iken zemine inmiştir.
        /// </summary>
        public void ApplyRopeFrame(Vector3 basePos, float landFade, Vector3 popUp, Vector3 camUp, float dt)
        {
            if (dt <= 0f) return;
            // NaN/sonsuz konum transform'a asla yazılmasın (render sıralaması assert'i)
            if (!float.IsFinite(basePos.x) || !float.IsFinite(basePos.y) || !float.IsFinite(basePos.z)) return;
            CubeMovementSettings s = Settings;

            Vector3 delta = basePos - m_RopeLastBase;
            m_RopeLastBase = basePos;
            float moved = delta.magnitude;
            float speed = moved / dt;

            // İp henüz bu küpü çekmedi: yerinde dur, hiçbir şey yazma (ucuz)
            if (m_State == MovementState.Idle)
            {
                if (moved < 1e-5f) return;
                m_State = MovementState.Anticipation;
                m_StateTimer = 0f;
                m_RopeDir = delta / moved;
                m_RopeRecoilDir = m_RopeDir;
            }

            // --- Yön yumuşatma + dönüş hızına göre viraja yatma (turning anticipation & tilt) ---
            if (moved > 1e-5f)
            {
                Vector3 dir = delta / moved;
                Vector3 prevDir = m_RopeDir;
                float follow = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, s.RotationSmoothness));
                m_RopeDir = Vector3.Slerp(m_RopeDir, dir, follow).normalized;

                // Ekran düzleminde işaretli dönüş hızı (derece/sn). Saat yönü (sağa dönüş) = eksi.
                float turnRate = Vector3.SignedAngle(prevDir, m_RopeDir, Vector3.forward) / dt;
                // ~120°/sn dönüşte tam yatma
                float targetTilt = Mathf.Clamp(turnRate / 120f, -1f, 1f) * s.TiltAmount * m_TiltMul;
                float tiltFollow = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, s.TiltSmoothness));
                m_CurrentBankAngle = Mathf.Lerp(m_CurrentBankAngle, targetTilt, tiltFollow);
            }
            else
            {
                // Dururken yatma söner
                m_CurrentBankAngle = Mathf.Lerp(m_CurrentBankAngle, 0f, 1f - Mathf.Exp(-dt * 8f));
            }

            // --- Hızlanırken hafif uzama, yavaşlarken hafif basılma (minimal squash) ---
            float accel = (speed - m_RopeLastSpeed) / dt;
            m_RopeLastSpeed = speed;
            float accelNorm = Mathf.Clamp(accel / Mathf.Max(0.1f, s.Acceleration * m_RopeCruise / Mathf.Max(0.1f, s.MoveSpeed)), -1f, 1f);
            float targetStretch = accelNorm * s.SquashAmount * 0.25f;
            m_RopeStretch = Mathf.Lerp(m_RopeStretch, targetStretch, 1f - Mathf.Exp(-dt * s.SquashSpeed));

            // --- Anticipation: kısa basılma + hareketin tersine minik geri esneme ---
            float antSquash = 0f;
            Vector3 recoil = Vector3.zero;
            if (m_State == MovementState.Anticipation)
            {
                m_StateTimer += dt;
                float t = Mathf.Clamp01(m_StateTimer / Mathf.Max(0.02f, s.AnticipationDuration));
                float a = Mathf.Sin(t * Mathf.PI);
                antSquash = a * s.SquashAmount * 0.5f;
                recoil = -m_RopeRecoilDir * (a * s.AnticipationRecoil);
                if (t >= 1f) m_State = MovementState.Moving;
            }

            // --- Karttan kalkış (hover) ---
            m_RopeLift = Mathf.MoveTowards(m_RopeLift, 1f, dt / 0.3f);
            float liftEase = Mathf.SmoothStep(0f, 1f, m_RopeLift);

            // --- Bob: katedilen yola bağlı, hız düştükçe söner ---
            m_RopeBobDist += moved;
            float speedRatio = Mathf.Clamp01(speed / m_RopeCruise);
            float bob = Mathf.Sin(m_RopeBobDist * s.BobSpeed + m_BobPhase) * s.BobAmount * m_BobMul * speedRatio;

            transform.position = basePos + recoil
                                 + popUp * (m_RopeLiftHeight * liftEase * landFade)
                                 + camUp * bob;

            // Gölge zeminde kalsın: gövdenin yol noktasından görsel kayması bildirilir
            if (m_Waddle != null) m_Waddle.GroundAnchorOffset = transform.position - basePos;

            float sy = m_RopeStretch - antSquash;
            transform.localScale = new Vector3(
                m_BaseScale.x * (1f - sy * 0.5f),
                m_BaseScale.y * (1f + sy),
                m_BaseScale.z * (1f - sy * 0.5f));

            ApplyHeading(dt);
        }

        /// <summary>
        /// Kumsal zemin düzleminin dünya z'sini verir; zemin gölgesi bu düzlemde çizilir.
        /// NaN verilirse gölge eski davranışla küpün yanında durur.
        /// </summary>
        public void SetGroundPlaneZ(float groundPlaneZ)
        {
            if (m_Waddle == null) m_Waddle = GetComponent<WaddleRunner>();
            if (m_Waddle != null)
            {
                m_Waddle.GroundPlaneZ = groundPlaneZ;
                m_Waddle.GroundAnchorOffset = Vector3.zero;
            }
        }

        /// <summary>Gövdeyi hareket yönüne kademeli çevirir ve viraja yatırır.</summary>
        public void ApplyHeading(float dt)
        {
            if (m_Waddle != null)
            {
                m_Waddle.BankTilt = m_CurrentBankAngle;
                m_Waddle.TurnToward(m_RopeDir, dt);
            }
            else if (m_CargoRunner != null)
            {
                m_CargoRunner.TurnToward(m_RopeDir, dt);
            }
            else
            {
                float targetYaw = CargoRunnerHeading.TargetYaw(m_RopeDir, 35f);
                m_CurrentHeadingYaw = Mathf.MoveTowardsAngle(m_CurrentHeadingYaw, targetYaw, Settings.TurnSpeed * dt);
                transform.rotation = CargoRunnerHeading.Apply(m_BaseRotation, m_CurrentHeadingYaw, m_CurrentBankAngle);
            }
        }

        /// <summary>Binme sırasında yön ve yatmayı dışarıdan sürmek için.</summary>
        public void SteerToward(Vector3 dir, float bankTarget, float dt)
        {
            if (dir.sqrMagnitude > 1e-8f)
            {
                float follow = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, Settings.RotationSmoothness));
                m_RopeDir = Vector3.Slerp(m_RopeDir, dir.normalized, follow).normalized;
            }
            m_CurrentBankAngle = Mathf.Lerp(m_CurrentBankAngle, bankTarget, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, Settings.TiltSmoothness)));
            ApplyHeading(dt);
        }
    }
}
