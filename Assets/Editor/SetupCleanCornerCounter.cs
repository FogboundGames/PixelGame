using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupCleanCornerCounter
    {
        private const string CasualUIDir = "Assets/UI/CasualUI";
        private const string FontPath = "Assets/Fonts/LilitaOne-Regular.ttf";

        static SetupCleanCornerCounter()
        {
            // Otomatik tetikleme kapatıldı: sahne editör açılışında değiştirilmesin.
            // Gerekirse Tools menüsünden elle çalıştırılır.
            // EditorApplication.delayCall += ApplyCleanCorner;
        }

        [MenuItem("Tools/PixelGame/🎯 Köşe Sayacını & Üst HUD'ı Düzenle (Clean Corner & Top UI)")]
        public static void ApplyCleanCorner()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

            TMP_FontAsset lilitaTMP = SetupCasualTMPFont.GetOrCreateFontAsset();
            Font lilitaFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

            // 1. Üst UI Canvas'ını ScreenSpaceOverlay yap ve en tepeye sabitle
            Canvas overlayCanvas = null;
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.gameObject.name == "Canvas" || c.renderMode == RenderMode.ScreenSpaceOverlay || c.transform.Find("TopUI") != null)
                {
                    overlayCanvas = c;
                    break;
                }
            }

            if (overlayCanvas != null)
            {
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = overlayCanvas.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = overlayCanvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;

                Transform topUI = overlayCanvas.transform.Find("TopUI");
                if (topUI != null)
                {
                    RectTransform topRt = topUI.GetComponent<RectTransform>();
                    if (topRt != null)
                    {
                        topRt.anchorMin = new Vector2(0f, 1f);
                        topRt.anchorMax = new Vector2(1f, 1f);
                        topRt.pivot = new Vector2(0.5f, 1f);
                        topRt.anchoredPosition = new Vector2(0f, -30f);
                        topRt.sizeDelta = new Vector2(0f, 140f);
                    }
                }

                EditorUtility.SetDirty(overlayCanvas);
            }

            // 2. SlotRow altındaki TrackCornerSlot'u modern parlak 3B kapsül (Badge_JuicyPill) yap
            TruckSlotRow slotRow = Object.FindFirstObjectByType<TruckSlotRow>();
            if (slotRow != null)
            {
                Transform cornerSlot = slotRow.transform.Find("TrackCornerSlot");
                if (cornerSlot != null)
                {
                    RectTransform csRt = cornerSlot.GetComponent<RectTransform>();
                    if (csRt != null)
                    {
                        csRt.anchorMin = new Vector2(0.5f, 0.5f);
                        csRt.anchorMax = new Vector2(0.5f, 0.5f);
                        csRt.pivot = new Vector2(0.5f, 0.5f);
                        csRt.anchoredPosition = new Vector2(-885f, 650f);
                        csRt.sizeDelta = new Vector2(150f, 68f);
                        csRt.localScale = Vector3.one;
                    }

                    Image csImg = cornerSlot.GetComponent<Image>();
                    if (csImg != null)
                    {
                        Sprite juicyPill = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Badge_JuicyPill.png");
                        if (juicyPill == null) juicyPill = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Badge_MiniPill.png");
                        if (juicyPill == null) juicyPill = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/ui_pill.png");

                        csImg.sprite = juicyPill;
                        csImg.type = Image.Type.Simple;
                        csImg.preserveAspect = true;
                        csImg.color = Color.white;
                    }

                    // Eski 3B renderer veya buton parçalarını temizle/gizle
                    foreach (MeshRenderer mr in cornerSlot.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        mr.enabled = false;
                    }

                    // CounterText nesnesini tam ortaya yerleştir ve formatla
                    Transform counterTextTr = cornerSlot.Find("CounterText");
                    if (counterTextTr == null)
                    {
                        GameObject ctObj = new GameObject("CounterText", typeof(RectTransform));
                        ctObj.transform.SetParent(cornerSlot, false);
                        counterTextTr = ctObj.transform;
                    }

                    // Diğer başıboş alt nesneleri temizle
                    for (int i = cornerSlot.childCount - 1; i >= 0; i--)
                    {
                        Transform ch = cornerSlot.GetChild(i);
                        if (ch != counterTextTr && !ch.name.StartsWith("Place_"))
                        {
                            Object.DestroyImmediate(ch.gameObject);
                        }
                    }

                    RectTransform ctRt = counterTextTr.GetComponent<RectTransform>();
                    if (ctRt != null)
                    {
                        ctRt.anchorMin = Vector2.zero;
                        ctRt.anchorMax = Vector2.one;
                        ctRt.pivot = new Vector2(0.5f, 0.5f);
                        ctRt.anchoredPosition = new Vector2(0f, 2f);
                        ctRt.sizeDelta = Vector2.zero;
                    }

                    TextMeshProUGUI counterTmp = counterTextTr.GetComponent<TextMeshProUGUI>();
                    if (counterTmp == null) counterTmp = counterTextTr.gameObject.AddComponent<TextMeshProUGUI>();
                    if (lilitaTMP != null) counterTmp.font = lilitaTMP;
                    counterTmp.fontSize = 38;
                    counterTmp.fontStyle = FontStyles.Bold;
                    counterTmp.color = Color.white;
                    counterTmp.alignment = TextAlignmentOptions.Center;
                    counterTmp.text = "2/5";
                    counterTmp.raycastTarget = false;

                    Text oldText = counterTextTr.GetComponent<Text>();
                    if (oldText != null) Object.DestroyImmediate(oldText);

                    TruckDispatcher dispatcher = Object.FindFirstObjectByType<TruckDispatcher>();
                    if (dispatcher != null)
                    {
                        SerializedObject so = new SerializedObject(dispatcher);
                        SerializedProperty tmpProp = so.FindProperty("m_TrackCornerCounterTMP");
                        if (tmpProp != null)
                        {
                            tmpProp.objectReferenceValue = counterTmp;
                            so.ApplyModifiedProperties();
                        }
                        dispatcher.UpdateTrackCornerCounter();
                        EditorUtility.SetDirty(dispatcher);
                    }

                    EditorUtility.SetDirty(cornerSlot.gameObject);
                }
            }

            AssetDatabase.SaveAssets();
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            Debug.Log("<color=#00FFAA><b>[CleanCornerCounter]</b></color> Köşe sayacı şık 3B kapsül olarak yenilendi, altındaki çift yazı kaldırıldı!");
        }
    }
}
