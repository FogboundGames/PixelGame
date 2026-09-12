using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame
{
    /// <summary>
    /// Ekranın altındaki kamyon slotu şeridini yönetir.
    /// Slotların kendisi UI görselidir; üstlerindeki kamyonlar 3D nesnedir.
    /// Ekran boyutu değiştiğinde kamyonları slotlara yeniden hizalar.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Truck Slot Row")]
    public class TruckSlotRow : MonoBehaviour
    {
        [Header("🅿️ Slotlar")]
        [SerializeField] private List<TruckSlot> m_Slots = new List<TruckSlot>();

        [Header("🎨 Görünüm")]
        [Tooltip("Park yerlerinin boyutu, aralığı ve görünümü. " +
                 "Kaç tane olacağı bölüm verisinden gelir.")]
        [SerializeField] private TruckPlaceStyle m_Style = new TruckPlaceStyle();

        [Header("✋ Elle Ayarlama Modu (Manual Control)")]
        [Tooltip("Açıkken sahnede gölge nesnelerini (SlotShadow_1..5, RowGroundShadow) serbestçe tutup sürükleyebilir, boyutlandırabilir ve renklendirebilirsiniz. Kod yaptığınız değişiklikleri asla ezmez veya sıfırlamaz.")]
        [SerializeField] private bool m_ManualShadowMode = true;

        public List<TruckSlot> Slots => m_Slots;
        public TruckPlaceStyle Style => m_Style;
        public int SlotCount => m_Slots != null ? m_Slots.Count : 0;
        public bool ManualShadowMode { get => m_ManualShadowMode; set => m_ManualShadowMode = value; }

        /// <summary>
        /// Şeridi verilen sütun/sıra sayısına göre yeniden kurar.
        /// Bölüm verisi değiştiğinde çağrılır.
        /// </summary>
        public void RebuildPlaces(int columns, int rows)
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null) return;

            // Doldurma slotlarının rolü sabittir: park yeri çizilir, tıklama küpleri engellemesin
            m_Style.showSprite = true;
            m_Style.interactive = false;

            m_Slots = TruckPlaceBuilder.Build(rect, m_Style, columns, rows, "Slot");
            UpdateShadows();
        }

        private Vector2 m_LastScreenSize;

        private void OnEnable()
        {
            CollectSlotsIfEmpty();
            AlignAll();
            UpdateShadows();
        }

        private void Start()
        {
            CollectSlotsIfEmpty();
            UpdateShadows();
        }

        private void OnValidate()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= DeferredRefresh;
            UnityEditor.EditorApplication.delayCall += DeferredRefresh;
            #endif
        }

        #if UNITY_EDITOR
        private void DeferredRefresh()
        {
            if (this == null) return;
            CollectSlotsIfEmpty();
            UpdateShadows();
        }
        #endif

        private void Update()
        {
            // Ekran boyutu / en-boy oranı değişirse slotların dünya karşılığı da kayar
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            if (screenSize != m_LastScreenSize)
            {
                m_LastScreenSize = screenSize;
                AlignAll();
                UpdateShadows();
            }
        }

        private void CollectSlotsIfEmpty()
        {
            if (m_Slots != null && m_Slots.Count > 0) return;

            m_Slots = new List<TruckSlot>(GetComponentsInChildren<TruckSlot>(true));
        }

        /// <summary>Tüm kamyonları slotlarına yeniden hizalar.</summary>
        [ContextMenu("🚚 Tüm Kamyonları Hizala")]
        public void AlignAll()
        {
            if (m_Slots == null) return;

            foreach (TruckSlot slot in m_Slots)
            {
                if (slot == null) continue;
                slot.AlignTruck();
            }
        }

        /// <summary>Tüm kamyonların renklerini yeniden uygular.</summary>
        [ContextMenu("🎨 Tüm Kamyon Renklerini Uygula")]
        public void ApplyAllColors()
        {
            if (m_Slots == null) return;

            foreach (TruckSlot slot in m_Slots)
            {
                if (slot == null) continue;
                slot.ApplyTruckColor();
            }
        }

        public TruckSlot GetSlot(int index)
        {
            if (m_Slots == null || index < 0 || index >= m_Slots.Count) return null;
            return m_Slots[index];
        }

        /// <summary>Verilen renge en yakın kamyonu taşıyan slotu döndürür (renk eşleştirme için).</summary>
        public TruckSlot FindSlotByColor(Color color, float threshold = 0.15f)
        {
            if (m_Slots == null) return null;

            TruckSlot best = null;
            float bestDistance = float.MaxValue;

            foreach (TruckSlot slot in m_Slots)
            {
                if (slot == null) continue;

                Color c = slot.TruckColor;
                float distance = Mathf.Abs(c.r - color.r) + Mathf.Abs(c.g - color.g) + Mathf.Abs(c.b - color.b);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = slot;
                }
            }

            return bestDistance <= threshold * 3f ? best : null;
        }

        #region 🌑 Fake Shadow Yönetimi

        /// <summary>
        /// Slotların ve şeridin altındaki sahte gölgeleri (Fake Shadow) günceller veya oluşturur.
        /// </summary>
        [ContextMenu("🌑 Fake Shadow'ları Yeniden Oluştur / Güncelle")]
        public void UpdateShadows()
        {
            RectTransform rowRect = transform as RectTransform;
            if (rowRect == null) return;

            Transform shadowsTrans = transform.Find("Shadows");

            if (!m_Style.enableShadow && !m_Style.enableRowGroundShadow)
            {
                if (shadowsTrans != null) shadowsTrans.gameObject.SetActive(false);
                return;
            }

            GameObject shadowsObj;
            if (shadowsTrans == null)
            {
                shadowsObj = new GameObject("Shadows", typeof(RectTransform));
                shadowsObj.transform.SetParent(transform, false);
            }
            else
            {
                shadowsObj = shadowsTrans.gameObject;
            }

            shadowsObj.SetActive(true);
            // Hiyerarşide en başa al: CanvasRenderer bu sayede gölgeleri slotların arkasına çizer
            shadowsObj.transform.SetAsFirstSibling();

            RectTransform shadowsRect = shadowsObj.GetComponent<RectTransform>();
            shadowsRect.anchorMin = Vector2.zero;
            shadowsRect.anchorMax = Vector2.one;
            shadowsRect.offsetMin = Vector2.zero;
            shadowsRect.offsetMax = Vector2.zero;
            shadowsRect.pivot = new Vector2(0.5f, 0.5f);
            shadowsRect.localPosition = Vector3.zero;
            shadowsRect.localRotation = Quaternion.identity;
            shadowsRect.localScale = Vector3.one;

            // 1. Şerit Zemin Gölgesi (Row Ground Shadow)
            UpdateRowGroundShadow(shadowsRect);

            // 2. Bireysel Slot Gölgeleri (Slot Drop Shadows)
            UpdateSlotShadows(shadowsRect);
        }

        private void UpdateRowGroundShadow(RectTransform shadowsRect, bool forceStyle = false)
        {
            Transform rgsTrans = shadowsRect.Find("RowGroundShadow");
            if (!m_Style.enableRowGroundShadow)
            {
                if (rgsTrans != null)
                {
                    if (Application.isPlaying) Destroy(rgsTrans.gameObject);
                    else DestroyImmediate(rgsTrans.gameObject);
                }
                return;
            }

            bool isNew = (rgsTrans == null);
            GameObject rgsObj = !isNew ? rgsTrans.gameObject : new GameObject("RowGroundShadow", typeof(RectTransform));
            if (isNew)
            {
                rgsObj.transform.SetParent(shadowsRect, false);
            }

            rgsObj.SetActive(true);
            rgsObj.transform.SetAsFirstSibling();

            RectTransform rgsRect = rgsObj.GetComponent<RectTransform>();
            Image img = rgsObj.GetComponent<Image>();
            if (img == null) img = rgsObj.AddComponent<Image>();

            Sprite rgsSprite = m_Style.rowGroundShadowSprite != null ? m_Style.rowGroundShadowSprite : ResolveRowGroundShadowSprite();
            if (img.sprite == null || img.sprite != rgsSprite)
                img.sprite = rgsSprite;
            img.raycastTarget = false;
            img.type = Image.Type.Simple;

            // Eğer elle ayarlama modu açıksa ve nesne zaten sahnede varsa, kullanıcının elle verdiği pozisyon/boyut/rengi ezme!
            if (m_ManualShadowMode && !isNew && !forceStyle)
            {
                return;
            }

            rgsRect.anchorMin = new Vector2(0.5f, 0.5f);
            rgsRect.anchorMax = new Vector2(0.5f, 0.5f);
            rgsRect.pivot = new Vector2(0.5f, 0.5f);

            RectTransform rowRect = transform as RectTransform;
            float rowWidth = rowRect != null ? rowRect.sizeDelta.x : 2000f;
            float cellH = m_Style.cellSize;

            rgsRect.sizeDelta = new Vector2(rowWidth + m_Style.rowGroundShadowPadding.x, cellH * 0.85f + m_Style.rowGroundShadowPadding.y);
            rgsRect.anchoredPosition3D = new Vector3(m_Style.rowGroundShadowOffset.x, m_Style.rowGroundShadowOffset.y, m_Style.rowGroundShadowZ);
            rgsRect.localRotation = Quaternion.Euler(m_Style.tilt, 0f, 0f);
            img.color = m_Style.rowGroundShadowColor;
        }

        private void UpdateSlotShadows(RectTransform shadowsRect, bool forceStyle = false)
        {
            CollectSlotsIfEmpty();
            if (m_Slots == null) return;

            Sprite shadowSprite = m_Style.shadowSprite != null ? m_Style.shadowSprite : ResolveSlotShadowSprite();

            for (int i = 0; i < m_Slots.Count; i++)
            {
                TruckSlot slot = m_Slots[i];
                if (slot == null) continue;

                string shadowName = $"SlotShadow_{i + 1}";
                Transform sTrans = shadowsRect.Find(shadowName);

                if (!m_Style.enableShadow)
                {
                    if (sTrans != null) sTrans.gameObject.SetActive(false);
                    continue;
                }

                bool isNew = (sTrans == null);
                GameObject sObj = !isNew ? sTrans.gameObject : new GameObject(shadowName, typeof(RectTransform));
                if (isNew)
                {
                    sObj.transform.SetParent(shadowsRect, false);
                }

                sObj.SetActive(true);

                RectTransform sRect = sObj.GetComponent<RectTransform>();
                Image img = sObj.GetComponent<Image>();
                if (img == null) img = sObj.AddComponent<Image>();

                if (img.sprite == null || img.sprite != shadowSprite)
                    img.sprite = shadowSprite;
                img.preserveAspect = true;
                img.raycastTarget = false;

                // Eğer elle ayarlama modu açıksa ve nesne zaten sahnede varsa, kullanıcının elle verdiği pozisyon/boyut/rengi ezme!
                if (m_ManualShadowMode && !isNew && !forceStyle)
                {
                    continue;
                }

                RectTransform targetSlotRect = slot.SlotRect;
                sRect.anchorMin = targetSlotRect.anchorMin;
                sRect.anchorMax = targetSlotRect.anchorMax;
                sRect.pivot = targetSlotRect.pivot;
                sRect.sizeDelta = new Vector2(targetSlotRect.sizeDelta.x * m_Style.shadowScale.x, targetSlotRect.sizeDelta.y * m_Style.shadowScale.y);

                Vector3 targetPos = targetSlotRect.anchoredPosition3D;
                sRect.anchoredPosition3D = new Vector3(
                    targetPos.x + m_Style.shadowOffset.x,
                    targetPos.y + m_Style.shadowOffset.y,
                    targetPos.z + m_Style.shadowZ
                );
                sRect.localRotation = targetSlotRect.localRotation;
                sRect.localScale = Vector3.one;
                img.color = m_Style.shadowColor;
            }

            // Fazlalık eski gölgeleri temizle
            for (int i = m_Slots.Count; ; i++)
            {
                Transform extra = shadowsRect.Find($"SlotShadow_{i + 1}");
                if (extra == null) break;
                if (Application.isPlaying) Destroy(extra.gameObject);
                else DestroyImmediate(extra.gameObject);
            }
        }

        /// <summary>
        /// Tüm gölgelere m_Style ayarlarını zorla uygular (kullanıcı standart stile eşitlemek istediğinde).
        /// </summary>
        [ContextMenu("🔄 Gölgeleri Stile Eşitle (Force Apply Style)")]
        public void ForceApplyStyleToShadows()
        {
            RectTransform rowRect = transform as RectTransform;
            if (rowRect == null) return;

            Transform shadowsTrans = transform.Find("Shadows");
            if (shadowsTrans == null)
            {
                UpdateShadows();
                return;
            }

            RectTransform shadowsRect = shadowsTrans.GetComponent<RectTransform>();
            UpdateRowGroundShadow(shadowsRect, forceStyle: true);
            UpdateSlotShadows(shadowsRect, forceStyle: true);
        }

        public Transform GetShadowsContainer() => transform.Find("Shadows");

        public GameObject GetRowGroundShadowObject()
        {
            Transform st = GetShadowsContainer();
            return st != null ? st.Find("RowGroundShadow")?.gameObject : null;
        }

        public GameObject GetSlotShadowObject(int index)
        {
            Transform st = GetShadowsContainer();
            return st != null ? st.Find($"SlotShadow_{index + 1}")?.gameObject : null;
        }

        public List<GameObject> GetAllShadowObjects()
        {
            var list = new List<GameObject>();
            Transform st = GetShadowsContainer();
            if (st == null) return list;

            Transform rgs = st.Find("RowGroundShadow");
            if (rgs != null) list.Add(rgs.gameObject);

            for (int i = 0; i < SlotCount; i++)
            {
                Transform s = st.Find($"SlotShadow_{i + 1}");
                if (s != null) list.Add(s.gameObject);
            }

            return list;
        }

        [ContextMenu("🌑 Fake Shadow'ları Temizle")]
        public void ClearShadows()
        {
            Transform shadowsTrans = transform.Find("Shadows");
            if (shadowsTrans != null)
            {
                if (Application.isPlaying) Destroy(shadowsTrans.gameObject);
                else DestroyImmediate(shadowsTrans.gameObject);
            }
        }

        private Sprite ResolveSlotShadowSprite()
        {
            #if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("SlotShadow t:Sprite");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) return s;
            }
            #endif
            return null;
        }

        private Sprite ResolveRowGroundShadowSprite()
        {
            #if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("RowGroundShadow t:Sprite");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) return s;
            }
            #endif
            return null;
        }

        #endregion
    }
}
