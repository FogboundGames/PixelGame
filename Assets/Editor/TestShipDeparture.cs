using UnityEngine;
using UnityEditor;
using PixelGame;

namespace PixelGame.Editor
{
    public static class TestShipDeparture
    {
        [MenuItem("Tools/PixelGame/🧪 Test Ship Departure and Badge Hide", priority = 20)]
        public static void RunTest()
        {
            Debug.Log("<color=#00FFAA><b>[TestShipDeparture]</b></color> Starting Ship Departure and Badge Disappearance Verification...");

            ShipController ship = Object.FindFirstObjectByType<ShipController>();
            if (ship == null)
            {
                Debug.LogError("[TestShipDeparture] Sahnede test edilecek ShipController bulunamadı!");
                return;
            }

            Debug.Log($"[TestShipDeparture] Bulunan Gemi: {ship.name}, Renk: {ship.ShipColor}, Kapasite: {ship.Capacity}, Kalan: {ship.RemainingCapacity}");

            // 1. Doldurma testi (AddCargo to full)
            int fillAmount = ship.RemainingCapacity;
            ship.AddCargo(fillAmount);

            // 2. Doğrulama
            bool isFull = ship.IsFull;
            bool isDeparting = ship.IsDeparting;
            int remaining = ship.RemainingCapacity;

            Transform badgeCanvas = ship.transform.Find("Ship_Capacity_Canvas");
            bool badgeInactive = (badgeCanvas == null || !badgeCanvas.gameObject.activeSelf);

            Debug.Log($"[TestShipDeparture] Sonuçlar:\n" +
                      $"- IsFull: {isFull} (Beklenen: True)\n" +
                      $"- RemainingCapacity: {remaining} (Beklenen: 0)\n" +
                      $"- IsDeparting: {isDeparting} (Beklenen: True)\n" +
                      $"- BadgeCanvas Inactive: {badgeInactive} (Beklenen: True)");

            if (isFull && isDeparting && remaining == 0 && badgeInactive)
            {
                Debug.Log("<color=#00FFAA><b>[TestShipDeparture]</b></color> ✅ TÜM TESTLER BAŞARIYLA GEÇTİ! Sayı dolduğunda text yok oldu ve kalkış hareketi başlatıldı.");
            }
            else
            {
                Debug.LogError("[TestShipDeparture] ❌ Test başarısız! Bazı koşullar sağlanamadı.");
            }
        }
    }
}
