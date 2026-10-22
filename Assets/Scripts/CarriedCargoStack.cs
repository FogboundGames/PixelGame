using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Denizcinin taşıdığı küp yığını. İlk küp ellerin arasında (CargoSocket) durur, sonrakiler onun üstüne
    /// dizilir. Dümdüz kule değil: küpler hafif bir yılan kıvrımıyla yana kayar, kıvrıma göre eğilir ve
    /// yürürken yukarı doğru artan, gecikmeli bir salınımla sallanır.
    ///
    /// Kök, SailorRunnerVisual.AttachCargo ile ModelPivot altına bağlanır ve her karede sokete taşınır;
    /// yerel uzay mesh birimidir (1 = bir küp kenarı), yerel +Y yığının yukarı yönüdür.
    /// </summary>
    [DisallowMultipleComponent]
    public class CarriedCargoStack : MonoBehaviour
    {
        [Tooltip("Küpler arası dikey aralık (küp kenarı cinsinden). 1'in biraz altı: küpler hafif iç içe oturur.")]
        [SerializeField] private float m_Spacing = 0.94f;
        [Tooltip("Yılan kıvrımının yana genliği (küp kenarı cinsinden).")]
        [SerializeField] private float m_SnakeAmplitude = 0.17f;
        [Tooltip("Kıvrımın kat başına faz ilerlemesi (radyan). Büyüdükçe kıvrım sıklaşır.")]
        [SerializeField] private float m_SnakeFrequency = 1.15f;
        [Tooltip("Yürürken salınım genliği (en üst küpte, küp kenarı cinsinden).")]
        [SerializeField] private float m_SwayAmplitude = 0.06f;
        [SerializeField] private float m_SwaySpeed = 5.5f;
        [Tooltip("Salınımın kat başına gecikmesi (radyan): üst küpler alttakileri biraz geriden izler.")]
        [SerializeField] private float m_SwayLag = 0.6f;
        [Tooltip("Her küpün kendi ekseninde rastgele dönüşü (derece): kule kusursuz hizalı durmasın.")]
        [SerializeField] private float m_RandomYaw = 7f;

        private readonly List<Transform> m_Cubes = new List<Transform>();
        private readonly List<float> m_Yaw = new List<float>();
        private float m_Phase;
        private float m_PhaseZ;

        public int Count => m_Cubes.Count;

        private void Awake()
        {
            // Her denizcinin yığını farklı kıvrılsın
            m_Phase = Random.Range(0f, Mathf.PI * 2f);
            m_PhaseZ = Random.Range(0f, Mathf.PI * 2f);
        }

        /// <summary>Küpü yığının en üstüne ekler. <paramref name="unitScale"/>: mesh'i bir küp kenarı boyuna getiren ölçek.</summary>
        public void Add(Transform cube, float unitScale = 1f)
        {
            if (cube == null) return;
            cube.SetParent(transform, false);
            cube.localRotation = Quaternion.identity;
            m_Cubes.Add(cube);
            m_Yaw.Add(m_Cubes.Count == 1 ? 0f : Random.Range(-m_RandomYaw, m_RandomYaw));
            Place(m_Cubes.Count - 1, Time.time);

            // Yeni küp yığına "pop" ile otursun; ilk küp ellerde zaten büyüyerek belirir
            cube.localScale = Vector3.one * (0.35f * unitScale);
            cube.DOScale(Vector3.one * unitScale, 0.2f).SetEase(Ease.OutBack).SetLink(cube.gameObject);
        }

        private void LateUpdate()
        {
            float t = Time.time;
            for (int i = 0; i < m_Cubes.Count; i++) Place(i, t);
        }

        private void Place(int i, float t)
        {
            Transform c = m_Cubes[i];
            if (c == null) return;

            // Kıvrım iki yatay eksende birden (X ve Z, farklı faz/sıklık): denizci hangi yöne bakarsa baksın
            // yığın ekranda dümdüz görünmez. x(0) = z(0) = 0: alttaki küp ellerin tam ortasında kalır.
            float fz = m_SnakeFrequency * 0.8f;
            float snakeX = m_SnakeAmplitude * (Mathf.Sin(i * m_SnakeFrequency + m_Phase) - Mathf.Sin(m_Phase));
            float snakeZ = 0.8f * m_SnakeAmplitude * (Mathf.Sin(i * fz + m_PhaseZ) - Mathf.Sin(m_PhaseZ));
            float inv = 1f / Mathf.Max(0.01f, m_Spacing);
            float slopeX = m_SnakeAmplitude * m_SnakeFrequency * Mathf.Cos(i * m_SnakeFrequency + m_Phase) * inv;
            float slopeZ = 0.8f * m_SnakeAmplitude * fz * Mathf.Cos(i * fz + m_PhaseZ) * inv;

            float heightFactor = m_Cubes.Count > 1 ? (float)i / (m_Cubes.Count - 1) : 0f;
            float swayX = m_SwayAmplitude * heightFactor * Mathf.Sin(t * m_SwaySpeed - i * m_SwayLag + m_Phase);
            float swayZ = 0.6f * m_SwayAmplitude * heightFactor * Mathf.Sin(t * m_SwaySpeed * 0.8f - i * m_SwayLag + m_PhaseZ);

            c.localPosition = new Vector3(snakeX + swayX, i * m_Spacing, snakeZ + swayZ);
            // Küp kıvrımın teğetine göre hafif yatar ve kendi ekseninde biraz döner (ilk küp dik)
            float tiltZ = i == 0 ? 0f : -Mathf.Atan(slopeX) * Mathf.Rad2Deg * 0.6f;
            float tiltX = i == 0 ? 0f : Mathf.Atan(slopeZ) * Mathf.Rad2Deg * 0.6f;
            float yaw = i < m_Yaw.Count ? m_Yaw[i] : 0f;
            c.localRotation = Quaternion.Euler(tiltX, yaw, tiltZ);
        }
    }
}
