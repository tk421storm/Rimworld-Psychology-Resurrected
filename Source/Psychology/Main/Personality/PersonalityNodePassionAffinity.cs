using RimWorld;

namespace Psychology
{
    // XML entry in PersonalityNodeDef.passionAffinities: how far this node leans a growth moment's passion options toward
    // (positive) or away from (negative) a skill. See GrowthMomentWeights.PassionWeight.
    public class PersonalityNodePassionAffinity
    {
        public SkillDef skill;
        public float affinity;
    }
}
