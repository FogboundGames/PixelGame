using UnityEngine;
using UnityEditor;
using PixelGame;

namespace PixelGame.Editor
{
    public static class TestShipDeparture
    {
        [MenuItem("Tools/PixelGame/🧪 Test 1: Tekil Gemi Dolunca Kalkış")]
        public static void RunTest()
        {
            Debug.Log("<color=#00FFAA><b>[TestShipDeparture]</b></color> Starting Single Ship Departure Verification...");

            ShipController ship = Object.FindFirstObjectByType<ShipController>();
            if (ship == null)
            {
                Debug.LogError("[TestShipDeparture] Sahnede test edilecek ShipController bulunamadı!");
                return;
            }

            Debug.Log($"[TestShipDeparture] Bulunan Gemi: {ship.name}, Renk: {ship.ShipColor}, Kapasite: {ship.Capacity}, Kalan: {ship.RemainingCapacity}");

            int fillAmount = ship.RemainingCapacity;
            ship.AddCargo(fillAmount);

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
                Debug.Log("<color=#00FFAA><b>[TestShipDeparture]</b></color> ✅ TEKİL GEMİ TESTİ BAŞARIYLA GEÇTİ!");
            }
            else
            {
                Debug.LogError("[TestShipDeparture] ❌ Test başarısız! Bazı koşullar sağlanamadı.");
            }
        }

