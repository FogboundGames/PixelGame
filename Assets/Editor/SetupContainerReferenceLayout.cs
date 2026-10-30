using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupContainerReferenceLayout
    {
        private const string AutoRunKey = "SetupContainerUiLayout_Ran_v5";
        private const string ScenePath = "Assets/Scenes/Gemi.unity";
        private const string UiPrefabPath = "Assets/Prefabs/Container_UI.prefab";
        private const string CleanBgPath = "Assets/Renkli Liman İskelesi ve Deniz.png";
        private const string FontPath = "Assets/Fonts/LilitaOne-Regular SDF.asset";

        static SetupContainerReferenceLayout()
        {
            EditorApplication.update -= CheckFlags;
            EditorApplication.update += CheckFlags;
        }

        private static void CheckFlags()
        {
            if (File.Exists("scratch/run_linked_preview.flag"))
            {
                try { File.Delete("scratch/run_linked_preview.flag"); } catch {}
                TestLinkedContainersPreview();
            }
        }

        // [MenuItem("PixelGame/📦 Apply Complete Cargo Container & Beach Layout (UI Containers)")]
        public static void ApplyCompleteReferenceLayout()
        {
            Debug.Log("<color=#00FFAA><b>[UI Container Setup]</b></color> Starting 100% 2D UI Container Transformation...");

            // 1. Ensure scene is open
            if (!Application.isPlaying && EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            // 2. Setup Texture Importers for all 8 container sprites and UI textures
            ShipController.ClearMaterialCache();
            ShipController.ClearContainerSpriteCache();
            SetupAllTextureImporters();

            // 3. Update Background (Remove Water, Apply Clean Beach Background)
            UpdateBackground();

            // 4. Create or Update Container_UI Prefab
            GameObject uiPrefab = CreateOrUpdateUiContainerPrefab();

            // 5. Clean up old 3D meshes and 3D boats from world space
            CleanupOld3DWorldElements();

            // 6. Setup UI Slots and UI Containers under HUD_Canvas
            SetupUiContainersInScene(uiPrefab);

            // 7. Save scene and assets
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
            }

            Debug.Log("<color=#00FFAA><b>[UI Container Setup]</b></color> Completed! Capturing screenshot to scratch/gameplay_view_9_16.png...");
            CaptureGameViewScreenshot.Capture();
        }

        public static void SetupAllTextureImporters()
        {
            string[] texturePaths = new string[]
            {
                "Assets/UI/Badge_Pill.png",
                "Assets/Textures/Marina/slot_rounded_square.png",
                "Assets/Textures/Containers/Container_Red.png",
                "Assets/Textures/Containers/Container_Blue.png",
                "Assets/Textures/Containers/Container_Yellow.png",
                "Assets/Textures/Containers/Container_Green.png",
                "Assets/Textures/Containers/Container_Purple.png",
                "Assets/Textures/Containers/Container_Orange.png",
                "Assets/Textures/Containers/Container_Cyan.png",
                "Assets/Textures/Containers/Container_Pink.png",
                "Assets/Textures/Containers/Container_Socket_Recess.png",
                "Assets/Textures/Containers/Bridge_Cap_Left.png",
                "Assets/Textures/Containers/Bridge_Cap_Right.png",
                "Assets/Textures/Containers/Bridge_Cylinder_Shine.png",
                "Assets/Resources/Containers/Container_Red.png",
                "Assets/Resources/Containers/Container_Blue.png",
                "Assets/Resources/Containers/Container_Yellow.png",
                "Assets/Resources/Containers/Container_Green.png",
                "Assets/Resources/Containers/Container_Purple.png",
                "Assets/Resources/Containers/Container_Orange.png",
                "Assets/Resources/Containers/Container_Cyan.png",
                "Assets/Resources/Containers/Container_Pink.png",
                "Assets/Resources/Containers/Container_Socket_Recess.png",
                "Assets/Resources/Containers/Bridge_Cap_Left.png",
                "Assets/Resources/Containers/Bridge_Cap_Right.png",
                "Assets/Resources/Containers/Bridge_Cylinder_Shine.png",
                "Assets/Resources/Containers/Container_Base.png"
            };

            foreach (var p in texturePaths)
            {
                if (!File.Exists(p)) continue;
                TextureImporter ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.alphaIsTransparency = true;
                    ti.mipmapEnabled = false;
                    if (p.Contains("Container_Base"))
                    {
                        ti.isReadable = true;
                    }
                    if (p.Contains("Bridge_Cap_Left"))
                    {
                        ti.spriteBorder = new Vector4(32f, 0f, 2f, 0f);
                    }
                    else if (p.Contains("Bridge_Cap_Right"))
                    {
                        ti.spriteBorder = new Vector4(2f, 0f, 32f, 0f);
                    }
                    else if (p.Contains("Bridge_Cylinder_Shine"))
                    {
                        ti.spriteBorder = new Vector4(32f, 0f, 32f, 0f);
                    }
                    ti.SaveAndReimport();
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        private static void UpdateBackground()
        {
            GameObject bgCanvas = GameObject.Find("Background_Canvas");
            if (bgCanvas != null)
            {
                Transform bgImg = bgCanvas.transform.Find("BackgroundImage");
                if (bgImg != null)
                {
                    var waterCtrl = bgImg.GetComponent<MonoBehaviour>();
                    if (waterCtrl != null && waterCtrl.GetType().Name.Contains("Water"))
                    {
                        waterCtrl.enabled = false;
                    }

                    RawImage raw = bgImg.GetComponent<RawImage>();
                    if (raw != null)
                    {
                        Texture2D cleanTex = AssetDatabase.LoadAssetAtPath<Texture2D>(CleanBgPath);
                        if (cleanTex != null)
                        {
                            raw.texture = cleanTex;
                        }
                        raw.color = Color.white;
                        raw.material = null;
                    }
                }
            }

            GameObject woodenBridge = GameObject.Find("[Zone_Wooden_Bridge]");
            if (woodenBridge != null) woodenBridge.SetActive(false);

            GameObject groundShadowCatcher = GameObject.Find("Ground_ShadowCatcher");
            if (groundShadowCatcher != null) groundShadowCatcher.SetActive(false);
        }

        public static GameObject CreateOrUpdateUiContainerPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");

            // Proportions: 160 x 163 (matches new steep top-down container angle)
            GameObject root = new GameObject("Container_UI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, 163f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;

            Image img = root.GetComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Containers/Container_Red.png");
            img.color = Color.white;
            img.raycastTarget = true;

            ShipController sc = root.AddComponent<ShipController>();
            sc.IsUiContainer = true;
            sc.ContainerImage = img;

            BoxCollider col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(1.60f, 1.63f, 0.2f);
            col.center = Vector3.zero;
            col.isTrigger = true;

            // Child 0: Socket_Recess (for linked containers, subtle 3D crater on top lid)
            GameObject socketObj = new GameObject("Socket_Recess", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            socketObj.transform.SetParent(root.transform, false);
            RectTransform socketRt = socketObj.GetComponent<RectTransform>();
            socketRt.anchorMin = new Vector2(0.5f, 0.5f);
            socketRt.anchorMax = new Vector2(0.5f, 0.5f);
            socketRt.pivot = new Vector2(0.5f, 0.5f);
            socketRt.anchoredPosition = new Vector2(0f, 14f);
            socketRt.sizeDelta = new Vector2(64f, 64f);
            socketRt.localScale = Vector3.one;

            Image socketImg = socketObj.GetComponent<Image>();
            socketImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Containers/Container_Socket_Recess.png");
            socketImg.color = Color.white;
            socketImg.raycastTarget = false;
            socketObj.SetActive(false); // only enabled when linked!

            // Child 1: Badge Pill on Roof (centered on top lid tray, transparent background so number is directly on lid)
            GameObject pillObj = new GameObject("Badge_Pill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pillObj.transform.SetParent(root.transform, false);

            RectTransform pillRt = pillObj.GetComponent<RectTransform>();
            pillRt.anchorMin = new Vector2(0.5f, 0.5f);
            pillRt.anchorMax = new Vector2(0.5f, 0.5f);
            pillRt.pivot = new Vector2(0.5f, 0.5f);
            pillRt.anchoredPosition = new Vector2(0f, 14f);
            pillRt.sizeDelta = new Vector2(140f, 110f);
            pillRt.localScale = Vector3.one;

            Image pillImg = pillObj.GetComponent<Image>();
            pillImg.color = Color.clear;
            pillImg.raycastTarget = false;

            // Child 2 inside Badge_Pill: Text for Badge (large bold white with clean black outline)
            GameObject textObj = new GameObject("Badge_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(pillObj.transform, false);

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            textRt.localScale = Vector3.one;

            TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) tmp.font = font;
            tmp.fontSize = 58f;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 36f;
            tmp.fontSizeMax = 64f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.outlineColor = Color.black;
            tmp.outlineWidth = 0.28f;
            tmp.text = "12";
            tmp.raycastTarget = false;

            var scSo = new SerializedObject(sc);
            scSo.FindProperty("m_IsUiContainer").boolValue = true;
            scSo.FindProperty("m_ContainerImage").objectReferenceValue = img;
            scSo.FindProperty("m_BadgeText").objectReferenceValue = tmp;
            scSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, UiPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SetupUiContainersInScene(GameObject uiPrefab)
        {
            Canvas hudCanvas = null;
            GameObject hudCanvasObj = GameObject.Find("HUD_Canvas");
            if (hudCanvasObj != null) hudCanvas = hudCanvasObj.GetComponent<Canvas>();

            if (hudCanvas == null)
            {
                Debug.LogError("[UI Container Setup] HUD_Canvas not found in scene!");
                return;
            }

            // Ensure CanvasScaler
            CanvasScaler scaler = hudCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 0f;
            }

            // Ensure GraphicRaycaster
            if (hudCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                hudCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            // Find or create [Containers_UI_Root]
            Transform existingRoot = hudCanvas.transform.Find("[Containers_UI_Root]");
            GameObject uiRootObj;
            if (existingRoot != null)
            {
                uiRootObj = existingRoot.gameObject;
                for (int c = uiRootObj.transform.childCount - 1; c >= 0; c--)
                {
                    Object.DestroyImmediate(uiRootObj.transform.GetChild(c).gameObject);
                }
            }
            else
            {
                uiRootObj = new GameObject("[Containers_UI_Root]", typeof(RectTransform));
                uiRootObj.transform.SetParent(hudCanvas.transform, false);
            }

            // Ensure it is rendered behind popups but in front of background
            uiRootObj.transform.SetSiblingIndex(0);

            RectTransform rootRt = uiRootObj.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = Vector2.zero;
            rootRt.sizeDelta = new Vector2(1080f, 1920f);

            // 1. Setup 4 Slots Row
            GameObject slotsRowObj = new GameObject("[Slots_Row]", typeof(RectTransform));
            slotsRowObj.transform.SetParent(uiRootObj.transform, false);
            RectTransform slotsRowRt = slotsRowObj.GetComponent<RectTransform>();
            slotsRowRt.anchorMin = new Vector2(0.5f, 0.5f);
            slotsRowRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotsRowRt.pivot = new Vector2(0.5f, 0.5f);
            slotsRowRt.anchoredPosition = new Vector2(0f, -342f);
            slotsRowRt.sizeDelta = new Vector2(1080f, 200f);

            Sprite slotSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Marina/slot_rounded_square.png");
            float[] colXs = new float[] { -345f, -115f, 115f, 345f };
            List<ShipSlot> slotList = new List<ShipSlot>();

            for (int i = 0; i < 4; i++)
            {
                GameObject slotObj = new GameObject($"UI_Slot_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ShipSlot));
                slotObj.transform.SetParent(slotsRowObj.transform, false);

                RectTransform srt = slotObj.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(0.5f, 0.5f);
                srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = new Vector2(colXs[i], 0f);
                srt.sizeDelta = new Vector2(195f, 175f);
                srt.localScale = Vector3.one;

                Image simg = slotObj.GetComponent<Image>();
                simg.sprite = slotSprite;
                simg.color = Color.white;
                simg.raycastTarget = false;

                ShipSlot slot = slotObj.GetComponent<ShipSlot>();
                slot.SlotIndex = i;
                slotList.Add(slot);
            }

            // 2. Setup 8 Queue Spots and Spawn the 8 Containers
            GameObject queueSpotsObj = new GameObject("[Queue_Spots]", typeof(RectTransform));
            queueSpotsObj.transform.SetParent(uiRootObj.transform, false);
            RectTransform queueSpotsRt = queueSpotsObj.GetComponent<RectTransform>();
            queueSpotsRt.anchorMin = new Vector2(0.5f, 0.5f);
            queueSpotsRt.anchorMax = new Vector2(0.5f, 0.5f);
            queueSpotsRt.pivot = new Vector2(0.5f, 0.5f);
            queueSpotsRt.anchoredPosition = new Vector2(0f, -359f);
            queueSpotsRt.sizeDelta = new Vector2(1080f, 1920f);

            float[] rowYs = new float[] { -215f, -435f };
            float[] spotXs = new float[] { -345f, -115f, 115f, 345f };

            Color[] colors = new Color[]
            {
                new Color(0.92f, 0.16f, 0.19f), // Red
                new Color(0.08f, 0.52f, 0.98f), // Blue
                new Color(0.98f, 0.78f, 0.06f), // Yellow
                new Color(0.13f, 0.78f, 0.38f), // Green
                new Color(0.55f, 0.18f, 0.92f), // Purple
                new Color(0.98f, 0.45f, 0.08f), // Orange
                new Color(0.08f, 0.76f, 0.90f), // Cyan
                new Color(0.92f, 0.18f, 0.63f)  // Pink
            };

            int[] caps = new int[] { 12, 30, 18, 25, 22, 17, 26, 30 };

            List<Transform> spotTransforms = new List<Transform>();
            List<ShipController> waitingShips = new List<ShipController>();

            string[] colorNames = new string[] { "Red", "Blue", "Yellow", "Green", "Purple", "Orange", "Cyan", "Pink" };

            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    int spotIdx = r * 4 + c;
                    string colName = colorNames[spotIdx];

                    GameObject spotObj = new GameObject($"Spot_{spotIdx}", typeof(RectTransform));
                    spotObj.transform.SetParent(queueSpotsObj.transform, false);

                    RectTransform spotRt = spotObj.GetComponent<RectTransform>();
                    spotRt.anchorMin = new Vector2(0.5f, 0.5f);
                    spotRt.anchorMax = new Vector2(0.5f, 0.5f);
                    spotRt.pivot = new Vector2(0.5f, 0.5f);
                    spotRt.anchoredPosition = new Vector2(spotXs[c], rowYs[r]);
                    spotRt.sizeDelta = new Vector2(160f, 163f);
                    spotRt.localScale = Vector3.one;

                    spotTransforms.Add(spotObj.transform);

                    // Instantiate UI Container under spot and unpack to ensure all serialized properties persist cleanly
                    GameObject containerObj = (GameObject)PrefabUtility.InstantiatePrefab(uiPrefab, spotObj.transform);
                    PrefabUtility.UnpackPrefabInstance(containerObj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    containerObj.name = $"UI_Container_{spotIdx}_{colName}";
                    RectTransform crt = containerObj.GetComponent<RectTransform>();
                    crt.anchoredPosition = Vector2.zero;
                    crt.sizeDelta = new Vector2(160f, 163f);
                    crt.localScale = Vector3.one;

                    Transform pillTr = containerObj.transform.Find("Badge_Pill");
                    if (pillTr != null)
                    {
                        RectTransform prt = pillTr.GetComponent<RectTransform>();
                        if (prt != null)
                        {
                            prt.anchoredPosition = new Vector2(0f, 14f);
                            prt.sizeDelta = new Vector2(140f, 110f);
                        }
                        Image pImg = pillTr.GetComponent<Image>();
                        if (pImg != null) pImg.color = Color.clear;
                    }

                    Sprite contSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Textures/Containers/Container_{colName}.png");
                    Image cImg = containerObj.GetComponent<Image>();
                    if (cImg != null)
                    {
                        cImg.sprite = contSprite;
                        cImg.color = Color.white;
                    }

                    TextMeshProUGUI tmp = containerObj.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (tmp != null)
                    {
                        tmp.text = caps[spotIdx].ToString();
                        tmp.fontSize = 58f;
                        tmp.enableAutoSizing = true;
                        tmp.fontSizeMin = 36f;
                        tmp.fontSizeMax = 64f;
                        tmp.fontStyle = FontStyles.Bold;
                        tmp.color = Color.white;
                        tmp.outlineColor = Color.black;
                        tmp.outlineWidth = 0.28f;
                    }

                    ShipController ship = containerObj.GetComponent<ShipController>();
                    ship.IsUiContainer = true;
                    ship.ContainerImage = cImg;

                    var shipSo = new SerializedObject(ship);
                    shipSo.FindProperty("m_ShipColor").colorValue = colors[spotIdx];
                    shipSo.FindProperty("m_ColorName").stringValue = colName;
                    shipSo.FindProperty("m_Capacity").intValue = caps[spotIdx];
                    shipSo.FindProperty("m_CurrentCargo").intValue = 0;
                    shipSo.FindProperty("m_IsUiContainer").boolValue = true;
                    if (cImg != null) shipSo.FindProperty("m_ContainerImage").objectReferenceValue = cImg;
                    if (tmp != null) shipSo.FindProperty("m_BadgeText").objectReferenceValue = tmp;
                    shipSo.ApplyModifiedPropertiesWithoutUndo();

                    waitingShips.Add(ship);
                }
            }

            // 3. Connect to ShipQueuePool
            ShipQueuePool pool = Object.FindFirstObjectByType<ShipQueuePool>();
            if (pool != null)
            {
                var so = new SerializedObject(pool);
                so.FindProperty("m_ShipPrefab").objectReferenceValue = uiPrefab;
                so.FindProperty("m_Columns").intValue = 4;
                so.FindProperty("m_Rows").intValue = 2;

                var spotsProp = so.FindProperty("m_QueueSpots");
                spotsProp.ClearArray();
                for (int i = 0; i < spotTransforms.Count; i++)
                {
                    spotsProp.InsertArrayElementAtIndex(i);
                    spotsProp.GetArrayElementAtIndex(i).objectReferenceValue = spotTransforms[i];
                }

                var shipsProp = so.FindProperty("m_WaitingShips");
                shipsProp.ClearArray();
                for (int i = 0; i < waitingShips.Count; i++)
                {
                    shipsProp.InsertArrayElementAtIndex(i);
                    shipsProp.GetArrayElementAtIndex(i).objectReferenceValue = waitingShips[i];
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pool);
            }

            // 4. Connect to ShipDispatcher
            ShipDispatcher dispatcher = Object.FindFirstObjectByType<ShipDispatcher>();
            if (dispatcher != null)
            {
                var so = new SerializedObject(dispatcher);
                var slotsProp = so.FindProperty("m_Slots");
                if (slotsProp != null)
                {
                    slotsProp.ClearArray();
                    for (int i = 0; i < slotList.Count; i++)
                    {
                        slotsProp.InsertArrayElementAtIndex(i);
                        slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotList[i];
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dispatcher);
            }
        }

        private static void CleanupOld3DWorldElements()
        {
            // Disable or destroy water zone and old 3D slots layout
            GameObject waterZone = GameObject.Find("[Zone_Water_LowerArea]");
            if (waterZone != null) waterZone.SetActive(false);

            GameObject oldSlots = GameObject.Find("[Marina_Dock_Slots]");
            if (oldSlots != null) oldSlots.SetActive(false);

            // Find all [WaterSlotsRow] objects in scene
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t != null && t.name == "[WaterSlotsRow]")
                {
                    t.gameObject.SetActive(false);
                }
            }

            // Clean up any 3D boats lingering in the scene
            ShipController[] allShips = Object.FindObjectsByType<ShipController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var s in allShips)
            {
                if (s == null) continue;
                if (!s.IsUiContainer)
                {
                    s.gameObject.SetActive(false);
                    Object.DestroyImmediate(s.gameObject);
                }
            }

            // Hide old queue pool transform children if it exists
            ShipQueuePool pool = Object.FindFirstObjectByType<ShipQueuePool>();
            if (pool != null && pool.transform.childCount > 0)
            {
                for (int i = pool.transform.childCount - 1; i >= 0; i--)
                {
                    var child = pool.transform.GetChild(i);
                    if (child.name.StartsWith("Spot_") || child.name.StartsWith("Waiting_"))
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }
            }
        }

        // [MenuItem("PixelGame/🔗 Test Linked Containers Preview")]
        public static void TestLinkedContainersPreview()
        {
            ApplyCompleteReferenceLayout();

            // Find UI containers in scene
            var allShips = Object.FindObjectsByType<ShipController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            List<ShipController> uiShips = new List<ShipController>();
            foreach (var s in allShips)
            {
                if (s != null && s.IsUiContainer && s.gameObject.activeInHierarchy)
                {
                    uiShips.Add(s);
                }
            }

            if (uiShips.Count < 5)
            {
                Debug.LogWarning("[TestLinkedContainersPreview] Need at least 5 UI containers in scene.");
                return;
            }

            // Spot 0 & Spot 4 -> Vertical Link (Top Cyan 58, Bottom White 41)
            // Spot 2 & Spot 3 -> Horizontal Link (Left Cyan 20, Right Dark/Purple 40)
            ShipController ship58 = uiShips.Find(s => s.name.Contains("_0")) ?? uiShips[0];
            ShipController ship41 = uiShips.Find(s => s.name.Contains("_4")) ?? uiShips[4];
            ShipController ship20 = uiShips.Find(s => s.name.Contains("_2")) ?? uiShips[2];
            ShipController ship40 = uiShips.Find(s => s.name.Contains("_3")) ?? uiShips[3];

            // Setup colors & capacities to match reference
            Color cyanColor = new Color(0.08f, 0.76f, 0.90f);
            Color whiteColor = new Color(0.94f, 0.95f, 0.96f);
            Color darkColor = new Color(0.24f, 0.20f, 0.28f);

            ship58.Configure(cyanColor, 58);
            ship58.SetCapacity(58);

            ship41.Configure(whiteColor, 41);
            ship41.SetCapacity(41);

            ship20.Configure(cyanColor, 20);
            ship20.SetCapacity(20);

            ship40.Configure(darkColor, 40);
            ship40.SetCapacity(40);

            // Create Vertical Tether
            LinkedShipTether tetherVert = LinkedShipTether.CreateTether(ship58, ship41, 101);
            ship58.SetLinkedPartner(ship41, 101, tetherVert);
            ship41.SetLinkedPartner(ship58, 101, tetherVert);

            // Create Horizontal Tether
            LinkedShipTether tetherHoriz = LinkedShipTether.CreateTether(ship20, ship40, 102);
            ship20.SetLinkedPartner(ship40, 102, tetherHoriz);
            ship40.SetLinkedPartner(ship20, 102, tetherHoriz);

            tetherVert.UpdateTetherPositions();
            tetherHoriz.UpdateTetherPositions();

            ship58.UpdateLinkedUiVisuals();
            ship41.UpdateLinkedUiVisuals();
            ship20.UpdateLinkedUiVisuals();
            ship40.UpdateLinkedUiVisuals();

            ship58.UpdateBadgeText();
            ship41.UpdateBadgeText();
            ship20.UpdateBadgeText();
            ship40.UpdateBadgeText();

            Debug.Log("<color=#00FFAA><b>[Linked Containers Preview]</b></color> Configured linked containers! Capturing screenshot...");
            CaptureGameViewScreenshot.Capture();
        }
    }
}

