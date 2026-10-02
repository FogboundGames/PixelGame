using UnityEngine;
using UnityEngine.EventSystems;

namespace PixelGame
{
    /// <summary>
    /// Piksel resmini oluşturan her bir 3D küpü temsil eder.
    /// Koordinat, renk ve 360 derece tüm yönleri çevreleyen sahte gölge (Fake Shadow) bilgilerini saklar.
    /// Küp tıklandığında parçaları aşağı dökülür; sahte gölgeleri ise panoda hep sabit kalır.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class PixelCube : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
    {
        [Header("Izgara Konumu")]
        [SerializeField] private int m_GridX;
        [SerializeField] private int m_GridY;

        [Header("Renk Bilgileri")]
        [SerializeField] private Color m_OriginalColor = Color.white;
        [SerializeField] private Color m_CurrentColor = Color.white;

        [Header("Gölge (Fake Shadow - Her Yönde)")]
        [Tooltip("Arka paneldeki 360 derece çevreleyen gölge quad'ı")]
        [SerializeField] private GameObject m_ShadowObject;
        [Tooltip("3D perspektifte küpün alt zeminindeki gölge quad'ı")]
        [SerializeField] private GameObject m_ShadowBottomObject;
        [Tooltip("Küp patladığında sahte gölgesi de gizlenir; kalan küplerin gölgeleri derinliği tamamlar")]
        [SerializeField] private bool m_KeepShadowPermanent = false;

        private MeshRenderer m_Renderer;
        // Küp gövdesi + (varsa) bacak/ayak gibi alt parçaların renderer'ları.
        // MainCube_Walk modeliyle birlikte küp artık tek mesh değil; renk ve
        // patlama görünürlüğü tüm parçalara birlikte uygulanmalı.
        private MeshRenderer[] m_BodyRenderers;
        // Önbelleğin kurulduğu andaki çocuk sayısı. Prefab'a sonradan parça eklenirse
        // (bacaklar) veya gölge quad'ları oluşturulursa önbellek bayatlar; bu sayı
        // değiştiğinde yeniden toplanır.
        private int m_BodyRenderersChildCount = -1;
        private Collider m_CubeCollider;
        private bool m_IsPopped = false;
        // Panodan ayrılıp kendi bacaklarıyla gemiye yürüyen küp: oyun mantığı için panoda değildir
        // (IsPopped), ama görünür kalır. Resim sıfırlanınca eski yerine dönebilsin diye yeri saklanır.
        private bool m_IsLeaving = false;
        private Vector3 m_HomePosition;
        private Quaternion m_HomeRotation;
        // Yürürken gerçek gölge kapatılır (kumda küpten kopuk lekeler oluşuyordu); geri dönüşte eski haline gelir.
        private UnityEngine.Rendering.ShadowCastingMode[] m_HomeShadowModes;

        private void Awake()
        {
            if (m_Renderer == null) m_Renderer = GetComponent<MeshRenderer>();
            if (m_CubeCollider == null) m_CubeCollider = GetComponent<Collider>();

            if (name.StartsWith("Pixel_"))
            {
                string[] parts = name.Split('_');
                if (parts.Length >= 3 && int.TryParse(parts[1], out int px) && int.TryParse(parts[2], out int py))
                {
                    m_GridX = px;
                    m_GridY = py;
                }
            }

            if (m_CurrentColor == Color.white && m_OriginalColor != Color.white)
            {
                m_CurrentColor = m_OriginalColor;
            }

            if (m_CurrentColor != Color.white || m_OriginalColor != Color.white)
            {
                ApplyColor(m_CurrentColor != Color.white ? m_CurrentColor : m_OriginalColor);
            }

            EnsureShadowReferences();

            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null && !gen.EvaluateShadowMode())
            {
                if (m_ShadowObject != null) m_ShadowObject.SetActive(false);
                if (m_ShadowBottomObject != null) m_ShadowBottomObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (m_CurrentColor != Color.white || m_OriginalColor != Color.white)
            {
                ApplyColor(m_CurrentColor != Color.white ? m_CurrentColor : m_OriginalColor);
            }
        }

        private void OnEnable()
        {
            if (m_CurrentColor != Color.white || m_OriginalColor != Color.white)
            {
                ApplyColor(m_CurrentColor != Color.white ? m_CurrentColor : m_OriginalColor);
            }
        }

        public void EnsureShadowReferences()
        {
            if (m_ShadowObject == null)
            {
                Transform st = transform.Find("CubeShadow");
                if (st != null) m_ShadowObject = st.gameObject;
            }
            if (m_ShadowBottomObject == null)
            {
                Transform sbt = transform.Find("CubeShadow_Bottom");
                if (sbt != null) m_ShadowBottomObject = sbt.gameObject;
            }
        }

        private static MaterialPropertyBlock s_PropertyBlock;
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

        public int GridX => m_GridX;
        public int GridY => m_GridY;
        public Color OriginalColor => m_OriginalColor;
        public Color CurrentColor => m_CurrentColor;
        public GameObject ShadowObject => m_ShadowObject;
        public GameObject ShadowBottomObject => m_ShadowBottomObject;
        public bool IsPopped => m_IsPopped;
        public bool IsLeaving => m_IsLeaving;
        public bool KeepShadowPermanent
        {
            get => m_KeepShadowPermanent;
            set => m_KeepShadowPermanent = value;
        }

        public void Initialize(int x, int y, Color originalColor, float emission = 0f)
        {
            m_GridX = x;
            m_GridY = y;
            m_OriginalColor = originalColor;
            m_CurrentColor = originalColor;
            m_IsPopped = false;
            ApplyColor(originalColor, emission);
        }

        public void SetColor(Color newColor, float emission = 0f)
        {
            m_OriginalColor = newColor;
            m_CurrentColor = newColor;
            ApplyColor(newColor, emission);
        }

        /// <summary>
        /// Küpün gövdesini oluşturan tüm renderer'ları toplar (gölge quad'ları hariç).
        /// Tek parça küpte sadece kök renderer, MainCube_Walk modelinde kök + bacaklar + ayaklar.
        /// </summary>
        private MeshRenderer[] GetBodyRenderers()
        {
            if (m_BodyRenderers != null
                && m_BodyRenderersChildCount == transform.childCount
                && m_BodyRenderers.Length > 0
                && m_BodyRenderers[0] != null)
                return m_BodyRenderers;

            MeshRenderer[] all = GetComponentsInChildren<MeshRenderer>(true);
            var list = new System.Collections.Generic.List<MeshRenderer>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                // Sahte gölge quad'ları gövdeye dahil değil — renkleri ayrı yönetiliyor.
                if (all[i].gameObject.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                list.Add(all[i]);
            }

            m_BodyRenderers = list.ToArray();
            m_BodyRenderersChildCount = transform.childCount;
            return m_BodyRenderers;
        }

        public void ApplyColor(Color color, float emission = 0f)
        {
            m_CurrentColor = color;

            if (m_Renderer == null)
                m_Renderer = GetComponent<MeshRenderer>();

            MeshRenderer[] body = GetBodyRenderers();
            if (body.Length == 0) return;

            if (s_PropertyBlock == null)
                s_PropertyBlock = new MaterialPropertyBlock();

            s_PropertyBlock.Clear();
            s_PropertyBlock.SetColor(BaseColorProp, color);
            s_PropertyBlock.SetColor(ColorProp, color);


            if (emission > 0f)
            {
                s_PropertyBlock.SetColor(EmissionColorProp, color * emission);
            }
            else
            {
                s_PropertyBlock.SetColor(EmissionColorProp, Color.black);
            }

            for (int i = 0; i < body.Length; i++)
            {
                if (body[i] != null) body[i].SetPropertyBlock(s_PropertyBlock);
            }
        }

        public void UpdateColorAdjustments(float brightness, float saturation, float contrast, float emission)
        {
            Color adjusted = AdjustColor(m_OriginalColor, brightness, saturation, contrast);
            ApplyColor(adjusted, emission);
        }

        public static Color AdjustColor(Color col, float brightness, float saturation, float contrast)
        {
            Color.RGBToHSV(col, out float h, out float s, out float v);

            // Saf çarpımsal doygunluk artışı, zaten soluk (düşük S) kaynak renkleri kurtaramıyordu
            // (ör. S=0.18 iken saturation=1.25 ile çarpınca sadece S=0.225 oluyor, hâlâ soluk).
            // "Vibrance" mantığı: S'yi 1'e doğru kaydır, kayma miktarı ne kadar soluksa o kadar
            // büyük olsun — zaten canlı renkler (S zaten 1'e yakın) neredeyse hiç değişmez.
            if (saturation >= 1f)
            {
                float boost = saturation - 1f;
                s = Mathf.Clamp01(s + (1f - s) * boost);
            }
            else
            {
                s = Mathf.Clamp01(s * saturation);
            }

            v = Mathf.Clamp01(v * brightness);

            Color result = Color.HSVToRGB(h, s, v);

            if (!Mathf.Approximately(contrast, 1f))
            {
                result.r = Mathf.Clamp01((result.r - 0.5f) * contrast + 0.5f);
                result.g = Mathf.Clamp01((result.g - 0.5f) * contrast + 0.5f);
                result.b = Mathf.Clamp01((result.b - 0.5f) * contrast + 0.5f);
            }

            result.a = col.a;
            return result;
        }

        #region 🌑 Fake Shadow (Küp Altı Sahte Gölge - Her Yönde)

        public void SetShadowObject(GameObject shadowObj)
        {
            m_ShadowObject = shadowObj;
            m_KeepShadowPermanent = false;
        }

        public void SetShadowObjects(GameObject backShadow, GameObject bottomShadow)
        {
            m_ShadowObject = backShadow;
            m_ShadowBottomObject = bottomShadow;
            m_KeepShadowPermanent = false;
        }

        private static Mesh s_QuadMesh;
        private static Mesh GetOrCreateQuadMesh()
        {
            if (s_QuadMesh != null) return s_QuadMesh;

            Mesh mesh = new Mesh();
            mesh.name = "ShadowQuadMesh";
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            s_QuadMesh = mesh;
            return s_QuadMesh;
        }

        private GameObject CreateShadowQuadObject(string shadowName)
        {
            GameObject quadObj = new GameObject(shadowName);
            quadObj.transform.SetParent(transform, false);

            MeshFilter mf = quadObj.AddComponent<MeshFilter>();
            #if UNITY_EDITOR
            Mesh builtinQuad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            mf.sharedMesh = builtinQuad != null ? builtinQuad : GetOrCreateQuadMesh();
            #else
            mf.sharedMesh = GetOrCreateQuadMesh();
            #endif

            MeshRenderer mr = quadObj.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            return quadObj;
        }

        /// <summary>
        /// Küpün altına yumuşak sahte gölge yerleştirir (Back Quad).
        /// Küp parçalanmamış haldeyken hafif gölgeli durur, küp patlatıldığında ise arkada hiçbir iz kalmaz.
        /// </summary>
        public void EnsureShadow(Material shadowMaterial, Vector2 offset, float scaleMultiplier, Color shadowColor)
        {
            // 1. Arka Duvar / Pano Gölgesi (Back Quad)
            Transform shadowTrans = transform.Find("CubeShadow");
            if (shadowTrans == null)
            {
                m_ShadowObject = CreateShadowQuadObject("CubeShadow");
            }
            else
            {
                m_ShadowObject = shadowTrans.gameObject;
            }

            // Hafif ofsetli ve küpün sınırlarında yumuşak sönümlenen gölge
            m_ShadowObject.transform.localPosition = new Vector3(offset.x, offset.y, 0.52f);
            m_ShadowObject.transform.localRotation = Quaternion.identity;
            m_ShadowObject.transform.localScale = new Vector3(scaleMultiplier, scaleMultiplier, 1f);

            MeshRenderer mr = m_ShadowObject.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                if (shadowMaterial != null) mr.sharedMaterial = shadowMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                if (shadowColor.a > 0.001f)
                {
                    MaterialPropertyBlock spb = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(spb);
                    spb.SetColor("_Color", shadowColor);
                    spb.SetColor("_BaseColor", shadowColor);
                    mr.SetPropertyBlock(spb);
                }
            }

            // 2. Varsa eski CubeShadow_Bottom objesini temizle (Küp patlayınca komşunun boşluğa taşmasını önler)
            Transform bottomTrans = transform.Find("CubeShadow_Bottom");
            if (bottomTrans != null)
            {
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(bottomTrans.gameObject);
                else
                #endif
                    Destroy(bottomTrans.gameObject);
            }
            m_ShadowBottomObject = null;

            m_KeepShadowPermanent = false;
        }

