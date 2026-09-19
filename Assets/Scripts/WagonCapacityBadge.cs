using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Vagonun üzerinde kalan parça/küp sayısını gösteren dinamik rozet (Badge).
    /// Kullanıcının referans görselindeki gibi:
    /// - Kalın beyaz yazı (#FFFFFF),
    /// - Her yönden belirgin koyu/siyah dış çizgi (Outline) ve hafif derinlik gölgesi (Shadow),
    /// - Arka planda fazlalık kutu olmadan doğrudan vagonun üzerinde temiz ve doğal duruş,
    /// - Her zaman kameraya dik bakan Billboard modu,
    /// - Küp yüklendiğinde tatlı bir büyüme-küçülme (DOPunchScale) geri bildirimi.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Wagon Capacity Badge")]
    public class WagonCapacityBadge : MonoBehaviour
    {
        private Canvas m_Canvas;
        private RectTransform m_BadgeRect;
        private Image m_Background;
        private Text m_Text;
        private Outline m_Outline1;
        private Outline m_Outline2;
        private Shadow m_Shadow;
        private int m_CurrentCount = -1;
        private TruckCargo m_Cargo;
        private float m_CurrentFillRatio = 0f;

        private static Material s_AlwaysOnTopMaterial;

        [Header("📐 Görsel Stil & Boyut")]
        [Tooltip("Arka plan kutusu görünsün mü? (Varsayılan: false, doğrudan vagonun üzerinde)")]
        [SerializeField] private bool m_ShowBackgroundBox = false;

        [Tooltip("Otomatik olarak vagonun 3D sınırlarını (bounds) bulup modelin tam ortasına yerleştir")]
        [SerializeField] private bool m_AutoCenterOnMesh = true;

        [Tooltip("Metnin vagon modeline göre büyüklük oranı. 0.5 = model genişliğinin yarısı. " +
                 "Rozetler çalışma anında AddComponent ile eklendiği için sahnedeki değil " +
                 "BU varsayılan geçerlidir.")]
        [Range(0.2f, 2.5f)]
        [SerializeField] private float m_SizeRatio = 0.95f;

        [Tooltip("Rozetin gövde merkezinden yukarı/aşağı kayması, gövde yüksekliğinin oranı olarak. " +
                 "0 = tam gövdenin ortasında (etiket gibi). Negatif değer aşağı indirir. " +
                 "Rozet çalışma anında eklendiği için sahnedeki değil BU varsayılan geçerlidir.")]
        [Range(-0.5f, 0.8f)]
        [SerializeField] private float m_VerticalLiftRatio = 0.32f;

        [Header("📦 Doluluk Dinamik Yükselmesi (Pile Float)")]
        [Tooltip("Kasa doldukça rozetin yukarı kayma oranı. 0 = hiç kaymasın (şişe gibi kapalı " +
                 "gövdeli modellerde istenen budur; aksi halde oynarken rozetin yeri sürekli değişir). " +
                 "Açık kasalı vagonlarda metnin yığının üstünde kalması için yükseltilebilir.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float m_FillRiseRatio = 0f;

        [Tooltip("Modelin merkezine eklenecek kamera uzayı ince ayar ofseti (X: sağ/sol, Y: yukarı/aşağı, Z: derinlik)")]
        [SerializeField] private Vector3 m_CenterOffset = Vector3.zero;

        [Tooltip("Yazı boyutu (Canvas birimi, varsayılan: 140)")]
        [Range(40, 200)]
        [SerializeField] private int m_FontSize = 140;

        [Header("🔧 Manuel Mod (AutoCenter kapalıysa)")]
        [Tooltip("Vagonun merkezinden manuel yerleşim ofseti")]
        [SerializeField] private Vector3 m_ManualOffset = new Vector3(0f, 0.15f, 0f);

        [Tooltip("Rozetin manuel ölçeği")]
        [SerializeField] private float m_ManualScale = 0.012f;

        /// <summary>
        /// Bu renderer vagonun gövdesi mi? Hem obje adına hem mesh adına bakar.
        /// </summary>
        private static bool IsBodyRenderer(Renderer r)
        {
            if (r == null) return false;

            if (r.name.StartsWith("MineCart_Body") || r.name.StartsWith("Truck_Cargo")) return true;
            if (r.name.IndexOf("Bottle", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (r.name.IndexOf("object_", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (r.name.IndexOf("Cannon", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (r.name.IndexOf("Turret", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;

            MeshFilter filter = r.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return false;

            return mesh.name.IndexOf("Body", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   mesh.name.IndexOf("Bottle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   mesh.name.IndexOf("object_", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   mesh.name.IndexOf("Cannon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   mesh.name.IndexOf("Turret", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   mesh.name.IndexOf("Cargo", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public int CurrentCount => m_CurrentCount;

        /// <summary>
        /// 3B parçaların ve vagon gövdesinin metni asla kapatamaması için ZTest Always UI materyali.
        /// </summary>
        public static Material GetAlwaysOnTopMaterial()
        {
            if (s_AlwaysOnTopMaterial == null)
            {
                Shader shader = Shader.Find("UI/Default");
                Material baseMat = shader != null ? new Material(shader) : new Material(Canvas.GetDefaultCanvasMaterial());
                baseMat.name = "UI_AlwaysOnTop_Mat";
                baseMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                s_AlwaysOnTopMaterial = baseMat;
            }
            return s_AlwaysOnTopMaterial;
        }

        private void Awake()
        {
            EnsureBadgeUI();
        }

        private void OnEnable()
        {
            EnsureBadgeUI();
            ApplyStyle();
            UpdatePlacement();
        }

        private void OnValidate()
        {
            ApplyStyle();
        }

        private void LateUpdate()
        {
            UpdatePlacement();
        }

        /// <summary>
        /// Rozeti vagon modelinin tam merkezine yerleştirir ve kameraya tam dik bakmasını sağlar (Billboard).
        /// Kasa doldukça metin yığının üzerinde dinamik olarak yükselir ve parçaların altında kalmaz.
        /// </summary>
        public void UpdatePlacement()
        {
            if (m_Canvas == null) return;

            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                m_Canvas.transform.rotation = cam.transform.rotation;
            }

            if (!m_AutoCenterOnMesh)
            {
                m_Canvas.transform.localPosition = m_ManualOffset;
                m_Canvas.transform.localScale = Vector3.one * m_ManualScale;
                return;
            }

            // Şişe için ayrı bir sabit yerleşim YOK: aşağıdaki sınır (bounds) tabanlı
            // yol zaten şişeyi tanıyor (gövde rendererı eşleşmesinde "Bottle" da aranır).
            //
            // Eskiden burada modele bakmayan sabit bir blok vardı:
            //   localPosition = (-0.04, 0.45, 0.45), localRotation = (0,180,0), scale = 0.0022
            // Üç sorun çıkarıyordu:
            //   1) Ölçek modelden türetilmediği için yazı modele göre çok büyük kalıyordu.
            //   2) 0.45'lik kaldırma sabitti; modelin boyuna bağlı olmadığı için çok yukarıda duruyordu.
            //   3) localRotation ataması, yukarıda kurulan kameraya dönük (billboard)
            //      duruşu eziyordu ve ofset yerel eksende olduğu için vagonun Y dönüşü
            //      değişince (180 -> 90) yazı yana kayıyordu.
            // Aşağıdaki yol ölçeği modelin genişliğinden, kaldırmayı boyundan alır ve
            // konumu dünya uzayında kurar; bu yüzden duruş açısından bağımsızdır.

            // Vagonun render sınırlarını hesapla (düşen parçacıklar ve canvas hariç)
            Renderer[] rends = GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds();
            bool found = false;
            Renderer bodyRenderer = null;

            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r.transform.IsChildOf(m_Canvas.transform)) continue;
                if (r.name.StartsWith("CargoPiece") || r.name.StartsWith("Voxel")) continue;

                // Gövde tespiti obje adına BAKAR ama mesh adına da bakmalı: prefab örneği
                // "Truck_FFD93D_16" gibi yeniden adlandırılıyor, gövde bilgisi yalnızca
                // mesh adında ("Bottle_Body") kalıyordu ve tespit ıskalanıyordu.
                if (IsBodyRenderer(r))
                {
                    bodyRenderer = r;
                }

                if (!found)
                {
                    b = r.bounds;
                    found = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            if (found && b.size.magnitude > 0.01f)
            {
                // Rozet GÖVDEYE sabitlenir: hem konumu hem ölçeği gövde rendererından alınır.
                //
                // Eskiden ölçek tüm vagon sınırlarından (b), konum ise gövdeden geliyordu.
                // Bu ikisi farklı şeylere bağlı olduğu için rozet modele göre kayabiliyordu.
                // Gövde tek referans olunca rozet şişenin üstünde bir etiket gibi sabit durur.
                Bounds anchor = bodyRenderer != null ? bodyRenderer.bounds : b;

                float wagonSize = Mathf.Max(anchor.size.x, anchor.size.z);
                Vector3 center = anchor.center;

                // Doluluk oranını TruckCargo'dan alıp pürüzsüzce takip et
                if (m_Cargo == null) m_Cargo = GetComponent<TruckCargo>();
                float targetFillRatio = m_Cargo != null ? m_Cargo.FillRatio : 0f;
                m_CurrentFillRatio = Mathf.MoveTowards(m_CurrentFillRatio, targetFillRatio, Time.deltaTime * 3.5f);

                bool isScifi = bodyRenderer != null && (
                    bodyRenderer.name.IndexOf("object_", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    bodyRenderer.name.IndexOf("Cannon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    bodyRenderer.name.IndexOf("Turret", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (bodyRenderer.GetComponent<MeshFilter>() != null && bodyRenderer.GetComponent<MeshFilter>().sharedMesh != null &&
                     bodyRenderer.GetComponent<MeshFilter>().sharedMesh.name.IndexOf("object_", System.StringComparison.OrdinalIgnoreCase) >= 0)
                );

                float activeLiftRatio = isScifi ? 0.14f : m_VerticalLiftRatio;
                float activeSizeRatio = isScifi ? 1.05f : m_SizeRatio;

                // 1. Gövde merkezinden kayma (0 = tam ortada, etiket gibi)
                float baseLift = anchor.size.y * activeLiftRatio;

                // 2. Doluluk yükselmesi. Şişe gibi kapalı gövdeli modellerde 0 olmalı:
                //    aksi halde kasa doldukça rozet yukarı tırmanır ve oyuncuya
                //    "yazının yeri sürekli değişiyor" gibi görünür.
                float fillLift = anchor.size.y * m_FillRiseRatio * m_CurrentFillRatio;

                Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;
                center += camUp * (baseLift + fillLift);

                // 3. Kamera bakış açısına göre kullanıcı ince ayar ofseti
                if (cam != null)
                {
                    center += cam.transform.rotation * m_CenterOffset;
                }
                else
                {
                    center += m_CenterOffset;
                }

                m_Canvas.transform.position = center;

                // Gövde genişliğine göre ölçekle
                float targetWorldSize = wagonSize * activeSizeRatio;
                float targetScale = targetWorldSize / 140f;

                m_Canvas.transform.localScale = Vector3.one * Mathf.Max(0.001f, targetScale);
            }
            else
            {
                m_Canvas.transform.localPosition = m_ManualOffset;
                m_Canvas.transform.localScale = Vector3.one * m_ManualScale;
            }
        }

        public void ApplyStyle()
        {
            if (m_Text != null)
            {
                m_Text.fontSize = m_FontSize;
                m_Text.resizeTextForBestFit = true;
                m_Text.resizeTextMinSize = 30;
                m_Text.resizeTextMaxSize = m_FontSize;
                m_Text.horizontalOverflow = HorizontalWrapMode.Overflow;
                m_Text.verticalOverflow = VerticalWrapMode.Overflow;

                // Asla parçaların arkasında kalmaması için Always-On-Top materyali ata
                Material alwaysOnTop = GetAlwaysOnTopMaterial();
                if (alwaysOnTop != null && m_Text.material != alwaysOnTop)
                {
                    m_Text.material = alwaysOnTop;
                }
            }

            if (m_Background != null)
            {
                m_Background.color = m_ShowBackgroundBox ? new Color(0.1f, 0.12f, 0.18f, 0.85f) : Color.clear;
            }
        }

        /// <summary>
        /// Kalan küp sayısını günceller. Değiştiğinde punch animasyonu yapar.
        /// </summary>
        public void SetCount(int remainingCount, bool punchAnimation = true)
        {
            EnsureBadgeUI();
            if (m_Text == null) return;

            int prev = m_CurrentCount;
            m_CurrentCount = Mathf.Max(0, remainingCount);

            if (m_CurrentCount == 0 && prev > 0)
            {
                PlayCompletionAnimation();
                return;
            }

            m_Text.text = m_CurrentCount.ToString();
            m_Text.color = Color.white;

            if (m_Cargo == null) m_Cargo = GetComponent<TruckCargo>();
            if (m_Cargo != null && m_Cargo.RemainingCapacity == m_Cargo.Capacity)
            {
                // Vagon sıfırlandıysa doluluk yükselmesini de anında tabana al
                m_CurrentFillRatio = 0f;
            }

            if (punchAnimation && prev != -1 && prev != m_CurrentCount && m_BadgeRect != null)
            {
                m_BadgeRect.DOKill();
                m_BadgeRect.localScale = Vector3.one;
                m_BadgeRect.DOPunchScale(new Vector3(0.32f, 0.32f, 0.32f), 0.20f, 6, 0.5f);
            }
        }

        /// <summary>
        /// Vagon kapasitesi tamamen dolduğunda (parçalar tamamlandığında)
        /// oynatılacak minik, tatlı kutlama/tamamlama animasyonu.
        /// </summary>
        public void PlayCompletionAnimation()
        {
            EnsureBadgeUI();
            if (m_Text == null || m_BadgeRect == null) return;

            m_BadgeRect.DOKill();
            m_Text.DOKill();

            m_Text.text = "✓";
            m_Text.color = new Color(0.25f, 0.95f, 0.45f, 1f); // Parlak tatlı yeşil

            m_BadgeRect.localScale = Vector3.one;
            m_BadgeRect.DOPunchScale(new Vector3(0.55f, 0.55f, 0.55f), 0.45f, 6, 0.55f);
        }

        public void EnsureBadgeUI()
        {
            if (m_Canvas != null && m_Text != null)
            {
                ApplyStyle();
                return;
            }

            Transform existing = transform.Find("CapacityBadgeCanvas");
            GameObject canvasObj;
            if (existing != null)
            {
                canvasObj = existing.gameObject;
            }
            else
            {
                canvasObj = new GameObject("CapacityBadgeCanvas");
                canvasObj.transform.SetParent(transform, false);
                canvasObj.transform.localPosition = m_ManualOffset;
            }

            m_Canvas = canvasObj.GetComponent<Canvas>();
            if (m_Canvas == null) m_Canvas = canvasObj.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.WorldSpace;
            m_Canvas.sortingOrder = 100; // Her şeyin önünde, net ve berrak görünsün

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(140f, 140f);
            canvasRect.localScale = Vector3.one * m_ManualScale;

            // Rozet Kökü
            Transform badgeTrans = canvasObj.transform.Find("BadgeRoot");
            GameObject badgeObj;
            if (badgeTrans != null)
            {
                badgeObj = badgeTrans.gameObject;
            }
            else
            {
                badgeObj = new GameObject("BadgeRoot");
                badgeObj.transform.SetParent(canvasObj.transform, false);
            }

            m_BadgeRect = badgeObj.GetComponent<RectTransform>();
            if (m_BadgeRect == null) m_BadgeRect = badgeObj.AddComponent<RectTransform>();
            m_BadgeRect.anchorMin = Vector2.zero;
            m_BadgeRect.anchorMax = Vector2.one;
            m_BadgeRect.sizeDelta = Vector2.zero;
            m_BadgeRect.anchoredPosition = Vector2.zero;

            // İsteğe bağlı arka plan (varsayılan saydam)
            m_Background = badgeObj.GetComponent<Image>();
            if (m_Background == null) m_Background = badgeObj.AddComponent<Image>();
            m_Background.raycastTarget = false;
            m_Background.color = m_ShowBackgroundBox ? new Color(0.1f, 0.12f, 0.18f, 0.85f) : Color.clear;

            // Metin nesnesi
            Transform textTrans = badgeObj.transform.Find("CountText");
            GameObject textObj;
            if (textTrans != null)
            {
                textObj = textTrans.gameObject;
            }
            else
            {
                textObj = new GameObject("CountText");
                textObj.transform.SetParent(badgeObj.transform, false);
            }

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            if (textRect == null) textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            m_Text = textObj.GetComponent<Text>();
            if (m_Text == null) m_Text = textObj.AddComponent<Text>();
#if UNITY_EDITOR
            Font customFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/LilitaOne-Regular.ttf");
            if (customFont != null) m_Text.font = customFont;
            else m_Text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
#else
            m_Text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            m_Text.fontSize = m_FontSize;
            m_Text.fontStyle = FontStyle.Normal;
            m_Text.alignment = TextAnchor.MiddleCenter;
            m_Text.color = Color.white;
            m_Text.raycastTarget = false;
            m_Text.horizontalOverflow = HorizontalWrapMode.Overflow;
            m_Text.verticalOverflow = VerticalWrapMode.Overflow;
            m_Text.resizeTextForBestFit = true;
            m_Text.resizeTextMinSize = 30;
            m_Text.resizeTextMaxSize = m_FontSize;

            // 3B nesneler ve yığılan parçalar metni asla örtmesin
            Material alwaysOnTop = GetAlwaysOnTopMaterial();
            if (alwaysOnTop != null)
            {
                m_Text.material = alwaysOnTop;
            }

            // 8 Yönlü Kesintisiz Kalın Outline:
            // Beyaz, sarı, siyah ya da herhangi bir renkteki parçanın üstünde %100 net kontrast sağlar
            Outline[] outlines = textObj.GetComponents<Outline>();
            m_Outline1 = outlines.Length > 0 ? outlines[0] : textObj.AddComponent<Outline>();
            m_Outline1.effectColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            m_Outline1.effectDistance = new Vector2(4.5f, 4.5f);

            m_Outline2 = outlines.Length > 1 ? outlines[1] : textObj.AddComponent<Outline>();
            m_Outline2.effectColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            m_Outline2.effectDistance = new Vector2(-4.5f, 4.5f);

            Outline outline3 = outlines.Length > 2 ? outlines[2] : textObj.AddComponent<Outline>();
            outline3.effectColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            outline3.effectDistance = new Vector2(4.5f, 0f);

            Outline outline4 = outlines.Length > 3 ? outlines[3] : textObj.AddComponent<Outline>();
            outline4.effectColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            outline4.effectDistance = new Vector2(0f, 4.5f);

            // Alt gölge: 3B derinlik
            m_Shadow = textObj.GetComponent<Shadow>();
            if (m_Shadow == null) m_Shadow = textObj.AddComponent<Shadow>();
            m_Shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            m_Shadow.effectDistance = new Vector2(2.5f, -5.5f);
        }
    }
}
