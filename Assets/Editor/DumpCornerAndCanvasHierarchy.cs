using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class DumpCornerAndCanvasHierarchy
    {
        static DumpCornerAndCanvasHierarchy()
        {
            // Otomatik tetikleme kapatıldı: dump dosyası editör açılışında yazılmasın.
            // Gerekirse Tools menüsünden elle çalıştırılır.
            // EditorApplication.delayCall += Dump;
        }

        [MenuItem("Tools/PixelGame/Dump Corner & Canvas Hierarchy")]
        public static void Dump()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== CORNER & SLOT CANVAS HIERARCHY ===");

            GameObject slotCanvas = GameObject.Find("SlotCanvas");
            if (slotCanvas != null)
            {
                DumpTransform(slotCanvas.transform, 0, sb);
            }

            GameObject cornerSlot = GameObject.Find("TrackCornerSlot");
            if (cornerSlot != null && slotCanvas == null)
            {
                DumpTransform(cornerSlot.transform, 0, sb);
            }

            GameObject rails = GameObject.Find("PerimeterRails");
            if (rails != null)
            {
                DumpTransform(rails.transform, 0, sb);
            }

            Debug.Log(sb.ToString());
            System.IO.File.WriteAllText("Assets/Editor/corner_hierarchy.txt", sb.ToString());
        }

        private static void DumpTransform(Transform t, int indent, StringBuilder sb)
        {
            string pad = new string(' ', indent * 2);
            string compList = "";
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null) continue;
                if (c is Text txt) compList += $" [Text: '{txt.text}']";
                else if (c is TMPro.TextMeshProUGUI tmp) compList += $" [TMP: '{tmp.text}']";
                else if (c is Image img) compList += $" [Image: {(img.sprite != null ? img.sprite.name : "null")}]";
                else compList += $" [{c.GetType().Name}]";
            }

            sb.AppendLine($"{pad}- '{t.name}' (activeSelf={t.gameObject.activeSelf}, pos={t.localPosition}) -> {compList}");

            for (int i = 0; i < t.childCount; i++)
            {
                DumpTransform(t.GetChild(i), indent + 1, sb);
            }
        }
    }
}
