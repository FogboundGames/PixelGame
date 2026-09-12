using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelGame
{
    /// <summary>
    /// Patlayan küpler için 3D mini voksel partikül efektlerini ve
    /// tatmin edici tıklama seslerini (prosedürel ses) yöneten merkezi sistem.
    /// Tıklanan küpün parçalara ayrılarak yerçekimiyle aşağıya doğru dökülmesini (spill/cascade) sağlar.
    /// Kamera, çerçeve ve diğer pikseller tamamen sabit kalır.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class VoxelParticleManager : MonoBehaviour
    {
        private static VoxelParticleManager s_Instance;
        public static VoxelParticleManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = Object.FindFirstObjectByType<VoxelParticleManager>();
                    if (s_Instance == null)
                    {
                        GameObject go = new GameObject("[VoxelParticleManager]");
                        s_Instance = go.AddComponent<VoxelParticleManager>();
                    }
                }
                return s_Instance;
            }
        }

        [Header("Partikül Özellikleri")]
        [SerializeField] private Mesh m_CubeMesh;
        [SerializeField] private Material m_ParticleMaterial;

        [Tooltip("Her parçalanan küpün bölüneceği ızgara sayısı (3 = 3x3x2 = 18 adet 3D mini voksel parçası)")]
        [SerializeField] [Range(2, 4)] private int m_SubGridDivision = 3;

        [Tooltip("Parçaların aşağıya doğru dökülmesini sağlayan yerçekimi katsayısı")]
        [SerializeField] [Range(1.0f, 6.0f)] private float m_GravityModifier = 3.0f;

        [Tooltip("Parçaların dökülme ve havada kalma süresi (saniye)")]
        [SerializeField] [Range(0.4f, 2.0f)] private float m_ParticleLifetime = 0.95f;

        [Tooltip("Parçalanma anında küp parçalarının ilk dışa saçılma ve hafif yukarı sıçrama kuvveti")]
        [SerializeField] [Range(0.5f, 4.0f)] private float m_ScatterForce = 1.5f;

        [Tooltip("Parçaların dökülürken 3 boyutlu olarak takla atarak dönmesi")]
        [SerializeField] private bool m_EnableTumbling = true;

        [Tooltip("Parçalanma anında partikül sisteminden aşağıya dökülen eski partiküller (yeni iki aşamalı rafta birikme ve vagona akma sistemi aktifken çakışmaması için varsayılan kapalıdır)")]
        [SerializeField] private bool m_EnableFallingParticles = false;

        [Header("Ses Efekti")]
        [SerializeField] private bool m_EnablePopSound = true;
        [SerializeField] [Range(0f, 1f)] private float m_SoundVolume = 0.65f;

        private ParticleSystem m_ParticleSystem;
        private AudioSource m_AudioSource;
        private AudioClip m_PopAudioClip;
        private System.Random m_Rnd = new System.Random();

        #if UNITY_EDITOR
        private double m_LastEditorTime;
        #endif

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;

            SetupParticleSystem();
            SetupAudio();
        }

        private void OnEnable()
        {
            s_Instance = this;
            SetupParticleSystem();

            #if UNITY_EDITOR
            m_LastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= UpdateEditorSimulation;
            EditorApplication.update += UpdateEditorSimulation;
            #endif
        }

        private void OnDisable()
        {
            #if UNITY_EDITOR
            EditorApplication.update -= UpdateEditorSimulation;
            #endif
        }

        private void OnValidate()
        {
            if (m_ParticleSystem != null)
            {
                ApplyParticleSystemSettings();
            }
        }

        #if UNITY_EDITOR
        private void UpdateEditorSimulation()
        {
            // Edit Mode'da (Play Mode değilken) SceneView'da tıklanan küplerin parçalarının dökülebilmesi için
            if (Application.isPlaying || m_ParticleSystem == null) return;

            double currentTime = EditorApplication.timeSinceStartup;
            float dt = (float)(currentTime - m_LastEditorTime);
            m_LastEditorTime = currentTime;

            if (dt > 0.001f && dt < 0.1f && m_ParticleSystem.particleCount > 0)
            {
                m_ParticleSystem.Simulate(dt, false, false, true);
                SceneView.RepaintAll();
            }
        }
        #endif

        private void SetupParticleSystem()
        {
            m_ParticleSystem = GetComponent<ParticleSystem>();
            if (m_ParticleSystem == null)
            {
                m_ParticleSystem = gameObject.AddComponent<ParticleSystem>();
            }

            // 1. 3D Birim Küp mesh'i hazırla (Normal, UV ve Beyaz Renk içeren)
            if (m_CubeMesh == null)
            {
                m_CubeMesh = CreateUnitCubeMesh();
            }

            // 2. Materyal hazırla (URP uyumlu Particle Unlit veya Unlit)
            if (m_ParticleMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                m_ParticleMaterial = new Material(shader);
                m_ParticleMaterial.name = "VoxelParticle_Mat";
                m_ParticleMaterial.enableInstancing = true;
            }

            // 3. Renderer ayarları (3D Küp Mesh Modu)
            ParticleSystemRenderer psRenderer = GetComponent<ParticleSystemRenderer>();
            psRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            psRenderer.mesh = m_CubeMesh;
            psRenderer.material = m_ParticleMaterial;
            psRenderer.alignment = ParticleSystemRenderSpace.World;
            psRenderer.enableGPUInstancing = true;

            ApplyParticleSystemSettings();
        }

        private void ApplyParticleSystemSettings()
        {
            if (m_ParticleSystem == null) return;

            // 4. Main modül ayarları (Yerçekimi ile aşağı dökülme aktif!)
            var main = m_ParticleSystem.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 8000;
            main.gravityModifier = m_GravityModifier; // Güçlü yerçekimi -> parçalar aşağı akar!
            main.startSpeed = 0f;                    // Hız her parçaya EmitParams ile özel verilir
            main.startRotation3D = true;              // 3D takla atma desteği

            // 5. Size Over Lifetime (Dökülürken tam boyutu koru, en son aşağıda küçülerek yok ol)
            var sizeOverLifetime = m_ParticleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 1f);       // Başlangıçta tam boyut
            curve.AddKey(0.65f, 0.95f);  // Havada dökülürken %65 süre boyunca neredeyse tam boyut
            curve.AddKey(1f, 0f);        // En son aşağıya ulaştığında küçülerek zarifçe kaybol
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            // 6. Emission kapalı (Sadece Emit ile çağıracağız)
            var emission = m_ParticleSystem.emission;
            emission.enabled = false;
        }

        private void SetupAudio()
        {
            m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource == null)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
            }

            m_AudioSource.playOnAwake = false;
            m_AudioSource.spatialBlend = 0f; // 2D net ses

            // Prosedürel tatmin edici "pop / çıt" sesi sentezle
            m_PopAudioClip = CreateProceduralPopClip();
            m_AudioSource.clip = m_PopAudioClip;
        }

        /// <summary>
        /// Küp tıklandığında kendi renginde 3D mini voksellere bölünür ve
        /// yerçekimiyle tatmin edici bir şekilde AŞAĞIYA DOĞRU DÖKÜLÜR.
        /// Çerçeve ve diğer küpler tamamen sabit kalır.
        /// </summary>
        public void SpawnVoxelBurst(Vector3 position, Vector3 cubeScale, Color cubeColor, int customCount = -1)
        {
            PlayPopSound();

            if (!m_EnableFallingParticles) return;

            if (m_ParticleSystem == null)
            {
                SetupParticleSystem();
                if (m_ParticleSystem == null) return;
            }

            // Gerekirse güncel yerçekimi katsayısını kontrol et
            var main = m_ParticleSystem.main;
            if (main.gravityModifier.constant != m_GravityModifier)
            {
                main.gravityModifier = m_GravityModifier;
            }

            int gridDim = Mathf.Max(2, m_SubGridDivision);
            int depthDim = 2; // Ön ve arka 2 katman -> 3x3x2 = 18 adet mini voksel
            float subSizeX = cubeScale.x / gridDim;
            float subSizeY = cubeScale.y / gridDim;
            float subSizeZ = (cubeScale.z > 0.001f ? cubeScale.z : cubeScale.x) / depthDim;
            float subSize = Mathf.Min(subSizeX, subSizeY) * 0.96f;

            float halfX = (gridDim - 1) * 0.5f;
            float halfY = (gridDim - 1) * 0.5f;
            float halfZ = (depthDim - 1) * 0.5f;

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();

            for (int gx = 0; gx < gridDim; gx++)
            {
                for (int gy = 0; gy < gridDim; gy++)
                {
                    for (int gz = 0; gz < depthDim; gz++)
                    {
                        // 1. Küpün kendi içindeki göreceli voksel konumu
                        Vector3 localOffset = new Vector3(
                            (gx - halfX) * subSizeX,
                            (gy - halfY) * subSizeY,
                            (gz - halfZ) * subSizeZ
                        );

                        // 2. Parçalara ayrılma ve dökülme hızları:
                        // X: Merkezden hafifçe sağa/sola açılma
                        float spreadX = (gx - halfX) * 0.9f + Random.Range(-0.35f, 0.35f);
                        // Y: Hafif yukarı yaylanma/zıplama (ark çizip yerçekimiyle aşağı dökülür)
                        float popY = Random.Range(0.2f, 1.8f);
                        // Z: Kameraya doğru hafifçe öne çıkma (arka plana veya yan küplere batmadan önden aksın)
                        float popZ = -Random.Range(0.5f, 1.6f);

                        Vector3 initialVelocity = new Vector3(
                            spreadX * m_ScatterForce,
                            popY * m_ScatterForce,
                            popZ * m_ScatterForce * 0.6f
                        );

                        // 3. Renk varyasyonu: Her mini vokselin tonunda %8 hafif parlaklık farkı
                        // Bu sayede tek renk küpler bile ayrıştığında tek tek bloklar halinde net görünür
                        float brightnessVar = Random.Range(0.92f, 1.08f);
                        Color voxelColor = new Color(
                            Mathf.Clamp01(cubeColor.r * brightnessVar),
                            Mathf.Clamp01(cubeColor.g * brightnessVar),
                            Mathf.Clamp01(cubeColor.b * brightnessVar),
                            cubeColor.a
                        );

                        emitParams.position = position + localOffset;
                        emitParams.velocity = initialVelocity;
                        emitParams.startColor = voxelColor;
                        emitParams.startSize = subSize;
                        emitParams.startLifetime = m_ParticleLifetime * Random.Range(0.85f, 1.15f);

                        // 4. Takla atma ve dönme
                        if (m_EnableTumbling)
                        {
                            emitParams.rotation3D = new Vector3(
                                Random.Range(0f, 360f),
                                Random.Range(0f, 360f),
                                Random.Range(0f, 360f)
                            );
                            emitParams.angularVelocity3D = new Vector3(
                                Random.Range(-300f, 300f),
                                Random.Range(-300f, 300f),
                                Random.Range(-300f, 300f)
                            );
                        }
                        else
                        {
                            emitParams.rotation3D = Vector3.zero;
                            emitParams.angularVelocity3D = Vector3.zero;
                        }

                        m_ParticleSystem.Emit(emitParams, 1);
                    }
                }
            }
        }

        private void PlayPopSound()
        {
            if (!m_EnablePopSound || m_AudioSource == null || m_PopAudioClip == null) return;

            // Her tıklamada rastgele ton değişimi (tatmin edici klik)
            m_AudioSource.pitch = Random.Range(0.92f, 1.25f);
            m_AudioSource.PlayOneShot(m_PopAudioClip, m_SoundVolume);
        }

        /// <summary>
        /// Prosedürel olarak hoş ve yumuşak bir 8-bit pop/çıt ses efekti üretir.
        /// </summary>
        private AudioClip CreateProceduralPopClip()
        {
            int sampleRate = 44100;
            float duration = 0.085f; // 85 ms kısa ve net
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float startFreq = 850f;
            float endFreq = 180f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;

                float currentFreq = Mathf.Lerp(startFreq, endFreq, Mathf.Pow(t, 0.6f));
                float phase = 2f * Mathf.PI * currentFreq * (i / (float)sampleRate);

                float envelope = Mathf.Exp(-t * 14f);
                float noise = ((float)m_Rnd.NextDouble() * 2f - 1f) * (1f - t) * 0.12f;

                samples[i] = (Mathf.Sin(phase) + noise) * envelope;
            }

            AudioClip clip = AudioClip.Create("ProceduralVoxelPop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Partiküllerde render edilmek üzere standart 3D Birim Küp (Unit Cube) mesh'i oluşturur.
        /// Vertex Color (beyaz), UV ve Normalleri tamdır.
        /// </summary>
        private static Mesh CreateUnitCubeMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "UnitVoxelMesh";

            float s = 0.5f;
            Vector3[] vertices = new Vector3[]
            {
                // Ön yüz
                new Vector3(-s, -s, -s), new Vector3(s, -s, -s), new Vector3(s, s, -s), new Vector3(-s, s, -s),
                // Arka yüz
                new Vector3(s, -s, s), new Vector3(-s, -s, s), new Vector3(-s, s, s), new Vector3(s, s, s),
                // Sol yüz
                new Vector3(-s, -s, s), new Vector3(-s, -s, -s), new Vector3(-s, s, -s), new Vector3(-s, s, s),
                // Sağ yüz
                new Vector3(s, -s, -s), new Vector3(s, -s, s), new Vector3(s, s, s), new Vector3(s, s, -s),
                // Üst yüz
                new Vector3(-s, s, -s), new Vector3(s, s, -s), new Vector3(s, s, s), new Vector3(-s, s, s),
                // Alt yüz
                new Vector3(-s, -s, s), new Vector3(s, -s, s), new Vector3(s, -s, -s), new Vector3(-s, -s, -s)
            };

            int[] triangles = new int[]
            {
                0, 2, 1, 0, 3, 2,
                4, 6, 5, 4, 7, 6,
                8, 10, 9, 8, 11, 10,
                12, 14, 13, 12, 15, 14,
                16, 18, 17, 16, 19, 18,
                20, 22, 21, 20, 23, 22
            };

            Vector2[] uvs = new Vector2[24];
            Color[] colors = new Color[24];
            for (int i = 0; i < 6; i++)
            {
                int b = i * 4;
                uvs[b + 0] = new Vector2(0f, 0f);
                uvs[b + 1] = new Vector2(1f, 0f);
                uvs[b + 2] = new Vector2(1f, 1f);
                uvs[b + 3] = new Vector2(0f, 1f);

                colors[b + 0] = Color.white;
                colors[b + 1] = Color.white;
                colors[b + 2] = Color.white;
                colors[b + 3] = Color.white;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
