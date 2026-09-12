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
        public Vector3 truckEuler = new Vector3(-180f, 0f, 0f);

        [Tooltip("Park yeri görseli çizilsin mi? Havuzda kapalıdır: yalnızca kamyonlar görünür.")]
        public bool showSprite = true;

        [Tooltip("Bu şeritteki yerler tıklanabilir mi? Havuzda açıktır.")]
        public bool interactive;

        [Tooltip("Şeridin ekran genişliğinin en fazla ne kadarını kaplayacağı")]
        public float rowWidthFill = 0.96f;
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

            ClearChildren(row);

            float totalWidth = columns * style.cellSize + (columns - 1) * style.gap;
            float totalHeight = rows * style.cellSize + (rows - 1) * style.gapY;

            row.sizeDelta = new Vector2(totalWidth, totalHeight);

            // Şerit referans genişliği aşıyorsa ekrana sığdır.
            // Z de ölçeklenmeli: kamyonlar bu şeridin altında yaşıyor ve
            // non-uniform ölçek 3B modeli derinlikte ezer.
            float available = ReferenceWidth * style.rowWidthFill;
            float fit = totalWidth > available ? available / totalWidth : 1f;
            row.localScale = new Vector3(fit, fit, fit);

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
                    }

                    image.raycastTarget = style.interactive;

                    TruckSlot slot = obj.AddComponent<TruckSlot>();
                    slot.Configure(rect, truckRotation);

                    if (style.interactive)
                    {
                        obj.AddComponent<TruckPoolPlace>();
                    }

                    result.Add(slot);
                    index++;
                }
            }

            return result;
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
