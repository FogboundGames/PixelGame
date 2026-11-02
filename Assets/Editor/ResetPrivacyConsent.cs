using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// İzin ekranı cihazda bir kez çıktığı için editörde yeniden görmek üzere yalnızca izin cevabını siler
    /// (seviye ilerlemesi ve diğer ayarlar korunur). Bir sonraki Play'de ekran tekrar gelir.
    /// </summary>
    public static class ResetPrivacyConsent
    {
        [MenuItem("PixelGame/🔒 İzin Ekranını Sıfırla")]
        public static void Reset()
        {
            PlayerPrefs.DeleteKey("LandFlow_DataConsent");
            PlayerPrefs.Save();
            Debug.Log("[LandFlowPrivacy] İzin cevabı silindi; bir sonraki Play'de ekran çıkacak.");
        }
    }
}
