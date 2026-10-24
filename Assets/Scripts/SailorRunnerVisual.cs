using System.Collections.Generic;
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
        [Tooltip("Koşu yerine yürüme kliplerini (hızlandırarak) kullan; daha az zıplar.")]
        [SerializeField] private bool m_UseWalkClips = true;
        [Tooltip("Yürüme durumundaki klibin 1x hızda karşıladığı hız (model birimi/sn). Animator'da yürüme klibini " +
                 "koşu klibiyle değiştirirsen bunu da ~2.5 yap. Ayaklar kayıyorsa düşür.")]
        [SerializeField] private float m_ClipReferenceSpeed = 1.25f;
        [Tooltip("Yürüme klibinin 1x hızda karşıladığı hız (model birimi/sn).")]
        [SerializeField] private float m_WalkClipReferenceSpeed = 1.25f;
        [Tooltip("Koşu klibinin 1x hızda karşıladığı hız (model birimi/sn).")]
        [SerializeField] private float m_RunClipReferenceSpeed = 2.5f;
        [Tooltip("MoveSpeed parametresinin alt/üst sınırı.")]
        [SerializeField] private Vector2 m_MoveSpeedRange = new Vector2(0.6f, 3.5f);
        [SerializeField] private Vector2 m_AnimatorSpeedRange = new Vector2(0.6f, 3.5f);

        [Header("📐 Kamera Açısı Eğim Düzeltmesi (Tilt)")]
        [Tooltip("Karakterin tepeden bakıldığında sadece şapka gibi görünmesini engelleyen, yüzünü, gözlerini ve göğsünü kameraya gösteren eğim açısı (derece).")]
        [SerializeField, Range(0f, 60f)] private float m_TiltTowardsCamera = 36f;

        public float TiltTowardsCamera
        {
            get => m_TiltTowardsCamera;
            set
            {
                m_TiltTowardsCamera = value;
                ApplyFacing(1f);
            }
        }

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

        // Animator Controller'da gerçekten olan parametreler. Sailor_Runner.controller'da hiç parametre yok (klipler
        // CrossFade ile oynatılıyor); olmayan parametreye her karede SetFloat/SetBool yapmak Unity'nin her seferinde
        // "Parameter does not exist" uyarısı basmasına ve kare düşmesine yol açıyordu.
        private RuntimeAnimatorController m_ParamsController;
        private readonly HashSet<int> m_AnimatorParams = new HashSet<int>();

        private bool HasParam(int hash)
        {
            if (m_Animator == null) return false;
            if (m_ParamsController != m_Animator.runtimeAnimatorController)
            {
                m_ParamsController = m_Animator.runtimeAnimatorController;
                m_AnimatorParams.Clear();
                foreach (var p in m_Animator.parameters) m_AnimatorParams.Add(p.nameHash);
            }
            return m_AnimatorParams.Contains(hash);
        }

        private void SetTriggerSafe(int hash) { if (HasParam(hash)) m_Animator.SetTrigger(hash); }
        private void ResetTriggerSafe(int hash) { if (HasParam(hash)) m_Animator.ResetTrigger(hash); }
        private void SetBoolSafe(int hash, bool value) { if (HasParam(hash)) m_Animator.SetBool(hash, value); }

        private Vector3 m_LastPos;
        private Vector3 m_Facing = Vector3.up;
        private Vector3 m_SmoothVel;
        private float m_SmoothSpeed;
        private string m_State;
        private string m_QueuedState;
        private float m_QueuedAt = -1f;

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
        [Tooltip("Ayakların altındaki renkli takım halkası (kullanıcı isteğiyle varsayılan olarak kapalıdır).")]
        [SerializeField] private bool m_EnableTeamRing = false;
        [Tooltip("Ayakların altındaki takım rengi halkasının çapı (model birimi; 1 = panodaki küp).")]
        [SerializeField] private float m_RingDiameter = 1.05f;
        [SerializeField, Range(0f, 1f)] private float m_RingAlpha = 0.6f;

        private SpriteRenderer m_Ring;
        private float m_GroundZ = float.NaN;
        private static Sprite s_RingSprite;

        /// <summary>Ayakların altına zemine yatık, takım renginde yumuşak bir halka koyar.</summary>
        public void SetupTeamRing(Color team, float groundZ)
        {
            if (!m_EnableTeamRing)
            {
                if (m_Ring != null)
                {
                    m_Ring.enabled = false;
                    if (m_Ring.gameObject != null) Destroy(m_Ring.gameObject);
                    m_Ring = null;
                }
                return;
            }

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
            if (m_Ring != null) m_Ring.enabled = visible && m_EnableTeamRing;
            SetShadowVisible(visible);
        }

        private void FollowRing()
        {
            if (m_Ring == null || !m_EnableTeamRing) return;
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

        [Header("🌑 Gölge (Ayak altı fake shadow)")]
        [Tooltip("Gemicilerin altında belirgin, yumuşak fake gölge.")]
        [SerializeField] private bool m_EnableFakeShadow = true;
        [Tooltip("Gölgenin genişliği (model birimi; panodaki küp boyutu ile orantılı).")]
        [SerializeField] private float m_ShadowWidth = 1.35f;
        [Tooltip("Gölgenin yüksekliği/derinliği (model birimi).")]
        [SerializeField] private float m_ShadowHeight = 0.72f;
        [Tooltip("Gölgenin dikey (Y) ofseti: ayakların tam altına oturması için.")]
        [SerializeField] private float m_ShadowOffsetY = -0.08f;
        [SerializeField, Range(0f, 1f)] private float m_ShadowAlpha = 0.75f;
        [SerializeField] private Color m_ShadowColor = new Color(0.02f, 0.04f, 0.10f, 0.75f);

        private SpriteRenderer m_ShadowRenderer;
        private bool m_ShadowVisible = true;
        private static Sprite s_ShadowSprite;

        public bool EnableFakeShadow
        {
            get => m_EnableFakeShadow;
            set
            {
                m_EnableFakeShadow = value;
                if (m_ShadowRenderer != null) m_ShadowRenderer.enabled = value && m_ShadowVisible;
            }
        }

        public float ShadowWidth { get => m_ShadowWidth; set => m_ShadowWidth = value; }
        public float ShadowHeight { get => m_ShadowHeight; set => m_ShadowHeight = value; }
        public float ShadowAlpha { get => m_ShadowAlpha; set { m_ShadowAlpha = value; UpdateShadowColor(); } }
        public float ShadowOffsetY { get => m_ShadowOffsetY; set => m_ShadowOffsetY = value; }

        private void UpdateShadowColor()
        {
            if (m_ShadowRenderer != null)
            {
                Color c = m_ShadowColor;
                c.a = m_ShadowAlpha;
                m_ShadowRenderer.color = c;
            }
        }

        public void SetupFakeShadow(float groundZ)
        {
            if (float.IsFinite(groundZ)) m_GroundZ = groundZ;
            else EnsureGroundZ();
            EnsureShadowObject();
            FollowShadow();
        }

        private void EnsureGroundZ()
        {
            if (float.IsFinite(m_GroundZ)) return;
            var beach = UpperBeachGround.Instance;
            if (beach != null) m_GroundZ = beach.GroundWorldZ;
        }

        public void SetShadowVisible(bool visible)
        {
            m_ShadowVisible = visible;
            if (m_ShadowRenderer != null)
            {
                m_ShadowRenderer.enabled = visible && m_EnableFakeShadow;
            }
        }

        private void EnsureShadowObject()
        {
            if (!m_EnableFakeShadow)
            {
                if (m_ShadowRenderer != null)
                {
                    m_ShadowRenderer.enabled = false;
                    if (m_ShadowRenderer.gameObject != null) Destroy(m_ShadowRenderer.gameObject);
                    m_ShadowRenderer = null;
                }
                return;
            }

            if (m_ShadowRenderer == null)
            {
                Transform existing = transform.Find("Sailor_FakeShadow");
                if (existing != null)
                {
                    m_ShadowRenderer = existing.GetComponent<SpriteRenderer>();
                }

                if (m_ShadowRenderer == null)
                {
                    var go = new GameObject("Sailor_FakeShadow");
                    go.transform.SetParent(transform, false);
                    m_ShadowRenderer = go.AddComponent<SpriteRenderer>();
                }

                m_ShadowRenderer.sprite = GetShadowSprite();
                m_ShadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m_ShadowRenderer.receiveShadows = false;
                m_ShadowRenderer.sortingLayerName = "Default";
                m_ShadowRenderer.sortingOrder = 5; // Sahil zemininden kesinlikle önde, ayakların altında
            }

            UpdateShadowColor();
            m_ShadowRenderer.enabled = m_EnableFakeShadow && m_ShadowVisible;
        }

        private void FollowShadow()
        {
            if (m_ShadowRenderer == null || !m_EnableFakeShadow || !m_ShadowVisible) return;
            float unit = m_ModelPivot != null ? m_ModelPivot.lossyScale.x : transform.lossyScale.x;
            Vector3 p = transform.position;
            p.y += m_ShadowOffsetY * unit;

            EnsureGroundZ();
            // Zemin düzleminin hemen önünde (kameraya doğru -Z), dönmeden, XY'de yatık
            if (float.IsFinite(m_GroundZ)) p.z = m_GroundZ - unit * 0.02f;
            else p.z = transform.position.z - unit * 0.02f;

            m_ShadowRenderer.transform.position = p;
            m_ShadowRenderer.transform.rotation = Quaternion.identity;
            float sizeX = m_ShadowWidth * unit;
            float sizeY = m_ShadowHeight * unit;
            Vector3 ps = transform.lossyScale;
            m_ShadowRenderer.transform.localScale = new Vector3(sizeX / Mathf.Max(1e-4f, ps.x), sizeY / Mathf.Max(1e-4f, ps.y), 1f);
        }

        private static Sprite GetShadowSprite()
        {
            if (s_ShadowSprite != null) return s_ShadowSprite;

            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // Merkezde (r <= 0.35) belirgin gölge, dışarıya doğru (0.35..1.0) yumuşak karesel falloff
                    float a;
                    if (r <= 0.35f) a = 1f;
                    else if (r >= 1.0f) a = 0f;
                    else
                    {
                        float t = (r - 0.35f) / 0.65f;
                        float smooth = Mathf.SmoothStep(1f, 0f, t);
                        a = smooth * smooth;
                    }
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_ShadowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return s_ShadowSprite;
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

            if (!m_EnableTeamRing)
            {
                var existingRing = transform.Find("TeamRing");
                if (existingRing != null) Destroy(existingRing.gameObject);
            }

            if (m_EnableFakeShadow)
            {
                EnsureShadowObject();
            }
        }

        private string MapState(string state)
        {
            if (state == null) return StateIdle;
            if (m_UseWalkClips)
            {
                if (state == StateRunEmpty) return "Walk_Empty";
                if (state == StateRunCarry) return "Walk_Carry";
            }
            return state;
        }

        private bool IsLocomotion(string state) => state == StateRunEmpty || state == StateRunCarry || state == "Walk_Empty" || state == "Walk_Carry" || state == "Run_Empty" || state == "Run_Carry";

        /// <summary>
        /// Görev aşamasını oynatır. Hem doğrudan CrossFade ile klibe geçer hem de Animator Controller parametrelerini günceller.
        /// </summary>
        public void Play(string state, float fade = 0.08f)
        {
            m_QueuedState = null;
            if (m_Animator == null || m_Animator.runtimeAnimatorController == null) return;

            string clipState = MapState(state);
            if (m_State == clipState) return;
            m_State = clipState;

            if (state == StateHopDown)
            {
                ResetTriggerSafe(s_Pickup); ResetTriggerSafe(s_HopBoard);
                SetBoolSafe(s_Carrying, false);
                SetTriggerSafe(s_HopDown);
            }
            else if (state == StatePickup)
            {
                SetBoolSafe(s_Carrying, true);
                SetTriggerSafe(s_Pickup);
            }
            else if (state == StateHopBoard)
            {
                SetTriggerSafe(s_HopBoard);
            }
            else if (state == StateRunEmpty)
            {
                SetBoolSafe(s_Carrying, false);
            }
            else if (state == StateRunCarry)
            {
                SetBoolSafe(s_Carrying, true);
            }

            m_Animator.CrossFadeInFixedTime(clipState, fade);
        }

        /// <summary>Tek seferlik klibi oynatır (ör. Pickup), bitince sıradaki duruma geçer (ör. StateRunCarry).</summary>
        public void PlayThen(string state, string next)
        {
            Play(state, 0.05f);
            m_QueuedState = next;
            m_QueuedAt = Time.time + GetClipLength(state);
        }

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
            clipName = MapState(clipName);
            if (m_Animator == null || m_Animator.runtimeAnimatorController == null) return 0.3f;
            foreach (var clip in m_Animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == clipName) return clip.length;
            return 0.3f;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (m_QueuedState != null && Time.time >= m_QueuedAt)
            {
                string next = m_QueuedState;
                m_QueuedState = null;
                Play(next, 0.05f);
            }

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
            FollowShadow();

            // Yürüme durumlarında ayak kaymasın: klip hızını yumuşatılmış yürüme hızına eşle
            m_SmoothSpeed = Mathf.Lerp(m_SmoothSpeed, vel.magnitude / unit, 1f - Mathf.Exp(-dt / 0.2f));
            if (m_Animator != null && m_Animator.runtimeAnimatorController != null)
            {
                if (IsLocomotion(m_State))
                {
                    bool walk = m_State == "Walk_Empty" || m_State == "Walk_Carry";
                    float reference = Mathf.Max(0.1f, walk ? m_WalkClipReferenceSpeed : m_RunClipReferenceSpeed);
                    m_Animator.speed = Mathf.Clamp(m_SmoothSpeed / reference, m_AnimatorSpeedRange.x, m_AnimatorSpeedRange.y);
                }
                else
                {
                    m_Animator.speed = 1f;
                }
                float moveSpeed = Mathf.Clamp(m_SmoothSpeed / Mathf.Max(0.1f, m_ClipReferenceSpeed), m_MoveSpeedRange.x, m_MoveSpeedRange.y);
                if (HasParam(s_MoveSpeed)) m_Animator.SetFloat(s_MoveSpeed, moveSpeed);
            }
        }

        private void ApplyFacing(float t)
        {
            if (m_ModelPivot == null) return;
            // Model: yukarı +Y, yüz +Z. Dünyada yukarı kameraya doğru (-Z), yüz yürüme yönünde (XY düzlemi).
            Quaternion baseFacing = Quaternion.LookRotation(m_Facing, Vector3.back);
            Quaternion target;
            if (m_TiltTowardsCamera > 0.01f)
            {
                // Karakterin omurgasını dünya X ekseninde kameraya doğru geriye yatır (Zemin -Z'den Ekran +Y'ye).
                // Böylece yürüme yönünden bağımsız olarak yüzü, göğsü ve vücudu kameraya net şekilde görünür.
                target = Quaternion.Euler(m_TiltTowardsCamera, 0f, 0f) * baseFacing;
            }
            else
            {
                target = baseFacing;
            }
            m_ModelPivot.rotation = t >= 1f ? target : Quaternion.Slerp(m_ModelPivot.rotation, target, t);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_ModelPivot == null) m_ModelPivot = transform.Find("ModelPivot");
            if (m_ModelPivot != null && !Application.isPlaying)
            {
                ApplyFacing(1f);
            }
        }
#endif

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
