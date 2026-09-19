using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// FreeLowpolyScifiObjects paketindeki malzemeleri Built-in Standard shader'dan
    /// Universal Render Pipeline (URP/Lit) shader'ına yükselterek mor (magenta) görünümünü düzeltir.
    /// </summary>
    [InitializeOnLoad]
    public static class UpgradeScifiMaterialsToURP
    {
        private const string SessionKey = "UpgradeScifiMaterialsToURP_Done_v1";

        static UpgradeScifiMaterialsToURP()
        {
            EditorApplication.delayCall += UpgradeMaterialsDelayed;
        }

        private static void UpgradeMaterialsDelayed()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            UpgradeMaterials(silent: true);
        }

        [MenuItem("Tools/PixelGame/🔧 Mor Kaplamaları Düzelt (Fix Scifi URP Materials)", priority = 30)]
        public static void MenuUpgradeMaterials()
        {
            UpgradeMaterials(silent: false);
        }

        public static void UpgradeMaterials(bool silent = false)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogWarning("[PixelGame] 'Universal Render Pipeline/Lit' shader'ı bulunamadı!");
                return;
            }

            string[] matPaths = new string[]
            {
                "Assets/FreeLowpolyScifiObjects/Art/Materials/ColorTexture 1.mat",
                "Assets/FreeLowpolyScifiObjects/Art/Materials/floor.mat",
                "Assets/FreeLowpolyScifiObjects/Art/Materials/Neon 1.mat",
                "Assets/FreeLowpolyScifiObjects/Art/Materials/Neon 2.mat",
                "Assets/FreeLowpolyScifiObjects/Art/Materials/Neon 3.mat"
            };

            int upgradedCount = 0;
            foreach (string path in matPaths)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                if (mat.shader != urpLit)
                {
                    Color col = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                    Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    Color emCol = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
                    bool hasEmission = emCol.maxColorComponent > 0.01f;

                    mat.shader = urpLit;

                    if (mainTex != null)
                    {
                        mat.SetTexture("_BaseMap", mainTex);
                        mat.SetTexture("_MainTex", mainTex);
                    }
                    mat.SetColor("_BaseColor", col);
                    mat.SetColor("_Color", col);

                    if (hasEmission)
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", emCol);
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }

                    mat.SetFloat("_Smoothness", 0.4f);
                    mat.SetFloat("_Metallic", 0f);

                    EditorUtility.SetDirty(mat);
                    upgradedCount++;
                }
            }

            if (upgradedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"<color=#00FFAA><b>[PixelGame]</b></color> {upgradedCount} adet malzeme başarıyla Universal Render Pipeline/Lit formatına yükseltildi (Mor renk düzeltildi)!");
                if (!silent)
                {
                    EditorUtility.DisplayDialog("Kaplamalar Düzeltildi",
                        $"{upgradedCount} adet malzeme URP Lit shader'ına dönüştürüldü.\nArtık mor görünmeyecek!", "Tamam");
                }
            }
            else if (!silent)
            {
                EditorUtility.DisplayDialog("Bilgi", "Tüm malzemeler zaten güncel URP Lit formatında!", "Tamam");
            }
        }
    }
}
