using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Denizci koşucunun görseli: modeli kumsal düzleminde (zemin XY, yukarı = kameraya doğru -Z) dik tutar ve
    /// yürüdüğü yöne çevirir. Animasyon akışı Animator Controller'dadır (Sailor_Runner.controller); bu script
    /// yalnızca parametreleri ayarlar:
    ///   HopDown (trigger)  : gemiden kumsala iniş
    ///   Pickup  (trigger)  : küpü alma
    ///   HopBoard (trigger) : gemiye binip küpü bırakma
    ///   Carrying (bool)    : elinde küp var mı
    ///   MoveSpeed (float)  : yürüme durumlarının hız çarpanı (ayak kaymasın diye yürüme hızına göre)
    /// Hangi klibin oynayacağı, geçiş süreleri ve sırası Animator'dan değiştirilir.
    /// </summary>
    public class SailorRunnerVisual : MonoBehaviour
    {
        public const string StateIdle = "Idle";
        public const string StateHopDown = "Hop_Down";
        public const string StateRunEmpty = "Run_Empty";
        public const string StatePickup = "Pickup";
        public const string StateRunCarry = "Run_Carry";
        public const string StateHopBoard = "Hop_Board_Drop";

        [SerializeField] private Transform m_ModelPivot;
        [SerializeField] private Animator m_Animator;
        [SerializeField] private Transform m_CargoSocket;
        [Tooltip("Dönüş yumuşaklığı: büyüdükçe daha hızlı döner.")]
        [SerializeField] private float m_TurnRate = 10f;
        [Tooltip("Yürüme durumundaki klibin 1x hızda karşıladığı hız (model birimi/sn). Animator'da yürüme klibini " +
                 "koşu klibiyle değiştirirsen bunu da ~2.5 yap. Ayaklar kayıyorsa düşür.")]
        [SerializeField] private float m_ClipReferenceSpeed = 1.25f;
        [Tooltip("MoveSpeed parametresinin alt/üst sınırı.")]
        [SerializeField] private Vector2 m_MoveSpeedRange = new Vector2(0.6f, 3.5f);

        [Header("📐 Kamera Açısı Eğim Düzeltmesi (Tilt)")]
        [Tooltip("Karakterin tepeden bakıldığında sadece şapka gibi görünmesini engelleyen, yüzünü, gözlerini ve göğsünü kameraya gösteren eğim açısı (derece).")]
        [SerializeField, Range(0f, 40f)] private float m_TiltTowardsCamera = 22f;

        // Animator parametre adları (Sailor_Runner.controller ile aynı olmalı)
        public const string ParamHopDown = "HopDown";
        public const string ParamPickup = "Pickup";
        public const string ParamHopBoard = "HopBoard";
        public const string ParamCarrying = "Carrying";
        public const string ParamMoveSpeed = "MoveSpeed";
        private static readonly int s_HopDown = Animator.StringToHash(ParamHopDown);
        private static readonly int s_Pickup = Animator.StringToHash(ParamPickup);
        private static readonly int s_HopBoard = Animator.StringToHash(ParamHopBoard);
        private static readonly int s_Carrying = Animator.StringToHash(ParamCarrying);
        private static readonly int s_MoveSpeed = Animator.StringToHash(ParamMoveSpeed);

        private Vector3 m_LastPos;
        private Vector3 m_Facing = Vector3.up;
        private Vector3 m_SmoothVel;
        private float m_SmoothSpeed;

        public Transform CargoSocket => m_CargoSocket;

        private Transform m_Cargo;

        /// <summary>
        /// Taşınan küpü bağlar. Küp sokete çocuk yapılmaz: animasyondaki squash/stretch ölçeği küpü
        /// esnetmesin diye ModelPivot altında durur, her kare soketin konumunu takip eder.
        /// </summary>
        public void AttachCargo(Transform cargo, float sizeInModelUnits, float meshEdge)
        {
            m_Cargo = cargo;
            if (cargo == null) return;
            Transform parent = m_ModelPivot != null ? m_ModelPivot : transform;
            cargo.SetParent(parent, false);
            cargo.localRotation = Quaternion.identity;
            cargo.localScale = Vector3.one * (sizeInModelUnits / Mathf.Max(1e-4f, meshEdge));
            FollowSocket();
        }

        [Header("Takım halkası (ayak altı)")]
        [Tooltip("Ayakların altındaki takım rengi halkasının çapı (model birimi; 1 = panodaki küp).")]
        [SerializeField] private float m_RingDiameter = 1.05f;
        [SerializeField, Range(0f, 1f)] private float m_RingAlpha = 0.6f;

        private SpriteRenderer m_Ring;
        private float m_GroundZ = float.NaN;
        private static Sprite s_RingSprite;

        /// <summary>Ayakların altına zemine yatık, takım renginde yumuşak bir halka koyar.</summary>
        public void SetupTeamRing(Color team, float groundZ)
        {
            m_GroundZ = groundZ;
            if (m_Ring == null)
            {
                var go = new GameObject("TeamRing");
                go.transform.SetParent(transform, false);
                m_Ring = go.AddComponent<SpriteRenderer>();
                m_Ring.sprite = GetRingSprite();
                m_Ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m_Ring.receiveShadows = false;
            }
            team.a = m_RingAlpha;
            m_Ring.color = team;
            FollowRing();
        }

        /// <summary>Gemiden inerken/binerken halka gizlenir (suyun üstünde görünmesin).</summary>
        public void SetRingVisible(bool visible)
        {
            if (m_Ring != null) m_Ring.enabled = visible;
        }

        private void FollowRing()
        {
            if (m_Ring == null) return;
            float unit = m_ModelPivot != null ? m_ModelPivot.lossyScale.x : transform.lossyScale.x;
            Vector3 p = transform.position;
            // Zemin düzleminin hemen önünde (kameraya doğru -Z), dönmeden, XY'de yatık
            if (float.IsFinite(m_GroundZ)) p.z = m_GroundZ - unit * 0.02f;
            m_Ring.transform.position = p;
            m_Ring.transform.rotation = Quaternion.identity;
            float size = m_RingDiameter * unit;
            Vector3 ps = transform.lossyScale;
            m_Ring.transform.localScale = new Vector3(size / Mathf.Max(1e-4f, ps.x), size / Mathf.Max(1e-4f, ps.y), 1f);
        }

        private static Sprite GetRingSprite()
        {
            if (s_RingSprite != null) return s_RingSprite;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // Dolgu hafif, kenar belirgin, dışa doğru yumuşak sönen halka
                    float fill = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.55f, 1f, r)) * 0.45f;
                    float rim = Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.12f, 2f));
                    float a = Mathf.Clamp01(Mathf.Max(fill, rim)) * (r < 1f ? 1f : 0f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_RingSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return s_RingSprite;
        }

        private void FollowSocket()
        {
            if (m_Cargo == null || m_CargoSocket == null) return;
            m_Cargo.position = m_CargoSocket.position;
            if (m_ModelPivot != null) m_Cargo.rotation = m_ModelPivot.rotation;
        }

        private void Awake()
        {
            if (m_ModelPivot == null) m_ModelPivot = transform.Find("ModelPivot");
            if (m_Animator == null) m_Animator = GetComponentInChildren<Animator>();
            if (m_CargoSocket == null) m_CargoSocket = FindDeep(transform, "CargoSocket");
            m_LastPos = transform.position;
            ApplyFacing(1f);
        }

        /// <summary>
        /// Görev aşamasını Animator'a bildirir (tetikleyici/parametre). Geçişin kendisi ve süresi Animator'dadır;
        /// yürüme durumlarına (StateRunEmpty/StateRunCarry) Animator'daki geçişlerle kendiliğinden geçilir.
        /// </summary>
        public void Play(string state, float fade = 0.08f)
        {
            if (m_Animator == null || m_Animator.runtimeAnimatorController == null) return;
            if (state == StateHopDown)
            {
                m_Animator.ResetTrigger(s_Pickup); m_Animator.ResetTrigger(s_HopBoard);
                m_Animator.SetBool(s_Carrying, false);
                m_Animator.SetTrigger(s_HopDown);
            }
            else if (state == StatePickup)
            {
                m_Animator.SetBool(s_Carrying, true);
                m_Animator.SetTrigger(s_Pickup);
            }
            else if (state == StateHopBoard)
            {
                m_Animator.SetTrigger(s_HopBoard);
            }
            else if (state == StateRunEmpty) m_Animator.SetBool(s_Carrying, false);
            else if (state == StateRunCarry) m_Animator.SetBool(s_Carrying, true);
        }

        /// <summary>Eski çağrılarla uyum: sonraki durum Animator'daki geçişle kendiliğinden gelir.</summary>
        public void PlayThen(string state, string next) => Play(state);

        private bool m_HasFacingOverride;
        private Vector3 m_FacingOverride;

        /// <summary>Yürüme yönünden bağımsız olarak bu yöne dön (ör. küpü alırken dönüş yoluna).</summary>
        public void SetFacingOverride(Vector3 dir)
        {
            dir.z = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            m_FacingOverride = dir.normalized;
            m_HasFacingOverride = true;
        }

        /// <summary>
        /// Yönü anında hedefe kilitler ve yumuşatma gecikmesini sıfırlar (ör. orta küp alma anında dönme/titreme olmasın).
        /// </summary>
        public void SnapFacing(Vector3 dir)
        {
            dir.z = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            m_Facing = dir.normalized;
            m_FacingOverride = m_Facing;
            m_HasFacingOverride = true;
            m_SmoothVel = Vector3.zero;
            ApplyFacing(1f);
        }

        public void ClearFacingOverride()
        {
            m_HasFacingOverride = false;
            m_SmoothVel = Vector3.zero;
        }

        /// <summary>Controller'daki klibin süresi (ör. küpü alırken ne kadar duracağı için "Pickup").</summary>
        public float GetClipLength(string clipName)
        {
            if (m_Animator == null || m_Animator.runtimeAnimatorController == null) return 0.3f;
            foreach (var clip in m_Animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == clipName) return clip.length;
            return 0.3f;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 delta = transform.position - m_LastPos;
            m_LastPos = transform.position;
            delta.z = 0f;

            // Kare süresi dalgalansa da yön ve hız titremesin: hızı yumuşat
            Vector3 vel = delta / dt;
            m_SmoothVel = Vector3.Lerp(m_SmoothVel, vel, 1f - Mathf.Exp(-dt / 0.08f));
            float sv = m_SmoothVel.magnitude;
            float unit = m_ModelPivot != null ? Mathf.Max(1e-4f, m_ModelPivot.lossyScale.y) : 1f;
            if (m_HasFacingOverride)
            {
                m_Facing = m_FacingOverride;
                m_SmoothVel = Vector3.zero;
            }
            // Çok yavaşken (durma/kalkış anı) yön güncellenmez: sıfıra yakın hızda yön titriyordu
            else if (sv > 0.3f * unit)
            {
                m_Facing = m_SmoothVel / sv;
            }
            ApplyFacing(m_HasFacingOverride ? 1f : (1f - Mathf.Exp(-dt * m_TurnRate)));
            FollowSocket();
            FollowRing();

            // Yürüme durumlarında ayak kaymasın: MoveSpeed = yumuşatılmış yürüme hızı / klibin referans hızı
            m_SmoothSpeed = Mathf.Lerp(m_SmoothSpeed, vel.magnitude / unit, 1f - Mathf.Exp(-dt / 0.2f));
            if (m_Animator != null && m_Animator.runtimeAnimatorController != null)
            {
                float moveSpeed = Mathf.Clamp(m_SmoothSpeed / Mathf.Max(0.1f, m_ClipReferenceSpeed), m_MoveSpeedRange.x, m_MoveSpeedRange.y);
                m_Animator.SetFloat(s_MoveSpeed, moveSpeed);
            }
        }

        private void ApplyFacing(float t)
        {
            if (m_ModelPivot == null) return;
            // Model: yukarı +Y, yüz +Z. Dünyada yukarı kameraya doğru (-Z), yüz yürüme yönünde (XY düzlemi).
            Quaternion target = Quaternion.LookRotation(m_Facing, Vector3.back);
            if (m_TiltTowardsCamera > 0.01f)
            {
                // Modeli hafifçe geriye/kameraya doğru yatırarak başın arkaya kaymasını ve yüzün/göğsün görünmesini sağla
                target = target * Quaternion.Euler(-m_TiltTowardsCamera, 0f, 0f);
            }
            m_ModelPivot.rotation = t >= 1f ? target : Quaternion.Slerp(m_ModelPivot.rotation, target, t);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform ch in root)
            {
                var f = FindDeep(ch, name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
