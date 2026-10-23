using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Denizcinin taşıdığı küp yığını. İlk küp ellerin arasında (CargoSocket) durur, sonrakiler onun üstüne
    /// dizilir. Dümdüz kule değil: küpler hafif bir yılan kıvrımıyla yana kayar ve kıvrıma göre eğilir.
    ///
    /// Yürürken kule yılan gibi canlıdır: her küp yaylı bir zincir halkası gibi davranır. Denizcinin ivmesi
    /// (dönüş, hızlanma, durma) küpleri ters yöne iter ve üstteki küpler geride kalıp yetişir; üstüne yürüme
    /// hızıyla orantılı, aşağıdan yukarı akan bir dalga biner. Denizci durunca salınım yumuşakça söner.
    ///
    /// Kök, SailorRunnerVisual.AttachCargo ile ModelPivot altına bağlanır ve her karede sokete taşınır;
    /// yerel uzay mesh birimidir (1 = bir küp kenarı), yerel +Y yığının yukarı yönüdür.
    /// </summary>
    [DisallowMultipleComponent]
    public class CarriedCargoStack : MonoBehaviour
    {
        [Tooltip("Küpler arası dikey aralık (küp kenarı cinsinden). 1'in biraz altı: küpler hafif iç içe oturur.")]
        [SerializeField] private float m_Spacing = 0.94f;
        [Tooltip("Durağan yılan kıvrımının yana genliği (küp kenarı cinsinden).")]
        [SerializeField] private float m_SnakeAmplitude = 0.17f;
        [Tooltip("Kıvrımın kat başına faz ilerlemesi (radyan). Büyüdükçe kıvrım sıklaşır.")]
        [SerializeField] private float m_SnakeFrequency = 1.15f;
        [Tooltip("Her küpün kendi ekseninde rastgele dönüşü (derece): kule kusursuz hizalı durmasın.")]
        [SerializeField] private float m_RandomYaw = 7f;

        [Header("Yürürken yılan gibi salınım")]
        [Tooltip("Yürüme dalgasının en üst küpteki genliği (küp kenarı cinsinden).")]
        [SerializeField] private float m_WaveAmplitude = 0.28f;
        [Tooltip("Dalganın hızı (rad/sn).")]
        [SerializeField] private float m_WaveSpeed = 7f;
        [Tooltip("Dalganın kat başına gecikmesi (radyan): dalga aşağıdan yukarı akar.")]
        [SerializeField] private float m_WaveLag = 0.75f;
        [Tooltip("Bu hızda (küp kenarı/sn) dalga tam genliğe ulaşır.")]
        [SerializeField] private float m_FullWaveSpeed = 8f;
        [Tooltip("İvmenin küpleri ters yöne itme gücü: dönüşte/duruşta üst küpler geride kalır.")]
        [SerializeField] private float m_InertiaGain = 0.3f;
        [Tooltip("Yay sertliği: büyüdükçe küpler yerine daha çabuk döner.")]
        [SerializeField] private float m_Stiffness = 55f;
        [Tooltip("Sönüm oranı (0-1): küçükse daha uzun sallanır.")]
        [SerializeField, Range(0.05f, 1f)] private float m_DampingRatio = 0.32f;
        [Tooltip("En üst küpün en fazla yana savrulması (küp kenarı cinsinden): kule devrilecekmiş gibi durmasın.")]
        [SerializeField] private float m_MaxSway = 0.5f;

        private readonly List<Transform> m_Cubes = new List<Transform>();
        private readonly List<float> m_Yaw = new List<float>();
        private readonly List<Vector2> m_Offset = new List<Vector2>();   // dinamik yana kayma (yerel X, Z)
        private readonly List<Vector2> m_OffsetVel = new List<Vector2>();
        private float m_Phase;
        private float m_PhaseZ;

        private Vector3 m_LastPos;
        private Vector3 m_Vel;
        private Vector3 m_Acc;
        private bool m_HasLast;
        private float m_Move;        // 0 duruyor .. 1 tam hızda yürüyor (yumuşatılmış)
        private float m_WaveTime;    // dalga yalnızca yürürken ilerler

        public int Count => m_Cubes.Count;

        private void Awake()
        {
            // Her denizcinin yığını farklı kıvrılsın
            m_Phase = Random.Range(0f, Mathf.PI * 2f);
            m_PhaseZ = Random.Range(0f, Mathf.PI * 2f);
            m_WaveTime = Random.Range(0f, 10f);
        }

        /// <summary>Küpü yığının en üstüne ekler. <paramref name="unitScale"/>: mesh'i bir küp kenarı boyuna getiren ölçek.</summary>
        public void Add(Transform cube, float unitScale = 1f)
        {
            if (cube == null) return;
            cube.SetParent(transform, false);
            cube.localRotation = Quaternion.identity;
            m_Cubes.Add(cube);
            m_Yaw.Add(m_Cubes.Count == 1 ? 0f : Random.Range(-m_RandomYaw, m_RandomYaw));
            // Yeni küp alttakinin o anki kaymasıyla başlasın: yığına eklenince yerinden sıçramasın
            Vector2 below = m_Offset.Count > 0 ? m_Offset[m_Offset.Count - 1] : Vector2.zero;
            m_Offset.Add(below);
            m_OffsetVel.Add(Vector2.zero);
            Place(m_Cubes.Count - 1);

            // Yeni küp yığına "pop" ile otursun; ilk küp ellerde zaten büyüyerek belirir
            cube.localScale = Vector3.one * (0.35f * unitScale);
            cube.DOScale(Vector3.one * unitScale, 0.2f).SetEase(Ease.OutBack).SetLink(cube.gameObject);
        }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt > 1e-5f) Simulate(dt);
            for (int i = 0; i < m_Cubes.Count; i++) Place(i);
        }

        private void Simulate(float dt)
        {
            // Kökün hızı ve ivmesi, yığının yerel uzayında (küp kenarı/sn): yön dönüşleri de ivme olarak gelir
            Vector3 pos = transform.position;
            if (!m_HasLast) { m_LastPos = pos; m_HasLast = true; }
            Vector3 vel = (pos - m_LastPos) / dt;
            m_LastPos = pos;
            Vector3 prevVel = m_Vel;
            m_Vel = Vector3.Lerp(m_Vel, vel, 1f - Mathf.Exp(-dt * 18f));   // soket titreşimini süz
            // İvme de süzülür: kare kare hız farkı gürültülüdür (düşük FPS'te kule sürekli sınıra yapışıyordu)
            m_Acc = Vector3.Lerp(m_Acc, (m_Vel - prevVel) / dt, 1f - Mathf.Exp(-dt * 8f));
            Vector3 accLocal = transform.InverseTransformVector(m_Acc);
            Vector3 velLocal = transform.InverseTransformVector(m_Vel);

            float speed = new Vector2(velLocal.x, velLocal.z).magnitude + Mathf.Abs(velLocal.y) * 0.5f;
            float targetMove = Mathf.Clamp01(speed / Mathf.Max(0.01f, m_FullWaveSpeed));
            m_Move = Mathf.Lerp(m_Move, targetMove, 1f - Mathf.Exp(-dt * (targetMove > m_Move ? 6f : 3f)));
            m_WaveTime += dt * Mathf.Lerp(0.35f, 1f, m_Move);

            float k = m_Stiffness;
            float c = 2f * Mathf.Sqrt(k) * m_DampingRatio;
            Vector2 push = new Vector2(-accLocal.x, -accLocal.z) * m_InertiaGain;

            for (int i = 1; i < m_Cubes.Count; i++)
            {
                float h = Height(i);
                Vector2 wave = new Vector2(
                    Mathf.Sin(m_WaveTime * m_WaveSpeed - i * m_WaveLag + m_Phase),
                    0.7f * Mathf.Sin(m_WaveTime * m_WaveSpeed * 0.83f - i * m_WaveLag + m_PhaseZ))
                    * (m_WaveAmplitude * m_Move * h);

                // Zincir: her küp kendi dalga hedefine ve biraz da alttakinin kaymasına çekilir (birlikte kıvrılırlar)
                Vector2 below = m_Offset[i - 1];
                Vector2 target = Vector2.Lerp(wave, below + wave * 0.5f, 0.35f);
                Vector2 o = m_Offset[i], v = m_OffsetVel[i];
                Vector2 a = (target - o) * k - v * c + push * h;

                // Sınıra yaklaşınca sertleşen yay: sert kesmeye çarpmadan yumuşakça geri döner
                float limit = m_MaxSway * Mathf.Max(0.15f, h);
                float mag = o.magnitude;
                float soft = limit * 0.65f;
                if (mag > soft) a -= o / mag * ((mag - soft) * k * 5f);

                v += a * dt;
                o += v * dt;

                if (o.sqrMagnitude > limit * limit)
                {
                    o = o.normalized * limit;
                    v *= 0.5f;
                }
                m_Offset[i] = o;
                m_OffsetVel[i] = v;
            }
        }

        /// <summary>Katın yüksekliğe göre salınım payı: alttaki küp sabit, dördüncü kattan itibaren tam.</summary>
        private static float Height(int i) => Mathf.Clamp01(i / 4f);

        private Vector2 Curve(int i)
        {
            // Durağan kıvrım iki yatay eksende birden (X ve Z, farklı faz/sıklık): denizci hangi yöne bakarsa baksın
            // yığın ekranda dümdüz görünmez. x(0) = z(0) = 0: alttaki küp ellerin tam ortasında kalır.
            float fz = m_SnakeFrequency * 0.8f;
            float snakeX = m_SnakeAmplitude * (Mathf.Sin(i * m_SnakeFrequency + m_Phase) - Mathf.Sin(m_Phase));
            float snakeZ = 0.8f * m_SnakeAmplitude * (Mathf.Sin(i * fz + m_PhaseZ) - Mathf.Sin(m_PhaseZ));
            Vector2 dyn = i < m_Offset.Count ? m_Offset[i] : Vector2.zero;
            return new Vector2(snakeX, snakeZ) + dyn;
        }

        private void Place(int i)
        {
            Transform c = m_Cubes[i];
            if (c == null) return;

            Vector2 p = Curve(i);
            c.localPosition = new Vector3(p.x, i * m_Spacing, p.y);

            // Küp, kendisiyle alttaki küp arasındaki kıvrımın eğimine göre yatar (ilk küp dik)
            float tiltZ = 0f, tiltX = 0f;
            if (i > 0)
            {
                Vector2 d = (p - Curve(i - 1)) / Mathf.Max(0.01f, m_Spacing);
                tiltZ = -Mathf.Atan(d.x) * Mathf.Rad2Deg * 0.7f;
                tiltX = Mathf.Atan(d.y) * Mathf.Rad2Deg * 0.7f;
            }
            float yaw = i < m_Yaw.Count ? m_Yaw[i] : 0f;
            c.localRotation = Quaternion.Euler(tiltX, yaw, tiltZ);
        }
    }
}
