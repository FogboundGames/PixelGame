using System;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Küp karakterlerin gemi ve arabalara giderkenki akıcı, organik ve hypercasual
    /// hareket parametrelerini tek bir merkezden yöneten ayar sınıfı.
    /// </summary>
    [CreateAssetMenu(fileName = "CubeMovementSettings", menuName = "PixelGame/Cube Movement Settings")]
    public class CubeMovementSettings : ScriptableObject
    {
        private static CubeMovementSettings s_DefaultInstance;
        public static CubeMovementSettings Default
        {
            get
            {
                if (s_DefaultInstance == null)
                {
                    s_DefaultInstance = Resources.Load<CubeMovementSettings>("CubeMovementSettings");
                    if (s_DefaultInstance == null)
                    {
                        s_DefaultInstance = CreateInstance<CubeMovementSettings>();
                        s_DefaultInstance.name = "CubeMovementSettings_RuntimeDefault";
                    }
                }
                return s_DefaultInstance;
            }
        }

        [Header("🚂 0. AKICI ZİNCİR & KARAKTER CANLILIĞI (Glide & Micro-Dynamics)")]
        [Tooltip("Referans videodaki gibi: küpler akıcı bir zincir halinde kayarak taşınır ama yaşayan bir karakter gibi tepki verir (%80 glide + %20 mikro-animasyon). " +
                 "Kaba paytak yürüyüş kapanır; momentum, pickup anticipation, viraj yön gecikmesi, mikro-yatma ve yumuşak fren devrededir.")]
        public bool SmoothGlide = true;

        [Tooltip("Görsel gövdenin yolu ne kadar geriden takip ettiği (sn). Momentum ve virajda yön gecikmesi hissi verir. 0 = yola yapışık.")]
        [Range(0f, 0.15f)]
        public float GlideFollowLag = 0.05f;

        [Tooltip("Pickup: küp ilk çekildiğinde çok kısa basılma (compression) süresi (sn).")]
        [Range(0.02f, 0.12f)]
        public float PickupCompressionDuration = 0.08f;

        [Tooltip("Pickup: küp çekildiğinde dikey basılma (0.035 = Y %3.5 basılır, X/Z %1.75 genişler).")]
        [Range(0.01f, 0.08f)]
        public float PickupSquash = 0.035f;

        [Tooltip("Pickup kalkış süresi (sn).")]
        [Range(0.06f, 0.20f)]
        public float PickupLiftDuration = 0.08f;

        [Tooltip("Pickup: ip küpü ilk çektiğinde küçük kalkış yüksekliği (dünya birimi).")]
        [Range(0f, 0.10f)]
        public float PickupLift = 0.03f;

        [Tooltip("Virajlarda yön gecikmesi (Direction Lag süresi, sn). Gövde yolun dönüşünü geriden takip eder.")]
        [Range(0.02f, 0.15f)]
        public float DirectionLag = 0.05f;

        [Tooltip("Mikro hareketlerin genel şiddeti: viraj yatması, hızlanma esnemesi, seyirde hafif nefes alma. 0 = tamamen düz kayma.")]
        [Range(0f, 1f)]
        public float GlideMicroMotion = 0.65f;

        [Tooltip("Sıralı zincir akışında küplerin panodan tek tek yola çıkış gecikmesi (sn). 0.10 - 0.18s arası hypercasual akış için idealdir.")]
        [Range(0.04f, 0.35f)]
        public float TrainStaggerInterval = 0.14f;

        // Geriye dönük uyumluluk aliasları
        public float GlidePickupLift { get => PickupLift; set => PickupLift = value; }
        public float GlidePickupSquash { get => PickupSquash; set => PickupSquash = value; }

        [Header("🚀 1. HAREKET (Movement)")]
        [Tooltip("Küpün maksimum seyir hızı (dünya birimi/sn).")]
        [Range(1.5f, 10f)]
        public float MoveSpeed = 4.4f;

        [Tooltip("Kalkışta seyir hızına ulaşma ivmesi (sn^-1 veya birim/sn^2).")]
        [Range(2f, 20f)]
        public float Acceleration = 10.0f;

        [Tooltip("Hedefe varışta yavaşlama ivmesi.")]
        [Range(2f, 20f)]
        public float Deceleration = 12.0f;

        [Tooltip("Dönüş hızı (derece / sn).")]
        [Range(90f, 720f)]
        public float TurnSpeed = 400f;

        [Tooltip("Rotasyon yumuşatma faktörü (0 = anlık, 1 = çok yumuşak).")]
        [Range(0.01f, 0.4f)]
        public float RotationSmoothness = 0.08f;

        [Tooltip("Kalkışta hız profili: X = hızlanma süresi oranı (0-1), Y = seyir hızı oranı (0-1). " +
                 "Hızlanma süresi = MoveSpeed / Acceleration.")]
        public AnimationCurve AccelerationCurve = new AnimationCurve(
            new Keyframe(0f, 0.15f, 0f, 1.6f), new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip("Varışta hız profili: X = hedefe kalan mesafe oranı (0 = hedefte, 1 = Arrival Distance), " +
                 "Y = ArrivalSlowdown ile seyir hızı arasındaki oran.")]
        public AnimationCurve DecelerationCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 0f));

        [Header("🛣️ 2. YOL (Path)")]
        [Tooltip("Yolun kavis genliği.")]
        [Range(0.1f, 2f)]
        public float PathCurvature = 1.0f;

        [Tooltip("Yol koridor genişliği / ofset ölçeği.")]
        [Range(0.05f, 0.5f)]
        public float PathWidth = 0.12f;

        [Tooltip("Yol eğrisi pürüzsüzleştirme faktörü.")]
        [Range(8, 32)]
        public int PathSmoothing = 16;

        [Tooltip("Yavaşlamanın başlayacağı hedefe kalan mesafe.")]
        [Range(0.1f, 1.5f)]
        public float ArrivalDistance = 0.50f;

        [Tooltip("Pixel-art konturundan ve canlı bloklardan bırakılacak fiziksel emniyet mesafesi (dünya birimi).")]
        [Range(0.15f, 1.2f)]
        public float ObstacleClearance = 0.38f;

        [Tooltip("Sahil ve yaklaşım koridorunun genişlik payı.")]
        [Range(0.05f, 0.4f)]
        public float CorridorClearance = 0.15f;

        [Header("💃 3. ANİMASYON & İKİNCİL HAREKET (Animation & Secondary Motion)")]
        [Tooltip("Yürüyüş sırasındaki dikey zıplama/sekme genliği (bob amount).")]
        [Range(0f, 0.12f)]
        public float BobAmount = 0f;

        [Tooltip("Yürüyüş sekme frekansı / hızı.")]
        [Range(4f, 24f)]
        public float BobSpeed = 14f;

        [Tooltip("Virajlara girerken gövdenin viraj yönüne yatma açısı (Bank Tilt, derece).")]
        [Range(1f, 10f)]
        public float TiltAmount = 2.4f;

        [Tooltip("Yatma açısının değişim yumuşaklığı.")]
        [Range(0.02f, 0.3f)]
        public float TiltSmoothness = 0.06f;

        [Tooltip("Kalkış ve inişlerdeki esneme/basılma genliği (Squash & Stretch).")]
        [Range(0f, 0.2f)]
        public float SquashAmount = 0.04f;

        [Tooltip("Squash-stretch geri toparlanma hızı.")]
        [Range(5f, 30f)]
        public float SquashSpeed = 16f;

        [Header("🏁 4. VARIŞ & YERLEŞME (Arrival & Settle)")]
        [Tooltip("Hedefe yaklaşırken hızın düşürüleceği minimum seyir oranı.")]
        [Range(0.1f, 0.7f)]
        public float ArrivalSlowdown = 0.45f;

        [Tooltip("Varış ease-out eğrisi yumuşaklığı.")]
        [Range(0.5f, 3f)]
        public float ArrivalEase = 1.6f;

        [Tooltip("Slota/araca varışta yaylanarak oturma süresi (sn).")]
        [Range(0.06f, 0.25f)]
        public float SettleDuration = 0.10f;

        [Tooltip("Varıştaki son yaylanma (bounce) yüksekliği.")]
        [Range(0f, 0.15f)]
        public float SettleBounce = 0.02f;

        [Tooltip("Gemi girişinden güverteye kayarak binme süresi (sn).")]
        [Range(0.12f, 0.6f)]
        public float BoardingDuration = 0.22f;

        [Tooltip("Güverteye binerken çizilen yayın yüksekliği (dünya birimi). 0 = zeminde kayarak biner (havadan gelme hissi yok).")]
        [Range(0f, 0.4f)]
        public float BoardingArcHeight = 0.02f;

        [Tooltip("Hareket ederken küpün karttan/zeminden kalkma yüksekliği (küp boyu cinsinden). " +
                 "0 = zeminde yürür. Arttıkça küpler havadan geliyormuş gibi görünür.")]
        [Range(0f, 0.6f)]
        public float LiftHeight = 0f;

        [Header("🎲 5. VARYASYON (Multi-Cube Variation)")]
        [Tooltip("Küpler arası seyir hızı rastgele varyasyonu (+/-).")]
        [Range(0f, 0.25f)]
        public float SpeedVariation = 0.08f;

        [Tooltip("Yürüyüş sekme fazı varyasyonu.")]
        [Range(0f, 1f)]
        public float BobVariation = 0.35f;

        [Tooltip("Arka arkaya çıkan küpler arasındaki başlama gecikmesi varyasyonu (sn).")]
        [Range(0.01f, 0.08f)]
        public float StartDelayVariation = 0.035f;

        [Tooltip("Hedefe varıştaki hafif zamanlama ofseti (sn).")]
        [Range(0f, 0.08f)]
        public float ArrivalVariation = 0.025f;

        [Header("⏱️ 6. ANTICIPATION (Hazırlık Hareketi)")]
        [Tooltip("Harekete başlamadan önceki hafif geriye esneme / compression süresi (sn).")]
        [Range(0.03f, 0.15f)]
        public float AnticipationDuration = 0.08f;

        [Tooltip("Anticipation sırasındaki geri çekilme/basılma miktarı.")]
        [Range(0.01f, 0.08f)]
        public float AnticipationRecoil = 0.035f;
    }
}
