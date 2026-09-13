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

        private MeshRenderer m_Renderer;
        private Material m_Material;
        private Sequence m_ActiveSequence;

        /// <summary>
        /// <summary>
        /// Parçayı küpten alt rafa fırlatır.
        /// Önce yerçekimiyle alt rafa düşer (başka yere düşse bile),
        /// ardından hafifçe belirlenen merkez toplanma alanına doğru çekilip öbeklenir.
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
                // Doğrudan hedefe akıcı düşüş ve yumuşak zıplama
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
                // Kesintisiz, pürüzsüz akış: Parça havada düşüş eğrisindeyken merkeze doğru ivmelenir,
                // yere değer değmez DURAKSAMADAN pürüzsüzce yuvarlanarak/kayarak merkeze toplanır.
                float slideDuration = Mathf.Clamp(pullDuration * 0.85f, 0.18f, 0.35f);
                float rollAngle = (finalShelfPosition.x > dropPosition.x ? -1f : 1f) * 200f * Mathf.Clamp01(distToCenter / 0.4f);

                // 1. Havada süzülerek düşüş: Y rafa doğru inerken, X merkeze doğru akışa başlar
                float intermediateX = Mathf.Lerp(dropPosition.x, finalShelfPosition.x, 0.45f);
                m_ActiveSequence.Append(transform.DOMoveY(dropPosition.y, fallDuration).SetEase(Ease.InQuad));
                m_ActiveSequence.Join(transform.DOMoveX(intermediateX, fallDuration).SetEase(Ease.InQuad));
                m_ActiveSequence.Join(transform.DOMoveZ(Mathf.Lerp(transform.position.z, finalShelfPosition.z, 0.5f), fallDuration).SetEase(Ease.Linear));
                m_ActiveSequence.Join(transform.DORotate(new Vector3(
                    UnityEngine.Random.Range(-120f, 120f),
                    UnityEngine.Random.Range(-120f, 120f),
                    rollAngle * 0.4f),
                    fallDuration, RotateMode.FastBeyond360).SetEase(Ease.InQuad));

                // 2. Yere temas: HİÇ DURAKSAMADAN (hitch yok!) anında akıcı kayma ve mikro yaylanma
                m_ActiveSequence.Append(transform.DOMoveY(finalShelfPosition.y + 0.04f * size, slideDuration * 0.35f).SetEase(Ease.OutQuad));
                m_ActiveSequence.Append(transform.DOMoveY(finalShelfPosition.y, slideDuration * 0.65f).SetEase(Ease.InQuad));

                // X ve Z hareketi düşüş anından itibaren kesintisiz devam eder (Insert ile tam temas anına bağlanır)
                m_ActiveSequence.Insert(fallDuration, transform.DOMoveX(finalShelfPosition.x, slideDuration).SetEase(Ease.OutCubic));
                m_ActiveSequence.Insert(fallDuration, transform.DOMoveZ(finalShelfPosition.z, slideDuration).SetEase(Ease.OutQuad));
                m_ActiveSequence.Insert(fallDuration, transform.DORotate(new Vector3(0f, 0f, rollAngle), slideDuration, RotateMode.WorldAxisAdd).SetEase(Ease.OutCubic));

                // Hedefe yerleştiğinde hafif tatlı yaylanma
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
            Transform targetWagon, float duration, float arcHeight, Action onArrive)
        {
            Vector3 startPos = transform.position;
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            Vector3 rotAxis = UnityEngine.Random.onUnitSphere;
            float rotSpeed = UnityEngine.Random.Range(360f, 720f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / duration);

                // Kavis ve takip: vagon hareket ettikçe hedefin güncel konumunu esas al
                Vector3 endPos = targetWagon != null ? targetWagon.position : startPos;
                Vector3 current = Vector3.Lerp(startPos, endPos, p);
                current.y += Mathf.Sin(p * Mathf.PI) * arcHeight;

                transform.position = current;
                transform.Rotate(rotAxis, rotSpeed * Time.deltaTime, Space.World);

                if (p > 0.82f)
                {
                    // Kasaya girerken zarifçe küçül
                    float shrinkP = (p - 0.82f) / 0.18f;
                    transform.localScale = Vector3.Lerp(startScale, Vector3.zero, shrinkP);
                }

                yield return null;
            }

            onArrive?.Invoke();
            Release();
        }

        /// <summary>
        /// İki aşamalı voksel akışını başlatır:
        /// 1) Küpten alt rafa (shelfPosition) düşüp zıplayarak birikir.
        /// 2) Belirtilen birikme süresinden (accumulateDelay) sonra vagona kavis çizerek akar.
        /// </summary>
        public static void LaunchAccumulateAndFlow(
            Vector3 worldStart,
            Vector3 shelfPosition,
            Transform targetWagon,
            Color color,
            float size,
            float fallDuration,
            float accumulateDelay,
            float flowDuration,
            float flowArcHeight,
            Action onArrive)
        {
            CargoFlyer flyer = Rent();

            flyer.CleanupTweens();
            flyer.transform.position = worldStart;
            flyer.transform.localScale = Vector3.one * size;
            flyer.transform.rotation = UnityEngine.Random.rotation;
            flyer.SetColor(color);
            flyer.gameObject.SetActive(true);

            flyer.AnimateAccumulateAndFlow(
                shelfPosition, targetWagon, size,
                fallDuration, accumulateDelay, flowDuration, flowArcHeight, onArrive
            );
        }

        /// <summary>
        /// Doğrudan hedefe uçuran tek aşamalı yedek fırlatıcı (eski çağrılar için geriye dönük uyumlu).
        /// </summary>
        public static void Launch(Vector3 worldStart, Transform target, Color color,
                                  float size, float duration, float arcHeight,
                                  Action onArrive)
        {
            if (target == null)
            {
                onArrive?.Invoke();
                return;
            }

            CargoFlyer flyer = Rent();

            flyer.CleanupTweens();
            flyer.transform.position = worldStart;
            flyer.transform.localScale = Vector3.one * size;
            flyer.SetColor(color);
            flyer.gameObject.SetActive(true);

            Vector3 endPos = target.position;
            flyer.m_ActiveSequence = DOTween.Sequence();
            flyer.m_ActiveSequence.Append(flyer.transform.DOJump(endPos, arcHeight, 1, duration).SetEase(Ease.InQuad));
            flyer.m_ActiveSequence.Join(flyer.transform.DORotate(new Vector3(
                UnityEngine.Random.Range(-180f, 180f),
                UnityEngine.Random.Range(-180f, 180f),
                UnityEngine.Random.Range(-180f, 180f)), duration, RotateMode.FastBeyond360));
            flyer.m_ActiveSequence.OnComplete(() =>
            {
                onArrive?.Invoke();
                flyer.Release();
            });
        }

        private void AnimateAccumulateAndFlow(
            Vector3 shelfPosition,
            Transform targetWagon,
            float size,
            float fallDuration,
            float accumulateDelay,
            float flowDuration,
            float flowArcHeight,
            Action onArrive)
        {
            CleanupTweens();
            m_ActiveSequence = DOTween.Sequence();

            // ─── 1. AŞAMA: Tablonun altındaki rafa düşüş & zıplayarak birikme ───
            m_ActiveSequence.Append(transform.DOMoveX(shelfPosition.x, fallDuration).SetEase(Ease.OutQuad));
            m_ActiveSequence.Join(transform.DOMoveY(shelfPosition.y, fallDuration).SetEase(Ease.OutBounce));
            m_ActiveSequence.Join(transform.DOMoveZ(shelfPosition.z, fallDuration).SetEase(Ease.OutQuad));
            m_ActiveSequence.Join(transform.DORotate(new Vector3(
                UnityEngine.Random.Range(-180f, 180f),
                UnityEngine.Random.Range(-180f, 180f),
                UnityEngine.Random.Range(-180f, 180f)),
                fallDuration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));

            // Rafa temas anında tatlı bir yaylanma / squash-stretch etkisi
            m_ActiveSequence.Append(transform.DOPunchScale(new Vector3(0.22f, -0.22f, 0.22f) * size, 0.16f, 6, 0.5f));

            // ─── BEKLEME: Rafta görünür şekilde birikme süresi ───
            m_ActiveSequence.AppendInterval(accumulateDelay);

            // ─── 2. AŞAMA: Raftan vagona akış (Cascade Flow) ───
            m_ActiveSequence.AppendCallback(() =>
            {
                if (targetWagon == null || !gameObject.activeSelf)
                {
                    onArrive?.Invoke();
                    Release();
                    return;
                }

                Vector3 destination = targetWagon.position;
                Sequence flowSeq = DOTween.Sequence();

                // Raftan vagona doğru kavisli uçuş
                flowSeq.Append(transform.DOJump(destination, flowArcHeight, 1, flowDuration).SetEase(Ease.InQuad));
                flowSeq.Join(transform.DORotate(new Vector3(
                    UnityEngine.Random.Range(-180f, 180f),
                    UnityEngine.Random.Range(-180f, 180f),
                    UnityEngine.Random.Range(-180f, 180f)),
                    flowDuration, RotateMode.FastBeyond360));

                // Kasaya girerken zarifçe içeri küçülme
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
                m_Renderer.sharedMaterial = m_Material;
                return;
            }

            CartoonShader.ApplyColor(m_Material, color);
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

            return obj.AddComponent<CargoFlyer>();
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
