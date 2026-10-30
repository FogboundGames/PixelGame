using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.IO;

namespace PixelGame.Editor
{
    public static class AdjustSlotsAndContainersPosition
    {
        // Çok hafif aşağı taşıma miktarı (Canvas piksel birimi)
        // [Slots_Row] -307 -> -342 (-35px)
        // [Queue_Spots] -324 -> -359 (-35px)
        public const float ShiftAmount = -35f;

        // [MenuItem("PixelGame/⬇️ Shift Slots and Containers Down (-35px)")]
        public static void ApplyShift35()
        {
            ApplyShift(ShiftAmount);
        }

        // [MenuItem("PixelGame/⬇️ Shift Slots and Containers Down (-25px)")]
        public static void ApplyShift25()
        {
            ApplyShift(-25f);
        }

        public static void ApplyShift(float deltaY)
        {
            var slotsRow = GameObject.Find("[Slots_Row]");
            var queueSpots = GameObject.Find("[Queue_Spots]");

            if (slotsRow == null)
            {
                Debug.LogError("[AdjustSlots] [Slots_Row] bulunamadı!");
                return;
            }
            if (queueSpots == null)
            {
                Debug.LogError("[AdjustSlots] [Queue_Spots] bulunamadı!");
                return;
            }

            var srt = slotsRow.GetComponent<RectTransform>();
            var qrt = queueSpots.GetComponent<RectTransform>();

            Undo.RecordObject(srt, "Shift Slots Row");
            Undo.RecordObject(qrt, "Shift Queue Spots");

            Vector2 sPos = srt.anchoredPosition;
            Vector2 qPos = qrt.anchoredPosition;

            srt.anchoredPosition = new Vector2(sPos.x, sPos.y + deltaY);
            qrt.anchoredPosition = new Vector2(qPos.x, qPos.y + deltaY);

            Debug.Log($"[AdjustSlots] [Slots_Row] Y: {sPos.y} -> {srt.anchoredPosition.y}");
            Debug.Log($"[AdjustSlots] [Queue_Spots] Y: {qPos.y} -> {qrt.anchoredPosition.y}");

            EditorSceneManager.MarkSceneDirty(slotsRow.scene);
            EditorSceneManager.SaveScene(slotsRow.scene);

            RenderPreviewScreenshot();
        }

        public static void RenderPreviewScreenshot()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var rt = new RenderTexture(1080, 1920, 24);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes("scratch/preview_after_slots_shifted.png", bytes);
            Debug.Log("[AdjustSlots] Screenshot saved to scratch/preview_after_slots_shifted.png");

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
