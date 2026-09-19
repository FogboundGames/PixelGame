using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame
{
    /// <summary>
    /// Bir park yeri şeridinin görsel ayarları.
    /// Sayılar (kaç sütun, kaç sıra) buraya değil bölüm verisine aittir;
    /// burada yalnızca boyut, aralık ve görünüm durur.
    /// </summary>
    [Serializable]
    public class TruckPlaceStyle
    {
        [Tooltip("Park yeri görseli. Havuzda genellikle çizilmez.")]
        public Sprite sprite;

        [Tooltip("Tek bir park yerinin referans çözünürlükteki kenarı")]
        public float cellSize = 400f;

        [Tooltip("Yan yana duran yerler arası boşluk")]
        public float gap = 16f;

        [Tooltip("Alt alta duran sıralar arası boşluk")]
        public float gapY = 0f;

        [Tooltip("Sıralar arası derinlik farkı. Alt sıra kameraya yaklaşır, " +
                 "kamyonlar iç içe değil arka arkaya dizilmiş görünür.")]
        public float stepZ = 0f;

        [Tooltip("Park yerinin X ekseni etrafındaki eğimi. Perspektif kamerada " +
                 "yere serilmiş bir zemin parçası gibi yamuk görünür.")]
        public float tilt = 45f;

        [Tooltip("Kamyonun park yeri içindeki duruşu (local Euler)")]
        public Vector3 truckEuler = new Vector3(-90f, 90f, -90f);

        [Tooltip("Park yeri görseli çizilsin mi? Havuzda kapalıdır: yalnızca kamyonlar görünür.")]
        public bool showSprite = true;

        [Tooltip("Bu şeritteki yerler tıklanabilir mi? Havuzda açıktır.")]
        public bool interactive;

        [Tooltip("Şeridin ekran genişliğinin en fazla ne kadarını kaplayacağı")]
        public float rowWidthFill = 0.96f;

        [Header("🛤️ Ray")]
        [Tooltip("Her park yerinin altına konacak ray parçası prefabı. " +
                 "Yan yana duran parçalar kesintisiz bir hat oluşturur. " +
                 "Havuzda genellikle boş bırakılır.")]
        public GameObject groundPrefab;

        [Tooltip("Ray parçasının park yeri içindeki duruşu (local Euler). " +
                 "Elle bulunup doğrulanmış değer.")]
        public Vector3 groundEuler = new Vector3(0f, -90f, -270f);

        [Header("⛏️ Maden Portalı")]
        [Tooltip("Rayın iki ucuna konacak maden girişi prefabı. " +
                 "Vagonlar bu portaldan geçtikten sonra kaybolur.")]
        public GameObject portalPrefab;

        [Tooltip("Portalın duruşu (local Euler). Sağdaki portal buna 180 derece eklenerek " +
                 "karşıya baktırılır.")]
        public Vector3 portalEuler = new Vector3(0f, -90f, -270f);

        [Tooltip("Portalın ölçeği. Otomatik sığdırma yerine sabit verilir: portal modelinin " +
                 "dar ekseninden ölçmek ölçeği şişiriyordu.")]
        public float portalScale = 200f;

        [Tooltip("Portalın şerit ucuna göre yeri. Negatif değer portalı içe alır: " +
                 "şerit ekranın neredeyse tamamını kapladığı için dışarıda kalan portal " +
                 "ekran kenarına düşüp görünmez oluyordu.")]
        public float portalMargin = -40f;

        [Header("🌑 Slot Gölgesi (Slot Fake Shadow)")]
        [Tooltip("Slotların altına yumuşak sahte gölge ekler.")]
        public bool enableShadow = true;

        [Tooltip("Bireysel slot gölge görseli. Boşsa Assets/UI/SlotShadow.png kullanılır.")]
        public Sprite shadowSprite;

        [Tooltip("Slot gölgesinin rengi ve opaklığı.")]
        public Color shadowColor = new Color(0.04f, 0.06f, 0.14f, 0.58f);

        [Tooltip("Slot gölgesinin X ve Y ofseti.")]
        public Vector2 shadowOffset = new Vector2(0f, -14f);

        [Tooltip("Slot gölgesinin boyut çarpanı (genişleme oranı).")]
        public Vector2 shadowScale = new Vector2(1.04f, 1.04f);

        [Tooltip("Gölgenin slot yüzeyinin arkasında kalacağı Z derinliği.")]
        public float shadowZ = 4f;

        [Header("🌑 Şerit Zemin Gölgesi (Row Ground Shadow)")]
        [Tooltip("Tüm slot şeridinin ve portalların altına zemini saran yumuşak gölge şeridi ekler.")]
        public bool enableRowGroundShadow = true;

        [Tooltip("Şerit zemin gölgesi görseli. Boşsa Assets/UI/RowGroundShadow.png kullanılır.")]
        public Sprite rowGroundShadowSprite;

        [Tooltip("Şerit zemin gölgesinin rengi ve opaklığı.")]
        public Color rowGroundShadowColor = new Color(0.02f, 0.03f, 0.05f, 0.42f);

        [Tooltip("Şerit zemin gölgesinin Y ofseti.")]
        public Vector2 rowGroundShadowOffset = new Vector2(0f, -16f);

        [Tooltip("Şerit zemin gölgesinin genişlik ve yükseklik payı.")]
        public Vector2 rowGroundShadowPadding = new Vector2(320f, 60f);

        [Tooltip("Şerit zemin gölgesinin Z derinliği.")]
        public float rowGroundShadowZ = 8f;

        [Header("⛏️ Maden Portalı Gölgeleri (Portal Fake Shadows)")]
        [Tooltip("Sol ve sağ maden portalının altına temas gölgesi ekler.")]
        public bool enablePortalShadow = true;

        [Tooltip("Portal gölge görseli. Boşsa Assets/UI/PortalShadow.png kullanılır.")]
        public Sprite portalShadowSprite;

        [Tooltip("Portal gölgesinin rengi ve opaklığı.")]
        public Color portalShadowColor = new Color(0.02f, 0.03f, 0.06f, 0.52f);

        [Tooltip("Portal gölgesinin X ve Y ofseti.")]
        public Vector2 portalShadowOffset = new Vector2(0f, -14f);

        [Tooltip("Portal gölgesinin boyut çarpanı.")]
        public Vector2 portalShadowScale = new Vector2(1.12f, 1.12f);

        [Tooltip("Portal gölgesinin Z derinliği.")]
        public float portalShadowZ = 4f;
    }

    /// <summary>
    /// Park yeri şeritlerini (doldurma slotları ve bekleme havuzu) kurar.
    ///
    /// Hem editör kurulumu hem de oyun içi yeniden kurulum aynı kodu kullanır;
    /// böylece bölüm verisinden gelen sütun/sıra sayısı değiştiğinde şerit
    /// sahnedekiyle birebir aynı şekilde yeniden üretilir.
    /// </summary>
    public static class TruckPlaceBuilder
    {
        /// <summary>Canvas referans genişliği; sığdırma hesabı buna göre yapılır.</summary>
        public const float ReferenceWidth = 1920f;

        /// <summary>
        /// Şeridi verilen sütun/sıra sayısına göre sıfırdan kurar ve
        /// oluşan park yerlerini döndürür. Var olan yerler silinir.
        /// </summary>
        public static List<TruckSlot> Build(RectTransform row, TruckPlaceStyle style,
                                            int columns, int rows, string namePrefix)
        {
            var result = new List<TruckSlot>();
            if (row == null || style == null) return result;

            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);

            if (style.showSprite && style.sprite == null)
            {
                Debug.LogWarning(
                    $"[TruckPlaceBuilder] '{row.name}' şeridinde park yeri görseli atanmamış; " +
                    "yerler boş beyaz kare olarak çizilecek. " +
                    "Tools > PixelGame > Kamyon Döngüsünü Kur menüsünü çalıştırarak görünüm ayarlarını doldur.",
                    row);
            }

            ClearChildren(row);

            float totalWidth = columns * style.cellSize + (columns - 1) * style.gap;
            float totalHeight = rows * style.cellSize + (rows - 1) * style.gapY;

            row.sizeDelta = new Vector2(totalWidth, totalHeight);

            // Raylar, portallar ve vagonlar her seviyede daima sabit ve özgün boyutta kalmalıdır.
            row.localScale = Vector3.one;

            float startX = -totalWidth * 0.5f + style.cellSize * 0.5f;
            float startY = totalHeight * 0.5f - style.cellSize * 0.5f;

            Quaternion truckRotation = Quaternion.Euler(style.truckEuler);
            int index = 0;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    float x = startX + c * (style.cellSize + style.gap);
                    float y = startY - r * (style.cellSize + style.gapY);

                    GameObject obj = new GameObject($"{namePrefix}_{index + 1}", typeof(RectTransform));
                    obj.transform.SetParent(row, false);

                    RectTransform rect = obj.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(style.cellSize, style.cellSize);
                    // Alt sıralar kameraya doğru gelsin: negatif Z öne çeker
                    rect.anchoredPosition3D = new Vector3(x, y, -r * style.stepZ);
                    // Yere yatır: perspektif kamera bunu yamuk gösterir
                    rect.localRotation = Quaternion.Euler(style.tilt, 0f, 0f);

                    Image image = obj.AddComponent<Image>();

                    if (style.showSprite)
                    {
                        image.sprite = style.sprite;
                        image.preserveAspect = true;
                    }
                    else
                    {
                        // Görünmez ama tıklanabilir yüzey: havuzda park yeri çizilmez,
                        // yalnızca kamyonun kendisi görünür
                        image.sprite = null;
                        image.color = new Color(1f, 1f, 1f, 0f);

                        // Tamamen saydam bir UI elemanının mesh'i varsayılan olarak atılır
                        // ve o zaman tıklama da almaz; bu yüzden atılmasını engelliyoruz
                        image.canvasRenderer.cullTransparentMesh = false;
                    }

                    image.raycastTarget = style.interactive;

                    TruckSlot slot = obj.AddComponent<TruckSlot>();
                    slot.Configure(rect, truckRotation);

                    // Ray parçası: park yerinin altında sabit durur, vagon gelip gitse de kalır
                    if (style.groundPrefab != null)
                    {
                        GameObject ground = UnityEngine.Object.Instantiate(style.groundPrefab, rect);
                        ground.name = "Track";
                        slot.SetGround(ground.transform, Quaternion.Euler(style.groundEuler));
                    }

                    if (style.interactive)
                    {
                        obj.AddComponent<TruckPoolPlace>();
                    }

                    result.Add(slot);
                    index++;
                }
            }

            BuildPortals(row, style, totalWidth);

            return result;
        }

        /// <summary>
        /// Rayın iki ucuna maden portalı yerleştirir.
        /// Vagonlar bu portallardan geçip kaybolur; portal hattın nerede bittiğini gösterir.
        /// </summary>
        private static void BuildPortals(RectTransform row, TruckPlaceStyle style, float totalWidth)
        {
            if (style.portalPrefab == null) return;

            float edge = totalWidth * 0.5f + style.portalMargin;

            CreatePortal(row, style, "Portal_Left", -edge, 180f);
            CreatePortal(row, style, "Portal_Right", edge, 0f);
        }

        private static void CreatePortal(RectTransform row, TruckPlaceStyle style,
                                         string name, float x, float extraYaw)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(row, false);

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(style.cellSize, style.cellSize);
            rect.anchoredPosition3D = new Vector3(x, 0f, 0f);
            rect.localRotation = Quaternion.Euler(style.tilt, 0f, 0f);

            GameObject portal = UnityEngine.Object.Instantiate(style.portalPrefab, rect);
            portal.name = "MinePortal";

            // Karşı uçtaki portal içeriye baksın
            portal.transform.localRotation = Quaternion.Euler(style.portalEuler) *
                                             Quaternion.AngleAxis(extraYaw, Vector3.up);

            PlacePortal(portal.transform, style.portalScale);
        }

        /// <summary>Portalı sabit ölçekte kurar ve hücrenin içine ortalar.</summary>
        private static void PlacePortal(Transform portal, float scale)
        {
            portal.localScale = Vector3.one * scale;
            portal.localPosition = Vector3.zero;

            // Ölçek belli olduktan sonra ortala; pivot nerede olursa olsun doğru oturur
            if (!TryGetBounds(portal, out Bounds bounds)) return;

            portal.localPosition = new Vector3(-bounds.center.x, -bounds.center.y, -bounds.max.z);
        }

        /// <summary>Modelin parent uzayındaki sınırlarını mesh'lerden hesaplar.</summary>
        private static bool TryGetBounds(Transform model, out Bounds bounds)
        {
            bounds = default;
            Transform parent = model.parent;
            if (parent == null) return false;

            bool found = false;

            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;

                Matrix4x4 toParent = parent.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds local = mesh.bounds;

                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3(
                        (i & 1) == 0 ? local.min.x : local.max.x,
                        (i & 2) == 0 ? local.min.y : local.max.y,
                        (i & 4) == 0 ? local.min.z : local.max.z);

                    Vector3 point = toParent.MultiplyPoint3x4(corner);

                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }

            return found;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;

                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }
        }
    }
}
