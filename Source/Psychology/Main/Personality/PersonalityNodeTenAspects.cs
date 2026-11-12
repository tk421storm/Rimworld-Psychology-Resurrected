using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace Psychology
{
    // XML entry in PersonalityNodeDef.tenAspects; see PersonalityNodeMatrix for how the weights become correlations.
    public class PersonalityNodeTenAspects
    {
        // The label of the 10 Aspects: "Openness", "Intellect", "Industriousness", "Orderliness",
        // "Enthusiasm", "Assertiveness", "Compassion", "Politeness", "Volatility", "Withdrawal"
        public string aspect;
        // The weight of the PersonalityNodeDef on the aspect
        public float weight;
    }
}
