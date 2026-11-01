using UnityEngine;
using UnityEngine.UI;

namespace PixelGame
{
    /// <summary>
    /// HUD'daki "i" düğmesi: veri izni ekranını açar. Sahnede durur, yeri Editor'den ayarlanır.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("PixelGame/Land Flow Privacy Button")]
    public sealed class LandFlowPrivacyButton : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(LandFlowPrivacy.ShowDialog);
        }
    }
}
