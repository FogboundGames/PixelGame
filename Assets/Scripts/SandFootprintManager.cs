using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Kumsalda yürüyen küplerin bastıkları yerlerde geçici sevimli ayak izleri (kum basma izleri)
    /// ve minik kum pufu efektleri oluşturan merkezi sistem.
    ///
    /// Özellikler:
    /// - Küpün Rengi: Küp hangi renkse o rengin belirgin ve canlı tonunda ayak izi bırakır.
    /// - Hızlı Yok Oluş: DOTween ile basıldığı an belirgin çıkıp 0.45 sn içinde hızla kumsalda erir.
    /// - Sıfır GC: Önceden ayrılmış (pre-allocated) Quad nesne havuzu (Object Pool) kullanır.
    /// - GPU Instancing: Tüm ayak izleri tek bir materyal ve MaterialPropertyBlock ile çizilir.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Sand Footprint Manager")]
    public class SandFootprintManager : MonoBehaviour
    {
        private static SandFootprintManager s_Instance;
        public static SandFootprintManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = Object.FindFirstObjectByType<SandFootprintManager>();
                    if (s_Instance == null)
                    {
                        GameObject root = GameObject.Find("[GAMEPLAY_MODELS]");
                        GameObject go = new GameObject("[SandFootprintManager]");
                        if (root != null) go.transform.SetParent(root.transform, false);
                        s_Instance = go.AddComponent<SandFootprintManager>();
                    }
                }
                return s_Instance;
            }
        }

        [Header("👣 Materyal ve Görsel")]
        [SerializeField] private Material m_FootprintMaterial;
        [SerializeField] private int m_InitialPoolSize = 140;

        [Header("✨ Kum Pufu Partikülü")]
        [SerializeField] private bool m_EnableSandPuff = true;
        [SerializeField] private Color m_SandPuffColor = new Color(0.96f, 0.82f, 0.46f, 0.75f);

        // İç nesne havuzu sınıfı
        private class PooledFootprint
        {
            public GameObject GameObject;
            public Transform Transform;
            public MeshRenderer Renderer;
            public Tween FadeTween;
            public bool IsActive;
        }

        private readonly List<PooledFootprint> m_Pool = new List<PooledFootprint>(160);
        private readonly List<PooledFootprint> m_ActiveList = new List<PooledFootprint>(160);
        private MaterialPropertyBlock m_PropBlock;
        private static readonly int s_ColorPropId = Shader.PropertyToID("_Color");

        private Mesh m_QuadMesh;
        private ParticleSystem m_PuffParticleSystem;
        private ParticleSystem.EmitParams m_PuffEmitParams;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }
            s_Instance = this;

            m_PropBlock = new MaterialPropertyBlock();
            EnsureMaterial();
            EnsureQuadMesh();
            EnsurePool();
            EnsurePuffParticles();
        }

        private void OnEnable()
        {
            if (s_Instance == null) s_Instance = this;
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        private void EnsureMaterial()
        {
            if (m_FootprintMaterial == null)
            {
#if UNITY_EDITOR
                m_FootprintMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SandFootprint_Mat.mat");
#endif
            }

            if (m_FootprintMaterial == null)
            {
                Shader sh = Shader.Find("Sprites/Default");
                if (sh != null)
                {
                    m_FootprintMaterial = new Material(sh) { name = "SandFootprint_Runtime_Mat" };
                    m_FootprintMaterial.enableInstancing = true;
                    m_FootprintMaterial.color = new Color(0.68f, 0.48f, 0.24f, 0.28f);
                }
            }
        }

        private void EnsureQuadMesh()
        {
            if (m_QuadMesh == null)
            {
                GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                MeshFilter mf = tempQuad.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    m_QuadMesh = mf.sharedMesh;
                }
                if (Application.isPlaying) Destroy(tempQuad);
                else DestroyImmediate(tempQuad);
            }
        }

        private void EnsurePool()
        {
            if (m_Pool.Count >= m_InitialPoolSize) return;

            int needed = m_InitialPoolSize - m_Pool.Count;
            for (int i = 0; i < needed; i++)
            {
                PooledFootprint item = CreateNewFootprintInstance();
                m_Pool.Add(item);
            }
        }

        private PooledFootprint CreateNewFootprintInstance()
        {
            GameObject go = new GameObject("[SandFootprint]");
            go.transform.SetParent(transform, false);
            go.layer = 2; // Ignore Raycast (asla tıklamaları engellemesin)

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = m_QuadMesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m_FootprintMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.sortingOrder = 2; // Kumsal zemin gölgesinin hemen üstünde, küplerin altında

            go.SetActive(false);

            return new PooledFootprint
            {
                GameObject = go,
                Transform = go.transform,
                Renderer = mr,
                IsActive = false
            };
        }

        private void EnsurePuffParticles()
        {
            if (m_PuffParticleSystem != null) return;

            Transform child = transform.Find("[SandPuffParticles]");
            if (child != null)
            {
                m_PuffParticleSystem = child.GetComponent<ParticleSystem>();
            }

            if (m_PuffParticleSystem == null)
            {
                GameObject puffGo = new GameObject("[SandPuffParticles]");
                puffGo.transform.SetParent(transform, false);
                puffGo.layer = 2;

                m_PuffParticleSystem = puffGo.AddComponent<ParticleSystem>();
                var main = m_PuffParticleSystem.main;
                main.playOnAwake = false;
                main.loop = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = 0.38f;
                main.startSpeed = 0.45f;
                main.startSize = 0.055f;
                main.startColor = m_SandPuffColor;
                main.maxParticles = 300;

                var emission = m_PuffParticleSystem.emission;
                emission.enabled = false;

                var shape = m_PuffParticleSystem.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.03f;

                var colorOverLifetime = m_PuffParticleSystem.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(m_SandPuffColor, 0f), new GradientColorKey(m_SandPuffColor, 1f) },
                    new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) }
                );
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

                var sizeOverLifetime = m_PuffParticleSystem.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                AnimationCurve sizeCurve = new AnimationCurve(
                    new Keyframe(0f, 0.4f),
                    new Keyframe(0.3f, 1f),
                    new Keyframe(1f, 0.2f)
                );
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

                var renderer = puffGo.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = m_FootprintMaterial;
                    renderer.sortingOrder = 3;
                }
            }
        }

        /// <summary>
        /// Kumsalda bir ayak izi basar. Küpün kendi renginde belirir ve DOTween ile hemen eriyip kaybolur.
        /// </summary>
        public void SpawnFootprint(
            Vector3 worldPos,
            Vector3 moveDir,
            float cubeSize,
            bool isLeftFoot,
            float groundWorldZ,
            Color cubeColor = default,
            CubeMovementSettings settings = null)
        {
            if (settings != null && !settings.EnableSandFootprints) return;

            PooledFootprint fp = GetPooledInstance();
            if (fp == null) return;

            // Önceki tween varsa sonlandır
            fp.FadeTween?.Kill();

            // Hemen yok olacak şekilde kısa ömür (0.46s)
            float lifetime = settings != null ? Mathf.Min(settings.FootprintLifetime, 0.60f) : 0.46f;
            float baseSize = settings != null ? settings.FootprintSize : 0.22f;
            // Çok hafif daha belirgin (0.58f opaklık)
            float startAlpha = settings != null ? Mathf.Max(settings.FootprintOpacity, 0.50f) : 0.58f;
            bool enablePuff = settings != null ? settings.FootstepPuff : m_EnableSandPuff;

            // Küp hangi renkse o rengi kullan; yoksa kumsal rengi
            Color baseColor;
            if (cubeColor != default && cubeColor.a > 0.05f)
            {
                baseColor = cubeColor;
            }
            else
            {
                baseColor = settings != null ? settings.FootprintColor : new Color(0.68f, 0.48f, 0.24f, 1f);
            }

            // Kumsal Z düzlemi: Z-fighting'i önlemek için kumsalın milimetrik önünde
            float z = float.IsFinite(groundWorldZ) ? (groundWorldZ - 0.0022f) : worldPos.z;
            Vector3 pos = new Vector3(worldPos.x, worldPos.y, z);

            // Yön açısı: Hareket yönüne doğru bakar
            float headingAngle = 0f;
            if (moveDir.sqrMagnitude > 1e-4f)
            {
                headingAngle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg - 90f;
            }

            // Sevimli paytak açı: Sol ayak hafif sola (+4.5°), sağ ayak hafif sağa (-4.5°) basar
            float splayAngle = isLeftFoot ? 4.5f : -4.5f;
            Quaternion rot = Quaternion.Euler(0f, 0f, headingAngle + splayAngle);

            // Ölçek: Küp boyu ile orantılı sevimli basılmış ayak izi (genişlik x boy)
            float s = Mathf.Max(0.01f, cubeSize) * baseSize;
            Vector3 scale = new Vector3(s * 0.78f, s * 1.18f, 1f);

            fp.Transform.position = pos;
            fp.Transform.rotation = rot;
            fp.Transform.localScale = scale;

            fp.IsActive = true;
            fp.GameObject.SetActive(true);
            m_ActiveList.Add(fp);

            // İlk belirgin rengi uygula
            Color initialColor = new Color(baseColor.r, baseColor.g, baseColor.b, startAlpha);
            m_PropBlock.SetColor(s_ColorPropId, initialColor);
            fp.Renderer.SetPropertyBlock(m_PropBlock);

            // DOTWEEN İLE HEMEN YOK OLUŞ:
            // Ayak izi basıldığı an belirgin görünür, ardından hızla eriyerek yok olur (0.46 sn)
            fp.FadeTween = DOVirtual.Float(startAlpha, 0f, lifetime, alpha =>
            {
                if (fp.Renderer != null && fp.GameObject != null && fp.GameObject.activeSelf)
                {
                    Color stepCol = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                    m_PropBlock.SetColor(s_ColorPropId, stepCol);
                    fp.Renderer.SetPropertyBlock(m_PropBlock);
                }
            })
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                fp.IsActive = false;
                if (fp.GameObject != null) fp.GameObject.SetActive(false);
                m_ActiveList.Remove(fp);
            });

            // Minik renkli kum tozu pufu
            if (enablePuff && m_PuffParticleSystem != null)
            {
                m_PuffEmitParams.position = pos + new Vector3(0f, 0f, -0.01f);
                m_PuffEmitParams.velocity = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.15f, 0.15f), -0.08f);
                m_PuffEmitParams.startSize = s * 0.38f;
                m_PuffEmitParams.startColor = Color.Lerp(baseColor, m_SandPuffColor, 0.40f);
                m_PuffEmitParams.startLifetime = 0.28f;
                m_PuffEmitParams.applyShapeToPosition = false;
                m_PuffParticleSystem.Emit(m_PuffEmitParams, Random.Range(1, 3));
            }
        }

        private PooledFootprint GetPooledInstance()
        {
            // Havuzda boşta olanı bul
            for (int i = 0; i < m_Pool.Count; i++)
            {
                if (!m_Pool[i].IsActive)
                {
                    return m_Pool[i];
                }
            }

            // Havuz dolduysa en eski aktifi geri dönüştür
            if (m_ActiveList.Count > 0)
            {
                PooledFootprint oldest = m_ActiveList[0];
                oldest.FadeTween?.Kill();
                m_ActiveList.RemoveAt(0);
                return oldest;
            }

            // Veya yeni üret
            PooledFootprint newItem = CreateNewFootprintInstance();
            m_Pool.Add(newItem);
            return newItem;
        }

        private void Update()
        {
            // DOTween tüm sönüm ve geri dönüşleri (OnComplete) asenkron yönettiği için Update boş bırakılmıştır (Sıfır CPU/GC yükü).
        }

        /// <summary>
        /// Sahnedeki veya seviyedeki tüm ayak izlerini temizler (yeni seviye başladığında vs.).
        /// </summary>
        public void ClearAll()
        {
            for (int i = 0; i < m_ActiveList.Count; i++)
            {
                m_ActiveList[i].FadeTween?.Kill();
                m_ActiveList[i].IsActive = false;
                if (m_ActiveList[i].GameObject != null)
                {
                    m_ActiveList[i].GameObject.SetActive(false);
                }
            }
            m_ActiveList.Clear();
        }
    }
}
