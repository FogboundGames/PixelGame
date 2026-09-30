using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Panodan koparılıp gemiye giden kargo parçasının "yürüyen küp" görseli.
    ///
    /// Görsel, sahnedeki ana küp prefab'ından (PixelArtGenerator.CubePrefab) türetilir:
    /// gövde mesh'i + Leg_L / Leg_R alt parçaları kopyalanır. Böylece ana küp
    /// değiştiğinde uçan parça da otomatik olarak ona benzer — ayrıca prefab'ın
    /// PixelCube / Collider bileşenleri kopyalanmadığı için Awake yan etkisi olmaz.
    ///
    /// Yürüyüş bir AnimationClip veya Animator ile değil, bacakların ebeveyn X ekseni
    /// etrafında ters fazda salınmasıyla üretilir; adım frekansı ilerleme hızına
    /// bağlandığı için kısa ve uzun yollarda ayaklar "kayıyormuş" gibi durmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public class WalkingCargoVisual : MonoBehaviour
    {
        // Bacakların dinlenme (rest) rotasyonları — salınım bunların üzerine bindirilir.
        private Transform m_Visual;
        private Transform m_LegL;
        private Transform m_LegR;
        private Quaternion m_LegLRest = Quaternion.identity;
        private Quaternion m_LegRRest = Quaternion.identity;
        private Quaternion m_VisualRest = Quaternion.identity;

        private float m_StepPhase;
        private float m_BaseScale = 1f;
        private float m_AirborneBlend;

        // ---- Paytak Yürüyüş Ayarları (Waddle Kinematics) ----
        /// <summary>Bacağın ileri/geri salınım genliği (derece).</summary>
        private const float SwingDegrees = 36f;
        /// <summary>Bacağın paytak (dışa doğru) açılma/yaylanma genliği (derece).</summary>
        private const float LegFlareDegrees = 14f;
        /// <summary>Gövdenin sağa/sola paytak yalpalama genliği (derece).</summary>
        private const float WaddleRollDegrees = 8.5f;
        /// <summary>Gövdenin kalça dönüş genliği (derece).</summary>
        private const float HipYawDegrees = 5.5f;
        /// <summary>Her adımda yukarı yaylanma/zıplama yüksekliği (ölçek çarpanı).</summary>
        private const float StepHopHeight = 0.055f;
        /// <summary>Öne doğru hafif hevesli eğim açısı (derece).</summary>
        private const float ForwardLeanDegrees = 3.5f;
        /// <summary>
        /// İlerleme hızının adım frekansına çarpanı (adım/sn = hız * bu).
        /// </summary>
        private const float StepsPerUnitSpeed = 1.65f;
        /// <summary>Adım frekansının alt sınırı (adım/sn) — çok yavaşta bacak donmasın.</summary>
        private const float MinStepsPerSecond = 2.2f;

        private float m_CurrentHeadingYaw = 0f;

        public Transform Visual => m_Visual;
        public bool HasLegs => m_LegL != null || m_LegR != null;

        /// <summary>
        /// Verilen köke yürüyen küp görselini kurar. Ana küp prefab'ı bulunamazsa
        /// basit bir kutuya düşer (eski davranış) — bu durumda HasLegs false olur.
        /// </summary>
        public static WalkingCargoVisual Attach(GameObject root, float baseScale)
        {
            WalkingCargoVisual w = root.AddComponent<WalkingCargoVisual>();
            w.m_BaseScale = baseScale;

            GameObject source = ResolveCubePrefab();

            GameObject visual;
            if (source != null)
            {
                visual = BuildFromPrefab(source.transform, null);
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Collider c = visual.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }

            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = ResolveCubeTilt();
            visual.transform.localScale = Vector3.one * baseScale;

            w.m_Visual = visual.transform;
            // Küp panodaki duruşunu AYNEN korur: yerinden ayrılırken dönmez, doğrulmaz.
            w.m_VisualRest = visual.transform.localRotation;
            w.m_LegL = visual.transform.Find("Leg_L");
            w.m_LegR = visual.transform.Find("Leg_R");
            if (w.m_LegL != null) w.m_LegLRest = w.m_LegL.localRotation;
            if (w.m_LegR != null) w.m_LegRRest = w.m_LegR.localRotation;

            // Adımlar her parçada aynı anda başlamasın — aynı renkten çok sayıda küp
            // patladığında hepsi tek vücut gibi yürüyor görünmesin.
            w.m_StepPhase = Random.Range(0f, Mathf.PI * 2f);

            return w;
        }

        private static GameObject ResolveCubePrefab()
        {
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null && gen.CubePrefab != null) return gen.CubePrefab;

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("MainCube t:Prefab");
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) return prefab;
            }
