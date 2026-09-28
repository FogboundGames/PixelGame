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

        // ---- Yürüyüş ayarları ----
        /// <summary>Bacağın ileri/geri salınım genliği (derece).</summary>
        private const float SwingDegrees = 34f;
        /// <summary>
        /// İlerleme hızının adım frekansına çarpanı (adım/sn = hız * bu).
        /// Yürüyüş hızı ~1.9 birim/sn olduğu için bu değer ~3 adım/sn veriyor.
        /// </summary>
        private const float StepsPerUnitSpeed = 1.6f;
        /// <summary>Adım frekansının alt sınırı (adım/sn) — çok yavaşta bacak donmasın.</summary>
        private const float MinStepsPerSecond = 2.0f;

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
        /// Bir karelik yürüyüş adımı. <paramref name="speed"/> parçanın o andaki
        /// dünya hızı (birim/sn); adım frekansı buna bağlanır.
        /// </summary>
        public void Walk(float deltaTime, float speed, Vector3 moveDirection)
        {
            if (m_Visual == null) return;

            m_AirborneBlend = Mathf.MoveTowards(m_AirborneBlend, 0f, deltaTime * 6f);

            float stepsPerSecond = Mathf.Max(MinStepsPerSecond, speed * StepsPerUnitSpeed);
            m_StepPhase += deltaTime * stepsPerSecond * Mathf.PI * 2f;

            // SADECE bacaklar hareket eder. Gövdenin konumu, rotasyonu ve yüksekliği
            // panodaki haliyle birebir aynı kalır — küp yerinden ayrılıp yürür, başka
            // hiçbir şey değişmez. (Gövde sekmesi/yaslanması/yalpalaması bilerek yok.)
            ApplyLegSwing(Mathf.Sin(m_StepPhase) * SwingDegrees);
            m_Visual.localPosition = Vector3.zero;
            m_Visual.localRotation = m_VisualRest;
        }

        /// <summary>Havadayken bacaklar toplanır, sekme durur.</summary>
        public void SetAirborne(float deltaTime)
        {
            if (m_Visual == null) return;

            m_AirborneBlend = Mathf.MoveTowards(m_AirborneBlend, 1f, deltaTime * 8f);

            // Zıplarken bacaklar öne toplanır (tuck).
            if (m_LegL != null)
                m_LegL.localRotation = Quaternion.Slerp(m_LegL.localRotation,
                    Quaternion.Euler(-38f, 0f, 0f) * m_LegLRest, m_AirborneBlend);
            if (m_LegR != null)
                m_LegR.localRotation = Quaternion.Slerp(m_LegR.localRotation,
                    Quaternion.Euler(-24f, 0f, 0f) * m_LegRRest, m_AirborneBlend);

        }

        /// <summary>Kıyıya varışta minik çömelme (zıplamaya hazırlık).</summary>
        public void Crouch(float amount01)
        {
            if (m_Visual == null) return;
            float a = Mathf.Clamp01(amount01);
            m_Visual.localRotation = m_VisualRest;
            m_Visual.localScale = new Vector3(
                m_BaseScale * (1f + 0.18f * a),
                m_BaseScale * (1f - 0.22f * a),
                m_BaseScale * (1f + 0.18f * a));
            ApplyLegSwing(Mathf.Lerp(0f, 14f, a));
        }

        private void ApplyLegSwing(float degrees)
        {
            // Ebeveyn (küp) X ekseni etrafında ön-çarpım: iki bacak da aynı eksende,
            // ters işaretle salınır. Bacakların kendi rest rotasyonları aynalı olduğu
            // için yerel eksende çarpmak ikisini de aynı yöne sallardı.
            if (m_LegL != null)
                m_LegL.localRotation = Quaternion.Euler(degrees, 0f, 0f) * m_LegLRest;
            if (m_LegR != null)
                m_LegR.localRotation = Quaternion.Euler(-degrees, 0f, 0f) * m_LegRRest;
        }
    }
}
