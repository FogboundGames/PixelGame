using UnityEngine;

namespace PixelGame
{
    /// <summary>Trende koşan kargo küplerinin ortak arayüzü (Mixamo koşucu, kodla paytak yürüyen vb.).</summary>
    public interface ICargoRunner
    {
        /// <summary>
        /// Yürüyüş başlarken çağrılır: o anki duruş taban kabul edilir. Trendeki sırası
        /// verilir ki yan yana küpler ters adımla yürüsün (tırtıl gibi, asker gibi değil).
        /// </summary>
        void BeginWalk(int indexInRope);

        /// <summary>Ekrandaki hareket yönüne doğru yumuşakça döner.</summary>
        void TurnToward(Vector3 screenMove, float deltaTime);
    }

    /// <summary>
    /// Koşucuların gittiği yöne "kafa çevirir gibi" dönmesi. Dönüş ekranın dikey ekseni
    /// etrafındadır: küpün alt kenarı hep yatay kalır, sadece yan yüzü görünür; bacaklar da o
    /// kenarın altında düzgün durur. (Eğik duruşun kendi ekseni etrafında dönünce -60°'de küp
    /// ekranda baklava gibi dönüyor, dik sarkan bacaklar bir kenara çapraz bağlı görünüyordu.)
    /// </summary>
    public static class CargoRunnerHeading
    {
        /// <summary>
        /// Hedef dönüş açısı: sağa giderken sağa, sola giderken sola döner; aşağı/yukarı giderken
        /// düz bakar. Açı sınırlıdır, yoksa eğik küpün altı görünmeye başlar.
        /// </summary>
        public static float TargetYaw(Vector3 screenMove, float maxDegrees)
        {
            float len = Mathf.Sqrt(screenMove.x * screenMove.x + screenMove.y * screenMove.y);
            if (len < 1e-4f) return 0f;
            // Gövde kameraya (-Z) bakıyor; Y ekseninde eksi açı yüzü +X'e (sağa) çevirir.
            return -(screenMove.x / len) * maxDegrees;
        }

        /// <summary>Taban (eğik) duruşu dünyanın dikey ekseni etrafında çevirir.</summary>
        public static Quaternion Apply(Quaternion baseRotation, float yaw)
        {
            return Quaternion.AngleAxis(yaw, Vector3.up) * baseRotation;
        }
    }
}