#endif
            return null;
        }

        /// <summary>
        /// Panodaki küplerin eğimi. Prefab'ın kendi kayıtlı rotasyonu KULLANILMAZ:
        /// generator sahneye yerleştirirken onu zaten CubeFrontTiltAngle ile eziyor,
        /// prefab'ta duran değer bayat (ters işaretli) olabiliyor. Ters eğimde
        /// bacaklar gövdenin arkasına düşüp kameradan görünmez oluyordu.
        /// </summary>
        private static Quaternion ResolveCubeTilt()
        {
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            float tilt = gen != null ? gen.CubeFrontTiltAngle : 25f;
            return Quaternion.Euler(tilt, 0f, 0f);
        }

        /// <summary>
        /// Prefab hiyerarşisini mesh + transform olarak kopyalar. Bileşen taşımaz,
        /// gölge quad'larını atlar.
        /// </summary>
        private static GameObject BuildFromPrefab(Transform src, Transform parent)
        {
            GameObject go = new GameObject(src.name);
            if (parent != null) go.transform.SetParent(parent, false);

            go.transform.localPosition = src.localPosition;
            // Kökün rotasyonu ResolveCubeTilt() ile ayrıca kuruluyor; burada kopyalanan
            // değer yalnızca alt parçalar (bacaklar) için anlamlı.
            go.transform.localRotation = parent != null ? src.localRotation : Quaternion.identity;
            go.transform.localScale = src.localScale;

            MeshFilter srcMf = src.GetComponent<MeshFilter>();
            MeshRenderer srcMr = src.GetComponent<MeshRenderer>();
            if (srcMf != null && srcMf.sharedMesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = srcMf.sharedMesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                if (srcMr != null && srcMr.sharedMaterial != null)
                {
                    mr.sharedMaterial = srcMr.sharedMaterial;
                }
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            for (int i = 0; i < src.childCount; i++)
            {
                Transform child = src.GetChild(i);
                if (child.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                BuildFromPrefab(child, go.transform);
            }

            return go;
        }

        private static MaterialPropertyBlock s_CargoPropertyBlock;
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");
        private static readonly int HColorProp = Shader.PropertyToID("_HColor");
        private static readonly int SColorProp = Shader.PropertyToID("_SColor");
        private static readonly int SpecularHighlightsProp = Shader.PropertyToID("_SpecularHighlights");
        private static readonly int SmoothnessProp = Shader.PropertyToID("_Smoothness");
        private static readonly int SpecularRoughnessPBRProp = Shader.PropertyToID("_SpecularRoughnessPBR");
        private static readonly int RampSmoothingProp = Shader.PropertyToID("_RampSmoothing");
        private static readonly int RampThresholdProp = Shader.PropertyToID("_RampThreshold");
        private static readonly int StylizedPlasticOnProp = Shader.PropertyToID("_StylizedPlasticOn");
        private static readonly int PlasticTopLightProp = Shader.PropertyToID("_PlasticTopLight");
        private static readonly int PlasticHighlightIntensityProp = Shader.PropertyToID("_PlasticHighlightIntensity");

        /// <summary>
        /// Görseldeki tüm renderer'lara rengi MaterialPropertyBlock ile uygular.
        /// Panodaki küpün materyali (PixelCube_Cartoon) ile %100 aynı görünür;
        /// fazladan parlama, emission (HDR glow) veya ton açılması olmaz.
        /// </summary>
        public void ApplyColor(Color color)
        {
            if (m_Visual == null) return;
            MeshRenderer[] all = m_Visual.GetComponentsInChildren<MeshRenderer>(true);
            if (all.Length == 0) return;

            if (s_CargoPropertyBlock == null)
                s_CargoPropertyBlock = new MaterialPropertyBlock();

            s_CargoPropertyBlock.Clear();
            s_CargoPropertyBlock.SetColor(BaseColorProp, color);
            s_CargoPropertyBlock.SetColor(ColorProp, color);
            s_CargoPropertyBlock.SetColor(EmissionColorProp, Color.black);

            Color hColor = Color.Lerp(Color.white, color, 0.45f);
            Color sColor = color * 0.70f;
            s_CargoPropertyBlock.SetColor(HColorProp, hColor);
            s_CargoPropertyBlock.SetColor(SColorProp, sColor);
            s_CargoPropertyBlock.SetFloat(SpecularHighlightsProp, 0f);
            s_CargoPropertyBlock.SetFloat(SmoothnessProp, 0.22f);
            s_CargoPropertyBlock.SetFloat(SpecularRoughnessPBRProp, 0.60f);
            s_CargoPropertyBlock.SetFloat(RampSmoothingProp, 0.65f);
            s_CargoPropertyBlock.SetFloat(RampThresholdProp, 0.42f);
            s_CargoPropertyBlock.SetFloat(StylizedPlasticOnProp, 0f);
            s_CargoPropertyBlock.SetFloat(PlasticTopLightProp, 0f);
            s_CargoPropertyBlock.SetFloat(PlasticHighlightIntensityProp, 0f);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                {
                    if (all[i].sharedMaterial == null)
                    {
                        Shader toonShader = CartoonShader.Get();
                        if (toonShader != null) all[i].sharedMaterial = new Material(toonShader);
                    }
                    all[i].SetPropertyBlock(s_CargoPropertyBlock);
                }
            }
        }

        /// <summary>Görseldeki tüm renderer'lara aynı materyali uygular.</summary>
        public void ApplyMaterial(Material mat)
        {
            if (mat == null || m_Visual == null) return;
            MeshRenderer[] all = m_Visual.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null) all[i].sharedMaterial = mat;
            }
        }

        /// <summary>
        /// Bir karelik paytak yürüyüş adımı.
        /// Gövde ağırlık basan ayağa doğru sevimli bir şekilde yalpalar (waddle roll & hip yaw),
        /// her adımda yaylanır ve bacaklar hafif dışa açılarak paytak paytak yürür.
        /// </summary>
        public void Walk(float deltaTime, float speed, Vector3 moveDirection)
        {
            if (m_Visual == null) return;

            m_AirborneBlend = Mathf.MoveTowards(m_AirborneBlend, 0f, deltaTime * 6f);

            float stepsPerSecond = Mathf.Max(MinStepsPerSecond, speed * StepsPerUnitSpeed);
            m_StepPhase += deltaTime * stepsPerSecond * Mathf.PI * 2f;

            float stepSin = Mathf.Sin(m_StepPhase);

            // 1. Paytak Bacak Hareketi (Swing + Dışa Açılma / Flare)
            // Sol bacak öne giderken hafif sola dışa açılır; sağ bacak öne giderken hafif sağa dışa açılır.
            float swingL = stepSin * SwingDegrees;
            float swingR = -stepSin * SwingDegrees;
            float flareL = Mathf.Max(0f, stepSin) * LegFlareDegrees;
            float flareR = Mathf.Max(0f, -stepSin) * LegFlareDegrees;

            if (m_LegL != null)
                m_LegL.localRotation = Quaternion.Euler(swingL, flareL * 0.4f, -flareL) * m_LegLRest;
            if (m_LegR != null)
                m_LegR.localRotation = Quaternion.Euler(swingR, -flareR * 0.4f, flareR) * m_LegRRest;

            // 2. Paytak Gövde Yalpalaması (Waddle Roll & Hip Sway)
            // Ağırlık basan tarafa doğru tatlı bir eğilme (roll) ve kalça dönüşü (yaw)
            float roll = -stepSin * WaddleRollDegrees;
            float hipYaw = stepSin * HipYawDegrees;

            // Hareket yönüne yumuşak yönelme (Heading Yaw)
            float targetHeadingYaw = 0f;
            if (moveDirection.sqrMagnitude > 1e-4f)
            {
                // Yatay hareket varsa o yöne hafif yönelir (maksimum 20 derece)
                targetHeadingYaw = Mathf.Clamp(moveDirection.x * 26f, -20f, 20f);
            }
            m_CurrentHeadingYaw = Mathf.Lerp(m_CurrentHeadingYaw, targetHeadingYaw, deltaTime * 8f);

            Quaternion waddleTilt = Quaternion.Euler(ForwardLeanDegrees, m_CurrentHeadingYaw + hipYaw, roll);
            m_Visual.localRotation = m_VisualRest * waddleTilt;

            // 3. Adım Başına Yukarı Zıplama/Yaylanma (Step Hop & Bounce)
            // Adım frekansı her iki ayakta da bir tepe noktası oluşturur (çift frekanslı zıplama)
            float hopFactor = stepSin * stepSin; // 0 (ayak yerde) -> 1 (havada)
            float hopY = hopFactor * StepHopHeight * m_BaseScale;
            m_Visual.localPosition = new Vector3(0f, hopY, 0f);

            // 4. Boyut Sabitliği: Küp panodaki özgün boyutunu (m_BaseScale) birebir korur, küçülmez.
            m_Visual.localScale = Vector3.one * m_BaseScale;
        }

        /// <summary>Havadayken bacaklar toplanır, küp boyutunu sabit korur.</summary>
        public void SetAirborne(float deltaTime)
        {
            if (m_Visual == null) return;

            m_AirborneBlend = Mathf.MoveTowards(m_AirborneBlend, 1f, deltaTime * 8f);

            // Zıplarken bacaklar tatlıca öne toplanır (tuck)
            if (m_LegL != null)
                m_LegL.localRotation = Quaternion.Slerp(m_LegL.localRotation,
                    Quaternion.Euler(-38f, 8f, -10f) * m_LegLRest, m_AirborneBlend);
            if (m_LegR != null)
                m_LegR.localRotation = Quaternion.Slerp(m_LegR.localRotation,
                    Quaternion.Euler(-38f, -8f, 10f) * m_LegRRest, m_AirborneBlend);

            m_Visual.localPosition = Vector3.zero;
            m_Visual.localScale = Vector3.one * m_BaseScale;
        }

        /// <summary>Kıyıya varışta minik çömelme ve yaylanma (zıplamaya hazırlık).</summary>
        public void Crouch(float amount01)
        {
            if (m_Visual == null) return;
            float a = Mathf.Clamp01(amount01);
            m_Visual.localPosition = new Vector3(0f, -0.06f * a * m_BaseScale, 0f);
            m_Visual.localRotation = m_VisualRest;
            m_Visual.localScale = Vector3.one * m_BaseScale;

            // Çömelirken bacaklar hafif dışa bükülür
            if (m_LegL != null)
                m_LegL.localRotation = Quaternion.Euler(14f * a, 8f * a, -12f * a) * m_LegLRest;
            if (m_LegR != null)
                m_LegR.localRotation = Quaternion.Euler(14f * a, -8f * a, 12f * a) * m_LegRRest;
        }
    }
}
