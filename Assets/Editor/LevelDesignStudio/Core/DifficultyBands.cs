namespace PixelGame.Editor.Studio
{
    public enum BandVerdict { NoData, InBand, TooEasy, TooHard, Mixed, Unsolvable }

    /// <summary>
    /// Zorluk başına hedef kazanma oranları. docs/level-tasarim/tools/design.py içindeki K, O ve S bantlarıyla aynıdır;
    /// biri değişirse öteki de değişmeli.
    /// </summary>
    public static class DifficultyBands
    {
        // Simülatör sonucu rastgelelik taşır, sınırda küçük sapmalar bant dışı sayılmaz
        private const float Tolerance = 0.02f;

        public static readonly string[] TierNames = { "Kolay", "Orta", "Zor" };

        public static void Get(int tier, out float randomLow, out float randomHigh, out float smartLow, out float smartHigh)
        {
            switch (tier)
            {
                case 0: randomLow = 0.55f; randomHigh = 0.80f; smartLow = 0.98f; smartHigh = 1f; break;
                case 1: randomLow = 0.30f; randomHigh = 0.55f; smartLow = 0.90f; smartHigh = 1f; break;
                default: randomLow = 0.12f; randomHigh = 0.30f; smartLow = 0.80f; smartHigh = 0.95f; break;
            }
        }

        public static BandVerdict Judge(int tier, float random, float smart, int solvable)
        {
            if (solvable == 0) return BandVerdict.Unsolvable;
            Get(tier, out float rl, out float rh, out float sl, out float sh);
            bool easy = random > rh + Tolerance || smart > sh + Tolerance;
            bool hard = random < rl - Tolerance || smart < sl - Tolerance;
            if (easy && hard) return BandVerdict.Mixed;
            if (easy) return BandVerdict.TooEasy;
            return hard ? BandVerdict.TooHard : BandVerdict.InBand;
        }

        public static string VerdictText(BandVerdict v)
        {
            switch (v)
            {
                case BandVerdict.InBand: return "Bantta";
                case BandVerdict.TooEasy: return "Çok kolay";
                case BandVerdict.TooHard: return "Çok zor";
                case BandVerdict.Mixed: return "Tutarsız";
                case BandVerdict.Unsolvable: return "Çözümsüz";
                default: return "Ölçülmedi";
            }
        }
    }
}
