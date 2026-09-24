using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

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
        private Collider m_CubeCollider;
        private bool m_IsPopped = false;

        private void Awake()
        {
            if (m_Renderer == null) m_Renderer = GetComponent<MeshRenderer>();
            if (m_CubeCollider == null) m_CubeCollider = GetComponent<Collider>();

            if (m_GridX == 0 && m_GridY == 0 && name.StartsWith("Pixel_"))
            {
                string[] parts = name.Split('_');
                if (parts.Length >= 3 && int.TryParse(parts[1], out int px) && int.TryParse(parts[2], out int py))
                {
                    m_GridX = px;
                    m_GridY = py;
                }
            }

            EnsureShadowReferences();

            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null && !gen.EvaluateShadowMode())
            {
                if (m_ShadowObject != null) m_ShadowObject.SetActive(false);
                if (m_ShadowBottomObject != null) m_ShadowBottomObject.SetActive(false);
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

        public int GridX => m_GridX;
        public int GridY => m_GridY;
        public Color OriginalColor => m_OriginalColor;
        public Color CurrentColor => m_CurrentColor;
        public GameObject ShadowObject => m_ShadowObject;
        public GameObject ShadowBottomObject => m_ShadowBottomObject;
        public bool IsPopped => m_IsPopped;
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

        public void ApplyColor(Color color, float emission = 0f)
        {
            m_CurrentColor = color;

            if (m_Renderer == null)
                m_Renderer = GetComponent<MeshRenderer>();

            if (m_Renderer == null) return;

            if (s_PropertyBlock == null)
                s_PropertyBlock = new MaterialPropertyBlock();

            m_Renderer.GetPropertyBlock(s_PropertyBlock);
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

            m_Renderer.SetPropertyBlock(s_PropertyBlock);
        }

        public void UpdateColorAdjustments(float brightness, float saturation, float contrast, float emission)
        {
            Color adjusted = AdjustColor(m_OriginalColor, brightness, saturation, contrast);
            ApplyColor(adjusted, emission);
        }

        public static Color AdjustColor(Color col, float brightness, float saturation, float contrast)
        {
            Color.RGBToHSV(col, out float h, out float s, out float v);

            s = Mathf.Clamp01(s * saturation);
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
        public void SetPoppedVisualState(bool popped)
        {
            m_IsPopped = popped;

            if (m_Renderer == null) m_Renderer = GetComponent<MeshRenderer>();
            if (m_CubeCollider == null) m_CubeCollider = GetComponent<Collider>();
            EnsureShadowReferences();

            if (popped)
            {
                // Sadece küpün kendisini gizle ve tıklanamaz yap
                if (m_Renderer != null) m_Renderer.enabled = false;
                if (m_CubeCollider != null) m_CubeCollider.enabled = false;

                // Küp patladığında kendi gölgelerinin tamamı gizlenir (Arkada hiçbir şey kalmaz!)
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
            else
            {
                // Yeniden görünür yap (Reset)
                if (m_Renderer != null) m_Renderer.enabled = true;
                if (m_CubeCollider != null) m_CubeCollider.enabled = true;
                if (m_ShadowObject != null) m_ShadowObject.SetActive(true);
            }
        }

        /// <summary>
        /// Küp ortada/içte kilitliyken tıklandığında hafifçe sallanarak kilitli olduğunu hissettirir.
        /// </summary>
        public void PlayBlockedWobble()
        {
            if (!Application.isPlaying || m_IsPopped) return;
            transform.DOKill(true);
            transform.DOShakeRotation(0.25f, new Vector3(0f, 0f, 12f), 10, 90f, true);
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

            // 0. Gemi veya Kamyon kuralı: rengine uyan bir gemi/kamyon slotta yoksa veya küp dışta değilse patlamaz.
            ShipDispatcher shipDispatcher = ShipDispatcher.Instance;
            if (shipDispatcher != null && !shipDispatcher.CanPop(this)) return;

            TruckDispatcher truckDispatcher = TruckDispatcher.Instance;
            if (truckDispatcher != null && !truckDispatcher.CanPop(m_CurrentColor)) return;

            // 1. Kendi renginde 3D mini vokseller aşağıya doğru dökülsün
            if (VoxelParticleManager.Instance != null)
            {
                VoxelParticleManager.Instance.SpawnVoxelBurst(transform.position, transform.lossyScale, m_CurrentColor);
            }

            // 2. Küpü rengine uyan gemiye veya kamyona yükle
            if (shipDispatcher != null)
            {
                shipDispatcher.NotifyCubePopped(m_CurrentColor, transform.position, m_CurrentColor, transform.lossyScale, transform.rotation);
            }
            else if (truckDispatcher != null)
            {
                truckDispatcher.NotifyCubePopped(m_CurrentColor, transform.position, m_CurrentColor, transform.lossyScale, transform.rotation);
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
