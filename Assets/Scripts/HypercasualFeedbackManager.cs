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
        private AudioSource m_SailSource;
        private AudioClip[] m_ChimeClips;
        private AudioClip m_ShipFullClip;
        private AudioClip m_SplashClip;
        private AudioClip m_DockClip;
        private AudioClip m_DepartClip;
        private AudioClip m_DenialClip;
        private AudioClip m_LiftoffClip;
        private AudioClip m_SailClip;
        private AudioClip m_GameLaunchClip;
        private int m_ActiveSailingShips = 0;
        private Coroutine m_SailFadeRoutine;

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

        private static bool s_LaunchSoundPlayed = false;

        private void OnEnable()
        {
            s_Instance = this;
            InitializeAudio();
            InitializeParticles();
        }

        private void Start()
        {
            if (Application.isPlaying && !s_LaunchSoundPlayed)
            {
                // LandFlowLoadingScreen aktif değilse doğrudan buradan açılış sesini tetikle
                if (LandFlowLoadingScreen.Instance == null || !LandFlowLoadingScreen.Instance.IsVisible)
                {
                    PlayGameLaunchFeedback();
                }
            }
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

            if (m_SailSource == null)
            {
                m_SailSource = gameObject.AddComponent<AudioSource>();
                m_SailSource.playOnAwake = false;
                m_SailSource.spatialBlend = 0f;
                m_SailSource.loop = true;
            }

            // Müzikal pentatonik küp sesleri (C5 - D5 - E5 - G5 - A5 - C6 - D6 - E6 - G6 - A6):
            // Modern hypercasual oyunlarındaki gibi parlak, tatlı, dopamin salgılatan kalimba & bubble-pop tınısı
            if (m_ChimeClips == null || m_ChimeClips.Length == 0 || System.Array.Exists(m_ChimeClips, c => c == null))
            {
                float[] freqs = new float[] {
                    523.25f, // C5
                    587.33f, // D5
                    659.25f, // E5
                    783.99f, // G5
                    880.00f, // A5
                    1046.50f, // C6
                    1174.66f, // D6
                    1318.51f, // E6
                    1567.98f, // G6
                    1760.00f  // A6
                };
                m_ChimeClips = new AudioClip[freqs.Length];
                for (int i = 0; i < freqs.Length; i++)
                {
                    m_ChimeClips[i] = CreateProceduralChimeClip(freqs[i]);
                }
            }

            if (m_ShipFullClip == null) m_ShipFullClip = CreateProceduralShipFullClip();
            if (m_SplashClip == null) m_SplashClip = CreateProceduralSplashClip();
            if (m_DockClip == null) m_DockClip = CreateProceduralDockClip();
            if (m_DepartClip == null) m_DepartClip = CreateProceduralDepartClip();
            if (m_DenialClip == null) m_DenialClip = CreateProceduralDenialClip();
            if (m_LiftoffClip == null) m_LiftoffClip = CreateProceduralLiftoffClip();
            if (m_SailClip == null) m_SailClip = CreateProceduralSailClip();
            if (m_GameLaunchClip == null) m_GameLaunchClip = CreateProceduralGameLaunchClip();
        }

        /// <summary>
        /// Küpün gemiye bindiğinde çıkardığı, modern hypercasual puzzle oyunlarındaki gibi
        /// psikolojik olarak son derece tatmin edici, sulu bubble-pop ve rezonanslı ahşap kalimba/marimba tınısı.
        /// </summary>
        private AudioClip CreateProceduralChimeClip(float fundamentalFreq)
        {
            int sampleRate = 44100;
            float duration = 0.15f; // 150 ms
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];

            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;

                // 1. İlk 18 ms içinde tatlı "bubble pop" mikro frekans bükülmesi (1.52x -> 1.0x f)
                // Bu hızlı bükülme kulağa o bağımlılık yapan dolgun "pop / plink / bloop" hissini verir.
                float curFreq = fundamentalFreq;
                if (t < 0.018f)
                {
                    float pRatio = t / 0.018f;
                    curFreq = fundamentalFreq * (1.52f - 0.52f * Mathf.Sqrt(pRatio));
                }
                phase += 2f * Mathf.PI * curFreq / sampleRate;

                // 2. Çok yumuşak 1.5 ms atak (tık/patlama çıtırtısı yapmaz)
                float attack = Mathf.Clamp01(t / 0.0015f);

                // 3. Kalimba / Marimba harmonikleri:
                // Temel ton (dolgun yuvarlak gövde)
                float h1 = Mathf.Sin(phase);
                // 2. harmonik (tatlı gövde sıcaklığı)
                float h2 = Mathf.Sin(phase * 2f) * 0.28f;
                // 4. marimba çubuk kısmi tonu (~3.93x f): ahşap tınısını veren rezonans
                float h3 = Mathf.Sin(phase * 3.93f) * 0.18f * Mathf.Exp(-t * 50f);

                // 4. İlk 3 ms minik ahşap tokmak temas darbesi (mallet transient)
                float click = 0f;
                if (t < 0.003f)
                {
                    click = Mathf.Sin(2f * Mathf.PI * 3400f * t) * (1f - t / 0.003f) * 0.32f;
                }

                // 5. Akıcı ve tatlı sönümlenme zarfı
                float envelope = attack * Mathf.Exp(-t * 22f);

                samples[i] = Mathf.Clamp((h1 + h2 + h3 + click) * envelope * 0.78f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create($"Chime_{Mathf.RoundToInt(fundamentalFreq)}", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Gemi tamamen dolduğunda çalan, dopamin salgılatan zafer major akor arpej tınısı (C6 - E6 - G6 - C7).
        /// </summary>
        private AudioClip CreateProceduralShipFullClip()
        {
            int sampleRate = 44100;
            float duration = 0.38f;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];

            // C6 (1046.5), E6 (1318.5), G6 (1568.0), C7 (2093.0)
            float[] freqs = new float[] { 1046.50f, 1318.51f, 1567.98f, 2093.00f };
            float[] delays = new float[] { 0.000f, 0.035f, 0.070f, 0.105f };
            float[] weights = new float[] { 0.40f, 0.38f, 0.35f, 0.30f };
            float[] phases = new float[4];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float acc = 0f;

                for (int n = 0; n < 4; n++)
                {
                    if (t >= delays[n])
                    {
                        float dt = t - delays[n];
                        float freq = freqs[n];
                        if (dt < 0.015f)
                        {
                            freq *= (1.25f - 0.25f * Mathf.Sqrt(dt / 0.015f));
                        }
                        phases[n] += 2f * Mathf.PI * freq / sampleRate;

                        float h1 = Mathf.Sin(phases[n]);
                        float h2 = Mathf.Sin(phases[n] * 2f) * 0.22f;
                        float att = Mathf.Clamp01(dt / 0.002f);
                        float env = att * Mathf.Exp(-dt * 14f);
                        acc += (h1 + h2) * env * weights[n];
                    }
                }

                samples[i] = Mathf.Clamp(acc * 0.82f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ShipFull_Celebration", count, 1, sampleRate, false);
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
        /// Gemi slota tam yerleştiğinde çıkan net, tok mekanik-ahşap kilitlenme (dock latch snap)
        /// ve gövde oturması ile su bloop sesi. Slota 'şak' diye oturma tatminini verir.
        /// </summary>
        private AudioClip CreateProceduralDockClip()
        {
            int sampleRate = 44100;
            float duration = 0.20f; // 200 ms net, çıtır ve tok
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];
            float snapPhase = 0f;
            float blupPhase = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float attack = Mathf.Clamp01(t / 0.0015f);

                // 1. Ahşap iskele kilit mandalı şak sesi (Faz integrasyonu ile temiz, pürüzsüz ton)
                float snapFreq = Mathf.Lerp(780f, 340f, Mathf.Clamp01(t / 0.022f));
                snapPhase += 2f * Mathf.PI * snapFreq / sampleRate;
                float snap = Mathf.Sin(snapPhase) * Mathf.Exp(-t * 62f) * 0.65f;

                // 2. Dolgun tekne gövdesi darbesi (160 Hz + 80 Hz sub-bass tokluk)
                float thud = (Mathf.Sin(2f * Mathf.PI * 160f * t) * 0.70f + Mathf.Sin(2f * Mathf.PI * 80f * t) * 0.40f) * Mathf.Exp(-t * 26f);

                // 3. 12 ms sonra başlayan sulu okyanus blup'u ve hafif serpinti
                float water = 0f;
                float bt = t - 0.012f;
                if (bt > 0f)
                {
                    float wProg = Mathf.Clamp01(bt / 0.12f);
                    float wFreq = Mathf.Lerp(420f, 180f, Mathf.Pow(wProg, 0.6f));
                    blupPhase += 2f * Mathf.PI * wFreq / sampleRate;
                    float wEnv = Mathf.Clamp01(bt / 0.004f) * Mathf.Exp(-bt * 20f);
                    water = Mathf.Sin(blupPhase) * wEnv * 0.40f;
                }

                samples[i] = Mathf.Clamp((snap + thud + water) * attack * 0.80f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ShipDock_LatchSnap", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Gemi slotlara doğru giderken çalan, modern hypercasual puzzle oyunları standartlarında
        /// son derece ferah, ipeksi su süzülmesi ve hidrodinamik dalga akıntısı sesi (Hydro-Glide Water Swoosh).
        /// Asla kaba motor uğultusu veya rahatsız edici beyaz gürültü yapmaz; ferahlatıcı, akıcı bir su kayması hissi verir.
        /// </summary>
        private AudioClip CreateProceduralSailClip()
        {
            int sampleRate = 44100;
            float duration = 1.0f; // 1 saniyelik dikişsiz döngü (seamless loop)
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];
            var rnd = new System.Random(101);

            // Çift kutuplu yumuşak alçak geçiren filtre (Two-pole gentle low-pass filter)
            float lp1 = 0f;
            float lp2 = 0f;
            float glidePhase = 0f;
            float bubblePhase = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;

                // 1. 🌊 İpeksi Su Süzülmesi (Velvety Hydrodynamic Water Flow):
                // Çiğ beyaz gürültüyü çift filtre ile 500-800 Hz arasına çekiyoruz.
                // Kulağı tırmalayan radyo paraziti veya hışırtı sıfırlanır, ipeksi su yarılması elde edilir.
                float rawNoise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp1 += (rawNoise - lp1) * 0.075f;
                lp2 += (lp1 - lp2) * 0.075f;

                // Okyanus dalga nefesi (1.8 Hz sakin, ferah su kabarması):
                float swell = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * 1.8f * t);
                float waterRush = lp2 * swell * 0.62f;

                // 2. 🛥️ Sıcak & Dolgun Gövde Süzülüşü (Velvet Hull Glide Harmonic):
                // Uğultusuz, tatlı, pürüzsüz sinüs gövdesi (G3 196 Hz + yumuşak 2. harmonik G4 392 Hz).
                // Rahatsız edici motor patlaması veya testere zırıltısı YOKTUR; akıcı süzülme vardır.
                glidePhase += 2f * Mathf.PI * 196f / sampleRate;
                float hullTone = (Mathf.Sin(glidePhase) * 0.18f + Mathf.Sin(glidePhase * 2f) * 0.05f) * (0.8f + 0.2f * swell);

                // 3. 🫧 Hafif Tatlı Su Kabarcığı & Şıpırtı Rezonansı (Micro-Bubble Shimmer):
                float ripEnv = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(2f * Mathf.PI * 3.6f * t)), 4f);
                bubblePhase += 2f * Mathf.PI * 540f / sampleRate;
                float ripple = Mathf.Sin(bubblePhase) * ripEnv * 0.10f;

                samples[i] = (waterRush + hullTone + ripple);
            }

            // Dikişsiz Döngü (Seamless Loop Crossfade - ilk ve son 40 ms pürüzsüz harmanlama):
            int fadeSamples = Mathf.RoundToInt(sampleRate * 0.04f);
            for (int i = 0; i < fadeSamples; i++)
            {
                float blend = i / (float)fadeSamples;
                int endIdx = count - fadeSamples + i;
                float head = samples[i];
                float tail = samples[endIdx];
                samples[i] = Mathf.Lerp(tail, head, blend);
                samples[endIdx] = Mathf.Lerp(tail, head, blend);
            }

            for (int i = 0; i < count; i++)
            {
                samples[i] = Mathf.Clamp(samples[i] * 0.85f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ShipSail_CruiseLoop", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Küplerin gemiye binmek üzere yerlerinden ilk ayrılırken çıkardığı, modern hypercasual ASMR standartlarına uyarlanmış
        /// hafif, tatlı, gevrek kinetik kum sürtünmesi ve tatlı havalanma pufu sesi (soft kinetic sand crunch & puff).
        /// Asla kaba gürültü yapmaz; kadifemsi kum tanesi dokunuşu ve tatmin edici bir kalkış çıtırtısı sunar.
        /// </summary>
        private AudioClip CreateProceduralLiftoffClip()
        {
            int sampleRate = 44100;
            float duration = 0.105f; // 105 ms (hafif, çıtır ve ferah)
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];
            var rnd = new System.Random(77);

            float filterPrev = 0f;
            float scoopPhase = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float progress = t / duration;

                // 1. 🏖️ Kadifemsi Kum Sürtünmesi (Soft Sand Friction & Grain Rustle):
                // Pembe/beyaz gürültünün yumuşatılmış bant filtresi (1.8 kHz - 3.2 kHz tatlı kum fısıltısı)
                float whiteNoise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                filterPrev += (whiteNoise - filterPrev) * 0.28f;
                float sandGrains = filterPrev * 0.38f;

                // 2. 💨 Havalanma Pufu ve Gövde Esintisi (Sandy Scoop Puff: 210 Hz -> 440 Hz):
                float scoopFreq = Mathf.Lerp(210f, 440f, Mathf.Sqrt(progress));
                scoopPhase += 2f * Mathf.PI * scoopFreq / sampleRate;
                float scoopTone = Mathf.Sin(scoopPhase) * 0.32f;

                // 3. 🍿 Kinetik Kum Çıtırtısı (İlk 22 ms içindeki mikro granül ayrılmaları):
                float crunch = 0f;
                if (t < 0.022f)
                {
                    float microTime = t / 0.022f;
                    float burst = Mathf.Sin(microTime * Mathf.PI * 4f);
                    crunch = burst * burst * (1f - microTime) * ((float)rnd.NextDouble() * 0.30f);
                }

                // 4. Zarf (ADSR): Yumuşak 1.5 ms atak, ardından üstel kadife sönüm
                float attack = Mathf.Clamp01(t / 0.0015f);
                float envelope = attack * Mathf.Exp(-t * 38f);

                samples[i] = Mathf.Clamp((sandGrains + scoopTone + crunch) * envelope * 0.85f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("CubeLiftoff_SandPuff", count, 1, sampleRate, false);
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

        /// <summary>
        /// Oyun açılışında çalan, modern hypercasual hit oyunları (Royal Match / Supercell stili) kalitesinde
        /// ferah, neşeli, tatmin edici ve akılda kalıcı açılış jingle'ı (Brand Splash & Launch Jingle).
        /// Yükselen parlak C Majör kalimba arpeji + tropikal çan parıltısı ve rezonanslı sahil ambiyansı.
        /// </summary>
        private AudioClip CreateProceduralGameLaunchClip()
        {
            int sampleRate = 44100;
            float duration = 1.15f; // 1.15 saniyelik zengin açılış melodisi
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];

            // 5 notalı yükselen neşeli melodi: C5, E5, G5, C6, E6
            float[] noteFreqs = { 523.25f, 659.25f, 783.99f, 1046.50f, 1318.51f };
            float[] noteTimes = { 0.00f, 0.10f, 0.20f, 0.30f, 0.42f };
            float[] noteWeights = { 0.55f, 0.65f, 0.75f, 0.90f, 1.00f };
            float[] phases = new float[noteFreqs.Length];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float acc = 0f;

                // 1. Yükselen Melodik Çanlar (Kalimba / Music Box / Celesta Arpej):
                for (int n = 0; n < noteFreqs.Length; n++)
                {
                    float dt = t - noteTimes[n];
                    if (dt >= 0f)
                    {
                        float freq = noteFreqs[n];
                        // İlk 15 ms mikro pitch sweep (tatlı bubble pop hissi)
                        if (dt < 0.015f)
                        {
                            freq *= (1.30f - 0.30f * Mathf.Sqrt(dt / 0.015f));
                        }
                        phases[n] += 2f * Mathf.PI * freq / sampleRate;

                        // Temel ton + 2. harmonik + çan parıltısı (3.harmonik)
                        float h1 = Mathf.Sin(phases[n]);
                        float h2 = Mathf.Sin(phases[n] * 2f) * 0.28f;
                        float h3 = Mathf.Sin(phases[n] * 3f) * 0.12f * Mathf.Exp(-dt * 20f);

                        // Hızlı yumuşak atak, son notada daha uzun rezonanslı sönüm
                        float att = Mathf.Clamp01(dt / 0.002f);
                        float decayRate = (n == noteFreqs.Length - 1) ? 5.5f : 11.0f;
                        float env = att * Mathf.Exp(-dt * decayRate);

                        acc += (h1 + h2 + h3) * env * noteWeights[n] * 0.35f;
                    }
                }

                // 2. Açılış Su & Kristal Parıltısı (Sparkle Shimmer Swoosh - 0.0s -> 0.45s):
                if (t < 0.45f)
                {
                    float shProg = t / 0.45f;
                    float shFreq = Mathf.Lerp(2200f, 4400f, shProg);
                    float shimmer = Mathf.Sin(2f * Mathf.PI * shFreq * t) * Mathf.Sin(shProg * Mathf.PI) * 0.08f;
                    acc += shimmer;
                }

                // 3. Sıcak Alt Gövde / Sahil Bası (Warm sub-warmth C4 261 Hz son notada rezonans):
                if (t >= 0.42f)
                {
                    float dtEnd = t - 0.42f;
                    float warm = Mathf.Sin(2f * Mathf.PI * 261.63f * dtEnd) * Mathf.Exp(-dtEnd * 4.5f) * 0.18f;
                    acc += warm;
                }

                samples[i] = Mathf.Clamp(acc * 0.90f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("GameLaunch_Jingle", count, 1, sampleRate, false);
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
        /// Küp gemiye binmek için yerinden ayrıldığında hafif ve tatmin edici bir kalkış sesi çalar.
        /// Çoklu küplerde 'index' ile minik arpeggio gecikmesi ve hafif artan pitch eklenerek
        /// bağımlılık yapan bir "pıt-pıt-pıt" ASMR melodisi oluşturulur.
        /// </summary>
        public void PlayCubeLiftoffFeedback(Vector3 position, int index = 0)
        {
            if (!m_EnableSound || m_ChimeSource == null || m_LiftoffClip == null) return;

            if (index <= 0)
            {
                PlayLiftoffSound(0);
                TriggerHapticLight();
            }
            else
            {
                StartCoroutine(PlayDelayedLiftoff(index));
            }
        }

        private System.Collections.IEnumerator PlayDelayedLiftoff(int index)
        {
            // Küpler sırayla hafifçe ayrılırken arpeggio gecikmesi (36 ms)
            yield return new WaitForSeconds(index * 0.036f);
            PlayLiftoffSound(index);
        }

        private void PlayLiftoffSound(int index)
        {
            if (m_ChimeSource == null || m_LiftoffClip == null) return;

            // Her küp için hafif yükselen sevimli pitch skalası (1.0 -> 1.25)
            float pitch = Mathf.Clamp(1.0f + (index % 6) * 0.045f + UnityEngine.Random.Range(-0.015f, 0.015f), 0.95f, 1.35f);
            m_ChimeSource.pitch = pitch;
            // Hafif, gevrek ve tatmin edici hypercasual kum kalkış sesi:
            m_ChimeSource.PlayOneShot(m_LiftoffClip, m_MasterVolume * 0.44f);
        }

        /// <summary>
        /// Her küp gemiye bindiğinde (HopCargoToShip):
        /// ✨ Parıltı partikülü saçar.
        /// 🔊 Artan pentatonik notayla melodik ses çalar (gemi dolduysa zafer akoru çalar).
        /// 📳 Hafif mobil titreşimi tetikler.
        /// </summary>
        public void PlayCubeBoardFeedback(Vector3 position, Color cubeColor, int cargoIndex, bool isShipFull = false)
        {
            // 1. Görsel Parıltı (✨ Sparkle)
            if (m_EnableSparkles && m_SparklePS != null)
            {
                SpawnSparkles(position, cubeColor, isShipFull ? 18 : 8);
            }

            // 2. Melodik Tatmin Edici Ses (🔊 Chime / ShipFull)
            if (m_EnableSound && m_ChimeSource != null)
            {
                if (isShipFull && m_ShipFullClip != null)
                {
                    m_ChimeSource.pitch = 1.0f;
                    m_ChimeSource.PlayOneShot(m_ShipFullClip, m_MasterVolume * 0.95f);
                }
                else if (m_ChimeClips != null && m_ChimeClips.Length > 0)
                {
                    int noteIdx = Mathf.Abs(cargoIndex) % m_ChimeClips.Length;
                    AudioClip clip = m_ChimeClips[noteIdx];
                    if (clip != null)
                    {
                        m_ChimeSource.pitch = UnityEngine.Random.Range(0.985f, 1.015f);
                        m_ChimeSource.PlayOneShot(clip, m_MasterVolume * 0.82f);
                    }
                }
            }

            // 3. Titreşim (📳 Haptic)
            if (isShipFull)
            {
                TriggerHapticMedium();
            }
            else
            {
                TriggerHapticLight();
            }
        }

        /// <summary>
        /// Gemi slotlara doğru yola çıktığında başlayan tatlı, ferah su süzülmesi ve dalga sesi.
        /// Slotlara gidene kadar yumuşakça çalar, varınca pürüzsüz sönerek kilit darbesine bağlanır.
        /// </summary>
        public void StartShipSailSound(Vector3 position)
        {
            m_ActiveSailingShips++;
            if (!m_EnableSound || m_SailSource == null || m_SailClip == null) return;

            if (m_SailFadeRoutine != null)
            {
                StopCoroutine(m_SailFadeRoutine);
                m_SailFadeRoutine = null;
            }

            m_SailSource.clip = m_SailClip;
            m_SailSource.loop = true;
            if (!m_SailSource.isPlaying)
            {
                m_SailSource.volume = 0f;
                m_SailSource.pitch = UnityEngine.Random.Range(0.98f, 1.02f);
                m_SailSource.Play();
            }
            m_SailFadeRoutine = StartCoroutine(FadeSailSource(m_MasterVolume * 0.35f, 0.05f, false));
        }

        /// <summary>
        /// Gemi slota varıp yerleştiğinde seyir sesini pürüzsüz sönümle durdurur (sıfır tıklama / patlama).
        /// </summary>
        public void StopShipSailSound()
        {
            m_ActiveSailingShips = Mathf.Max(0, m_ActiveSailingShips - 1);
            if (m_ActiveSailingShips == 0 && m_SailSource != null && m_SailSource.isPlaying)
            {
                if (m_SailFadeRoutine != null)
                {
                    StopCoroutine(m_SailFadeRoutine);
                    m_SailFadeRoutine = null;
                }
                m_SailFadeRoutine = StartCoroutine(FadeSailSource(0f, 0.07f, true));
            }
        }

        private System.Collections.IEnumerator FadeSailSource(float targetVol, float duration, bool stopOnZero)
        {
            if (m_SailSource == null) yield break;
            float startVol = m_SailSource.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (m_SailSource != null)
                {
                    m_SailSource.volume = Mathf.Lerp(startVol, targetVol, t);
                }
                yield return null;
            }
            if (m_SailSource != null)
            {
                m_SailSource.volume = targetVol;
                if (stopOnZero && targetVol <= 0.001f)
                {
                    m_SailSource.Stop();
                }
            }
            m_SailFadeRoutine = null;
        }

        /// <summary>
        /// Gemi slota yanaşıp kilitlendiğinde:
        /// 💦 Su sıçraması ve köpük parçacıkları yayar.
        /// 🔊 Tok mekanik-ahşap kilit ve su darbesi sesi çalar.
        /// 📳 Orta şiddette tok mobil titreşimi tetikler.
        /// </summary>
        public void PlayShipDockFeedback(Vector3 position)
        {
            // 1. Su Sıçraması (💦 Water Splash)
            if (m_EnableWaterSplash && m_SplashPS != null)
            {
                SpawnWaterSplash(position, 16);
            }

            // 2. Tok İskele Kilit Darbesi Sesi (🔊 Dock Snap & Thud)
            if (m_EnableSound && m_FxSource != null && m_DockClip != null)
            {
                m_FxSource.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
                m_FxSource.PlayOneShot(m_DockClip, m_MasterVolume * 0.95f);
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

        /// <summary>
        /// Oyun açılışında, stüdyo ve Land Flow açılış ekranında çalan ferahlatıcı, neşeli açılış jingle'ı.
        /// </summary>
        public void PlayGameLaunchFeedback()
        {
            if (!m_EnableSound || s_LaunchSoundPlayed) return;
            s_LaunchSoundPlayed = true;

            if (m_FxSource != null && m_GameLaunchClip != null)
            {
                m_FxSource.pitch = 1.0f;
                m_FxSource.PlayOneShot(m_GameLaunchClip, m_MasterVolume * 0.95f);
            }
            else if (m_ChimeSource != null && m_GameLaunchClip != null)
            {
                m_ChimeSource.pitch = 1.0f;
                m_ChimeSource.PlayOneShot(m_GameLaunchClip, m_MasterVolume * 0.95f);
            }

            TriggerHapticMedium();
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
