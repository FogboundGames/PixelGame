using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Hypercasual Puzzle oyun deneyimini zenginleştiren merkezi geri bildirim yöneticisi:
    /// ✨ Küçük görsel parıltı (Sparkle / Star pop FX)
    /// 💦 Canlı su sıçraması (Water splash droplets & foam ripples)
    /// 🔊 Tatmin edici hiper-kaliteli prosedürel ses efektleri (Melodik küp dizilimi, su bloop, iskele kütüğü, gemi düdüğü)
    /// 📳 Mobil cihazlar için hafif, tatlı dokunsal titreşim (Light & Medium haptics)
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Hypercasual Feedback Manager")]
    public class HypercasualFeedbackManager : MonoBehaviour
    {
        private static HypercasualFeedbackManager s_Instance;
        public static HypercasualFeedbackManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = UnityEngine.Object.FindFirstObjectByType<HypercasualFeedbackManager>();
                    if (s_Instance == null)
                    {
                        GameObject go = new GameObject("[HypercasualFeedbackManager]");
                        s_Instance = go.AddComponent<HypercasualFeedbackManager>();
                    }
                }
                return s_Instance;
            }
        }

        [Header("✨ Görsel Efektler")]
        [SerializeField] private bool m_EnableSparkles = true;
        [SerializeField] private bool m_EnableWaterSplash = true;

        [Header("🔊 Ses Ayarları")]
        [SerializeField] private bool m_EnableSound = true;
        [Range(0f, 1f)] [SerializeField] private float m_MasterVolume = 0.75f;

        [Header("📳 Mobil Titreşim (Haptics)")]
        [SerializeField] private bool m_EnableHaptics = true;
        [SerializeField] private bool m_HapticsOnlyOnMobile = true;

        // Ses kaynakları ve klipleri
        private AudioSource m_ChimeSource;
        private AudioSource m_FxSource;
        private AudioClip[] m_ChimeClips;
        private AudioClip m_SplashClip;
        private AudioClip m_DockClip;
        private AudioClip m_DepartClip;
        private AudioClip m_DenialClip;

        // Partikül sistemleri
        private ParticleSystem m_SparklePS;
        private ParticleSystem m_SplashPS;
        private Material m_SparkleMaterial;
        private Material m_SplashMaterial;

        // Haptic zamanlayıcı (Aşırı sık titreşimi engellemek için)
        private float m_LastHapticTime = -1f;
        private const float MinHapticInterval = 0.040f; // En az 40 ms aralık

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }
            s_Instance = this;

            InitializeAudio();
            InitializeParticles();
        }

        private void OnEnable()
        {
            s_Instance = this;
            InitializeAudio();
            InitializeParticles();
        }

        #region 🔊 Tatmin Edici Prosedürel Ses Sistemi

        private void InitializeAudio()
        {
            if (m_ChimeSource == null)
            {
                m_ChimeSource = gameObject.AddComponent<AudioSource>();
                m_ChimeSource.playOnAwake = false;
                m_ChimeSource.spatialBlend = 0f; // 2D berrak ses
            }

            if (m_FxSource == null)
            {
                m_FxSource = gameObject.AddComponent<AudioSource>();
                m_FxSource.playOnAwake = false;
                m_FxSource.spatialBlend = 0f;
            }

            // Müzikal pentatonik küp sesleri (C5 - D5 - E5 - G5 - A5 - C6 - D6 - E6)
            if (m_ChimeClips == null || m_ChimeClips.Length == 0)
            {
                float[] freqs = new float[] { 523.25f, 587.33f, 659.25f, 783.99f, 880.00f, 1046.50f, 1174.66f, 1318.51f };
                m_ChimeClips = new AudioClip[freqs.Length];
                for (int i = 0; i < freqs.Length; i++)
                {
                    m_ChimeClips[i] = CreateProceduralChimeClip(freqs[i]);
                }
            }

            if (m_SplashClip == null) m_SplashClip = CreateProceduralSplashClip();
            if (m_DockClip == null) m_DockClip = CreateProceduralDockClip();
            if (m_DepartClip == null) m_DepartClip = CreateProceduralDepartClip();
            if (m_DenialClip == null) m_DenialClip = CreateProceduralDenialClip();
        }

        /// <summary>
        /// Küpün gemiye bindiğinde çıkardığı tatlı, parlak marimba/kristal plink sesi.
        /// </summary>
        private AudioClip CreateProceduralChimeClip(float fundamentalFreq)
        {
            int sampleRate = 44100;
            float duration = 0.12f; // 120 ms
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float progress = t / duration;

                // 1. Temel dalga ve kristal harmonikleri
                float p1 = 2f * Mathf.PI * fundamentalFreq * t;
                float p2 = 2f * Mathf.PI * (fundamentalFreq * 2.01f) * t;
                float p3 = 2f * Mathf.PI * (fundamentalFreq * 3.02f) * t;
                float tone = Mathf.Sin(p1) * 0.65f + Mathf.Sin(p2) * 0.25f + Mathf.Sin(p3) * 0.10f;

                // 2. İlk 5 ms perküsyif mallet 'tık' vuruşu
                float click = 0f;
                if (t < 0.005f)
                {
                    click = Mathf.Sin(2f * Mathf.PI * 3200f * t) * (1f - t / 0.005f) * 0.35f;
                }

                // 3. Üstel tatlı sönümlenme
                float envelope = Mathf.Exp(-progress * 16f);
                samples[i] = Mathf.Clamp((tone + click) * envelope, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create($"Chime_{Mathf.RoundToInt(fundamentalFreq)}", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Ferahlatıcı su damlası ve sıçrama bloop sesi.
        /// </summary>
        private AudioClip CreateProceduralSplashClip()
        {
            int sampleRate = 44100;
            float duration = 0.16f;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];
            var rnd = new System.Random(1337);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float progress = t / duration;

                // Frekans düşüşü (720Hz -> 180Hz baloncuğun suya batışı)
                float freq = Mathf.Lerp(720f, 180f, Mathf.Pow(progress, 0.45f));
                float bubble = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.65f;

                // İlk 40 ms su serpintisi gürültüsü
                float splashNoise = 0f;
                if (progress < 0.25f)
                {
                    splashNoise = ((float)rnd.NextDouble() * 2f - 1f) * (1f - progress / 0.25f) * 0.35f;
                }

                float envelope = Mathf.Exp(-progress * 13f);
                samples[i] = Mathf.Clamp((bubble + splashNoise) * envelope, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("WaterSplash_FX", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Geminin iskeleye yanaştığında çıkan tok, tatmin edici ahşap kütüğü darbesi.
        /// </summary>
        private AudioClip CreateProceduralDockClip()
        {
            int sampleRate = 44100;
            float duration = 0.18f;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float progress = t / duration;

                float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 65f, progress) * t) * 0.65f;
                float woodResonance = Mathf.Sin(2f * Mathf.PI * 260f * t) * 0.25f;

                float envelope = Mathf.Exp(-progress * 14f);
                samples[i] = Mathf.Clamp((body + woodResonance) * envelope, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ShipDock_Thud", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Geminin kalkış anında öttürdüğü sıcak, sevimli liman düdüğü.
        /// </summary>
        private AudioClip CreateProceduralDepartClip()
        {
            int sampleRate = 44100;
            float duration = 0.34f;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float progress = t / duration;

                // F4 (349Hz) ve A4 (440Hz) liman düdüğü akoru
                float f1 = Mathf.Sin(2f * Mathf.PI * 349.2f * t) * 0.50f;
                float f2 = Mathf.Sin(2f * Mathf.PI * 440.0f * t) * 0.35f;
                float f3 = Mathf.Sin(2f * Mathf.PI * 698.4f * t) * 0.15f;

                // Yumuşak başlangıç ve sönüm zarfı
                float attack = Mathf.Clamp01(t / 0.04f);
                float decay = Mathf.Clamp01((duration - t) / 0.08f);

                samples[i] = Mathf.Clamp((f1 + f2 + f3) * attack * decay, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ShipDepart_Whistle", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Slotlar doluyken tıklanırsa çıkan tok uyarı tıklaması.
        /// </summary>
        private AudioClip CreateProceduralDenialClip()
        {
            int sampleRate = 44100;
            float duration = 0.11f;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float progress = t / duration;

                // İki vuruşlu kuru ahşap tok-tok sesi
                float pulse1 = (t < 0.045f) ? Mathf.Sin(2f * Mathf.PI * 280f * t) * Mathf.Exp(-t * 40f) : 0f;
                float pulse2 = (t >= 0.045f) ? Mathf.Sin(2f * Mathf.PI * 220f * (t - 0.045f)) * Mathf.Exp(-(t - 0.045f) * 45f) : 0f;

                samples[i] = Mathf.Clamp((pulse1 + pulse2) * 0.8f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Denial_Knock", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion

        #region ✨ & 💦 Görsel Partikül Sistemleri

        private void InitializeParticles()
        {
            // 1. Sparkle Partikül Sistemi
            Transform sparkleChild = transform.Find("[Sparkle_FX]");
            if (sparkleChild == null)
            {
                GameObject spGo = new GameObject("[Sparkle_FX]");
                spGo.transform.SetParent(transform, false);
                m_SparklePS = spGo.AddComponent<ParticleSystem>();
            }
            else
            {
                m_SparklePS = sparkleChild.GetComponent<ParticleSystem>();
            }

            SetupSparkleSystem(m_SparklePS);

            // 2. Water Splash Partikül Sistemi
            Transform splashChild = transform.Find("[Splash_FX]");
            if (splashChild == null)
            {
                GameObject splashGo = new GameObject("[Splash_FX]");
                splashGo.transform.SetParent(transform, false);
                m_SplashPS = splashGo.AddComponent<ParticleSystem>();
            }
            else
            {
                m_SplashPS = splashChild.GetComponent<ParticleSystem>();
            }

            SetupSplashSystem(m_SplashPS);
        }

        private void SetupSparkleSystem(ParticleSystem ps)
        {
            if (ps == null) return;

            if (m_SparkleMaterial == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                m_SparkleMaterial = new Material(sh);
                m_SparkleMaterial.name = "Sparkle_Particle_Mat";
                m_SparkleMaterial.mainTexture = CreateSparkleTexture();
            }

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = m_SparkleMaterial;
            renderer.alignment = ParticleSystemRenderSpace.View;

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.maxParticles = 500;
            main.startLifetime = 0.38f;
            main.gravityModifier = -0.10f; // Hafif yukarı yükselir

            var emission = ps.emission;
            emission.enabled = false;
        }

        private void SetupSplashSystem(ParticleSystem ps)
        {
            if (ps == null) return;

            if (m_SplashMaterial == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                m_SplashMaterial = new Material(sh);
                m_SplashMaterial.name = "WaterSplash_Particle_Mat";
                m_SplashMaterial.mainTexture = CreateDropletTexture();
            }

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = m_SplashMaterial;
            renderer.alignment = ParticleSystemRenderSpace.View;

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.maxParticles = 500;
            main.startLifetime = 0.42f;
            main.gravityModifier = 1.6f; // Su damlaları yerçekimiyle geri düşer

            var emission = ps.emission;
            emission.enabled = false;
        }

        private Texture2D CreateSparkleTexture()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SparkleStar_Tex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x - center) / center);
                    float dy = Mathf.Abs((y - center) / center);

                    // 4-noktalı ışıltılı yıldız şekli
                    float starCross = Mathf.Pow(Mathf.Clamp01(1f - dx), 4f) * Mathf.Pow(Mathf.Clamp01(1f - dy), 0.7f) +
                                      Mathf.Pow(Mathf.Clamp01(1f - dy), 4f) * Mathf.Pow(Mathf.Clamp01(1f - dx), 0.7f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float core = Mathf.Clamp01(1f - dist * 1.8f);
                    core = core * core;

                    float alpha = Mathf.Clamp01(starCross * 0.75f + core * 0.85f);
                    byte aByte = (byte)(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, aByte);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private Texture2D CreateDropletTexture()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "WaterDroplet_Tex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Dairesel yumuşak su damlası
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = Mathf.SmoothStep(0f, 1f, alpha);
                    byte aByte = (byte)(alpha * 255f);
                    pixels[y * size + x] = new Color32(230, 245, 255, aByte);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        #endregion

        #region 📳 Mobil Haptic (Titreşim) Motoru

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject s_AndroidVibrator;
        private static bool s_AndroidVibratorInitialized = false;

        private static void EnsureAndroidVibrator()
        {
            if (s_AndroidVibratorInitialized) return;
            s_AndroidVibratorInitialized = true;
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    s_AndroidVibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
            }
            catch { }
        }

        private static void VibrateAndroid(long ms, int amplitude)
        {
            try
            {
                EnsureAndroidVibrator();
                if (s_AndroidVibrator == null) return;

                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    int sdkInt = version.GetStatic<int>("SDK_INT");
                    if (sdkInt >= 26) // Android 8.0 Oreo+
                    {
                        using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        {
                            var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude);
                            s_AndroidVibrator.Call("vibrate", effect);
                        }
                    }
                    else
                    {
                        s_AndroidVibrator.Call("vibrate", ms);
                    }
                }
            }
            catch { }
        }
#endif

        /// <summary>
        /// Küp her gemiye bindiğinde çalışan çok hafif, tatlı mikro titreşim (Light Haptic).
        /// </summary>
        public void TriggerHapticLight()
        {
            if (!CanTriggerHaptics()) return;
            m_LastHapticTime = Time.unscaledTime;

#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(12, 45); // Çok hafif 12 ms darbe
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        /// <summary>
        /// Gemi slota tam oturduğunda çalışan tatmin edici orta titreşim (Medium Haptic).
        /// </summary>
        public void TriggerHapticMedium()
        {
            if (!CanTriggerHaptics()) return;
            m_LastHapticTime = Time.unscaledTime;

#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(28, 90); // Dolgun 28 ms darbe
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        /// <summary>
        /// Slotlar doluyken tıklanırsa çalışan hafif ret titreşimi.
        /// </summary>
        public void TriggerHapticWarning()
        {
            if (!CanTriggerHaptics()) return;
            m_LastHapticTime = Time.unscaledTime;

#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(35, 110);
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        private bool CanTriggerHaptics()
        {
            if (!m_EnableHaptics) return false;
            if (m_HapticsOnlyOnMobile && !Application.isMobilePlatform) return false;
            if (Time.unscaledTime - m_LastHapticTime < MinHapticInterval) return false;
            return true;
        }

        #endregion

        #region 🎯 Birleşik Oynanış Geri Bildirimleri (Juice API)

        /// <summary>
        /// Her küp gemiye bindiğinde (HopCargoToShip):
        /// ✨ Parıltı partikülü saçar.
        /// 🔊 Artan pentatonik notayla melodik ses çalar.
        /// 📳 Hafif mobil titreşimi tetikler.
        /// </summary>
        public void PlayCubeBoardFeedback(Vector3 position, Color cubeColor, int cargoIndex)
        {
            // 1. Görsel Parıltı (✨ Sparkle)
            if (m_EnableSparkles && m_SparklePS != null)
            {
                SpawnSparkles(position, cubeColor, 8);
            }

            // 2. Melodik Tatmin Edici Ses (🔊 Chime)
            if (m_EnableSound && m_ChimeSource != null && m_ChimeClips != null && m_ChimeClips.Length > 0)
            {
                int noteIdx = Mathf.Abs(cargoIndex) % m_ChimeClips.Length;
                AudioClip clip = m_ChimeClips[noteIdx];
                if (clip != null)
                {
                    m_ChimeSource.pitch = UnityEngine.Random.Range(0.98f, 1.02f);
                    m_ChimeSource.PlayOneShot(clip, m_MasterVolume * 0.85f);
                }
            }

            // 3. Hafif Titreşim (📳 Light Haptic)
            TriggerHapticLight();
        }

        /// <summary>
        /// Gemi slota yanaşıp kilitlendiğinde:
        /// 💦 Su sıçraması ve köpük parçacıkları yayar.
        /// 🔊 Tok iskele kütüğü ve su darbesi sesi çalar.
        /// 📳 Orta şiddette tok mobil titreşimi tetikler.
        /// </summary>
        public void PlayShipDockFeedback(Vector3 position)
        {
            // 1. Su Sıçraması (💦 Water Splash)
            if (m_EnableWaterSplash && m_SplashPS != null)
            {
                SpawnWaterSplash(position, 14);
            }

            // 2. Tok İskele Darbesi Sesi (🔊 Dock Thud)
            if (m_EnableSound && m_FxSource != null)
            {
                if (m_DockClip != null) m_FxSource.PlayOneShot(m_DockClip, m_MasterVolume);
                if (m_SplashClip != null) m_FxSource.PlayOneShot(m_SplashClip, m_MasterVolume * 0.70f);
            }

            // 3. Orta Titreşim (📳 Medium Haptic)
            TriggerHapticMedium();
        }

        /// <summary>
        /// Gemi dolup açık denize kalkış yaptığında:
        /// 🔊 Neşeli liman düdüğü çalar.
        /// 📳 Tatmin edici kalkış titreşimi tetikler.
        /// </summary>
        public void PlayShipDepartFeedback(Vector3 position)
        {
            if (m_EnableSound && m_FxSource != null && m_DepartClip != null)
            {
                m_FxSource.PlayOneShot(m_DepartClip, m_MasterVolume * 0.90f);
            }

            TriggerHapticMedium();
        }

        /// <summary>
        /// Slotlar doluyken oyuncu gemiye tıkladığında ret sesi ve titreşimi verir.
        /// </summary>
        public void PlayDenialFeedback()
        {
            if (m_EnableSound && m_FxSource != null && m_DenialClip != null)
            {
                m_FxSource.PlayOneShot(m_DenialClip, m_MasterVolume * 0.75f);
            }

            TriggerHapticWarning();
        }

        private void SpawnSparkles(Vector3 pos, Color tint, int count)
        {
            if (m_SparklePS == null) return;

            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            Color sparkleColor = Color.Lerp(tint, new Color(1f, 0.96f, 0.65f, 1f), 0.45f);

            for (int i = 0; i < count; i++)
            {
                Vector2 rCircle = UnityEngine.Random.insideUnitCircle * 0.16f;
                emit.position = pos + new Vector3(rCircle.x, rCircle.y + 0.05f, -0.05f);
                emit.velocity = new Vector3(rCircle.x * 1.4f, UnityEngine.Random.Range(0.4f, 0.9f), -0.2f);
                emit.startColor = sparkleColor;
                emit.startSize = UnityEngine.Random.Range(0.12f, 0.20f);
                emit.startLifetime = UnityEngine.Random.Range(0.28f, 0.42f);
                emit.rotation = UnityEngine.Random.Range(0f, 360f);

                m_SparklePS.Emit(emit, 1);
            }
        }

        private void SpawnWaterSplash(Vector3 pos, int count)
        {
            if (m_SplashPS == null) return;

            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            Color dropletColor = new Color(0.85f, 0.96f, 1f, 0.90f);

            for (int i = 0; i < count; i++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float radius = UnityEngine.Random.Range(0.10f, 0.35f);
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.5f, 0f);

                emit.position = pos + offset;
                // Yukarı ve hafifçe dışarı radyal fışkırma
                emit.velocity = new Vector3(offset.x * 2.2f, UnityEngine.Random.Range(1.2f, 2.2f), UnityEngine.Random.Range(-0.4f, 0.4f));
                emit.startColor = dropletColor;
                emit.startSize = UnityEngine.Random.Range(0.08f, 0.16f);
                emit.startLifetime = UnityEngine.Random.Range(0.32f, 0.48f);

                m_SplashPS.Emit(emit, 1);
            }
        }

        #endregion
    }
}
