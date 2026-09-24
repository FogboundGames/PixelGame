using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelGame
{
    /// <summary>
    /// Panoyu çevreleyen modüler mavi ray çerçevesinin (PerimeterRails) dış sınırlarına
    /// yumuşak, ayarlanabilir ve 2.5D derinlik kazandıran sahte gölge (Fake Shadow) ekler.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TrackFakeShadow : MonoBehaviour
    {
        [Header("Gölge Rengi ve Opaklığı")]
        [SerializeField] private Color m_ShadowColor = new Color(0.002f, 0.004f, 0.012f, 0.95f);
        [Range(0f, 1f)]
        [SerializeField] private float m_Intensity = 1f;

        [Header("Gölge Boyutları (World Units)")]
        [Tooltip("Rayın dış kenarından dışarıya doğru yayılma mesafesi")]
        [Min(0.05f)]
        [SerializeField] private float m_ShadowSpread = 0.55f;

        [Tooltip("Ray ile gölge arasında boşluk kalmaması için rayın altına giren pay")]
        [Min(0f)]
        [SerializeField] private float m_InnerOverlap = 0.22f;

        [Tooltip("Işık açısından kaynaklanan 2.5D derinlik ofseti (X, Y)")]
        [SerializeField] private Vector2 m_ShadowOffset = new Vector2(0f, -0.10f);

        [Tooltip("Rayın arkasında kalması için Z ofseti (Camera -Z'de olduğu için pozitif Z arkadadır)")]
        [SerializeField] private float m_ZOffset = 0.02f;

        [Header("Yumuşaklık ve Kalite")]
        [Tooltip("Gölgenin dışarı doğru sönümlenme eğrisi (1: Doğrusal, 1.5-2.0: Yumuşak Gauss benzeri)")]
        [Range(0.5f, 3.5f)]
        [SerializeField] private float m_FalloffPower = 1.65f;

        [Tooltip("Gölge degrade geçişindeki eşmerkezli halka sayısı")]
        [Range(3, 16)]
        [SerializeField] private int m_RadialSteps = 10;

        [Tooltip("Köşe yaylarının yuvarlaklık kalitesi (segment sayısı)")]
        [Range(4, 24)]
        [SerializeField] private int m_CornerSegments = 16;

        [Header("Otomatik Senkronizasyon")]
        [Tooltip("İşaretlenirse rayların sınırlarını otomatik senkronize eder. Kapalı tutulduğunda sahnede ayarladığınız özel konum, ölçek ve ölçüler kesinlikle korunur.")]
        [SerializeField] private bool m_AutoSyncWithRails = false;

        [Header("Gölge Referans Boyutları ve Sınırları")]
        [SerializeField] private float m_LeftX = -2.7126f;
        [SerializeField] private float m_RightX = 2.6326f;
        [SerializeField] private float m_BottomY = -2.7326f;
        [SerializeField] private float m_TopY = 2.6526f;
        [SerializeField] private float m_CornerRadius = 0.30f;
        [SerializeField] private float m_TrackScale = 0.60f;
        [SerializeField] private float m_BaseZ = -0.12f;

        private Mesh m_Mesh;
        private MeshFilter m_MeshFilter;
        private MeshRenderer m_MeshRenderer;

        public Color shadowColor { get => m_ShadowColor; set { m_ShadowColor = value; RebuildMesh(); } }
        public float intensity { get => m_Intensity; set { m_Intensity = Mathf.Clamp01(value); RebuildMesh(); } }
        public float shadowSpread { get => m_ShadowSpread; set { m_ShadowSpread = Mathf.Max(0.02f, value); RebuildMesh(); } }
        public float innerOverlap { get => m_InnerOverlap; set { m_InnerOverlap = Mathf.Max(0f, value); RebuildMesh(); } }
        public Vector2 shadowOffset { get => m_ShadowOffset; set { m_ShadowOffset = value; RebuildMesh(); } }
        public float falloffPower { get => m_FalloffPower; set { m_FalloffPower = Mathf.Max(0.1f, value); RebuildMesh(); } }

        private void OnEnable()
        {
            EnsureComponents();
            if (m_AutoSyncWithRails)
            {
                SyncWithSceneRails();
            }
            RebuildMesh();
        }

        private void OnValidate()
        {
            EnsureComponents();
            RebuildMesh();
        }

        private void EnsureComponents()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshRenderer == null) m_MeshRenderer = GetComponent<MeshRenderer>();

            if (m_MeshRenderer != null)
            {
                m_MeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m_MeshRenderer.receiveShadows = false;

                if (m_MeshRenderer.sharedMaterial == null)
                {
                    m_MeshRenderer.sharedMaterial = GetOrCreateShadowMaterial();
                }
            }
        }

        public static Material GetOrCreateShadowMaterial()
        {
            const string matPath = "Assets/Materials/TrackFakeShadow_Mat.mat";
            #if UNITY_EDITOR
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null) return existing;
            #endif

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.name = "TrackFakeShadow_Mat";
            mat.renderQueue = 2990;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

            #if UNITY_EDITOR
            string dir = System.IO.Path.GetDirectoryName(matPath);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
            #endif

            return mat;
        }

        /// <summary>
        /// Sahnedeki rayların sınırlarına göre gölgenin referans ölçülerini senkronize eder.
        /// </summary>
        public void SyncWithSceneRails()
        {
            TruckDispatcher td = Object.FindFirstObjectByType<TruckDispatcher>();
            if (td != null && td.TryGetExistingRailBounds(out _, out float railW, out float railH, out float railRadius, out float railZ))
            {
                td.SetupPerimeterLoop();
                var loop = td.TrackLoop;
                if (loop != null)
                {
                    SetupBounds(loop.LeftX, loop.RightX, loop.BottomY, loop.TopY, loop.CornerRadius, td.ModularTrackScale, loop.Z);
                    return;
                }
            }

            // Ray objelerinden doğrudan sınır bul
            GameObject railsObj = GameObject.Find("PerimeterRails");
            if (railsObj != null)
            {
                Transform tl = railsObj.transform.Find("Corner_TL");
                Transform br = railsObj.transform.Find("Corner_BR");
                if (tl != null && br != null)
                {
                    float left = tl.position.x;
                    float top = tl.position.y;
                    float right = br.position.x;
                    float bottom = br.position.y;
                    float z = tl.position.z;
                    SetupBounds(left, right, bottom, top, 0.30f, 0.60f, z);
                }
            }
        }

        public void SetupBounds(float leftX, float rightX, float bottomY, float topY, float cornerRadius, float trackScale, float z)
        {
            m_LeftX = leftX;
            m_RightX = rightX;
            m_BottomY = bottomY;
            m_TopY = topY;
            m_CornerRadius = Mathf.Max(0.05f, cornerRadius);
            m_TrackScale = Mathf.Max(0.1f, trackScale);
            m_BaseZ = z;

            RebuildMesh();
        }

        [ContextMenu("🌑 Gölgeleri Yeniden Oluştur (Rebuild Mesh)")]
        public void RebuildMesh()
        {
            EnsureComponents();

            if (m_Mesh == null)
            {
                m_Mesh = new Mesh();
                m_Mesh.name = "Track_FakeShadow_Mesh";
                #if UNITY_EDITOR
                m_Mesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                #endif
            }
            else
            {
                m_Mesh.Clear();
            }

            float hw = m_TrackScale * 0.5f; // ray yarı kalınlığı
            float r = m_CornerRadius;       // ray merkez hattı köşe yarıçapı
            float baseOuterR = r + hw;      // rayın en dış sınırının köşe yay yarıçapı

            // 4 Köşe yay merkezleri
            Vector2 cTL = new Vector2(m_LeftX + r, m_TopY - r);
            Vector2 cTR = new Vector2(m_RightX - r, m_TopY - r);
            Vector2 cBR = new Vector2(m_RightX - r, m_BottomY + r);
            Vector2 cBL = new Vector2(m_LeftX + r, m_BottomY + r);

            int segs = Mathf.Max(3, m_CornerSegments);

            // Dış kontur noktaları (P_base) ve dışarı bakan birim normalleri (Normal)
            List<Vector2> basePts = new List<Vector2>(segs * 4 + 8);
            List<Vector2> normals = new List<Vector2>(segs * 4 + 8);

            // 1. Üst Kenar (Sağdan Sola)
            basePts.Add(new Vector2(cTR.x, m_TopY + hw));
            normals.Add(new Vector2(0f, 1f));

            basePts.Add(new Vector2(cTL.x, m_TopY + hw));
            normals.Add(new Vector2(0f, 1f));

            // 2. Sol Üst Köşe (Arc: 90° -> 180° / PI/2 -> PI)
            for (int i = 1; i <= segs; i++)
            {
                float a = Mathf.Lerp(Mathf.PI * 0.5f, Mathf.PI, (float)i / segs);
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                basePts.Add(cTL + dir * baseOuterR);
                normals.Add(dir);
            }

            // 3. Sol Kenar (Yukarıdan Aşağıya)
            basePts.Add(new Vector2(m_LeftX - hw, cBL.y));
            normals.Add(new Vector2(-1f, 0f));

            // 4. Sol Alt Köşe (Arc: 180° -> 270° / PI -> 1.5*PI)
            for (int i = 1; i <= segs; i++)
            {
                float a = Mathf.Lerp(Mathf.PI, Mathf.PI * 1.5f, (float)i / segs);
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                basePts.Add(cBL + dir * baseOuterR);
                normals.Add(dir);
            }

            // 5. Alt Kenar (Soldan Sağa)
            basePts.Add(new Vector2(cBR.x, m_BottomY - hw));
            normals.Add(new Vector2(0f, -1f));

            // 6. Sağ Alt Köşe (Arc: 270° -> 360° / 1.5*PI -> 2*PI)
            for (int i = 1; i <= segs; i++)
            {
                float a = Mathf.Lerp(Mathf.PI * 1.5f, Mathf.PI * 2f, (float)i / segs);
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                basePts.Add(cBR + dir * baseOuterR);
                normals.Add(dir);
            }

            // 7. Sağ Kenar (Aşağıdan Yukarıya)
            basePts.Add(new Vector2(m_RightX + hw, cTR.y));
            normals.Add(new Vector2(1f, 0f));

            // 8. Sağ Üst Köşe (Arc: 0° -> 90° / 0 -> 0.5*PI)
            for (int i = 1; i < segs; i++)
            {
                float a = Mathf.Lerp(0f, Mathf.PI * 0.5f, (float)i / segs);
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                basePts.Add(cTR + dir * baseOuterR);
                normals.Add(dir);
            }

            int count = basePts.Count;
            int steps = Mathf.Max(2, m_RadialSteps);

            // Z derinliği: rayın hemen arkasında
            float targetZ = m_BaseZ + m_ZOffset;

            // Her kontur noktası için iç ve dış sınırları hesapla
            Vector3[,] ringVerts = new Vector3[steps + 1, count];
            Color[] ringColors = new Color[steps + 1];

            for (int k = 0; k <= steps; k++)
            {
                float t = (float)k / steps;
                float falloff = Mathf.Pow(Mathf.Clamp01(1f - t), m_FalloffPower);
                Color c = m_ShadowColor;
                c.a *= m_Intensity * falloff;
                ringColors[k] = c;

                for (int i = 0; i < count; i++)
                {
                    Vector2 baseP = basePts[i];
                    Vector2 n = normals[i];

                    // t = 0: rayın altına doğru overlap payı kadar içte
                    // t = 1: rayın dışına doğru shadowSpread kadar dışta + düşey ofset
                    Vector2 pInner = baseP - n * m_InnerOverlap;
                    Vector2 pOuter = baseP + n * m_ShadowSpread + m_ShadowOffset;

                    Vector2 pt = Vector2.Lerp(pInner, pOuter, t);
                    ringVerts[k, i] = new Vector3(pt.x, pt.y, targetZ);
                }
            }

            // Vertex ve Triangle dizilerini oluştur
            int totalVerts = (steps + 1) * count;
            int totalTris = steps * count * 6;

            Vector3[] vertices = new Vector3[totalVerts];
            Color[] colors = new Color[totalVerts];
            Vector2[] uvs = new Vector2[totalVerts];
            int[] triangles = new int[totalTris];

            int vIdx = 0;
            for (int k = 0; k <= steps; k++)
            {
                float vCoord = (float)k / steps;
                Color col = ringColors[k];
                for (int i = 0; i < count; i++)
                {
                    vertices[vIdx] = ringVerts[k, i];
                    colors[vIdx] = col;
                    uvs[vIdx] = new Vector2((float)i / count, vCoord);
                    vIdx++;
                }
            }

            int tIdx = 0;
            for (int k = 0; k < steps; k++)
            {
                int rowCurrent = k * count;
                int rowNext = (k + 1) * count;

                for (int i = 0; i < count; i++)
                {
                    int nextI = (i + 1) % count;

                    int v0 = rowCurrent + i;
                    int v1 = rowCurrent + nextI;
                    int v2 = rowNext + nextI;
                    int v3 = rowNext + i;

                    triangles[tIdx++] = v0;
                    triangles[tIdx++] = v1;
                    triangles[tIdx++] = v2;

                    triangles[tIdx++] = v0;
                    triangles[tIdx++] = v2;
                    triangles[tIdx++] = v3;
                }
            }

            m_Mesh.vertices = vertices;
            m_Mesh.colors = colors;
            m_Mesh.uv = uvs;
            m_Mesh.triangles = triangles;
            m_Mesh.RecalculateBounds();

            if (m_MeshFilter != null)
            {
                m_MeshFilter.sharedMesh = m_Mesh;
            }
        }

        /// <summary>
        /// Sahnede veya belirtilen ray grubu altında TrackFakeShadow nesnesini bulur veya oluşturur.
        /// </summary>
        public static TrackFakeShadow EnsureShadow(Transform railsTransform, TruckDispatcher.PerimeterTrackLoop loop = null)
        {
            if (railsTransform == null)
            {
                GameObject rObj = GameObject.Find("PerimeterRails");
                if (rObj != null) railsTransform = rObj.transform;
            }

            if (railsTransform == null) return null;

            bool isNew = false;
            Transform found = railsTransform.Find("TrackFakeShadow");
            GameObject shadowObj;
            if (found == null)
            {
                Transform sibling = railsTransform.parent != null ? railsTransform.parent.Find("TrackFakeShadow") : null;
                if (sibling != null)
                {
                    shadowObj = sibling.gameObject;
                }
                else
                {
                    shadowObj = new GameObject("TrackFakeShadow");
                    #if UNITY_EDITOR
                    Undo.RegisterCreatedObjectUndo(shadowObj, "Create Track Fake Shadow");
                    #endif
                    shadowObj.transform.SetParent(railsTransform, false);
                    shadowObj.transform.SetAsFirstSibling();
                    isNew = true;
                }
            }
            else
            {
                shadowObj = found.gameObject;
                shadowObj.transform.SetAsFirstSibling();
            }

            TrackFakeShadow shadow = shadowObj.GetComponent<TrackFakeShadow>();
            if (shadow == null)
            {
                shadow = shadowObj.AddComponent<TrackFakeShadow>();
                isNew = true;
            }

            // Sadece YENİ bir nesne oluşturulduğunda varsayılan sınırları ata.
            // Sahnede zaten mevcutsa kullanıcının sahnedeki Transform konumuna ve ölçülerine asla dokunma!
            if (isNew)
            {
                if (loop != null)
                {
                    TruckDispatcher td = Object.FindFirstObjectByType<TruckDispatcher>();
                    float scale = td != null ? td.ModularTrackScale : 0.60f;
                    shadow.SetupBounds(loop.LeftX, loop.RightX, loop.BottomY, loop.TopY, loop.CornerRadius, scale, loop.Z);
                }
                else
                {
                    shadow.SyncWithSceneRails();
                }
            }
            else
            {
                shadow.RebuildMesh();
            }

            #if UNITY_EDITOR
            EditorUtility.SetDirty(shadowObj);
            EditorUtility.SetDirty(shadow);
            #endif

            return shadow;
        }

        #if UNITY_EDITOR
        [ContextMenu("🔄 Ray Ölçüleriyle Yeniden Eşitle (Sync Bounds From Rails)")]
        public void SyncBoundsFromRailsMenu()
        {
            SyncWithSceneRails();
            RebuildMesh();
            EditorUtility.SetDirty(this);
        }

        // [MenuItem("Tools/PixelGame/🌑 Mavi Ray Sahte Gölgesini Güncelle (Track Fake Shadow)", priority = 38)]
        public static void CreateOrUpdateShadowMenu()
        {
            TrackFakeShadow shadow = EnsureShadow(null);
            if (shadow != null)
            {
                shadow.RebuildMesh();
                Selection.activeGameObject = shadow.gameObject;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                Debug.Log("<color=#00FFAA><b>[TrackFakeShadow]</b></color> Mavi ray sahte gölgesi güncellendi ve seçildi!");
            }
            else
            {
                Debug.LogWarning("[TrackFakeShadow] Sahnede PerimeterRails bulunamadı! Önce rayları döşeyin.");
            }
        }
        #endif
    }
}
