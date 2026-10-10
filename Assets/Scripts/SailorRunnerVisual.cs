using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Denizci koşucunun görseli: modeli kumsal düzleminde (zemin XY, yukarı = kameraya doğru -Z) dik tutar,
    /// yürüdüğü yöne çevirir ve görev aşamalarına göre animasyon oynatır. Kökün dönüşünü
    /// (CubeMovementController'ın sağa-sola yatırması) yok sayar; sadece ModelPivot'un dünya dönüşünü yazar.
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
        [Tooltip("Yöne dönüş hızı (derece/sn).")]
        [SerializeField] private float m_TurnSpeed = 720f;
        [Tooltip("Koşu klibinin 1x hızda karşıladığı yürüme hızı (model birimi/sn). Daha hızlı yürürken klip hızlanır.")]
        [SerializeField] private float m_ClipReferenceSpeed = 2.5f;
        [SerializeField] private Vector2 m_AnimatorSpeedRange = new Vector2(0.8f, 3f);

        private Vector3 m_LastPos;
        private Vector3 m_Facing = Vector3.up;
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

        /// <summary>Durumu yumuşak geçişle oynatır; aynı durum tekrar istenirse baştan başlatmaz.</summary>
        public void Play(string state, float fade = 0.08f)
        {
            m_QueuedState = null;
            if (m_Animator == null || m_State == state) return;
            m_State = state;
            m_Animator.CrossFadeInFixedTime(state, fade);
        }

        /// <summary>Tek seferlik klibi oynatır, bitince sıradaki duruma geçer (ör. Pickup → Run_Carry).</summary>
        public void PlayThen(string state, string next)
        {
            Play(state, 0.05f);
            m_QueuedState = next;
            m_QueuedAt = Time.time + ClipLength(state);
        }

        private float ClipLength(string state)
        {
            if (m_Animator == null || m_Animator.runtimeAnimatorController == null) return 0.3f;
            foreach (var clip in m_Animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == state) return clip.length;
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
                Play(next, 0f);
            }

            Vector3 delta = transform.position - m_LastPos;
            m_LastPos = transform.position;
            delta.z = 0f;
            float moved = delta.magnitude;
            if (moved > 1e-5f) m_Facing = delta / moved;
            ApplyFacing(Mathf.Clamp01(m_TurnSpeed * dt / 180f));
            FollowSocket();

            // Koşu kliplerinde ayak kaymasın: klip hızını yürüme hızına eşle
            if (m_Animator != null && (m_State == StateRunEmpty || m_State == StateRunCarry))
            {
                float unit = m_ModelPivot != null ? Mathf.Max(1e-4f, m_ModelPivot.lossyScale.y) : 1f;
                float speedInModelUnits = moved / dt / unit;
                m_Animator.speed = Mathf.Clamp(speedInModelUnits / Mathf.Max(0.1f, m_ClipReferenceSpeed), m_AnimatorSpeedRange.x, m_AnimatorSpeedRange.y);
            }
            else if (m_Animator != null)
            {
                m_Animator.speed = 1f;
            }
        }

        private void ApplyFacing(float t)
        {
            if (m_ModelPivot == null) return;
            // Model: yukarı +Y, yüz +Z. Dünyada yukarı kameraya doğru (-Z), yüz yürüme yönünde (XY düzlemi).
            Quaternion target = Quaternion.LookRotation(m_Facing, Vector3.back);
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
