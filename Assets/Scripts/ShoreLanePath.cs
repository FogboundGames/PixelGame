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
        /// İki nokta arasında hiçbir yere sapmadan, dosdoğru giden doğrudan yürüyüş hattı oluşturur.
        /// </summary>
        public static ShoreLanePath BuildDirect(Vector3 start, Vector3 end, int samples = 32)
        {
            samples = Mathf.Max(2, samples);
            var pts = new Vector3[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;
                pts[i] = Vector3.Lerp(start, end, t);
            }
            return new ShoreLanePath(pts);
        }

        /// <summary>
        /// Küpün panodaki anlık konumundan hedef iskeleye, görselin içinden geçmeden
        /// dış konturundan dolaşarak (veya doğrudan) pürüzsüz yürüyüş yolu oluşturur.
        /// </summary>
        public static ShoreLanePath BuildAroundObstacle(Vector3 startPos, float shipX, float boardMinX, float boardMaxX, float boardBottomY, float pierY, float pierSurfaceZ)
        {
            float centerX = (boardMinX + boardMaxX) * 0.5f;
            bool isVeryBottom = startPos.y <= (boardBottomY + 0.15f);

            // Ana görselin üzerinde duran küpler için asla doğrudan merkez çizgisine izin verilmez.
            // Bu safha, küpün tam üzerindeki resim alanını delerek geçmesini engeller.
            bool isAboveBoardVisual = startPos.y > (boardBottomY + 0.15f);

            // Hedefe giden düz hat ana görselin merkezinden/içinden geçiyor mu?
            bool needsContour = !isVeryBottom || isAboveBoardVisual;
            if (startPos.x >= centerX && shipX >= (startPos.x - 0.25f) && !isAboveBoardVisual)
            {
                needsContour = false;
            }
            else if (startPos.x < centerX && shipX <= (startPos.x + 0.25f) && !isAboveBoardVisual)
            {
                needsContour = false;
            }

            Vector3 pierLandingPos = new Vector3(shipX, pierY, pierSurfaceZ);

            if (!needsContour)
            {
                // Görselin içinden geçme riski yok: doğrudan hedef iskeleye yürü
                return BuildDirect(startPos, pierLandingPos, 32);
            }

            // Görselin içinden geçmek yerine dış konturundan (etrafından) dolaş
            bool goRight = (startPos.x >= centerX);
            const float contourMargin = 0.22f; // Görselin hemen ~1 küp dışı
            float contourX = goRight ? (boardMaxX + contourMargin) : (boardMinX - contourMargin);
            float sandY = boardBottomY - 0.25f;

            float elbowDrop = Mathf.Min(0.25f, (startPos.y - sandY) * 0.20f);
            System.Collections.Generic.List<Vector3> pts = new System.Collections.Generic.List<Vector3>(48);

            // 1. Dirsek: Kendi konumundan dış kontur çizgisine yumuşak geçiş (Y sürekli azalır, asla geriye gitmez)
            const int elbowSamples = 12;
            for (int i = 0; i <= elbowSamples; i++)
            {
                float t = (float)i / elbowSamples;
                float st = t * t * (3f - 2f * t); // SmoothStep
                float x = Mathf.Lerp(startPos.x, contourX, st);
                float y = Mathf.Lerp(startPos.y, startPos.y - elbowDrop, t);
                float z = Mathf.Lerp(startPos.z, pierSurfaceZ, st);
                pts.Add(new Vector3(x, y, z));
            }

            // 2. Dış Kontur: Görselin dış bordüründen kumsala (sandY) kadar iniş
            float contourLen = (startPos.y - elbowDrop) - sandY;
            if (contourLen > 0.05f)
            {
                int samplesDown = Mathf.Max(4, Mathf.RoundToInt(contourLen / 0.15f));
                for (int i = 1; i <= samplesDown; i++)
                {
                    float t = (float)i / samplesDown;
                    float y = Mathf.Lerp(startPos.y - elbowDrop, sandY, t);
                    pts.Add(new Vector3(contourX, y, pierSurfaceZ));
                }
            }

            // 3. Kumsal: Görselin altındaki açık kumsaldan geminin iskelesine yumuşak kavis
            const int sandSamples = 16;
            for (int i = 1; i <= sandSamples; i++)
            {
                float t = (float)i / sandSamples;
                float stX = t * t * (3f - 2f * t);
                float x = Mathf.Lerp(contourX, shipX, stX);
                float y = Mathf.Lerp(sandY, pierY, t);
                pts.Add(new Vector3(x, y, pierSurfaceZ));
            }

            return new ShoreLanePath(pts.ToArray());
        }

        /// <summary>
        /// Verilen kontrol noktalarından geçen doğrusal (polyline) yürüyüş hattı oluşturur.
        /// Segmentler maxStep aralıklarla yoğun şekilde örneklenir; böylece Catmull-Rom eğrilerinin
        /// köşelerde engel içine taşması (overshoot) önlenir ve küpler boş alanlar boyunca
        /// hiçbir pikselin üstünden geçmeden yürür.
        /// </summary>
        public static ShoreLanePath BuildLinear(System.Collections.Generic.IList<Vector3> waypoints, float maxStep = 0.04f)
        {
            if (waypoints == null || waypoints.Count < 2)
                return new ShoreLanePath(new[] { Vector3.zero, Vector3.zero });

            var pts = new System.Collections.Generic.List<Vector3>(waypoints.Count * 8);
            pts.Add(waypoints[0]);

            float stepSize = Mathf.Max(0.005f, maxStep);
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Vector3 a = waypoints[i];
                Vector3 b = waypoints[i + 1];
                float dist = Vector3.Distance(a, b);
                if (dist <= 1e-5f) continue;

                int steps = Mathf.Max(1, Mathf.CeilToInt(dist / stepSize));
                for (int s = 1; s <= steps; s++)
                {
                    float t = (float)s / steps;
                    pts.Add(Vector3.Lerp(a, b, t));
                }
            }

            if (pts.Count < 2) pts.Add(waypoints[waypoints.Count - 1]);
            return new ShoreLanePath(pts.ToArray());
        }

        /// <summary>
        /// Verilen kontrol noktalarından geçen, ancak KÖŞELERİ DOĞAL VE YUMUŞAK BİR BEZIER KAVİSLE
        /// (Fillet) yuvarlatılmış akıcı yürüyüş hattı oluşturur.
        /// Köşeler dışarıya asla taşmaz (overshoot yapmaz; konveks üçgenin içinde kalır),
        /// ancak sivri 90° kırılmalar ortadan kalktığı için küpler virajlarda titremeden,
        /// kayarak doğal bir kavisle döner.
        /// </summary>
        public static ShoreLanePath BuildFilleted(System.Collections.Generic.IList<Vector3> waypoints, float cornerRadius = 0.14f, float maxStep = 0.025f)
        {
            if (waypoints == null || waypoints.Count < 2)
                return new ShoreLanePath(new[] { Vector3.zero, Vector3.zero });

            // 1. Birbirine aşırı yakın ardışık noktaları temizle
            var clean = new System.Collections.Generic.List<Vector3>(waypoints.Count);
            clean.Add(waypoints[0]);
            for (int i = 1; i < waypoints.Count; i++)
            {
                if ((waypoints[i] - clean[clean.Count - 1]).sqrMagnitude > 1e-4f)
                {
                    clean.Add(waypoints[i]);
                }
            }

            if (clean.Count < 2)
                return new ShoreLanePath(new[] { waypoints[0], waypoints[waypoints.Count - 1] });

            if (clean.Count == 2)
                return BuildLinear(clean, maxStep);

            // 2. Doğrusal noktaları (aradaki aynı doğrultudaki gereksiz noktaları) ayıkla
            var corners = new System.Collections.Generic.List<Vector3>(clean.Count);
            corners.Add(clean[0]);
            for (int i = 1; i < clean.Count - 1; i++)
            {
                Vector3 prev = corners[corners.Count - 1];
                Vector3 curr = clean[i];
                Vector3 next = clean[i + 1];

                Vector3 d1 = (curr - prev).normalized;
                Vector3 d2 = (next - curr).normalized;
                // Doğrultu neredeyse aynıysa (aynı düz çizgi) köşe kabul etme
                if (Vector3.Dot(d1, d2) > 0.998f)
                    continue;

                corners.Add(curr);
            }
            corners.Add(clean[clean.Count - 1]);

            if (corners.Count <= 2)
                return BuildLinear(corners, maxStep);

            // 3. Her köşe için Bezier Fillet üret
            var pts = new System.Collections.Generic.List<Vector3>(corners.Count * 16);
            pts.Add(corners[0]);

            float stepSize = Mathf.Max(0.005f, maxStep);

            for (int i = 0; i < corners.Count - 1; i++)
            {
                Vector3 pA = corners[i];
                Vector3 pB = corners[i + 1];

                // pA noktasından çıkış
                Vector3 startPt = pA;
                if (i > 0)
                {
                    Vector3 prevCorner = corners[i - 1];
                    Vector3 nextCorner = corners[i + 1];
                    float lenPrev = Vector3.Distance(prevCorner, pA);
                    float lenNext = Vector3.Distance(pA, nextCorner);
                    float r = Mathf.Min(cornerRadius, lenPrev * 0.42f, lenNext * 0.42f);
                    startPt = pA + (nextCorner - pA).normalized * r;
                }

                // pB noktasına varış
                Vector3 endPt = pB;
                bool hasNextCorner = (i + 1 < corners.Count - 1);
                float rNext = 0f;
                if (hasNextCorner)
                {
                    Vector3 afterNext = corners[i + 2];
                    float lenThis = Vector3.Distance(pA, pB);
                    float lenAfter = Vector3.Distance(pB, afterNext);
                    rNext = Mathf.Min(cornerRadius, lenThis * 0.42f, lenAfter * 0.42f);
                    endPt = pB - (pB - pA).normalized * rNext;
                }

                // Düz çizgi kısmı: startPt -> endPt
                float straightDist = Vector3.Distance(startPt, endPt);
                if (straightDist > 1e-4f)
                {
                    int straightSteps = Mathf.Max(1, Mathf.CeilToInt(straightDist / stepSize));
                    for (int s = 1; s <= straightSteps; s++)
                    {
                        float t = (float)s / straightSteps;
                        pts.Add(Vector3.Lerp(startPt, endPt, t));
                    }
                }

                // Köşe kavis kısmı (pB köşesinde yuvarlama): endPt -> pB -> nextStart
                if (hasNextCorner && rNext > 1e-4f)
                {
                    Vector3 afterNext = corners[i + 2];
                    Vector3 nextStart = pB + (afterNext - pB).normalized * rNext;

                    // Quadratic Bezier: endPt (start), pB (control), nextStart (end)
                    float arcApprox = Vector3.Distance(endPt, pB) + Vector3.Distance(pB, nextStart);
                    int arcSteps = Mathf.Max(4, Mathf.CeilToInt(arcApprox / stepSize));
                    for (int s = 1; s <= arcSteps; s++)
                    {
                        float t = (float)s / arcSteps;
                        float u = 1f - t;
                        Vector3 arcPt = u * u * endPt + 2f * u * t * pB + t * t * nextStart;
                        pts.Add(arcPt);
                    }
                }
            }

            if (pts.Count < 2) pts.Add(corners[corners.Count - 1]);
            return new ShoreLanePath(pts.ToArray());
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
                    pts.Add(CentripetalCatmullRom(p0, p1, p2, p3, t));
                }
            }
            pts.Add(waypoints[n - 1]);

            return new ShoreLanePath(pts.ToArray());
        }

        /// <summary>
        /// Centripetal Catmull-Rom (alpha = 0.5): Segmentler arası mesafeye duyarlı parametrelendirme.
        /// Standart uniform Catmull-Rom'un aksine virajlarda ve ani dönüşlerde ASLA ilmik (loop),
        /// sivri uç (cusp) veya geriye taşma (retrograde overshoot) oluşturmaz.
        /// </summary>
        private static Vector3 CentripetalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            const float alpha = 0.5f;
            float d01 = Mathf.Pow(Mathf.Max(1e-4f, (p1 - p0).sqrMagnitude), alpha * 0.5f);
            float d12 = Mathf.Pow(Mathf.Max(1e-4f, (p2 - p1).sqrMagnitude), alpha * 0.5f);
            float d23 = Mathf.Pow(Mathf.Max(1e-4f, (p3 - p2).sqrMagnitude), alpha * 0.5f);

            float t0 = 0f;
            float t1 = t0 + d01;
            float t2 = t1 + d12;
            float t3 = t2 + d23;

            float curT = Mathf.Lerp(t1, t2, t);

            Vector3 a1 = (t1 - curT) / (t1 - t0) * p0 + (curT - t0) / (t1 - t0) * p1;
            Vector3 a2 = (t2 - curT) / (t2 - t1) * p1 + (curT - t1) / (t2 - t1) * p2;
            Vector3 a3 = (t3 - curT) / (t3 - t2) * p2 + (curT - t2) / (t3 - t2) * p3;

            Vector3 b1 = (t2 - curT) / (t2 - t0) * a1 + (curT - t0) / (t2 - t0) * a2;
            Vector3 b2 = (t3 - curT) / (t3 - t1) * a2 + (curT - t1) / (t3 - t1) * a3;

            return (t2 - curT) / (t2 - t1) * b1 + (curT - t1) / (t2 - t1) * b2;
        }

        /// <summary>
        /// Örnek noktasının şerit başından uzaklığı. <see cref="BuildThrough"/> ile kurulan
        /// şeritte i. ara nokta, i * samplesPerSegment indeksinde durur.
        /// </summary>
        public float DistanceAtIndex(int index)
        {
            return m_Cumulative[Mathf.Clamp(index, 0, m_Cumulative.Length - 1)];
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

        /// <summary>Şerit üzerinde verilen mesafedeki teğet (ilerleme yönü) birim vektörü.</summary>
        public Vector3 TangentAtDistance(float distance)
        {
            float total = Length;
            if (total <= 0.001f || m_Points.Length < 2) return Vector3.down;

            float d0 = Mathf.Clamp(distance, 0f, Mathf.Max(0f, total - 0.02f));
            float d1 = Mathf.Clamp(distance + 0.05f, 0.01f, total);
            Vector3 p0 = PointAtDistance(d0);
            Vector3 p1 = PointAtDistance(d1);
            Vector3 delta = p1 - p0;
            return delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector3.down;
        }
    }
}
