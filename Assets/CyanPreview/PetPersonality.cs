using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    // Immutable snapshot of the installed game's ID-seeded Personality constructor.
    public sealed class PetPersonality
    {
        public readonly float Energy, Bravery, Sympathy, Dominance, Nervous, Aggression;
        internal PetPersonality(object value)
        {
            Energy = (float)Get(value, "energy"); Bravery = (float)Get(value, "bravery");
            Sympathy = (float)Get(value, "sympathy"); Dominance = (float)Get(value, "dominance");
            Nervous = (float)Get(value, "nervous"); Aggression = (float)Get(value, "aggression");
        }
        public override string ToString() => $"Energy {Energy:0.00} / Bravery {Bravery:0.00} / Sympathy {Sympathy:0.00}\nDominance {Dominance:0.00} / Nervous {Nervous:0.00} / Aggression {Aggression:0.00}";
    }
}
