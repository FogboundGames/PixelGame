using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    /// <summary>
    /// Kamyon döngüsünün sahne kurulumunu yapar.
    ///
    /// Ekranda iki şerit vardır:
    ///   • Slotlar  — tablonun hemen altında, boş başlar; kamyonlar burada doldurulur.
    ///   • Havuz    — en altta, sıradaki kamyonlar burada bekler ve buradan seçilir.
    ///
    /// İkisi de Screen Space - Camera modundaki ayrı bir Canvas'ta UI görselidir;
    /// kamyonlar bu görsellerin çocuğu olan gerçek 3D nesnelerdir, böylece
    /// park yerinin eğik düzlemine kendiliğinden otururlar.
    /// Mevcut Overlay Canvas'a (MainPlane / FakeShadow) dokunulmaz.
    /// </summary>
    public static class SetupTruckSlots
    {
        private const string k_SlotCanvasName = "SlotCanvas";
        private const string k_SlotRowName = "SlotRow";
        private const string k_PoolRowName = "TruckPool";
        private const string k_DispatcherName = "[TruckDispatcher]";
        private const string k_TrucksRootName = "Trucks";

        // --- Slot şeridi (doldurma alanı) — yolun üzerindeki boş park yerleri ---
        private const float k_SlotSize = 400f;
        private const float k_SlotGap = 16f;
        private const float k_SlotGapY = 0f;
        private const float k_SlotRowScreenHeight = 0.33f;

        // --- Havuz (bekleme alanı) — kamyonlar dikey kuyruklar halinde bekler ---
        //
        // Hücre boyutu, aralığı ve sütun sayısı slot şeridiyle BİREBİR aynıdır.
        // Sebebi: şeritler ekrana ayrı ayrı sığdırılıyor ve farklı ölçüler farklı
        // sığdırma oranı doğuruyor; o zaman havuzdaki kamyonlar slottakinden büyük
        // görünüyor ve aynı düzlemde durmuyorlarmış gibi algılanıyor.
        // Aynı ölçüler sayesinde iki şerit aynı ölçeği alır, kamyonlar aynı boyutta
        // ve aynı sütun hizasında durur.


        /// <summary>Havuzda sıralar arası dikey boşluk.</summary>
        private const float k_PoolGapY = 0f;

        /// <summary>
        /// Havuzda sıralar arası derinlik farkı.
        /// Alttaki sıra kameraya daha yakın durur; kamyonlar böylece iç içe geçmiş gibi
        /// değil, arka arkaya dizilmiş bir kuyruk gibi görünür.
        /// </summary>
        private const float k_PoolStepZ = 120f;

        private const float k_PoolRowScreenHeight = 0.15f;

        /// <summary>Şeridin ekran genişliğinin en fazla ne kadarını kaplayacağı.</summary>
        private const float k_RowScreenWidthFill = 0.96f;

        /// <summary>
        /// Park yeri görsellerinin X ekseni etrafındaki eğimi (derece).
        /// Canvas perspektif kamerayla render edildiği için eğilen görsel gerçek perspektif alır
        /// ve yere serilmiş bir zemin parçası gibi yamuk görünür.
        /// </summary>
        private const float k_SlotTilt = 45f;

        /// <summary>
        /// Kamyonun park yeri içindeki duruşu (local Euler, derece).
        /// Slotun eğimi zaten miras alındığı için burada sadece kamyonun yönü belirtilir.
        /// Bu değer elle bulunup doğrulanmıştır.
        /// </summary>
        private static readonly Vector3 k_TruckLocalEuler = new Vector3(-180f, 0f, 0f);

        /// <summary>Canvas'ın kameradan uzaklığı.</summary>
        private const float k_CanvasPlaneDistance = 11f;

        [MenuItem("Tools/PixelGame/🚚 Kamyon Döngüsünü Kur (Slot + Havuz)", priority = 20)]
        public static void Setup()
        {
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                EditorUtility.DisplayDialog("Kamera Bulunamadı",
                    "Sahnede bir kamera yok. Park yerlerinin hizalanabilmesi için kamera gerekli.", "Tamam");
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

            Undo.SetCurrentGroupName("Kamyon Döngüsünü Kur");
            int undoGroup = Undo.GetCurrentGroup();

            CleanupLegacyTrucksRoot();

            Canvas canvas = EnsureSlotCanvas(cam);

            // 1. Doldurma slotları — boş başlar, kamyonlar havuzdan gelir
            RectTransform slotRow = EnsureRow(canvas, k_SlotRowName, k_SlotRowScreenHeight);
            TruckSlotRow rowComponent = EnsureComponent<TruckSlotRow>(slotRow.gameObject);

            ApplyStyle(rowComponent.Style, slotSprite, k_SlotGapY, 0f,
                       showSprite: true, interactive: false);

            // 2. Havuz — sıradaki kamyonlar burada bekler, tıklanınca slota gider
            RectTransform poolRow = EnsureRow(canvas, k_PoolRowName, k_PoolRowScreenHeight);
            TruckPool poolComponent = EnsureComponent<TruckPool>(poolRow.gameObject);

            // Havuzda park yeri görseli yok: sadece kamyonlar görünür
            ApplyStyle(poolComponent.Style, slotSprite, k_PoolGapY, k_PoolStepZ,
                       showSprite: false, interactive: true);

            // Sayılar bölüm verisinden gelir; burada yalnızca bir önizleme kurulur
            PixelLevelData level = GetActiveLevel();
            int slotCount = level != null ? level.SlotCount : 5;
            int poolColumns = level != null ? level.PoolColumns : 5;
            int poolRows = level != null ? level.PoolRows : 2;

            rowComponent.RebuildPlaces(slotCount, 1);
            poolComponent.RebuildPlaces(poolColumns, poolRows);

            // 3. Yönetici
            SetupDispatcher(rowComponent, poolComponent, truckPrefab);

            EditorUtility.SetDirty(rowComponent);
            EditorUtility.SetDirty(poolComponent);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = slotRow.gameObject;

            Debug.Log($"<color=#00FFAA><b>[PixelGame]</b></color> Kamyon döngüsü kuruldu: " +
                      $"{slotCount} slot, {poolColumns}x{poolRows} havuz. " +
                      "Sayılar bölüm verisinden gelir (Level Designer > Kamyon Düzeni).");
        }

        [MenuItem("Tools/PixelGame/🚚 Kamyon Döngüsünü Kaldır", priority = 21)]
        public static void Remove()
        {
            GameObject canvas = GameObject.Find(k_SlotCanvasName);
            GameObject dispatcher = GameObject.Find(k_DispatcherName);
            GameObject trucks = GameObject.Find(k_TrucksRootName);

            if (canvas == null && dispatcher == null && trucks == null)
            {
                EditorUtility.DisplayDialog("Bulunamadı", "Sahnede kamyon döngüsü yok.", "Tamam");
                return;
            }

            if (canvas != null) Undo.DestroyObjectImmediate(canvas);
            if (dispatcher != null) Undo.DestroyObjectImmediate(dispatcher);
            if (trucks != null) Undo.DestroyObjectImmediate(trucks);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        #region 🔧 Kurulum Parçaları

        private static void SetupDispatcher(TruckSlotRow slots, TruckPool pool, GameObject truckPrefab)
        {
            GameObject obj = GameObject.Find(k_DispatcherName);
            if (obj == null)
            {
                obj = new GameObject(k_DispatcherName);
                Undo.RegisterCreatedObjectUndo(obj, "Dispatcher Oluştur");
            }

            TruckDispatcher dispatcher = EnsureComponent<TruckDispatcher>(obj);

            SerializedObject so = new SerializedObject(dispatcher);
            so.FindProperty("m_Slots").objectReferenceValue = slots;
            so.FindProperty("m_Pool").objectReferenceValue = pool;
            so.FindProperty("m_TruckPrefab").objectReferenceValue = truckPrefab;
            so.FindProperty("m_Generator").objectReferenceValue =
                Object.FindFirstObjectByType<PixelArtGenerator>();
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(dispatcher);
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null) component = Undo.AddComponent<T>(target);
            return component;
        }

        /// <summary>
        /// Şerit kökünü kurar: ekran yüksekliğine orantısal ankrajlanır ve
        /// gerekirse ekran genişliğine sığacak şekilde ölçeklenir.
        /// </summary>
        private static RectTransform EnsureRow(Canvas canvas, string name, float screenHeight)
        {
            Transform existing = canvas.transform.Find(name);
            GameObject rowObj;

            if (existing != null)
            {
                rowObj = existing.gameObject;
            }
            else
            {
                rowObj = new GameObject(name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(rowObj, "Şerit Oluştur");
                rowObj.transform.SetParent(canvas.transform, false);
            }

            // Yüksekliği orana göre ankrajla: her ekran boyutunda şerit aynı yerde dursun.
            // Genişlik, yükseklik ve sığdırma ölçeği park yerleri kurulurken hesaplanır.
            RectTransform rect = rowObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, screenHeight);
            rect.anchorMax = new Vector2(0.5f, screenHeight);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            return rect;
        }

        /// <summary>Şeridin görsel ayarlarını kurulum sabitlerinden doldurur.</summary>
        private static void ApplyStyle(TruckPlaceStyle style, Sprite sprite,
                                       float gapY, float stepZ,
                                       bool showSprite, bool interactive)
        {
            style.sprite = sprite;
            style.cellSize = k_SlotSize;
            style.gap = k_SlotGap;
            style.gapY = gapY;
            style.stepZ = stepZ;
            style.tilt = k_SlotTilt;
            style.truckEuler = k_TruckLocalEuler;
            style.showSprite = showSprite;
            style.interactive = interactive;
            style.rowWidthFill = k_RowScreenWidthFill;
        }

        private static PixelLevelData GetActiveLevel()
        {
            PixelArtGenerator generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            return generator != null ? generator.ActiveLevelData : null;
        }


        /// <summary>
        /// Park yerleri için Screen Space - Camera modunda ayrı bir Canvas kurar.
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

            Canvas canvas = EnsureComponent<Canvas>(canvasObj);

            Undo.RecordObject(canvas, "Slot Canvas Ayarla");
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = k_CanvasPlaneDistance;
            canvas.sortingOrder = -10; // piksel tablosunun Canvas'ının altında kalsın

            CanvasScaler scaler = EnsureComponent<CanvasScaler>(canvasObj);

            Undo.RecordObject(scaler, "Slot Canvas Scaler");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Genişliğe göre eşle: dikey (portrait) ekranda şeridin taşmasını engeller.
            // Sahnedeki mevcut Canvas da aynı ayarı kullanıyor.
            scaler.matchWidthOrHeight = 0f;

            // Havuzdaki kamyonlara tıklanabilmesi için raycaster gerekli.
            // Doldurma slotlarının görselleri raycastTarget=false olduğu için küp tıklamasını yutmaz.
            EnsureComponent<GraphicRaycaster>(canvasObj);

            EnsureEventSystem();

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

            GameObject obj = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            Undo.RegisterCreatedObjectUndo(obj, "EventSystem Oluştur");
        }

        /// <summary>
        /// Kamyonlar artık park yerlerinin çocuğu; eski kurulumlardan kalan "Trucks" kökünü temizler.
        /// </summary>
        private static void CleanupLegacyTrucksRoot()
        {
            GameObject root = GameObject.Find(k_TrucksRootName);
            if (root != null) Undo.DestroyObjectImmediate(root);
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

        #endregion
    }
}
