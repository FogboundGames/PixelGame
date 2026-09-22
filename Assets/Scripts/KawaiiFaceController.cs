using System;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// KawaiiCube vagonunun sevimli yüz ifadelerini (gülümseme, göz kırpma, mutlu, heyecanlı, kedi)
    /// yöneten bileşen. Kullanıcının "+ olarak yüzler de yapmanı istiyorum" talebine yanıt verir.
    /// Her vagonun sahnede canlı, çeşitli ve sevimli görünmesini sağlar.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class KawaiiFaceController : MonoBehaviour
    {
        public enum ExpressionType
        {
            Random = -1,
            Smile = 0,    // Klasik sevimli gülümseme (• ‿ •)
            Wink = 1,     // Neşeli göz kırpma (• ‿ <)
            Happy = 2,    // Yanakları allı süper mutlu (^ ‿ ^)
            Excited = 3,  // Ağzı açık heyecanlı (• ᗜ •)
            Cat = 4       // Sevimli kedi / anime gülüşü (• ω •)
        }

        [Header("😊 Yüz İfadesi")]
        [Tooltip("Belirli bir ifade seçilebilir veya Random ile vagonlara otomatik dağıtılabilir.")]
        [SerializeField] private ExpressionType m_Expression = ExpressionType.Random;

        [Header("🖼️ Sprite Renderer")]
        [SerializeField] private SpriteRenderer m_FaceRenderer;

        [Header("🎨 İfade Spriteları")]
        [SerializeField] private Sprite m_SmileSprite;
        [SerializeField] private Sprite m_WinkSprite;
        [SerializeField] private Sprite m_HappySprite;
        [SerializeField] private Sprite m_ExcitedSprite;
        [SerializeField] private Sprite m_CatSprite;

        private ExpressionType m_CurrentApplied = (ExpressionType)(-99);

        public ExpressionType Expression
        {
            get => m_Expression;
            set
            {
                m_Expression = value;
                ApplyExpression(force: true);
            }
        }

        public SpriteRenderer FaceRenderer => m_FaceRenderer;

        private void Awake()
        {
            EnsureRendererReference();
            ApplyExpression(force: false);
        }

        private void OnEnable()
        {
            EnsureRendererReference();
            ApplyExpression(force: false);
        }

        private void OnValidate()
        {
            EnsureRendererReference();
            ApplyExpression(force: true);
        }

        public void EnsureRendererReference()
        {
            if (m_FaceRenderer == null)
            {
                Transform faceChild = transform.Find("Face");
                if (faceChild != null)
                {
                    m_FaceRenderer = faceChild.GetComponent<SpriteRenderer>();
                }
                if (m_FaceRenderer == null)
                {
                    m_FaceRenderer = GetComponentInChildren<SpriteRenderer>(true);
                }
            }

            // Gerekirse varsayılan sprite'ları Resources / Assets üzerinden yükle
            #if UNITY_EDITOR
            if (m_SmileSprite == null)
                m_SmileSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/KawaiiFace_Smile.png");
            if (m_WinkSprite == null)
                m_WinkSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/KawaiiFace_Wink.png");
            if (m_HappySprite == null)
                m_HappySprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/KawaiiFace_Happy.png");
            if (m_ExcitedSprite == null)
                m_ExcitedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/KawaiiFace_Excited.png");
            if (m_CatSprite == null)
                m_CatSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/KawaiiFace_Cat.png");
            #endif
        }

        public void ApplyExpression(bool force = false)
        {
            if (m_FaceRenderer == null) return;

            ExpressionType target = m_Expression;
            if (target == ExpressionType.Random)
            {
                // Vagonun pozisyonu veya hash'ine göre sabit ama çeşitli bir ifade belirle
                int hash = Mathf.Abs(gameObject.GetInstanceID());
                target = (ExpressionType)(hash % 5);
            }

            if (!force && m_CurrentApplied == target) return;
            m_CurrentApplied = target;

            Sprite s = GetSpriteForExpression(target);
            if (s != null)
            {
                m_FaceRenderer.sprite = s;
            }

            // Yüzün vagonun önünde daima net görünmesi için ayarlar
            m_FaceRenderer.sortingOrder = 15;
            m_FaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_FaceRenderer.receiveShadows = false;
        }

        public void SetExpressionByIndex(int index)
        {
            int clamped = Mathf.Clamp(index % 5, 0, 4);
            m_Expression = (ExpressionType)clamped;
            ApplyExpression(force: true);
        }

        private Sprite GetSpriteForExpression(ExpressionType expr)
        {
            switch (expr)
            {
                case ExpressionType.Smile:
                    return m_SmileSprite != null ? m_SmileSprite : m_WinkSprite;
                case ExpressionType.Wink:
                    return m_WinkSprite != null ? m_WinkSprite : m_SmileSprite;
                case ExpressionType.Happy:
                    return m_HappySprite != null ? m_HappySprite : m_SmileSprite;
                case ExpressionType.Excited:
                    return m_ExcitedSprite != null ? m_ExcitedSprite : m_SmileSprite;
                case ExpressionType.Cat:
                    return m_CatSprite != null ? m_CatSprite : m_SmileSprite;
                default:
                    return m_SmileSprite;
            }
        }
    }
}
