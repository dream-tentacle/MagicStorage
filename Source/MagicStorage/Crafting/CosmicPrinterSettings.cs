using System;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public sealed class CosmicPrinterSettings : IExposable
    {
        public float WorkSpeed = 1f, Efficiency = 1f;
        public int SkillLevel = 12, QualityOffset;
        public float InspirationChance = .3f, FailureChance = .2f;

        public void Reset()
        { WorkSpeed = Efficiency = 1f; SkillLevel = 12; QualityOffset = 0; InspirationChance = .3f; FailureChance = .2f; }

        internal CosmicPrinterSettings Copy() => (CosmicPrinterSettings)MemberwiseClone();

        internal bool TryApply(CosmicPrinterSettings proposed, bool allowChanges)
        {
            if (!allowChanges || proposed == null) return false;
            var values = proposed.Copy();
            values.Normalize();
            WorkSpeed = values.WorkSpeed;
            Efficiency = values.Efficiency;
            SkillLevel = values.SkillLevel;
            QualityOffset = values.QualityOffset;
            InspirationChance = values.InspirationChance;
            FailureChance = values.FailureChance;
            return true;
        }

        public void Normalize()
        {
            WorkSpeed = FiniteClamp(WorkSpeed, 0f, 100000f, 1f);
            Efficiency = FiniteClamp(Efficiency, 0f, 1000f, 1f);
            SkillLevel = Math.Max(0, Math.Min(20, SkillLevel));
            QualityOffset = Math.Max(-6, Math.Min(6, QualityOffset));
            InspirationChance = FiniteClamp(InspirationChance, 0f, 1f, .3f);
            FailureChance = FiniteClamp(FailureChance, 0f, 1f, .2f);
        }

        private static float FiniteClamp(float value, float min, float max, float fallback)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value));

        internal int ApplyQuality(int quality, bool inspired, bool failed) =>
            Math.Max(0, Math.Min(6, quality + QualityOffset + (inspired ? 1 : 0) - (failed ? 1 : 0)));

        internal QualityCategory GenerateQuality()
        {
            // These are two independent draws, even at the minimum/maximum quality.
            int quality = (int)QualityUtility.GenerateQualityCreatedByPawn(SkillLevel, false);
            bool inspired = Rand.Value < InspirationChance;
            bool failed = Rand.Value < FailureChance;
            return (QualityCategory)ApplyQuality(quality, inspired, failed);
        }

        // Integrate vanilla's asymmetric Gaussian and its 50% masterwork reroll.
        // No game random numbers are consumed when opening or drawing the preview.
        internal double[] QualityProbabilities()
        {
            double[] centers = { .7, 1.1, 1.5, 1.8, 2, 2.2, 2.4, 2.6, 2.8, 2.95,
                3.1, 3.25, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9, 4, 4.1, 4.2 };
            double center = centers[Math.Max(0, Math.Min(20, SkillLevel))];
            double[] first = Distribution(center, .8), second = Distribution(center, .95);
            double reroll = first[5] * .5;
            first[5] -= reroll;
            var result = new double[7];
            for (int q = 0; q < 6; q++)
            {
                double probability = first[q] + reroll * second[q];
                for (int up = 0; up <= 1; up++)
                    for (int down = 0; down <= 1; down++)
                        result[ApplyQuality(q, up == 1, down == 1)] += probability *
                            (up == 1 ? InspirationChance : 1 - InspirationChance) *
                            (down == 1 ? FailureChance : 1 - FailureChance);
            }
            return result;
        }

        private static double[] Distribution(double center, double upperWidth)
        {
            var result = new double[6];
            double previous = 0;
            for (int q = 0; q < 5; q++)
            {
                double x = q + 1 - center;
                double cumulative = NormalCdf(x / (x <= 0 ? .6 : upperWidth));
                result[q] = cumulative - previous;
                previous = cumulative;
            }
            result[5] = 1 - previous;
            return result;
        }

        private static double NormalCdf(double x)
        {
            double a = Math.Abs(x), t = 1 / (1 + .2316419 * a);
            double tail = .3989422804014327 * Math.Exp(-a * a / 2) * t *
                (.319381530 + t * (-.356563782 + t * (1.781477937 + t * (-1.821255978 + t * 1.330274429))));
            return x < 0 ? tail : 1 - tail;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref WorkSpeed, "workSpeed", 1f);
            Scribe_Values.Look(ref Efficiency, "efficiency", 1f);
            Scribe_Values.Look(ref SkillLevel, "skillLevel", 12);
            Scribe_Values.Look(ref QualityOffset, "qualityOffset", 0);
            Scribe_Values.Look(ref InspirationChance, "inspirationChance", .3f);
            Scribe_Values.Look(ref FailureChance, "failureChance", .2f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Normalize();
        }
    }
}
