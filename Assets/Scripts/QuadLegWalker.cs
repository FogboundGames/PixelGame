using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Dört ayaklı küpün yürüyüşü (tırıs): çapraz bacak çiftleri birlikte adım atar —
    /// sol ön ile sağ arka, sağ ön ile sol arka.
    ///
    /// Bacaklar küpün gittiği yönde ileri geri sallanır; öne giden bacak yerden kalkar,
    /// yerdeki bacak geriye iter. Gövde her adımda yerden hafifçe yükselip iner.
    /// Adım sıklığı katedilen yola bağlıdır (ayaklar kaymaz); küp durunca bacaklar
    /// yumuşakça prefab'daki duruşa döner.
    ///
    /// Bacakların duruşu (konum/açı/ölçek) tamamen prefab'dadır; kod yalnızca hareketi bu
    /// duruşun üstüne ekler. Bacaklar, adı "Leg_L"/"Leg_R" olan pivotlardır; pivotun -Y yönü
    /// bacağın uzandığı yöndür. Ön/arka, küpün yerel Y'sine göre (küçük Y = ön) bulunur.
    /// </summary>
    [DisallowMultipleComponent]
    public class QuadLegWalker : MonoBehaviour, ICargoRunner
    {
        [Header("Adım")]
        [Tooltip("Bir tam adım döngüsünde (iki çiftin birer adımı) katedilen yol, küp boyu cinsinden. Küçük değer = daha sık, kısa adımlar.")]
        [SerializeField] private float m_StrideLength = 0.9f;
        [Tooltip("Bacağın ileri/geri sallanma açısı (derece).")]
        [SerializeField] private float m_SwingDegrees = 28f;
        [Tooltip("Öne giden bacağın yerden kalkma yüksekliği (küp boyu cinsinden).")]
        [SerializeField] private float m_LegLift = 0.12f;

        [Header("Gövde")]
        [Tooltip("Her adımda gövdenin yerden yükselme miktarı (küp boyu cinsinden).")]
        [SerializeField] private float m_BodyBob = 0.06f;
        [Tooltip("Gövdenin adımla birlikte hafif öne/arkaya yalpalaması (derece).")]
        [SerializeField] private float m_BodyRock = 4f;

        [Header("Yönelme")]
        [Tooltip("Ön yüzün (yerel -Y) gittiği yöne dönme hızı (derece / sn).")]
        [SerializeField] private float m_TurnSpeed = 540f;

        [Header("Başlama / Durma")]
        [Tooltip("Hareket başlarken/biterken bacak salınımının açılıp kapanma hızı.")]
        [SerializeField] private float m_BlendSpeed = 8f;

        private class Leg
        {
            public Transform Pivot;
            public Vector3 RestPosition;
            public Quaternion RestRotation;
            public float PhaseOffset;
        }

        private readonly List<Leg> m_Legs = new List<Leg>();
        private float m_Phase;
        private float m_Amount;
        private Vector3 m_LastPosition;
        private Vector3 m_MoveDir;
        private Vector3 m_AppliedBob;
        private Quaternion m_AppliedRock = Quaternion.identity;
        private Vector3 m_WrittenPosition;
        private Quaternion m_WrittenRotation;
        private bool m_Walking;
        private Quaternion m_BaseRotation = Quaternion.identity;
        private float m_Heading;

        private void Awake()
        {
            CollectLegs();
            m_LastPosition = transform.position;
        }

        private void CollectLegs()
        {
            m_Legs.Clear();
            var pivots = new List<Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Leg_L" || t.name == "Leg_R") pivots.Add(t);
            }
            if (pivots.Count == 0) return;

            // Ön/arka: küpün yerel Y'si (küçük = ön, ekranın altına bakan taraf)
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pivots)
            {
                float y = transform.InverseTransformPoint(p.position).y;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
            float midY = (minY + maxY) * 0.5f;

            foreach (var p in pivots)
            {
                bool front = maxY - minY < 1e-3f || transform.InverseTransformPoint(p.position).y <= midY;
                bool left = p.name == "Leg_L";
                // Tırıs: sol-ön ile sağ-arka aynı fazda, diğer çapraz çift ters fazda
                bool groupA = front == left;
                m_Legs.Add(new Leg
                {
                    Pivot = p,
                    RestPosition = p.localPosition,
                    RestRotation = p.localRotation,
                    PhaseOffset = groupA ? 0f : Mathf.PI
                });
            }
        }

        public void BeginWalk(int indexInRope)
        {
            m_LastPosition = transform.position;
            m_WrittenPosition = new Vector3(float.NaN, 0f, 0f);
            m_AppliedBob = Vector3.zero;
            m_AppliedRock = Quaternion.identity;
            m_BaseRotation = transform.rotation;
            m_Heading = 0f;
            // Yan yana küpler ters adımla başlasın (tırtıl gibi akış)
            m_Phase = (indexInRope % 2) * Mathf.PI * 0.5f;
            m_Walking = true;
        }

        /// <summary>
        /// Ön yüzü (yerel -Y, panoda ekranın altına bakan yüz) gittiği yöne çevirir. Dönüş yere dik
        /// eksen (yerel Z: üst yüz ile alt yüzü birleştiren eksen) etrafındadır; üst yüz yukarıda kalır.
        /// </summary>
        public void TurnToward(Vector3 worldMove, float deltaTime)
        {
            Vector3 local = Quaternion.Inverse(m_BaseRotation) * worldMove;
            if (local.x * local.x + local.y * local.y < 1e-8f) return;

            // Rz(θ) ile (0,-1,0) → (sinθ, -cosθ): ön yüz gidiş yönüne baksın
            float target = Mathf.Atan2(local.x, -local.y) * Mathf.Rad2Deg;
            m_Heading = Mathf.MoveTowardsAngle(m_Heading, target, m_TurnSpeed * deltaTime);
            transform.rotation = m_BaseRotation * Quaternion.AngleAxis(m_Heading, Vector3.forward);
        }

        private void LateUpdate()
        {
            if (!m_Walking) return;

            // Geçen karede eklenen gövde yükselmesini/yalpasını çıkar — ama sadece o değeri başka bir
            // kod (tren yolu, gemiye zıplama) bu karede yeniden yazmadıysa; yazdıysa zaten temizdir.
            Vector3 basePosition = transform.position == m_WrittenPosition ? transform.position - m_AppliedBob : transform.position;
            Quaternion baseRotation = transform.rotation == m_WrittenRotation ? transform.rotation * Quaternion.Inverse(m_AppliedRock) : transform.rotation;

            float size = Mathf.Max(1e-4f, transform.lossyScale.x);
            Vector3 delta = basePosition - m_LastPosition;
            m_LastPosition = basePosition;

            float dt = Mathf.Max(1e-5f, Time.deltaTime);
            float moved = delta.magnitude;
            bool moving = moved / dt > size * 0.2f;
            if (moved > 1e-6f) m_MoveDir = delta / moved;

            m_Amount = Mathf.MoveTowards(m_Amount, moving ? 1f : 0f, m_BlendSpeed * dt);
            m_Phase += moved / size / Mathf.Max(0.05f, m_StrideLength) * Mathf.PI * 2f;

            foreach (var leg in m_Legs)
            {
                if (leg.Pivot == null) continue;
                float p = m_Phase + leg.PhaseOffset;
                float swing = Mathf.Sin(p) * m_SwingDegrees * m_Amount;
                // Bacak öne doğru giderken (sin artarken) havada
                float lift = Mathf.Max(0f, Mathf.Cos(p)) * m_LegLift * m_Amount;

                Transform parent = leg.Pivot.parent;
                // Gidiş yönünü bacağın dinlenme çerçevesine çevir; -Y bacağın uzandığı yön
                Vector3 moveInParent = parent != null ? parent.InverseTransformDirection(m_MoveDir) : m_MoveDir;
                Vector3 moveInRest = Quaternion.Inverse(leg.RestRotation) * moveInParent;
                Vector3 down = Vector3.down;
                Vector3 alongGround = Vector3.ProjectOnPlane(moveInRest, down);
                Vector3 axis = alongGround.sqrMagnitude > 1e-6f ? Vector3.Cross(down, alongGround.normalized) : Vector3.right;

                leg.Pivot.localRotation = leg.RestRotation * Quaternion.AngleAxis(swing, axis);

                // Kalkış: pivot gövdeye doğru (bacağın tersi yönünde) çekilir
                Vector3 upInParent = leg.RestRotation * Vector3.up;
                float parentScale = parent != null ? Mathf.Max(1e-4f, parent.lossyScale.x) : 1f;
                leg.Pivot.localPosition = leg.RestPosition + upInParent * (lift * size / parentScale);
            }

            // Gövde: her adımda (çift frekans) yerden yükselir; adımla birlikte hafif yalpalar
            float bob = Mathf.Abs(Mathf.Sin(m_Phase)) * m_BodyBob * size * m_Amount;
            Vector3 awayFromGround = -(baseRotation * Vector3.forward); // alt yüz +Z: yerden uzağa -Z
            m_AppliedBob = awayFromGround * bob;

            Vector3 rockAxis = Vector3.Cross(awayFromGround, m_MoveDir);
            m_AppliedRock = rockAxis.sqrMagnitude > 1e-6f
                ? Quaternion.AngleAxis(Mathf.Sin(m_Phase * 2f) * m_BodyRock * m_Amount, rockAxis.normalized)
                : Quaternion.identity;

            transform.SetPositionAndRotation(basePosition + m_AppliedBob, m_AppliedRock * baseRotation);
            m_WrittenPosition = transform.position;
            m_WrittenRotation = transform.rotation;
            // Yalpalama dünya ekseninde uygulandı; bir sonraki karede geri almak için yerel karşılığını sakla
            m_AppliedRock = Quaternion.Inverse(baseRotation) * m_AppliedRock * baseRotation;
        }
    }
}
