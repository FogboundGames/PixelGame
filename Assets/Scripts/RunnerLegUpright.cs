using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Koşan kargo küpünün duruşunu yönetir: gittiği yöne döner ve bacaklarını küpün
    /// eğiminden bağımsız tutar.
    ///
    /// Panodaki küpler kameraya doğru eğik (ör. -60°) durur; yürüyen küp de aynı açıyla
    /// doğar (doğduğu andaki dönüş "taban" kabul edilir). Gittiği yöne ekranın dikey ekseni
    /// etrafında, kafasını çeviren biri gibi sınırlı bir açıyla döner (bkz. CargoRunnerHeading).
    ///
    /// Bacaklar gövdeye bağlı olduğu için eğimle birlikte gövdenin arkasına kaçıp görünmez
    /// oluyordu. Animator güncellendikten sonra (LateUpdate) bacak kökleri küpün ön-alt
    /// kenarına taşınır ve eğim geri alınır; böylece bacaklar ekranda hep aşağı sarkar,
    /// ama küple birlikte gittiği yöne döner.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunnerLegUpright : MonoBehaviour, ICargoRunner
    {
        [Tooltip("Bacak köklerinin küp merkezine göre konumu (küp boyu = 1). Y: aşağı, Z: kameraya doğru (eksi).")]
        [SerializeField] private Vector2 m_HipAnchorYZ = new Vector2(-0.42f, -0.4f);
        [Tooltip("Gittiği yöne dönme hızı (derece / sn).")]
        [SerializeField] private float m_TurnSpeed = 360f;
        [Tooltip("Yana giderken en fazla ne kadar döneceği (derece). Fazlası eğik küpün altını gösterir.")]
        [SerializeField] private float m_MaxTurnDegrees = 35f;
        [Tooltip("Eğik kameralı (masa üstü) sahne: küp yerde düz durur, ön yüzü (yerel -Y) gidiş yönüne döner, bacaklara dokunulmaz.")]
        [SerializeField] private bool m_TabletopMode = false;
        [SerializeField] private string m_LeftUpLegBone = "mixamorig:LeftUpLeg";
        [SerializeField] private string m_RightUpLegBone = "mixamorig:RightUpLeg";

        private Transform m_LeftUpLeg;
        private Transform m_RightUpLeg;
        private Quaternion m_BaseRotation;
        private float m_Heading;
        // Kalça genişliği (küp boyu = 1). Her karede kemikten okunmaz: bacak kökünü biz
        // taşıdığımız için okunan değer bir sonraki karede kayıyor ve bacaklar gövdeden kopuyordu.
        private float m_LeftSideX;
        private float m_RightSideX;

        private void Awake()
        {
            m_BaseRotation = transform.rotation;

            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == m_LeftUpLegBone) m_LeftUpLeg = t;
                else if (t.name == m_RightUpLegBone) m_RightUpLeg = t;
            }

            if (m_LeftUpLeg != null) m_LeftSideX = transform.InverseTransformPoint(m_LeftUpLeg.position).x;
            if (m_RightUpLeg != null) m_RightSideX = transform.InverseTransformPoint(m_RightUpLeg.position).x;
        }

        /// <summary>
        /// Koşucuyu sabit, dik bir duruşa kilitler: gittiği yöne dönmez, BeginWalk duruşu yeniden
        /// "taban" kabul etmez (önceden her fazda dönmüş hali taban olup koşucu giderek yamuluyordu).
        /// </summary>
        public void LockUpright(Quaternion uprightRotation)
        {
            m_LockedUpright = true;
            m_BaseRotation = uprightRotation;
            m_Heading = 0f;
            transform.rotation = uprightRotation;
        }

        private bool m_LockedUpright;

        /// <summary>Masa üstü (eğik kameralı) duruş: zemin XY düzlemi, "yukarı" kameraya doğru (-Z).</summary>
        public bool IsTabletop => m_TabletopMode;

        public void BeginWalk(int indexInRope)
        {
            if (m_LockedUpright)
            {
                transform.rotation = m_BaseRotation;
                m_Heading = 0f;
                return;
            }
            m_BaseRotation = transform.rotation;
            m_Heading = 0f;
        }

        /// <summary>
        /// Ekrandaki hareket yönüne doğru yumuşakça döner: yana giderken o yana, aşağı/yukarı
        /// giderken düz bakar.
        /// </summary>
        public void TurnToward(Vector3 screenMove, float deltaTime)
        {
            if (m_LockedUpright)
            {
                transform.rotation = m_BaseRotation;
                return;
            }

            if (m_TabletopMode)
            {
                // Yere dik eksen (yerel Z) etrafında: ön yüz (yerel -Y) gidiş yönüne baksın
                Vector3 local = Quaternion.Inverse(m_BaseRotation) * screenMove;
                if (local.x * local.x + local.y * local.y < 1e-8f) return;
                float yaw = Mathf.Atan2(local.x, -local.y) * Mathf.Rad2Deg;
                m_Heading = Mathf.MoveTowardsAngle(m_Heading, yaw, m_TurnSpeed * deltaTime);
                transform.rotation = m_BaseRotation * Quaternion.AngleAxis(m_Heading, Vector3.forward);
                return;
            }

            if (screenMove.x * screenMove.x + screenMove.y * screenMove.y < 1e-8f) return;

            float target = CargoRunnerHeading.TargetYaw(screenMove, m_MaxTurnDegrees);
            m_Heading = Mathf.MoveTowards(m_Heading, target, m_TurnSpeed * deltaTime);
            transform.rotation = CargoRunnerHeading.Apply(m_BaseRotation, m_Heading);
        }

        private void LateUpdate()
        {
            if (m_TabletopMode) return;
            Quaternion heading = Quaternion.AngleAxis(m_Heading, Vector3.up);
            // Bacakların dünyadaki duruşu: gövde hiç eğik değilmiş, sadece yöne dönmüş gibi.
            Quaternion legFrame = heading * Quaternion.Inverse(transform.rotation);
            Straighten(m_LeftUpLeg, m_LeftSideX, heading, legFrame);
            Straighten(m_RightUpLeg, m_RightSideX, heading, legFrame);
        }

        private void Straighten(Transform upLeg, float sideX, Quaternion heading, Quaternion legFrame)
        {
            if (upLeg == null) return;

            // Kalça genişliği yönle birlikte döner; "kameraya doğru" itiş ise hep kameraya
            // kalır ki bacaklar dönerken de gövdenin önünden sarksın.
            Vector3 scale = transform.lossyScale;
            upLeg.position = transform.position
                + m_BaseRotation * Vector3.Scale(scale, new Vector3(0f, m_HipAnchorYZ.x, m_HipAnchorYZ.y))
                + heading * new Vector3(sideX * scale.x, 0f, 0f);
            upLeg.rotation = legFrame * upLeg.rotation;
        }
    }
}
