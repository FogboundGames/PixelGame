using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    /// <summary>
    /// Ekranın altına kamyon slotu şeridini kurar.
    /// Slotlar Screen Space - Camera modunda ayrı bir Canvas'ta UI görseli olarak durur;
    /// kamyonlar ise onların önündeki Z düzleminde gerçek 3D nesne olarak yerleşir.
    /// Mevcut Overlay Canvas'a (MainPlane / FakeShadow) dokunulmaz.
    /// </summary>
    public static class SetupTruckSlots
    {
        private const string k_SlotCanvasName = "SlotCanvas";
        private const string k_SlotRowName = "SlotRow";
        private const string k_TrucksRootName = "Trucks";

        private const int k_SlotCount = 4;
        private const float k_SlotSize = 700f;      // referans çözünürlükte slot kenarı (kare görsel)
        private const float k_SlotGap = 20f;        // slotlar arası boşluk

        /// <summary>Şeridin ekran genişliğinin en fazla ne kadarını kaplayacağı.</summary>
        private const float k_RowScreenWidthFill = 0.96f;

        /// <summary>
        /// Şeridin ekran yüksekliğindeki yeri (0 = en alt, 1 = en üst).
        /// Oran olarak verilir ki her ekran boyutunda aynı yerde dursun.
        /// Tablonun hemen altına denk gelir.
        /// </summary>
        private const float k_RowScreenHeight = 0.33f;

        /// <summary>
        /// Slot görsellerinin X ekseni etrafındaki eğimi (derece).
        /// Canvas perspektif kamerayla render edildiği için eğilen slot gerçek perspektif alır
        /// ve yere serilmiş bir zemin parçası gibi yamuk görünür.
        /// 0 = ekrana dik (düz kare), 90 = tamamen yere yatık.
        /// </summary>
        private const float k_SlotTilt = 45f;

        /// <summary>
        /// Kamyonun slot içindeki duruşu (local Euler, derece).
        /// Slotun eğimi zaten miras alındığı için burada sadece kamyonun park yerindeki
        /// yönü belirtilir. Bu değer elle bulunup doğrulanmıştır.
        /// </summary>
        private static readonly Vector3 k_TruckLocalEuler = new Vector3(-180f, 0f, 0f);

        /// <summary>Kamyonun kendi ekseni etrafındaki ek dönüşü (derece).</summary>
        private const float k_TruckYaw = 0f;

        /// <summary>
        /// Kameranın eğimi (derece). Slotlar ekranın ortasına yakın durduğu için
        /// kameraya eğim vermeye gerek kalmıyor; 0 tabloyu hiç bozmaz.
        /// </summary>
        private const float k_CameraPitch = 0f;

        /// <summary>Canvas'ın kameradan uzaklığı. Kamyonların (Z=0) arkasında kalmalı.</summary>
        private const float k_CanvasPlaneDistance = 11f;

        private static readonly Color[] k_TruckColors =
        {
            TruckPaint.Red,
            TruckPaint.Blue,
            TruckPaint.Yellow,
            TruckPaint.Green,
        };

        [MenuItem("Tools/PixelGame/🚚 Slot Şeridi ve Kamyonları Kur", priority = 20)]
        public static void Setup()
        {
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                EditorUtility.DisplayDialog("Kamera Bulunamadı",
                    "Sahnede bir kamera yok. Slotların dünyaya hizalanabilmesi için kamera gerekli.", "Tamam");
                return;
            }

            GameObject truckPrefab = LoadTruckPrefab();
            if (truckPrefab == null)
            {
                EditorUtility.DisplayDialog("Kamyon Prefab'ı Bulunamadı",
                    "Assets/Prefabs/ToyTruck.prefab bulunamadı.", "Tamam");
                return;
            }

            Sprite slotSprite = LoadSlotSprite();
            if (slotSprite == null)
            {
                EditorUtility.DisplayDialog("Slot Görseli Bulunamadı",
                    "Assets/UI/Slot.png bulunamadı veya Sprite olarak import edilmemiş.", "Tamam");
                return;
            }

            Undo.SetCurrentGroupName("Slot Şeridi ve Kamyonları Kur");
            int undoGroup = Undo.GetCurrentGroup();

            ApplyCameraPitch(cam);

            Canvas slotCanvas = EnsureSlotCanvas(cam);
            RectTransform row = EnsureSlotRow(slotCanvas);
            CleanupLegacyTrucksRoot();

            TruckSlotRow rowComponent = row.GetComponent<TruckSlotRow>();
            if (rowComponent == null)
            {
                rowComponent = Undo.AddComponent<TruckSlotRow>(row.gameObject);
            }

            var slots = new List<TruckSlot>();
            float totalWidth = k_SlotCount * k_SlotSize + (k_SlotCount - 1) * k_SlotGap;
            float startX = -totalWidth * 0.5f + k_SlotSize * 0.5f;

            for (int i = 0; i < k_SlotCount; i++)
            {
                float x = startX + i * (k_SlotSize + k_SlotGap);
                TruckSlot slot = BuildSlot(i, row, truckPrefab, slotSprite, x);
                slots.Add(slot);
            }

            rowComponent.Slots.Clear();
            rowComponent.Slots.AddRange(slots);
            rowComponent.AlignAll();

            RegeneratePixelArt();

            EditorUtility.SetDirty(rowComponent);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = row.gameObject;

            Debug.Log($"<color=#00FFAA><b>[PixelGame]</b></color> {k_SlotCount} slot ve kamyon kuruldu. " +
                      "Kamyon açısını, boyutunu ve yüksekliğini TruckSlot bileşeninden ayarlayabilirsin.");
        }

        [MenuItem("Tools/PixelGame/🚚 Slot Şeridini Kaldır", priority = 21)]
        public static void Remove()
        {
            GameObject canvas = GameObject.Find(k_SlotCanvasName);
            GameObject trucks = GameObject.Find(k_TrucksRootName);

            if (canvas == null && trucks == null)
            {
                EditorUtility.DisplayDialog("Bulunamadı", "Sahnede slot şeridi yok.", "Tamam");
                return;
            }

            if (canvas != null) Undo.DestroyObjectImmediate(canvas);
            if (trucks != null) Undo.DestroyObjectImmediate(trucks);

            // Kurulumda verilen kamera eğimini geri al ve tabloyu yeniden üret
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                Transform camTransform = cam.transform;
                Undo.RecordObject(camTransform, "Kamera Eğimini Sıfırla");

                Vector3 euler = camTransform.eulerAngles;
                camTransform.rotation = Quaternion.Euler(0f, euler.y, euler.z);

                RegeneratePixelArt();
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        /// <summary>
        /// Kamerayı kamyonlara doğru hafifçe eğer.
        /// Perspektif kamera ekranın altındaki nesneye tepeden bakar; bu eğim o açıyı azaltır,
        /// böylece kamyonlar dimdik dururken bile doğal bir 3/4 açıdan görünür.
        /// </summary>
        private static void ApplyCameraPitch(Camera cam)
        {
            Transform camTransform = cam.transform;
            Undo.RecordObject(camTransform, "Kamera Eğimi");

            Vector3 euler = camTransform.eulerAngles;
            camTransform.rotation = Quaternion.Euler(k_CameraPitch, euler.y, euler.z);
        }

        /// <summary>
        /// Kamera açısı değiştiği için piksel tablosunu yeniden üretir;
        /// böylece tablo ekrandaki yerini korur.
        /// </summary>
        private static void RegeneratePixelArt()
        {
            PixelArtGenerator generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (generator == null) return;

            generator.GeneratePixelArt();
        }

        private static GameObject LoadTruckPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ToyTruck.prefab");
            if (prefab != null) return prefab;

            string[] guids = AssetDatabase.FindAssets("ToyTruck t:Prefab");
            if (guids.Length == 0) return null;

            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static Sprite LoadSlotSprite()
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Slot.png");
            if (sprite != null) return sprite;

            string[] guids = AssetDatabase.FindAssets("Slot t:Sprite");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("Slot.png")) continue;

                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) return sprite;
            }

            return null;
        }

        /// <summary>
        /// Slotlar için Screen Space - Camera modunda ayrı bir Canvas kurar.
        /// Mevcut Overlay Canvas'a dokunulmaz; böylece MainPlane hizalaması ve FakeShadow bozulmaz.
        /// </summary>
        private static Canvas EnsureSlotCanvas(Camera cam)
        {
            GameObject canvasObj = GameObject.Find(k_SlotCanvasName);
            if (canvasObj == null)
            {
                canvasObj = new GameObject(k_SlotCanvasName);
                Undo.RegisterCreatedObjectUndo(canvasObj, "Slot Canvas Oluştur");
            }

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null) canvas = Undo.AddComponent<Canvas>(canvasObj);

            Undo.RecordObject(canvas, "Slot Canvas Ayarla");
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = k_CanvasPlaneDistance;
            canvas.sortingOrder = -10; // piksel tablosunun Canvas'ının altında kalsın

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvasObj);

            Undo.RecordObject(scaler, "Slot Canvas Scaler");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Genişliğe göre eşle: dikey (portrait) ekranda şeridin taşmasını engeller.
            // Sahnedeki mevcut Canvas da aynı ayarı kullanıyor.
            scaler.matchWidthOrHeight = 0f;

            // Slot görselleri tıklamayı yutmasın; küplere tıklama serbest kalsın
            GraphicRaycaster raycaster = canvasObj.GetComponent<GraphicRaycaster>();
            if (raycaster != null) Undo.DestroyObjectImmediate(raycaster);

            return canvas;
        }

        private static RectTransform EnsureSlotRow(Canvas canvas)
        {
            Transform existing = canvas.transform.Find(k_SlotRowName);
            GameObject rowObj;

            if (existing != null)
            {
                rowObj = existing.gameObject;

                // Eski slotları temizle, baştan kur
                for (int i = rowObj.transform.childCount - 1; i >= 0; i--)
                {
                    Undo.DestroyObjectImmediate(rowObj.transform.GetChild(i).gameObject);
                }
            }
            else
            {
                rowObj = new GameObject(k_SlotRowName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(rowObj, "Slot Row Oluştur");
                rowObj.transform.SetParent(canvas.transform, false);
            }

            // Yüksekliği orana göre ankrajla: her ekran boyutunda şerit aynı yerde dursun
            RectTransform rect = rowObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, k_RowScreenHeight);
            rect.anchorMax = new Vector2(0.5f, k_RowScreenHeight);
            rect.pivot = new Vector2(0.5f, 0.5f);
            float totalWidth = k_SlotCount * k_SlotSize + (k_SlotCount - 1) * k_SlotGap;
            rect.sizeDelta = new Vector2(totalWidth, k_SlotSize);
            rect.anchoredPosition = Vector2.zero;

            // Şerit referans genişliği aşıyorsa ekrana sığdır.
            // Canvas match=0 (genişliğe göre) olduğu için referans genişlik ekran genişliğine denktir.
            float available = canvas.GetComponent<CanvasScaler>().referenceResolution.x * k_RowScreenWidthFill;
            float fit = totalWidth > available ? available / totalWidth : 1f;
            // Z de ölçeklenmeli: kamyonlar slotların çocuğu olduğu için
            // non-uniform ölçek 3B modeli derinlikte ezer
            rect.localScale = new Vector3(fit, fit, fit);

            return rect;
        }

        /// <summary>
        /// Kamyonlar artık slotların çocuğu; eski kurulumlardan kalan "Trucks" kökünü temizler.
        /// </summary>
        private static void CleanupLegacyTrucksRoot()
        {
            GameObject root = GameObject.Find(k_TrucksRootName);
            if (root != null) Undo.DestroyObjectImmediate(root);
        }

        private static TruckSlot BuildSlot(int index, RectTransform row,
                                           GameObject truckPrefab, Sprite slotSprite, float x)
        {
            // 1. Slot UI görseli
            GameObject slotObj = new GameObject($"Slot_{index + 1}", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(slotObj, "Slot Oluştur");
            slotObj.transform.SetParent(row, false);

            RectTransform slotRect = slotObj.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = new Vector2(k_SlotSize, k_SlotSize);
            slotRect.anchoredPosition = new Vector2(x, 0f);
            // Yere yatır: perspektif kamera bunu yamuk gösterir, zemin parçası hissi verir
            slotRect.localRotation = Quaternion.Euler(k_SlotTilt, 0f, 0f);

            Image image = Undo.AddComponent<Image>(slotObj);
            image.sprite = slotSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            // 2. Kamyon slotun çocuğu olur: eğimi, konumu ve ölçeği slottan miras alır,
            //    böylece park yerinin düzlemine kendiliğinden oturur
            GameObject truck = (GameObject)PrefabUtility.InstantiatePrefab(truckPrefab, slotObj.transform);
            truck.name = $"Truck_{index + 1}";
            Undo.RegisterCreatedObjectUndo(truck, "Kamyon Oluştur");

            // 3. Slot bileşenini bağla
            TruckSlot slot = Undo.AddComponent<TruckSlot>(slotObj);

            SerializedObject so = new SerializedObject(slot);
            so.FindProperty("m_SlotRect").objectReferenceValue = slotRect;
            so.FindProperty("m_Truck").objectReferenceValue = truck.transform;
            // Kamyonun slot içindeki doğrulanmış duruşu; yaw bunun üzerine uygulanacak
            so.FindProperty("m_BaseRotation").quaternionValue = Quaternion.Euler(k_TruckLocalEuler);
            so.FindProperty("m_TruckColor").colorValue = k_TruckColors[index % k_TruckColors.Length];
            so.FindProperty("m_TruckYaw").floatValue = k_TruckYaw;
            so.ApplyModifiedPropertiesWithoutUndo();

            slot.ApplyTruckColor();
            slot.AlignTruck();

            return slot;
        }
    }
}
