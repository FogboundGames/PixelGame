using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelGame.Creative
{
    [ExecuteAlways]
    [SelectionBase]
    [DisallowMultipleComponent]
    public class ContainerShadowTweaker : MonoBehaviour
    {
        [Header("📐 Boyut Ayarları (Size)")]
        [Tooltip("Gölgenin yatay genişliği")]
        [Range(0.5f, 2.5f)]
        public float Width = 1.28f;

        [Tooltip("Gölgenin dikey uzunluğu (aşağı uzama)")]
        [Range(0.2f, 2.0f)]
        public float Height = 0.78f;

        [Header("📍 Konum Ayarları (Position)")]
        [Tooltip("Yatay merkez konumu")]
        [Range(-1.0f, 1.0f)]
        public float PositionX = 0.0f;

        [Tooltip("Dikey konumu (Aşağı/Yukarı)")]
        [Range(0.8f, 2.2f)]
        public float PositionY = 1.38f;

        [Tooltip("Derinlik konumu")]
        public float PositionZ = 0.44f;

        [Header("🎨 Renk ve Saydamlık")]
        [Range(0f, 1f)]
        public float Alpha = 0.65f;
        public Color ShadowColor = new Color(0.02f, 0.045f, 0.12f, 0.65f);

        private MeshRenderer m_Renderer;
        private MaterialPropertyBlock m_Mpb;

        private void OnEnable()
        {
            ApplySettings();
        }

        private void OnValidate()
        {
            ApplySettings();
        }

        public void ApplySettings()
        {
            transform.position = new Vector3(PositionX, PositionY, PositionZ);
            transform.localScale = new Vector3(Width, Height, 1f);

            if (m_Renderer == null) m_Renderer = GetComponent<MeshRenderer>();
            if (m_Renderer != null)
            {
                if (m_Mpb == null) m_Mpb = new MaterialPropertyBlock();
                m_Renderer.GetPropertyBlock(m_Mpb);
                Color c = ShadowColor;
                c.a = Alpha;
                m_Mpb.SetColor("_Color", c);
                m_Mpb.SetColor("_BaseColor", c);
                m_Renderer.SetPropertyBlock(m_Mpb);
            }
        }

        public void ReadCurrentTransform()
        {
            PositionX = transform.position.x;
            PositionY = transform.position.y;
            PositionZ = transform.position.z;
            Width = transform.localScale.x;
            Height = transform.localScale.y;
            ApplySettings();
        }
    }
}
