using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Panodan kıyıya giden ORTAK yürüyüş şeridi.
    ///
    /// Eskiden her küp kendi panodaki yerinden kıyıya kendi düz çizgisiyle gidiyordu;
    /// yol uzunlukları 2.35 ile 7.37 birim arasında değiştiği için küpler yelpaze gibi
    /// açılıyor, tek sıra oluşmuyordu. Referans oyunda bütün küpler tek bir eğriyi
    /// takip ediyor ve birbirine değecek şekilde dizilip akıyor.
    ///
    /// Bu sınıf o eğriyi kübik Bezier olarak kurup yay uzunluğuna göre örnekler;
    /// böylece <see cref="PointAtDistance"/> sabit hızda ilerleme verir (parametreye
    /// göre örnekleme yapılsaydı küp virajlarda yavaşlayıp düzlükte hızlanırdı).
    /// </summary>
    public class ShoreLanePath
    {
        private readonly Vector3[] m_Points;
        private readonly float[] m_Cumulative;

        public float Length => m_Cumulative[m_Cumulative.Length - 1];
        public Vector3 Start => m_Points[0];
        public Vector3 End => m_Points[m_Points.Length - 1];

        private ShoreLanePath(Vector3[] pts)
        {
            m_Points = pts;
            m_Cumulative = new float[pts.Length];
            m_Cumulative[0] = 0f;
            for (int i = 1; i < pts.Length; i++)
                m_Cumulative[i] = m_Cumulative[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
        }

        /// <summary>
        /// Şeridi kurar. Başlangıçta YATAY çıkar (panonun alt kenarı boyunca), sonra
        /// kıyıya doğru kıvrılır — böylece ne şerit ne de arkasındaki bekleme kuyruğu
        /// piksel resmin önüne biner. <paramref name="startTangent"/> şeridin ilk
        /// yönüdür (normalize edilmiş olmalı).
        /// </summary>
        public static ShoreLanePath Build(Vector3 start, Vector3 end, Vector3 startTangent, float bend, int samples = 64)
        {
            samples = Mathf.Max(8, samples);

            float span = Vector3.Distance(start, end);
            // İlk kontrol noktası başlangıç teğeti boyunca: eğri buradan YATAY çıkar.
            Vector3 c1 = start + startTangent.normalized * Mathf.Max(0.1f, span * 0.45f);
            // İkinci kontrol noktası kıyıya dikeyce yaklaştırır.
            Vector3 c2 = end + new Vector3(0f, Mathf.Max(0.1f, span * 0.35f) + bend, 0f);

            var pts = new Vector3[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;
                float u = 1f - t;
                pts[i] = u * u * u * start
                       + 3f * u * u * t * c1
                       + 3f * u * t * t * c2
                       + t * t * t * end;
            }

            return new ShoreLanePath(pts);
        }

        /// <summary>
        /// Verilen noktalardan GEÇEN yumuşak eğri (Catmull-Rom). Hiçbir parçası düz
        /// çizgi olmaz; köşelerde teğetler komşu noktalardan türetildiği için geçişler
        /// sürekli olur.
        /// </summary>
        public static ShoreLanePath BuildThrough(System.Collections.Generic.IList<Vector3> waypoints, int samplesPerSegment = 24)
        {
            if (waypoints == null || waypoints.Count < 2)
                return new ShoreLanePath(new[] { Vector3.zero, Vector3.zero });

            int n = waypoints.Count;
            var pts = new System.Collections.Generic.List<Vector3>((n - 1) * samplesPerSegment + 1);

            for (int i = 0; i < n - 1; i++)
            {
                // Uç segmentlerde komşu nokta yoksa yansıtarak türet.
                Vector3 p0 = i == 0 ? waypoints[0] * 2f - waypoints[1] : waypoints[i - 1];
                Vector3 p1 = waypoints[i];
                Vector3 p2 = waypoints[i + 1];
                Vector3 p3 = (i + 2 < n) ? waypoints[i + 2] : waypoints[n - 1] * 2f - waypoints[n - 2];

                for (int k = 0; k < samplesPerSegment; k++)
                {
                    float t = (float)k / samplesPerSegment;
                    pts.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }
            pts.Add(waypoints[n - 1]);

            return new ShoreLanePath(pts.ToArray());
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>Şerit başından <paramref name="distance"/> kadar ileride olan nokta.</summary>
        public Vector3 PointAtDistance(float distance)
        {
            float total = Length;
            if (distance <= 0f) return m_Points[0];
            if (distance >= total) return m_Points[m_Points.Length - 1];

            // Kümülatif uzunlukta ikili arama
            int lo = 0, hi = m_Cumulative.Length - 1;
            while (lo + 1 < hi)
            {
                int mid = (lo + hi) / 2;
                if (m_Cumulative[mid] <= distance) lo = mid; else hi = mid;
            }

            float segLen = m_Cumulative[hi] - m_Cumulative[lo];
            float f = segLen > 0.0001f ? (distance - m_Cumulative[lo]) / segLen : 0f;
            return Vector3.Lerp(m_Points[lo], m_Points[hi], f);
        }

        /// <summary>Verilen noktaya en yakın şerit noktasının baştan uzaklığı.</summary>
        public float ClosestDistance(Vector3 world)
        {
            float best = 0f, bestSq = float.MaxValue;
            for (int i = 0; i < m_Points.Length; i++)
            {
                float sq = (m_Points[i] - world).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = m_Cumulative[i]; }
            }
            return best;
        }
    }
}
