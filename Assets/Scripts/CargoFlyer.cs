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

        /// <summary>
        /// Küp cam gibi kırıldığında parçayı (shard) küp içindeki gerçek ofsetinden dışarı fırlatır (belirgin patlama),
        /// havada takla atarak yerçekimiyle önce çerçevenin altındaki rafa (belirlenen alan) dökülüp zıplar,
        /// ardından oradan slottaki vagona doğru şelale gibi kayarak dökülür.
        /// </summary>
        public static CargoFlyer LaunchGlassShardViaShelfToWagon(
            Vector3 worldStart,
            Quaternion worldRotation,
            Vector3 outwardDir,
            Vector3 dropPosition,
            Vector3 shelfTargetPosition,
            Transform targetWagon,
            Vector3 targetOffset,
            Color color,
            Mesh shardMesh,
            Vector3 shardScale,
            float delay,
            float fallToShelfDuration,
            float pauseOnShelf,
            float slideToWagonDuration,
            Action onArrive)
        {
            CargoFlyer flyer = Rent();
            flyer.CleanupTweens();
            flyer.transform.position = worldStart;
            flyer.transform.rotation = worldRotation;
            flyer.transform.localScale = shardScale;
            flyer.SetMesh(shardMesh != null ? shardMesh : FracturedCubeData.Instance.IntactMesh);
            flyer.SetColor(color);
            flyer.gameObject.SetActive(true);

            flyer.AnimateGlassShardViaShelfToWagon(
                outwardDir,
                dropPosition,
                shelfTargetPosition,
                targetWagon,
                targetOffset,
                shardScale,
                delay,
                fallToShelfDuration,
                pauseOnShelf,
                slideToWagonDuration,
                onArrive
            );
            return flyer;
        }

        private void AnimateGlassShardViaShelfToWagon(
            Vector3 outwardDir,
            Vector3 dropPosition,
            Vector3 shelfTargetPosition,
            Transform targetWagon,
            Vector3 targetOffset,
            Vector3 initialScale,
            float delay,
            float fallDuration,
            float pauseOnShelf,
            float slideDuration,
            Action onArrive)
        {
            CleanupTweens();
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(GlassShardViaShelfRoutine(
                    outwardDir,
                    dropPosition,
                    shelfTargetPosition,
                    targetWagon,
                    targetOffset,
                    initialScale,
                    delay,
                    fallDuration,
                    pauseOnShelf,
                    slideDuration,
                    onArrive
                ));
            }
            else
            {
                onArrive?.Invoke();
                Release();
            }
        }

        private System.Collections.IEnumerator GlassShardViaShelfRoutine(
            Vector3 outwardDir,
            Vector3 dropPosition,
            Vector3 shelfTargetPosition,
            Transform targetWagon,
            Vector3 targetOffset,
            Vector3 initialScale,
            float delay,
            float fallDuration,
            float pauseOnShelf,
            float slideDuration,
            Action onArrive)
        {
            if (delay > 0.001f) yield return new WaitForSeconds(delay);

            Vector3 startPos = transform.position;

            // 1. AŞAMA: Belirgin 3D Cam Çatlama & Dışa Fırlama (~0.12s)
            // Parçalar birbirlerinden net bir mesafeyle dışa açılarak kırık görüntüsünü belirginleştirir
            Vector3 popDir = (outwardDir.sqrMagnitude > 0.001f ? outwardDir.normalized : Vector3.up);
            float popDist = UnityEngine.Random.Range(0.24f, 0.44f);
            Vector3 popPos = startPos + popDir * popDist + Vector3.up * UnityEngine.Random.Range(0.08f, 0.22f);

            float burstDuration = 0.12f;
            float elapsedBurst = 0f;

            Vector3 randomTorque = new Vector3(
                UnityEngine.Random.Range(-480f, 480f),
                UnityEngine.Random.Range(-480f, 480f),
                UnityEngine.Random.Range(-480f, 480f)
            );

            while (elapsedBurst < burstDuration)
            {
                elapsedBurst += Time.deltaTime;
                float tb = Mathf.Clamp01(elapsedBurst / burstDuration);
                transform.position = Vector3.Lerp(startPos, popPos, Mathf.Sin(tb * Mathf.PI * 0.5f));
                transform.Rotate(randomTorque * Time.deltaTime, Space.Self);
                yield return null;
            }

            // 2. AŞAMA: Yerçekimiyle Raf Alanına (Belirlenen Alan) Düşüş ve Zıplama
            float elapsedFall = 0f;
            Vector3 fallStartPos = transform.position;

            while (elapsedFall < fallDuration)
            {
                elapsedFall += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedFall / fallDuration);

                // Yerçekimi ivmesi (InQuad Y ekseni)
                float tY = t * t;
                float currentY = Mathf.Lerp(fallStartPos.y, dropPosition.y, tY);
                float currentX = Mathf.Lerp(fallStartPos.x, dropPosition.x, Mathf.SmoothStep(0f, 1f, t));
                float currentZ = Mathf.Lerp(fallStartPos.z, dropPosition.z, t);

                transform.position = new Vector3(currentX, currentY, currentZ);
                transform.Rotate(randomTorque * (1f + t * 0.5f) * Time.deltaTime, Space.Self);
                yield return null;
            }

            // Rafa temas: Hafif yaylanma / zıplama ve toplanma (Settle)
            float elapsedSettle = 0f;
            float settleDuration = Mathf.Max(0.08f, pauseOnShelf);
            Vector3 preSettlePos = transform.position;

            while (elapsedSettle < settleDuration)
            {
                elapsedSettle += Time.deltaTime;
                float ts = Mathf.Clamp01(elapsedSettle / settleDuration);
                float bounceY = Mathf.Sin(ts * Mathf.PI) * 0.08f;
                Vector3 settlePos = Vector3.Lerp(preSettlePos, shelfTargetPosition, ts);
                settlePos.y += bounceY;
                transform.position = settlePos;
                yield return null;
            }

            // 3. AŞAMA: Raftan Slottaki Vagona Doğru Kayarak / Şelale Gibi Akış
            float elapsedSlide = 0f;
            Vector3 slideStartPos = transform.position;

            while (elapsedSlide < slideDuration)
            {
                elapsedSlide += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedSlide / slideDuration);

                Vector3 wagonEndPos = (targetWagon != null) ? (targetWagon.position + targetOffset) : (slideStartPos + Vector3.down * 4f);

                // Kavisli akış (raf kenarından vagona dökülüş yayı)
                Vector3 midPoint = Vector3.Lerp(slideStartPos, wagonEndPos, 0.40f) + Vector3.down * 0.08f;
                float u = 1f - t;
                Vector3 slidePos = (u * u * slideStartPos) + (2f * u * t * midPoint) + (t * t * wagonEndPos);

                transform.position = slidePos;
                transform.Rotate(randomTorque * Time.deltaTime, Space.Self);

                // Vagona girerken yumuşak küçülme
                if (t > 0.65f)
                {
                    float scaleT = (t - 0.65f) / 0.35f;
                    transform.localScale = Vector3.Lerp(initialScale, initialScale * 0.25f, scaleT * scaleT);
                }

                yield return null;
            }

            onArrive?.Invoke();
            Release();
        }

        /// <summary>
        /// Geriye uyumluluk için direkt vagona fırlatma.
        /// </summary>
        public static CargoFlyer LaunchGlassShardToWagon(
            Vector3 worldStart,
            Quaternion worldRotation,
            Vector3 outwardDir,
            Transform targetWagon,
            Vector3 targetOffset,
            Color color,
            Mesh shardMesh,
            Vector3 shardScale,
            float delay,
            float duration,
            Action onArrive)
        {
            Vector3 fallbackDrop = worldStart + Vector3.down * 2f;
            return LaunchGlassShardViaShelfToWagon(
                worldStart,
                worldRotation,
                outwardDir,
                fallbackDrop,
                fallbackDrop,
                targetWagon,
                targetOffset,
                color,
                shardMesh,
                shardScale,
                delay,
                duration * 0.45f,
                0.08f,
                duration * 0.45f,
                onArrive
            );
        }

        /// <summary>
        /// Mermi darbesiyle küp parçalandığında Voronoi kırık parçalarını mermi yönünde ve 360 radyal
        /// uzayda takla atarak fırlatır, havada süzülürken pürüzsüzce küçülerek (Ease.InBack) yok eder.
        /// </summary>
        public static CargoFlyer LaunchShardScatterAndVanish(
            Vector3 worldStart,
            Quaternion worldRotation,
            Vector3 outwardDir,
            Color color,
            Mesh shardMesh,
            Vector3 shardScale,
            Vector3 impactDirection,
            float duration = 0.52f)
        {
            CargoFlyer flyer = Rent();
            flyer.CleanupTweens();
            flyer.SetMesh(shardMesh != null ? shardMesh : FracturedCubeData.Instance.IntactMesh);
            flyer.transform.position = worldStart;
            flyer.transform.rotation = worldRotation;
            flyer.transform.localScale = shardScale;
            flyer.SetColor(color);
            flyer.gameObject.SetActive(true);

            // Mermi darbe yönü + dışa saçılma yönü
            Vector3 flightDir = (outwardDir * 0.55f + impactDirection * 0.45f).normalized;
            if (flightDir.sqrMagnitude < 0.001f) flightDir = UnityEngine.Random.insideUnitSphere.normalized;
            // Kameraya doğru hafif kabarma (-Z)
            flightDir.z = -Mathf.Abs(flightDir.z) * 0.8f - 0.25f;

            float scatterDist = UnityEngine.Random.Range(0.40f, 0.90f);
            Vector3 targetPos = worldStart + flightDir * scatterDist;
            Vector3 randomTorque = new Vector3(
                UnityEngine.Random.Range(-360f, 360f),
                UnityEngine.Random.Range(-360f, 360f),
                UnityEngine.Random.Range(-360f, 360f)
            );

            Sequence seq = DOTween.Sequence();
            // 1. Dışarıya fırlama ve takla atma
            seq.Append(flyer.transform.DOJump(targetPos, UnityEngine.Random.Range(0.20f, 0.45f), 1, duration).SetEase(Ease.OutQuad));
            seq.Join(flyer.transform.DORotate(randomTorque, duration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
            // 2. Havada süzüldükten sonra pürüzsüzce küçülerek yok olma
            seq.Insert(duration * 0.40f, flyer.transform.DOScale(Vector3.zero, duration * 0.60f).SetEase(Ease.InBack));
            seq.OnComplete(() =>
            {
                flyer.Release();
            });

            flyer.m_ActiveSequence = seq;
            return flyer;
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

        /// <summary>
        /// Küp parçalanınca parçanın tam olarak küpün olduğu alana kir/kırıntı gibi
        /// 3D yay çizerek saçılmasını, havada takla atmasını ve zemine sekerek oturmasını sağlar.
        /// Parça havuza dönmez, raydan geçen vagonun vakumlaması için orada bekler.
        /// </summary>
        public static CargoFlyer LaunchShardLocalDrop(
            Vector3 worldStart,
            Quaternion worldRotation,
            Vector3 outwardDir,
            Vector3 dropPosition,
            Color color,
            Mesh shardMesh,
            Vector3 shardScale,
            float delay,
            float fallDuration,
            Action onLanded,
            float arcHeight = 0.12f)
        {
            CargoFlyer flyer = Rent();
            flyer.CleanupTweens();
            flyer.transform.position = worldStart;
            flyer.transform.rotation = worldRotation;
            flyer.transform.localScale = shardScale;
            flyer.SetMesh(shardMesh != null ? shardMesh : FracturedCubeData.Instance.IntactMesh);
            flyer.SetColor(color);
            flyer.gameObject.SetActive(true);

            flyer.AnimateShardLocalDrop(
                outwardDir,
                dropPosition,
                shardScale,
                delay,
                fallDuration,
                arcHeight,
                onLanded
            );
            return flyer;
        }

        private void AnimateShardLocalDrop(
            Vector3 outwardDir,
            Vector3 dropPosition,
            Vector3 initialScale,
            float delay,
            float fallDuration,
            float arcHeight,
            Action onLanded)
        {
            CleanupTweens();
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(ShardLocalDropRoutine(outwardDir, dropPosition, initialScale, delay, fallDuration, arcHeight, onLanded));
            }
            else
            {
                onLanded?.Invoke();
            }
        }

        private System.Collections.IEnumerator ShardLocalDropRoutine(
            Vector3 outwardDir,
            Vector3 dropPosition,
            Vector3 initialScale,
            float delay,
            float fallDuration,
            float arcHeight,
            Action onLanded)
        {
            if (delay > 0.001f) yield return new WaitForSeconds(delay);

            Vector3 startPos = transform.position;

            // Rastgele 3D dönme/takla torku (parçalar havada takla atar)
            Vector3 randomTorque = new Vector3(
                UnityEngine.Random.Range(-350f, 350f),
                UnityEngine.Random.Range(-350f, 350f),
                UnityEngine.Random.Range(-350f, 350f)
            );

            // 1. AŞAMA: 3D Balistik Saçılma Yayı (Ease-Out patlama + Kameraya doğru kabarma)
            float elapsedFall = 0f;
            fallDuration = Mathf.Max(0.12f, fallDuration);

            while (elapsedFall < fallDuration)
            {
                elapsedFall += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedFall / fallDuration);

                // Patlama anında hızlı dışa fırlama, inişe doğru pürüzsüz yavaşlama (Ease-Out)
                float tPlanar = 1f - Mathf.Pow(1f - t, 2.2f);
                Vector3 currentPlanar = Vector3.Lerp(startPos, dropPosition, tPlanar);

                // 3D Parabolik Kabarma Yayı: Kameraya (-Z) doğru havalanma ve hafif yukarı yaylanma
                float arc = Mathf.Sin(t * Mathf.PI);
                float zArc = -arc * arcHeight;
                float yArc = arc * (arcHeight * 0.35f);

                transform.position = new Vector3(currentPlanar.x, currentPlanar.y + yArc, currentPlanar.z + zArc);

                // Havada takla atma (yere yaklaştıkça hafif sönümlenir)
                transform.Rotate(randomTorque * (1f - t * 0.45f) * Time.deltaTime, Space.Self);
                yield return null;
            }

            // 2. AŞAMA: Mikro Sekme & Çökme (Settle & Dampen Bounce - ~0.07s)
            float settleDuration = 0.07f;
            float elapsedSettle = 0f;
            Vector3 groundPos = dropPosition;

            while (elapsedSettle < settleDuration)
            {
                elapsedSettle += Time.deltaTime;
                float tb = Mathf.Clamp01(elapsedSettle / settleDuration);

                // Sönümlü mikro sıçrama / çökme
                float settleBounce = Mathf.Sin(tb * Mathf.PI) * 0.015f * (1f - tb);
                transform.position = new Vector3(groundPos.x, groundPos.y + settleBounce, groundPos.z - settleBounce);

                // Temasta hafif ezilme / yaylanma (squash & stretch)
                Vector3 squashScale = initialScale;
                squashScale.x *= (1f + settleBounce * 1.5f);
                squashScale.y *= (1f + settleBounce * 1.5f);
                squashScale.z *= (1f - settleBounce * 0.8f);
                transform.localScale = squashScale;

                yield return null;
            }

            // Tam dinlenme pozisyonuna ve orijinal ölçeğe sabitle
            transform.position = groundPos;
            transform.localScale = initialScale;
            onLanded?.Invoke();
        }

        /// <summary>
        /// Küpün yanından geçen hareket halindeki vagona doğru parçayı direkt vakumlar.
        /// </summary>
        public static CargoFlyer LaunchShardVacuumToMovingWagon(
            Vector3 worldStart,
            Quaternion worldRotation,
            Color color,
            Mesh shardMesh,
            Vector3 shardScale,
            Transform targetWagon,
            Vector3 targetOffset,
            float duration,
            float arcHeight,
            Action onArrived)
        {
            CargoFlyer flyer = Rent();
            flyer.CleanupTweens();
            flyer.SetMesh(shardMesh);
            flyer.transform.position = worldStart;
            flyer.transform.rotation = worldRotation;
            flyer.transform.localScale = shardScale;
            flyer.SetColor(color);
            flyer.gameObject.SetActive(true);

            flyer.VacuumPullToMovingTarget(targetWagon, targetOffset, duration, arcHeight, onArrived);
            return flyer;
        }

        /// <summary>
        /// Yerde bekleyen parçayı, yanından geçen hareket halindeki vagona doğru
        /// dinamik bir vakum çekim kavisle (suction curve) çeker.
        /// </summary>
        public void VacuumPullToMovingTarget(
            Transform targetWagon,
            Vector3 targetOffset,
            float duration,
            float arcHeight,
            Action onArrived)
        {
            CleanupTweens();
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(VacuumPullRoutine(targetWagon, targetOffset, duration, arcHeight, onArrived));
            }
            else
            {
                onArrived?.Invoke();
                Release();
            }
        }

        private System.Collections.IEnumerator VacuumPullRoutine(
            Transform targetWagon,
            Vector3 targetOffset,
            float duration,
            float arcHeight,
            Action onArrived)
        {
            Vector3 startPos = transform.position;
            Vector3 initialScale = transform.localScale;
            float elapsed = 0f;

            Vector3 randomTorque = new Vector3(
                UnityEngine.Random.Range(-500f, 500f),
                UnityEngine.Random.Range(-500f, 500f),
                UnityEngine.Random.Range(-500f, 500f)
            );

            // Hafif havalanma / çekilme titremesi (Anticipation 0.04s)
            Vector3 liftPos = startPos + Vector3.up * 0.06f;
            float liftElapsed = 0f;
            float liftDuration = 0.04f;
            while (liftElapsed < liftDuration)
            {
                liftElapsed += Time.deltaTime;
                float lt = Mathf.Clamp01(liftElapsed / liftDuration);
                transform.position = Vector3.Lerp(startPos, liftPos, lt);
                yield return null;
            }

            startPos = transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                Vector3 currentWagonEnd = (targetWagon != null)
                    ? (targetWagon.position + (targetWagon.rotation * targetOffset))
                    : (startPos + Vector3.up * 2f);

                // Dinamik vakum çekim eğrisi (Bezier ile hareketli hedef takibi)
                Vector3 midPoint = Vector3.Lerp(startPos, currentWagonEnd, 0.45f) + Vector3.up * arcHeight;
                float u = 1f - t;
                Vector3 suctionPos = (u * u * startPos) + (2f * u * t * midPoint) + (t * t * currentWagonEnd);

                transform.position = suctionPos;
                transform.Rotate(randomTorque * (1f + t * 1.5f) * Time.deltaTime, Space.Self);

                // Kasaya girerken pürüzsüz vakum küçülmesi
                if (t > 0.65f)
                {
                    float scaleT = (t - 0.65f) / 0.35f;
                    transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, scaleT * scaleT);
                }

                yield return null;
            }

            onArrived?.Invoke();
            Release();
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
