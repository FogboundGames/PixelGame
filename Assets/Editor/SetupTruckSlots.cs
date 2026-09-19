using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
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
    [InitializeOnLoad]
    public static class SetupTruckSlots
    {
        private const string SessionKey = "SetupTruckSlots_SlotFakeShadow_v2";

        static SetupTruckSlots()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (SessionState.GetBool(SessionKey, false)) return;

            TruckSlotRow row = Object.FindFirstObjectByType<TruckSlotRow>();
            if (row == null)
            {
                GameObject rowObj = GameObject.Find("SlotRow");
                if (rowObj != null) row = rowObj.GetComponent<TruckSlotRow>();
            }

            if (row != null)
            {
                SessionState.SetBool(SessionKey, true);

                // Slotların altına slot1.png kavislerine uygun sahte gölgeleri (Fake Shadow) kur
                row.Style.enableShadow = true;
                row.Style.shadowSprite = SlotShadowTextureGenerator.GetOrGenerateSlotShadowSprite();
                row.Style.shadowColor = new Color(0.04f, 0.06f, 0.14f, 0.58f);
                row.Style.shadowOffset = new Vector2(0f, -14f);
                row.Style.shadowScale = new Vector2(1.04f, 1.04f);
                row.Style.shadowZ = 4f;

                row.ForceApplyStyleToShadows();

                EditorUtility.SetDirty(row);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                Debug.Log("<color=#00FFB4><b>[PixelGame]</b></color> 🌑 Slot Fake Shadow'ları (SlotShadow_1..5) başarıyla kuruldu ve sahneye kaydedildi!");
            }
            else
            {
                EditorApplication.delayCall += OnEditorReady;
            }
        }

        private const string k_SlotCanvasName = "SlotCanvas";
        private const string k_SlotRowName = "SlotRow";
        private const string k_PoolRowName = "TruckPool";
        private const string k_DispatcherName = "[TruckDispatcher]";
        private const string k_TrucksRootName = "Trucks";

        private const string k_CartModelPath = "Assets/Models/MineCart/MineCart.fbx";
        private const string k_TrackModelPath = "Assets/Models/Track/Track.fbx";
        private const string k_CartPrefabPath = "Assets/Prefabs/MineCart.prefab";
        private const string k_TrackPrefabPath = "Assets/Prefabs/Track.prefab";
        private const string k_PortalModelPath = "Assets/Models/MinePortal/MinePortal.fbx";
        private const string k_PortalPrefabPath = "Assets/Prefabs/MinePortal.prefab";
        private const string k_CartControllerPath = "Assets/Prefabs/MineCart_Roll.controller";
        private const string k_CartLoopClipPath = "Assets/Prefabs/MineCart_Roll_Loop.anim";

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
        /// <summary>
        /// Vagonun park yeri içindeki duruşu (local Euler, derece).
        /// Ray ile aynı eksen dönüşümünden geldiği için ray ile aynı değeri kullanır;
        /// eski kamyon değeri (-180, 0, 0) vagonu baş aşağı çeviriyordu.
        /// </summary>
        private static readonly Vector3 k_TruckLocalEuler = new Vector3(0f, -90f, -270f);

        /// <summary>
        /// Ray parçasının park yeri içindeki duruşu (local Euler, derece).
        /// Vagonun duruşundan bağımsızdır; bu değer elle bulunup doğrulanmıştır.
        /// </summary>
        private static readonly Vector3 k_TrackLocalEuler = new Vector3(0f, -90f, -270f);

        /// <summary>Canvas'ın kameradan uzaklığı.</summary>
        private const float k_CanvasPlaneDistance = 11f;

        [MenuItem("Tools/PixelGame/🛤️ Vagon Döngüsünü Kur (Ray + Havuz)", priority = 20)]
        public static void Setup()
        {
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                EditorUtility.DisplayDialog("Kamera Bulunamadı",
                    "Sahnede bir kamera yok. Park yerlerinin hizalanabilmesi için kamera gerekli.", "Tamam");
                return;
            }

            GameObject cartPrefab = LoadCartPrefab();
            if (cartPrefab == null)
            {
                EditorUtility.DisplayDialog("Vagon Prefab'ı Bulunamadı",
                    "Assets/Models/MineCart/MineCart.fbx bulunamadı, prefab üretilemedi.", "Tamam");
                return;
            }

            GameObject trackPrefab = LoadTrackPrefab();
            if (trackPrefab == null)
            {
                EditorUtility.DisplayDialog("Ray Prefab'ı Bulunamadı",
                    "Assets/Models/Track/Track.fbx bulunamadı, prefab üretilemedi.", "Tamam");
                return;
            }

            // Portal zorunlu değil: bulunamazsa ray portalsız kurulur
            GameObject portalPrefab = LoadPortalPrefab();

            Undo.SetCurrentGroupName("Vagon Döngüsünü Kur");
            int undoGroup = Undo.GetCurrentGroup();

            CleanupLegacyTrucksRoot();

            Canvas canvas = EnsureSlotCanvas(cam);

            // 1. Doldurma slotları — boş başlar, kamyonlar havuzdan gelir
            RectTransform slotRow = EnsureRow(canvas, k_SlotRowName, k_SlotRowScreenHeight);
            TruckSlotRow rowComponent = EnsureComponent<TruckSlotRow>(slotRow.gameObject);

            // Slot şeridi: park yeri görseli yok, altında gerçek ray modeli var
            ApplyStyle(rowComponent.Style, k_SlotGapY, 0f, trackPrefab, portalPrefab,
                       showSprite: false, interactive: false);

            // 2. Havuz — sıradaki kamyonlar burada bekler, tıklanınca slota gider
            RectTransform poolRow = EnsureRow(canvas, k_PoolRowName, k_PoolRowScreenHeight);
            TruckPool poolComponent = EnsureComponent<TruckPool>(poolRow.gameObject);

            // Havuzda ne park yeri görseli ne ray var: sadece bekleyen vagonlar görünür
            // Havuzda ray ve portal yok: sadece bekleyen vagonlar görünür
            ApplyStyle(poolComponent.Style, k_PoolGapY, k_PoolStepZ, null, null,
                       showSprite: false, interactive: true);

            // Sayılar bölüm verisinden gelir; burada yalnızca bir önizleme kurulur
            PixelLevelData level = GetActiveLevel();
            int slotCount = level != null ? level.SlotCount : 5;
            int poolColumns = level != null ? level.PoolColumns : 5;
            int poolRows = level != null ? level.PoolRows : 2;

            rowComponent.RebuildPlaces(slotCount, 1);
            poolComponent.RebuildPlaces(poolColumns, poolRows);

            // 3. Yönetici
            TruckDispatcher dispatcher = SetupDispatcher(rowComponent, poolComponent, cartPrefab, trackPrefab, slotCount);
            if (dispatcher != null)
            {
                dispatcher.SetupPerimeterLoop();
                dispatcher.GeneratePerimeterRails();
            }

            EditorUtility.SetDirty(rowComponent);
            EditorUtility.SetDirty(poolComponent);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = slotRow.gameObject;

            Debug.Log($"<color=#00FFAA><b>[PixelGame]</b></color> Vagon döngüsü kuruldu: " +
                      $"{slotCount} ray yeri, {poolColumns}x{poolRows} havuz. " +
                      "Mavi çerçeve etrafındaki raylar başarıyla dizildi!");
        }

        [MenuItem("Tools/PixelGame/🛤️ Çevresel Rayları Diz (Mavi Çerçeve)", priority = 19)]
        public static void GeneratePerimeterRailsMenu()
        {
            TruckDispatcher dispatcher = Object.FindFirstObjectByType<TruckDispatcher>();
            if (dispatcher == null)
            {
                Setup();
                dispatcher = Object.FindFirstObjectByType<TruckDispatcher>();
            }

            if (dispatcher != null)
            {
                dispatcher.SetupPerimeterLoop();
                dispatcher.GeneratePerimeterRails();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                Debug.Log("<color=#00FFAA><b>[PixelGame]</b></color> Mavi çerçevenin etrafına raylar başarıyla dizildi ve sahne kaydedildi!");
            }
        }

        [MenuItem("Tools/PixelGame/🛤️ Vagon Döngüsünü Kaldır", priority = 21)]
        public static void Remove()
        {
            GameObject canvas = GameObject.Find(k_SlotCanvasName);
            GameObject dispatcher = GameObject.Find(k_DispatcherName);
            GameObject trucks = GameObject.Find(k_TrucksRootName);
            GameObject perimRoot = GameObject.Find("[PerimeterWagonsRoot]");

            if (canvas == null && dispatcher == null && trucks == null && perimRoot == null)
            {
                EditorUtility.DisplayDialog("Bulunamadı", "Sahnede kamyon döngüsü yok.", "Tamam");
                return;
            }

            if (canvas != null) Undo.DestroyObjectImmediate(canvas);
            if (dispatcher != null) Undo.DestroyObjectImmediate(dispatcher);
            if (trucks != null) Undo.DestroyObjectImmediate(trucks);
            if (perimRoot != null) Undo.DestroyObjectImmediate(perimRoot);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        [MenuItem("Tools/PixelGame/🌑 Slot Sahte Gölgelerini (Fake Shadow) Kur / Güncelle", priority = 22)]
        public static void SelectOrUpdateSlotShadow()
        {
            SelectOrUpdateSlotShadow(showDialog: true);
        }

        public static void SelectOrUpdateSlotShadow(bool showDialog)
        {
            TruckSlotRow row = Object.FindFirstObjectByType<TruckSlotRow>();
            if (row == null)
            {
                Setup();
                return;
            }

            row.Style.enableShadow = true;
            row.Style.shadowSprite = SlotShadowTextureGenerator.GetOrGenerateSlotShadowSprite();
            row.Style.shadowColor = new Color(0.04f, 0.06f, 0.14f, 0.58f);
            row.Style.shadowOffset = new Vector2(0f, -14f);
            row.Style.shadowScale = new Vector2(1.04f, 1.04f);
            row.Style.shadowZ = 4f;

            row.Style.enableRowGroundShadow = true;
            row.Style.enablePortalShadow = true;

            row.Style.rowGroundShadowSprite = SlotShadowTextureGenerator.GetOrGenerateRowGroundShadowSprite();
            row.Style.portalShadowSprite = SlotShadowTextureGenerator.GetOrGeneratePortalShadowSprite();

            row.ForceApplyStyleToShadows();
            EditorUtility.SetDirty(row);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            Selection.activeGameObject = row.gameObject;

            SceneView sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                sv.FrameSelected();
            }

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Slot Fake Shadow Hazır!",
                    "Slotların (Slot_1..5) ve ray şeridinin sahte gölgeleri (Fake Shadow) başarıyla kuruldu ve güncellendi!\n\n" +
                    "Sağdaki Inspector panelinden veya sahne üzerinde Gizmo ile gölgeleri serbestçe taşıyabilir, boyutlandırabilir ve rengini ayarlayabilirsiniz.", "Tamam");
            }
        }

        #region 🔧 Kurulum Parçaları

        private static TruckDispatcher SetupDispatcher(TruckSlotRow slots, TruckPool pool, GameObject cartPrefab, GameObject trackPrefab, int slotCount)
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
            so.FindProperty("m_TruckPrefab").objectReferenceValue = cartPrefab;
            so.FindProperty("m_Generator").objectReferenceValue =
                Object.FindFirstObjectByType<PixelArtGenerator>();
            
            SerializedProperty perimProp = so.FindProperty("m_PerimeterTrain");
            if (perimProp != null) perimProp.boolValue = true;

            SerializedProperty maxWagonsProp = so.FindProperty("m_MaxTrackWagons");
            if (maxWagonsProp != null) maxWagonsProp.intValue = slotCount;

            SerializedProperty trackProp = so.FindProperty("m_TrackPrefab");
            if (trackProp != null && trackPrefab != null) trackProp.objectReferenceValue = trackPrefab;

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(dispatcher);
            return dispatcher;
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
        private static void ApplyStyle(TruckPlaceStyle style,
                                       float gapY, float stepZ, GameObject groundPrefab,
                                       GameObject portalPrefab,
                                       bool showSprite, bool interactive)
        {
            style.groundPrefab = groundPrefab;
            style.groundEuler = k_TrackLocalEuler;
            style.portalPrefab = portalPrefab;
            style.portalEuler = k_TrackLocalEuler;
            style.cellSize = k_SlotSize;
            style.gap = k_SlotGap;
            style.gapY = gapY;
            style.stepZ = stepZ;
            style.tilt = k_SlotTilt;
            style.truckEuler = k_TruckLocalEuler;
            style.showSprite = showSprite;
            style.interactive = interactive;
            style.rowWidthFill = k_RowScreenWidthFill;

            style.enableShadow = false;
            style.enableRowGroundShadow = false;
            style.enablePortalShadow = false;
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

        /// <summary>Vagon prefabını bulur; yoksa modelden üretir.</summary>
        private static GameObject LoadCartPrefab()
        {
            return EnsurePrefab(k_CartModelPath, k_CartPrefabPath, "MineCart", withMover: true);
        }

        /// <summary>Ray prefabını bulur; yoksa modelden üretir.</summary>
        private static GameObject LoadTrackPrefab()
        {
            return EnsurePrefab(k_TrackModelPath, k_TrackPrefabPath, "Track", withMover: false);
        }

        /// <summary>
        /// Vagonun yuvarlanma animasyonu için Animator Controller döndürür; yoksa üretir.
        ///
        /// FBX içindeki klip salt okunurdur ve döngüsü kapalı gelir; bu yüzden bir kopyası
        /// alınıp döngü açılarak asset olarak kaydedilir. Aksi halde tekerlekler bir kez
        /// dönüp durur.
        /// </summary>
        private static AnimatorController EnsureCartController()
        {
            AnimatorController existing =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(k_CartControllerPath);
            if (existing != null) return existing;

            AnimationClip source = FindRollClip();
            if (source == null)
            {
                Debug.LogWarning("[PixelGame] MineCart.fbx içinde yuvarlanma animasyonu bulunamadı; " +
                                 "vagon animasyonsuz hareket edecek.");
                return null;
            }

            AnimationClip looped = AssetDatabase.LoadAssetAtPath<AnimationClip>(k_CartLoopClipPath);

            if (looped == null)
            {
                looped = Object.Instantiate(source);
                looped.name = "MineCart_Roll_Loop";

                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(looped);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(looped, settings);

                AssetDatabase.CreateAsset(looped, k_CartLoopClipPath);
            }

            return AnimatorController.CreateAnimatorControllerAtPathWithClip(k_CartControllerPath, looped);
        }

        /// <summary>
        /// Önceden üretilmiş vagon prefabına eksik olan Animator'ı ekler.
        /// Prefab eski sürümden kalmışsa yuvarlanma animasyonu olmadan geliyordu.
        /// </summary>
        private static void UpgradeCartPrefab(string prefabPath)
        {
            AnimatorController controller = EnsureCartController();
            if (controller == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                bool changed = false;

                Animator animator = root.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = root.AddComponent<Animator>();
                    changed = true;
                }

                if (animator.runtimeAnimatorController != controller)
                {
                    animator.runtimeAnimatorController = controller;
                    changed = true;
                }

                // Kök hareketi kapalı: vagonu ilerleten kalkış kodu, animasyon değil
                if (animator.applyRootMotion)
                {
                    animator.applyRootMotion = false;
                    changed = true;
                }

                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // Tekerlek dönüşü artık animasyondan geliyor; mover ile çakışmasın
                MineCartMover mover = root.GetComponent<MineCartMover>();
                if (mover != null)
                {
                    SerializedObject so = new SerializedObject(mover);
                    so.FindProperty("m_MoveOnStart").boolValue = false;
                    so.FindProperty("m_SpinWheels").boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                if (changed || mover != null)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.Log($"<color=#00FFAA><b>[PixelGame]</b></color> Vagon prefabı güncellendi: " +
                              "yuvarlanma animasyonu bağlandı.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static AnimationClip FindRollClip()
        {
            AnimationClip fallback = null;

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(k_CartModelPath))
            {
                if (!(asset is AnimationClip clip)) continue;
                if (clip.name.StartsWith("__preview__")) continue;

                if (clip.name.IndexOf("Roll", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return clip;
                }

                if (fallback == null) fallback = clip;
            }

            return fallback;
        }

        /// <summary>Maden portalı prefabını bulur; yoksa modelden üretir.</summary>
        private static GameObject LoadPortalPrefab()
        {
            return EnsurePrefab(k_PortalModelPath, k_PortalPrefabPath, "MinePortal", withMover: false);
        }

        /// <summary>
        /// Prefabı döndürür; yoksa FBX modelinden oluşturup kaydeder.
        ///
        /// Üretimi burada yapıyoruz ki kurulum başka bir editör script'inin
        /// metot adlarına ve erişim düzeyine bağımlı kalmasın.
        /// </summary>
        private static GameObject EnsurePrefab(string modelPath, string prefabPath,
                                               string objectName, bool withMover)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                // Prefab önceden üretilmiş olabilir; eksik bileşenleri tamamla
                if (withMover) UpgradeCartPrefab(prefabPath);
                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) return null;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);

            try
            {
                instance.name = objectName;

                if (instance.GetComponent<TruckPaint>() == null)
                {
                    instance.AddComponent<TruckPaint>();
                }

                if (withMover && instance.GetComponent<MineCartMover>() == null)
                {
                    MineCartMover mover = instance.AddComponent<MineCartMover>();

                    // Vagon park ederken sabit durmalı; hareketi kalkış animasyonu yönetir.
                    // Tekerlek döndürmeyi de kapatıyoruz: dönüşü artık FBX'teki yuvarlanma
                    // animasyonu yapıyor, ikisi birlikte çalışırsa birbirini ezer.
                    SerializedObject moverSo = new SerializedObject(mover);
                    moverSo.FindProperty("m_MoveOnStart").boolValue = false;
                    moverSo.FindProperty("m_SpinWheels").boolValue = false;
                    moverSo.ApplyModifiedPropertiesWithoutUndo();

                    AnimatorController controller = EnsureCartController();
                    if (controller != null)
                    {
                        Animator animator = instance.GetComponent<Animator>();
                        if (animator == null) animator = instance.AddComponent<Animator>();

                        animator.runtimeAnimatorController = controller;
                        // Kök hareketi kapalı: vagonu ilerleten kalkış kodu, animasyon değil
                        animator.applyRootMotion = false;
                        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    }
                }

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(prefabPath));
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

                Debug.Log($"<color=#00FFAA><b>[PixelGame]</b></color> Prefab üretildi: {prefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }


        #endregion
    }
}
