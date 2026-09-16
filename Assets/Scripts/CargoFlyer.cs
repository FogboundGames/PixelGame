using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Kırılan küp parçalarının önce çerçevenin altındaki rafta birikmesini,
    /// ardından DOTween ile raydaki eşleşen renkli vagona kavisle akmasını yöneten nesne.
    ///
    /// İki aşamalı yaşam döngüsü (Two-Phase Flow):
    /// 1. Aşama: Küpten alt rafa düşüş, zıplayarak yerleşme (OutBounce) ve birikme.
    /// 2. Aşama: Rafta toplanan parçaların sırayla/kademeli olarak vagona şelale gibi akması.
    ///
    /// Parçalar havuzdan gelir ve DOTween animasyonları tamamlandığında havuza geri döner.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Cargo Flyer")]
    public class CargoFlyer : MonoBehaviour
    {
        private static readonly Stack<CargoFlyer> s_Pool = new Stack<CargoFlyer>();
        private static Transform s_PoolRoot;

        private MeshFilter m_MeshFilter;
        private MeshRenderer m_Renderer;
        private Material m_Material;
        private Sequence m_ActiveSequence;

        public Mesh CurrentMesh => m_MeshFilter != null ? m_MeshFilter.sharedMesh : null;

        public void SetMesh(Mesh mesh)
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null && mesh != null)
            {
                m_MeshFilter.sharedMesh = mesh;
            }
        }

        /// <summary>
        /// Doğal kırılma parçası (Voronoi/Cell Fracture shard) için rafa fırlatma.
        /// Küpün kendi merkezindeki yerel pozisyonunda başlar, mikro çatlak açılması yapar
        /// ve ardından rafa yumuşak bir kavisle dökülür.
        /// </summary>
        public static CargoFlyer LaunchShardToShelf(
            Vector3 worldStart,
            Quaternion worldRotation,
            Vector3 outwardDir,
            Vector3 dropPosition,
            Vector3 finalShelfPosition,
            Color color,
            Mesh shardMesh,
            Vector3 shardScale,
            float fallDuration,
            float pullDuration,
            Action<CargoFlyer> onLanded)
        {
            CargoFlyer flyer = Rent();

            flyer.CleanupTweens();
            flyer.SetMesh(shardMesh);
            flyer.transform.position = worldStart;
            flyer.transform.rotation = worldRotation;
            flyer.transform.localScale = shardScale;
            flyer.SetColor(color);
            flyer.gameObject.SetActive(true);

            flyer.AnimateShardToShelfWithPull(worldStart, outwardDir, dropPosition, finalShelfPosition, shardScale.x, fallDuration, pullDuration, () => onLanded?.Invoke(flyer));
            return flyer;
        }

        private void AnimateShardToShelfWithPull(
            Vector3 worldStart,
            Vector3 outwardDir,
            Vector3 dropPosition,
            Vector3 finalShelfPosition,
            float size,
            float fallDuration,
            float pullDuration,
            Action onLanded)
        {
            CleanupTweens();
            m_ActiveSequence = DOTween.Sequence();

            // 1. Patlama / Çatlak Açılması (Micro Seam Burst - ~0.06s)
            // Parça kendi kırılma yönünde hafifçe dışa fırlar (çatlaklar birbirinden ayrılır)
            float popDistance = Mathf.Clamp(0.12f * size, 0.02f, 0.15f);
            Vector3 popPos = worldStart + outwardDir * popDistance;
            float popDuration = 0.06f;

            m_ActiveSequence.Append(transform.DOMove(popPos, popDuration).SetEase(Ease.OutQuad));

            // 2. Ardından rafa dökülüş (Yerçekimi & Slide)
            float distToCenter = Mathf.Abs(finalShelfPosition.x - dropPosition.x);
            bool hasPull = distToCenter > 0.02f && pullDuration > 0.01f;

            float rollAngle = (finalShelfPosition.x > dropPosition.x ? -1f : 1f) * 180f * Mathf.Clamp01(distToCenter / 0.4f);

            if (!hasPull)
            {
                m_ActiveSequence.Append(transform.DOMoveX(finalShelfPosition.x, fallDuration).SetEase(Ease.OutQuad));
                m_ActiveSequence.Join(transform.DOMoveY(finalShelfPosition.y, fallDuration).SetEase(Ease.OutBounce));
                m_ActiveSequence.Join(transform.DOMoveZ(finalShelfPosition.z, fallDuration).SetEase(Ease.OutQuad));
                m_ActiveSequence.Join(transform.DORotate(new Vector3(
                    UnityEngine.Random.Range(-180f, 180f),
                    UnityEngine.Random.Range(-180f, 180f),
                    UnityEngine.Random.Range(-180f, 180f)),
                    fallDuration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
                m_ActiveSequence.Append(transform.DOPunchScale(new Vector3(0.12f, -0.12f, 0.12f) * size, 0.08f, 3, 0.5f));
            }
            else
            {
                float slideDuration = Mathf.Clamp(pullDuration * 0.85f, 0.18f, 0.35f);
                float intermediateX = Mathf.Lerp(dropPosition.x, finalShelfPosition.x, 0.45f);

                m_ActiveSequence.Append(transform.DOMoveY(dropPosition.y, fallDuration).SetEase(Ease.InQuad));
                m_ActiveSequence.Join(transform.DOMoveX(intermediateX, fallDuration).SetEase(Ease.InQuad));
                m_ActiveSequence.Join(transform.DOMoveZ(Mathf.Lerp(transform.position.z, finalShelfPosition.z, 0.5f), fallDuration).SetEase(Ease.Linear));
                m_ActiveSequence.Join(transform.DORotate(new Vector3(
                    UnityEngine.Random.Range(-120f, 120f),
                    UnityEngine.Random.Range(-120f, 120f),
                    rollAngle * 0.4f),
                    fallDuration, RotateMode.FastBeyond360).SetEase(Ease.InQuad));

                // Yere temas: pürüzsüz kayma ve yerleşme
                m_ActiveSequence.Append(transform.DOMoveY(finalShelfPosition.y + 0.04f * size, slideDuration * 0.35f).SetEase(Ease.OutQuad));
                m_ActiveSequence.Append(transform.DOMoveY(finalShelfPosition.y, slideDuration * 0.65f).SetEase(Ease.InQuad));

                m_ActiveSequence.Insert(popDuration + fallDuration, transform.DOMoveX(finalShelfPosition.x, slideDuration).SetEase(Ease.OutCubic));
                m_ActiveSequence.Insert(popDuration + fallDuration, transform.DOMoveZ(finalShelfPosition.z, slideDuration).SetEase(Ease.OutQuad));
                m_ActiveSequence.Insert(popDuration + fallDuration, transform.DORotate(new Vector3(0f, 0f, rollAngle), slideDuration, RotateMode.WorldAxisAdd).SetEase(Ease.OutCubic));

                m_ActiveSequence.Append(transform.DOPunchScale(new Vector3(0.08f, -0.08f, 0.08f) * size, 0.08f, 2, 0.4f));
            }

            m_ActiveSequence.OnComplete(() => onLanded?.Invoke());
        }

        /// <summary>
        /// Geriye uyumluluk için standart küp fırlatma desteği.
        /// </summary>
        public static CargoFlyer LaunchToShelf(
            Vector3 worldStart,
            Vector3 shelfPosition,
            Color color,
            float size,
            float fallDuration,
            Action<CargoFlyer> onLanded)
        {
            return LaunchToShelf(worldStart, shelfPosition, shelfPosition, color, size, fallDuration, 0f, onLanded);
        }

        public static CargoFlyer LaunchToShelf(
            Vector3 worldStart,
            Vector3 dropPosition,
            Vector3 finalShelfPosition,
            Color color,
            float size,
            float fallDuration,
            float pullDuration,
            Action<CargoFlyer> onLanded)
        {
            CargoFlyer flyer = Rent();

            flyer.CleanupTweens();
            flyer.transform.position = worldStart;
            flyer.transform.localScale = Vector3.one * size;
            flyer.transform.rotation = UnityEngine.Random.rotation;
            flyer.SetColor(color);
            flyer.gameObject.SetActive(true);

            flyer.AnimateToShelfWithPull(dropPosition, finalShelfPosition, size, fallDuration, pullDuration, () => onLanded?.Invoke(flyer));
            return flyer;
        }

        private void AnimateToShelfWithPull(
            Vector3 dropPosition,
            Vector3 finalShelfPosition,
            float size,
            float fallDuration,
            float pullDuration,
            Action onLanded)
        {
            CleanupTweens();
            m_ActiveSequence = DOTween.Sequence();

            float distToCenter = Mathf.Abs(finalShelfPosition.x - dropPosition.x);
            bool hasPull = distToCenter > 0.02f && pullDuration > 0.01f;

            if (!hasPull)
            {
                m_ActiveSequence.Append(transform.DOMoveX(finalShelfPosition.x, fallDuration).SetEase(Ease.OutQuad));
                m_ActiveSequence.Join(transform.DOMoveY(finalShelfPosition.y, fallDuration).SetEase(Ease.OutBounce));
                m_ActiveSequence.Join(transform.DOMoveZ(finalShelfPosition.z, fallDuration).SetEase(Ease.OutQuad));
                m_ActiveSequence.Join(transform.DORotate(new Vector3(
                    UnityEngine.Random.Range(-180f, 180f),
                    UnityEngine.Random.Range(-180f, 180f),
                    UnityEngine.Random.Range(-180f, 180f)),
                    fallDuration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
                m_ActiveSequence.Append(transform.DOPunchScale(new Vector3(0.12f, -0.12f, 0.12f) * size, 0.08f, 3, 0.5f));
            }
            else
            {
                float slideDuration = Mathf.Clamp(pullDuration * 0.85f, 0.18f, 0.35f);
                float rollAngle = (finalShelfPosition.x > dropPosition.x ? -1f : 1f) * 200f * Mathf.Clamp01(distToCenter / 0.4f);

                float intermediateX = Mathf.Lerp(dropPosition.x, finalShelfPosition.x, 0.45f);
                m_ActiveSequence.Append(transform.DOMoveY(dropPosition.y, fallDuration).SetEase(Ease.InQuad));
                m_ActiveSequence.Join(transform.DOMoveX(intermediateX, fallDuration).SetEase(Ease.InQuad));
                m_ActiveSequence.Join(transform.DOMoveZ(Mathf.Lerp(transform.position.z, finalShelfPosition.z, 0.5f), fallDuration).SetEase(Ease.Linear));
                m_ActiveSequence.Join(transform.DORotate(new Vector3(
                    UnityEngine.Random.Range(-120f, 120f),
                    UnityEngine.Random.Range(-120f, 120f),
                    rollAngle * 0.4f),
                    fallDuration, RotateMode.FastBeyond360).SetEase(Ease.InQuad));

                m_ActiveSequence.Append(transform.DOMoveY(finalShelfPosition.y + 0.04f * size, slideDuration * 0.35f).SetEase(Ease.OutQuad));
                m_ActiveSequence.Append(transform.DOMoveY(finalShelfPosition.y, slideDuration * 0.65f).SetEase(Ease.InQuad));

                m_ActiveSequence.Insert(fallDuration, transform.DOMoveX(finalShelfPosition.x, slideDuration).SetEase(Ease.OutCubic));
                m_ActiveSequence.Insert(fallDuration, transform.DOMoveZ(finalShelfPosition.z, slideDuration).SetEase(Ease.OutQuad));
                m_ActiveSequence.Insert(fallDuration, transform.DORotate(new Vector3(0f, 0f, rollAngle), slideDuration, RotateMode.WorldAxisAdd).SetEase(Ease.OutCubic));

                m_ActiveSequence.Append(transform.DOPunchScale(new Vector3(0.08f, -0.08f, 0.08f) * size, 0.08f, 2, 0.4f));
            }

            m_ActiveSequence.OnComplete(() => onLanded?.Invoke());
        }

        /// <summary>
        /// Rafta bekleyen parçayı hareket halindeki vagona doğru kavisli bir yayla takip ettirerek fırlatır.
        /// Vagon ray üzerinde ilerlese dahi hedef şaşmadan tam kasaya oturur.
        /// </summary>
        public void FlowToMovingTarget(Transform targetWagon, float duration, float arcHeight, Action onArrive)
        {
            CleanupTweens();
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(FlowToMovingTargetRoutine(targetWagon, duration, arcHeight, onArrive));
            }
            else
            {
                onArrive?.Invoke();
                Release();
            }
        }

        private System.Collections.IEnumerator FlowToMovingTargetRoutine(
            Transform targetWagon,
            float duration,
            float arcHeight,
            Action onArrive)
        {
            Vector3 startPos = transform.position;
            float elapsed = 0f;

            Vector3 randomTorque = new Vector3(
                UnityEngine.Random.Range(-360f, 360f),
                UnityEngine.Random.Range(-360f, 360f),
                UnityEngine.Random.Range(-360f, 360f)
            );

            Vector3 initialScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                Vector3 currentTargetPos = (targetWagon != null) ? targetWagon.position : startPos;

                Vector3 linearPos = Vector3.Lerp(startPos, currentTargetPos, t);
                float arcY = 4f * arcHeight * t * (1f - t);
                linearPos.y += arcY;

                transform.position = linearPos;
                transform.Rotate(randomTorque * Time.deltaTime, Space.Self);

                // Kasaya girerken hafifçe küçülme
                if (t > 0.75f)
                {
                    float scaleT = (t - 0.75f) / 0.25f;
                    transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, scaleT * scaleT);
                }

                yield return null;
            }

            onArrive?.Invoke();
            Release();
        }

        public void FlowToCart(Vector3 cartPosition, float delay, float flowDuration, float arcHeight, Action onArrive)
        {
            CleanupTweens();

            Vector3 start = transform.position;
            Vector3 mid = (start + cartPosition) * 0.5f + Vector3.up * arcHeight;

            DOVirtual.DelayedCall(delay, () =>
            {
                if (!gameObject.activeInHierarchy)
                {
                    onArrive?.Invoke();
                    Release();
                    return;
                }

                Sequence flowSeq = DOTween.Sequence();

                Vector3[] path = new Vector3[] { start, mid, cartPosition };
                flowSeq.Append(transform.DOPath(path, flowDuration, PathType.CatmullRom).SetEase(Ease.InQuad));
                flowSeq.Join(transform.DORotate(new Vector3(
                    UnityEngine.Random.Range(-260f, 260f),
                    UnityEngine.Random.Range(-260f, 260f),
                    UnityEngine.Random.Range(-260f, 260f)),
                    flowDuration, RotateMode.FastBeyond360));

                flowSeq.Append(transform.DOScale(0f, 0.08f).SetEase(Ease.InBack));

                flowSeq.OnComplete(() =>
                {
                    onArrive?.Invoke();
                    Release();
                });

                m_ActiveSequence = flowSeq;
            });
        }

        private void SetColor(Color color)
        {
            if (m_Renderer == null) m_Renderer = GetComponent<MeshRenderer>();
            if (m_Renderer == null) return;

            if (m_Material == null)
            {
                m_Material = CartoonShader.CreateMaterial(color, "CargoFlyer_Mat");
            }
            else
            {
                CartoonShader.ApplyColor(m_Material, color);
            }

            int subMeshCount = m_MeshFilter != null && m_MeshFilter.sharedMesh != null ? m_MeshFilter.sharedMesh.subMeshCount : 1;
            if (subMeshCount > 1)
            {
                Material[] mats = new Material[subMeshCount];
                for (int i = 0; i < subMeshCount; i++) mats[i] = m_Material;
                m_Renderer.sharedMaterials = mats;
            }
            else
            {
                m_Renderer.sharedMaterial = m_Material;
            }
        }

        private void CleanupTweens()
        {
            if (m_ActiveSequence != null)
            {
                m_ActiveSequence.Kill();
                m_ActiveSequence = null;
            }
            transform.DOKill();
        }

        private void OnDisable()
        {
            CleanupTweens();
        }

        #region ♻️ Havuz

        private static CargoFlyer Rent()
        {
            while (s_Pool.Count > 0)
            {
                CargoFlyer pooled = s_Pool.Pop();
                if (pooled != null) return pooled;
            }

            return Create();
        }

        private static CargoFlyer Create()
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "CargoFlyer";

            Collider collider = obj.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            obj.transform.SetParent(EnsurePoolRoot(), false);

            CargoFlyer flyer = obj.AddComponent<CargoFlyer>();
            flyer.m_MeshFilter = obj.GetComponent<MeshFilter>();
            flyer.m_Renderer = renderer;
            return flyer;
        }

        private static Transform EnsurePoolRoot()
        {
            if (s_PoolRoot != null) return s_PoolRoot;

            GameObject root = new GameObject("[CargoFlyers]");
            s_PoolRoot = root.transform;

            if (Application.isPlaying) DontDestroyOnLoad(root);

            return s_PoolRoot;
        }

        private void Release()
        {
            CleanupTweens();
            gameObject.SetActive(false);
            transform.SetParent(EnsurePoolRoot(), false);
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
            s_Pool.Push(this);
        }

        private void OnDestroy()
        {
            CleanupTweens();
            if (m_Material != null) Destroy(m_Material);
        }

        #endregion
    }
}
