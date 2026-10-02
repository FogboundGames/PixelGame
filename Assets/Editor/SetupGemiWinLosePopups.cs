using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    /// <summary>
    /// Gemi sahnesindeki (Assets/Scenes/Gemi.unity) HUD_Canvas altına
    /// Win (Level Complete) ve Lose (Level Fail) modal panellerini kurar veya var olanları bağlar.
    /// 
    /// KURAL:
    /// - Asla otomatik (InitializeOnLoad / delayCall / static constructor) çalışmaz.
    /// - Yalnızca kullanıcı menüden veya Level Designer butonundan tıkladığında çalışır.
    /// - Sahnedeki var olan panelleri ASLA silmez (DestroyImmediate yapmaz), var olanları korur ve bağlar.
    /// </summary>
    public static class SetupGemiWinLosePopups
    {
        private const string ScenePath = "Assets/Scenes/Gemi.unity";

        private const string WinPanelSpritePath = "Assets/UI/panellevel_clean.png";
        private const string WinRewardSpritePath = "Assets/UI/coin_reward_button(1)(1).png";
        private const string WinContinueSpritePath = "Assets/UI/continue.png";
        private const string FailPanelSpritePath = "Assets/UI/fail.png";
        private const string FailRetrySpritePath = "Assets/UI/tryagain_clean.png";

        [MenuItem("Tools/PixelGame/UI/🏆 Sahneye Win & Fail Panellerini Kur")]
        public static void SetupModalsInScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                Debug.LogWarning($"<color=#FFAA00><b>[WinLosePopups]</b></color> Aktif sahne Gemi sahnesi değil ({activeScene.name}). Lütfen önce Gemi sahnesini açın.");
                return;
            }

            GameObject canvasGo = GameObject.Find("HUD_Canvas");
            if (canvasGo == null)
            {
                Debug.LogError("<color=#FF4444><b>[WinLosePopups]</b></color> HUD_Canvas sahnede bulunamadı!");
                return;
            }

            CasualHudController hudCtrl = canvasGo.GetComponent<CasualHudController>();
            if (hudCtrl == null)
            {
                hudCtrl = canvasGo.AddComponent<CasualHudController>();
            }

            // Sprite yüklemeleri (mevcut olanlar korunur, sadece boşsa atanır)
            if (hudCtrl.CompletePanelSprite == null)
                hudCtrl.CompletePanelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WinPanelSpritePath);
            if (hudCtrl.CompleteRewardSprite == null)
                hudCtrl.CompleteRewardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WinRewardSpritePath);
            if (hudCtrl.CompleteContinueSprite == null)
                hudCtrl.CompleteContinueSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WinContinueSpritePath);
            if (hudCtrl.FailPanelSprite == null)
                hudCtrl.FailPanelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FailPanelSpritePath);
            if (hudCtrl.FailRetrySprite == null)
                hudCtrl.FailRetrySprite = AssetDatabase.LoadAssetAtPath<Sprite>(FailRetrySpritePath);

            Undo.RegisterCompleteObjectUndo(canvasGo, "Setup Win & Fail Popups");

            // 1. Level Complete Popup (Varsa KORU, Yoksa OLUŞTUR)
            Transform winTr = canvasGo.transform.Find("LevelComplete_Popup");
            GameObject winPopup;
            if (winTr != null)
            {
                winPopup = winTr.gameObject;
                Debug.Log("<color=#00FFAA><b>[WinLosePopups]</b></color> Sahnede mevcut 'LevelComplete_Popup' korundu ve controller'a bağlandı.");
            }
            else
            {
                winPopup = BuildCompletePopup(canvasGo.transform, hudCtrl.CompletePanelSprite, hudCtrl.CompleteRewardSprite, hudCtrl.CompleteContinueSprite);
                Undo.RegisterCreatedObjectUndo(winPopup, "Create LevelComplete_Popup");
                winPopup.SetActive(false);
                Debug.Log("<color=#00FFAA><b>[WinLosePopups]</b></color> Yeni 'LevelComplete_Popup' oluşturuldu.");
            }
            hudCtrl.LevelCompletePopup = winPopup;

            // 2. Level Fail Popup (Varsa KORU, Yoksa OLUŞTUR)
            Transform failTr = canvasGo.transform.Find("LevelFail_Popup");
            GameObject failPopup;
            if (failTr != null)
            {
                failPopup = failTr.gameObject;
                Debug.Log("<color=#00FFAA><b>[WinLosePopups]</b></color> Sahnede mevcut 'LevelFail_Popup' korundu ve controller'a bağlandı.");
            }
            else
            {
                failPopup = BuildFailPopup(canvasGo.transform, hudCtrl.FailPanelSprite, hudCtrl.FailRetrySprite);
                Undo.RegisterCreatedObjectUndo(failPopup, "Create LevelFail_Popup");
                failPopup.SetActive(false);
                Debug.Log("<color=#00FFAA><b>[WinLosePopups]</b></color> Yeni 'LevelFail_Popup' oluşturuldu.");
            }
            hudCtrl.LevelFailPopup = failPopup;

            // 3. Buton Event Bağlantıları
            hudCtrl.WireExistingCompletePopupButtons();
            hudCtrl.WireExistingFailPopupButtons();

            EditorUtility.SetDirty(hudCtrl);
            EditorUtility.SetDirty(canvasGo);
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log("<color=#00FFAA><b>[WinLosePopups]</b></color> ✅ Win ve Fail panelleri HUD_Canvas üzerinde başarıyla yapılandırıldı.");
        }

        private static GameObject BuildCompletePopup(Transform parent, Sprite panelSp, Sprite rewardSp, Sprite continueSp)
        {
            const float panelWidth = 780f;

            // Kök Karartma
            GameObject root = new GameObject("LevelComplete_Popup", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rt = root.GetComponent<RectTransform>();
            SetStretch(rt);
            Image bg = root.GetComponent<Image>();
            bg.color = new Color(0.03f, 0.08f, 0.12f, 0.6f);
            bg.raycastTarget = true;

            // Complete_Card
            Vector2 panelSize = SpriteSize(panelSp, panelWidth);
            GameObject card = CreateSpriteImage(root.transform, "Complete_Card", panelSp, panelSize, new Vector2(0f, 40f));
            card.GetComponent<Image>().raycastTarget = true;

            // Reward
            CreateSpriteImage(card.transform, "Reward", rewardSp, SpriteSize(rewardSp, panelWidth * 0.58f), new Vector2(0f, -panelSize.y * 0.03f));

            // Btn_Continue
            GameObject btnObj = CreateSpriteImage(card.transform, "Btn_Continue", continueSp, SpriteSize(continueSp, panelWidth * 0.71f), new Vector2(0f, -panelSize.y * 0.29f));
            Image btnImg = btnObj.GetComponent<Image>();
            btnImg.raycastTarget = true;
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.transition = Selectable.Transition.None;

            return root;
        }

        private static GameObject BuildFailPopup(Transform parent, Sprite panelSp, Sprite retrySp)
        {
            const float panelWidth = 780f;

            // Kök Karartma
            GameObject root = new GameObject("LevelFail_Popup", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rt = root.GetComponent<RectTransform>();
            SetStretch(rt);
            Image bg = root.GetComponent<Image>();
            bg.color = new Color(0.03f, 0.06f, 0.12f, 0.6f);
            bg.raycastTarget = true;

            // Fail_Card
            Vector2 panelSize = SpriteSize(panelSp, panelWidth);
            GameObject card = CreateSpriteImage(root.transform, "Fail_Card", panelSp, panelSize, new Vector2(0f, 40f));
            card.GetComponent<Image>().raycastTarget = true;

            // Btn_Retry_Modal
            GameObject btnObj = CreateSpriteImage(card.transform, "Btn_Retry_Modal", retrySp, SpriteSize(retrySp, panelWidth * 0.71f), new Vector2(0f, -panelSize.y * 0.31f));
            Image btnImg = btnObj.GetComponent<Image>();
            btnImg.raycastTarget = true;
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.transition = Selectable.Transition.None;

            return root;
        }

        private static void SetStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Vector2 SpriteSize(Sprite sprite, float width)
        {
            if (sprite == null) return new Vector2(width, width * 0.75f);
            return new Vector2(width, width * sprite.rect.height / Mathf.Max(1f, sprite.rect.width));
        }

        private static GameObject CreateSpriteImage(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 pos)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            Image img = obj.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return obj;
        }

        [MenuItem("Tools/PixelGame/UI/👁️ Win Panelini Aç - Kapat")]
        public static void ToggleWinPanel()
        {
            GameObject canvasGo = GameObject.Find("HUD_Canvas");
            if (canvasGo == null) return;
            Transform winTr = canvasGo.transform.Find("LevelComplete_Popup");
            if (winTr != null)
            {
                Undo.RecordObject(winTr.gameObject, "Toggle Win Panel");
                winTr.gameObject.SetActive(!winTr.gameObject.activeSelf);
                Selection.activeGameObject = winTr.gameObject;
            }
            else
            {
                Debug.LogWarning("LevelComplete_Popup sahnede bulunamadı. Önce 'Sahneye Win & Fail Panellerini Kur' menüsünü çalıştırın.");
            }
        }

        [MenuItem("Tools/PixelGame/UI/👁️ Fail Panelini Aç - Kapat")]
        public static void ToggleFailPanel()
        {
            GameObject canvasGo = GameObject.Find("HUD_Canvas");
            if (canvasGo == null) return;
            Transform failTr = canvasGo.transform.Find("LevelFail_Popup");
            if (failTr != null)
            {
                Undo.RecordObject(failTr.gameObject, "Toggle Fail Panel");
                failTr.gameObject.SetActive(!failTr.gameObject.activeSelf);
                Selection.activeGameObject = failTr.gameObject;
            }
            else
            {
                Debug.LogWarning("LevelFail_Popup sahnede bulunamadı. Önce 'Sahneye Win & Fail Panellerini Kur' menüsünü çalıştırın.");
            }
        }
    }
}