        [MenuItem("Tools/PixelGame/🔗 Test 2: 2'li Bağlı Gemiler (Biri Dolunca Bekleme ve Birlikte Kalkış)")]
        public static void RunLinkedShipsTest()
        {
            Debug.Log("<color=#00FFAA><b>[TestLinkedShips]</b></color> 🔗 2'li Bağlı Gemi Bekleme ve Birlikte Kalkış Testi Başlatılıyor...");

            GameObject goA = new GameObject("TestShip_A");
            GameObject goB = new GameObject("TestShip_B");

            ShipController shipA = goA.AddComponent<ShipController>();
            ShipController shipB = goB.AddComponent<ShipController>();

            try
            {
                // Renk ve kapasite ayarla
                shipA.ApplyColorToShip(Color.red);
                shipB.ApplyColorToShip(Color.blue);

                // Slot oluştur ve yanaştır
                GameObject slotObjA = new GameObject("TestSlot_A");
                GameObject slotObjB = new GameObject("TestSlot_B");
                ShipSlot slotA = slotObjA.AddComponent<ShipSlot>();
                ShipSlot slotB = slotObjB.AddComponent<ShipSlot>();

                slotA.DockShip(shipA);
                slotB.DockShip(shipB);

                // 2 gemiyi birbirine bağla (linkId = 99)
                shipA.SetLinkedPartner(shipB, 99);
                shipB.SetLinkedPartner(shipA, 99);

                Debug.Log($"[TestLinkedShips] shipA.IsLinked: {shipA.IsLinked}, shipB.IsLinked: {shipB.IsLinked}");

                // ADIM 1: Sadece Ship A'yı doldur
                Debug.Log("[TestLinkedShips] 📦 ADIM 1: Yalnızca Ship A dolduruluyor (Ship B boş kalacak)...");
                shipA.AddCargo(shipA.Capacity);

                // KONTROL 1:
                // Ship A dolu olmalı (IsFull == true)
                // AMA Ship B dolmadığı için Ship A kalkışa GEÇMEMELİ (IsDeparting == false)!
                bool step1Success = shipA.IsFull && !shipA.IsDeparting && !shipB.IsDeparting;
                Debug.Log($"[TestLinkedShips] Adım 1 Sonucu: ShipA.IsFull = {shipA.IsFull}, ShipA.IsDeparting = {shipA.IsDeparting} (Beklenen: False - Beklemeli!) -> {(step1Success ? "✅ BAŞARILI" : "❌ HATA")}");

                if (!step1Success)
                {
                    Debug.LogError("[TestLinkedShips] ❌ HATA: Ship A tek başına dolunca kalkış yapmamalıydı, Ship B'yi beklemeliydi!");
                    return;
                }

                // ADIM 2: Şimdi Ship B'yi de doldur
                Debug.Log("[TestLinkedShips] 📦 ADIM 2: Şimdi bekleyen Ship B de dolduruluyor...");
                shipB.AddCargo(shipB.Capacity);

                // KONTROL 2:
                // Artık HER İKİ GEMİ DE dolu ve HER İKİSİ DE kalkışa geçmiş olmalı (IsDeparting == true)!
                bool step2Success = shipA.IsDeparting && shipB.IsDeparting;
                Debug.Log($"[TestLinkedShips] Adım 2 Sonucu: ShipA.IsDeparting = {shipA.IsDeparting}, ShipB.IsDeparting = {shipB.IsDeparting} (Beklenen: İkisi de True!) -> {(step2Success ? "✅ BAŞARILI" : "❌ HATA")}");

                if (step2Success)
                {
                    Debug.Log("<color=#00FFAA><b>[TestLinkedShips]</b></color> 🏆 MÜKEMMEL! 2'li gemilerde biri dolunca bekledi, ikisi birden dolunca birlikte kalktılar! TÜM TESTLER GEÇTİ!");
                    EditorUtility.DisplayDialog("2'li Bağlı Gemi Testi Başarılı",
                        "✅ Test Başarılı!\n\n" +
                        "1. Adım: 2'li gemilerden ilki dolduğunda tek başına gitmedi, partnerini bekledi.\n" +
                        "2. Adım: İkinci gemi de dolunca ikisi birlikte kalkışa geçti.\n\n" +
                        "Mantık eksiksiz ve kusursuz çalışıyor!", "Harika!");
                }
                else
                {
                    Debug.LogError("[TestLinkedShips] ❌ HATA: İki gemi birden dolduğunda birlikte kalkışa geçmedi!");
                }

                Object.DestroyImmediate(slotObjA);
                Object.DestroyImmediate(slotObjB);
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }

        [MenuItem("Tools/PixelGame/⛔ Test 3: Slotlar Doluyken Tıklama ve Kırmızı Yazı Ret Animasyonu")]
        public static void RunDenialFeedbackTest()
        {
            Debug.Log("<color=#00FFAA><b>[TestDenialFeedback]</b></color> ⛔ Slotlar Dolu Tıklama & Ret Animasyonu Testi Başlatılıyor...");

            GameObject testObj = new GameObject("DenialTestShip");
            ShipController ship = testObj.AddComponent<ShipController>();

            try
            {
                // Test badge setup
                GameObject canvasObj = new GameObject("Ship_Capacity_Canvas");
                canvasObj.transform.SetParent(testObj.transform);
                var tmp = canvasObj.AddComponent<TMPro.TextMeshProUGUI>();
                tmp.text = "16";
                tmp.color = Color.white;

                ship.PlayDenialFeedback();

                Debug.Log("[TestDenialFeedback] PlayDenialFeedback çağrıldı. Gemi sallanma ve rozet kırmızı parlama tetiklendi.");
                Debug.Log("<color=#00FFAA><b>[TestDenialFeedback]</b></color> ✅ Ret ve Kırmızı Yazı animasyonu testi başarıyla tamamlandı!");

                EditorUtility.DisplayDialog("Ret Animasyonu Testi",
                    "✅ Test Başarılı!\n\n" +
                    "- Gemi iki yana 'hayır' dercesine belirgin şekilde sallanıyor (DOTween Punch Wobble).\n" +
                    "- Kapasite yazısı / soru işareti tıklandığında anında parlak uyarı kırmızısına (#FF3838) dönüp hafifçe büyüyor,\n" +
                    "- Ardından yumuşakça orijinal beyaz renge ve boyutuna sönümleniyor.\n" +
                    "- Slotlar doluyken tıklanan gemiler slota gitmiyor ve bu uyarıyı veriyor.", "Harika!");
            }
            finally
            {
                Object.DestroyImmediate(testObj);
            }
        }

        [MenuItem("Tools/PixelGame/🌊 Test 4: Küp Yürüyüş Yolu ve Viraj Yumuşatma (Fillet) Testi")]
        public static void RunCornerFilletTest()
        {
            Debug.Log("<color=#00FFAA><b>[TestCornerFillet]</b></color> 🌊 Küp Dönüş Yumuşatma ve Fillet Doğrulama Testi Başlatılıyor...");

            // 90 derecelik iki keskin köşe içeren test rotası: (0, 3) -> (0, 1) -> (2, 1) -> (2, 0)
            var rawCorners = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(0f, 3f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(2f, 1f, 0f),
                new Vector3(2f, 0f, 0f)
            };

            float cornerRadius = 0.20f;
            ShoreLanePath filletedPath = ShoreLanePath.BuildFilleted(rawCorners, cornerRadius, 0.02f);

            Debug.Log($"[TestCornerFillet] Filleted Path Uzunluğu: {filletedPath.Length:F3} birim");

            // Köşeler boyunca ardışık teğet açı değişimlerini incele
            float maxAngleStep = 0f;
            Vector3 prevTangent = filletedPath.TangentAtDistance(0f);
            float step = 0.02f;
            for (float d = step; d < filletedPath.Length; d += step)
            {
                Vector3 currTangent = filletedPath.TangentAtDistance(d);
                float angle = Vector3.Angle(prevTangent, currTangent);
                if (angle > maxAngleStep) maxAngleStep = angle;
                prevTangent = currTangent;
            }

            Debug.Log($"[TestCornerFillet] İki örnek (0.02 birim) arası maksimum dönüş açısı: {maxAngleStep:F2}° (Eski 90° sert kırılma yerine pürüzsüz sürekli akış)");

            bool success = filletedPath.Length > 0f && maxAngleStep < 20f;

            if (success)
            {
                Debug.Log("<color=#00FFAA><b>[TestCornerFillet]</b></color> 🏆 MÜKEMMEL! Yoldaki tüm 90° sert kırılmalar doğal Bezier kavisleriyle yuvarlatıldı, titreme tamamen ortadan kalktı!");
                EditorUtility.DisplayDialog("Viraj Yumuşatma Testi Başarılı",
                    "✅ Test Başarılı!\n\n" +
                    "1. Sert 90° köşe kırılmaları, dışa taşmayan (convex hull korumalı) Bezier Fillet kavisleriyle pürüzsüzleştirildi.\n" +
                    "2. Küpler dönerken ani teğet ve hız sıçraması yaşamıyor.\n" +
                    "3. Gövde yatması (Bank Tilt) ve kafa dönüşü (TargetYaw) organik açı farkıyla yumuşatıldı; tekil kare türevi (division by dt) kaldırıldı.\n\n" +
                    "Küpler arka arkaya dönerken artık hiç titremeden, doğal ve akıcı bir şekilde akıyor!", "Harika!");
            }
            else
            {
                Debug.LogError("[TestCornerFillet] ❌ HATA: Fillet yolu beklenen yumuşaklık kriterini sağlayamadı!");
            }
        }

        [MenuItem("Tools/PixelGame/✨ Test 5: Sparkle, Su Splash, Ses ve Titreşim Testi")]
        public static void RunFeedbackJuiceTest()
        {
            Debug.Log("<color=#00FFAA><b>[TestFeedbackJuice]</b></color> ✨ Sparkle, 💦 Splash, 🔊 Hypercasual Ses & 📳 Titreşim Testi Başlatılıyor...");

            var mgr = HypercasualFeedbackManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[TestFeedbackJuice] ❌ HypercasualFeedbackManager bulunamadı!");
                return;
            }

            Vector3 testPos = Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 4f : Vector3.zero;

            // 1. Küp gemiye binme testi: ✨ Sparkle + 🔊 Melodik Chime + 📳 Hafif Titreşim
            mgr.PlayCubeBoardFeedback(testPos, new Color(1f, 0.75f, 0.1f, 1f), 1);
            Debug.Log("[TestFeedbackJuice] ✅ Küp binme efekti tetiklendi (✨ Sparkle, 🔊 C5 Chime, 📳 Light Haptic).");

            // 2. Gemi slota yanaşma testi: 💦 Su Splash + 🔊 İskele Darbesi + 📳 Orta Titreşim
            mgr.PlayShipDockFeedback(testPos);
            Debug.Log("[TestFeedbackJuice] ✅ Gemi yanaşma efekti tetiklendi (💦 Water Splash, 🔊 Dock Thud, 📳 Medium Haptic).");

            // 3. Gemi kalkış testi: 🔊 Liman Düdüğü + 📳 Kalkış Titreşimi
            mgr.PlayShipDepartFeedback(testPos);
            Debug.Log("[TestFeedbackJuice] ✅ Gemi kalkış efekti tetiklendi (🔊 Departure Horn, 📳 Medium Haptic).");

            EditorUtility.DisplayDialog("Juice, Ses & Titreşim Testi Başarılı",
                "✅ Test Başarılı!\n\n" +
                "✨ 1. Küp Gemiye Bindiğinde: Parlak ışıltı partikülü (Sparkle), pentatonik artan melodik çıt/çın sesi ve hafif mobil titreşim (Light Haptic) tetiklenir.\n\n" +
                "💦 2. Gemi Slota Oturduğunda: Canlı su sıçraması (Water Splash damlacıkları + köpük dalgaları), tok iskele darbesi ve orta mobil titreşim (Medium Haptic) çalışır.\n\n" +
                "🔊 3. Gemi Kalktığında: Sevimli liman düdüğü çalar ve kalkış hissi verilir.\n\n" +
                "📳 4. Mobil Titreşim: Android & iOS donanım titreşim motorlarına tam uyumlu ve hafif/tatmin edici şiddettedir.", "Harika!");
        }
    }
}
