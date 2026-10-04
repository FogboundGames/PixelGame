using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Kodla yürüyen kargo küpü: kısa, tombul iki bacakla paytak paytak yürür.
    ///
    /// Mixamo koşusu bacakları ileri geri sallıyordu; kamera küpe önden baktığı için bu
    /// hareket görünmüyor, bacaklar uzayıp kısalan çubuklar gibi duruyordu. Burada bacaklar
    /// sağa sola açılıp kalkar, gövde de basan ayağa doğru yalpalar — önden en iyi okunan hareket.
    ///
    /// Bacakların duruşu tamamen prefab'dadır: LegsMount, Legs ve bacakların konumu/açısı/ölçeği
    /// prefab'da nasıl ayarlandıysa sahnede ve oyunda öyle durur; kod bunlara dokunmaz.
    /// Kod yalnızca oyunda yürürken adım hareketini bacakların kendi duruşunun üstüne ekler
    /// (küp durunca bacaklar prefab'daki duruşa döner). Adım sıklığı katedilen yola bağlıdır;
    /// hız ne olursa olsun ayaklar kaymaz.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaddleRunner : MonoBehaviour, ICargoRunner
    {
        [Header("Parçalar")]
        [SerializeField] private Transform m_Body;
        [Tooltip("Bacakların bağlantı noktası (LegsMount). Ayrı bir gövde parçası zıplarsa bacaklar onunla birlikte kalkar.")]
        [SerializeField] private Transform m_Legs;
        [SerializeField] private Transform m_LegL;
        [SerializeField] private Transform m_LegR;

        [Header("Paytak Yürüyüş")]
        [Tooltip("Küp boyu kadar yolda atılan adım sayısı.")]
        [SerializeField] private float m_StepsPerCube = 1.6f;
        [Tooltip("Adım atan bacağın yana açılma açısı (derece).")]
        [SerializeField] private float m_LegSplayDegrees = 24f;
        [Tooltip("Adım atan bacağın kalkma yüksekliği (küp boyu cinsinden).")]
        [SerializeField] private float m_LegLift = 0.08f;
        [Tooltip("Gövdenin basan ayağa doğru yalpalama açısı (derece).")]
        [SerializeField] private float m_BodyRollDegrees = 7f;
        [Tooltip("Her adımda gövdenin zıplama yüksekliği (küp boyu cinsinden).")]
        [SerializeField] private float m_BodyBob = 0.05f;
        [Tooltip("Gittiği yöne dönme hızı (derece / sn).")]
        [SerializeField] private float m_TurnSpeed = 360f;
        [Tooltip("Yana giderken en fazla ne kadar döneceği (derece). Fazlası eğik küpün altını gösterir.")]
        [SerializeField] private float m_MaxTurnDegrees = 35f;

        [Header("🌑 Sahil Zemin Temas Gölgesi")]
        [Tooltip("Yürürken küpün altında kumsalda beliren yumuşak zemin temas gölgesi.")]
        [SerializeField] private bool m_EnableFootstepShadow = true;
        [SerializeField] private Vector2 m_ShadowBaseSize = new Vector2(0.34f, 0.16f);

        private GameObject m_FootstepShadow;
        private Transform m_FootstepShadowTransform;
        private MeshRenderer m_FootstepShadowRenderer;
        private static Material s_FootstepShadowMaterial;

        private Quaternion m_BaseRotation;
        private float m_Heading;
        private float m_Phase;
        private Vector3 m_LastPosition;
        private Vector3 m_LegLRest;
        private Vector3 m_LegRRest;
        private Quaternion m_LegLRestRotation = Quaternion.identity;
        private Quaternion m_LegRRestRotation = Quaternion.identity;
        private Vector3 m_LegsRest;
        private bool m_IsAirborne;
        public bool IsAirborne { get => m_IsAirborne; set => m_IsAirborne = value; }

        private void Awake()
        {
            m_BaseRotation = transform.rotation;
            m_LastPosition = transform.position;
            if (m_LegL != null) { m_LegLRest = m_LegL.localPosition; m_LegLRestRotation = m_LegL.localRotation; }
            if (m_LegR != null) { m_LegRRest = m_LegR.localPosition; m_LegRRestRotation = m_LegR.localRotation; }
            if (m_Legs != null) m_LegsRest = m_Legs.localPosition;
        }

        /// <summary>
        /// Panodaki küpün boyutunu alır. Kök eşit ölçeklenir (bacaklar eğilip dönerken çarpılmasın),
        /// gövdenin derinlik farkı sadece gövdeye uygulanır.
        /// </summary>
        public void Setup(Vector3 cubeScale)
        {
            float size = Mathf.Max(1e-4f, cubeScale.x);
            transform.localScale = Vector3.one * size;
            if (m_Body != null) m_Body.localScale = new Vector3(1f, cubeScale.y / size, cubeScale.z / size);
        }

        public void BeginWalk(int indexInRope)
        {
            // Panodaki küp, generator onu yerleştirip eğdikten sonra yürümeye başlar;
            // Awake'teki duruş o yüzden eski olabilir.
            m_BaseRotation = transform.rotation;
            m_LastPosition = transform.position;
            m_Heading = 0f;
            m_Phase = (indexInRope % 2) * Mathf.PI;

            // Küpün ve bacaklarının sahil zeminine gerçek zamanlı URP gölgesi düşürmesini sağla
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (renderers[i].name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            if (m_EnableFootstepShadow)
            {
                EnsureFootstepShadow();
            }
        }

        private void EnsureFootstepShadow()
        {
            if (m_FootstepShadow == null)
            {
                Transform existing = transform.Find("[WalkFootstepShadow]");
                if (existing != null)
                {
                    m_FootstepShadow = existing.gameObject;
                }
                else
                {
                    m_FootstepShadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    m_FootstepShadow.name = "[WalkFootstepShadow]";
                    m_FootstepShadow.transform.SetParent(transform, false);

                    Collider col = m_FootstepShadow.GetComponent<Collider>();
                    if (col != null)
                    {
                        if (Application.isPlaying) Destroy(col);
                        else DestroyImmediate(col);
                    }
                    m_FootstepShadow.layer = 2; // Ignore Raycast
                }

                if (s_FootstepShadowMaterial == null)
                {
#if UNITY_EDITOR
                    s_FootstepShadowMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SoftVoxelShadow_Mat.mat");
#endif
                    if (s_FootstepShadowMaterial == null)
                    {
                        Shader sh = Shader.Find("Sprites/Default");
                        if (sh != null) s_FootstepShadowMaterial = new Material(sh);
                    }
                }

                m_FootstepShadowRenderer = m_FootstepShadow.GetComponent<MeshRenderer>();
                if (m_FootstepShadowRenderer != null)
                {
                    if (s_FootstepShadowMaterial != null) m_FootstepShadowRenderer.sharedMaterial = s_FootstepShadowMaterial;
                    m_FootstepShadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    m_FootstepShadowRenderer.receiveShadows = false;
                    m_FootstepShadowRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    m_FootstepShadowRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                    m_FootstepShadowRenderer.sortingOrder = 2;
                }

                m_FootstepShadowTransform = m_FootstepShadow.transform;
            }

            if (m_FootstepShadow != null)
            {
                m_FootstepShadow.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (m_FootstepShadow != null)
            {
                m_FootstepShadow.SetActive(false);
            }
        }

        public void TurnToward(Vector3 screenMove, float deltaTime)
        {
            if (screenMove.x * screenMove.x + screenMove.y * screenMove.y < 1e-8f) return;

            float target = CargoRunnerHeading.TargetYaw(screenMove, m_MaxTurnDegrees);
            m_Heading = Mathf.MoveTowards(m_Heading, target, m_TurnSpeed * deltaTime);
            transform.rotation = CargoRunnerHeading.Apply(m_BaseRotation, m_Heading);
        }

        private void LateUpdate()
        {
            if (m_IsAirborne)
            {
                if (m_FootstepShadow != null && m_FootstepShadow.activeSelf)
                {
                    m_FootstepShadow.SetActive(false);
                }
                if (m_Body != null)
                {
                    m_Body.localRotation = Quaternion.identity;
                    m_Body.localPosition = Vector3.zero;
                }
                if (m_Legs != null)
                {
                    m_Legs.localPosition = m_LegsRest;
                }
                if (m_LegL != null)
                {
                    m_LegL.localRotation = m_LegLRestRotation * Quaternion.Euler(-25f, 0f, -12f);
                    m_LegL.localPosition = m_LegLRest + new Vector3(0f, 0.06f, 0.02f);
                }
                if (m_LegR != null)
                {
                    m_LegR.localRotation = m_LegRRestRotation * Quaternion.Euler(-25f, 0f, 12f);
                    m_LegR.localPosition = m_LegRRest + new Vector3(0f, 0.06f, 0.02f);
                }
                return;
            }

            float size = Mathf.Max(1e-4f, transform.lossyScale.x);
            Vector3 delta = transform.position - m_LastPosition;
            m_LastPosition = transform.position;
            float moved = new Vector2(delta.x, delta.y).magnitude;
            m_Phase += moved / size * m_StepsPerCube * Mathf.PI;
            if (moved < size * 0.002f)
            {
                // Trende öndekini beklerken adım ortasında donup kalmasın: iki ayağını yere basıp dursun
                float rest = Mathf.Round(m_Phase / Mathf.PI) * Mathf.PI;
                m_Phase = Mathf.MoveTowards(m_Phase, rest, Time.deltaTime * 6f);
            }

            float step = Mathf.Sin(m_Phase);
            float leftUp = Mathf.Max(0f, step);
            float rightUp = Mathf.Max(0f, -step);

            if (m_Body != null)
            {
                // Basan ayağa doğru yalpala, her adımda hafifçe zıpla
                m_Body.localRotation = Quaternion.Euler(0f, 0f, -step * m_BodyRollDegrees);
                m_Body.localPosition = new Vector3(0f, Mathf.Abs(step) * m_BodyBob, 0f);
            }

            if (m_Legs != null && m_Body != null)
            {
                // Gövde zıplarken bacaklar da onunla birlikte kalkar (gövdeden ayrılmasın)
                m_Legs.localPosition = m_LegsRest + m_Body.localPosition;
            }

            if (m_LegL != null)
            {
                // Adım hareketi, bacağın prefab'daki kendi açısının üstüne eklenir
                m_LegL.localRotation = m_LegLRestRotation * Quaternion.Euler(0f, 0f, -leftUp * m_LegSplayDegrees);
                m_LegL.localPosition = m_LegLRest + new Vector3(0f, leftUp * m_LegLift, 0f);
            }
            if (m_LegR != null)
            {
                m_LegR.localRotation = m_LegRRestRotation * Quaternion.Euler(0f, 0f, rightUp * m_LegSplayDegrees);
                m_LegR.localPosition = m_LegRRest + new Vector3(0f, rightUp * m_LegLift, 0f);
            }

            if (m_FootstepShadowTransform != null && m_FootstepShadow != null && m_FootstepShadow.activeSelf)
            {
                // Gölge kumsal zeminine paralel (ekrana dik) durur
                m_FootstepShadowTransform.rotation = Quaternion.identity;

                // Gövdenin/bacakların altına, basan ayağa doğru hafifçe eşlik ederek yerleşir
                float stepOffset = -step * size * 0.08f;
                Vector3 shadowPos = transform.position + new Vector3(stepOffset, -size * 0.44f, 0.02f);
                m_FootstepShadowTransform.position = shadowPos;

                // Adım atarken zıplamaya göre gölgenin nefes alması (hop etkisi)
                float bobFactor = 1f - (Mathf.Abs(step) * 0.15f);
                float shadowW = size * m_ShadowBaseSize.x * 3.8f * bobFactor;
                float shadowH = size * m_ShadowBaseSize.y * 3.5f * bobFactor;
                m_FootstepShadowTransform.localScale = new Vector3(shadowW, shadowH, 1f);
            }
        }
    }
}
