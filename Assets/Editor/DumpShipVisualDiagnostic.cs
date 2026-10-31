using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class DumpShipVisualDiagnostic
    {
        static DumpShipVisualDiagnostic()
        {
            // Otomatik çalıştırma kapalı. Menüden elle çalıştırılabilir.
            /*
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                RunDiagnostic();
            };
            */
        }

        // [MenuItem("PixelGame/🔍 Diagnostic Dump")]
        public static void RunDiagnostic()
        {
            Debug.Log("=== SHIP VISUAL DIAGNOSTIC ===");
            var ships = Object.FindObjectsByType<ShipController>(FindObjectsSortMode.None);
            Debug.Log($"Total ShipControllers in scene: {ships.Length}");

            for (int i = 0; i < Mathf.Min(ships.Length, 3); i++)
            {
                var s = ships[i];
                if (s == null || s.IsUiContainer) continue;
                Debug.Log($"Ship [{s.name}] active={s.gameObject.activeSelf}, pos={s.transform.position}, scale={s.transform.localScale}");
                var vr = s.transform.Find("[VisualRoot]");
                if (vr == null)
                {
                    Debug.LogError($"Ship [{s.name}] has NO [VisualRoot]!");
                }
                else
                {
                    var mf = vr.GetComponent<MeshFilter>();
                    var mr = vr.GetComponent<MeshRenderer>();
                    Debug.Log($"  VisualRoot active={vr.gameObject.activeSelf}, localPos={vr.localPosition}, localScale={vr.localScale}, layer={vr.gameObject.layer}");
                    Debug.Log($"  MeshFilter mesh={(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name + " (verts=" + mf.sharedMesh.vertexCount + ", sub=" + mf.sharedMesh.subMeshCount + ")" : "NULL")}");
                    if (mr != null)
                    {
                        Debug.Log($"  MeshRenderer enabled={mr.enabled}, matCount={mr.sharedMaterials.Length}");
                        for (int m = 0; m < mr.sharedMaterials.Length; m++)
                        {
                            var mat = mr.sharedMaterials[m];
                            Debug.Log($"    Mat[{m}]: {(mat != null ? mat.name + " (shader=" + mat.shader.name + ", color=" + (mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor").ToString() : "no _BaseColor") + ")" : "NULL")}");
                        }
                    }
                    else
                    {
                        Debug.LogError("  NO MeshRenderer on VisualRoot!");
                    }
                }
            }

            Debug.Log("=== BACKGROUND DIAGNOSTIC ===");
            var bgObj = GameObject.Find("BackgroundImage");
            if (bgObj != null)
            {
                var raw = bgObj.GetComponent<RawImage>();
                Debug.Log($"BackgroundImage: active={bgObj.activeSelf}, texture={(raw != null && raw.texture != null ? raw.texture.name : "NULL")}, mat={(raw != null && raw.material != null ? raw.material.name + " (" + raw.material.shader.name + ")" : "NULL")}");
                var water = bgObj.GetComponent<HypercasualWaterController>();
                Debug.Log($"HypercasualWaterController on BackgroundImage: {(water != null ? "enabled=" + water.enabled : "NONE")}");
            }

            Debug.Log("=== MARINA PIER DIAGNOSTIC ===");
            var bridge = GameObject.Find("[Zone_Wooden_Bridge]");
            Debug.Log($"[Zone_Wooden_Bridge]: {(bridge != null ? "active=" + bridge.activeInHierarchy : "NOT FOUND")}");
            var curvedPier = GameObject.Find("[Marina_Curved_Pier]");
            Debug.Log($"[Marina_Curved_Pier]: {(curvedPier != null ? "active=" + curvedPier.activeInHierarchy : "NOT FOUND")}");
        }
    }
}
