using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Kırılan küpü tablodan vagona uçuran mini küp.
    ///
    /// Küp başına tek bir parça uçar: bir parça = kasaya bir küp. Böylece görsel
    /// ile yük sayacı birebir örtüşür. Kırılma hissini zaten voksel patlaması veriyor.
    ///
    /// Parçalar havuzdan gelir; hızlı tıklamada yüzlerce nesne oluşmasın diye
    /// kullanılan parçalar geri dönüştürülür.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Cargo Flyer")]
    public class CargoFlyer : MonoBehaviour
    {
        private static readonly Stack<CargoFlyer> s_Pool = new Stack<CargoFlyer>();
        private static Transform s_PoolRoot;

        private MeshRenderer m_Renderer;
        private Material m_Material;

        /// <summary>
        /// Bir küpü hedefe doğru uçurur. Varışta <paramref name="onArrive"/> çağrılır.
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

            flyer.transform.position = worldStart;
            flyer.transform.localScale = Vector3.one * size;
            flyer.SetColor(color);
            flyer.gameObject.SetActive(true);

            flyer.StartCoroutine(flyer.FlyRoutine(worldStart, target, duration, arcHeight, onArrive));
        }

        private IEnumerator FlyRoutine(Vector3 start, Transform target,
                                       float duration, float arcHeight, Action onArrive)
        {
            float elapsed = 0f;
            Vector3 spin = new Vector3(
                UnityEngine.Random.Range(-180f, 180f),
                UnityEngine.Random.Range(-180f, 180f),
                UnityEngine.Random.Range(-180f, 180f));

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Hedef hareket edebilir (vagon kayıyor olabilir), her karede yeniden oku
                if (target == null) break;

                Vector3 end = target.position;

                // Yukarıdan aşağıya bir kavis: düz çizgi cansız durur
                Vector3 position = Vector3.Lerp(start, end, t);
                position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

                transform.position = position;
                transform.Rotate(spin * Time.deltaTime, Space.Self);

                yield return null;
            }

            onArrive?.Invoke();
            Release();
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

            // Uçan parça tıklamayı yutmasın; oyuncu tabloya basıyor
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
            gameObject.SetActive(false);
            transform.SetParent(EnsurePoolRoot(), false);
            s_Pool.Push(this);
        }

        private void OnDestroy()
        {
            if (m_Material != null) Destroy(m_Material);
        }

        #endregion
    }
}
