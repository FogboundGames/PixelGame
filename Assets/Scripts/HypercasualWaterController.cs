using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame
{
    /// <summary>
    /// Hypercasual Su Görseli Yöneticisi (Water Visual Controller).
    /// Arka plan su görselindeki (BeachBackground_Clean) dalgalanmayı, güneş parıltısını (caustic shimmer)
    /// ve gemiler/nesneler hareket ettiğinde oluşan dinamik su dalgalarını yönetir.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Hypercasual Water Controller")]
    public class HypercasualWaterController : MonoBehaviour
    {
        private static HypercasualWaterController s_Instance;
        public static HypercasualWaterController Instance => s_Instance;

        [Header("🌊 Referanslar")]
        [SerializeField] private RawImage m_RawImage;
        [SerializeField] private Camera m_WorldCamera;

        [Header("✨ Dalga & Parıltı Ayarları")]
        [SerializeField] [Range(0.2f, 4.0f)] private float m_WaveSpeed = 1.35f;
        [SerializeField] [Range(5.0f, 35.0f)] private float m_WaveFrequency = 16.0f;
        [SerializeField] [Range(0.001f, 0.025f)] private float m_WaveAmplitude = 0.0065f;
        [SerializeField] [Range(0.0f, 0.6f)] private float m_ShimmerIntensity = 0.22f;

        private Material m_WaterMaterial;
        private static readonly int WaveSpeedProp = Shader.PropertyToID("_WaveSpeed");
        private static readonly int WaveFrequencyProp = Shader.PropertyToID("_WaveFrequency");
        private static readonly int WaveAmplitudeProp = Shader.PropertyToID("_WaveAmplitude");
        private static readonly int ShimmerIntensityProp = Shader.PropertyToID("_ShimmerIntensity");

        private static readonly int[] RippleProps = new int[]
        {
            Shader.PropertyToID("_Ripple0"),
            Shader.PropertyToID("_Ripple1"),
            Shader.PropertyToID("_Ripple2"),
            Shader.PropertyToID("_Ripple3")
        };

        private struct ActiveRipple
        {
            public Vector2 centerUV;
            public float radius;
            public float strength;
            public float maxRadius;
            public float expandSpeed;
            public float fadeSpeed;
        }

        private readonly List<ActiveRipple> m_Ripples = new List<ActiveRipple>();
        private float m_LastWakeTime = 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            if (s_Instance == null)
            {
                var rawImages = Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None);
                foreach (var img in rawImages)
                {
                    if (img != null && (img.gameObject.name.Contains("Background") || (img.texture != null && img.texture.name.Contains("BeachBackground"))))
                    {
                        var ctrl = img.GetComponent<HypercasualWaterController>();
                        if (ctrl == null)
                        {
                            ctrl = img.gameObject.AddComponent<HypercasualWaterController>();
                        }
                        ctrl.EnsureSetup();
                        break;
                    }
                }
            }
            else
            {
                s_Instance.EnsureSetup();
            }
        }

        private void Awake()
        {
            s_Instance = this;
            EnsureSetup();
        }

        private void OnEnable()
        {
            s_Instance = this;
            EnsureSetup();
        }

        public void EnsureSetup()
        {
            if (m_RawImage == null)
            {
                m_RawImage = GetComponent<RawImage>();
                if (m_RawImage == null)
                {
                    var rawImages = Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None);
                    foreach (var img in rawImages)
                    {
                        if (img != null && (img.gameObject.name.Contains("Background") || (img.texture != null && img.texture.name.Contains("BeachBackground"))))
                        {
                            m_RawImage = img;
                            break;
                        }
                    }
                }
            }

            if (m_WorldCamera == null)
            {
                m_WorldCamera = Camera.main;
                if (m_WorldCamera == null)
                {
                    m_WorldCamera = Object.FindFirstObjectByType<Camera>();
                }
            }

            EnsureMaterial();
        }

        public Material EnsureMaterial()
        {
            if (m_RawImage == null) return null;

            Shader waterShader = Shader.Find("PixelGame/HypercasualWaterBackground");
            if (waterShader == null) return null;

            if (m_RawImage.material == null || m_RawImage.material.shader != waterShader)
            {
                m_WaterMaterial = new Material(waterShader);
                m_WaterMaterial.name = "HypercasualWater_RuntimeMat";
                m_RawImage.material = m_WaterMaterial;
            }
            else
            {
                m_WaterMaterial = m_RawImage.material;
            }

            UpdateMaterialParameters();
            return m_WaterMaterial;
        }

        private void Update()
        {
            if (m_WaterMaterial == null)
            {
                EnsureSetup();
                if (m_WaterMaterial == null) return;
            }

            UpdateMaterialParameters();
            UpdateRipples();

            if (Application.isPlaying)
            {
                TrackMovingShips();
            }
        }

        private void UpdateMaterialParameters()
        {
            if (m_WaterMaterial == null) return;

            m_WaterMaterial.SetFloat(WaveSpeedProp, m_WaveSpeed);
            m_WaterMaterial.SetFloat(WaveFrequencyProp, m_WaveFrequency);
            m_WaterMaterial.SetFloat(WaveAmplitudeProp, m_WaveAmplitude);
            m_WaterMaterial.SetFloat(ShimmerIntensityProp, m_ShimmerIntensity);
        }

        /// <summary>
        /// Dünya koordinatındaki bir noktada (örn. gemi yanaştığında, küp düştüğünde) su üzerinde dalgalanma halkası başlatır.
        /// </summary>
        public static void TriggerWaterRipple(Vector3 worldPos, float strength = 1.0f, float maxRadius = 0.22f)
        {
            if (s_Instance != null)
            {
                s_Instance.AddRippleInternal(worldPos, strength, maxRadius);
            }
        }

        public void AddRippleInternal(Vector3 worldPos, float strength, float maxRadius)
        {
            if (m_WorldCamera == null)
            {
                m_WorldCamera = Camera.main;
                if (m_WorldCamera == null) return;
            }

            Vector3 viewportPos = m_WorldCamera.WorldToViewportPoint(worldPos);
            Vector2 uv = new Vector2(viewportPos.x, viewportPos.y);

            // Sadece su bölgesinde olan dalgaları kabul et (ekranın alt yarısı)
            if (uv.y > 0.48f) return;

            if (m_Ripples.Count >= 4)
            {
                m_Ripples.RemoveAt(0);
            }

            ActiveRipple r = new ActiveRipple
            {
                centerUV = uv,
                radius = 0.01f,
                strength = Mathf.Clamp01(strength),
                maxRadius = maxRadius,
                expandSpeed = 0.22f,
                fadeSpeed = 0.85f
            };
            m_Ripples.Add(r);
        }

        private void UpdateRipples()
        {
            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;

            for (int i = m_Ripples.Count - 1; i >= 0; i--)
            {
                ActiveRipple r = m_Ripples[i];
                r.radius += r.expandSpeed * dt;
                r.strength -= r.fadeSpeed * dt;

                if (r.strength <= 0.01f || r.radius >= r.maxRadius)
                {
                    m_Ripples.RemoveAt(i);
                }
                else
                {
                    m_Ripples[i] = r;
                }
            }

            // Shader'a gönder
            for (int i = 0; i < 4; i++)
            {
                if (i < m_Ripples.Count)
                {
                    ActiveRipple r = m_Ripples[i];
                    m_WaterMaterial.SetVector(RippleProps[i], new Vector4(r.centerUV.x, r.centerUV.y, r.radius, r.strength));
                }
                else
                {
                    m_WaterMaterial.SetVector(RippleProps[i], Vector4.zero);
                }
            }
        }

        /// <summary>
        /// Sahnedeki hareket eden gemileri takip ederek arkalarında periyodik su izi dalgaları oluşturur.
        /// </summary>
        private void TrackMovingShips()
        {
            if (Time.time - m_LastWakeTime < 0.14f) return;

            var ships = Object.FindObjectsByType<ShipController>(FindObjectsSortMode.None);
            bool spawnedAny = false;

            foreach (var ship in ships)
            {
                if (ship != null && ship.IsMoving)
                {
                    AddRippleInternal(ship.transform.position, 0.45f, 0.14f);
                    spawnedAny = true;
                }
            }

            if (spawnedAny)
            {
                m_LastWakeTime = Time.time;
            }
        }
    }
}
