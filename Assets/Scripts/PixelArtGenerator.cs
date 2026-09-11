using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelGame
{
    public enum SamplingMode
    {
        [InspectorName("🎯 Point (Keskin / Pixel Art - Önerilen)")]
        Point,

        [InspectorName("🌊 Bilinear (Yumuşak / Geçişli)")]
        Bilinear
    }

    /// <summary>
    /// Verilen bir piksel görselini okur ve MainCube prefab'larından
    /// sahnedeki mavi çerçeve (MainPlane) içine tam sığacak bir 3D piksel resmi oluşturur.
    /// 1:1 piksel uyumu, renk paleti özelleştirmesi ve canlı güncellemeleri destekler.
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    [AddComponentMenu("PixelGame/Pixel Art Generator")]
    public class PixelArtGenerator : MonoBehaviour
    {
        [Header("📦 Prefab & Hedefler")]
        [Tooltip("Oluşturulacak küp prefabı (MainCube.prefab)")]
        [SerializeField] private GameObject m_CubePrefab;

        [Tooltip("Piksel resmi yapılacak kaynak doku (örn. PixelArt_Raccoon)")]
        [SerializeField] private Texture2D m_SourceTexture;

        [Tooltip("Alternatif olarak Sprite seçilebilir")]
        [SerializeField] private Sprite m_SourceSprite;

        [Tooltip("Küplerin yerleştirileceği UI çerçevesi (Canvas altındaki MainPlane)")]
        [SerializeField] private RectTransform m_TargetFrameRect;

        [Tooltip("Hizalamada kullanılacak kamera (boş bırakılırsa Camera.main kullanılır)")]
        [SerializeField] private Camera m_WorldCamera;

        [Header("📋 Aktif Bölüm Verisi")]
        [Tooltip("Şu anda sahnede oluşturulan bölüm verisi (opsiyonel)")]
        [SerializeField] private PixelLevelData m_ActiveLevelData;

        [Header("📐 1:1 Doğal Piksel Uyumu")]
        [Tooltip("Görselin kendi piksel çözünürlüğünü 1:1 kullan (1 Piksel = 1 Küp). Görselin bozulmasını kesinlikle önler!")]
        [SerializeField] private bool m_UseNativeResolution = true;

        [Tooltip("Eğer 'Use Native Resolution' kapalıysa kullanılacak manuel ızgara boyutu (X: Sütun, Y: Satır)")]
        [SerializeField] private Vector2Int m_GridResolution = new Vector2Int(24, 24);

        [Header("🎨 Renk & Kalite Ayarları")]
        [Tooltip("Piksel okuma modu. Pixel Art için Point modu renklerin bulanıklaşmasını engeller.")]
        [SerializeField] private SamplingMode m_SamplingMode = SamplingMode.Point;

        [Tooltip("Renk parlaklığı (Scene ışıklarının küpleri karartmasını engeller)")]
        [Range(0.5f, 2.5f)]
        [SerializeField] private float m_ColorBrightness = 1.25f;

        [Tooltip("Renk doygunluğu (Sarı, mavi ve kahverengileri çok daha canlı ve zengin yapar)")]
        [Range(0f, 2.5f)]
        [SerializeField] private float m_ColorSaturation = 1.25f;

        [Tooltip("Renk kontrastı")]
        [Range(0.5f, 2f)]
        [SerializeField] private float m_ColorContrast = 1.05f;

        [Tooltip("Işıma yoğunluğu (Küp renklerinin arkadan aydınlatmalı gibi canlı parlamasını sağlar)")]
        [Range(0f, 2f)]
        [SerializeField] private float m_EmissionIntensity = 0.35f;

        [Header("🔲 Izgara & Küp Yerleşimi")]
        [Tooltip("Küpler arasındaki boşluk oranı (0 = bitişik, 0.04 = %4 boşluk ile ızgara görünümü)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float m_CubeSpacing = 0.04f;

        [Tooltip("Küplerin Z eksenindeki kalınlığı / derinliği (3D kabartma hissi)")]
        [Range(0.05f, 2f)]
        [SerializeField] private float m_CubeDepth = 0.4f;

        [Tooltip("Mavi çerçevenin iç payı (çerçevenin eğimli kenarlarına taşmaması için)")]
        [Range(0f, 0.3f)]
        [SerializeField] private float m_InnerPadding = 0.08f;

        [Tooltip("Küplerin 3D dünyadaki Z düzlemi mesafesi")]
        [SerializeField] private float m_TargetZ = 0f;

        [Tooltip("Şeffaf (alpha < 0.1) pikseller için küp oluşturulmasın mı?")]
        [SerializeField] private bool m_SkipTransparent = true;

        [Tooltip("Oyun başladığında otomatik oluştursun mu?")]
        [SerializeField] private bool m_GenerateOnStart = true;

        [Header("📂 Kapsayıcı (Container)")]
        [SerializeField] private Transform m_CubesContainer;

        // Properties
        public GameObject CubePrefab { get => m_CubePrefab; set => m_CubePrefab = value; }
        public Texture2D SourceTexture { get => m_SourceTexture; set => m_SourceTexture = value; }
        public Sprite SourceSprite { get => m_SourceSprite; set => m_SourceSprite = value; }
        public RectTransform TargetFrameRect { get => m_TargetFrameRect; set => m_TargetFrameRect = value; }
        public PixelLevelData ActiveLevelData { get => m_ActiveLevelData; set => m_ActiveLevelData = value; }
        public bool UseNativeResolution { get => m_UseNativeResolution; set => m_UseNativeResolution = value; }
        public Vector2Int GridResolution { get => m_GridResolution; set => m_GridResolution = value; }
        public float CubeSpacing { get => m_CubeSpacing; set => m_CubeSpacing = value; }
        public float CubeDepth { get => m_CubeDepth; set => m_CubeDepth = value; }
        public float InnerPadding { get => m_InnerPadding; set => m_InnerPadding = value; }
        public float ColorBrightness { get => m_ColorBrightness; set { m_ColorBrightness = value; UpdateExistingCubesLive(); } }
        public float ColorSaturation { get => m_ColorSaturation; set { m_ColorSaturation = value; UpdateExistingCubesLive(); } }
        public float ColorContrast { get => m_ColorContrast; set { m_ColorContrast = value; UpdateExistingCubesLive(); } }
        public float EmissionIntensity { get => m_EmissionIntensity; set { m_EmissionIntensity = value; UpdateExistingCubesLive(); } }
        public SamplingMode Sampling { get => m_SamplingMode; set => m_SamplingMode = value; }
        public bool SkipTransparent { get => m_SkipTransparent; set => m_SkipTransparent = value; }
        public Transform CubesContainer => m_CubesContainer;

        private void Start()
        {
            if (Application.isPlaying && m_GenerateOnStart)
            {
                if (m_CubesContainer == null || m_CubesContainer.childCount == 0)
                {
                    GeneratePixelArt();
                }
            }
        }

        private void OnValidate()
        {
            if (m_CubesContainer != null && m_CubesContainer.childCount > 0)
            {
                UpdateExistingCubesLive();
            }
        }

        /// <summary>
        /// Bir LevelData varlığını yükler, renk paletini hazırlar ve küpleri otomatik oluşturur.
        /// </summary>
        public void LoadLevel(PixelLevelData levelData)
        {
            if (levelData == null) return;

            m_ActiveLevelData = levelData;
            m_SourceTexture = levelData.LevelTexture;
            m_SourceSprite = levelData.LevelSprite;
            m_UseNativeResolution = levelData.UseNativeResolution;
            m_GridResolution = levelData.CustomResolution;
            m_CubeSpacing = levelData.CubeSpacing;
            m_CubeDepth = levelData.CubeDepth;
            m_InnerPadding = levelData.InnerPadding;
            m_ColorBrightness = levelData.ColorBrightness;
            m_ColorSaturation = levelData.ColorSaturation;
            m_EmissionIntensity = levelData.EmissionIntensity;
            m_SkipTransparent = levelData.SkipTransparent;

            // Palet boşsa otomatik çıkar
            if (levelData.ColorPalette.Count == 0 && levelData.GetActiveTexture() != null)
            {
                levelData.ExtractPaletteFromTexture();
            }

            GeneratePixelArt();
        }

        /// <summary>
        /// Sahnede var olan küplerin renklerini anında günceller.
        /// </summary>
        public void UpdateExistingCubesLive()
        {
            if (m_CubesContainer == null) return;

            Texture2D activeTex = GetActiveTexture();
            GetEffectiveGridSize(activeTex, out int cols, out int rows);

            PixelCube[] cubes = m_CubesContainer.GetComponentsInChildren<PixelCube>();
            foreach (var cube in cubes)
            {
                if (activeTex != null)
                {
                    Color rawColor = SampleRawColor(activeTex, cube.GridX, cube.GridY, cols, rows);
                    cube.SetColor(rawColor);
                }

                cube.UpdateColorAdjustments(m_ColorBrightness, m_ColorSaturation, m_ColorContrast, m_EmissionIntensity);
            }
        }

        /// <summary>
        /// Küpleri mavi çerçeveye göre hesaplayarak 1:1 piksel uyumuyla oluşturur.
        /// </summary>
        [ContextMenu("Piksel Resmi Oluştur")]
        public void GeneratePixelArt()
        {
            Texture2D activeTex = GetActiveTexture();
            if (activeTex == null)
            {
                Debug.LogWarning("[PixelArtGenerator] Kaynak doku seçilmedi!");
                return;
            }

            if (m_CubePrefab == null)
            {
                #if UNITY_EDITOR
                string[] guids = AssetDatabase.FindAssets("MainCube t:Prefab");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    m_CubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
                #endif

                if (m_CubePrefab == null)
                {
                    Debug.LogError("[PixelArtGenerator] MainCube prefab'ı bulunamadı!");
                    return;
                }
            }

            EnsureTargetFrameRect();
            if (m_TargetFrameRect == null)
            {
                Debug.LogError("[PixelArtGenerator] Hedef mavi çerçeve (MainPlane RectTransform) bulunamadı!");
                return;
            }

            Camera cam = GetActiveCamera();
            if (cam == null)
            {
                Debug.LogError("[PixelArtGenerator] Sahne kamerası (Camera.main) bulunamadı!");
                return;
            }

            // 1. Hedef dünya alanı sınırlarını hesapla
            if (!CalculateTargetWorldBounds(cam, out Vector3 worldCenter, out float worldWidth, out float worldHeight))
            {
                Debug.LogError("[PixelArtGenerator] Hedef çerçevenin dünya koordinatları hesaplanamadı!");
                return;
            }

            // 2. Izgara boyutlarını belirle (1:1 piksel koruması)
            GetEffectiveGridSize(activeTex, out int cols, out int rows);

            if (cols <= 0 || rows <= 0)
            {
                cols = 24;
                rows = 24;
            }

            // 3. Küp boyutunu ve ızgara yerleşimini hesapla (kare orantıyı korur)
            float cellSizeX = worldWidth / cols;
            float cellSizeY = worldHeight / rows;
            float cellSize = Mathf.Min(cellSizeX, cellSizeY);

            float totalWidth = cols * cellSize;
            float totalHeight = rows * cellSize;

            Vector3 startPos = new Vector3(
                worldCenter.x - totalWidth * 0.5f + cellSize * 0.5f,
                worldCenter.y - totalHeight * 0.5f + cellSize * 0.5f,
                m_TargetZ
            );

            Vector3 cubeScale = new Vector3(
                cellSize * (1f - m_CubeSpacing),
                cellSize * (1f - m_CubeSpacing),
                cellSize * m_CubeDepth
            );

            // 4. Eski küpleri temizle
            ClearCubes();

            // 5. Kapsayıcıyı hazırla
            EnsureContainer();

            // 6. Pikselleri oku ve küpleri oluştur
            int createdCount = 0;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    Color rawColor = SampleRawColor(activeTex, x, y, cols, rows);

                    // Şeffaf piksel kontrolü
                    if (m_SkipTransparent && rawColor.a < 0.1f)
                        continue;

                    Color adjustedColor = PixelCube.AdjustColor(rawColor, m_ColorBrightness, m_ColorSaturation, m_ColorContrast);

                    Vector3 pos = startPos + new Vector3(x * cellSize, y * cellSize, 0f);

                    GameObject cubeObj;
                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        cubeObj = (GameObject)PrefabUtility.InstantiatePrefab(m_CubePrefab, m_CubesContainer);
                        cubeObj.transform.position = pos;
                        cubeObj.transform.localScale = cubeScale;
                        cubeObj.name = $"Pixel_{x}_{y}";
                        Undo.RegisterCreatedObjectUndo(cubeObj, "Generate Pixel Cube");
                    }
                    else
                    #endif
                    {
                        cubeObj = Instantiate(m_CubePrefab, pos, Quaternion.identity, m_CubesContainer);
                        cubeObj.transform.localScale = cubeScale;
                        cubeObj.name = $"Pixel_{x}_{y}";
                    }

                    // PixelCube bileşeni ekle ve renklendir
                    PixelCube pixelCube = cubeObj.GetComponent<PixelCube>();
                    if (pixelCube == null)
                    {
                        pixelCube = cubeObj.AddComponent<PixelCube>();
                    }

                    pixelCube.Initialize(x, y, rawColor, m_EmissionIntensity);
                    pixelCube.ApplyColor(adjustedColor, m_EmissionIntensity);
                    createdCount++;
                }
            }

            Debug.Log($"<color=#00FFAA><b>[PixelArtGenerator]</b></color> Başarıyla {createdCount} adet küp oluşturuldu! ({cols}x{rows} ızgara)");
        }

        public void GetEffectiveGridSize(Texture2D tex, out int cols, out int rows)
        {
            if (m_UseNativeResolution && tex != null)
            {
                cols = tex.width;
                rows = tex.height;
                return;
            }

            cols = m_GridResolution.x > 0 ? m_GridResolution.x : (tex != null ? tex.width : 24);
            rows = m_GridResolution.y > 0 ? m_GridResolution.y : (tex != null ? tex.height : 24);
        }

        /// <summary>
        /// Mevcut tüm küpleri temizler.
        /// </summary>
        [ContextMenu("Küpleri Temizle")]
        public void ClearCubes()
        {
            if (m_CubesContainer == null) return;

            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                while (m_CubesContainer.childCount > 0)
                {
                    Transform child = m_CubesContainer.GetChild(0);
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
                return;
            }
            #endif

            for (int i = m_CubesContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(m_CubesContainer.GetChild(i).gameObject);
            }
        }

        public bool CalculateTargetWorldBounds(Camera cam, out Vector3 worldCenter, out float worldWidth, out float worldHeight)
        {
            worldCenter = Vector3.zero;
            worldWidth = 0f;
            worldHeight = 0f;

            if (m_TargetFrameRect == null || cam == null)
                return false;

            Canvas canvas = m_TargetFrameRect.GetComponentInParent<Canvas>();
            Vector3[] corners = new Vector3[4];
            m_TargetFrameRect.GetWorldCorners(corners);

            float camDist = Mathf.Abs(cam.transform.position.z - m_TargetZ);
            if (camDist < 0.1f) camDist = 10f;

            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                for (int i = 0; i < 4; i++)
                {
                    corners[i] = cam.ScreenToWorldPoint(new Vector3(corners[i].x, corners[i].y, camDist));
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 screenPoint = cam.WorldToScreenPoint(corners[i]);
                    corners[i] = cam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, camDist));
                }
            }

            Vector3 bottomLeft = corners[0];
            Vector3 topRight = corners[2];

            worldCenter = (bottomLeft + topRight) * 0.5f;
            worldWidth = Mathf.Abs(topRight.x - bottomLeft.x);
            worldHeight = Mathf.Abs(topRight.y - bottomLeft.y);

            float padX = worldWidth * m_InnerPadding;
            float padY = worldHeight * m_InnerPadding;

            worldWidth = Mathf.Max(0.1f, worldWidth - padX * 2f);
            worldHeight = Mathf.Max(0.1f, worldHeight - padY * 2f);

            return true;
        }

        private Color SampleRawColor(Texture2D tex, int gridX, int gridY, int totalCols, int totalRows)
        {
            #if UNITY_EDITOR
            if (!tex.isReadable)
            {
                EnsureTextureReadableEditor(tex);
            }
            #endif

            Color basePixel;

            // 1:1 Doğal Piksel Çözünürlüğü: Doğrudan piksel koordinatını oku (Sıfır bozulma!)
            if (totalCols == tex.width && totalRows == tex.height)
            {
                basePixel = tex.GetPixel(gridX, gridY);
            }
            else
            {
                float u = (gridX + 0.5f) / totalCols;
                float v = (gridY + 0.5f) / totalRows;

                if (m_SamplingMode == SamplingMode.Point)
                {
                    int px = Mathf.Clamp(Mathf.FloorToInt(u * tex.width), 0, tex.width - 1);
                    int py = Mathf.Clamp(Mathf.FloorToInt(v * tex.height), 0, tex.height - 1);
                    basePixel = tex.GetPixel(px, py);
                }
                else
                {
                    basePixel = tex.GetPixelBilinear(u, v);
                }
            }

            // Aktif level verisi varsa palet değişikliğini (recolor), ton kaydırmayı ve tint filtresini uygula
            if (m_ActiveLevelData != null)
            {
                basePixel = m_ActiveLevelData.ApplyColorPipeline(basePixel);
            }

            return basePixel;
        }

        #if UNITY_EDITOR
        private void EnsureTextureReadableEditor(Texture2D tex)
        {
            try
            {
                string path = AssetDatabase.GetAssetPath(tex);
                if (!string.IsNullOrEmpty(path))
                {
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && !importer.isReadable)
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }
                }
            }
            catch
            {
                // sessizce devam et
            }
        }
        #endif

        public Texture2D GetActiveTexture()
        {
            if (m_SourceTexture != null)
                return m_SourceTexture;

            if (m_SourceSprite != null && m_SourceSprite.texture != null)
                return m_SourceSprite.texture;

            return null;
        }

        private Camera GetActiveCamera()
        {
            if (m_WorldCamera != null)
                return m_WorldCamera;

            return Camera.main;
        }

        private void EnsureTargetFrameRect()
        {
            if (m_TargetFrameRect == null)
            {
                GameObject mainPlane = GameObject.Find("MainPlane");
                if (mainPlane != null)
                {
                    m_TargetFrameRect = mainPlane.GetComponent<RectTransform>();
                }
            }
        }

        private void EnsureContainer()
        {
            if (m_CubesContainer != null) return;

            Transform existing = transform.Find("PixelArtContainer");
            if (existing != null)
            {
                m_CubesContainer = existing;
                return;
            }

            GameObject containerObj = new GameObject("PixelArtContainer");
            containerObj.transform.SetParent(transform, false);
            m_CubesContainer = containerObj.transform;

            #if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(containerObj, "Create PixelArtContainer");
            #endif
        }

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Camera cam = GetActiveCamera();
            if (cam == null || m_TargetFrameRect == null) return;

            if (CalculateTargetWorldBounds(cam, out Vector3 center, out float width, out float height))
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(center, new Vector3(width, height, 0.1f));
            }
        }
        #endif
    }
}