        #endregion

        #region 💥 Tıklama & Patlama (Voxel Burst & Gölge Yönetimi)

        /// <summary>
        /// Küp patlatıldığında veya geri yüklendiğinde görsel durumunu ayarlar.
        /// Parçalandığında küp ve gölgeleri tamamen gizlenir (Arkada hiçbir şey kalmaz!).
        /// Parçalanmamış halinde ise hafif gölgeli ve 3D derinlikli görünür.
        /// </summary>
        private void SetBodyRenderersEnabled(bool enabled)
        {
            MeshRenderer[] body = GetBodyRenderers();
            for (int i = 0; i < body.Length; i++)
            {
                if (body[i] != null) body[i].enabled = enabled;
            }
        }

        /// <summary>
        /// Küp panodan ayrılır ve kendisi yürüyerek gider: oyun mantığı için artık panoda değildir
        /// (IsPopped), ama gövdesi ve bacakları görünür kalır. Tıklanamaz, sahte gölgesi gizlenir.
        /// </summary>
        public void BeginLeaving(bool regenerateContourShadow = true)
        {
            if (m_IsPopped) return;

            m_HomePosition = transform.position;
            m_HomeRotation = transform.rotation;
            m_IsLeaving = true;
            m_IsPopped = true;

            if (m_CubeCollider == null) m_CubeCollider = GetComponent<Collider>();
            if (m_CubeCollider != null) m_CubeCollider.enabled = false;
            EnsureShadowReferences();
            HideShadows();

            MeshRenderer[] body = GetBodyRenderers();
            m_HomeShadowModes = new UnityEngine.Rendering.ShadowCastingMode[body.Length];
            for (int i = 0; i < body.Length; i++)
            {
                if (body[i] == null) continue;
                m_HomeShadowModes[i] = body[i].shadowCastingMode;
                body[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            if (Application.isPlaying && regenerateContourShadow)
            {
                PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
                if (gen != null) gen.RegenerateContourShadowFromLiveCubeState();
            }
        }

        /// <summary>Yürüyerek ayrılan küp gemiye indiğinde gizlenir.</summary>
        public void FinishLeaving()
        {
            gameObject.SetActive(false);
        }

        private void HideShadows()
        {
            // Küpün kendi gölgelerinin tamamı gizlenir (Arkada hiçbir şey kalmaz!)
            if (m_ShadowObject != null) m_ShadowObject.SetActive(false);
            if (m_ShadowBottomObject != null) m_ShadowBottomObject.SetActive(false);

            // Garanti olsun diye çocuk objelerdeki tüm gölge nesnelerini devre dışı bırak
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        public void SetPoppedVisualState(bool popped, bool regenerateContourShadow = true)
        {
            m_IsPopped = popped;

            if (m_Renderer == null) m_Renderer = GetComponent<MeshRenderer>();
            if (m_CubeCollider == null) m_CubeCollider = GetComponent<Collider>();
            EnsureShadowReferences();

            if (popped)
            {
                // Sadece küpün kendisini (gövde + bacaklar) gizle ve tıklanamaz yap
                SetBodyRenderersEnabled(false);
                if (m_CubeCollider != null) m_CubeCollider.enabled = false;
                HideShadows();
            }
            else
            {
                // Yeniden görünür yap (Reset). Yürüyerek ayrılmış küp eski yerine döner.
                if (m_IsLeaving)
                {
                    m_IsLeaving = false;
                    transform.SetPositionAndRotation(m_HomePosition, m_HomeRotation);
                    MeshRenderer[] body = GetBodyRenderers();
                    if (m_HomeShadowModes != null)
                    {
                        for (int i = 0; i < body.Length && i < m_HomeShadowModes.Length; i++)
                        {
                            if (body[i] != null) body[i].shadowCastingMode = m_HomeShadowModes[i];
                        }
                    }
                }
                SetBodyRenderersEnabled(true);
                if (m_CubeCollider != null) m_CubeCollider.enabled = true;
                if (m_ShadowObject != null) m_ShadowObject.SetActive(true);
            }

            // Kontur gölgesi de küplerle birlikte parça parça küçülsün/geri büyüsün diye canlı yeniden üret.
            // Toplu koparmada (kargo treni) çağıran taraf bunu tek seferde yapar.
            if (Application.isPlaying && regenerateContourShadow)
            {
                PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
                if (gen != null) gen.RegenerateContourShadowFromLiveCubeState();
            }
        }

        /// <summary>
        /// Küpü kendi renginde 3D mini voksel partiküllerine ayırarak patlatır.
        /// Parçalar aşağı doğru dökülür, gölgesi ise panoda sabit kalır.
        /// </summary>
        public void BurstAndDestroy()
        {
            // Edit Mode'da (Play Mode değilken) sahne tasarımı yaparken küplerin silinmesini kesinlikle engelle!
            if (!Application.isPlaying) return;
            if (m_IsPopped || !gameObject.activeSelf) return;

            // Gemi sahnesinde küpler elle patlatılmaz; gemi yanaşınca kargo treniyle kendisi çeker.
            if (ShipDispatcher.Instance != null) return;

            // 1. Kendi renginde 3D mini vokseller aşağıya doğru dökülsün
            if (VoxelParticleManager.Instance != null)
            {
                VoxelParticleManager.Instance.SpawnVoxelBurst(transform.position, transform.lossyScale, m_CurrentColor);
            }

            // 3. Etkileşim yöneticisine bildir
            PixelCubeInteraction interaction = PixelCubeInteraction.Instance != null
                ? PixelCubeInteraction.Instance
                : Object.FindFirstObjectByType<PixelCubeInteraction>();

            if (interaction != null)
            {
                interaction.RegisterPoppedCube(this);
            }
            else
            {
                SetPoppedVisualState(true);
            }
        }

        // Unity UI EventSystem tıklandığında
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Application.isPlaying) return;
            BurstAndDestroy();
        }

        // Unity UI EventSystem basıldığında
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Application.isPlaying) return;
            BurstAndDestroy();
        }

        // Klasik Unity Physics tıklaması fallback
        private void OnMouseDown()
        {
            if (!Application.isPlaying) return;
            BurstAndDestroy();
        }

        #endregion
    }
}
